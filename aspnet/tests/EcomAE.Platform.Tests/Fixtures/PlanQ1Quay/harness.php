<?php
// PHP 8.3 goldens for plan Q1-quay (tenant PDO pool). Live opens use throwaway MariaDB.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function quay_err($err)
{
	$err = (string) $err;
	if ($err === '') {
		return '';
	}
	if ($err === 'Missing DB connection fields' || $err === 'Tenant DB credentials incomplete') {
		return $err;
	}
	return 'SQLSTATE';
}

function quay_pack($pdo, $err)
{
	return array(
		'ok' => $pdo instanceof PDO ? 1 : 0,
		'err' => quay_err($err),
	);
}

function quay_id($pdo)
{
	if (!$pdo instanceof PDO) {
		return 0;
	}
	try {
		return (int) $pdo->query('SELECT CONNECTION_ID()')->fetchColumn();
	} catch (Throwable $e) {
		return 0;
	}
}

function quay_dedicated($row)
{
	return epc_tenant_row_uses_dedicated_db($row) ? 1 : 0;
}

function quay_run_fields()
{
	$out = array();
	[$a, $e] = epc_tenant_pdo('', 'db', 'u', 'p');
	$out[] = quay_pack($a, $e);
	[$a, $e] = epc_tenant_pdo('h', '', 'u', 'p');
	$out[] = quay_pack($a, $e);
	[$a, $e] = epc_tenant_pdo('h', 'db', '', 'p');
	$out[] = quay_pack($a, $e);
	[$a, $e] = epc_tenant_pdo('  ', ' db ', '  ', 'p');
	$out[] = quay_pack($a, $e);
	$out[] = epc_tenant_pdo_resolve_host(array('db_host' => ' Host.Example '));
	$out[] = epc_tenant_pdo_resolve_host(array('db_host' => '0'));
	$GLOBALS['DP_Config'] = (object) array('host' => 'from-global');
	$out[] = epc_tenant_pdo_resolve_host(array('db_host' => ''));
	$out[] = epc_tenant_pdo_resolve_host(array('db_host' => '   '));
	$GLOBALS['DP_Config'] = (object) array('host' => '');
	$doc = $GLOBALS['__doc'];
	file_put_contents($doc . '/config.php', "<?php class DP_Config { public \$host = 'from-file'; }\n");
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$out[] = epc_tenant_pdo_resolve_host(array());
	unset($GLOBALS['DP_Config']);
	@unlink($doc . '/config.php');
	$out[] = epc_tenant_pdo_resolve_host(array());
	$out[] = array(
		quay_dedicated(array('dedicated_db' => 1, 'db_name' => 'docpart')),
		quay_dedicated(array('dedicated_db' => '1', 'db_name' => 'docpart')),
		quay_dedicated(array('dedicated_db' => 0, 'db_name' => 'docpart')),
		quay_dedicated(array('dedicated_db' => '0', 'db_name' => 'docpart')),
		quay_dedicated(array('scale_policy' => 'Dedicated_MySQL')),
		quay_dedicated(array('scale_policy' => ' shared ')),
		quay_dedicated(array('erp_only_shared' => 1, 'db_name' => 'acme')),
		quay_dedicated(array('erp_only_shared' => '0', 'db_name' => 'docpart')),
		quay_dedicated(array('erp_only_shared' => '1', 'db_name' => 'docpart')),
		quay_dedicated(array('db_name' => 'tenant_acme')),
		quay_dedicated(array('db' => 'OtherDb')),
		quay_dedicated(array('db_name' => 'DocPart')),
		quay_dedicated(array('db_name' => '')),
		quay_dedicated(array()),
	);
	$out[] = epc_tenant_pdo_pool_stats();
	return $out;
}

function quay_run_from_row($password, $tenantDb)
{
	if (!function_exists('epc_portal_resolve_tenant_db_credentials')) {
		function epc_portal_resolve_tenant_db_credentials()
		{
			if (empty($GLOBALS['QUAY_CREDS']) || !is_array($GLOBALS['QUAY_CREDS'])) {
				return array();
			}
			return $GLOBALS['QUAY_CREDS'];
		}
	}
	$out = array();
	[$a, $e] = epc_tenant_pdo_from_row(array());
	$out[] = quay_pack($a, $e);
	[$a, $e] = epc_tenant_pdo_from_row(array('db_host' => '127.0.0.1', 'db_name' => 'x'));
	$out[] = quay_pack($a, $e);
	[$a, $e] = epc_tenant_pdo_from_row(array(
		'db_host' => '127.0.0.1',
		'db_name' => 'no_such_db_quay',
		'db_user' => 'u',
		'db_password' => 'p',
	), array('timeout' => 1));
	$out[] = quay_pack($a, $e);
	[$a, $e] = epc_tenant_pdo_from_row(array(
		'db_host' => '127.0.0.1',
		'db' => 'no_such_db_quay',
		'user' => 'u',
		'password' => 'p',
	), array('timeout' => 1));
	$out[] = quay_pack($a, $e);
	[$a, $e] = epc_tenant_pdo_from_row(array(
		'db_host' => '127.0.0.1',
		'db_name' => 'no_such_db_quay',
		'db_user' => 'u',
		'db_pass' => 'p',
	), array('timeout' => 1));
	$out[] = quay_pack($a, $e);
	[$a, $e] = epc_tenant_pdo_from_row(array(
		'db_host' => '127.0.0.1',
		'db_name' => $tenantDb,
		'db_user' => 'ecomae',
		'db_password' => $password,
	), array('timeout' => 2));
	$out[] = quay_pack($a, $e);
	$GLOBALS['QUAY_CREDS'] = array('user' => 'ecomae', 'password' => $password, 'db' => $tenantDb);
	[$a, $e] = epc_tenant_pdo_from_row(array(
		'db_host' => '127.0.0.1',
		'db_name' => 'docpart',
		'db_user' => 'ignored',
		'db_password' => '',
	), array('timeout' => 2));
	$out[] = quay_pack($a, $e);
	$GLOBALS['QUAY_CREDS'] = array();
	[$a, $e] = epc_tenant_pdo_from_row(array(
		'db_host' => '127.0.0.1',
		'db_name' => 'docpart',
		'db_user' => 'ecomae',
		'db_password' => '',
	), array('timeout' => 1));
	$out[] = quay_pack($a, $e);
	return $out;
}

