<?php
/**
 * Super CP tenant control — registry is_active toggle, operator credentials, unified tenant list.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_portal.php';
require_once __DIR__ . '/epc_portal_db.php';
require_once __DIR__ . '/epc_portal_tenant.php';

function epc_portal_tenant_control_ensure_schema(PDO $pdo): void
{
	epc_portal_db_ensure($pdo);
	foreach (array(
		'is_active' => 'TINYINT(1) NOT NULL DEFAULT 1 AFTER `demo_extended_count`',
		'operator_temp_password' => "VARCHAR(120) NOT NULL DEFAULT '' AFTER `is_active`",
	) as $col => $def) {
		$chk = $pdo->query("SHOW COLUMNS FROM `epc_portal_tenants` LIKE " . $pdo->quote($col))->fetch(PDO::FETCH_ASSOC);
		if (!$chk) {
			$pdo->exec('ALTER TABLE `epc_portal_tenants` ADD COLUMN `' . $col . '` ' . $def);
		}
	}
	epc_portal_tenant_control_audit_ensure_schema($pdo);
}

function epc_portal_tenant_control_audit_ensure_schema(PDO $pdo): void
{
	$pdo->exec("CREATE TABLE IF NOT EXISTS `epc_portal_tenant_credential_audit` (
		`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`time` INT NOT NULL DEFAULT 0,
		`operator_id` INT NOT NULL DEFAULT 0,
		`site_key` VARCHAR(64) NOT NULL DEFAULT '',
		`action` VARCHAR(64) NOT NULL DEFAULT '',
		`detail_json` TEXT NULL,
		KEY `idx_site_time` (`site_key`, `time`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Super CP tenant credential changes'");
}

function epc_portal_tenant_control_audit_log(PDO $pdo, string $siteKey, string $action, array $detail = array(), int $operatorId = 0): void
{
	epc_portal_tenant_control_audit_ensure_schema($pdo);
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return;
	}
	if ($operatorId <= 0 && class_exists('DP_User')) {
		$operatorId = (int) DP_User::getAdminId();
	}
	$pdo->prepare(
		'INSERT INTO `epc_portal_tenant_credential_audit` (`time`, `operator_id`, `site_key`, `action`, `detail_json`)
		 VALUES (?, ?, ?, ?, ?)'
	)->execute(array(time(), $operatorId, $key, $action, json_encode($detail, JSON_UNESCAPED_UNICODE)));
}

function epc_portal_tenant_control_set_registry_login_email(PDO $platformPdo, array $row, string $email): void
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
	$email = strtolower(trim($email));
	if ($key === '' || $email === '') {
		return;
	}
	if (!empty($row['is_demo'])) {
		$platformPdo->prepare(
			'UPDATE `epc_portal_tenants` SET `demo_contact_email` = ?, `from_email` = ?, `updated_at` = ? WHERE `site_key` = ?'
		)->execute(array($email, $email, time(), $key));
		return;
	}
	$intro = array();
	if (!empty($row['intro_json'])) {
		$decoded = json_decode((string) $row['intro_json'], true);
		if (is_array($decoded)) {
			$intro = $decoded;
		}
	}
	$intro['admin_email'] = $email;
	$intro['admin_cp_email'] = $email;
	$platformPdo->prepare(
		'UPDATE `epc_portal_tenants` SET `intro_json` = ?, `from_email` = ?, `updated_at` = ? WHERE `site_key` = ?'
	)->execute(array(json_encode($intro, JSON_UNESCAPED_UNICODE), $email, time(), $key));
}

function epc_portal_tenant_control_list_cp_backend_users(PDO $tenantPdo): array
{
	try {
		$st = $tenantPdo->query(
			'SELECT u.`user_id`, u.`email`, u.`email_confirmed`, u.`unlocked`, u.`time_registered`,
			 GROUP_CONCAT(DISTINCT g.`value` ORDER BY g.`value` SEPARATOR \', \') AS `groups`
			 FROM `users` u
			 INNER JOIN `users_groups_bind` b ON b.`user_id` = u.`user_id`
			 INNER JOIN `groups` g ON g.`id` = b.`group_id` AND g.`for_backend` = 1
			 GROUP BY u.`user_id`, u.`email`, u.`email_confirmed`, u.`unlocked`, u.`time_registered`
			 ORDER BY u.`user_id` ASC'
		);
		return $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
	} catch (Exception $e) {
		return array();
	}
}

function epc_portal_tenant_control_migrate_cp_email(PDO $tenantPdo, string $oldEmail, string $newEmail): array
{
	$oldEmail = strtolower(trim($oldEmail));
	$newEmail = strtolower(trim($newEmail));
	if ($oldEmail === '' || $newEmail === '' || $oldEmail === $newEmail) {
		return array('migrated' => false);
	}
	$st = $tenantPdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($oldEmail));
	$uid = (int) $st->fetchColumn();
	if ($uid <= 0) {
		return array('migrated' => false, 'reason' => 'no_user_for_old_email');
	}
	$dup = $tenantPdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? AND `user_id` != ? LIMIT 1');
	$dup->execute(array($newEmail, $uid));
	if ((int) $dup->fetchColumn() > 0) {
		return array('migrated' => false, 'reason' => 'email_taken');
	}
	$tenantPdo->prepare('UPDATE `users` SET `email` = ?, `email_confirmed` = 1 WHERE `user_id` = ?')
		->execute(array($newEmail, $uid));
	return array('migrated' => true, 'user_id' => $uid);
}

/**
 * Load tenant + CP backend users for Demo access control panel.
 */
