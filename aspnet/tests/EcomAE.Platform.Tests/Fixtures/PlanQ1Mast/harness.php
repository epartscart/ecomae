<?php
// PHP 8.3 goldens for plan Q1-mast (REST API v2). Clock / RNG patched for stable goldens.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function mast_norm_ts($value)
{
	if ($value === null || $value === '') {
		return $value;
	}
	return 'TS';
}

function mast_norm_key($row)
{
	if (!is_array($row)) {
		return $row;
	}
	foreach (array('last_used', 'created_at', 'last_request') as $k) {
		if (array_key_exists($k, $row)) {
			$row[$k] = mast_norm_ts($row[$k]);
		}
	}
	if (isset($row['key']) && is_array($row['key'])) {
		$row['key'] = mast_norm_key($row['key']);
	}
	return $row;
}

function mast_norm_list($rows)
{
	$rows = array_values($rows);
	if ($rows !== array() && isset($rows[0]['id'])) {
		usort($rows, function ($a, $b) {
			return ((int) $b['id']) <=> ((int) $a['id']);
		});
	} elseif ($rows !== array() && isset($rows[0]['site_key']) && isset($rows[0]['total_requests'])) {
		usort($rows, function ($a, $b) {
			$c = ((int) $b['total_requests']) <=> ((int) $a['total_requests']);
			return $c !== 0 ? $c : strcmp((string) $a['site_key'], (string) $b['site_key']);
		});
	} elseif ($rows !== array() && isset($rows[0]['count'])) {
		usort($rows, function ($a, $b) {
			$c = ((int) $b['count']) <=> ((int) $a['count']);
			return $c !== 0 ? $c : strcmp((string) $a['endpoint'], (string) $b['endpoint']);
		});
	}
	$out = array();
	foreach ($rows as $row) {
		$out[] = mast_norm_key($row);
	}
	return $out;
}

function mast_run_keys(PDO $pdo)
{
	$a = epc_api_key_generate($pdo, 'acme');
	$b = epc_api_key_generate($pdo, 'beta', array('scopes' => array('read', 'write'), 'label' => 'Beta write', 'rate_limit' => 10, 'created_by' => 4));
	$c = epc_api_key_generate($pdo, 'acme', array('expires_at' => '2020-01-01 00:00:00', 'label' => 'expired'));
	$d = epc_api_key_generate($pdo, 'acme', array('expires_at' => '0', 'label' => 'empty-exp'));
	$ok = epc_api_key_validate($pdo, $a['api_key']);
	$bad = epc_api_key_validate($pdo, 'epc_nope');
	$expired = epc_api_key_validate($pdo, $c['api_key']);
	$empty = epc_api_key_validate($pdo, '');
	$acme = mast_norm_list(epc_api_keys_list($pdo, 'acme'));
	$beta = mast_norm_list(epc_api_keys_list($pdo, 'beta'));
	$other = mast_norm_list(epc_api_keys_list($pdo, 'missing'));
	$revoked = epc_api_key_revoke($pdo, (int) $c['key_id']);
	$afterRevoke = epc_api_key_validate($pdo, $c['api_key']);
	return array(
		array(
			'ok' => $a['ok'],
			'key_id' => $a['key_id'],
			'api_key' => $a['api_key'],
			'prefix' => $a['prefix'],
			'scopes' => $a['scopes'],
			'warning' => $a['warning'],
			'hash_ok' => hash('sha256', $a['api_key']) === ($ok['key']['key_hash'] ?? ''),
		),
		array('ok' => $b['ok'], 'site_via_validate' => epc_api_key_validate($pdo, $b['api_key'])['key']['site_key'] ?? '', 'scopes' => $b['scopes'], 'prefix' => $b['prefix']),
		mast_norm_key($ok),
		$bad,
		$expired,
		$empty,
		$acme,
		$beta,
		$other,
		$revoked,
		$afterRevoke,
		$d['ok'],
		mast_norm_list(epc_api_keys_list($pdo, 'acme')),
	);
}

function mast_run_rate(PDO $pdo)
{
	$gen = epc_api_key_generate($pdo, 'acme', array('rate_limit' => 2, 'label' => 'rate'));
	$id = (int) $gen['key_id'];
	$one = epc_api_rate_check($pdo, $id, 2);
	$two = epc_api_rate_check($pdo, $id, 2);
	$three = epc_api_rate_check($pdo, $id, 2);
	$other = epc_api_rate_check($pdo, $id + 99, 5);
	return array($one, $two, $three, $other);
}

