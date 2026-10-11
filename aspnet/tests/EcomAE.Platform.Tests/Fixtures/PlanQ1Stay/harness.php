<?php
// PHP 8.3 goldens for plan Q1-stay (CP page assets). Leftover version / ajax / host / ERP-nav parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function stay_capture(callable $fn): string
{
	ob_start();
	$fn();
	return (string) ob_get_clean();
}

function stay_reset(): void
{
	$GLOBALS['DP_Config'] = null;
	$GLOBALS['epc_cp_page_assets'] = null;
	$GLOBALS['STAY_HOST'] = '';
	$GLOBALS['STAY_ERP_NAV'] = '/content/shop/finance/erp-shell-nav.js?v=nav1';
	$_GET = array();
	$_SERVER = array();
}

function stay_backend($dir)
{
	if ($dir === null) {
		$GLOBALS['DP_Config'] = null;
		return;
	}
	$GLOBALS['DP_Config'] = (object) array('backend_dir' => $dir);
}

function stay_pick(array $map, array $keys): array
{
	$out = array();
	foreach ($keys as $key) {
		$out[$key] = $map[$key] ?? null;
	}
	return $out;
}

function stay_run_map()
{
	stay_reset();
	$defaultVer = epc_cp_page_asset_version();
	stay_backend(null);
	$defaultMap = epc_cp_page_asset_url_map();
	stay_backend('ops');
	$opsMap = epc_cp_page_asset_url_map();
	stay_backend('');
	$emptyMap = epc_cp_page_asset_url_map();
	stay_backend('cp');
	$_GET = array();
	$ordersPlain = epc_cp_page_asset_url_map()['shop/orders/orders'];
	$_GET['order_id'] = '7abc';
	$_GET['status_id'] = '';
	$ordersGet = epc_cp_page_asset_url_map()['shop/orders/orders'];
	$_GET = array();
	$sampleKeys = array(
		'control/communications',
		'control/config',
		'control/portal/epc_auto_price_engine',
		'control/portal/epc_power_bi',
		'control/portal/epc_api_documentation_guide',
		'control/portal/epc_visual_page_editor',
		'filemanager',
		'shop/orders/orders',
		'shop/finance/account_operations',
	);
	return array(
		$defaultVer,
		array_keys($defaultMap),
		stay_pick($defaultMap, $sampleKeys),
		stay_pick($opsMap, array('control/communications', 'filemanager')),
		stay_pick($emptyMap, array('control/communications', 'shop/orders/items')),
		$ordersPlain,
		$ordersGet,
	);
}

function stay_run_resolve()
{
	stay_reset();
	stay_backend('cp');
	$unknown = epc_cp_page_assets_for_url('no/such/page');
	$erp = epc_cp_page_assets_for_url('shop/finance/erp');
	$erpChild = epc_cp_page_assets_for_url('/shop/finance/erp/invoices/');
	$erpFoo = epc_cp_page_assets_for_url('shop/finance/erpfoo');
	$comms = epc_cp_page_assets_for_url('/control/communications/');
	$ao = epc_cp_page_assets_for_url('shop/finance/account_operations');
	$vpe = epc_cp_page_assets_for_url('control/portal/epc_visual_page_editor');
	$GLOBALS['epc_cp_page_assets'] = array(
		'css' => array("https://cdn.example/o'x.css" => 1),
		'js' => array("https://cdn.example/o'x.js" => 1),
	);
	$merged = epc_cp_page_assets_for_url('control/communications');
	$GLOBALS['epc_cp_page_assets'] = array('css' => array(), 'js' => array());
	$emptyExtra = epc_cp_page_assets_for_url('control/communications');
	return array(
		$unknown,
		$erp,
		$erpChild,
		$erpFoo,
		$comms,
		$ao,
		$vpe,
		$merged,
		$emptyExtra,
		epc_erp_shell_nav_js_src(),
	);
}

function stay_run_head()
{
	stay_reset();
	stay_backend('cp');
	$comms = stay_capture(function () {
		epc_cp_page_head_assets('control/communications');
	});
	$unknown = stay_capture(function () {
		epc_cp_page_head_assets('missing');
	});
	$erp = stay_capture(function () {
		epc_cp_page_head_assets('shop/finance/erp');
	});
	$GLOBALS['epc_cp_page_assets'] = array(
		'css' => array("https://cdn.example/o'x.css" => true),
	);
	$quote = stay_capture(function () {
		epc_cp_page_head_assets('missing');
	});
	$vpe = stay_capture(function () {
		$GLOBALS['epc_cp_page_assets'] = null;
		epc_cp_page_head_assets('control/portal/epc_visual_page_editor');
	});
	return array($comms, $unknown, $erp, $quote, $vpe);
}

function stay_tabs(): array
{
	$names = array(
		'discover', 'discovery', 'taxonomy', 'disc_sources', 'market_sources',
		'settings', 'dashboard', 'my_imports', 'imports', 'custom_tab',
	);
	$out = array();
	foreach ($names as $tab) {
		$_GET['tab'] = $tab;
		$out[$tab] = epc_cp_apai_discover_tab_key();
	}
	unset($_GET['tab']);
	$out['missing'] = epc_cp_apai_discover_tab_key();
	return $out;
}