function epc_portal_tenant_control_demo_access_load(PDO $platformPdo, string $siteKey): array
{
	$row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
	if ($row === null) {
		return array('ok' => false, 'message' => 'Tenant not in registry — onboard via Tenant Hub first');
	}
	$tenantConnect = epc_portal_tenant_control_tenant_pdo_connect($row);
	$tenantPdo = $tenantConnect['pdo'];
	$users = $tenantPdo instanceof PDO ? epc_portal_tenant_control_list_cp_backend_users($tenantPdo) : array();
	return array(
		'ok' => true,
		'site_key' => (string) ($row['site_key'] ?? ''),
		'trade_name' => (string) ($row['trade_name'] ?? ''),
		'hostname' => (string) ($row['hostname'] ?? ''),
		'is_demo' => !empty($row['is_demo']) ? 1 : 0,
		'is_active' => epc_portal_tenant_control_row_is_active($row) ? 1 : 0,
		'tenant_type' => epc_portal_tenant_control_tenant_type($row),
		'login_email' => epc_portal_tenant_control_resolve_admin_email($row),
		'has_stored_password' => trim((string) ($row['operator_temp_password'] ?? '')) !== '',
		'urls' => epc_portal_tenant_control_urls($row),
		'cp_users' => $users,
		'tenant_db_ok' => $tenantPdo instanceof PDO,
		'tenant_db_error' => $tenantPdo instanceof PDO ? '' : (string) ($tenantConnect['error'] ?? ''),
	);
}

/**
 * Update CP login email and/or password; optional is_demo flag (0 = production registry).
 */
