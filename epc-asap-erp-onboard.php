<?php
/**
 * Onboard ASAP as shared ERP-only tenant (Full ERP) on www.ecomae.com — no subdomain.
 * https://www.ecomae.com/epc-asap-erp-onboard.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(300);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_intro.php';
require_once __DIR__ . '/content/general_pages/epc_portal_erp_modules.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';

$apply = !empty($_GET['apply']);
$hostname = 'www.ecomae.com';
$siteKey = 'asap';
$adminLogin = 'asap_admin@asap-ae.com';
$demoLogin = 'asap_demo@asap-ae.com';

$fullErpMods = epc_portal_erp_modules_presets()['full_erp']['modules'];

function epc_asap_probe(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	$location = '';
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
			if (stripos($h, 'Location:') === 0) {
				$location = trim(substr($h, 9));
			}
		}
	}
	$text = is_string($body) ? $body : '';
	$flags = array();
	if (stripos($text, 'No DB connect') !== false) {
		$flags[] = 'no-db';
	}
	if (stripos($text, 'epc_erp_shell') !== false || stripos($text, 'erp-shell') !== false) {
		$flags[] = 'erp-shell';
	}
	return array('code' => $code, 'location' => $location, 'flags' => $flags);
}

function epc_asap_platform_pdo(): PDO
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

function epc_asap_tenant_db_creds(): array
{
	$siteKey = 'asap';
	$dbName = $siteKey;
	$dbUser = $siteKey;
	$dbPass = trim((string) ($_GET['db_password'] ?? ''));
	if ($dbPass !== '') {
		return array('db' => $dbName, 'user' => $dbUser, 'password' => $dbPass);
	}
	try {
		$platformPdo = epc_asap_platform_pdo();
		$st = $platformPdo->prepare('SELECT `db_name`, `db_user`, `db_password` FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($siteKey));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row && trim((string) ($row['db_name'] ?? '')) !== '' && trim((string) ($row['db_name'] ?? '')) !== 'docpart') {
			return array(
				'db' => (string) $row['db_name'],
				'user' => (string) ($row['db_user'] ?? $row['db_name']),
				'password' => (string) ($row['db_password'] ?? ''),
			);
		}
	} catch (Exception $e) {
		// fall through — dedicated DB must be provisioned via epc-asap-db-isolate.php
	}
	return array('db' => $dbName, 'user' => $dbUser, 'password' => '');
}

function epc_asap_cp_admin(PDO $tenantPdo, $cfg, string $login, string $password, bool $apply): array
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

echo "=== ASAP shared ERP-only onboard (www.ecomae.com) ===\n";
echo "hostname={$hostname} site_key={$siteKey} erp_only_shared=1\n";
echo 'erp_modules=' . count($fullErpMods) . " (Full ERP preset)\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

$adminPass = bin2hex(random_bytes(8)) . 'A!';
$demoPass = bin2hex(random_bytes(8)) . 'D!';
$dbCreds = epc_asap_tenant_db_creds();
$dbName = $dbCreds['db'];
$dbUser = $dbCreds['user'];
$dbPass = $dbCreds['password'];

try {
	$platformPdo = epc_asap_platform_pdo();
	echo "Platform DB: OK\n";
} catch (Exception $e) {
	exit('Platform DB FAIL: ' . $e->getMessage() . "\n");
}

try {
	$test = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
	$tables = (int) $test->query('SHOW TABLES')->rowCount();
	echo "Tenant DB {$dbName}: OK tables={$tables}\n";
} catch (Exception $e) {
	echo "Tenant DB FAIL: " . $e->getMessage() . "\n";
	echo "Run epc-asap-db-isolate.php?apply=1 first to create dedicated asap MySQL database.\n";
	if (!$apply) {
		exit("Fix tenant DB then re-run apply=1\n");
	}
	exit("Cannot onboard — ASAP requires isolated DB (not docpart).\n");
}

$post = array(
	'trade_name' => 'ASAP',
	'hostname' => $hostname,
	'site_key' => $siteKey,
	'industry_code' => 'erp_standalone',
	'hub_name' => 'Electronic World Group',
	'from_email' => 'admin@asap-ae.com',
	'status' => 'live',
	'erp_only' => '1',
	'erp_only_shared' => '1',
	'hosted_on_platform' => '1',
	'access_mode' => 'erp_only',
	'erp_modules_preset' => 'full_erp',
	'erp_modules' => $fullErpMods,
	'contact_person' => 'ASAP Administrator',
	'contact_email' => 'admin@asap-ae.com',
	'admin_email' => $adminLogin,
	'contact_phone' => '',
	'city' => 'Dubai',
	'country' => 'United Arab Emirates',
	'db_name' => $dbName,
	'db_user' => $dbUser,
	'db_password' => $dbPass,
	'notes' => 'ASAP shared ERP on www.ecomae.com — epc-asap-erp-onboard.php',
);

if (!$apply) {
	echo "\nDry run — would register:\n";
	echo json_encode(array(
		'hostname' => $hostname,
		'erp_only_shared' => 1,
		'access_mode' => 'erp_only',
		'erp_modules' => $fullErpMods,
		'db' => $dbName,
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE) . "\n";
	echo "\nRe-run with apply=1\n";
	exit;
}

$onboard = epc_portal_onboard_client($platformPdo, $post, 'epc-asap-erp-onboard.php');
echo 'Onboard: ' . ($onboard['ok'] ? 'OK' : 'FAIL') . ' — ' . ($onboard['message'] ?? '') . "\n";

$save = epc_portal_save_tenant($platformPdo, array(
	'site_key' => $siteKey,
	'hostname' => $hostname,
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
	'notes' => 'ASAP shared ERP live on www.ecomae.com',
));
echo 'Registry live: ' . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";

$sync = epc_portal_sync_tenant_packs_to_client_db($platformPdo, $siteKey);
echo 'Client sync: ' . ($sync['ok'] ? 'OK' : 'FAIL') . ' — ' . ($sync['message'] ?? '') . "\n";

define('_ASTEXE_', 1);
require_once '/home/ecomae/htdocs/www.ecomae.com/config.php';
$cfg = new DP_Config();
try {
	$tenantPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$adminResult = epc_asap_cp_admin($tenantPdo, $cfg, $adminLogin, $adminPass, true);
	$demoResult = epc_asap_cp_admin($tenantPdo, $cfg, $demoLogin, $demoPass, true);
	echo 'Admin user: ' . json_encode($adminResult, JSON_UNESCAPED_UNICODE) . "\n";
	echo 'Demo user: ' . json_encode($demoResult, JSON_UNESCAPED_UNICODE) . "\n";
} catch (Exception $e) {
	echo 'CP users: FAIL — ' . $e->getMessage() . "\n";
}

$paths = array(
	'cp_login' => 'https://www.ecomae.com/cp/',
	'erp_shell' => 'https://www.ecomae.com/cp/shop/finance/erp?epc_erp_shell=1',
);
echo "\n=== URL probes ===\n";
foreach ($paths as $label => $url) {
	$p = epc_asap_probe($url);
	echo "{$label}: HTTP {$p['code']}" . ($p['location'] !== '' ? ' → ' . $p['location'] : '')
		. (count($p['flags']) ? ' [' . implode(',', $p['flags']) . ']' : '') . "\n";
}

$modLabels = array();
foreach ($fullErpMods as $modId) {
	$reg = epc_portal_erp_modules_registry();
	if (isset($reg[$modId]['label'])) {
		$modLabels[] = $reg[$modId]['label'];
	}
}

echo "\n=== ACCESS HANDOFF (change passwords on first login) ===\n";
echo "Company: ASAP\n";
echo "Shared host: www.ecomae.com (no client domain)\n";
echo "CP login: https://www.ecomae.com/cp/\n";
echo "ERP shell (direct): https://www.ecomae.com/cp/shop/finance/erp?epc_erp_shell=1\n";
echo "Credentials doc: stage/docs/ECOM-ERP-SHARED-ACCESS.md\n";
echo "Super CP manage: https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?edit={$siteKey}\n";
echo "Admin login (email field): {$adminLogin}\n";
echo "Admin temp password: {$adminPass}\n";
echo "Demo login (email field): {$demoLogin}\n";
echo "Demo temp password: {$demoPass}\n";
echo "ERP modules enabled: " . implode(', ', $modLabels) . "\n";
echo "Disabled: storefront, catalogue, shop CP sidebar, commerce packs, per-client DNS\n";
echo "\nDone.\n";
