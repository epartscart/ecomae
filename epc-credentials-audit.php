<?php
/**
 * CP credentials audit — list backend users per DB, test known passwords, optional ASAP reset.
 * https://www.ecomae.com/epc-credentials-audit.php?token=epartscart-deploy-2026
 * https://www.ecomae.com/epc-credentials-audit.php?token=...&format=json
 * https://www.ecomae.com/epc-credentials-audit.php?token=...&reset_asap=1&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

$format = strtolower(trim((string) ($_GET['format'] ?? 'text')));
$resetAsap = !empty($_GET['reset_asap']);
$apply = !empty($_GET['apply']);
$testHttp = !isset($_GET['test_http']) || $_GET['test_http'] !== '0';

if ($format === 'json') {
	header('Content-Type: application/json; charset=utf-8');
} else {
	header('Content-Type: text/plain; charset=utf-8');
}

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_intro.php';
require_once __DIR__ . '/content/general_pages/epc_portal_erp_modules.php';

/** @return array<int> */
function epc_cred_backend_group_ids(PDO $pdo): array
{
	$st = $pdo->query('SELECT `id`, `count` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = $st->fetch(PDO::FETCH_ASSOC);
	if (!$root) {
		return array();
	}
	$ids = array();
	$walk = function (int $parentId) use ($pdo, &$walk, &$ids): void {
		$ids[$parentId] = true;
		$ch = $pdo->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($parentId));
		while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
			if ((int) $row['count'] > 0) {
				$walk((int) $row['id']);
			} else {
				$ids[(int) $row['id']] = true;
			}
		}
	};
	$walk((int) $root['id']);
	return array_map('intval', array_keys($ids));
}

