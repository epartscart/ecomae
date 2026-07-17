<?php
declare(strict_types=1);

@set_time_limit(120);

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_seo_indexing.php';

$cfg = new DP_Config();
$lastmod = date('Y-m-d');

header('Content-Type: application/xml; charset=utf-8');

echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
echo "<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";

/**
 * Industry hubs (energy.ecomae.com, jewellery.ecomae.com, …) must advertise their
 * own /sitemap.xml — that file lists hub + every sub-industry (e.g. biomass-bioenergy).
 * Previously these hosts fell through to www.ecomae.com product sitemaps and Google
 * never discovered sub-industry URLs from robots.txt → sitemap-index.php.
 */
$requestBase = epc_sitemap_request_host_base($cfg);
$isIndustryHost = epc_sitemap_is_industry_host();

if ($isIndustryHost) {
	$childMaps = array('sitemap.xml');
	$base = $requestBase;
} elseif (epc_seo_is_ecomae_marketing_host()) {
	// ALL industry hubs + every sub-industry: submit sitemap-industries.php in GSC.
	// Per-hub maps (energy only, etc.) remain on each subdomain's /sitemap.xml.
	$childMaps = array(
		'sitemap-industries.php',
		'sitemap-marketing.php',
		'sitemap.xml',
		'sitemap-pages.php',
	);
	$base = epc_sitemap_base_url($cfg);
} else {
	// Tenant / warehouse (epartscart): pages + brand hub map + sharded product SKUs.
	$childMaps = array(
		'sitemap-pages.php',
		'sitemap-products.php',
	);
	$base = epc_sitemap_base_url($cfg);

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

$base = rtrim((string) $base, '/');

foreach ($childMaps as $file) {
	echo "\t<sitemap>\n";
	echo "\t\t<loc>" . htmlspecialchars($base . '/' . ltrim((string) $file, '/'), ENT_XML1, 'UTF-8') . "</loc>\n";
	echo "\t\t<lastmod>" . $lastmod . "</lastmod>\n";
	echo "\t</sitemap>\n";
}

echo "</sitemapindex>\n";
