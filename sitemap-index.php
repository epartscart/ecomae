<?php
declare(strict_types=1);

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
	$childMaps = array('sitemap-marketing.php', 'sitemap.xml', 'sitemap-pages.php');
	$base = epc_sitemap_base_url($cfg);
} else {
	$childMaps = array('sitemap-pages.php', 'sitemap-products.php');
	$base = epc_sitemap_base_url($cfg);
}

foreach ($childMaps as $file) {
	echo "\t<sitemap>\n";
	echo "\t\t<loc>" . htmlspecialchars(rtrim($base, '/') . '/' . ltrim($file, '/'), ENT_XML1, 'UTF-8') . "</loc>\n";
	echo "\t\t<lastmod>" . $lastmod . "</lastmod>\n";
	echo "\t</sitemap>\n";
}

echo "</sitemapindex>\n";
