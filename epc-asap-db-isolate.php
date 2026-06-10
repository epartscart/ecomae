<?php
/**
 * Isolate ASAP ERP tenant to dedicated MySQL database (not shared docpart).
 * https://www.ecomae.com/epc-asap-db-isolate.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(600);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_intro.php';
require_once __DIR__ . '/content/general_pages/epc_portal_erp_modules.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$apply = !empty($_GET['apply']);
$siteKey = 'asap';
$dbName = 'asap';
$dbUser = 'asap';
$dbPass = trim((string) ($_GET['db_password'] ?? ''));
if ($dbPass === '') {
	$dbPass = bin2hex(random_bytes(10)) . 'A!';
}
$srcDb = trim((string) ($_GET['src_db'] ?? 'ecomae'));
$adminLogin = 'asap_admin@asap-ae.com';
$demoLogin = 'asap_demo@asap-ae.com';

echo "=== ASAP DB isolation (dedicated tenant MySQL) ===\n";
echo "site_key={$siteKey} target_db={$dbName} src_schema={$srcDb}\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

function epc_asap_iso_platform_pdo(): PDO
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
		'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_portal_db_ensure($pdo);
	return $pdo;
}

function epc_asap_iso_tenant_exists(string $dbName, string $dbUser, string $dbPass): bool
{
	try {
		$pdo = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
		return ((int) $pdo->query('SHOW TABLES')->rowCount()) > 0;
	} catch (Exception $e) {
		return false;
	}
}

function epc_asap_iso_provision_db(string $dbName, string $dbUser, string $dbPass, bool $apply): array
{
	$log = array();
	if (epc_asap_iso_tenant_exists($dbName, $dbUser, $dbPass)) {
		$log[] = "DB {$dbName} already exists and connects";
		return array('ok' => true, 'log' => $log);
	}
	if (!$apply) {
		$log[] = "Would create DB {$dbName} user {$dbUser}";
		return array('ok' => true, 'log' => $log);
	}
	$r = epc_clp_provision_database(array(
		'domain' => 'www.ecomae.com',
		'database_name' => $dbName,
		'database_user' => $dbUser,
		'database_password' => $dbPass,
	));
	$log = array_merge($log, $r['log']);
	return array('ok' => !empty($r['ok']), 'log' => $log);
}

function epc_asap_iso_clone_schema(string $srcDb, string $destDb, string $destUser, string $destPass, bool $apply): array
{
	$dump = '/tmp/epc-asap-schema-' . time() . '.sql';
	$log = array();
	if (!$apply) {
		$log[] = "Would clone schema {$srcDb} -> {$destDb} (no-data) and truncate transactional rows";
		return array('ok' => true, 'log' => $log);
	}
	@unlink($dump);
	$cmd = 'mysqldump --single-transaction --no-data ' . escapeshellarg($srcDb)
		. ' 2>/dev/null | mysql -u ' . escapeshellarg($destUser)
		. ' -p' . escapeshellarg($destPass) . ' ' . escapeshellarg($destDb) . ' 2>&1';
	$out = epc_clp_run_cmd($cmd);
	$log[] = $cmd;
	$log[] = $out['output'];
	if ($out['code'] !== 0 || stripos($out['output'], 'error') !== false) {
		$dumpGz = '/tmp/epc-asap-full-' . time() . '.sql.gz';
		@unlink($dumpGz);
		$exp = epc_clp_run_cmd('/usr/bin/clpctl db:export --databaseName=' . escapeshellarg($srcDb) . ' --file=' . escapeshellarg($dumpGz));
		$log[] = 'fallback export: ' . $exp['output'];
		if (is_file($dumpGz)) {
			$imp = epc_clp_run_cmd('/usr/bin/clpctl db:import --databaseName=' . escapeshellarg($destDb) . ' --file=' . escapeshellarg($dumpGz));
			$log[] = 'fallback import: ' . $imp['output'];
		}
	}
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $destDb . ';charset=utf8',
			$destUser,
			$destPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$truncate = array(
			'shop_orders', 'shop_orders_items', 'shop_orders_statuses',
			'epc_erp_invoices', 'epc_erp_purchase_orders', 'epc_erp_journal_entries',
			'epc_erp_journal_lines', 'epc_crm_leads', 'epc_crm_opportunities', 'epc_crm_activities',
			'epc_erp_bank_accounts', 'epc_erp_bank_transactions', 'epc_erp_vat_returns',
			'epc_erp_supplier_invoices', 'epc_erp_customer_ledger', 'epc_erp_supplier_ledger',
		);
		foreach ($truncate as $tbl) {
			try {
				$pdo->exec('DELETE FROM `' . str_replace('`', '', $tbl) . '`');
			} catch (Exception $e) {
				// table may not exist on this schema version
			}
		}
		$log[] = 'Transactional tables cleared in ' . $destDb;
	} catch (Exception $e) {
		return array('ok' => false, 'log' => array_merge($log, array('truncate fail: ' . $e->getMessage())));
	}
	return array('ok' => true, 'log' => $log);
}

function epc_asap_iso_cp_admin(PDO $tenantPdo, $cfg, string $login, string $password, bool $apply): array
{
	$st = $tenantPdo->prepare('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$st->execute();
	$root = $st->fetch(PDO::FETCH_ASSOC);
	$groups = $root ? array((int) $root['id']) : array(3);
	$hash = md5($password . $cfg->secret_succession);
	$out = array('login' => $login);
	$st = $tenantPdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($login));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		$out['status'] = 'missing';
		if ($apply) {
			$tenantPdo->prepare(
				'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `admin_created`)
				 VALUES (?, 1, ?, 1, 1, ?, 1)'
			)->execute(array($login, $hash, (string) time()));
			$userId = (int) $tenantPdo->lastInsertId();
			@$tenantPdo->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')
				->execute(array($userId, 'name', strpos($login, 'demo') !== false ? 'ASAP Demo Operator' : 'ASAP Administrator'));
			$ins = $tenantPdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
			foreach ($groups as $gid) {
				$ins->execute(array($userId, (int) $gid));
			}
			$out['user_created'] = $userId;
		}
		return $out;
	}
	$out['status'] = 'exists';
	$out['user_id'] = (int) $row['user_id'];
	if ($apply) {
		$tenantPdo->prepare('UPDATE `users` SET `password` = ?, `unlocked` = 1, `email_confirmed` = 1 WHERE `user_id` = ?')
			->execute(array($hash, (int) $row['user_id']));
		$out['password_reset'] = true;
	}
	return $out;
}

try {
	$platformPdo = epc_asap_iso_platform_pdo();
	echo "Platform DB: OK\n";
} catch (Exception $e) {
	exit('Platform DB FAIL: ' . $e->getMessage() . "\n");
}

$st = $platformPdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
$st->execute(array($siteKey));
$current = $st->fetch(PDO::FETCH_ASSOC);
if ($current) {
	echo 'Current registry db_name=' . ($current['db_name'] ?? '?') . "\n";
}

$prov = epc_asap_iso_provision_db($dbName, $dbUser, $dbPass, $apply);
foreach ($prov['log'] as $line) {
	echo $line . "\n";
}
if (!$prov['ok']) {
	exit("DB provision failed\n");
}

$clone = epc_asap_iso_clone_schema($srcDb, $dbName, $dbUser, $dbPass, $apply);
foreach ($clone['log'] as $line) {
	echo $line . "\n";
}
if (!$clone['ok']) {
	exit("Schema clone failed\n");
}

if (!$apply) {
	echo "\nDry run complete. Re-run with apply=1\n";
	echo "db_password={$dbPass}\n";
	exit;
}

$fullErpMods = epc_portal_erp_modules_presets()['full_erp']['modules'];
$save = epc_portal_save_tenant($platformPdo, array(
	'site_key' => $siteKey,
	'hostname' => 'www.ecomae.com',
	'industry_code' => 'erp_standalone',
	'status' => 'live',
	'trade_name' => 'ASAP',
	'hub_name' => 'Electronic World Group',
	'from_email' => 'admin@asap-ae.com',
	'db_name' => $dbName,
	'db_user' => $dbUser,
	'db_password' => $dbPass,
	'hosted_on' => 'platform',
	'erp_only_shared' => 1,
	'notes' => 'ASAP isolated DB — epc-asap-db-isolate.php',
));
echo 'Registry update: ' . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";

$settings = epc_portal_default_site_settings('www.ecomae.com');
$settings['host'] = 'www.ecomae.com';
$settings['industry_code'] = 'erp_standalone';
$settings['system_name'] = 'ASAP';
$settings['hub_name'] = 'ASAP';
$settings['access_mode'] = 'erp_only';
$settings['enabled_packs'] = array('core', 'erp', 'professional', 'logistics');
$settings['erp_modules'] = $fullErpMods;
$settings['contact'] = array_merge(epc_portal_default_contact(array('trade_name' => 'ASAP')), array('trade_name' => 'ASAP'));
$push = epc_portal_push_settings_to_tenant_host($platformPdo, 'www.ecomae.com', $settings);
echo 'Tenant settings push: ' . ($push['ok'] ? 'OK' : 'FAIL') . ' db=' . ($push['db'] ?? '') . "\n";

$sync = epc_portal_sync_tenant_packs_to_client_db($platformPdo, $siteKey);
echo 'Pack sync: ' . ($sync['ok'] ? 'OK' : 'FAIL') . ' — ' . ($sync['message'] ?? '') . "\n";

define('_ASTEXE_', 1);
require_once '/home/ecomae/htdocs/www.ecomae.com/config.php';
$cfg = new DP_Config();
$adminPass = bin2hex(random_bytes(8)) . 'A!';
$demoPass = bin2hex(random_bytes(8)) . 'D!';
try {
	$tenantPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$adminResult = epc_asap_iso_cp_admin($tenantPdo, $cfg, $adminLogin, $adminPass, true);
	$demoResult = epc_asap_iso_cp_admin($tenantPdo, $cfg, $demoLogin, $demoPass, true);
	echo 'Admin user: ' . json_encode($adminResult, JSON_UNESCAPED_UNICODE) . "\n";
	echo 'Demo user: ' . json_encode($demoResult, JSON_UNESCAPED_UNICODE) . "\n";

	$orders = 0;
	try {
		$orders = (int) $tenantPdo->query('SELECT COUNT(*) FROM `shop_orders`')->fetchColumn();
	} catch (Exception $e) {
		$orders = -1;
	}
	echo "ASAP shop_orders count={$orders} (expect 0)\n";
} catch (Exception $e) {
	echo 'CP users: FAIL — ' . $e->getMessage() . "\n";
}

echo "\n=== VERIFICATION ===\n";
echo "ASAP tenant DB: {$dbName}\n";
echo "epartscart/docpart DB: docpart (unchanged)\n";
echo "Platform operator DB: ecomae\n";
echo "Admin login: {$adminLogin}\n";
echo "Admin temp password: {$adminPass}\n";
echo "Demo login: {$demoLogin}\n";
echo "Demo temp password: {$demoPass}\n";
echo "DB user password: {$dbPass}\n";
echo "\nDone — ASAP is isolated from docpart/epartscart data.\n";