function epc_portal_tenant_control_demo_access_save(
	PDO $platformPdo,
	string $siteKey,
	string $email,
	?string $password,
	?int $isDemoFlag,
	int $operatorId = 0
): array {
	$row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
	if ($row === null) {
		return array('ok' => false, 'message' => 'Tenant not in registry');
	}
	$email = strtolower(trim($email));
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'Valid login email required');
	}
	$tenantConnect = epc_portal_tenant_control_tenant_pdo_connect($row);
	$tenantPdo = $tenantConnect['pdo'];
	if (!$tenantPdo instanceof PDO) {
		return array('ok' => false, 'message' => (string) ($tenantConnect['error'] ?? 'Cannot connect to tenant database'));
	}
	require_once __DIR__ . '/epc_portal_demo.php';

	$oldEmail = epc_portal_tenant_control_resolve_admin_email($row);
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$detail = array('old_email' => $oldEmail, 'new_email' => $email);

	if ($oldEmail !== '' && strcasecmp($oldEmail, $email) !== 0) {
		$mig = epc_portal_tenant_control_migrate_cp_email($tenantPdo, $oldEmail, $email);
		$detail['email_migrate'] = $mig;
		if (!empty($mig['reason']) && $mig['reason'] === 'email_taken') {
			return array('ok' => false, 'message' => 'That email is already used by another CP user in this tenant DB');
		}
	}
	epc_portal_tenant_control_set_registry_login_email($platformPdo, $row, $email);
	$row = epc_portal_tenant_control_get_row($platformPdo, $key) ?: $row;

	$passOut = '';
	$userId = 0;
	if ($password !== null && $password !== '') {
		$passOut = $password;
		$name = trim((string) ($row['trade_name'] ?? 'Admin'));
		$userResult = epc_portal_demo_create_cp_user($tenantPdo, $email, $passOut, $name);
		$userId = (int) ($userResult['user_id'] ?? 0);
		$platformPdo->prepare(
			'UPDATE `epc_portal_tenants` SET `operator_temp_password` = ?, `updated_at` = ? WHERE `site_key` = ?'
		)->execute(array($passOut, time(), $key));
		$detail['password_set'] = true;
	} elseif (strcasecmp($oldEmail, $email) !== 0) {
		$chk = $tenantPdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
		$chk->execute(array($email));
		if ((int) $chk->fetchColumn() <= 0) {
			$stored = trim((string) ($row['operator_temp_password'] ?? ''));
			if ($stored !== '') {
				$name = trim((string) ($row['trade_name'] ?? 'Admin'));
				$userResult = epc_portal_demo_create_cp_user($tenantPdo, $email, $stored, $name);
				$userId = (int) ($userResult['user_id'] ?? 0);
			}
		}
	}

	if ($isDemoFlag !== null) {
		$demoVal = $isDemoFlag ? 1 : 0;
		$platformPdo->prepare(
			'UPDATE `epc_portal_tenants` SET `is_demo` = ?, `updated_at` = ? WHERE `site_key` = ?'
		)->execute(array($demoVal, time(), $key));
		$detail['is_demo'] = $demoVal;
	}

	epc_portal_tenant_control_audit_log($platformPdo, $key, 'demo_access_save', $detail, $operatorId);

	$msg = $passOut !== '' ? 'CP login and password updated for ' . $email : 'CP login email updated for ' . $email;
	return array(
		'ok' => true,
		'message' => $msg,
		'email' => $email,
		'password' => $passOut,
		'user_id' => $userId,
		'is_demo' => $isDemoFlag !== null ? (int) $isDemoFlag : (int) (!empty($row['is_demo'])),
	);
}

function epc_portal_tenant_control_row_is_active(?array $row): bool
{
	if (!is_array($row)) {
		return true;
	}
	if (array_key_exists('is_active', $row) && (int) $row['is_active'] === 0) {
		return false;
	}
	return true;
}

