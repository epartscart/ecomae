<?php
/**
 * Warehouse product sitemap for epartscart CHPU pages.
 *
 * URL format (same as live warehouse results / canonical):
 *   https://www.epartscart.com/en/parts/{BRAND}/{ARTICLE}
 * Example:
 *   https://www.epartscart.com/en/parts/GMB/GUT21
 *
 *   /sitemap-products.php              → hubs + brand browse pages
 *   /sitemap-products.php?brand=BOSCH  → all in-stock articles for that brand
 *   /sitemap-products.php?shard=0      → legacy numeric shard (brand/article pairs)
 *
 * Google discovers brand (and shard) children via /sitemap-index.php.
 * Do NOT emit /en/parts/brands/{article} — that path 302s and is Disallow in robots.txt.
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
$debugError = '';

header('Content-Type: application/xml; charset=utf-8');

$pdo = epc_sitemap_pdo($cfg);
if ($pdo === null) {
	epc_sitemap_emit_urlset(array());
	exit;
}

$priceClause = epc_seo_sitemap_price_clause($pdo);
// Qualify price for aliased queries (d.price).
if ($priceClause !== '' && strpos($priceClause, 'd.`price`') === false) {
	$priceClause = str_replace('`price`', 'd.`price`', $priceClause);
}
$storefrontPriceFilter = '';
if (function_exists('epc_ssf_price_data_active_sql')) {
	require_once __DIR__ . '/content/shop/docpart/epc_storefront_storage_flags.php';
	epc_ssf_ensure_schema($pdo);
	$storefrontPriceFilter = ' AND ' . epc_ssf_price_data_active_sql('d');
}

$brandParam = isset($_GET['brand']) ? trim((string) $_GET['brand']) : '';
$shardParam = isset($_GET['shard']) ? (int) $_GET['shard'] : null;
$isBrandMap = $brandParam !== '';
$isProductShard = !$isBrandMap && $shardParam !== null && $shardParam >= 0;
$shardSize = 5000;

/**
 * @param array<int, array<string, mixed>> $rows
 */
function epc_sitemap_products_add_pair_rows(
	array $rows,
	DP_Config $cfg,
	string $lang,
	string $lastmod,
	array &$entries,
	array &$seen
): void {
	foreach ($rows as $row) {
		$mfr = trim((string) ($row['manufacturer'] ?? ''));
		$artRaw = trim((string) ($row['article'] ?? ''));
		if ($mfr === '' || $artRaw === '') {
			continue;
		}
		$loc = epc_sitemap_part_loc($cfg, $lang, $mfr, $artRaw);
		if ($loc === '' || strpos($loc, '/parts/brands/') !== false) {
			continue;
		}
		epc_sitemap_add_entry(
			$entries,
			$seen,
			$loc,
			$lastmod,
			'weekly',
			'0.6'
		);
	}
}

try {
	if (!$isBrandMap && !$isProductShard) {
		// Hubs + brand pages (fast). Product SKUs live in ?brand= / ?shard= maps.
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
			// Brand browse: /en/parts/{BRAND} (uppercase to match CHPU)
			$mfrUpper = function_exists('mb_strtoupper')
				? mb_strtoupper($mfr, 'UTF-8')
				: strtoupper($mfr);
			epc_sitemap_add_entry(
				$entries,
				$seen,
				epc_sitemap_parts_path($cfg, $lang, $parts_root, $slash_code, epc_sitemap_segment($mfrUpper, $slash_code)),
				$lastmod,
				'weekly',
				'0.7'
			);
		}
	} elseif ($isBrandMap) {
		// Canonical product pages: /en/parts/{BRAND}/{ARTICLE}
		// Distinct article only — avoids ONLY_FULL_GROUP_BY failures that emptied ?shard= maps.
		$stmt = $pdo->prepare(
			'SELECT DISTINCT TRIM(d.`article`) AS article
			FROM `shop_docpart_prices_data` d
			WHERE TRIM(d.`manufacturer`) = ?
			AND TRIM(IFNULL(d.`article`, \'\')) != \'\'
			AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter . '
			ORDER BY article ASC
			LIMIT 45000'
		);
		$stmt->execute(array($brandParam));
		$rows = array();
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$rows[] = array(
				'manufacturer' => $brandParam,
				'article' => (string) ($row['article'] ?? ''),
			);
		}
		epc_sitemap_products_add_pair_rows(
			$rows,
			$cfg,
			$lang,
			$lastmod,
			$entries,
			$seen
		);
	} else {
		// Legacy numeric shards — manufacturer+article only (no article_show in SELECT).
		$offset = $shardParam * $shardSize;
		$sql = 'SELECT manufacturer, article FROM (
				SELECT TRIM(d.`manufacturer`) AS manufacturer,
					TRIM(d.`article`) AS article
				FROM `shop_docpart_prices_data` d
				WHERE TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\'
				AND TRIM(IFNULL(d.`article`, \'\')) != \'\'
				AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter . '
				GROUP BY TRIM(d.`manufacturer`), TRIM(d.`article`)
			) t
			ORDER BY manufacturer ASC, article ASC
			LIMIT ' . (int) $shardSize . ' OFFSET ' . (int) $offset;
		$product_stmt = $pdo->query($sql);
		$rows = array();
		while ($row = $product_stmt->fetch(PDO::FETCH_ASSOC)) {
			$rows[] = $row;
		}
		epc_sitemap_products_add_pair_rows(
			$rows,
			$cfg,
			$lang,
			$lastmod,
			$entries,
			$seen
		);
	}
} catch (Throwable $e) {
	$debugError = $e->getMessage();
	// Keep a valid (possibly empty) sitemap if DB is slow/unavailable.
}

// Optional diagnostics for deploy token (does not affect Google).
$token = (string) ($_GET['token'] ?? '');
if ($token !== '' && $debugError !== '' && is_file(__DIR__ . '/epc_deploy_auth.php')) {
	require_once __DIR__ . '/epc_deploy_auth.php';
	if (function_exists('epc_deploy_token') && hash_equals(epc_deploy_token(), $token)) {
		echo '<!-- sitemap-products error: ' . htmlspecialchars($debugError, ENT_XML1, 'UTF-8') . " -->\n";
	}
}

epc_sitemap_emit_urlset($entries);
