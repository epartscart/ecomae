<?php
// PHP 8.3 goldens for plan Q1-hive (public platform marketing pages).
// Real pages file is included. Leftover unique portal / platform-data parent
// stay their own gaps and are only loaded by PHP. GET never mints a session.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function hive_boot(string $root, string $backend = 'cp'): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$_SERVER['DOCUMENT_ROOT'] = $root;
	if (!isset($_SERVER['REQUEST_METHOD'])) {
		$_SERVER['REQUEST_METHOD'] = 'GET';
	}
	$GLOBALS['DP_Config'] = (object) array('backend_dir' => $backend);
	require_once $root . '/content/general_pages/epc_ecomae_platform_pages.php';
}

function hive_score_text($kw, $q)
{
	if (!$q) {
		return 1;
	}
	$words = preg_split('/\s+/', (string) $q);
	$score = 0;
	foreach ($words as $w) {
		if ($w !== '' && strpos((string) $kw, $w) !== false) {
			$score++;
		}
	}
	return $score;
}

function hive_filter_subs(array $items, $q)
{
	$visible = array();
	foreach ($items as $it) {
		$kw = (string) ($it['kw'] ?? '');
		$ok = !$q || hive_score_text($kw, $q) > 0;
		if ($ok) {
			$visible[] = (string) ($it['label'] ?? '');
		}
	}
	$n = count($visible);
	return array(
		'visible' => $visible,
		'count' => $n,
		'label' => $n . ' page' . ($n === 1 ? '' : 's'),
	);
}

function hive_page_flags($html): array
{
	$html = (string) $html;
	return array(
		'contact' => (strpos($html, 'Contact ECOM AE') !== false) ? 1 : 0,
		'about' => (strpos($html, 'About ECOM AE') !== false) ? 1 : 0,
		'api_docs' => (strpos($html, 'API documentation') !== false) ? 1 : 0,
		'api_svc' => (strpos($html, 'Autoparts Catalog') !== false) ? 1 : 0,
		'home' => (strpos($html, 'epm-hub-section') !== false) ? 1 : 0,
		'flash_ok' => (strpos($html, 'Thank you — we respond within one business day (GST).') !== false) ? 1 : 0,
		'flash_err' => (strpos($html, 'Please enter your name and a valid email.') !== false) ? 1 : 0,
		'prefix' => substr($html, 0, 48),
	);
}

function hive_run_empty(): array
{
	$items = array(
		array('label' => 'Auto parts', 'kw' => 'auto parts catalog vin'),
		array('label' => 'Fashion', 'kw' => 'fashion apparel retail'),
		array('label' => 'Tax', 'kw' => 'tax advisory vat'),
	);
	return array(
		hive_score_text('', ''),
		hive_score_text('auto parts catalog vin', 'auto'),
		hive_score_text('auto parts catalog vin', 'vin search'),
		hive_score_text('auto parts catalog vin', ''),
		hive_filter_subs($items, ''),
		hive_filter_subs($items, 'tax'),
		hive_filter_subs($items, 'missing'),
	);
}

function hive_run_gates(): array
{
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_POST = array();
	$get = epc_ecomae_platform_page_contact();
	$_SERVER['REQUEST_METHOD'] = 'POST';
	$_POST = array('epc_contact_request' => '1', 'contact_name' => 'Ada', 'contact_email' => 'not-an-email');
	$bad = epc_ecomae_platform_page_contact();
	$_POST = array(
		'epc_contact_request' => '1',
		'contact_name' => 'Ada & Co',
		'contact_email' => 'ada@example.com',
		'company' => 'Acme <x>',
		'message' => 'Hello',
	);
	$ok = epc_ecomae_platform_page_contact();
	$_SERVER['REQUEST_METHOD'] = 'POST';
	$_POST = array('contact_name' => 'Ada', 'contact_email' => 'ada@example.com');
	$noFlag = epc_ecomae_platform_page_contact();
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_POST = array();
	$innerBang = epc_ecomae_platform_render_inner('contact!!!');
	$innerHyphen = epc_ecomae_platform_render_inner('api-documentation');
	return array(
		$get,
		$bad,
		$ok,
		$noFlag,
		hive_page_flags($innerBang),
		hive_page_flags($innerHyphen),
		(strpos($noFlag, 'Thank you') !== false) ? 1 : 0,
	);
}

function hive_run_live(): array
{
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_POST = array();
	$about = epc_ecomae_platform_page_about();
	$docs = epc_ecomae_platform_page_api_documentation();
	$svc = epc_ecomae_platform_page_api_services(array());
	$svcCat = epc_ecomae_platform_page_api_services(array('focus' => 'catalog'));
	$svcPrice = epc_ecomae_platform_page_api_services(array('focus' => 'price_pro'));
	$missing = epc_ecomae_platform_page_industry(array('code' => 'nope'));
	$innerAbout = epc_ecomae_platform_render_inner('about');
	$innerDocs = epc_ecomae_platform_render_page('api_documentation', array(), 'inner');
	$eco = function_exists('epc_ecomae_platform_ecosystem_model_section')
		? epc_ecomae_platform_ecosystem_model_section()
		: '';
	return array(
		$about,
		$docs,
		$svc,
		$svcCat,
		$svcPrice,
		$missing,
		$innerAbout === $about ? 1 : 0,
		$innerDocs === $docs ? 1 : 0,
		$eco,
		hive_page_flags($svcCat),
		(strpos($svcCat, "getElementById('catalog-api')") !== false) ? 1 : 0,
		(strpos($svcPrice, "getElementById('price-pro-api')") !== false) ? 1 : 0,
	);
}

function hive_run_tenant(): array
{
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_POST = array();
	$_SERVER['HTTP_HOST'] = 'acme.example';
	$GLOBALS['DP_Config']->backend_dir = 'acme-cp';
	$acme = epc_ecomae_platform_page_contact();
	$aboutAcme = epc_ecomae_platform_page_about();
	$GLOBALS['DP_Config']->backend_dir = 'beta-cp';
	$_SERVER['HTTP_HOST'] = 'beta.example';
	$beta = epc_ecomae_platform_page_contact();
	$aboutBeta = epc_ecomae_platform_page_about();
	return array(
		$acme,
		$beta,
		$aboutAcme,
		$aboutBeta,
		(strpos($acme, 'https://www.ecomae.com/acme-cp') !== false) ? 1 : 0,
		(strpos($beta, 'https://www.ecomae.com/beta-cp') !== false) ? 1 : 0,
		(strpos($acme, 'https://www.ecomae.com/beta-cp') !== false) ? 1 : 0,
		(strpos($aboutAcme, 'https://acme.example') !== false) ? 1 : 0,
		(strpos($aboutBeta, 'https://www.ecomae.com/') !== false) ? 1 : 0,
	);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	hive_boot($root, 'cp');
	$fn = 'hive_run_' . $case['name'];
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1hive_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1hive_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