function epc_portal_tenant_control_get_row(PDO $pdo, string $siteKey): ?array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return null;
	}
	epc_portal_tenant_control_ensure_schema($pdo);
	$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_portal_tenant_control_get_row_by_hostname(PDO $pdo, string $host): ?array
{
	$host = strtolower(trim($host));
	if ($host === '') {
		return null;
	}
	epc_portal_tenant_control_ensure_schema($pdo);
	$bare = preg_replace('/^www\./', '', $host);
	$st = $pdo->prepare(
		'SELECT * FROM `epc_portal_tenants` WHERE `hostname` = ? OR `hostname` = ? LIMIT 1'
	);
	$st->execute(array($host, $bare));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_portal_tenant_control_tenant_type(array $row): string
{
	if (!empty($row['is_demo'])) {
		require_once __DIR__ . '/epc_portal_demo.php';
		if (epc_portal_demo_row_is_erp_only($row)) {
			return 'demo_erp_only';
		}
		return 'demo';
	}
	if (!empty($row['erp_only_shared'])) {
		return 'erp_only';
	}
	if ((string) ($row['industry_code'] ?? '') === 'erp_standalone'
		&& (string) ($row['hosted_on'] ?? '') === 'platform') {
		return 'erp_only';
	}
	return 'commerce';
}

function epc_portal_tenant_control_type_label(string $type): string
{
	$labels = array(
		'commerce' => 'Commerce',
		'demo' => 'Demo',
		'demo_erp_only' => 'ERP-only demo',
		'erp_only' => 'ERP-only',
	);
	return isset($labels[$type]) ? $labels[$type] : 'Commerce';
}

function epc_portal_tenant_control_type_badge_class(string $type): string
{
	$map = array(
		'commerce' => 'label-primary',
		'demo' => 'label-info',
		'demo_erp_only' => 'label-warning',
		'erp_only' => 'label-warning',
	);
	return isset($map[$type]) ? $map[$type] : 'label-default';
}

function epc_portal_tenant_control_admin_email(array $row): string
{
	if (!empty($row['is_demo']) && !empty($row['demo_contact_email'])) {
		return strtolower(trim((string) $row['demo_contact_email']));
	}
	if (!empty($row['intro_json'])) {
		$intro = json_decode((string) $row['intro_json'], true);
		if (is_array($intro)) {
			// CP operator fields first — intro admin_email is often a business contact, not CP login.
			foreach (array('admin_cp_email', 'operator_login_email') as $key) {
				$em = trim((string) ($intro[$key] ?? ''));
				if ($em !== '' && filter_var($em, FILTER_VALIDATE_EMAIL)) {
					return strtolower($em);
				}
			}
		}
	}
	$from = trim((string) ($row['from_email'] ?? ''));
	if ($from !== '' && filter_var($from, FILTER_VALIDATE_EMAIL)) {
		return strtolower($from);
	}
	return '';
}

/** First CP backend user email in tenant DB (registry rows only). */
function epc_portal_tenant_control_admin_email_from_tenant_db(array $row): string
{
	if (!empty($row['virtual'])) {
		return '';
	}
	$connect = epc_portal_tenant_control_tenant_pdo_connect($row);
	$pdo = $connect['pdo'];
	if (!$pdo instanceof PDO) {
		return '';
	}
	$users = epc_portal_tenant_control_list_cp_backend_users($pdo);
	if (empty($users)) {
		return '';
	}
	foreach ($users as $u) {
		$em = strtolower(trim((string) ($u['email'] ?? '')));
		if ($em !== '' && filter_var($em, FILTER_VALIDATE_EMAIL)) {
			return $em;
		}
	}
	return '';
}

/**
 * Registry email with tenant DB + conventional {site_key}_admin@ecomae.com fallbacks.
 */
function epc_portal_tenant_control_resolve_admin_email(array $row, bool $allowTenantDb = true): string
{
	$email = epc_portal_tenant_control_admin_email($row);
	if ($email !== '') {
		return $email;
	}
	if ($allowTenantDb) {
		// Tenant DB CP user is authoritative when registry has no explicit CP login email.
		$email = epc_portal_tenant_control_admin_email_from_tenant_db($row);
		if ($email !== '') {
			return $email;
		}
		$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
		if ($key !== '') {
			$guess = $key . '_admin@ecomae.com';
			$connect = epc_portal_tenant_control_tenant_pdo_connect($row);
			$pdo = $connect['pdo'];
			if ($pdo instanceof PDO) {
				$st = $pdo->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
				$st->execute(array($guess));
				if ((int) $st->fetchColumn() > 0) {
					return $guess;
				}
			}
		}
	}
	if (!empty($row['intro_json'])) {
		$intro = json_decode((string) $row['intro_json'], true);
		if (is_array($intro)) {
			$em = trim((string) ($intro['admin_email'] ?? ''));
			if ($em !== '' && filter_var($em, FILTER_VALIDATE_EMAIL)) {
				return strtolower($em);
			}
		}
	}
	return '';
}

/** Persist resolved login email into registry intro_json / from_email when missing. */
function epc_portal_tenant_control_backfill_registry_login_email(PDO $platformPdo, array $row, string $email): bool
{
	$email = strtolower(trim($email));
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
	if ($key === '' || $email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return false;
	}
	if (epc_portal_tenant_control_admin_email($row) !== '') {
		return false;
	}
	epc_portal_tenant_control_set_registry_login_email($platformPdo, $row, $email);
	return true;
}

/** Normalize registry/template hostname to www.{domain} for commerce links. */
function epc_portal_tenant_control_commerce_host(string $hostname): string
{
	$host = strtolower(trim($hostname));
	if ($host === '') {
		return '';
	}
	$host = preg_replace('#^https?://#', '', $host);
	$host = preg_replace('#/.*$#', '', $host);
	$host = preg_replace('/^www\./', '', $host);
	if ($host === '' || strpos($host, '.') === false) {
		return '';
	}
	return 'www.' . $host;
}

function epc_portal_tenant_control_urls(array $row): array
{
	$type = epc_portal_tenant_control_tenant_type($row);
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
	if (($type === 'demo' || $type === 'demo_erp_only') && $key !== '') {
		require_once __DIR__ . '/epc_portal_demo.php';
		$urls = epc_portal_demo_urls($key, $row);
		if (function_exists('epc_portal_demo_cp_autologin_url')) {
			$urls['cp_autologin'] = epc_portal_demo_cp_autologin_url($key);
		}
		return $urls;
	}
	if ($type === 'erp_only' && $key !== '') {
		require_once __DIR__ . '/epc_client_erp_router.php';
		$erpLogin = 'https://www.ecomae.com' . epc_client_erp_login_url($key);
		return array(
			'storefront' => '',
			'cp' => '',
			'client_erp' => $erpLogin,
			'erp_login' => $erpLogin,
			'erp_shell' => 'https://www.ecomae.com' . epc_client_erp_shell_url($key),
		);
	}
	$host = epc_portal_tenant_control_commerce_host((string) ($row['hostname'] ?? ''));
	if ($host !== '') {
		return array(
			'storefront' => 'https://' . $host . '/en/',
			'cp' => 'https://' . $host . '/cp/',
		);
	}
	return array('storefront' => '', 'cp' => '');
}

function epc_portal_tenant_control_tenant_pdo(array $row): ?PDO
{
	$result = epc_portal_tenant_control_tenant_pdo_connect($row);
	return $result['pdo'];
}

/** @return array{pdo:?PDO,error:string} */
function epc_portal_tenant_control_tenant_pdo_connect(array $row): array
{
	require_once __DIR__ . '/epc_tenant_pdo.php';
	$db = trim((string) ($row['db_name'] ?? ''));
	$user = trim((string) ($row['db_user'] ?? ''));
	if ($user === '' && $db !== '') {
		$user = $db;
		$row['db_user'] = $user;
	}
	[$pdo, $err] = epc_tenant_pdo_from_row($row, array('timeout' => 5));
	if ($pdo instanceof PDO) {
		return array('pdo' => $pdo, 'error' => '');
	}
	if ($db === '' || $user === '' || trim((string) ($row['db_password'] ?? '')) === '') {
		$pass = (string) ($row['db_password'] ?? '');
		$detail = $db === '' ? 'missing db_name in registry' : ($pass === '' ? 'missing db_password in registry' : 'missing db_user in registry');
		return array('pdo' => null, 'error' => 'Cannot connect to tenant database (' . $detail . ')');
	}
	return array(
		'pdo' => null,
		'error' => 'Cannot connect to tenant database `' . $db . '` as `' . $user . '`: ' . ($err !== '' ? $err : 'unknown error'),
	);
}

function epc_portal_tenant_control_generate_password(): string
{
	if (function_exists('epc_portal_demo_temp_password')) {
		require_once __DIR__ . '/epc_portal_demo.php';
		return epc_portal_demo_temp_password();
	}
	return substr(bin2hex(random_bytes(4)), 0, 4) . 'Op' . random_int(10, 99) . '!';
}

function epc_portal_tenant_control_reset_cp_password(PDO $platformPdo, string $siteKey): array
{
	$row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
	if ($row === null) {
		return array('ok' => false, 'message' => 'Tenant not in registry — onboard via Tenant Hub first');
	}
	$email = epc_portal_tenant_control_resolve_admin_email($row);
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'No admin email on registry row — set login email in Demo access control or run tenant provision');
	}
	if (epc_portal_tenant_control_admin_email($row) === '') {
		epc_portal_tenant_control_backfill_registry_login_email($platformPdo, $row, $email);
	}
	$tenantConnect = epc_portal_tenant_control_tenant_pdo_connect($row);
	$tenantPdo = $tenantConnect['pdo'];
	if (!$tenantPdo instanceof PDO) {
		return array('ok' => false, 'message' => (string) ($tenantConnect['error'] ?? 'Cannot connect to tenant database'));
	}
	require_once __DIR__ . '/epc_portal_demo.php';
	$pass = epc_portal_tenant_control_generate_password();
	$name = (string) ($row['trade_name'] ?? 'Admin');
	$result = epc_portal_demo_create_cp_user($tenantPdo, $email, $pass, $name);
	$platformPdo->prepare(
		'UPDATE `epc_portal_tenants` SET `operator_temp_password` = ?, `updated_at` = ? WHERE `site_key` = ?'
	)->execute(array($pass, time(), preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey))));
	return array(
		'ok' => true,
		'message' => 'Password reset for ' . $email,
		'email' => $email,
		'password' => $pass,
		'user_id' => (int) ($result['user_id'] ?? 0),
	);
}

