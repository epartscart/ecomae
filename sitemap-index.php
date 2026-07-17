<?php
declare(strict_types=1);

@set_time_limit(120);

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_seo_indexing.php';

$cfg = new DP_Config();
$base = rtrim(epc_sitemap_base_url($cfg), '/');
$lastmod = date('Y-m-d');

header('Content-Type: application/xml; charset=utf-8');

echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
echo "<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";

$childMaps = array();

if (epc_seo_is_ecomae_marketing_host()) {
	$childMaps = array(
		'sitemap-marketing.php',
		'sitemap.xml',
		'sitemap-pages.php',
	);
} else {
	// Tenant / warehouse (epartscart): pages + brand hub map + sharded product SKUs.
	$childMaps[] = 'sitemap-pages.php';
	$childMaps[] = 'sitemap-products.php';

	$pdo = epc_sitemap_pdo($cfg);
	$shardSize = 5000;
	$productCount = 0;
	if ($pdo instanceof PDO) {
		try {
			$priceClause = epc_seo_sitemap_price_clause($pdo);
			$storefrontPriceFilter = '';
			if (function_exists('epc_ssf_price_data_active_sql')) {
				require_once __DIR__ . '/content/shop/docpart/epc_storefront_storage_flags.php';
				epc_ssf_ensure_schema($pdo);
				$storefrontPriceFilter = ' AND ' . epc_ssf_price_data_active_sql('d');
			}
			$productCount = (int) $pdo->query(
				'SELECT COUNT(*) FROM (
					SELECT 1
					FROM `shop_docpart_prices_data` d
					WHERE TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\'
					AND TRIM(IFNULL(d.`article`, \'\')) != \'\'
					AND IFNULL(d.`exist`, 0) > 0' . $priceClause . $storefrontPriceFilter . '
					GROUP BY TRIM(d.`manufacturer`), TRIM(d.`article`)
				) t'
			)->fetchColumn();
		} catch (Throwable $e) {
			$productCount = 0;
		}
	}
	$shards = $productCount > 0 ? (int) ceil($productCount / $shardSize) : 0;
	// Cap shards to stay within Google's 50k sitemap / reasonable index size.
	if ($shards > 40) {
		$shards = 40;
	}
	for ($i = 0; $i < $shards; $i++) {
		$childMaps[] = 'sitemap-products.php?shard=' . $i;
	}
}

foreach ($childMaps as $file) {
	echo "\t<sitemap>\n";
	echo "\t\t<loc>" . htmlspecialchars($base . '/' . $file, ENT_XML1, 'UTF-8') . "</loc>\n";
	echo "\t\t<lastmod>" . $lastmod . "</lastmod>\n";
	echo "\t</sitemap>\n";
}

echo "</sitemapindex>\n";
