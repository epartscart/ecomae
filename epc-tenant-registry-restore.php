<?php
/**
 * Idempotent restore of canonical Super CP tenant registry (Model C platform).
 * https://www.ecomae.com/epc-tenant-registry-restore.php?token=...&apply=1
 *
 * Optional: asap_db_password=... if asap MySQL password is not already in registry.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(180);
@ini_set('default_socket_timeout', '5');

require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';

$apply = !empty($_GET['apply']);
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';

echo "=== EPC tenant registry restore ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'time=' . gmdate('Y-m-d H:i:s') . " UTC\n\n";

/** @return array<string, array<string, mixed>> */
function epc_trr_canonical_tenants(): array
{
	$tpl = epc_portal_tenant_templates();
	return array(
		'epartscart' => array(
			'site_key' => 'epartscart',
			'hostname' => 'www.epartscart.com',
			'industry_code' => 'auto_parts',
			'status' => 'live',
			'trade_name' => $tpl['epartscart']['trade_name'],
			'hub_name' => $tpl['epartscart']['hub_name'],
			'from_email' => $tpl['epartscart']['from_email'],
			'db_name' => 'docpart',
			'db_user' => 'docpart',
			'erp_only_shared' => 0,
			'hosted_on' => 'client',
			'notes' => 'Canonical registry restore — auto parts storefront',
		),
		'taxofinca' => array(
			'site_key' => 'taxofinca',
			'hostname' => 'www.taxofinca.com',
			'industry_code' => 'tax_advisory',
			'status' => 'live',
			'trade_name' => $tpl['taxofinca']['trade_name'],
			'hub_name' => $tpl['taxofinca']['hub_name'],
			'from_email' => $tpl['taxofinca']['from_email'],
			'db_name' => 'docpart',
			'db_user' => 'docpart',
			'erp_only_shared' => 0,
			'hosted_on' => 'client',
			'notes' => 'Canonical registry restore — tax advisory',
		),
		'electronicae' => array(
			'site_key' => 'electronicae',
			'hostname' => 'www.electronicae.com',
			'industry_code' => 'electronics',
			'status' => 'live',
			'trade_name' => $tpl['electronicae']['trade_name'],
			'hub_name' => $tpl['electronicae']['hub_name'],
			'from_email' => $tpl['electronicae']['from_email'],
			'db_name' => 'docpart',
			'db_user' => 'docpart',
			'erp_only_shared' => 0,
			'hosted_on' => 'client',
			'notes' => 'Canonical registry restore — electronics retail',
		),
		'stylenlook' => array(
			'site_key' => 'stylenlook',
			'hostname' => 'www.stylenlook.com',
			'industry_code' => 'fashion',
			'status' => 'live',
			'trade_name' => $tpl['stylenlook']['trade_name'],
			'hub_name' => $tpl['stylenlook']['hub_name'],
			'from_email' => $tpl['stylenlook']['from_email'],
			'db_name' => 'docpart',
			'db_user' => 'docpart',
			'erp_only_shared' => 0,
			'hosted_on' => 'client',
			'notes' => 'Canonical registry restore — fashion retail',
		),
		'thejewellerytrend' => array(
			'site_key' => 'thejewellerytrend',
			'hostname' => 'www.thejewellerytrend.com',
			'industry_code' => 'jewellery',
			'status' => 'live',
			'trade_name' => $tpl['thejewellerytrend']['trade_name'],
			'hub_name' => $tpl['thejewellerytrend']['hub_name'],
			'from_email' => $tpl['thejewellerytrend']['from_email'],
			'db_name' => 'docpart',
			'db_user' => 'docpart',
			'erp_only_shared' => 0,
			'hosted_on' => 'client',
			'notes' => 'Canonical registry restore — jewellery retail',
		),
		'asap' => array(
			'site_key' => 'asap',
			'hostname' => 'www.ecomae.com',
			'industry_code' => 'erp_standalone',
			'status' => 'live',
			'trade_name' => $tpl['asap']['trade_name'],
			'hub_name' => $tpl['asap']['hub_name'],
			'from_email' => $tpl['asap']['from_email'],
			'db_name' => 'asap',
			'db_user' => 'asap',
			'erp_only_shared' => 1,
			'hosted_on' => 'platform',
			'notes' => 'Canonical registry restore — shared ERP-only tenant on ecomae.com',
		),
	);
}

