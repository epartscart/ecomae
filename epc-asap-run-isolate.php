<?php
/**
 * Minimal ASAP isolation runner — invoked via platform-fix POST run_action=asap_isolate.
 */
declare(strict_types=1);
@ini_set('default_socket_timeout', '5');

echo "=== ASAP isolate (platform-fix action) ===\n";
flush();
$apply = !empty($_POST['apply']) || !empty($_GET['apply']);
$siteKey = 'asap';
$dbName = 'asap';
$dbUser = 'asap';
$dbPass = trim((string) ($_POST['db_password'] ?? $_GET['db_password'] ?? ''));
$clpPass = trim((string) ($_POST['clp_pass'] ?? $_GET['clp_pass'] ?? ''));
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";

$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$epc_config_local = null;
if (!is_file($cfgFile)) {
	exit("Missing config.local.php\n");
}
include $cfgFile;
$platDb = (string) ($epc_config_local['db'] ?? 'ecomae');
$platUser = (string) ($epc_config_local['user'] ?? 'ecomae');
$platPass = (string) ($epc_config_local['password'] ?? '');

try {
	$platformPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8',
		$platUser,
		$platPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit('Platform: ' . $e->getMessage() . "\n");
}
echo "Platform DB OK\n";

if ($dbPass === '') {
	$st = $platformPdo->prepare('SELECT db_password FROM epc_portal_tenants WHERE site_key = ? LIMIT 1');
	$st->execute(array($siteKey));
	$regPass = (string) $st->fetchColumn();
	if ($regPass !== '') {
		$dbPass = $regPass;
	}
}
if ($dbPass === '') {
	$dbPass = bin2hex(random_bytes(8)) . 'Ax!';
}

$tenantReady = false;
$cookie = '';
$login = array('ok' => false);
try {
	$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
	$tenantReady = ((int) $tp->query('SHOW TABLES')->rowCount()) > 10;
	echo 'Tenant exists tables=' . (int) $tp->query('SHOW TABLES')->rowCount() . "\n";
	if ($tenantReady && $apply) {
		$purgeTables = array(
			'shop_orders', 'shop_orders_items', 'shop_orders_logs', 'shop_orders_statuses',
			'shop_users_accounting', 'epc_erp_supplier_accounting', 'epc_erp_cash_accounts',
			'epc_erp_cash_movements', 'epc_erp_gl_journal_lines', 'epc_erp_gl_journals',
			'epc_crm_leads', 'epc_crm_opportunities', 'epc_crm_activities',
		);
		foreach ($purgeTables as $tbl) {
			try {
				$tp->exec('DELETE FROM `' . str_replace('`', '', $tbl) . '`');
			} catch (Exception $e) {
			}
		}
		$ordersLeft = (int) $tp->query('SELECT COUNT(*) FROM shop_orders')->fetchColumn();
		$crmLeft = (int) $tp->query('SELECT COUNT(*) FROM epc_crm_opportunities')->fetchColumn();
		echo "Purged transactional data shop_orders={$ordersLeft} crm_opps={$crmLeft}\n";
	}
} catch (Exception $e) {
	echo "Tenant missing: " . $e->getMessage() . "\n";
}