function epc_cred_user_display_name(PDO $pdo, int $userId): string
{
	$st = $pdo->prepare(
		"SELECT MAX(CASE WHEN `data_key`='name' THEN `data_value` END) AS n,
		        MAX(CASE WHEN `data_key`='surname' THEN `data_value` END) AS s
		 FROM `users_profiles` WHERE `user_id` = ?"
	);
	$st->execute(array($userId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$name = trim((string) (($row['n'] ?? '') . ' ' . ($row['s'] ?? '')));
	return $name !== '' ? $name : '';
}

function epc_cred_password_candidates(): array
{
	return array(
		'12345678' => 'documented Super CP default',
		'EpcStaff2026!' => 'ERP staff seed default',
		'AsapAudit2026!A' => 'ASAP audit temp password (2026-05-30)',
	);
}

function epc_cred_test_password(string $storedHash, string $secret, array $candidates): array
{
	$matches = array();
	foreach ($candidates as $plain => $note) {
		if (hash_equals($storedHash, md5($plain . $secret))) {
			$matches[] = array('password' => $plain, 'source' => $note);
		}
	}
	return $matches;
}

function epc_cred_tenant_pdo(array $row): ?PDO
{
	$db = trim((string) ($row['db_name'] ?? ''));
	$user = trim((string) ($row['db_user'] ?? ''));
	$pass = (string) ($row['db_password'] ?? '');
	if ($db === '' || $user === '') {
		return null;
	}
	try {
		return new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
		);
	} catch (Throwable $e) {
		return null;
	}
}

function epc_cred_audit_db(PDO $pdo, string $secret, string $contextLabel, array $siteSettings = null): array
{
	$backendGids = epc_cred_backend_group_ids($pdo);
	$candidates = epc_cred_password_candidates();
	$usersOut = array();
	try {
		$st = $pdo->query(
			'SELECT u.`user_id`, u.`email`, u.`phone`, u.`unlocked`, u.`email_confirmed`, u.`password`
			 FROM `users` u ORDER BY u.`user_id` ASC'
		);
		while ($u = $st->fetch(PDO::FETCH_ASSOC)) {
			$uid = (int) $u['user_id'];
			$bind = $pdo->prepare('SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?');
			$bind->execute(array($uid));
			$userGids = array_map('intval', $bind->fetchAll(PDO::FETCH_COLUMN));
			$isBackend = count(array_intersect($userGids, $backendGids)) > 0;
			if (!$isBackend) {
				continue;
			}
			$groupNames = array();
			if ($userGids !== array()) {
				$ph = implode(',', array_fill(0, count($userGids), '?'));
				$gs = $pdo->prepare("SELECT `id`, `value` FROM `groups` WHERE `id` IN ($ph)");
				$gs->execute($userGids);
				while ($g = $gs->fetch(PDO::FETCH_ASSOC)) {
					$groupNames[] = (string) $g['value'] . ' (#' . $g['id'] . ')';
				}
			}
			$pwMatches = epc_cred_test_password((string) $u['password'], $secret, $candidates);
			$usersOut[] = array(
				'user_id' => $uid,
				'email' => (string) ($u['email'] ?? ''),
				'phone' => (string) ($u['phone'] ?? ''),
				'name' => epc_cred_user_display_name($pdo, $uid),
				'unlocked' => (int) ($u['unlocked'] ?? 0),
				'email_confirmed' => (int) ($u['email_confirmed'] ?? 0),
				'groups' => $groupNames,
				'password_known' => count($pwMatches) > 0,
				'password_matches' => $pwMatches,
			);
		}
	} catch (Throwable $e) {
		return array(
			'context' => $contextLabel,
			'error' => $e->getMessage(),
			'users' => array(),
		);
	}
	$settings = $siteSettings ?? array();
	return array(
		'context' => $contextLabel,
		'access_mode' => (string) ($settings['access_mode'] ?? ''),
		'erp_modules' => isset($settings['erp_modules']) && is_array($settings['erp_modules'])
			? array_values($settings['erp_modules']) : array(),
		'industry_code' => (string) ($settings['industry_code'] ?? ''),
		'backend_user_count' => count($usersOut),
		'users' => $usersOut,
	);
}

function epc_cred_http_login(string $baseUrl, string $email, string $password, string $erpTenantPick = ''): array
{
	$loginUrl = rtrim($baseUrl, '/') . '/';
	$post = array(
		'authentication' => 'authentication',
		'auth_contact' => $email,
		'auth_contact_select' => 'email',
		'password' => $password,
	);
	if ($erpTenantPick !== '') {
		$post['epc_erp_tenant_pick'] = $erpTenantPick;
	}
	$body = http_build_query($post);
	$cookieFile = tempnam(sys_get_temp_dir(), 'epc_cred_');
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => 'POST',
			'header' => "Content-Type: application/x-www-form-urlencoded\r\n"
				. "Content-Length: " . strlen($body) . "\r\n",
			'content' => $body,
			'timeout' => 25,
			'ignore_errors' => true,
			'follow_location' => 0,
		),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	@file_get_contents($loginUrl, false, $ctx);
	$code1 = 0;
	$location = '';
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code1 = (int) $m[1];
			}
			if (stripos($h, 'Location:') === 0) {
				$location = trim(substr($h, 9));
			}
		}
	}
	$cookieHeader = '';
	if (is_file($cookieFile)) {
		@unlink($cookieFile);
	}
	// Re-run with CURLOPT if available for cookies — fallback: parse Set-Cookie from headers
	$cookies = array();
	if (isset($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (stripos($h, 'Set-Cookie:') === 0) {
				$part = trim(substr($h, 11));
				$semi = strpos($part, ';');
				if ($semi !== false) {
					$part = substr($part, 0, $semi);
				}
				$cookies[] = $part;
			}
		}
	}
	$cookieHeader = implode('; ', $cookies);
	$dashUrl = rtrim($baseUrl, '/') . '/shop/tenant_hub/tenant_hub';
	$dashCtx = stream_context_create(array(
		'http' => array(
			'method' => 'GET',
			'header' => $cookieHeader !== '' ? "Cookie: {$cookieHeader}\r\n" : '',
			'timeout' => 25,
			'ignore_errors' => true,
		),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$dashBody = @file_get_contents($dashUrl, false, $dashCtx);
	$code2 = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code2 = (int) $m[1];
			}
		}
	}
	$text = is_string($dashBody) ? $dashBody : '';
	$loggedIn = stripos($text, 'wrong_authentication') === false
		&& (stripos($text, 'tenant_hub') !== false || stripos($text, 'Tenant hub') !== false
			|| stripos($text, 'erp-shell') !== false || stripos($text, 'shop/finance/erp') !== false
			|| stripos($text, 'admin_session') !== false);
	$loginOk = ($code1 === 302 || $code1 === 301 || $location !== '') && stripos($location, 'wrong') === false;
	if (!$loginOk && $code1 === 200 && stripos((string) @file_get_contents($loginUrl, false, $ctx), 'wrong_authentication') === false) {
		// Some installs redirect via JS — treat absence of wrong_authentication on POST response as weak success
	}
	return array(
		'login_http_code' => $code1,
		'redirect' => $location,
		'dashboard_http_code' => $code2,
		'login_likely_ok' => $loginOk || $loggedIn,
		'dashboard_likely_ok' => $loggedIn && $code2 === 200,
	);
}

