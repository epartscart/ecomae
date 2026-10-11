<?php
// PHP 8.3 goldens for plan Q1-drift (MFA UI renderers).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function drift_freeze($value)
{
	if (is_string($value)) {
		return $value;
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = drift_freeze($v);
		}
		return $out;
	}
	return $value;
}

function epc_mfa_qr_data_uri($uri): string
{
	return 'QR[' . (string) $uri . ']';
}

function drift_run_enroll()
{
	$full = epc_mfa_render_enroll_page(array(
		'secret' => "AB&C's <x>",
		'qr_uri' => 'otpauth://totp/EcomAE:user?secret=AB&issuer=Ecom',
		'backup_codes' => array("CODE-1", "O'Reilly"),
	));
	$empty = epc_mfa_render_enroll_page(array(
		'secret' => '',
		'qr_uri' => '',
	));
	$zero = epc_mfa_render_enroll_page(array(
		'secret' => '0',
		'qr_uri' => 'x',
		'backup_codes' => '0',
	));
	return drift_freeze(array($full, $empty, $zero));
}

function drift_run_verify()
{
	return drift_freeze(epc_mfa_render_verify_page());
}

function drift_run_settings()
{
	$off = epc_mfa_render_settings_panel(array(
		'enrolled' => false,
		'backup_codes_left' => 3,
		'session_verified' => true,
	));
	$on = epc_mfa_render_settings_panel(array(
		'enrolled' => true,
		'backup_codes_left' => 3,
		'session_verified' => true,
	));
	$nobackup = epc_mfa_render_settings_panel(array(
		'enrolled' => 1,
		'backup_codes_left' => 0,
		'session_verified' => false,
	));
	return drift_freeze(array($off, $on, $nobackup));
}

function drift_run_methods()
{
	$html = epc_mfa_render_settings_panel(array(
		'enrolled' => true,
		'backup_codes_left' => 1,
		'session_verified' => true,
		'methods' => array(
			array('method' => 'totp', 'label' => "App <main>", 'confirmed' => 1, 'last_used_at' => '2026-10-09 12:00:00'),
			array('method' => 'backup', 'label' => 'Codes', 'confirmed' => '0'),
		),
	));
	$none = epc_mfa_render_settings_panel(array(
		'enrolled' => true,
		'backup_codes_left' => 2,
		'session_verified' => true,
		'methods' => array(),
	));
	return drift_freeze(array($html, $none, str_contains($html, 'epcMfaAjax'), str_contains($html, 'epcMfaStartEnroll'), str_contains($html, 'epcMfaRegenBackup'), str_contains($html, 'epcMfaDisable')));
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$cleanup = function () use ($admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	require $root . '/content/general_pages/epc_mfa_ui.php';
	$_SERVER = array();
	$_GET = array();
	$GLOBALS['db_link'] = $pdo;
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1d_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1d_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