function mast_run_handle(PDO $pdo)
{
	$read = epc_api_key_generate($pdo, 'acme', array('scopes' => array('read'), 'rate_limit' => 50));
	$admin = epc_api_key_generate($pdo, 'beta', array('scopes' => array('admin'), 'rate_limit' => 50));
	$star = epc_api_key_generate($pdo, 'acme', array('scopes' => array('*'), 'rate_limit' => 50));
	$out = array();
	$_SERVER = array();
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/products');
	$_SERVER['HTTP_AUTHORIZATION'] = 'Bearer ' . $read['api_key'];
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/products', array('site_key' => 'beta', 'page' => '2', 'per_page' => '7abc'));
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/products/99');
	$out[] = epc_api_v2_handle($pdo, 'POST', '/api/v2/products');
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/nope');
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/invoices');
	$_SERVER = array();
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/products', array('api_key' => $read['api_key']));
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/products', array('api_key' => '0'));
	$_SERVER['HTTP_AUTHORIZATION'] = 'bearer ' . $read['api_key'];
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/products');
	$_SERVER['HTTP_AUTHORIZATION'] = 'Bearer ' . $admin['api_key'];
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/invoices');
	$out[] = epc_api_v2_handle($pdo, 'put', '/api/v2/inventory/SKU-1');
	$_SERVER['HTTP_AUTHORIZATION'] = 'Bearer ' . $star['api_key'];
	$out[] = epc_api_v2_handle($pdo, 'POST', '/api/v2/webhooks');
	$_SERVER['HTTP_AUTHORIZATION'] = 'Bearer bad-key';
	$out[] = epc_api_v2_handle($pdo, 'GET', '/api/v2/products');
	return $out;
}

function mast_run_meta(PDO $pdo)
{
	$eps = epc_api_v2_endpoints();
	$spec = epc_api_v2_openapi_spec();
	$read = epc_api_key_generate($pdo, 'acme', array('scopes' => array('read')));
	$_SERVER['HTTP_AUTHORIZATION'] = 'Bearer ' . $read['api_key'];
	epc_api_v2_handle($pdo, 'GET', '/api/v2/products');
	epc_api_v2_handle($pdo, 'GET', '/api/v2/nope');
	$beta = epc_api_key_generate($pdo, 'beta', array('scopes' => array('read')));
	$_SERVER['HTTP_AUTHORIZATION'] = 'Bearer ' . $beta['api_key'];
	epc_api_v2_handle($pdo, 'GET', '/api/v2/orders');
	$pdo->prepare('INSERT INTO `epc_api_logs` (`key_id`,`site_key`,`method`,`endpoint`,`status_code`,`response_ms`,`created_at`) VALUES (9,?,?,?,?,?,DATE_SUB(NOW(), INTERVAL 25 HOUR))')
		->execute(array('acme', 'GET', '/old', 200, 5));
	epc_api_log($pdo, array('key_id' => 1, 'site_key' => 'acme', 'method' => 'GET', 'endpoint' => '/x', 'status_code' => 200, 'user_agent' => str_repeat('U', 300), 'request_body' => 'body'));
	return array(
		count($eps),
		$eps[0],
		$eps[count($eps) - 1],
		epc_api_has_scope(array('read'), 'read'),
		epc_api_has_scope(array('read'), 'write'),
		epc_api_has_scope(array('admin'), 'finance'),
		epc_api_has_scope(array('*'), 'reports'),
		epc_api_v2_error(401, 'Missing API key. Use Authorization: Bearer <key>'),
		$spec['info']['version'],
		$spec['paths']['/api/v2/products/{id}']['get']['tags'],
		count($spec['paths']),
		mast_norm_list(epc_api_fleet_stats($pdo)),
		mast_norm_list(epc_api_usage_by_endpoint($pdo, 'acme', 24)),
		mast_norm_list(epc_api_usage_by_endpoint($pdo, 'beta', 24)),
	);
}

function mast_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace('bin2hex(random_bytes(24))', 'bin2hex(str_repeat("\x0a", 23) . chr(((int) ($GLOBALS["MAST_SEQ"]++)) & 255))', $code);
	$code = str_replace("date('Y-m-d H:00:00')", "date('Y-m-d H:00:00', (int) (\$GLOBALS['MAST_NOW'] ?? time()))", $code);
	$code = str_replace("strtotime(\$key['expires_at']) < time()", "strtotime(\$key['expires_at']) < (int) (\$GLOBALS['MAST_NOW'] ?? time())", $code);
	$code = str_replace('microtime(true)', '(float) ($GLOBALS[\'MAST_MICRO\'] ?? microtime(true))', $code);
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
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1m_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	mast_patch($root . '/content/general_pages/epc_rest_api_v2.php', $doc . '/epc_rest_api_v2.php');
	$cleanup = function () use ($admin, $dbName, $doc) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		foreach (glob($doc . '/*') ?: array() as $file) { @unlink($file); }
		@rmdir($doc);
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['MAST_NOW'] = 1760083200;
	$GLOBALS['MAST_MICRO'] = 1000.0;
	$GLOBALS['MAST_SEQ'] = 0;
	$GLOBALS['db_link'] = $pdo;
	$_SERVER = array();
	require $doc . '/epc_rest_api_v2.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1mast_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1mast_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
