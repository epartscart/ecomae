<?php
/**
 * All industry hubs + every sub-industry path for Google.
 *
 * Submit this in Search Console (Domain property for ecomae.com):
 *   https://www.ecomae.com/sitemap-industries.php
 *
 * Per-hub maps (energy-only, jewellery-only, …) stay on each subdomain:
 *   https://energy.ecomae.com/sitemap.xml
 */
declare(strict_types=1);

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_sitemap_lib.php';
require_once __DIR__ . '/content/general_pages/epc_industry_seo.php';
require_once __DIR__ . '/content/general_pages/epc_industry_consolidation.php';

$lastmod = date('Y-m-d');
$entries = array();
$seen = array();

header('Content-Type: application/xml; charset=utf-8');
header('X-Robots-Tag: noindex'); // the sitemap itself; listed URLs remain indexable

foreach (epc_industry_seo_sitemap_entries() as $ind) {
	epc_sitemap_add_entry(
		$entries,
		$seen,
		htmlspecialchars((string) $ind[0], ENT_XML1, 'UTF-8'),
		$lastmod,
		(string) $ind[2],
		(string) $ind[1]
	);
}

epc_sitemap_emit_urlset($entries);