if (!$tenantReady && $apply) {
	require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
	if ($clpPass !== '' && empty($login['ok'])) {
		$login = epc_clp_web_login('admin', $clpPass, $cookie);
	}
	if (!empty($login['ok'])) {
		$dbCreate = epc_clp_web_add_database($cookie, 'www.ecomae.com', $dbName, $dbUser, $dbPass);
		foreach ($dbCreate['log'] as $line) {
			echo $line . "\n";
		}
		sleep(2);
	}
	$dumpGz = '/tmp/epc-asap-clone-' . time() . '.sql.gz';
	@unlink($dumpGz);
	$exp = epc_clp_run_cmd('/usr/bin/clpctl db:export --databaseName=ecomae --file=' . escapeshellarg($dumpGz));
	echo 'schema export: ' . substr($exp['output'], 0, 200) . "\n";
	if (is_file($dumpGz)) {
		$imp = epc_clp_run_cmd('/usr/bin/clpctl db:import --databaseName=' . escapeshellarg($dbName) . ' --file=' . escapeshellarg($dumpGz));
		echo 'schema import: ' . substr($imp['output'], 0, 200) . "\n";
		if ($clpPass !== '' && !empty($login['ok'])) {
			$panel = epc_clp_panel_url();
			$editPath = '/site/www.ecomae.com/database/user/edit/' . rawurlencode($dbUser);
			$html = epc_clp_web_request($panel . $editPath, array(), $cookie);
			if (preg_match('/name="site_database_user_edit\[_token\]" value="([^"]+)"/', $html, $m)) {
				epc_clp_web_request($panel . $editPath, array(
					'method' => 'POST',
					'body' => http_build_query(array(
						'site_database_user_edit' => array(
							'password' => $dbPass,
							'_token' => $m[1],
							'submit' => '',
						),
					)),
				), $cookie);
				echo "CloudPanel asap user password synced\n";
			}
		}
		sleep(1);
		try {
			$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
			$tenantReady = ((int) $tp->query('SHOW TABLES')->rowCount()) > 10;
		} catch (Exception $e) {
			echo 'After import: ' . $e->getMessage() . "\n";
			try {
				$admin = new PDO('mysql:host=127.0.0.1;charset=utf8', $platUser, $platPass);
				$admin->exec("ALTER USER '" . str_replace("'", '', $dbUser) . "'@'localhost' IDENTIFIED BY " . $admin->quote($dbPass));
				$admin->exec('FLUSH PRIVILEGES');
				echo "Reset MySQL user password via admin connection\n";
				$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
				$tenantReady = ((int) $tp->query('SHOW TABLES')->rowCount()) > 10;
			} catch (Exception $e2) {
				echo 'Admin grant failed: ' . $e2->getMessage() . "\n";
				try {
					$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $platUser, $platPass);
					$tenantReady = ((int) $tp->query('SHOW TABLES')->rowCount()) > 10;
					if ($tenantReady) {
						$dbUser = $platUser;
						$dbPass = $platPass;
						echo "Using platform DB user {$dbUser} for tenant database {$dbName}\n";
					}
				} catch (Exception $e3) {
					echo 'Platform user fallback failed: ' . $e3->getMessage() . "\n";
					require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
					$doc = epc_portal_resolve_tenant_db_credentials();
					try {
						$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $doc['user'], $doc['password']);
						$tenantReady = ((int) $tp->query('SHOW TABLES')->rowCount()) > 10;
						if ($tenantReady) {
							$dbUser = (string) $doc['user'];
							$dbPass = (string) $doc['password'];
							echo "Using docpart DB user {$dbUser} for tenant database {$dbName}\n";
						}
					} catch (Exception $e4) {
						echo 'Docpart user fallback failed: ' . $e4->getMessage() . "\n";
					}
				}
			}
		}
		if ($tenantReady) {
				$purgeTables = array(
					'shop_orders', 'shop_orders_items', 'shop_users_accounting', 'epc_erp_supplier_accounting',
					'epc_erp_cash_movements', 'epc_erp_gl_journal_lines', 'epc_erp_gl_journals',
					'epc_crm_leads', 'epc_crm_opportunities', 'epc_crm_activities',
				);
				foreach ($purgeTables as $tbl) {
					try {
						$tp->exec('DELETE FROM `' . str_replace('`', '', $tbl) . '`');
					} catch (Exception $e) {
					}
				}
				echo "Cleared transactional tables in {$dbName}\n";
		}
	}
}

if (!$tenantReady) {
	exit($apply ? "Tenant DB not ready\n" : "Dry run — would provision {$dbName}\n");
}