function epc_cred_reset_asap_admin(PDO $tenantPdo, $cfg, bool $apply): array
{
	$login = 'asap_admin@asap-ae.com';
	$tempPass = 'AsapAudit2026!A';
	$hash = md5($tempPass . $cfg->secret_succession);
	$st = $tenantPdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($login));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'message' => 'ASAP admin user missing', 'login' => $login);
	}
	$uid = (int) $row['user_id'];
	if ($apply) {
		$tenantPdo->prepare('UPDATE `users` SET `password` = ?, `unlocked` = 1, `email_confirmed` = 1 WHERE `user_id` = ?')
			->execute(array($hash, $uid));
	}
	return array(
		'ok' => true,
		'login' => $login,
		'user_id' => $uid,
		'password_reset' => $apply,
		'temp_password' => $apply ? $tempPass : '(pass apply=1 to set AsapAudit2026!A)',
	);
}

function epc_cred_pdo_for_creds(array $creds): ?PDO
{
	$db = trim((string) ($creds['db'] ?? ''));
	$user = trim((string) ($creds['user'] ?? ''));
	$pass = (string) ($creds['password'] ?? '');
	if ($db === '' || $user === '') {
		return null;
	}
	try {
		return new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
		);
	} catch (Throwable $e) {
		return null;
	}
}

function epc_cred_discover_databases(PDO $seedPdo, array $seedCreds): array
{
	$found = array();
	try {
		$st = $seedPdo->query('SHOW DATABASES');
		while ($row = $st->fetch(PDO::FETCH_NUM)) {
			$name = strtolower((string) ($row[0] ?? ''));
			if ($name === '' || in_array($name, array('information_schema', 'mysql', 'performance_schema', 'sys'), true)) {
				continue;
			}
			$found[$name] = array(
				'db' => $name,
				'user' => (string) ($seedCreds['user'] ?? ''),
				'password' => (string) ($seedCreds['password'] ?? ''),
			);
		}
	} catch (Throwable $e) {
		// ignore
	}
	return $found;
}

function epc_cred_db_has_users(PDO $pdo): bool
{
	try {
		$pdo->query('SELECT 1 FROM `users` LIMIT 1');
		return true;
	} catch (Throwable $e) {
		return false;
	}
}

function epc_cred_platform_cfg_db(): string
{
	try {
		$cfgFile = $_SERVER['DOCUMENT_ROOT'] . '/config.local.php';
		if (is_file($cfgFile)) {
			$epc_config_local = null;
			include $cfgFile;
			if (isset($epc_config_local['db']) && (string) $epc_config_local['db'] !== '') {
				return (string) $epc_config_local['db'];
			}
		}
	} catch (Throwable $e) {
	}
	return (string) (new DP_Config())->db;
}

