<?php
declare(strict_types=1);

require_once __DIR__ . '/epc_sitemap_lib.php';

$cfg = new DP_Config();
$base = rtrim(epc_sitemap_base_url($cfg), '/');
$lastmod = date('Y-m-d');
$entries = array();
$seen = array();

header('Content-Type: application/xml; charset=utf-8');

$paths = array(
	'/' => array('daily', '1.0'),
	'/platform' => array('weekly', '0.9'),
	'/platform/industries' => array('weekly', '0.9'),
	'/platform/capabilities' => array('monthly', '0.8'),
	'/platform/auto-price-ai' => array('monthly', '0.8'),
	'/platform/pricing' => array('monthly', '0.8'),
	'/platform/demo' => array('monthly', '0.8'),
	'/platform/faq' => array('monthly', '0.8'),
	'/platform/contact' => array('monthly', '0.8'),
	'/platform/about' => array('monthly', '0.7'),
	'/platform/customer-results' => array('monthly', '0.7'),
	'/platform/platform-guides' => array('monthly', '0.7'),
	'/platform/business-continuity' => array('monthly', '0.6'),
	'/platform/api-documentation' => array('monthly', '0.7'),
	'/platform/api-services' => array('monthly', '0.7'),
);

foreach ($paths as $path => $meta) {
	epc_sitemap_add_entry(
		$entries,
		$seen,
		htmlspecialchars($base . $path, ENT_XML1, 'UTF-8'),
		$lastmod,
		$meta[0],
		$meta[1]
	);
}

epc_sitemap_emit_urlset($entries);
