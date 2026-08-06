<?php
/**
 * Render EVERY www.ecomae.com marketing page into a static HTML snapshot using
 * the PHP marketing router itself (epc_ecomae_platform_render_standalone) —
 * byte-parity with the PHP reference for the whole marketing site, not just home.
 *
 * ASP.NET serves these snapshots at the same canonical URLs on marketing hosts.
 *
 * Run from the repo root (CLI):
 *   php scripts/render_ecomae_marketing_snapshots.php
 * Output: content/general_pages/epc_rendered_marketing/<slug>.html
 *   (path → slug: strip leading '/', replace '/' with '__'; '/' → home.html)
 */

error_reporting(E_ERROR | E_PARSE);

$root = dirname(__DIR__);
define('_ASTEXE_', 1);

/*
 * The marketing layout requires /config.php (parts-agent widget). On the live
 * server it exists; locally use a shadow DOCUMENT_ROOT with a stub DP_Config
 * so snapshots render (the live re-render picks up the real config).
 */
$docRoot = $root;
if (!is_file($root . '/config.php')) {
	$shadow = sys_get_temp_dir() . '/epc-marketing-snapshot-root';
	if (!is_dir($shadow)) {
		mkdir($shadow, 0755, true);
	}
	foreach (scandir($root) as $entry) {
		if ($entry === '.' || $entry === '..') {
			continue;
		}
		$link = $shadow . '/' . $entry;
		if (!file_exists($link) && !is_link($link)) {
			@symlink($root . '/' . $entry, $link);
		}
	}
	file_put_contents(
		$shadow . '/config.php',
		"<?php /* snapshot stub — live render uses the real config */\n"
		. "if (!class_exists('DP_Config')) {\n"
		. "\t#[\\AllowDynamicProperties]\n"
		. "\tclass DP_Config { public function __get(\$k) { return null; } }\n"
		. "}\n"
	);
	$docRoot = $shadow;
}
$_SERVER['DOCUMENT_ROOT'] = $docRoot;
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['REQUEST_METHOD'] = 'GET';
$GLOBALS['multilang_params'] = array('lang_href' => '/en');

require_once $docRoot . '/content/general_pages/epc_portal.php';
require_once $docRoot . '/content/general_pages/epc_portal_tenant.php'; // epc_portal_is_platform_hostname()
require_once $docRoot . '/content/general_pages/epc_ecomae_platform_router.php';
require_once $docRoot . '/content/general_pages/epc_ecomae_platform_pages.php';
require_once $docRoot . '/content/general_pages/epc_ecomae_free_tools.php';
require_once $docRoot . '/content/general_pages/epc_ecomae_marketing_pages.php';
require_once $docRoot . '/content/general_pages/epc_ecomae_marketing_content.php';
require_once $docRoot . '/content/general_pages/epc_ecomae_legal_pages.php';
require_once $docRoot . '/content/general_pages/epc_ecomae_legal_content.php';

$routes = array(
	'/',
	'/platform',
	'/platform/industries',
	'/platform/capabilities',
	'/platform/platform-guides',
	'/platform/free-tools',
	'/platform/pricing',
	'/platform/about',
	'/platform/contact',
	'/platform/demo',
	'/platform/customer-results',
	'/platform/business-continuity',
	'/platform/api-services',
	'/platform/api-documentation',
	'/platform/auto-price-ai',
	'/platform/faq',
	'/brochure',
	'/brochure/cp',
	'/documentation',
	'/compare',
	'/blockchain',
	'/solutions',
	'/legal',
);

// Nested catalogs — same enumeration the PHP sitemap uses.
if (function_exists('epc_free_tools_catalog')) {
	foreach (array_keys(epc_free_tools_catalog()) as $tool) {
		$routes[] = '/platform/free-tools/' . $tool;
	}
}
if (function_exists('epc_ecomae_docs_catalog')) {
	foreach (array_keys(epc_ecomae_docs_catalog()) as $slug) {
		$routes[] = '/documentation/' . $slug;
	}
}
if (function_exists('epc_ecomae_compare_catalog')) {
	foreach (array_keys(epc_ecomae_compare_catalog()) as $slug) {
		$routes[] = '/compare/' . $slug;
	}
}
if (function_exists('epc_ecomae_bos_articles_catalog')) {
	// Bare /bos is the product BOS app (Super-CP) — articles only.
	foreach (array_keys(epc_ecomae_bos_articles_catalog()) as $slug) {
		$routes[] = '/bos/' . $slug;
	}
}
if (function_exists('epc_ecomae_solutions_catalog')) {
	foreach (array_keys(epc_ecomae_solutions_catalog()) as $slug) {
		$routes[] = '/solutions/' . $slug;
	}
}
if (function_exists('epc_ecomae_legal_catalog')) {
	foreach (array_keys(epc_ecomae_legal_catalog()) as $slug) {
		$routes[] = '/legal/' . $slug;
	}
}
if (function_exists('epc_ecomae_legal_top_level_aliases')) {
	foreach (array_keys(epc_ecomae_legal_top_level_aliases()) as $alias) {
		$routes[] = '/' . ltrim($alias, '/');
	}
}
if (function_exists('epc_ecomae_platform_industry_marketing')) {
	foreach (array_keys(epc_ecomae_platform_industry_marketing()) as $code) {
		$routes[] = '/platform/industry/' . $code;
	}
}

$routes = array_values(array_unique($routes));

$outDir = $root . '/content/general_pages/epc_rendered_marketing';
if (!is_dir($outDir)) {
	mkdir($outDir, 0755, true);
}

function epc_snapshot_slug(string $path): string
{
	$trim = trim($path, '/');
	if ($trim === '') {
		return 'home';
	}
	return str_replace('/', '__', $trim);
}

$ok = 0;
$failures = 0;
foreach ($routes as $path) {
	$_SERVER['REQUEST_URI'] = $path;
	try {
		ob_start();
		$rendered = epc_ecomae_platform_render_standalone($path);
		$html = (string) ob_get_clean();
	} catch (\Throwable $e) {
		while (ob_get_level() > 0) {
			ob_end_clean();
		}
		fwrite(STDERR, "FAIL {$path}: {$e->getMessage()} @ {$e->getFile()}:{$e->getLine()}\n");
		$failures++;
		continue;
	}

	if (!$rendered || trim($html) === '') {
		fwrite(STDERR, "FAIL {$path}: router did not render\n");
		$failures++;
		continue;
	}

	$file = $outDir . '/' . epc_snapshot_slug($path) . '.html';
	file_put_contents($file, $html);
	$ok++;
	printf("OK  %-45s %8s bytes\n", $path, number_format(strlen($html)));
}

printf("\nrendered=%d failed=%d out=%s\n", $ok, $failures, $outDir);
exit($failures > 0 ? 1 : 0);