function quay_run_connect($password, $tenantDb)
{
	[$a, $e1] = epc_tenant_pdo('127.0.0.1', $tenantDb, 'ecomae', $password, array('timeout' => 2));
	$id1 = quay_id($a);
	[$b, $e2] = epc_tenant_pdo('127.0.0.1', $tenantDb, 'ecomae', $password);
	$id2 = quay_id($b);
	[$c, $e3] = epc_tenant_pdo('127.0.0.1', 'no_such_db_quay', 'ecomae', $password, array('timeout' => 1));
	if ($a instanceof PDO && $id1 > 0) {
		try {
			$a->exec('KILL CONNECTION ' . $id1);
		} catch (Throwable $e) {
		}
	}
	[$d, $e4] = epc_tenant_pdo('127.0.0.1', $tenantDb, 'ecomae', $password);
	$id4 = quay_id($d);
	[$e5p, $e5] = epc_tenant_pdo('127.0.0.1', $tenantDb, 'Ecomae', $password, array('timeout' => 1));
	return array(
		quay_pack($a, $e1),
		array('reused' => (int) ($id1 !== 0 && $id1 === $id2)),
		quay_pack($c, $e3),
		array('reconnected' => (int) ($id4 !== 0 && $id4 !== $id1)),
		quay_pack($e5p, $e5),
	);
}

function quay_run_pool($password, $dbA, $dbB, $dbC)
{
	[$a] = epc_tenant_pdo('127.0.0.1', $dbA, 'ecomae', $password);
	$idA1 = quay_id($a);
	[$b] = epc_tenant_pdo('127.0.0.1', $dbB, 'ecomae', $password);
	$idB1 = quay_id($b);
	[$c] = epc_tenant_pdo('127.0.0.1', $dbC, 'ecomae', $password);
	$idC1 = quay_id($c);
	[$b2] = epc_tenant_pdo('127.0.0.1', $dbB, 'ecomae', $password);
	$idB2 = quay_id($b2);
	[$c2] = epc_tenant_pdo('127.0.0.1', $dbC, 'ecomae', $password);
	$idC2 = quay_id($c2);
	[$a2] = epc_tenant_pdo('127.0.0.1', $dbA, 'ecomae', $password);
	$idA2 = quay_id($a2);
	return array(
		'stats' => epc_tenant_pdo_pool_stats(),
		'a_evicted' => (int) ($idA2 !== 0 && $idA2 !== $idA1),
		'b_reused' => (int) ($idB2 !== 0 && $idB2 === $idB1),
		'c_reused' => (int) ($idC2 !== 0 && $idC2 === $idC1),
	);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$tenantDb = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$dbA = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$dbB = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$dbC = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $tenantDb . '`');
	$admin->exec('CREATE DATABASE `' . $dbA . '`');
	$admin->exec('CREATE DATABASE `' . $dbB . '`');
	$admin->exec('CREATE DATABASE `' . $dbC . '`');
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1q_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	$cleanup = function () use ($admin, $tenantDb, $dbA, $dbB, $dbC, $doc) {
		foreach (array($tenantDb, $dbA, $dbB, $dbC) as $name) {
			try { $admin->exec('DROP DATABASE IF EXISTS `' . $name . '`'); } catch (Throwable $e) {}
		}
		foreach (glob($doc . '/*') ?: array() as $file) {
			@unlink($file);
		}
		@rmdir($doc);
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	if ($case['name'] === 'pool' && !defined('EPC_TENANT_PDO_POOL_MAX')) {
		define('EPC_TENANT_PDO_POOL_MAX', 2);
	}
	require $root . '/content/general_pages/epc_tenant_pdo.php';
	$GLOBALS['__doc'] = $doc;
	$GLOBALS['__db_pass'] = $password;
	$GLOBALS['__tenant_db'] = $tenantDb;
	$GLOBALS['__db_a'] = $dbA;
	$GLOBALS['__db_b'] = $dbB;
	$GLOBALS['__db_c'] = $dbC;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1quay_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1quay_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
