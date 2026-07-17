<?php
declare(strict_types=1);

@set_time_limit(120);

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_seo_indexing.php';
require_once __DIR__ . '/content/general_pages/epc_sitemap_warehouse.php';

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
	$childMaps = array(
		'sitemap-industries.php',
		'sitemap-marketing.php',
		'sitemap.xml',
		'sitemap-pages.php',
	);
	$base = epc_sitemap_base_url($cfg);
} else {
	// Tenant / warehouse (epartscart):
	// pages + brand hub + cached warehouse shards (/en/parts/{BRAND}/{ARTICLE}).
	// Use ~27 path-based sitemap-warehouse-N.xml children (not 1000+ ?brand= maps)
	// so Google can finish crawling and reach ~133k SKUs.
	$childMaps = array(
		'sitemap-pages.php',
		'sitemap-products.php',
	);
	$base = epc_sitemap_base_url($cfg);

	// IMPORTANT: use static path locs only — sitemap-warehouse-N.xml
	// GSC successfully read those (5,000 URLs). Query-string children
	// (sitemap-warehouse.php?n=N) come back as "Couldn't fetch" after resubmit
	// (Google often requests ? as %3F → 404).
	$shards = epc_sitemap_warehouse_existing_shard_count();
	if ($shards <= 0) {
		$meta = epc_sitemap_warehouse_meta_read();
		$shards = (int) ($meta['shards'] ?? 0);
	}
	if ($shards > epc_sitemap_warehouse_max_shards()) {
		$shards = epc_sitemap_warehouse_max_shards();
	}
	for ($i = 0; $i < $shards; $i++) {
		$childMaps[] = 'sitemap-warehouse-' . $i . '.xml';
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
