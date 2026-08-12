<?php
/**
 * Render industry showcase hubs (+ sub-industry pages) for ASP.NET primary.
 *
 * PHP templates under content/general_pages/industry_templates/ remain the
 * look authority; ASP.NET serves these snapshots on {slug}.ecomae.com.
 *
 * Run from repo root:
 *   php scripts/render_ecomae_industry_snapshots.php
 * Output: content/general_pages/epc_rendered_industry/{hostSlug}.html
 *         content/general_pages/epc_rendered_industry/{hostSlug}__{sub}.html
 */

error_reporting(E_ERROR | E_PARSE);

$root = dirname(__DIR__);
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

$docRoot = $root;
if (!is_file($root . '/config.php')) {
	$shadow = sys_get_temp_dir() . '/epc-industry-snapshot-root';
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
		"<?php\n"
		. "if (!class_exists('DP_Config')) {\n"
		. "\t#[\\AllowDynamicProperties]\n"
		. "\tclass DP_Config { public function __get(\$k) { return null; } }\n"
		. "}\n"
	);
	$docRoot = $shadow;
}

$_SERVER['DOCUMENT_ROOT'] = $docRoot;
$_SERVER['REQUEST_METHOD'] = 'GET';
$GLOBALS['multilang_params'] = array('lang_href' => '/en');

require_once $docRoot . '/content/general_pages/epc_industry_subdomain_router.php';

/** @var array<string,string> $hostToTemplate host slug → template file key */
$hostToTemplate = array(
	'agriculture' => 'agriculture',
	'automotive' => 'automotive',
	'beauty' => 'beauty',
	'cleaning' => 'cleaning',
	'construction' => 'construction',
	'education' => 'education',
	'electronics' => 'electronics',
	'energy' => 'energy',
	'fashion' => 'fashion',
	'finance' => 'finance',
	'food' => 'food_beverage',
	'healthcare' => 'healthcare',
	'homeliving' => 'home_living',
	'hospitality' => 'hospitality',
	'jewellery' => 'jewellery',
	'logistics' => 'logistics',
	'manufacturing' => 'manufacturing',
	'media' => 'media',
	'nonprofit' => 'nonprofit',
	'pet' => 'pet',
	'printing' => 'printing',
	'professional' => 'professional',
	'rental' => 'rental',
	'retail' => 'retail',
	'security' => 'security',
	'sports' => 'sports',
	'technology' => 'it_software',
	'wholesale' => 'wholesale',
);

$outDir = $root . '/content/general_pages/epc_rendered_industry';
if (!is_dir($outDir)) {
	mkdir($outDir, 0755, true);
}

function epc_industry_snapshot_render(string $docRoot, string $hostSlug, string $group, string $templateKey, string $uri): string
{
	$_SERVER['HTTP_HOST'] = $hostSlug . '.ecomae.com';
	$_SERVER['REQUEST_URI'] = $uri;
	$_SERVER['SERVER_NAME'] = $_SERVER['HTTP_HOST'];
	$GLOBALS['epc_industry_subdomain_active'] = true;
	$GLOBALS['epc_industry_subdomain_slug'] = $hostSlug;
	$GLOBALS['epc_industry_subdomain_group'] = $group;

	$templateFile = $docRoot . '/content/general_pages/industry_templates/' . $templateKey . '.php';
	if (!is_file($templateFile)) {
		throw new RuntimeException('missing template ' . $templateKey);
	}

	ob_start();
	require $templateFile;
	return (string) ob_get_clean();
}

/**
 * @return list<string> unique sub-path slugs linked from the hub HTML
 */
function epc_industry_snapshot_subs_from_hub(string $hostSlug, string $hubHtml): array
{
	$subs = array();
	$hostRe = preg_quote($hostSlug . '.ecomae.com', '#');
	if (preg_match_all('#https?://' . $hostRe . '/([a-z0-9][a-z0-9-]{2,})#i', $hubHtml, $m)) {
		foreach ($m[1] as $seg) {
			$seg = strtolower($seg);
			if (in_array($seg, array('cp', 'erp', 'api', 'platform', 'documentation', 'bos', 'storefront', 'marketing'), true)) {
				continue;
			}
			$subs[$seg] = true;
		}
	}
	return array_keys($subs);
}

$ok = 0;
$failures = 0;
foreach ($hostToTemplate as $hostSlug => $templateKey) {
	$group = function_exists('epc_industry_subdomain_resolve_group')
		? epc_industry_subdomain_resolve_group($hostSlug)
		: $hostSlug;
	try {
		$html = epc_industry_snapshot_render($docRoot, $hostSlug, $group, $templateKey, '/');
	} catch (Throwable $e) {
		fwrite(STDERR, "FAIL hub {$hostSlug}: {$e->getMessage()}\n");
		$failures++;
		continue;
	}
	if (trim($html) === '') {
		fwrite(STDERR, "FAIL hub {$hostSlug}: empty\n");
		$failures++;
		continue;
	}
	$file = $outDir . '/' . $hostSlug . '.html';
	file_put_contents($file, $html);
	$ok++;
	printf("OK  hub %-18s %8s bytes\n", $hostSlug, number_format(strlen($html)));

	foreach (epc_industry_snapshot_subs_from_hub($hostSlug, $html) as $subSlug) {
		$uri = '/' . $subSlug;
		try {
			$subHtml = epc_industry_snapshot_render($docRoot, $hostSlug, $group, $templateKey, $uri);
		} catch (Throwable $e) {
			fwrite(STDERR, "FAIL sub {$hostSlug}{$uri}: {$e->getMessage()}\n");
			$failures++;
			continue;
		}
		if (trim($subHtml) === '') {
			fwrite(STDERR, "FAIL sub {$hostSlug}{$uri}: empty\n");
			$failures++;
			continue;
		}
		$subFile = $outDir . '/' . $hostSlug . '__' . $subSlug . '.html';
		file_put_contents($subFile, $subHtml);
		$ok++;
		printf("OK  sub %-18s %-40s %8s bytes\n", $hostSlug, $subSlug, number_format(strlen($subHtml)));
	}
}

printf("\nrendered=%d failed=%d out=%s\n", $ok, $failures, $outDir);
exit($failures > 0 ? 1 : 0);