function stay_run_scripts()
{
	stay_reset();
	stay_backend('cp');
	$tabs = stay_tabs();
	$_GET = array(
		'site_key' => 'Acme-1!',
		'tab' => 'discovery',
		'imports_filter' => 'nope',
	);
	$discover = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_auto_price_engine');
	});
	$_GET = array(
		'site_key' => 'Beta_2!',
		'tab' => 'taxonomy',
		'view' => 'grid',
		'taxonomy_id' => '7abc',
		'imports_filter' => 'price_changes',
	);
	$taxonomy = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_auto_price_engine');
	});
	$_GET = array(
		'tab' => 'discover',
		'view' => '  ',
		'taxonomy_id' => '0',
		'imports_filter' => 'duplicates',
	);
	$viewBlank = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_auto_price_engine');
	});
	stay_backend('');
	$_GET = array('tab' => 'discover', 'site_key' => 'Acme-1!');
	$emptyBackend = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_auto_price_engine');
	});
	stay_backend('cp');
	$_GET = array('tab' => 'discover');
	$GLOBALS['STAY_HOST'] = 'shop.epartscart.ae';
	$hostParts = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_auto_price_engine');
	});
	$GLOBALS['STAY_HOST'] = 'www.electronicae.com';
	$hostEl = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_auto_price_engine');
	});
	$GLOBALS['STAY_HOST'] = 'other.example';
	$hostOther = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_auto_price_engine');
	});
	$GLOBALS['STAY_HOST'] = '';
	$_GET = array(
		'site_key' => 'Acme-1!',
		'page_key' => 'Home Page!',
	);
	$vpe = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_visual_page_editor');
	});
	$_GET = array(
		'site_key' => 'acme1',
		'page_key' => 'my-page!',
	);
	$ver = epc_cp_page_asset_version() . 'vpe1';
	$configSrc = '/cp/content/control/portal/epc_visual_page_editor_config.php?v=' . rawurlencode($ver)
		. '&site_key=' . rawurlencode('acme1')
		. '&page_key=' . rawurlencode('my-page');
	$jsSrc = '/cp/content/control/portal/epc_visual_page_editor.js?v=' . rawurlencode($ver);
	$GLOBALS['epc_cp_page_assets'] = array(
		'js' => array(
			$configSrc => true,
			$jsSrc => true,
		),
	);
	$vpeSkip = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_visual_page_editor');
	});
	$GLOBALS['epc_cp_page_assets'] = array(
		'js' => array(
			$configSrc => 0,
		),
	);
	$vpeEmpty0 = stay_capture(function () {
		epc_cp_page_footer_scripts('control/portal/epc_visual_page_editor');
	});
	$GLOBALS['epc_cp_page_assets'] = array(
		'js' => array("https://cdn.example/o'x.js" => true),
	);
	$comms = stay_capture(function () {
		epc_cp_page_footer_scripts('control/communications');
	});
	$GLOBALS['epc_cp_page_assets'] = null;
	$unknown = stay_capture(function () {
		epc_cp_page_footer_scripts('missing');
	});
	$inlineOnly = stay_capture(function () {
		$_GET = array('site_key' => 'Acme-1!', 'tab' => 'my_imports', 'imports_filter' => 'new');
		epc_cp_apai_inline_discover_config_script();
	});
	$shellOnly = stay_capture(function () {
		$_GET = array('site_key' => 'Acme-1!', 'tab' => 'discover');
		epc_cp_apai_shell_config_script();
	});
	return array(
		epc_cp_page_asset_version(),
		$tabs,
		$discover,
		$taxonomy,
		$viewBlank,
		$emptyBackend,
		$hostParts,
		$hostEl,
		$hostOther,
		$vpe,
		$vpeSkip,
		$vpeEmpty0,
		$comms,
		$unknown,
		$inlineOnly,
		$shellOnly,
	);
}

function stay_boot(array $case): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_erp_shell_nav_js_href')) {
		eval('function epc_erp_shell_nav_js_href(): string { return (string) ($GLOBALS["STAY_ERP_NAV"] ?? "/content/shop/finance/erp-shell-nav.js?v=nav1"); }');
	}
	if (!function_exists('epc_portal_host')) {
		eval('function epc_portal_host() { return (string) ($GLOBALS["STAY_HOST"] ?? ""); }');
	}
	if (!empty($case['ver_fn']) && !function_exists('epc_cp_shell_css_version')) {
		eval('function epc_cp_shell_css_version(): string { return (string) ($GLOBALS["STAY_VER"] ?? "injectedver1"); }');
	}
	stay_reset();
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	stay_boot($case);
	require $root . '/content/general_pages/epc_cp_page_assets.php';
	$fn = 'stay_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1stay_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1stay_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
