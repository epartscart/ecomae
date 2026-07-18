<?php
/**
 * Best-path: create/migrate a tenant onto a dedicated MySQL DB named after site_key.
 *
 * Use when a shared-ERP tenant is stuck on a demo-pool DB (dpXXXXXX) or has dead credentials.
 *
 * One-shot (after CloudPanel admin password is known):
 *   # As root on the VPS (if panel login is stale):
 *   clpctl user:reset:password --userName=admin --password='NEW_STRONG_PASS'
 *
 *   curl -sk "https://www.ecomae.com/epc-tenant-dedicated-db.php?token=epartscart-deploy-2026&site_key=spare247&clp_pass=NEW_STRONG_PASS&apply=1"
 *
 * Dry run (default):
 *   curl -sk "https://www.ecomae.com/epc-tenant-dedicated-db.php?token=...&site_key=spare247"
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(600);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? $_POST['site_key'] ?? ''))));
$clpPass = trim((string) ($_GET['clp_pass'] ?? $_POST['clp_pass'] ?? ''));
$desiredDb = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['desired_db'] ?? $_POST['desired_db'] ?? ''))));
$dbPassOverride = trim((string) ($_GET['db_password'] ?? $_POST['db_password'] ?? ''));

if ($siteKey === '') {
	exit("site_key required\n");
}

echo "=== Tenant dedicated DB (best path) ===\n";
echo 'site_key=' . $siteKey . ' apply=' . ($apply ? 'yes' : 'no') . "\n\n";

function epc_tdd_platform_pdo(): PDO
{
	$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
	$epc_config_local = null;
	include $cfgFile;
	return new PDO(
		'mysql:host=127.0.0.1;dbname=' . ($epc_config_local['db'] ?? 'ecomae') . ';charset=utf8mb4',
		(string) ($epc_config_local['user'] ?? 'ecomae'),
		(string) ($epc_config_local['password'] ?? ''),
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

function epc_tdd_connects(string $db, string $user, string $pass): bool
{
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
			$user,
			$pass,
			array(PDO::ATTR_TIMEOUT => 5, PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$pdo->query('SELECT 1');
		return true;
	} catch (Throwable $e) {
		return false;
	}
}

function epc_tdd_table_count(string $db, string $user, string $pass): int
{
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		return (int) $pdo->query('SHOW TABLES')->rowCount();
	} catch (Throwable $e) {
		return -1;
	}
}

/**
 * Copy schema+data from source DB to destination using clpctlWrapper export/import.
 */
function epc_tdd_migrate(string $srcDb, string $destDb): array
{
	$dump = '/tmp/epc-tdd-' . preg_replace('/[^a-z0-9_]/', '', $srcDb) . '-' . time() . '.sql.gz';
	@unlink($dump);
	$exp = epc_clp_run('db:export --databaseName=' . escapeshellarg($srcDb) . ' --file=' . escapeshellarg($dump));
	$log = array('export' => trim((string) ($exp['output'] ?? '')));
	if (!is_file($dump)) {
		return array('ok' => false, 'log' => $log, 'error' => 'export file missing');
	}
	$imp = epc_clp_run('db:import --databaseName=' . escapeshellarg($destDb) . ' --file=' . escapeshellarg($dump));
	$log['import'] = trim((string) ($imp['output'] ?? ''));
	@unlink($dump);
	$ok = ((int) ($imp['code'] ?? 1) === 0) || stripos($log['import'], 'imported') !== false;
	return array('ok' => $ok, 'log' => $log);
}

try {
	$platformPdo = epc_tdd_platform_pdo();
	epc_portal_db_ensure($platformPdo);
} catch (Throwable $e) {
	exit('platform_pdo_fail: ' . $e->getMessage() . "\n");
}

$row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
if ($row === null) {
	exit("tenant_not_in_registry: {$siteKey}\n");
}

$currentDb = (string) ($row['db_name'] ?? '');
$currentUser = (string) ($row['db_user'] ?? '');
$currentPass = (string) ($row['db_password'] ?? '');
if ($desiredDb === '') {
	$desiredDb = preg_replace('/[^a-z0-9_]/', '', $siteKey);
}
$desiredUser = $desiredDb;
$desiredPass = $dbPassOverride !== '' ? $dbPassOverride : epc_portal_tenant_control_generate_password();