function epc_portal_tenant_control_set_active(PDO $platformPdo, string $siteKey, bool $active): array
{
	$row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
	if ($row === null) {
		return array('ok' => false, 'message' => 'Tenant not in registry');
	}
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	$val = $active ? 1 : 0;
	$platformPdo->prepare(
		'UPDATE `epc_portal_tenants` SET `is_active` = ?, `updated_at` = ? WHERE `site_key` = ?'
	)->execute(array($val, time(), $key));
	return array(
		'ok' => true,
		'message' => $active ? 'Tenant enabled' : 'Tenant disabled — storefront and CP blocked',
		'is_active' => $val,
	);
}

function epc_portal_tenant_control_virtual_tenants(): array
{
	$out = array();
	foreach (epc_portal_tenant_templates() as $siteKey => $tpl) {
		if ($siteKey === 'erp_only_demo') {
			continue;
		}
		$out[] = array(
			'site_key' => $siteKey,
			'hostname' => (string) ($tpl['hostname'] ?? ''),
			'trade_name' => (string) ($tpl['trade_name'] ?? $siteKey),
			'from_email' => (string) ($tpl['from_email'] ?? ''),
			'erp_only_shared' => !empty($tpl['erp_only_shared']) ? 1 : 0,
			'is_demo' => 0,
			'industry_code' => (string) ($tpl['industry'] ?? ''),
			'status' => 'template',
			'db_name' => !empty($tpl['erp_only_shared']) ? $siteKey : 'docpart',
			'db_user' => !empty($tpl['erp_only_shared']) ? $siteKey : 'docpart',
			'db_password' => '',
			'is_active' => 1,
			'operator_temp_password' => '',
			'virtual' => true,
		);
	}
	return $out;
}