function epc_cred_known_virtual_tenants(): array
{
	$out = array();
	foreach (epc_portal_tenant_templates() as $siteKey => $tpl) {
		if ($siteKey === 'erp_only_demo') {
			continue;
		}
		$host = (string) ($tpl['hostname'] ?? '');
		$out[] = array(
			'site_key' => $siteKey,
			'hostname' => $host,
			'trade_name' => (string) ($tpl['trade_name'] ?? $siteKey),
			'from_email' => (string) ($tpl['from_email'] ?? ''),
			'erp_only_shared' => !empty($tpl['erp_only_shared']) ? 1 : 0,
			'access_mode' => (string) ($tpl['access_mode'] ?? 'full'),
			'industry_code' => (string) ($tpl['industry'] ?? ''),
			'status' => 'template',
			'db_name' => !empty($tpl['erp_only_shared']) ? $siteKey : 'docpart',
			'virtual' => true,
		);
	}
	return $out;
}

$report = array(
	'audited_at' => gmdate('c'),
	'platform_ip' => epc_portal_platform_ip(),
	'super_cp_url' => 'https://www.ecomae.com/cp/',
	'secret_source' => 'config.php secret_succession (same across tenants on this docroot)',
	'tenants' => array(),
	'databases' => array(),
	'http_tests' => array(),
	'asap_reset' => null,
);

try {
	$platformPdo = epc_portal_platform_pdo();
	epc_portal_db_ensure($platformPdo);
} catch (Throwable $e) {
	$msg = array('status' => false, 'error' => 'Platform DB: ' . $e->getMessage());
	echo $format === 'json' ? json_encode($msg, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE) : $msg['error'];
	exit;
}

$secret = (string) $cfg->secret_succession;
$platDbName = (string) $platformPdo->query('SELECT DATABASE()')->fetchColumn();
$report['platform_db'] = $platDbName;
$auditedDbKeys = array();

// Platform Super CP
try {
	$platSettings = epc_portal_load_site_settings_for_host($platformPdo, 'www.ecomae.com');
} catch (Throwable $e) {
	$platSettings = array();
}
$report['databases'][] = array_merge(
	array('db_name' => $platDbName, 'role' => 'super_cp', 'site_key' => 'ecomae', 'hostname' => 'www.ecomae.com'),
	epc_cred_audit_db($platformPdo, $secret, 'Super CP — platform DB ' . $platDbName, $platSettings)
);
$auditedDbKeys[$platDbName] = true;

$st = $platformPdo->query(
	'SELECT * FROM `epc_portal_tenants` ORDER BY `erp_only_shared` DESC, `site_key` ASC'
);
$tenantRows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

foreach ($tenantRows as $row) {
	$siteKey = (string) $row['site_key'];
	$hostname = (string) $row['hostname'];
	$dbName = (string) $row['db_name'];
	$intro = array();
	if (!empty($row['intro_json'])) {
		$decoded = json_decode((string) $row['intro_json'], true);
		if (is_array($decoded)) {
			$intro = $decoded;
		}
	}
	$adminEmail = trim((string) ($intro['admin_cp_email'] ?? $row['from_email'] ?? ''));
	$tenantMeta = array(
		'site_key' => $siteKey,
		'hostname' => $hostname,
		'trade_name' => (string) ($row['trade_name'] ?? ''),
		'db_name' => $dbName,
		'status' => (string) ($row['status'] ?? ''),
		'erp_only_shared' => (int) ($row['erp_only_shared'] ?? 0),
		'hosted_on' => (string) ($row['hosted_on'] ?? ''),
		'registry_admin_email' => $adminEmail,
		'cp_login_url' => ((int) ($row['erp_only_shared'] ?? 0) === 1)
			? 'https://www.ecomae.com/cp/'
			: 'https://' . preg_replace('/^www\./', 'www.', $hostname) . '/cp/',
		'erp_shell_url' => ((int) ($row['erp_only_shared'] ?? 0) === 1)
			? 'https://www.ecomae.com/cp/shop/finance/erp?epc_erp_shell=1'
			: 'https://' . preg_replace('/^www\./', 'www.', $hostname) . '/cp/shop/finance/erp?epc_erp_shell=1',
	);
	$report['tenants'][] = $tenantMeta;

	$tpdo = epc_cred_tenant_pdo($row);
	if (!$tpdo instanceof PDO) {
		$report['databases'][] = array(
			'db_name' => $dbName,
			'site_key' => $siteKey,
			'role' => (int) ($row['erp_only_shared'] ?? 0) === 1 ? 'shared_erp' : 'tenant_cp',
			'hostname' => $hostname,
			'error' => 'Cannot connect to tenant DB',
			'users' => array(),
		);
		continue;
	}
	$settings = array();
	try {
		epc_portal_db_ensure($tpdo);
		$settings = epc_portal_load_site_settings_for_host($tpdo, $hostname);
	} catch (Throwable $e) {
		$settings = array();
	}
	$label = ((int) ($row['erp_only_shared'] ?? 0) === 1 ? 'Shared ERP' : 'Tenant CP')
		. " — {$siteKey} ({$dbName}) @ {$hostname}";
	$audit = epc_cred_audit_db($tpdo, $secret, $label, $settings);
	$report['databases'][] = array_merge(
		array(
			'db_name' => $dbName,
			'site_key' => $siteKey,
			'role' => (int) ($row['erp_only_shared'] ?? 0) === 1 ? 'shared_erp' : 'tenant_cp',
			'hostname' => $hostname,
			'registry_admin_email' => $adminEmail,
		),
		$audit
	);

	if ($siteKey === 'asap' && $resetAsap) {
		$report['asap_reset'] = epc_cred_reset_asap_admin($tpdo, $cfg, $apply);
	}
	if ($dbName !== '') {
		$auditedDbKeys[$dbName] = true;
	}
}

