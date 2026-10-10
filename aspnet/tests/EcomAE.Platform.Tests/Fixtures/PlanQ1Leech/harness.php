<?php
// PHP 8.3 goldens for plan Q1-leech (CP SSL checker). Leftover top-alert parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function leech_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_top_alerts.php';", '// leftover top-alert stubbed', $code);
	$code = str_replace(
		'$read = stream_socket_client("ssl://".$orignal_parse.":443", $err_no, $err_str, 30, STREAM_CLIENT_CONNECT, $context);',
		'$err_no = $GLOBALS["LEECH_ERR"]; $err_str = ""; $read = false;',
		$code
	);
	file_put_contents($dest, $code);
}

function leech_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_cp_https_redirect_is_configured')) {
		eval('function epc_cp_https_redirect_is_configured($cfg) { return !empty($GLOBALS["LEECH_REDIR"]); }');
	}
	if (!function_exists('epc_cp_top_alerts_render_ssl_item')) {
		eval('function epc_cp_top_alerts_render_ssl_item() { $GLOBALS["LEECH_RENDERED"] = 1; }');
	}
	if (!function_exists('translate_str_by_id')) {
		eval('function translate_str_by_id($id) { return "t".$id; }');
	}
}

function leech_run(int $err, bool $redir): array
{
	$GLOBALS['LEECH_ERR'] = $err;
	$GLOBALS['LEECH_REDIR'] = $redir;
	$GLOBALS['LEECH_RENDERED'] = 0;
	$DP_Config = (object) array('domain_path' => 'https://shop.example/');
	$GLOBALS['DP_Config'] = $DP_Config;
	ob_start();
	include $GLOBALS['LEECH_PAGE'];
	ob_end_clean();
	return array(
		(string) $ssl_state,
		(string) ($ssl_status_text ?? ''),
		(string) ($ssl_a_style ?? ''),
		(string) ($ssl_sign_after ?? ''),
		(int) $GLOBALS['LEECH_RENDERED'],
	);
}

function leech_run_okredir() { return leech_run(0, true); }
function leech_run_oknored() { return leech_run(0, false); }
function leech_run_failredir() { return leech_run(111, true); }
function leech_run_failnored() { return leech_run(111, false); }

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_leech_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	leech_patch($root . '/cp/modules/check_ssl/check_ssl.php', $tmp . '/check.php');
	$GLOBALS['LEECH_PAGE'] = $tmp . '/check.php';
	leech_boot();
	$fn = 'leech_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/check.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1leech_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1leech_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
