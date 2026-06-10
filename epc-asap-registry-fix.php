<?php
/**
 * Fast registry repoint: ASAP tenant -> dedicated asap MySQL (skip schema clone).
 * Run epc-asap-db-isolate.php separately if DB does not exist yet.
 * https://www.ecomae.com/epc-asap-registry-fix.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(60);
@ini_set('default_socket_timeout', '3');

echo "=== ASAP registry fix (fast) ===\n";
$apply = !empty($_GET['apply']);
$siteKey = 'asap';
$dbName = 'asap';
$dbUser = 'asap';
$dbPass = trim((string) ($_GET['db_password'] ?? ''));
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";

$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$epc_config_local = null;
if (!is_file($cfgFile)) {
	exit("Missing config.local.php\n");
}
include $cfgFile;
$platDb = trim((string) ($epc_config_local['db'] ?? 'ecomae'));
$platUser = trim((string) ($epc_config_local['user'] ?? 'ecomae'));
$platPass = trim((string) ($epc_config_local['password'] ?? ''));
if ($platPass === '') {
	exit("Platform password missing\n");
}

try {
	$platformPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8;connect_timeout=3',
		$platUser,
		$platPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
	);
} catch (Exception $e) {
	exit('Platform DB: ' . $e->getMessage() . "\n");
}
echo "Platform DB: OK\n";

require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';
epc_portal_db_ensure($platformPdo);

$st = $platformPdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
$st->execute(array($siteKey));
$row = $st->fetch(PDO::FETCH_ASSOC);
echo 'Current db_name=' . ($row['db_name'] ?? 'none') . "\n";

if ($dbPass === '' && $row && trim((string) ($row['db_password'] ?? '')) !== '' && ($row['db_name'] ?? '') === $dbName) {
	$dbPass = (string) $row['db_password'];
}

$tenantOk = false;
$tables = 0;
if ($dbPass !== '') {
	try {
		$tenantPdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8;connect_timeout=3',
			$dbUser,
			$dbPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
		);
		$tables = (int) $tenantPdo->query('SHOW TABLES')->rowCount();
		$tenantOk = $tables > 0;
		echo "Tenant DB {$dbName}: OK tables={$tables}\n";
	} catch (Exception $e) {
		echo "Tenant DB {$dbName}: " . $e->getMessage() . "\n";
	}
}

if (!$tenantOk) {
	echo "Tenant DB not ready — run epc-asap-db-isolate.php?apply=1 with db_password after CloudPanel creates asap DB\n";
	if (!$apply) {
		exit("Dry run done\n");
	}
	exit("Cannot update registry without working asap DB\n");
}

if (!$apply) {
	echo "Would repoint registry {$siteKey} -> db {$dbName}\n";
	exit("Dry run done\n");
}

$save = epc_portal_save_tenant($platformPdo, array(
	'site_key' => $siteKey,
	'hostname' => 'www.ecomae.com',
	'industry_code' => 'erp_standalone',
	'status' => 'live',
	'trade_name' => 'ASAP',
	'hub_name' => 'ASAP',
	'from_email' => 'admin@asap-ae.com',
	'db_name' => $dbName,
	'db_user' => $dbUser,
	'db_password' => $dbPass,
	'hosted_on' => 'platform',
	'erp_only_shared' => 1,
	'notes' => 'ASAP registry fix — dedicated asap DB',
));
echo 'Registry: ' . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";

$settings = epc_portal_default_site_settings('www.ecomae.com');
$settings['host'] = 'www.ecomae.com';
$settings['system_name'] = 'ASAP';
$settings['hub_name'] = 'ASAP';
$settings['access_mode'] = 'erp_only';
$settings['industry_code'] = 'erp_standalone';
$settings['contact'] = array('trade_name' => 'ASAP');
$push = epc_portal_push_settings_to_tenant_host($platformPdo, 'www.ecomae.com', $settings);
echo 'Settings push: ' . ($push['ok'] ? 'OK' : 'FAIL') . ' db=' . ($push['db'] ?? '') . "\n";

$sync = epc_portal_sync_tenant_packs_to_client_db($platformPdo, $siteKey);
echo 'Sync: ' . ($sync['ok'] ? 'OK' : 'FAIL') . "\n";
echo "Done. ASAP registry now points to {$dbName} (not docpart).\n";
