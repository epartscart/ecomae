<?php
/**
 * Tenant DB connect probe + registry repair.
 * epartscart may use shared docpart; taxofinca and other clients must be dedicated
 * (see epc-client-tenant-db-isolate.php). This script must NEVER re-bind them to docpart.
 * https://www.ecomae.com/epc-tenant-db-connect-fix.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$apply = !empty($_GET['apply']);
$hosts = array('www.epartscart.com', 'www.taxofinca.com');
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';

echo "=== Tenant DB connect fix ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'docroot=' . ($_SERVER['DOCUMENT_ROOT'] ?? '') . "\n\n";

function epc_tdbf_mysqli_test(string $db, string $user, string $pass): array
{
	$mysqli = @new mysqli('127.0.0.1', $user, $pass, $db);
	if ($mysqli->connect_errno) {
		return array('ok' => false, 'message' => $mysqli->connect_error);
	}
	$tables = 0;
	$res = $mysqli->query('SHOW TABLES');
	if ($res instanceof mysqli_result) {
		$tables = $res->num_rows;
		$res->free();
	}
	$mysqli->close();
	return array('ok' => true, 'message' => 'tables=' . $tables);
}

function epc_tdbf_probe_host(string $host): array
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
	require_once __DIR__ . '/config.php';
	$cfg = new DP_Config();
	if (function_exists('epc_portal_resolve_tenant_db')) {
		epc_portal_resolve_tenant_db($cfg);
	}
	$db = (string) ($cfg->db ?? '');
	$user = (string) ($cfg->user ?? '');
	$pass = (string) ($cfg->password ?? '');
	$isolation = (string) ($GLOBALS['epc_tenant_db_isolation_error'] ?? '');
	if ($db === '' || $user === '' || $pass === '') {
		return array(
			'host' => $host,
			'db' => $db,
			'user' => $user,
			'pass_len' => strlen($pass),
			'domain_path' => 'https://' . $host . '/',
			'ok' => false,
			'detail' => $isolation !== '' ? $isolation : 'empty_credentials (dedicated DB required)',
		);
	}
	$test = epc_tdbf_mysqli_test($db, $user, $pass);
	return array(
		'host' => $host,
		'db' => $db,
		'user' => $user,
		'pass_len' => strlen($pass),
		'domain_path' => 'https://' . $host . '/',
		'ok' => $test['ok'],
		'detail' => $test['message'],
	);
}

function epc_tdbf_find_working_docpart_pass(string $platformDocroot): string
{
	$candidates = array();
	$resolved = epc_portal_resolve_tenant_db_credentials();
	if (!empty($resolved['password'])) {
		$candidates[] = (string) $resolved['password'];
	}
	require_once __DIR__ . '/config.php';
	$cfg = new DP_Config();
	if (!empty($cfg->password)) {
		$candidates[] = (string) $cfg->password;
	}
	foreach (array(
		'/home/epartscart/htdocs/www.epartscart.com/config.local.php',
		'/home/epartscart/htdocs/www.epartscart.com/config.php',
		rtrim($platformDocroot, '/') . '/config.php',
	) as $path) {
		if (!is_file($path)) {
			continue;
		}
		if (substr($path, -13) === 'config.local.php') {
			$epc_config_local = null;
			include $path;
			if (!empty($epc_config_local['password'])) {
				$candidates[] = (string) $epc_config_local['password'];
			}
			continue;
		}
		if (preg_match('/public\s+\$password\s*=\s*[\'"]([^\'"]+)[\'"]/', (string) file_get_contents($path), $m)) {
			$candidates[] = $m[1];
		}
	}
	foreach (array('EpC4rt_Db_2026_xK9mQ2') as $p) {
		$candidates[] = $p;
	}
	$candidates = array_values(array_unique(array_filter($candidates)));
	foreach ($candidates as $pass) {
		$t = epc_tdbf_mysqli_test('docpart', 'docpart', $pass);
		if ($t['ok']) {
			return $pass;
		}
	}
	return '';
}

echo "=== Docpart mysqli (resolve_tenant_db_credentials) ===\n";
$resolved = epc_portal_resolve_tenant_db_credentials();
echo 'resolved db=' . $resolved['db'] . ' user=' . $resolved['user'] . ' pass_len=' . strlen($resolved['password']) . "\n";
$baseTest = epc_tdbf_mysqli_test($resolved['db'], $resolved['user'], $resolved['password']);
echo 'mysqli: ' . ($baseTest['ok'] ? 'OK ' . $baseTest['message'] : 'FAIL ' . $baseTest['message']) . "\n\n";

echo "=== Portal apply_config per host (before apply) ===\n";
foreach ($hosts as $host) {
	$row = epc_tdbf_probe_host($host);
	echo $row['host'] . ': db=' . $row['db'] . ' user=' . $row['user'] . ' pass_len=' . $row['pass_len']
		. ' domain_path=' . $row['domain_path'] . ' → ' . ($row['ok'] ? 'OK ' . $row['detail'] : 'FAIL ' . $row['detail']) . "\n";
}
echo "\n";

if (!$apply) {
	echo "Dry run. Re-run with apply=1 to fix Super CP registry + re-probe.\n";
	echo "Optional: db_password= (ecomae MySQL) if platform registry update needed.\n";
	exit;
}

$workingPass = epc_tdbf_find_working_docpart_pass($platformDocroot);
if ($workingPass === '') {
	echo "BLOCKER: no working docpart password found. Run epc-docpart-db-fix.php?apply=1&clp_pass=...\n";
	exit(1);
}
echo "Working docpart password len=" . strlen($workingPass) . "\n\n";

$ecomaePass = trim((string) ($_GET['db_password'] ?? ''));
if ($ecomaePass === '' && is_file($platformDocroot . '/config.local.php')) {
	$epc_config_local = null;
	include $platformDocroot . '/config.local.php';
	if (!empty($epc_config_local['password'])) {
		$ecomaePass = (string) $epc_config_local['password'];
	}
}

if ($ecomaePass !== '') {
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
			'ecomae',
			$ecomaePass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		epc_portal_db_ensure($pdo);
		foreach (epc_portal_tenant_templates() as $key => $tpl) {
			if (!empty($tpl['erp_only_shared']) || (string) ($tpl['hosted_on'] ?? '') === 'platform') {
				echo "registry {$key}: SKIP (shared ERP / platform)\n";
				continue;
			}
			// Never re-bind taxofinca/etc. onto shared docpart.
			if ($key !== 'epartscart') {
				echo "registry {$key}: SKIP (dedicated isolate required — epc-client-tenant-db-isolate.php)\n";
				continue;
			}
			$save = epc_portal_save_tenant($pdo, array(
				'site_key' => $key,
				'hostname' => $tpl['hostname'],
				'industry_code' => $tpl['industry'],
				'status' => 'live',
				'trade_name' => $tpl['trade_name'],
				'hub_name' => $tpl['hub_name'],
				'from_email' => $tpl['from_email'],
				'db_name' => 'docpart',
				'db_user' => 'docpart',
				'db_password' => $workingPass,
				'hosted_on' => 'client',
				'erp_only_shared' => 0,
				'notes' => 'epc-tenant-db-connect-fix.php (epartscart only)',
			));
			echo "registry {$key}: " . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
		}
	} catch (Exception $e) {
		echo 'Super CP registry: FAIL — ' . $e->getMessage() . "\n";
	}
} else {
	echo "Super CP registry skipped — pass db_password= for ecomae MySQL\n";
}

try {
	$docPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=docpart;charset=utf8',
		'docpart',
		$workingPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_portal_db_ensure($docPdo);
	echo "docpart epc_portal_site_settings table: OK\n";
} catch (Exception $e) {
	echo 'docpart site_settings: ' . $e->getMessage() . "\n";
}

echo "\n=== AFTER apply_config + mysqli ===\n";
$allOk = true;
foreach ($hosts as $host) {
	$row = epc_tdbf_probe_host($host);
	$line = $row['host'] . ': ' . ($row['ok'] ? 'OK ' . $row['detail'] : 'FAIL ' . $row['detail'])
		. ' (db=' . $row['db'] . ' pass_len=' . $row['pass_len'] . ')';
	echo $line . "\n";
	if (!$row['ok']) {
		$allOk = false;
	}
}

echo "\n=== HTTP probe (origin) ===\n";
foreach ($hosts as $host) {
	foreach (array('/', '/cp/') as $path) {
		$ctx = stream_context_create(array(
			'http' => array('timeout' => 15, 'ignore_errors' => true, 'header' => "Host: {$host}\r\n"),
		));
		$body = @file_get_contents('http://127.0.0.1' . $path, false, $ctx);
		$code = 0;
		if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
			$code = (int) $m[1];
		}
		$snippet = is_string($body) ? substr($body, 0, 80) : '';
		$bad = is_string($body) && stripos($body, 'No DB connect') !== false;
		echo "  {$host}{$path}: HTTP {$code}" . ($bad ? ' [no-db]' : '') . ' ' . trim(preg_replace('/\s+/', ' ', $snippet)) . "\n";
		if ($bad) {
			$allOk = false;
		}
	}
}

echo "\n=== Summary ===\n";
echo $allOk ? "PASS — tenant DB routing OK\n" : "FAIL — check docpart MySQL user/password or deploy portal files\n";