function epc_trr_platform_pdo(): PDO
{
	$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
	if (!is_file($cfgFile)) {
		throw new RuntimeException('Missing platform config.local.php');
	}
	$epc_config_local = null;
	include $cfgFile;
	$dbName = trim((string) ($epc_config_local['db'] ?? 'ecomae'));
	$dbUser = trim((string) ($epc_config_local['user'] ?? 'ecomae'));
	$dbPass = trim((string) ($epc_config_local['password'] ?? ''));
	if ($dbPass === '') {
		throw new RuntimeException('Platform DB password missing');
	}
	$pdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8;connect_timeout=5',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 5)
	);
	epc_portal_db_ensure($pdo);
	return $pdo;
}

function epc_trr_mysqli_ok(string $db, string $user, string $pass): array
{
	if ($db === '' || $user === '' || $pass === '') {
		return array('ok' => false, 'message' => 'missing credentials');
	}
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

function epc_trr_find_working_docpart_pass(string $platformDocroot): string
{
	$candidates = array();
	$resolved = epc_portal_resolve_tenant_db_credentials();
	if (!empty($resolved['password'])) {
		$candidates[] = (string) $resolved['password'];
	}
	foreach (array(
		rtrim($platformDocroot, '/') . '/config.tenant-db.php',
		'/home/epartscart/htdocs/www.epartscart.com/config.local.php',
		'/home/epartscart/htdocs/www.epartscart.com/config.php',
		rtrim($platformDocroot, '/') . '/config.php',
	) as $path) {
		if (!is_file($path)) {
			continue;
		}
		if (substr($path, -20) === 'config.tenant-db.php' || substr($path, -13) === 'config.local.php') {
			$epc_config_local = null;
			$epc_tenant_db = null;
			include $path;
			$src = null;
			if (isset($epc_tenant_db) && is_array($epc_tenant_db)) {
				$src = $epc_tenant_db;
			} elseif (isset($epc_config_local) && is_array($epc_config_local)) {
				$src = $epc_config_local;
			}
			if (is_array($src) && !empty($src['password'])) {
				$candidates[] = (string) $src['password'];
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
		$t = epc_trr_mysqli_ok('docpart', 'docpart', $pass);
		if ($t['ok']) {
			return $pass;
		}
	}
	return '';
}

function epc_trr_find_asap_pass(PDO $pdo): string
{
	$fromGet = trim((string) ($_GET['asap_db_password'] ?? $_GET['db_password'] ?? ''));
	if ($fromGet !== '') {
		return $fromGet;
	}
	foreach (array('asap', 'eep') as $siteKey) {
		$st = $pdo->prepare('SELECT db_name, db_user, db_password FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($siteKey));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			continue;
		}
		$db = trim((string) ($row['db_name'] ?? ''));
		$user = trim((string) ($row['db_user'] ?? 'asap'));
		$pass = trim((string) ($row['db_password'] ?? ''));
		if ($db === 'asap' && $pass !== '') {
			$test = epc_trr_mysqli_ok('asap', $user !== '' ? $user : 'asap', $pass);
			if ($test['ok']) {
				return $pass;
			}
		}
	}
	return '';
}

function epc_trr_dump_tenants(PDO $pdo, string $label): int
{
	echo "--- {$label} ---\n";
	$rows = $pdo->query('SELECT site_key, hostname, industry_code, db_name, status, erp_only_shared FROM `epc_portal_tenants` ORDER BY site_key')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as $row) {
		echo sprintf(
			"  %s | %s | %s | db=%s | %s | erp_only=%s\n",
			$row['site_key'],
			$row['hostname'],
			$row['industry_code'],
			$row['db_name'],
			$row['status'],
			(string) ($row['erp_only_shared'] ?? '0')
		);
	}
	echo 'count=' . count($rows) . "\n\n";
	return count($rows);
}

try {
	$pdo = epc_trr_platform_pdo();
} catch (Exception $e) {
	exit('Platform DB: ' . $e->getMessage() . "\n");
}

$beforeCount = epc_trr_dump_tenants($pdo, 'BEFORE');

$canonical = epc_trr_canonical_tenants();
$canonicalKeys = array_keys($canonical);
$removeKeys = array('eep', 'erp_only_demo', 'ecomae');

$docpartPass = epc_trr_find_working_docpart_pass($platformDocroot);
echo 'docpart password: ' . ($docpartPass !== '' ? 'found len=' . strlen($docpartPass) : 'NOT FOUND') . "\n";
$asapPass = epc_trr_find_asap_pass($pdo);
echo 'asap password: ' . ($asapPass !== '' ? 'found len=' . strlen($asapPass) : 'NOT FOUND (pass asap_db_password= or run epc-asap-db-isolate.php)') . "\n\n";

if ($docpartPass === '') {
	echo "BLOCKER: cannot connect to docpart — fix MySQL user first (epc-docpart-db-fix.php)\n";
	if (!$apply) {
		exit("Dry run stopped\n");
	}
	exit(1);
}

foreach ($removeKeys as $badKey) {
	$st = $pdo->prepare('SELECT site_key, hostname, db_name FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
	$st->execute(array($badKey));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		continue;
	}
	echo 'Remove erroneous row site_key=' . $badKey . ' hostname=' . ($row['hostname'] ?? '') . ' db=' . ($row['db_name'] ?? '') . "\n";
	if ($apply) {
		$del = $pdo->prepare('DELETE FROM `epc_portal_tenants` WHERE `site_key` = ?');
		$del->execute(array($badKey));
	}
}

$stOrphans = $pdo->query('SELECT site_key FROM `epc_portal_tenants` ORDER BY site_key')->fetchAll(PDO::FETCH_COLUMN);
foreach ($stOrphans as $orphanKey) {
	$orphanKey = (string) $orphanKey;
	if (in_array($orphanKey, $canonicalKeys, true) || in_array($orphanKey, $removeKeys, true)) {
		continue;
	}
	echo 'Remove orphan site_key=' . $orphanKey . "\n";
	if ($apply) {
		$pdo->prepare('DELETE FROM `epc_portal_tenants` WHERE `site_key` = ?')->execute(array($orphanKey));
	}
}

$stDupEcomae = $pdo->query(
	"SELECT site_key FROM `epc_portal_tenants` WHERE `hostname` = 'www.ecomae.com' AND `site_key` != 'asap'"
)->fetchAll(PDO::FETCH_COLUMN);
foreach ($stDupEcomae as $dupKey) {
	echo 'Remove duplicate shared-ERP row on www.ecomae.com site_key=' . $dupKey . "\n";
	if ($apply) {
		$pdo->prepare('DELETE FROM `epc_portal_tenants` WHERE `site_key` = ?')->execute(array($dupKey));
	}
}

echo "\n--- Upsert canonical tenants ---\n";
foreach ($canonical as $key => $data) {
	$dbPass = ($key === 'asap') ? $asapPass : $docpartPass;
	if ($key === 'asap' && $dbPass === '') {
		echo "{$key}: SKIP — asap DB password unknown\n";
		continue;
	}
	$data['db_password'] = $dbPass;
	if (!$apply) {
		$test = epc_trr_mysqli_ok((string) $data['db_name'], (string) $data['db_user'], $dbPass);
		echo "{$key}: would save hostname={$data['hostname']} db={$data['db_name']} db_test=" . ($test['ok'] ? 'OK' : 'FAIL ' . $test['message']) . "\n";
		continue;
	}
	$save = epc_portal_save_tenant($pdo, $data);
	echo "{$key}: " . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
}

if ($apply && $asapPass !== '') {
	$sync = epc_portal_sync_tenant_packs_to_client_db($pdo, 'asap');
	echo 'asap pack sync: ' . ($sync['ok'] ? 'OK' : 'FAIL') . ' — ' . ($sync['message'] ?? '') . "\n";
}

echo "\n--- DB connect verify ---\n";
$verifyRows = $pdo->query('SELECT site_key, db_name, db_user, db_password FROM `epc_portal_tenants` ORDER BY site_key')->fetchAll(PDO::FETCH_ASSOC);
foreach ($verifyRows as $row) {
	$db = (string) $row['db_name'];
	$user = trim((string) ($row['db_user'])) !== '' ? (string) $row['db_user'] : $db;
	$pass = (string) ($row['db_password']);
	if ($db === 'docpart' && $pass === '') {
		$pass = $docpartPass;
	}
	$test = epc_trr_mysqli_ok($db, $user, $pass);
	echo $row['site_key'] . ' db=' . $db . ': ' . ($test['ok'] ? 'OK ' . $test['message'] : 'FAIL ' . $test['message']) . "\n";
}

$afterCount = epc_trr_dump_tenants($pdo, 'AFTER');

echo "SUMMARY before={$beforeCount} after={$afterCount} expected=" . count($canonical) . "\n";
if (!$apply) {
	echo "Dry run complete — re-run with apply=1\n";
} else {
	echo "DONE — refresh Super CP Tenant hub\n";
}