// Shared docpart DB (eParts Cart + domain-alias tenants)
$docCreds = epc_portal_resolve_tenant_db_credentials();
if (!empty($docCreds['db']) && empty($auditedDbKeys[$docCreds['db']])) {
	$docPdo = epc_cred_pdo_for_creds($docCreds);
	if ($docPdo instanceof PDO) {
		$settings = array();
		try {
			epc_portal_db_ensure($docPdo);
			$settings = epc_portal_load_site_settings_for_host($docPdo, 'www.epartscart.com');
		} catch (Throwable $e) {
			$settings = array();
		}
		$report['databases'][] = array_merge(
			array(
				'db_name' => $docCreds['db'],
				'site_key' => 'docpart_shared',
				'role' => 'tenant_cp_shared',
				'hostname' => 'www.epartscart.com (+ domain aliases)',
				'note' => 'Shared MySQL for epartscart, taxofinca, electronicae, stylenlook, jewellery when not split',
			),
			epc_cred_audit_db($docPdo, $secret, 'Shared tenant DB ' . $docCreds['db'], $settings)
		);
		$auditedDbKeys[$docCreds['db']] = true;
	}
}

// Discover other MySQL databases with CP users table
$seedCreds = $docCreds;
if ($seedCreds['password'] === '') {
	$seedCreds = array('db' => $platDbName, 'user' => $cfg->user, 'password' => $cfg->password);
}
$seedPdo = epc_cred_pdo_for_creds($seedCreds);
if ($seedPdo instanceof PDO) {
	foreach (epc_cred_discover_databases($seedPdo, $seedCreds) as $dbName => $creds) {
		if (!empty($auditedDbKeys[$dbName])) {
			continue;
		}
		$probe = epc_cred_pdo_for_creds($creds);
		if (!$probe instanceof PDO || !epc_cred_db_has_users($probe)) {
			continue;
		}
		$report['databases'][] = array_merge(
			array('db_name' => $dbName, 'site_key' => $dbName, 'role' => 'discovered', 'hostname' => '(discovered)'),
			epc_cred_audit_db($probe, $secret, 'Discovered DB ' . $dbName, array())
		);
		$auditedDbKeys[$dbName] = true;
	}
}