echo "current db={$currentDb} user={$currentUser} connect="
	. (epc_tdd_connects($currentDb, $currentUser, $currentPass) ? 'ok' : 'fail')
	. ' tables=' . epc_tdd_table_count($currentDb, $currentUser, $currentPass) . "\n";
echo "desired db={$desiredDb} user={$desiredUser}\n";

$alreadyDedicated = ($currentDb === $desiredDb)
	&& epc_tdd_connects($currentDb, $currentUser, $currentPass)
	&& epc_tdd_table_count($currentDb, $currentUser, $currentPass) > 10;

if ($alreadyDedicated) {
	echo "Status: already on dedicated DB with schema — nothing to do.\n";
	exit(0);
}

if ($clpPass === '') {
	$clpPass = epc_portal_demo_clp_password();
}

echo 'clp_pass=' . ($clpPass !== '' ? 'provided/saved len=' . strlen($clpPass) : 'MISSING') . "\n";
echo 'clpctl bin=' . epc_clp_bin() . "\n\n";

if (!$apply) {
	echo "Dry run. To create dedicated `{$desiredDb}` and migrate from `{$currentDb}`:\n";
	echo "  1) If CloudPanel admin login is stale, as root on VPS:\n";
	echo "       clpctl user:reset:password --userName=admin --password='NEW_STRONG_PASS'\n";
	echo "  2) Then:\n";
	echo "       curl -sk \"https://www.ecomae.com/epc-tenant-dedicated-db.php?token=epartscart-deploy-2026"
		. "&site_key={$siteKey}&clp_pass=NEW_STRONG_PASS&apply=1\"\n";
	echo "This saves config.demo-clp.php, creates the DB via CloudPanel web UI,"
		. " migrates data (clpctl export/import), and updates the tenant registry.\n";
	exit(0);
}

if ($clpPass === '') {
	exit("apply=1 requires clp_pass= (or a working config.demo-clp.php)\n");
}

// 1) Persist CLP password for future provisioning
$saved = epc_portal_demo_clp_password_save($clpPass);
echo 'config.demo-clp.php: ' . (!empty($saved['ok']) ? 'saved' : ('FAIL ' . ($saved['message'] ?? ''))) . "\n";

// 2) Login CloudPanel
$cookie = '';
$login = epc_clp_web_login('admin', $clpPass, $cookie, true);
if (empty($login['ok'])) {
	echo "CloudPanel login FAILED\n";
	if (!empty($login['detail']) && is_array($login['detail'])) {
		foreach ($login['detail'] as $k => $v) {
			if (is_scalar($v)) {
				echo "  {$k}={$v}\n";
			}
		}
	}
	echo "\nReset admin on VPS as root, then re-run with the new password:\n";
	echo "  clpctl user:reset:password --userName=admin --password='NEW_STRONG_PASS'\n";
	exit(1);
}
echo "CloudPanel login: OK\n";

// 3) Create dedicated DB if needed
if (!epc_tdd_connects($desiredDb, $desiredUser, $desiredPass)) {
	// Try current password on desired name (DB may exist with old pass)
	if ($currentPass !== '' && epc_tdd_connects($desiredDb, $desiredUser, $currentPass)) {
		$desiredPass = $currentPass;
		echo "desired DB already reachable with registry/current password\n";
	} else {
		echo "Creating CloudPanel database {$desiredDb}...\n";
		$created = epc_clp_web_add_database($cookie, 'www.ecomae.com', $desiredDb, $desiredUser, $desiredPass);
		foreach (array_slice((array) ($created['log'] ?? array()), 0, 8) as $line) {
			echo '  ' . $line . "\n";
		}
		$ok = false;
		for ($i = 0; $i < 12; $i++) {
			if ($i > 0) {
				sleep(2);
			}
			if (epc_tdd_connects($desiredDb, $desiredUser, $desiredPass)) {
				$ok = true;
				echo 'create_verify=ok wait=' . ($i * 2) . "s\n";
				break;
			}
		}
		if (!$ok) {
			exit("dedicated DB create failed — check CloudPanel → Databases for www.ecomae.com\n");
		}
	}
} else {
	echo "desired DB already connects with new password\n";
}

