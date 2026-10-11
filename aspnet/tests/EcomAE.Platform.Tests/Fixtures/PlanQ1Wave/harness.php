<?php
// PHP 8.3 goldens for plan Q1-wave (MFA helpers).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function wave_freeze($value)
{
	if (is_string($value)) {
		if (preg_match('/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/', $value)) {
			return '2026-10-10 00:00:00';
		}
		if (preg_match('/^[A-Z2-7]{16,}$/', $value)) {
			return 'SECRET';
		}
		if (preg_match('/^[A-F0-9]{10}$/', $value)) {
			return 'CODE';
		}
		$value = preg_replace('/secret=[A-Z2-7]+/', 'secret=SECRET', $value);
		$value = preg_replace('/ecomae_cpw_[a-f0-9]+/', 'TENANT_DB', $value);
		return $value;
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = wave_freeze($v);
		}
		return $out;
	}
	return $value;
}

function wave_schema($pdo)
{
	$pdo->exec('CREATE TABLE `users` (
		`user_id` int NOT NULL,
		`type` int NOT NULL DEFAULT 0,
		`email` varchar(255) NOT NULL DEFAULT \'\',
		PRIMARY KEY (`user_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `user_groups` (
		`id` int NOT NULL AUTO_INCREMENT,
		`name` varchar(128) NOT NULL DEFAULT \'\',
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `user_group_link` (
		`user_id` int NOT NULL,
		`group_id` int NOT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `epc_erp_user_departments` (
		`user_id` int NOT NULL,
		`department` varchar(128) NOT NULL DEFAULT \'\'
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function wave_run_pure($pdo)
{
	$enc = epc_mfa_base32_encode('Hello');
	$dec = epc_mfa_base32_decode($enc);
	$secret = 'JBSWY3DPEHPK3PXP';
	$code = epc_mfa_totp_code($secret, 1000);
	$uri = epc_mfa_otpauth_uri($secret, 'ops@ecomae.com');
	$qr = epc_mfa_qr_data_uri($uri);
	$sess = epc_mfa_session_verified();
	$erp = epc_mfa_erp_finance_gate($pdo, 7, 'orders');
	$_GET['mfa_action'] = 'nope';
	$ajax = epc_mfa_handle_ajax($pdo, 7);
	$path = epc_mfa_path_requires_mfa('/cp/shop/finance/erp', $pdo);
	$path2 = epc_mfa_path_requires_mfa('/cp/users', $pdo);
	return wave_freeze(array($enc, $dec, $code, $uri, $qr, $sess, $erp, $ajax, $path, $path2));
}

function wave_run_enroll($pdo)
{
	$pdo->exec("INSERT INTO `users` (`user_id`, `type`, `email`) VALUES (7, 1, 'ops@ecomae.com')");
	$first = epc_mfa_enroll($pdo, 7, 'ops@ecomae.com');
	$secret = (string) $first['secret'];
	$bad = epc_mfa_confirm_enrollment($pdo, 7, '000000');
	$ok = epc_mfa_confirm_enrollment($pdo, 7, epc_mfa_totp_code($secret));
	$again = epc_mfa_enroll($pdo, 7, 'ops@ecomae.com');
	$ver = epc_mfa_verify($pdo, 7, epc_mfa_totp_code($secret));
	$st = $pdo->query('SELECT `user_id`, `action`, `success` FROM `epc_mfa_audit_log` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($st as &$r) {
		$r['user_id'] = (int) $r['user_id'];
		$r['success'] = (int) $r['success'];
	}
	unset($r);
	$status = epc_mfa_user_status($pdo, 7);
	$status['backup_codes_left'] = (int) $status['backup_codes_left'];
	return wave_freeze(array($first, $bad, $ok, $again, $ver, $st, $status));
}

function wave_run_policy($pdo)
{
	$def = epc_mfa_get_policy($pdo);
	$saved = epc_mfa_save_policy($pdo, array(
		'require_mfa_for_roles' => array('finance_user'),
		'require_mfa_for_paths' => array('/cp/shop/finance/'),
		'grace_period_hours' => 12,
	), 'acme');
	$got = epc_mfa_get_policy($pdo, 'acme');
	$upd = epc_mfa_update_policy($pdo, 'acme', array(
		'require_mfa_for_roles' => 'finance_admin',
		'require_mfa_for_paths' => '/erp/gl',
		'grace_period_hours' => 4,
	));
	$got2 = epc_mfa_get_policy($pdo, 'acme');
	$pdo->exec("INSERT INTO `users` (`user_id`, `type`, `email`) VALUES (1, 1, 'a@e.com'), (2, 0, 'b@e.com'), (3, 0, 'c@e.com')");
	$pdo->exec("INSERT INTO `user_groups` (`name`) VALUES ('Finance Admin'), ('sales')");
	$pdo->exec('INSERT INTO `user_group_link` (`user_id`, `group_id`) VALUES (2, 1), (3, 2)');
	$pdo->exec("INSERT INTO `epc_erp_user_departments` (`user_id`, `department`) VALUES (3, 'Finance Ops')");
	$req1 = epc_mfa_required_for_user($pdo, 1);
	$req2 = epc_mfa_required_for_user($pdo, 2);
	$req3 = epc_mfa_required_for_user($pdo, 3);
	$gate = epc_mfa_cp_auth_gate($pdo, 2, '/cp/shop/finance/erp');
	$fin = epc_mfa_erp_finance_gate($pdo, 2, 'gl');
	return wave_freeze(array($def, $saved, $got, $upd, $got2, $req1, $req2, $req3, $gate, $fin));
}

function wave_run_ajax($pdo)
{
	$pdo->exec("INSERT INTO `users` (`user_id`, `type`, `email`) VALUES (9, 0, '')");
	$_POST = array('mfa_action' => 'enroll');
	$en = epc_mfa_handle_ajax($pdo, 9);
	$secret = (string) $en['secret'];
	$_POST = array('mfa_action' => 'confirm', 'code' => epc_mfa_totp_code($secret));
	$cf = epc_mfa_handle_ajax($pdo, 9);
	$_POST = array('mfa_action' => 'verify', 'code' => epc_mfa_totp_code($secret));
	$vf = epc_mfa_handle_ajax($pdo, 9);
	$sess = epc_mfa_session_verified();
	$_POST = array('mfa_action' => 'status');
	$st = epc_mfa_handle_ajax($pdo, 9);
	$st['backup_codes_left'] = (int) $st['backup_codes_left'];
	$_POST = array('mfa_action' => 'disable');
	$ds = epc_mfa_handle_ajax($pdo, 9);
	$acts = $pdo->query('SELECT `action`, `success` FROM `epc_mfa_audit_log` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($acts as &$a) {
		$a['success'] = (int) $a['success'];
	}
	unset($a);
	return wave_freeze(array($en, $cf, $vf, $sess, $st, $ds, $acts));
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
	wave_schema($pdo);
	$cleanup = function () use ($admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	require $root . '/content/general_pages/epc_auth_mfa.php';
	$_SERVER['REMOTE_ADDR'] = '198.51.100.20';
	$_SERVER['HTTP_USER_AGENT'] = 'WaveTest/1.0';
	$_SESSION = array();
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1w_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1w_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