// Merge known tenant templates not in registry
$registryKeys = array();
foreach ($report['tenants'] as $t) {
	$registryKeys[(string) $t['site_key']] = true;
}
foreach (epc_cred_known_virtual_tenants() as $vt) {
	if (!empty($registryKeys[(string) $vt['site_key']])) {
		continue;
	}
	$host = (string) $vt['hostname'];
	$report['tenants'][] = array(
		'site_key' => (string) $vt['site_key'],
		'hostname' => $host,
		'trade_name' => (string) $vt['trade_name'],
		'db_name' => (string) $vt['db_name'],
		'status' => (string) $vt['status'],
		'erp_only_shared' => (int) ($vt['erp_only_shared'] ?? 0),
		'hosted_on' => 'client',
		'registry_admin_email' => (string) $vt['from_email'],
		'cp_login_url' => ((int) ($vt['erp_only_shared'] ?? 0) === 1)
			? 'https://www.ecomae.com/cp/'
			: 'https://' . $host . '/cp/',
		'erp_shell_url' => ((int) ($vt['erp_only_shared'] ?? 0) === 1)
			? 'https://www.ecomae.com/cp/shop/finance/erp?epc_erp_shell=1'
			: 'https://' . $host . '/cp/shop/finance/erp?epc_erp_shell=1',
		'in_registry' => false,
	);
}

// Password + HTTP tests for documented accounts
$httpCases = array(
	array(
		'label' => 'Super CP operator',
		'url' => 'https://www.ecomae.com/cp/',
		'email' => 'taxofin2025@gmail.com',
		'password' => '12345678',
		'erp_pick' => '',
	),
	array(
		'label' => 'ASAP ERP admin',
		'url' => 'https://www.ecomae.com/cp/',
		'email' => 'asap_admin@asap-ae.com',
		'password' => 'AsapAudit2026!A',
		'erp_pick' => 'asap',
	),
	array(
		'label' => 'eParts Cart tenant CP (registry email)',
		'url' => 'https://www.epartscart.com/cp/',
		'email' => 'partsdoc2025@gmail.com',
		'password' => '12345678',
		'erp_pick' => '',
	),
);

foreach ($httpCases as $case) {
	$entry = array(
		'label' => $case['label'],
		'url' => $case['url'],
		'email' => $case['email'],
	);
	// DB hash test on platform or asap
	$testPdo = $platformPdo;
	if ($case['email'] === 'asap_admin@asap-ae.com') {
		foreach ($tenantRows as $tr) {
			if ((string) $tr['site_key'] === 'asap') {
				$testPdo = epc_cred_tenant_pdo($tr) ?? $platformPdo;
				break;
			}
		}
	} elseif ($case['email'] === 'partsdoc2025@gmail.com') {
		$docPdo = epc_cred_pdo_for_creds(epc_portal_resolve_tenant_db_credentials());
		if ($docPdo instanceof PDO) {
			$testPdo = $docPdo;
		}
	}
	$ust = $testPdo->prepare('SELECT `user_id`, `password`, `unlocked` FROM `users` WHERE `email` = ? LIMIT 1');
	$ust->execute(array($case['email']));
	$urow = $ust->fetch(PDO::FETCH_ASSOC);
	if ($urow) {
		$entry['user_id'] = (int) $urow['user_id'];
		$entry['db_password_matches'] = epc_cred_test_password((string) $urow['password'], $secret, epc_cred_password_candidates());
		if ($case['email'] === 'asap_admin@asap-ae.com' && $apply && $resetAsap) {
			$entry['db_password_matches'][] = array('password' => 'AsapAudit2026!A', 'source' => 'reset_asap apply=1');
		}
		$entry['password_works_in_db'] = count($entry['db_password_matches']) > 0;
	} else {
		$entry['user_id'] = null;
		$entry['password_works_in_db'] = false;
		$entry['db_password_matches'] = array();
	}

	$tryPassword = $case['password'];
	if ($tryPassword === null) {
		if (!empty($entry['db_password_matches'][0]['password'])) {
			$tryPassword = (string) $entry['db_password_matches'][0]['password'];
		} else {
			$tryPassword = '12345678';
		}
	}
	$entry['test_password_used'] = $tryPassword;
	if ($testHttp) {
		$entry['http'] = epc_cred_http_login($case['url'], $case['email'], $tryPassword, (string) ($case['erp_pick'] ?? ''));
	}
	$report['http_tests'][] = $entry;
}

