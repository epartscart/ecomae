<?php
declare(strict_types=1);

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_seo_indexing.php';

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

// Parts hub — primary entry for product discovery.
epc_sitemap_add_entry(
	$entries,
	$seen,
	epc_sitemap_parts_path($cfg, $lang, $parts_root, $slash_code, ''),
	$lastmod,
	'daily',
	'1.0'
);

if (epc_seo_warehouse_indexing_enabled($pdo)) {
	foreach (array('available-brands', 'spare-parts') as $catalogPage) {
		epc_sitemap_add_entry(
			$entries,
			$seen,
			epc_sitemap_page_url($cfg, $lang, $catalogPage),
			$lastmod,
			'weekly',
			'0.8'
		);
	}
}

try {
	// Brand browse pages — only manufacturers with in-stock SKUs.
	$brand_stmt = $pdo->query(
		'SELECT DISTINCT TRIM(d.`manufacturer`) AS manufacturer
		FROM `shop_docpart_prices_data` d
		WHERE TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\'
		AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter . '
		ORDER BY manufacturer ASC
		LIMIT 2500'
	);
	while ($brand = $brand_stmt->fetch(PDO::FETCH_ASSOC)) {
		epc_sitemap_add_entry(
			$entries,
			$seen,
			epc_sitemap_parts_path($cfg, $lang, $parts_root, $slash_code, epc_sitemap_segment($brand['manufacturer'], $slash_code)),
			$lastmod,
			'weekly',
			'0.7'
		);
	}

	// Canonical product pages: /parts/{BRAND}/{ARTICLE} — in stock only.
	$product_stmt = $pdo->query(
		'SELECT DISTINCT TRIM(d.`manufacturer`) AS manufacturer,
			COALESCE(NULLIF(TRIM(d.`article_show`), \'\'), TRIM(d.`article`)) AS article
		FROM `shop_docpart_prices_data` d
		WHERE TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\'
		AND TRIM(IFNULL(d.`article`, \'\')) != \'\'
		AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter . '
		ORDER BY d.`manufacturer` ASC, article ASC
		LIMIT 12000'
	);
	while ($row = $product_stmt->fetch(PDO::FETCH_ASSOC)) {
		$path = epc_sitemap_segment($row['manufacturer'], $slash_code) . '/'
			. epc_sitemap_segment($row['article'], $slash_code);
		epc_sitemap_add_entry(
			$entries,
			$seen,
			epc_sitemap_parts_path($cfg, $lang, $parts_root, $slash_code, $path),
			$lastmod,
			'weekly',
			'0.6'
		);
	}
} catch (Exception $e) {
	// Keep a valid sitemap if the DB is temporarily unavailable.
}

epc_sitemap_emit_urlset($entries);