/** ERP module pack label for Super CP tenant list (registry ERP-only rows). */
function epc_portal_tenant_control_erp_pack_summary(array $row): array
{
	$out = array(
		'preset_id' => '',
		'preset_label' => '',
		'modules_count' => 0,
		'modules' => array(),
	);
	if (epc_portal_tenant_control_tenant_type($row) !== 'erp_only') {
		return $out;
	}
	require_once __DIR__ . '/epc_portal_erp_modules.php';
	$intro = array();
	if (!empty($row['intro_json'])) {
		$decoded = json_decode((string) $row['intro_json'], true);
		if (is_array($decoded)) {
			$intro = $decoded;
		}
	}
	$expectedPreset = '';
	if (!empty($intro['erp_modules_preset'])) {
		$expectedPreset = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $intro['erp_modules_preset']));
	}
	if ($expectedPreset === '') {
		$expectedPreset = epc_portal_industry_erp_modules_preset((string) ($row['industry_code'] ?? ''));
	}
	$settings = array();
	$tenantPdo = epc_portal_tenant_control_tenant_pdo($row);
	if ($tenantPdo instanceof PDO) {
		$settings = epc_portal_load_site_settings($tenantPdo);
	}
	$mods = epc_portal_erp_modules_enabled($settings);
	$presetId = epc_portal_erp_modules_detect_preset($mods);
	if ($presetId === '') {
		$presetId = $expectedPreset;
	}
	$presets = epc_portal_erp_modules_presets();
	$out['preset_id'] = $presetId;
	$out['preset_label'] = ($presetId !== '' && isset($presets[$presetId]['label']))
		? (string) $presets[$presetId]['label']
		: (count($mods) > 0 ? count($mods) . ' modules' : '');
	$out['modules_count'] = count($mods);
	$out['modules'] = $mods;
	return $out;
}

