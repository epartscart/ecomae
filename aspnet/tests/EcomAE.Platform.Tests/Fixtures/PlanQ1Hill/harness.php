<?php
// PHP 8.3 goldens for plan Q1-hill (public Free Tools catalog + page).
// Real file is included. Leftover finance localization / labour-law engines
// stay their own gaps and are only loaded by the PHP file. GET never mints
// a session.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function hill_boot(string $root): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$_SERVER['DOCUMENT_ROOT'] = $root;
	require_once $root . '/content/general_pages/epc_ecomae_free_tools.php';
}

function hill_catalog_digest(): array
{
	$cat = epc_free_tools_catalog();
	$keys = array_keys($cat);
	$names = array();
	foreach ($cat as $k => $row) {
		$names[$k] = (string) ($row['name'] ?? '');
	}
	return array('keys' => $keys, 'names' => $names, 'count' => count($cat));
}

function hill_run_empty(): array
{
	$catalog = hill_catalog_digest();
	$countries = epc_free_tools_countries();
	$citAe = epc_free_tools_cit_rate('AE');
	$citXx = epc_free_tools_cit_rate('zz');
	$auth = epc_free_tools_authority('AE');
	$h = epc_free_tools_h('Ada & Co <x>');
	$round = epc_free_tools_round('1.235');
	$find = epc_free_tools_finding('warn', 'Ada & Co');
	return array($catalog, $countries['AE'] ?? '', $countries['XX'] ?? '', $citAe, $citXx, $auth, $h, $round, $find);
}

function hill_run_gates(): array
{
	unset($_SERVER['HTTP_HOST'], $_SERVER['HTTPS']);
	$baseNone = epc_free_tools_site_base('');
	$baseFb = epc_free_tools_site_base('https://acme.example/');
	$_SERVER['HTTP_HOST'] = 'beta.example.com';
	$_SERVER['HTTPS'] = 'off';
	$baseHost = epc_free_tools_site_base('');
	$seoHub = epc_free_tools_seo('');
	$seoVat = epc_free_tools_seo('vat');
	$faq = epc_free_tools_faq_html('vat');
	$guide = epc_free_tools_guide('vat');
	$disabled = epc_free_tools_disabled_map();
	$active = epc_free_tools_is_active('vat');
	return array($baseNone, $baseFb, $baseHost, $seoHub['title'] ?? '', $seoVat['title'] ?? '', $faq, $guide['what'] ?? '', $disabled, $active);
}

function hill_run_live(): array
{
	$base = 'https://www.ecomae.com/';
	$hub = epc_ecomae_platform_page_free_tools(array());
	$vat = epc_ecomae_platform_page_free_tools(array('tool' => 'vat'));
	$bad = epc_ecomae_platform_page_free_tools(array('tool' => 'nope!'));
	$json = epc_free_tools_jsonld($base, 'vat');
	$styles = epc_free_tools_styles();
	$scripts = epc_free_tools_scripts($base);
	$fields = epc_free_tools_fields_html('vat');
	return array(
		substr($hub, 0, 80),
		(strpos($hub, 'VAT / GST Return') !== false) ? 1 : 0,
		(strpos($vat, 'Create free account') !== false) ? 1 : 0,
		(strpos($bad, 'Free business tools') !== false) ? 1 : 0,
		(strpos($json, 'FAQPage') !== false) ? 1 : 0,
		strlen($styles),
		strlen($scripts),
		(strpos($fields, 'data-eft-field="standard_sales"') !== false) ? 1 : 0,
		$hub,
		$vat,
	);
}

function hill_run_tenant(): array
{
	$acme = epc_free_tools_render_hub(epc_free_tools_catalog(), 'https://acme.example/');
	$beta = epc_free_tools_render_hub(epc_free_tools_catalog(), 'https://beta.example/');
	$off = epc_free_tools_render_tool('vat', epc_free_tools_catalog()['vat'], 'https://acme.example/');
	return array($acme, $beta, $off);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	hill_boot($root);
	$fn = 'hill_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		exit(1);
	}
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1hill_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1hill_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
