<?php
/**
 * Warehouse product sitemap for epartscart CHPU pages.
 *
 *   /sitemap-products.php           → hubs + brand browse pages
 *   /sitemap-products.php?shard=0   → in-stock brand/article pages (shard 0)
 *
 * Google discovers shards via /sitemap-index.php.
 */
declare(strict_types=1);

@set_time_limit(180);
@ini_set('memory_limit', '512M');

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_seo_indexing.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';

$cfg = new DP_Config();
$lang = epc_sitemap_lang();
$parts_root = epc_sitemap_parts_root($cfg);
$slash_code = epc_sitemap_slash_code($cfg);
$lastmod = date('Y-m-d');
$entries = array();
$seen = array();

header('Content-Type: application/xml; charset=utf-8');

$pdo = epc_sitemap_pdo($cfg);
if ($pdo === null) {
	epc_sitemap_emit_urlset(array());
	exit;
}

$priceClause = epc_seo_sitemap_price_clause($pdo);
$storefrontPriceFilter = '';
if (function_exists('epc_ssf_price_data_active_sql')) {
	require_once __DIR__ . '/content/shop/docpart/epc_storefront_storage_flags.php';
	epc_ssf_ensure_schema($pdo);
	$storefrontPriceFilter = ' AND ' . epc_ssf_price_data_active_sql('d');
}

$shardSize = 5000;
$shardParam = isset($_GET['shard']) ? (int) $_GET['shard'] : null;
$isProductShard = $shardParam !== null && $shardParam >= 0;

try {
	if (!$isProductShard) {
		// Hubs + brand pages (fast). Product SKUs live in ?shard=N maps.
		epc_sitemap_add_entry(
			$entries,
			$seen,
			epc_sitemap_parts_path($cfg, $lang, $parts_root, $slash_code, ''),
			$lastmod,
			'daily',
			'1.0'
		);

		if (epc_seo_warehouse_indexing_enabled($pdo)) {
			epc_sitemap_add_entry(
				$entries,
				$seen,
				epc_sitemap_page_url($cfg, $lang, 'available-brands'),
				$lastmod,
				'weekly',
				'0.8'
			);
		}

		$brand_stmt = $pdo->query(
			'SELECT DISTINCT TRIM(d.`manufacturer`) AS manufacturer
			FROM `shop_docpart_prices_data` d
			WHERE TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\'
			AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter . '
			ORDER BY manufacturer ASC
			LIMIT 5000'
		);
		while ($brand = $brand_stmt->fetch(PDO::FETCH_ASSOC)) {
			$mfr = trim((string) $brand['manufacturer']);
			if ($mfr === '') {
				continue;
			}
			epc_sitemap_add_entry(
				$entries,
				$seen,
				epc_sitemap_parts_path($cfg, $lang, $parts_root, $slash_code, epc_sitemap_segment($mfr, $slash_code)),
				$lastmod,
				'weekly',
				'0.7'
			);
		}
	} else {
		// Canonical product pages: /en/parts/{BRAND}/{NORMALIZED_ARTICLE}
		$offset = $shardParam * $shardSize;
		$product_stmt = $pdo->prepare(
			'SELECT TRIM(d.`manufacturer`) AS manufacturer,
				TRIM(d.`article`) AS article,
				COALESCE(NULLIF(TRIM(d.`article_show`), \'\'), TRIM(d.`article`)) AS article_show
			FROM `shop_docpart_prices_data` d
			WHERE TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\'
			AND TRIM(IFNULL(d.`article`, \'\')) != \'\'
			AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter . '
			GROUP BY TRIM(d.`manufacturer`), TRIM(d.`article`)
			ORDER BY TRIM(d.`manufacturer`) ASC, TRIM(d.`article`) ASC
			LIMIT ' . (int) $shardSize . ' OFFSET ' . (int) $offset
		);
		$product_stmt->execute();
		while ($row = $product_stmt->fetch(PDO::FETCH_ASSOC)) {
			$mfr = trim((string) $row['manufacturer']);
			$artRaw = trim((string) ($row['article_show'] ?? $row['article'] ?? ''));
			$artNorm = docpart_normalize_article_for_price($artRaw !== '' ? $artRaw : (string) $row['article']);
			if ($mfr === '' || $artNorm === '') {
				continue;
			}
			$path = epc_sitemap_segment($mfr, $slash_code) . '/'
				. epc_sitemap_segment($artNorm, $slash_code);
			epc_sitemap_add_entry(
				$entries,
				$seen,
				epc_sitemap_parts_path($cfg, $lang, $parts_root, $slash_code, $path),
				$lastmod,
				'weekly',
				'0.6'
			);
		}
	}
} catch (Throwable $e) {
	// Keep a valid (possibly partial) sitemap if DB is slow/unavailable.
}

epc_sitemap_emit_urlset($entries);