// Registry admin emails — match against shared DB or site-specific DB
foreach ($report['tenants'] as &$tm) {
	$found = false;
	$knownPw = false;
	$targetDb = (string) ($tm['db_name'] ?? '');
	foreach ($report['databases'] as $db) {
		if ($targetDb !== '' && ($db['db_name'] ?? '') !== $targetDb) {
			if (($db['site_key'] ?? '') !== ($tm['site_key'] ?? '')) {
				continue;
			}
		} elseif (($db['site_key'] ?? '') !== ($tm['site_key'] ?? '')) {
			continue;
		}
		foreach ($db['users'] ?? array() as $u) {
			if (strcasecmp((string) $u['email'], (string) $tm['registry_admin_email']) === 0) {
				$found = true;
				$knownPw = !empty($u['password_known']);
				$tm['registry_admin_user_id'] = $u['user_id'];
				break 2;
			}
		}
	}
	$tm['registry_admin_in_db'] = $found;
	$tm['registry_admin_password_known'] = $knownPw;
}
unset($tm);

if ($format === 'json') {
	echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

echo "=== ECOM AE CP Credentials Audit ===\n";
echo 'Time: ' . $report['audited_at'] . "\n";
echo 'Super CP: ' . $report['super_cp_url'] . "\n\n";

echo "--- Tenant registry ---\n";
foreach ($report['tenants'] as $t) {
	echo sprintf(
		"%s | %s | db=%s | %s | admin=%s | CP=%s\n",
		$t['site_key'],
		$t['trade_name'],
		$t['db_name'],
		$t['status'],
		$t['registry_admin_email'],
		$t['cp_login_url']
	);
}

echo "\n--- Backend CP users per database ---\n";
foreach ($report['databases'] as $db) {
	echo "\n[" . ($db['context'] ?? ($db['db_name'] ?? '?')) . "]\n";
	if (!empty($db['error'])) {
		echo "  ERROR: {$db['error']}\n";
		continue;
	}
	echo '  access_mode=' . ($db['access_mode'] ?? '') . ' industry=' . ($db['industry_code'] ?? '') . "\n";
	if (!empty($db['erp_modules'])) {
		echo '  erp_modules=' . implode(', ', $db['erp_modules']) . "\n";
	}
	foreach ($db['users'] ?? array() as $u) {
		$pw = !empty($u['password_known']) ? 'KNOWN' : 'unknown';
		if (!empty($u['password_matches'])) {
			$pw = 'KNOWN (' . $u['password_matches'][0]['source'] . ')';
		}
		echo sprintf(
			"  #%d %s | %s | groups: %s | password: %s\n",
			$u['user_id'],
			$u['email'] !== '' ? $u['email'] : $u['phone'],
			$u['name'] !== '' ? $u['name'] : '-',
			implode(', ', $u['groups']),
			$pw
		);
	}
}

echo "\n--- Login verification ---\n";
foreach ($report['http_tests'] as $ht) {
	echo $ht['label'] . ' (' . $ht['email'] . ")\n";
	echo '  DB password works: ' . (!empty($ht['password_works_in_db']) ? 'YES' : 'NO') . "\n";
	if (!empty($ht['http'])) {
		echo '  HTTP login: ' . ($ht['http']['login_likely_ok'] ? 'likely OK' : 'FAILED')
			. ' (code ' . $ht['http']['login_http_code'] . ")\n";
		echo '  HTTP dashboard: ' . ($ht['http']['dashboard_likely_ok'] ? '200 OK' : 'not confirmed')
			. ' (code ' . $ht['http']['dashboard_http_code'] . ")\n";
	}
}

if ($report['asap_reset'] !== null) {
	echo "\n--- ASAP password reset ---\n";
	echo json_encode($report['asap_reset'], JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE) . "\n";
}

echo "\nDone. Use format=json for machine-readable output.\n";