// 4) Migrate from current if different and source has data
$srcTables = epc_tdd_table_count($currentDb, $currentUser, $currentPass);
$destTables = epc_tdd_table_count($desiredDb, $desiredUser, $desiredPass);
echo "tables src={$srcTables} dest={$destTables}\n";

if ($currentDb !== $desiredDb && $srcTables > 10 && $destTables < 10) {
	echo "Migrating {$currentDb} → {$desiredDb} via clpctl export/import...\n";
	$mig = epc_tdd_migrate($currentDb, $desiredDb);
	foreach ((array) ($mig['log'] ?? array()) as $k => $v) {
		echo "  {$k}: {$v}\n";
	}
	if (empty($mig['ok'])) {
		exit("migrate_failed\n");
	}
	$destTables = epc_tdd_table_count($desiredDb, $desiredUser, $desiredPass);
	echo "tables_after_migrate={$destTables}\n";
	if ($destTables < 10) {
		exit("migrate_verify_failed (dest still empty)\n");
	}
} elseif ($destTables < 10) {
	echo "Dest empty and no usable source — run epc-erp-tenant-provision.php?site_key={$siteKey}&apply=1 after registry update.\n";
}

// 5) Update registry
$intro = array();
if (!empty($row['intro_json'])) {
	$decoded = json_decode((string) $row['intro_json'], true);
	if (is_array($decoded)) {
		$intro = $decoded;
	}
}
$intro['desired_db_name'] = $desiredDb;
$intro['dedicated_db_migrated_at'] = date('c');
if ($currentDb !== '' && $currentDb !== $desiredDb) {
	$intro['previous_db_name'] = $currentDb;
}

$save = epc_portal_save_tenant($platformPdo, array(
	'site_key' => $siteKey,
	'hostname' => (string) ($row['hostname'] ?? 'www.ecomae.com'),
	'industry_code' => (string) ($row['industry_code'] ?? 'erp_standalone'),
	'status' => (string) ($row['status'] ?? 'live'),
	'trade_name' => (string) ($row['trade_name'] ?? $siteKey),
	'hub_name' => (string) ($row['hub_name'] ?? ''),
	'from_email' => (string) ($row['from_email'] ?? ''),
	'db_name' => $desiredDb,
	'db_user' => $desiredUser,
	'db_password' => $desiredPass,
	'hosted_on' => 'platform',
	'erp_only_shared' => 1,
	'notes' => trim((string) ($row['notes'] ?? '') . ' | dedicated-db ' . date('Y-m-d H:i') . " ({$currentDb}→{$desiredDb})"),
	'intro_json' => json_encode($intro, JSON_UNESCAPED_UNICODE),
));
echo 'registry_save=' . (!empty($save['ok']) ? 'ok' : 'fail') . ' ' . ($save['message'] ?? '') . "\n";

// 6) Mark pool row released if we left a dp* DB
if (preg_match('/^dp[a-f0-9]{6}$/', $currentDb)) {
	try {
		$platformPdo->prepare(
			"UPDATE `epc_portal_demo_db_pool`
			 SET `status` = 'released', `claimed_by_site_key` = ?
			 WHERE `db_name` = ? AND `status` = 'claimed'"
		)->execute(array($siteKey . ':migrated', $currentDb));
		echo "pool_slot {$currentDb}: marked released\n";
	} catch (Throwable $e) {
		echo 'pool_slot_update: ' . $e->getMessage() . "\n";
	}
}

$finalOk = epc_tdd_connects($desiredDb, $desiredUser, $desiredPass);
echo 'final_connect=' . ($finalOk ? 'ok' : 'FAIL')
	. ' tables=' . epc_tdd_table_count($desiredDb, $desiredUser, $desiredPass) . "\n";
echo "client_erp=https://www.ecomae.com/cp/client-erp/{$siteKey}/\n";
echo "Re-check: epc-commerce-isolation-audit.php?token=...&format=json\n";
echo "Done.\n";
exit($finalOk ? 0 : 1);
