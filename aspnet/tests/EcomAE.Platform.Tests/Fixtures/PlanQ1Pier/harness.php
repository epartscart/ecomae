<?php
// PHP 8.3 goldens for plan Q1-pier (platform job queue). Leftover parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function pier_freeze($value)
{
	if (is_string($value)) {
		return preg_replace('/ecomae_cpw_[a-f0-9]+/', 'TENANT_DB', $value);
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = pier_freeze($v);
		}
		return $out;
	}
	return $value;
}

function pier_snap(PDO $pdo, $id)
{
	$st = $pdo->prepare('SELECT `id`,`job_type`,`tenant_key`,`payload_json`,`status`,`priority`,`attempts`,`max_attempts`,`locked_by`,`last_error`,`result_json` FROM `epc_platform_jobs` WHERE `id`=?');
	$st->execute(array($id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	$row['id'] = (int) $row['id'];
	$row['priority'] = (int) $row['priority'];
	$row['attempts'] = (int) $row['attempts'];
	$row['max_attempts'] = (int) $row['max_attempts'];
	return $row;
}

function pier_run_enqueue(PDO $pdo)
{
	$empty = epc_platform_jobs_enqueue('');
	$a = epc_platform_jobs_enqueue('NoOp', 'Alpha', array('n' => 1), array('priority' => 10));
	$b = epc_platform_jobs_enqueue('noop', 'alpha', array('n' => 2), array('dedupe' => 1));
	$c = epc_platform_jobs_enqueue('scan', 'beta', array(), array('delay_sec' => 0, 'max_attempts' => 2));
	$d = epc_platform_jobs_enqueue('scan', 'gamma', array('x' => 'y'), array('dedupe' => '0'));
	return pier_freeze(array($empty, $a, $b, $c, $d, pier_snap($pdo, $a), pier_snap($pdo, $c), pier_snap($pdo, $d)));
}

function pier_run_claim(PDO $pdo)
{
	epc_platform_jobs_enqueue('noop', 'alpha', array('a' => 1), array('priority' => 50));
	epc_platform_jobs_enqueue('noop', 'beta', array('b' => 1), array('priority' => 10));
	$claimed = epc_platform_jobs_claim('w1!', 1);
	$ids = array();
	foreach ($claimed as $row) {
		$ids[] = (int) $row['id'];
	}
	if ($ids) {
		epc_platform_jobs_complete($ids[0], array('ok' => true));
	}
	epc_platform_jobs_enqueue('noop', 'gamma', array(), array('priority' => 1));
	$claimed2 = epc_platform_jobs_claim('worker-two', 5);
	$failRetry = (int) ($claimed2[0]['id'] ?? 0);
	$failDead = (int) ($claimed2[1]['id'] ?? 0);
	epc_platform_jobs_fail($failRetry, 'boom', true);
	epc_platform_jobs_fail($failDead, str_repeat('e', 12), false);
	return pier_freeze(array(
		array_map(function ($r) { return array('id' => (int) $r['id'], 'tenant_key' => $r['tenant_key'], 'status' => $r['status'], 'locked_by' => $r['locked_by'], 'priority' => (int) $r['priority']); }, $claimed),
		pier_snap($pdo, $ids[0] ?? 0),
		array_map(function ($r) { return array('id' => (int) $r['id'], 'tenant_key' => $r['tenant_key'], 'attempts' => (int) $r['attempts']); }, $claimed2),
		pier_snap($pdo, $failRetry),
		pier_snap($pdo, $failDead),
	));
}

function pier_run_dispatch(PDO $pdo)
{
	$GLOBALS['PIER_TENANTS'] = array(
		'alpha' => array('site_key' => 'alpha', 'db_name' => 'alpha', 'dedicated_db' => 1),
	);
	epc_platform_jobs_register_handler('custom', function ($tenant, $payload, $job) {
		return array('ok' => true, 'result' => array('tenant' => $tenant, 'n' => $payload['n'] ?? 0));
	});
	$noop = epc_platform_jobs_dispatch(array('job_type' => 'noop', 'tenant_key' => 'alpha', 'payload_json' => json_encode(array('echo' => 1))));
	$custom = epc_platform_jobs_dispatch(array('job_type' => 'custom', 'tenant_key' => 'Alpha', 'payload_json' => json_encode(array('n' => 7))));
	$unk = epc_platform_jobs_dispatch(array('job_type' => 'nope', 'tenant_key' => 'alpha'));
	$miss = epc_platform_jobs_handle_tenant_health_ping('', array());
	$none = epc_platform_jobs_handle_tenant_health_ping('beta', array());
	$ok = epc_platform_jobs_handle_tenant_health_ping('alpha', array());
	$warm = epc_platform_jobs_handle_tenant_warmup('alpha', array());
	$chain = epc_platform_jobs_dispatch(array('job_type' => 'blockchain_anchor_batch', 'tenant_key' => 'alpha', 'payload_json' => '{}'));
	return pier_freeze(array($noop, $custom, $unk, $miss, $none, $ok, $warm, $chain));
}

function pier_run_batch(PDO $pdo)
{
	epc_platform_jobs_enqueue('noop', 'alpha', array('z' => 1));
	epc_platform_jobs_enqueue('nope', 'beta');
	$out = epc_platform_jobs_run_batch(10, 'cron-test');
	$rows = $pdo->query('SELECT `job_type`,`tenant_key`,`status`,`last_error` IS NOT NULL AS has_err FROM `epc_platform_jobs` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$r) {
		$r['has_err'] = (int) $r['has_err'];
	}
	return pier_freeze(array($out, $rows));
}

function pier_write_stubs($stub)
{
	file_put_contents($stub . '/epc_portal_tenant.php', "<?php\nfunction epc_portal_platform_pdo(){return \$GLOBALS['db_link'] ?? null;}\n");
	file_put_contents($stub . '/epc_portal_tenant_intro.php', "<?php\nfunction epc_portal_tenant_get(\$pdo,\$key){\$key=strtolower(trim(\$key));return \$GLOBALS['PIER_TENANTS'][\$key] ?? null;}\n");
	file_put_contents($stub . '/epc_tenant_pdo.php', "<?php\nfunction epc_tenant_pdo_from_row(\$row,\$opts=array()){if(!\$row){return array(null,'DB connect failed');}return array(\$GLOBALS['db_link'],'');}\nfunction epc_tenant_row_uses_dedicated_db(\$row){return !empty(\$row['dedicated_db']);}\n");
	file_put_contents($stub . '/epc_blockchain_bos.php', "<?php\nfunction epc_bc_bos_job_anchor_batch(\$t,\$p,\$j){return array('ok'=>true,'result'=>array('anchored'=>1,'tenant'=>\$t));}\n");
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
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8mb4', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$cleanup = function () use ($admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$stub = sys_get_temp_dir() . '/pier_' . substr(md5(uniqid('', true)), 0, 8);
	mkdir($stub, 0777, true);
	pier_write_stubs($stub);
	copy($root . '/content/general_pages/epc_platform_jobs.php', $stub . '/epc_platform_jobs.php');
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['PIER_TENANTS'] = array();
	require $stub . '/epc_platform_jobs.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1pier_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1pier_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