function epc_portal_tenant_control_list_all(PDO $pdo): array
{
	epc_portal_tenant_control_ensure_schema($pdo);
	require_once __DIR__ . '/epc_portal_demo.php';
	epc_portal_demo_ensure_schema($pdo);
	$registry = $pdo->query('SELECT * FROM `epc_portal_tenants` ORDER BY `is_demo` DESC, `erp_only_shared` DESC, `site_key` ASC')->fetchAll(PDO::FETCH_ASSOC);
	$byKey = array();
	foreach ($registry as $row) {
		$byKey[(string) $row['site_key']] = $row;
	}
	foreach (epc_portal_tenant_control_virtual_tenants() as $virt) {
		$k = (string) $virt['site_key'];
		if (!isset($byKey[$k])) {
			$byKey[$k] = $virt;
		}
	}
	$rows = array_values($byKey);
	usort($rows, function ($a, $b) {
		$order = array('demo' => 0, 'demo_erp_only' => 0, 'erp_only' => 1, 'commerce' => 2);
		$ta = epc_portal_tenant_control_tenant_type($a);
		$tb = epc_portal_tenant_control_tenant_type($b);
		$oa = isset($order[$ta]) ? $order[$ta] : 9;
		$ob = isset($order[$tb]) ? $order[$tb] : 9;
		if ($oa !== $ob) {
			return $oa - $ob;
		}
		return strcmp((string) ($a['site_key'] ?? ''), (string) ($b['site_key'] ?? ''));
	});
	$now = time();
	foreach ($rows as &$r) {
		$r['tenant_type'] = epc_portal_tenant_control_tenant_type($r);
		$r['type_label'] = epc_portal_tenant_control_type_label($r['tenant_type']);
		$r['in_registry'] = empty($r['virtual']);
		$r['admin_email'] = epc_portal_tenant_control_resolve_admin_email($r);
		if ($r['admin_email'] !== '' && !empty($r['in_registry']) && epc_portal_tenant_control_admin_email($r) === '') {
			epc_portal_tenant_control_backfill_registry_login_email($pdo, $r, (string) $r['admin_email']);
			$refreshed = epc_portal_tenant_control_get_row($pdo, (string) ($r['site_key'] ?? ''));
			if (is_array($refreshed)) {
				$r = array_merge($r, $refreshed);
				$r['in_registry'] = empty($r['virtual']);
				$r['admin_email'] = epc_portal_tenant_control_resolve_admin_email($r);
			}
		}
		$r['stored_password'] = trim((string) ($r['operator_temp_password'] ?? ''));
		$r['is_active_flag'] = epc_portal_tenant_control_row_is_active($r) ? 1 : 0;
		$r['urls'] = epc_portal_tenant_control_urls($r);
		if ($r['tenant_type'] === 'erp_only') {
			$pack = epc_portal_tenant_control_erp_pack_summary($r);
			$r['erp_pack_id'] = (string) ($pack['preset_id'] ?? '');
			$r['erp_pack_label'] = (string) ($pack['preset_label'] ?? '');
			$r['erp_modules_count'] = (int) ($pack['modules_count'] ?? 0);
			$r['erp_login_url'] = (string) ($r['urls']['erp_login'] ?? $r['urls']['client_erp'] ?? '');
		} else {
			$r['erp_pack_id'] = '';
			$r['erp_pack_label'] = '';
			$r['erp_modules_count'] = 0;
			$r['erp_login_url'] = '';
		}
		$exp = (int) ($r['demo_expires_at'] ?? 0);
		$r['demo_expired'] = !empty($r['is_demo']) && $exp > 0 && $exp < $now;
		$r['access_blocked'] = !$r['is_active_flag'] || $r['demo_expired'];
		$r['status_display'] = (string) ($r['status'] ?? '');
		if (!$r['is_active_flag']) {
			$r['status_display'] = 'disabled';
		} elseif ($r['demo_expired']) {
			$r['status_display'] = 'expired';
		}
	}
	unset($r);
	return $rows;
}