$st = $platformPdo->prepare('SELECT db_name FROM epc_portal_tenants WHERE site_key = ? LIMIT 1');
$st->execute(array($siteKey));
$oldDb = (string) $st->fetchColumn();
echo "Registry was db_name={$oldDb}\n";

if (!$apply) {
	exit("Dry run OK — would point {$siteKey} -> {$dbName}\n");
}

$now = time();
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
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
	'notes' => 'ASAP isolated DB — epc-asap-run-isolate.php',
));
echo 'Registry: ' . ($save['ok'] ? 'OK' : 'FAIL') . ' -> ' . $dbName . ' (was ' . $oldDb . ")\n";

require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';
$settings = epc_portal_default_site_settings('www.ecomae.com');
$settings['host'] = 'www.ecomae.com';
$settings['system_name'] = 'ASAP';
$settings['hub_name'] = 'ASAP';
$settings['access_mode'] = 'erp_only';
$settings['industry_code'] = 'erp_standalone';
$settings['enabled_packs'] = array('core', 'erp', 'professional', 'logistics');
$settings['contact'] = array('trade_name' => 'ASAP');
$push = epc_portal_push_settings_to_tenant_host($platformPdo, 'www.ecomae.com', $settings);
echo 'Settings: ' . ($push['ok'] ? 'OK' : 'FAIL') . ' db=' . ($push['db'] ?? '') . "\n";
try {
	$tpSettings = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
	epc_portal_db_ensure($tpSettings);
	$tpSettings->prepare(
		'UPDATE `epc_portal_site_settings` SET `system_name` = ?, `hub_name` = ?, `access_mode` = ?, `industry_code` = ?
		 WHERE `host` IN (\'www.ecomae.com\', \'ecomae.com\') OR `host` LIKE \'%ecomae%\''
	)->execute(array('ASAP', 'ASAP', 'erp_only', 'erp_standalone'));
	echo "Tenant site_settings branded ASAP / erp_only\n";
} catch (Exception $e) {
	echo 'Settings direct update: ' . $e->getMessage() . "\n";
}

$sync = epc_portal_sync_tenant_packs_to_client_db($platformPdo, $siteKey);
echo 'Sync: ' . ($sync['ok'] ? 'OK' : 'FAIL') . "\n";

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
$adminLogin = 'asap_admin@asap-ae.com';
$adminPass = bin2hex(random_bytes(8)) . 'A!';
$hash = md5($adminPass . $cfg->secret_succession);
try {
	$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
	$tp->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$uid = (int) $tp->query("SELECT user_id FROM users WHERE email=" . $tp->quote($adminLogin) . " LIMIT 1")->fetchColumn();
	if ($uid <= 0) {
		$tp->prepare('INSERT INTO users (email,email_confirmed,password,unlocked,reg_variant,time_registered,admin_created) VALUES (?,1,?,1,1,?,1)')
			->execute(array($adminLogin, $hash, (string) time()));
		$uid = (int) $tp->lastInsertId();
		$gid = (int) $tp->query('SELECT id FROM groups WHERE for_backend=1 LIMIT 1')->fetchColumn();
		if ($gid <= 0) {
			$gid = 3;
		}
		$tp->prepare('INSERT IGNORE INTO users_groups_bind (user_id,group_id) VALUES (?,?)')->execute(array($uid, $gid));
	} else {
		$tp->prepare('UPDATE users SET password=?, unlocked=1 WHERE user_id=?')->execute(array($hash, $uid));
	}
	$orders = (int) $tp->query('SELECT COUNT(*) FROM shop_orders')->fetchColumn();
	echo "Admin reset user_id={$uid} shop_orders={$orders}\n";
	echo "Login: {$adminLogin}\n";
	echo "Temp password: {$adminPass}\n";
	echo "DB password: {$dbPass}\n";
} catch (Exception $e) {
	echo 'Users: ' . $e->getMessage() . "\n";
}

echo "Done. ASAP isolated to {$dbName} (was {$oldDb}).\n";
