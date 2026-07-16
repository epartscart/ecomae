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

// SEO content hubs + their entries (documentation, comparisons, BOS articles,
// solution pages, industry verticals). Guarded so the sitemap never breaks.
$paths['/documentation'] = array('weekly', '0.7');
$paths['/compare'] = array('weekly', '0.7');
$paths['/bos'] = array('weekly', '0.7');
$paths['/solutions'] = array('weekly', '0.7');
$paths['/legal'] = array('monthly', '0.6');
$paths['/privacy'] = array('monthly', '0.5');
$paths['/terms'] = array('monthly', '0.5');
$paths['/blockchain'] = array('weekly', '0.9');

try {
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__), '/');
	$mc = $docRoot . '/content/general_pages/epc_ecomae_marketing_content.php';
	$pd = $docRoot . '/content/general_pages/epc_ecomae_platform_data.php';
	$lc = $docRoot . '/content/general_pages/epc_ecomae_legal_content.php';
	if (is_file($mc)) {
		require_once $mc;
		if (function_exists('epc_ecomae_docs_catalog')) {
			foreach (array_keys(epc_ecomae_docs_catalog()) as $s) { $paths['/documentation/' . $s] = array('monthly', '0.6'); }
		}
		if (function_exists('epc_ecomae_compare_catalog')) {
			foreach (array_keys(epc_ecomae_compare_catalog()) as $s) { $paths['/compare/' . $s] = array('monthly', '0.6'); }
		}
		if (function_exists('epc_ecomae_bos_articles_catalog')) {
			foreach (array_keys(epc_ecomae_bos_articles_catalog()) as $s) { $paths['/bos/' . $s] = array('monthly', '0.6'); }
		}
		if (function_exists('epc_ecomae_solutions_catalog')) {
			foreach (array_keys(epc_ecomae_solutions_catalog()) as $s) { $paths['/solutions/' . $s] = array('monthly', '0.7'); }
		}
	}
	if (is_file($lc)) {
		require_once $lc;
		if (function_exists('epc_ecomae_legal_catalog')) {
			foreach (array_keys(epc_ecomae_legal_catalog()) as $s) { $paths['/legal/' . $s] = array('monthly', '0.5'); }
		}
	}
	if (is_file($pd)) {
		require_once $pd;
		if (function_exists('epc_ecomae_platform_industry_marketing')) {
			foreach (array_keys(epc_ecomae_platform_industry_marketing()) as $code) { $paths['/platform/industry/' . $code] = array('monthly', '0.7'); }
		}
	}
} catch (Throwable $e) {
	// keep the static path list if dynamic catalogs are unavailable
}

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