function epc_portal_tenant_control_render_blocked(string $title, string $message, int $httpCode = 503): void
{
	http_response_code($httpCode);
	header('Content-Type: text/html; charset=utf-8');
	echo '<!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">';
	echo '<title>' . htmlspecialchars($title, ENT_QUOTES, 'UTF-8') . '</title>';
	echo '<style>body{font-family:Inter,Segoe UI,sans-serif;background:#0f172a;color:#e2e8f0;margin:0;padding:32px 20px}';
	echo '.card{max-width:520px;margin:0 auto;background:#1e293b;border:1px solid #334155;border-radius:12px;padding:24px}';
	echo 'h1{font-size:1.35rem;margin:0 0 8px;color:#fff}p{line-height:1.55;color:#94a3b8;margin:0 0 16px}a{color:#38bdf8}</style></head><body><div class="card">';
	echo '<h1>' . htmlspecialchars($title, ENT_QUOTES, 'UTF-8') . '</h1>';
	echo '<p>' . htmlspecialchars($message, ENT_QUOTES, 'UTF-8') . '</p>';
	echo '<p><a href="https://www.ecomae.com/">ecomae.com</a> · <a href="mailto:hello@ecomae.com">Contact support</a></p>';
	echo '</div></body></html>';
	exit;
}

/**
 * Block storefront + tenant CP when registry marks tenant inactive (client hostnames only).
 */
function epc_portal_tenant_control_maybe_block(): void
{
	if (!empty($GLOBALS['epc_demo_storefront_context']) || !empty($GLOBALS['epc_demo_cp_context'])
		|| !empty($GLOBALS['epc_client_erp_context'])) {
		return;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return;
	}
	if (!function_exists('epc_portal_is_client_hostname') || !epc_portal_is_client_hostname()) {
		return;
	}
	// eParts Cart uses shared docpart DB — skip ecomae registry PDO on every storefront request.
	if (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname()) {
		return;
	}
	$pdo = epc_portal_platform_pdo();
	if (!$pdo instanceof PDO) {
		return;
	}
	$row = epc_portal_tenant_control_get_row_by_hostname($pdo, epc_portal_host());
	if ($row === null) {
		return;
	}
	if (epc_portal_tenant_control_row_is_active($row)) {
		return;
	}
	$trade = (string) ($row['trade_name'] ?? 'This site');
	epc_portal_tenant_control_render_blocked(
		$trade . ' — temporarily unavailable',
		'This tenant has been disabled by the platform operator. Please try again later or contact support.',
		503
	);
}
