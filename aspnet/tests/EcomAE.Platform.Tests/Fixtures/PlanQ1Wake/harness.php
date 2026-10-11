<?php
// PHP 8.3 goldens for plan Q1-wake (BOC page shell). Leftover console/portal/scope parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function wake_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/epc_boc_kernel.php';", '// leftover kernel stubbed', $code);
	$code = preg_replace('/if \(!function_exists\(\'epc_boc_console_open\'\).*?require_once __DIR__ \. \'\/epc_boc_console\.php\';\n\}/s', '// leftover console stubbed', $code);
	$code = preg_replace('/if \(is_file\(__DIR__ \. \'\/epc_portal\.php\'\).*?require_once __DIR__ \. \'\/epc_portal\.php\';\n\}/s', '// leftover portal stubbed', $code);
	$code = preg_replace('/if \(is_file\(__DIR__ \. \'\/epc_boc_tenant_scope\.php\'\)\).*?epc_boc_handle_tenant_switch\(.*?\);\n\t\}/s', "if (function_exists('epc_boc_handle_tenant_switch')) { epc_boc_handle_tenant_switch(null); }", $code);
	file_put_contents($dest, $code);
}

function wake_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_boc_areas')) {
		eval('function epc_boc_areas() { return (array) ($GLOBALS["WAKE_AREAS"] ?? array()); }');
		eval('function epc_portal_is_super_cp_host() { return !empty($GLOBALS["WAKE_SUPER"]); }');
		eval('function epc_boc_nav() { return (array) ($GLOBALS["WAKE_NAV"] ?? array("home")); }');
		eval('function epc_boc_console_open($ctx) { $GLOBALS["WAKE_CTX"] = $ctx; $GLOBALS["WAKE_OPENS"] = (int) ($GLOBALS["WAKE_OPENS"] ?? 0) + 1; }');
		eval('function epc_boc_console_close() { $GLOBALS["WAKE_CLOSES"] = (int) ($GLOBALS["WAKE_CLOSES"] ?? 0) + 1; }');
		eval('function epc_boc_scope_label() { return (string) ($GLOBALS["WAKE_SCOPE"] ?? ""); }');
		eval('function epc_boc_handle_tenant_switch($db) { $GLOBALS["WAKE_SWITCH"] = 1; }');
	}
}

function wake_include(): void
{
	include $GLOBALS['WAKE_PAGE'];
}

function wake_run_norm()
{
	wake_include();
	return array(
		epc_boc_normalize_content_url('/CP/Shop/Foo?x=1'),
		epc_boc_normalize_content_url('control/'),
		epc_boc_normalize_content_url(''),
		epc_boc_normalize_content_url('LOGIN'),
	);
}

function wake_run_resolve()
{
	$GLOBALS['WAKE_AREAS'] = array(
		'shop' => array('path' => '/shop', 'label' => 'Shop'),
		'orders' => array('path' => '/shop/orders', 'label' => 'Orders'),
		'empty' => array('path' => '', 'label' => 'Skip'),
	);
	wake_include();
	$hit = epc_boc_resolve_area('Shop/Orders/Card');
	$miss = epc_boc_resolve_area('finance/ledger');
	$none = epc_boc_resolve_area('');
	return array(
		is_array($hit) ? $hit['id'] : null,
		is_array($hit) ? $hit['area']['label'] : null,
		$miss === null ? 1 : 0,
		$none === null ? 1 : 0,
	);
}

function wake_run_should()
{
	wake_include();
	$GLOBALS['WAKE_SUPER'] = 1;
	$GLOBALS['epc_cp_boc_page'] = false;
	$GLOBALS['epc_boc_page_shell_open'] = false;
	$ok = epc_boc_should_use_page_shell('shop/prices');
	$control = epc_boc_should_use_page_shell('control');
	$login = epc_boc_should_use_page_shell('control/login');
	$GLOBALS['epc_boc_page_shell_open'] = true;
	$already = epc_boc_should_use_page_shell('shop/prices');
	$GLOBALS['epc_boc_page_shell_open'] = false;
	$GLOBALS['WAKE_SUPER'] = 0;
	$tenant = epc_boc_should_use_page_shell('shop/prices');
	return array($ok ? 1 : 0, $control ? 1 : 0, $login ? 1 : 0, $already ? 1 : 0, $tenant ? 1 : 0);
}

function wake_run_open()
{
	$GLOBALS['WAKE_SUPER'] = 1;
	$GLOBALS['WAKE_AREAS'] = array('shop' => array('path' => 'shop', 'label' => 'Shop desk'));
	$GLOBALS['WAKE_NAV'] = array('n1');
	$GLOBALS['WAKE_SCOPE'] = '';
	$GLOBALS['WAKE_OPENS'] = 0;
	$GLOBALS['WAKE_CLOSES'] = 0;
	$GLOBALS['WAKE_SWITCH'] = 0;
	$GLOBALS['epc_boc_page_shell_open'] = false;
	$GLOBALS['epc_cp_boc_page'] = false;
	$DP_Config = (object) array('backend_dir' => 'cp');
	$DP_Content = (object) array('url' => 'shop/prices', 'value' => '');
	$GLOBALS['DP_Config'] = $DP_Config;
	$GLOBALS['DP_Content'] = $DP_Content;
	$GLOBALS['db_link'] = null;
	wake_include();
	epc_boc_page_shell_open(array());
	$first = array(
		(int) $GLOBALS['WAKE_OPENS'],
		(string) $GLOBALS['WAKE_CTX']['title'],
		(string) $GLOBALS['WAKE_CTX']['base'],
		(string) $GLOBALS['WAKE_CTX']['operator'],
		(string) $GLOBALS['WAKE_CTX']['scope'],
		!empty($GLOBALS['epc_cp_skip_page_header']) ? 1 : 0,
		(int) $GLOBALS['WAKE_SWITCH'],
	);
	epc_boc_page_shell_open(array());
	$secondOpens = (int) $GLOBALS['WAKE_OPENS'];
	epc_boc_page_shell_close();
	$closed = array((int) $GLOBALS['WAKE_CLOSES'], empty($GLOBALS['epc_boc_page_shell_open']) ? 1 : 0);
	$GLOBALS['WAKE_SUPER'] = 0;
	$GLOBALS['epc_boc_page_shell_open'] = false;
	epc_boc_page_shell_open(array('title' => 'Nope'));
	$tenant = (int) $GLOBALS['WAKE_OPENS'];
	return array($first, $secondOpens, $closed, $tenant);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_wake_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	wake_patch($root . '/content/general_pages/epc_boc_page_shell.php', $tmp . '/shell.php');
	$GLOBALS['WAKE_PAGE'] = $tmp . '/shell.php';
	wake_boot();
	$fn = 'wake_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/shell.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1wake_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1wake_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
