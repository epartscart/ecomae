<?php
declare(strict_types=1);

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_seo_indexing.php';

$cfg = new DP_Config();
$base = epc_sitemap_base_url($cfg);
$lastmod = date('Y-m-d');

header('Content-Type: application/xml; charset=utf-8');

echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
echo "<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n";

$childMaps = array('sitemap-pages.php');
if (epc_seo_is_ecomae_marketing_host()) {
	$childMaps = array('sitemap-marketing.php', 'sitemap-pages.php');
} else {
	$childMaps[] = 'sitemap-products.php';
}

foreach ($childMaps as $file) {
	echo "\t<sitemap>\n";
	echo "\t\t<loc>" . htmlspecialchars($base . '/' . $file, ENT_XML1, 'UTF-8') . "</loc>\n";
	echo "\t\t<lastmod>" . $lastmod . "</lastmod>\n";
	echo "\t</sitemap>\n";
}

echo "</sitemapindex>\n";
