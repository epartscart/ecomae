<?php
// PHP 8.3 goldens for plan Q1-yard (tenant readiness score). Design-token leftover stays stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function yard_norm($row)
{
	if (!is_array($row)) {
		return $row;
	}
	if (isset($row['generated_at'])) {
		$row['generated_at'] = 'ISO';
	}
	if (isset($row['tenants']) && is_array($row['tenants'])) {
		foreach ($row['tenants'] as $i => $tenant) {
			$row['tenants'][$i] = yard_norm($tenant);
		}
	}
	return $row;
}

function yard_schema(PDO $pdo): void
{
	$pdo->exec('CREATE TABLE `epc_settings` (
		`site_key` VARCHAR(64) NOT NULL,
		`setting_key` VARCHAR(64) NOT NULL,
		`setting_value` TEXT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `epc_portal_tenants` (
		`site_key` VARCHAR(64) NOT NULL,
		`trade_name` VARCHAR(255) NOT NULL DEFAULT \'\',
		`status` VARCHAR(32) NOT NULL DEFAULT \'\',
		`industry_code` VARCHAR(64) NOT NULL DEFAULT \'\',
		`erp_enabled` TINYINT(1) NOT NULL DEFAULT 0
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
	$pdo->exec('CREATE TABLE `epc_webhooks` (
		`tenant_key` VARCHAR(64) NOT NULL,
		`active` TINYINT(1) NOT NULL DEFAULT 0
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function yard_set(PDO $pdo, $site, $key, $value): void
{
	$pdo->prepare('INSERT INTO `epc_settings` (`site_key`,`setting_key`,`setting_value`) VALUES (?,?,?)')
		->execute(array($site, $key, $value));
}

function yard_ids($score)
{
	$out = array();
	foreach ($score['checks'] as $check) {
		$out[$check['id']] = array(
			'status' => $check['status'],
			'earned' => $check['earned'],
			'detail' => $check['detail'],
		);
	}
	return array(
		'site_key' => $score['site_key'],
		'score' => $score['score'],
		'tier' => $score['tier'],
		'tier_label' => $score['tier_label'],
		'total_weight' => $score['total_weight'],
		'earned_weight' => $score['earned_weight'],
		'generated_at' => 'ISO',
		'checks' => $out,
	);
}

function yard_run_tiers(PDO $pdo)
{
	yard_schema($pdo);
	$empty = yard_ids(epc_readiness_score($pdo, 'missing'));
	yard_set($pdo, 'pilot', 'isolation_audit_status', 'warn');
	yard_set($pdo, 'pilot', 'homepage_load_ms', '1500');
	$pilot = yard_ids(epc_readiness_score($pdo, 'pilot'));
	yard_set($pdo, 'paid', 'isolation_audit_status', 'pass');
	yard_set($pdo, 'paid', 'mfa_enabled', '1');
	yard_set($pdo, 'paid', 'last_backup_time', (string) (1760083200 - 3600));
	yard_set($pdo, 'paid', 'einvoice_asp_mode', 'api');
	yard_set($pdo, 'paid', 'homepage_load_ms', '900');
	yard_set($pdo, 'paid', 'vat_trn', '100');
	$paid = yard_ids(epc_readiness_score($pdo, 'paid'));
	yard_set($pdo, 'ent', 'isolation_audit_status', 'ok');
	yard_set($pdo, 'ent', 'mfa_enabled', '1');
	yard_set($pdo, 'ent', 'last_backup_time', (string) (1760083200 - 1800));
	yard_set($pdo, 'ent', 'einvoice_asp_mode', 'live');
	yard_set($pdo, 'ent', 'homepage_load_ms', '800');
	yard_set($pdo, 'ent', 'vat_trn', '100');
	yard_set($pdo, 'ent', 'trade_license', 'TL-1');
	yard_set($pdo, 'ent', 'erp_modules_active', '["a","b","c","d","e"]');
	yard_set($pdo, 'ent', 'brand_logo', 'x');
	yard_set($pdo, 'ent', 'brand_color', 'y');
	yard_set($pdo, 'ent', 'brand_font', 'z');
	$pdo->prepare('INSERT INTO `epc_webhooks` (`tenant_key`,`active`) VALUES (?,1)')->execute(array('ent'));
	$ent = yard_ids(epc_readiness_score($pdo, 'ent'));
	return array(
		epc_readiness_tier_label('demo'),
		epc_readiness_tier_label('pilot'),
		epc_readiness_tier_label('paid'),
		epc_readiness_tier_label('enterprise'),
		epc_readiness_tier_label('nope'),
		$empty,
		$pilot,
		$paid,
		$ent,
	);
}

function yard_run_checks(PDO $pdo)
{
	yard_schema($pdo);
	yard_set($pdo, 'acme', 'isolation_audit_status', 'pass');
	yard_set($pdo, 'beta', 'isolation_audit_status', 'fail');
	yard_set($pdo, 'warn', 'isolation_audit_status', 'warn');
	yard_set($pdo, 'acme', 'mfa_enabled', '1');
	yard_set($pdo, 'beta', 'mfa_enabled', '0');
	yard_set($pdo, 'acme', 'last_backup_time', (string) (1760083200 - 7200));
	yard_set($pdo, 'old', 'last_backup_time', (string) (1760083200 - 100000));
	yard_set($pdo, 'stale', 'last_backup_time', (string) (1760083200 - 200000));
	yard_set($pdo, 'zero', 'last_backup_time', '0');
	yard_set($pdo, 'acme', 'einvoice_asp_mode', 'api');
	yard_set($pdo, 'man', 'einvoice_asp_mode', 'manual');
	yard_set($pdo, 'tst', 'einvoice_asp_mode', 'test');
	yard_set($pdo, 'fast', 'homepage_load_ms', '1999');
	yard_set($pdo, 'slow', 'homepage_load_ms', '2000');
	yard_set($pdo, 'dead', 'homepage_load_ms', '5000');
	yard_set($pdo, 'half', 'vat_trn', 'TRN');
	yard_set($pdo, 'full', 'vat_trn', 'TRN');
	yard_set($pdo, 'full', 'trade_license', 'LIC');
	yard_set($pdo, 'blank', 'vat_trn', '');
	yard_set($pdo, 'one', 'brand_logo', 'a');
	yard_set($pdo, 'three', 'brand_logo', 'a');
	yard_set($pdo, 'three', 'brand_color', 'b');
	yard_set($pdo, 'three', 'brand_font', 'c');
	$pdo->prepare('INSERT INTO `epc_webhooks` (`tenant_key`,`active`) VALUES (?,1), (?,0), (?,1)')
		->execute(array('hook', 'hook', 'hook2'));
	$out = array();
	foreach (array('acme', 'beta', 'warn', 'old', 'stale', 'zero', 'man', 'tst', 'fast', 'slow', 'dead', 'half', 'full', 'blank', 'one', 'three', 'hook', 'hook2') as $site) {
		$out[$site] = yard_ids(epc_readiness_score($pdo, $site));
	}
	$out['iso_direct'] = array(
		epc_readiness_check_isolation($pdo, 'acme'),
		epc_readiness_check_isolation($pdo, 'beta'),
		epc_readiness_check_isolation($pdo, 'missing'),
	);
	return $out;
}

function yard_run_erp(PDO $pdo)
{
	yard_schema($pdo);
	yard_set($pdo, 'five', 'erp_modules_active', '["a","b","c","d","e"]');
	yard_set($pdo, 'three', 'erp_modules_active', '["a","b","c"]');
	yard_set($pdo, 'empty', 'erp_modules_active', '[]');
	yard_set($pdo, 'bad', 'erp_modules_active', '{');
	yard_set($pdo, 'blank', 'erp_modules_active', '');
	$pdo->prepare('INSERT INTO `epc_portal_tenants` (`site_key`,`trade_name`,`status`,`industry_code`,`erp_enabled`) VALUES (?,?,?,?,?)')
		->execute(array('erp1', 'ERP One', 'live', 'auto', 1));
	$pdo->prepare('INSERT INTO `epc_portal_tenants` (`site_key`,`trade_name`,`status`,`industry_code`,`erp_enabled`) VALUES (?,?,?,?,?)')
		->execute(array('site1', 'Site One', 'live', 'retail', 0));
	$out = array();
	foreach (array('five', 'three', 'empty', 'bad', 'blank', 'erp1', 'site1') as $site) {
		$score = epc_readiness_score($pdo, $site);
		$erp = null;
		foreach ($score['checks'] as $check) {
			if ($check['id'] === 'erp_modules') {
				$erp = $check;
			}
		}
		$out[$site] = $erp;
	}
	$pdo->exec('DROP TABLE `epc_settings`');
	$missing = epc_readiness_check_erp_modules($pdo, 'erp1');
	$pdo->exec('DROP TABLE `epc_webhooks`');
	$hooks = epc_readiness_check_webhooks($pdo, 'hook');
	return array($out, $missing, $hooks);
}

function yard_run_fleet(PDO $pdo)
{
	yard_schema($pdo);
	$ins = $pdo->prepare('INSERT INTO `epc_portal_tenants` (`site_key`,`trade_name`,`status`,`industry_code`,`erp_enabled`) VALUES (?,?,?,?,?)');
	$ins->execute(array('acme', 'Acme Parts', 'live', 'auto', 1));
	$ins->execute(array('beta', 'Beta Shop', 'live', 'retail', 0));
	$ins->execute(array('draft', 'Draft Co', 'draft', 'auto', 1));
	yard_set($pdo, 'acme', 'isolation_audit_status', 'pass');
	yard_set($pdo, 'acme', 'mfa_enabled', '1');
	yard_set($pdo, 'acme', 'last_backup_time', (string) (1760083200 - 1000));
	yard_set($pdo, 'acme', 'einvoice_asp_mode', 'live');
	yard_set($pdo, 'acme', 'homepage_load_ms', '700');
	yard_set($pdo, 'acme', 'vat_trn', '1');
	yard_set($pdo, 'acme', 'trade_license', '1');
	yard_set($pdo, 'acme', 'erp_modules_active', '["a","b","c","d","e"]');
	yard_set($pdo, 'acme', 'brand_logo', 'a');
	yard_set($pdo, 'acme', 'brand_color', 'b');
	yard_set($pdo, 'acme', 'brand_font', 'c');
	$pdo->prepare('INSERT INTO `epc_webhooks` (`tenant_key`,`active`) VALUES (?,1)')->execute(array('acme'));
	yard_set($pdo, 'beta', 'isolation_audit_status', 'fail');
	$GLOBALS['YARD_TOKENS'] = array('beta' => array('logo' => 1));
	$none = yard_norm(epc_readiness_fleet_summary($pdo));
	$pdo->exec('DELETE FROM `epc_portal_tenants`');
	$empty = yard_norm(epc_readiness_fleet_summary($pdo));
	$pdo->exec('DROP TABLE `epc_portal_tenants`');
	$missing = yard_norm(epc_readiness_fleet_summary($pdo));
	return array($none, $empty, $missing, yard_ids(epc_readiness_score($pdo, 'beta')));
}

function yard_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("date('c')", "date('c', (int) (\$GLOBALS['YARD_NOW'] ?? time()))", $code);
	$code = str_replace('time() - $lastBackup', '((int) ($GLOBALS[\'YARD_NOW\'] ?? time())) - $lastBackup', $code);
	file_put_contents($dest, $code);
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
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1y_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	yard_patch($root . '/content/general_pages/epc_readiness_score.php', $doc . '/epc_readiness_score.php');
	$cleanup = function () use ($admin, $dbName, $doc) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		foreach (glob($doc . '/*') ?: array() as $file) { @unlink($file); }
		@rmdir($doc);
	};
	if (!function_exists('epc_design_tokens_tenant_catalog')) {
		eval('function epc_design_tokens_tenant_catalog() { return is_array($GLOBALS["YARD_TOKENS"] ?? null) ? $GLOBALS["YARD_TOKENS"] : array(); }');
	}
	$GLOBALS['YARD_NOW'] = 1760083200;
	$GLOBALS['YARD_TOKENS'] = array();
	$GLOBALS['db_link'] = $pdo;
	$_SERVER = array();
	require $doc . '/epc_readiness_score.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1yard_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1yard_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
