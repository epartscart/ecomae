<?php
/**
 * Provision / repair shared ERP-only tenant MySQL + registry credentials.
 * https://www.ecomae.com/epc-erp-tenant-provision.php?token=...&site_key=asapcustom&apply=1
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
$dbPass = trim((string) ($_GET['db_password'] ?? $_POST['db_password'] ?? ''));
$srcDb = trim((string) ($_GET['src_db'] ?? 'ecomae'));
$forceShared = !empty($_GET['force_shared']) || !empty($_POST['force_shared']);

if ($siteKey === '') {
	exit("site_key required\n");
}

echo "=== ERP tenant provision / repair ===\n";
echo "site_key={$siteKey} apply=" . ($apply ? 'yes' : 'no') . "\n\n";

function epc_erp_prov_platform_pdo(): PDO
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

function epc_erp_prov_db_connects(string $dbName, string $dbUser, string $dbPass): bool
{
	try {
		$pdo = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8;connect_timeout=5', $dbUser, $dbPass);
		$pdo->query('SELECT 1');
		return true;
	} catch (Throwable $e) {
		return false;
	}
}

function epc_erp_prov_missing_core_tables(PDO $tp): array
{
	$core = array('users', 'groups', 'sessions', 'templates', 'content');
	$missing = array();
	foreach ($core as $tbl) {
		try {
			$tp->query('SELECT 1 FROM `' . str_replace('`', '', $tbl) . '` LIMIT 1');
		} catch (Throwable $e) {
			$missing[] = $tbl;
		}
	}
	return $missing;
}

function epc_erp_prov_seed_cp_rows(PDO $tp, PDO $srcPdo, string $srcDb, string $destDb, string $dbUser, string $dbPass, ?array $srcCreds = null): array
{
	$log = array();
	$srcEsc = str_replace('`', '', $srcDb);
	$destEsc = str_replace('`', '', $destDb);
	try {
		$mainCount = (int) $tp->query('SELECT COUNT(*) FROM `content` WHERE `main_flag` = 1 AND `is_frontend` = 0')->fetchColumn();
		$pluginCount = (int) $tp->query('SELECT COUNT(*) FROM `plugins` WHERE `is_frontend` = 0')->fetchColumn();
		if ($mainCount > 0 && $pluginCount > 0) {
			$log[] = 'seed_skip=login_content_exists';
			return $log;
		}
	} catch (Throwable $e) {
		$log[] = 'seed_probe_fail: ' . $e->getMessage();
	}
	$essential = array(
		'lang_languages', 'groups', 'templates', 'modules', 'plugins', 'content',
	);
	foreach ($essential as $tbl) {
		$tblEsc = str_replace('`', '', $tbl);
		try {
			$tp->exec('DELETE FROM `' . $tblEsc . '`');
		} catch (Throwable $e) {
			$log[] = 'seed_delete_' . $tblEsc . '_fail: ' . $e->getMessage();
		}
	}
	$srcUser = $srcCreds ? (string) ($srcCreds['user'] ?? '') : '';
	$srcPass = $srcCreds ? (string) ($srcCreds['password'] ?? '') : '';
	if ($srcUser !== '' && $srcPass !== '') {
		$tableList = implode(' ', array_map('escapeshellarg', $essential));
		$cmd = 'mysqldump --single-transaction --no-create-info -u ' . escapeshellarg($srcUser)
			. ' -p' . escapeshellarg($srcPass) . ' ' . escapeshellarg($srcEsc) . ' ' . $tableList
			. ' 2>/dev/null | mysql -u ' . escapeshellarg($dbUser)
			. ' -p' . escapeshellarg($dbPass) . ' ' . escapeshellarg($destEsc) . ' 2>&1';
		$pipe = epc_clp_run_cmd($cmd);
		$log[] = 'seed_pipe=' . trim((string) ($pipe['output'] ?? ''));
	}
	foreach ($essential as $tbl) {
		$tblEsc = str_replace('`', '', $tbl);
		try {
			$count = (int) $tp->query('SELECT COUNT(*) FROM `' . $tblEsc . '`')->fetchColumn();
			$log[] = 'seed_table=' . $tblEsc . ' rows=' . $count;
		} catch (Throwable $e) {
			$log[] = 'seed_count_' . $tblEsc . '_fail: ' . $e->getMessage();
		}
	}
	if (function_exists('epc_portal_demo_seed_erp_cp_content')) {
		require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
		$erpSeed = epc_portal_demo_seed_erp_cp_content($tp);
		$log[] = 'erp_cp_content=' . json_encode($erpSeed);
	}
	return $log;
}

function epc_erp_prov_clone_schema(string $srcDb, string $dbName, string $dbUser, string $dbPass, ?array $srcCreds = null): array
{
	$srcUser = $srcCreds ? (string) ($srcCreds['user'] ?? '') : '';
	$srcPass = $srcCreds ? (string) ($srcCreds['password'] ?? '') : '';
	if ($srcUser !== '' && $srcPass !== '') {
		$cmd = 'mysqldump --single-transaction --no-data -u ' . escapeshellarg($srcUser)
			. ' -p' . escapeshellarg($srcPass) . ' ' . escapeshellarg($srcDb)
			. ' 2>/dev/null | mysql -u ' . escapeshellarg($dbUser)
			. ' -p' . escapeshellarg($dbPass) . ' ' . escapeshellarg($dbName) . ' 2>&1';
	} else {
		$cmd = 'mysqldump --single-transaction --no-data ' . escapeshellarg($srcDb)
			. ' 2>/dev/null | mysql -u ' . escapeshellarg($dbUser)
			. ' -p' . escapeshellarg($dbPass) . ' ' . escapeshellarg($dbName) . ' 2>&1';
	}
	$out = epc_clp_run_cmd($cmd);
	$log = array((string) ($out['output'] ?? ''));
	if ((int) ($out['code'] ?? 1) !== 0 || stripos((string) ($out['output'] ?? ''), 'error') !== false) {
		$dumpGz = '/tmp/epc-tenant-schema-' . preg_replace('/[^a-z0-9_]/', '', $dbName) . '-' . time() . '.sql.gz';
		@unlink($dumpGz);
		$exp = epc_clp_run_cmd('/usr/bin/clpctl db:export --databaseName=' . escapeshellarg($srcDb) . ' --file=' . escapeshellarg($dumpGz));
		$log[] = 'fallback export: ' . (string) ($exp['output'] ?? '');
		if (is_file($dumpGz)) {
			$imp = epc_clp_run_cmd('/usr/bin/clpctl db:import --databaseName=' . escapeshellarg($dbName) . ' --file=' . escapeshellarg($dumpGz));
			$log[] = 'fallback import: ' . (string) ($imp['output'] ?? '');
		}
	}
	return array('output' => implode("\n", $log), 'code' => (int) ($out['code'] ?? 1));
}

try {
	$platformPdo = epc_erp_prov_platform_pdo();
} catch (Throwable $e) {
	exit('platform_pdo_fail: ' . $e->getMessage() . "\n");
}

$platCfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$platDbUser = 'ecomae';
$platDbPass = '';
if (is_file($platCfgFile)) {
	$epc_config_local = null;
	include $platCfgFile;
	$platDbUser = trim((string) ($epc_config_local['user'] ?? 'ecomae'));
	$platDbPass = trim((string) ($epc_config_local['password'] ?? ''));
}
$srcCreds = array('user' => $platDbUser, 'password' => $platDbPass);

$row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
if ($row === null) {
	exit("tenant_not_in_registry: {$siteKey}\n");
}

$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($row['db_name'] ?? $siteKey))));
$dbUser = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($row['db_user'] ?? $dbName))));
if ($dbUser === '') {
	$dbUser = $dbName;
}
$registryPass = trim((string) ($row['db_password'] ?? ''));
if ($dbPass === '' && $registryPass !== '') {
	$dbPass = $registryPass;
}
if ($dbPass === '') {
	$dbPass = epc_portal_tenant_control_generate_password();
	echo "generated_password={$dbPass}\n";
}

echo 'registry db=' . $dbName . ' user=' . $dbUser . ' erp_only_shared=' . (int) ($row['erp_only_shared'] ?? 0)
	. ' industry=' . ($row['industry_code'] ?? '') . ' hostname=' . ($row['hostname'] ?? '') . "\n";

$connectBefore = epc_erp_prov_db_connects($dbName, $dbUser, $dbPass);
echo 'connect_before=' . ($connectBefore ? 'ok' : 'fail') . "\n";

if (!$connectBefore) {
	if (!$apply) {
		echo "Would provision MySQL database {$dbName} / user {$dbUser}\n";
		echo "Fallback if CREATE fails: claim a ready demo DB pool slot\n";
	} else {
		$prov = epc_portal_demo_provision_database_raw($dbName, $dbUser, $dbPass);
		echo "provision_ok=" . (!empty($prov['ok']) ? 'yes' : 'no') . "\n";
		if (!empty($prov['log']) && is_array($prov['log'])) {
			foreach (array_slice($prov['log'], -8) as $line) {
				echo '  ' . $line . "\n";
			}
		}
		if (empty($prov['ok'])) {
			// CloudPanel may lack db:add / stale admin password — use pre-seeded pool.
			echo "provision_failed — trying demo DB pool claim...\n";
			$claimed = epc_portal_demo_pool_claim($platformPdo, $siteKey);
			if ($claimed === null) {
				exit("provision_failed (no ready pool DB; seed via epc-demo-pool-seed.php)\n");
			}
			$dbName = (string) $claimed['db_name'];
			$dbUser = (string) $claimed['db_user'];
			$dbPass = (string) $claimed['db_password'];
			echo "pool_claim=ok db={$dbName} user={$dbUser} pool_id=" . (int) ($claimed['pool_id'] ?? 0) . "\n";
		} elseif (!empty($prov['db_name'])) {
			$dbName = (string) $prov['db_name'];
		}
	}
}

if ($apply && !epc_erp_prov_db_connects($dbName, $dbUser, $dbPass)) {
	exit("connect_after_provision=fail\n");
}

$tables = 0;
$missingCore = array();
if (epc_erp_prov_db_connects($dbName, $dbUser, $dbPass)) {
	try {
		$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
		$tables = (int) $tp->query('SHOW TABLES')->rowCount();
		echo "tables={$tables}\n";
		$missingCore = epc_erp_prov_missing_core_tables($tp);
		if ($missingCore !== array()) {
			echo 'missing_core=' . implode(',', $missingCore) . "\n";
		}
	} catch (Throwable $e) {
		echo 'tables_probe_fail: ' . $e->getMessage() . "\n";
	}
}

$needsSchemaClone = $tables === 0 || $missingCore !== array();
if (!$apply && $needsSchemaClone) {
	echo "Would clone schema from {$srcDb} (empty or missing core CP tables)\n";
}
if ($apply && $needsSchemaClone) {
	echo "cloning_schema from {$srcDb}...\n";
	$clone = epc_erp_prov_clone_schema($srcDb, $dbName, $dbUser, $dbPass, $srcCreds);
	echo $clone['output'] . "\n";
	try {
		$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
		$tables = (int) $tp->query('SHOW TABLES')->rowCount();
		$missingCore = epc_erp_prov_missing_core_tables($tp);
		echo "tables_after_clone={$tables}\n";
		if ($missingCore !== array()) {
			echo 'missing_core_after=' . implode(',', $missingCore) . "\n";
		}
		try {
			$srcPdo = new PDO(
				'mysql:host=127.0.0.1;dbname=' . $srcDb . ';charset=utf8',
				$platDbUser,
				$platDbPass,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			foreach (epc_erp_prov_seed_cp_rows($tp, $srcPdo, $srcDb, $dbName, $dbUser, $dbPass, $srcCreds) as $line) {
				echo $line . "\n";
			}
		} catch (Throwable $e) {
			echo 'seed_cp_rows_fail: ' . $e->getMessage() . "\n";
		}
	} catch (Throwable $e) {
		echo 'clone_verify_fail: ' . $e->getMessage() . "\n";
	}
}

$saveData = array(
	'site_key' => $siteKey,
	'hostname' => (string) ($row['hostname'] ?? 'www.ecomae.com'),
	'industry_code' => (string) ($row['industry_code'] ?? 'erp_standalone'),
	'status' => (string) ($row['status'] ?? 'live'),
	'trade_name' => (string) ($row['trade_name'] ?? $siteKey),
	'hub_name' => (string) ($row['hub_name'] ?? ''),
	'from_email' => (string) ($row['from_email'] ?? ''),
	'db_name' => $dbName,
	'db_user' => $dbUser,
	'db_password' => $dbPass,
	'notes' => (string) ($row['notes'] ?? '') . ' | epc-erp-tenant-provision ' . date('Y-m-d H:i'),
);

$shouldShared = !empty($row['erp_only_shared'])
	|| (string) ($row['industry_code'] ?? '') === 'erp_standalone'
	|| $forceShared;
if ($shouldShared) {
	$saveData['erp_only_shared'] = 1;
	$saveData['hosted_on'] = 'platform';
	$saveData['hostname'] = 'www.ecomae.com';
}

if ($apply) {
	$save = epc_portal_save_tenant($platformPdo, $saveData);
	echo 'registry_save=' . (!empty($save['ok']) ? 'ok' : 'fail') . ' ' . ($save['message'] ?? '') . "\n";
} else {
	echo "Would update registry with db_password and shared ERP flags\n";
}

require_once __DIR__ . '/content/general_pages/epc_portal_erp_modules.php';

function epc_erp_prov_resolve_modules_preset(array $row): string
{
	$explicit = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['erp_modules_preset'] ?? $_POST['erp_modules_preset'] ?? ''))));
	if ($explicit !== '') {
		return $explicit;
	}
	$intro = array();
	if (!empty($row['intro_json'])) {
		$decoded = json_decode((string) $row['intro_json'], true);
		if (is_array($decoded) && !empty($decoded['erp_modules_preset'])) {
			return preg_replace('/[^a-z0-9_]/', '', strtolower((string) $decoded['erp_modules_preset']));
		}
	}
	return epc_portal_industry_erp_modules_preset((string) ($row['industry_code'] ?? 'erp_standalone'));
}

if ($apply && epc_erp_prov_db_connects($dbName, $dbUser, $dbPass)) {
	try {
		$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
		$settings = epc_portal_load_site_settings($tp);
		$industry = (string) ($row['industry_code'] ?? 'erp_standalone');
		$presetId = epc_erp_prov_resolve_modules_preset($row);
		$presets = epc_portal_erp_modules_presets();
		$mods = isset($presets[$presetId]['modules'])
			? epc_portal_erp_modules_normalize_list($presets[$presetId]['modules'])
			: epc_portal_erp_modules_default_ids('erp_only');
		$settings['access_mode'] = 'erp_only';
		$settings['industry_code'] = $industry;
		$settings['erp_modules'] = $mods;
		epc_portal_save_site_settings($tp, $settings);
		$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
		$intro = array();
		if (!empty($row['intro_json'])) {
			$decoded = json_decode((string) $row['intro_json'], true);
			if (is_array($decoded)) {
				$intro = $decoded;
			}
		}
		$intro['erp_modules_preset'] = $presetId;
		$intro['erp_modules'] = $mods;
		$platformPdo->prepare(
			'UPDATE `epc_portal_tenants` SET `intro_json` = ?, `updated_at` = ? WHERE `site_key` = ?'
		)->execute(array(json_encode($intro, JSON_UNESCAPED_UNICODE), time(), $key));
		echo 'erp_modules_preset=' . $presetId . ' count=' . count($mods) . ' modules=' . implode(',', $mods) . "\n";
	} catch (Throwable $e) {
		echo 'erp_modules_sync_fail: ' . $e->getMessage() . "\n";
	}
}

require_once __DIR__ . '/content/general_pages/epc_client_erp_router.php';
echo 'client_erp_login=https://www.ecomae.com' . epc_client_erp_login_url($siteKey) . "\n";
echo 'client_erp_shell=https://www.ecomae.com' . epc_client_erp_shell_url($siteKey) . "\n";

if (epc_erp_prov_db_connects($dbName, $dbUser, $dbPass)) {
	try {
		$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		require_once __DIR__ . '/content/shop/finance/epc_erp_schema.php';
		epc_erp_ensure_schema($tp);
		echo "erp_schema=ok\n";
		$tp->exec("CREATE TABLE IF NOT EXISTS `epc_price_settings` (
			`setting_key` varchar(128) NOT NULL,
			`setting_value` text,
			PRIMARY KEY (`setting_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8");
		$tp->prepare("INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES ('vat_percent', '5.00') ON DUPLICATE KEY UPDATE `setting_value` = `setting_value`")->execute();
		echo "price_settings=ok\n";

		require_once __DIR__ . '/config.php';
		$cfg = new DP_Config();
		if (function_exists('epc_portal_tenant_control_resolve_admin_email')) {
			$adminEmail = epc_portal_tenant_control_resolve_admin_email($row);
		} elseif (function_exists('epc_portal_tenant_control_admin_email')) {
			$adminEmail = epc_portal_tenant_control_admin_email($row);
		} else {
			$adminEmail = trim((string) ($row['contact_email'] ?? ''));
		}
		if ($adminEmail === '' || !filter_var($adminEmail, FILTER_VALIDATE_EMAIL)) {
			$adminEmail = trim((string) ($row['from_email'] ?? ''));
		}
		if ($adminEmail === '' || !filter_var($adminEmail, FILTER_VALIDATE_EMAIL)) {
			$adminEmail = $siteKey . '_admin@ecomae.com';
		}
		$adminPass = trim((string) ($_GET['admin_password'] ?? $_POST['admin_password'] ?? ''));
		if ($adminPass === '') {
			$adminPass = bin2hex(random_bytes(8)) . 'A!';
		}
		$st = $tp->prepare('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
		$st->execute();
		$root = $st->fetch(PDO::FETCH_ASSOC);
		$groups = $root ? array((int) $root['id']) : array(3);
		$hash = md5($adminPass . $cfg->secret_succession);
		$ust = $tp->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
		$ust->execute(array($adminEmail));
		$urow = $ust->fetch(PDO::FETCH_ASSOC);
		if (!$urow) {
			if ($apply) {
				$tp->prepare(
					'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `admin_created`)
					 VALUES (?, 1, ?, 1, 1, ?, 1)'
				)->execute(array($adminEmail, $hash, (string) time()));
				$userId = (int) $tp->lastInsertId();
				@$tp->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')
					->execute(array($userId, 'name', (string) ($row['trade_name'] ?? $siteKey) . ' Administrator'));
				$ins = $tp->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
				foreach ($groups as $gid) {
					$ins->execute(array($userId, (int) $gid));
				}
				echo "admin_created={$adminEmail} user_id={$userId}\n";
			} else {
				echo "Would create admin user {$adminEmail}\n";
			}
		} else {
			if ($apply) {
				$tp->prepare('UPDATE `users` SET `password` = ?, `unlocked` = 1, `email_confirmed` = 1 WHERE `user_id` = ?')
					->execute(array($hash, (int) $urow['user_id']));
				echo "admin_reset={$adminEmail} user_id=" . (int) $urow['user_id'] . "\n";
			} else {
				echo "admin_exists={$adminEmail} user_id=" . (int) $urow['user_id'] . "\n";
			}
		}
		if ($apply) {
			echo "admin_password={$adminPass}\n";
			$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
			$platformPdo->prepare(
				'UPDATE `epc_portal_tenants` SET `operator_temp_password` = ?, `updated_at` = ? WHERE `site_key` = ?'
			)->execute(array($adminPass, time(), $key));
			echo "operator_temp_password_saved=yes\n";
			$intro = array();
			if (!empty($row['intro_json'])) {
				$decoded = json_decode((string) $row['intro_json'], true);
				if (is_array($decoded)) {
					$intro = $decoded;
				}
			}
			if (empty($intro['admin_cp_email'])) {
				$intro['admin_cp_email'] = $adminEmail;
				if (empty($intro['admin_email'])) {
					$intro['admin_email'] = $adminEmail;
				}
				$platformPdo->prepare(
					'UPDATE `epc_portal_tenants` SET `intro_json` = ?, `from_email` = COALESCE(NULLIF(`from_email`, \'\'), ?), `updated_at` = ? WHERE `site_key` = ?'
				)->execute(array(json_encode($intro, JSON_UNESCAPED_UNICODE), $adminEmail, time(), $key));
				echo "registry_admin_cp_email_saved={$adminEmail}\n";
			}
		}

	try {
		$mainCount = (int) $tp->query('SELECT COUNT(*) FROM `content` WHERE `main_flag` = 1 AND `is_frontend` = 0')->fetchColumn();
		$pluginCount = (int) $tp->query('SELECT COUNT(*) FROM `plugins` WHERE `is_frontend` = 0')->fetchColumn();
		if ($mainCount < 1 || $pluginCount < 1) {
			echo "seeding_cp_login_content...\n";
			$srcPdo = new PDO(
				'mysql:host=127.0.0.1;dbname=' . $srcDb . ';charset=utf8',
				$platDbUser,
				$platDbPass,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			foreach (epc_erp_prov_seed_cp_rows($tp, $srcPdo, $srcDb, $dbName, $dbUser, $dbPass, $srcCreds) as $line) {
				echo $line . "\n";
			}
			$st = $tp->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
			$st->execute(array($adminEmail));
			$urow2 = $st->fetch(PDO::FETCH_ASSOC);
			if (is_array($urow2)) {
				$gst = $tp->prepare('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
				$gst->execute();
				$grow = $gst->fetch(PDO::FETCH_ASSOC);
				$gid = $grow ? (int) $grow['id'] : 3;
				$tp->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)')
					->execute(array((int) $urow2['user_id'], $gid));
				echo "admin_group_rebind=user_id=" . (int) $urow2['user_id'] . " group_id={$gid}\n";
			}
		}
	} catch (Throwable $e) {
		echo 'seed_login_content_fail: ' . $e->getMessage() . "\n";
	}
} catch (Throwable $e) {
		echo 'tenant_repair_fail: ' . $e->getMessage() . "\n";
	}
}

echo "done\n";
