<?php
/**
 * Multi-company ERP-only tenants on www.ecomae.com — session/user tenant context (no per-client domain).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_portal.php';
require_once __DIR__ . '/epc_portal_tenant.php';

function epc_portal_shared_erp_cookie_name(): string
{
	return 'epc_erp_tenant';
}

function epc_portal_shared_erp_platform_hostname(): string
{
	return 'www.ecomae.com';
}

function epc_portal_shared_erp_list_tenants(?PDO $platformPdo = null): array
{
	if ($platformPdo === null) {
		$platformPdo = epc_portal_platform_pdo();
	}
	if (!$platformPdo instanceof PDO) {
		return array();
	}
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($platformPdo);
	$st = $platformPdo->query(
		'SELECT * FROM `epc_portal_tenants`
		 WHERE `erp_only_shared` = 1 AND (`is_demo` = 0 OR `is_demo` IS NULL)
		   AND `status` IN (\'dns_pending\', \'live\')
		 ORDER BY `trade_name` ASC, `site_key` ASC'
	);
	$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
	$out = array();
	foreach ($rows as $row) {
		if (!function_exists('epc_portal_tenant_is_shared_erp_row') || !epc_portal_tenant_is_shared_erp_row($row)) {
			continue;
		}
		$out[] = $row;
	}
	return $out;
}

function epc_portal_shared_erp_load_by_site_key(string $siteKey, ?PDO $platformPdo = null): ?array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return null;
	}
	if ($platformPdo === null) {
		$platformPdo = epc_portal_platform_pdo();
	}
	if (!$platformPdo instanceof PDO) {
		return null;
	}
	require_once __DIR__ . '/epc_portal_db.php';
	epc_portal_db_ensure($platformPdo);
	$st = $platformPdo->prepare(
		'SELECT * FROM `epc_portal_tenants`
		 WHERE `site_key` = ? AND `erp_only_shared` = 1
		   AND (`is_demo` = 0 OR `is_demo` IS NULL)
		   AND `status` IN (\'dns_pending\', \'live\')
		 LIMIT 1'
	);
	$st->execute(array($key));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	require_once __DIR__ . '/epc_portal_tenant_control.php';
	if (!epc_portal_tenant_control_row_is_active($row)) {
		return null;
	}
	return $row;
}

function epc_portal_shared_erp_tenant_pdo(array $tenantRow): ?PDO
{
	require_once __DIR__ . '/epc_tenant_pdo.php';
	[$pdo] = epc_tenant_pdo_from_row($tenantRow, array('timeout' => 3));
	return $pdo instanceof PDO ? $pdo : null;
}

function epc_portal_shared_erp_cookie_site_key(): string
{
	$cookieName = epc_portal_shared_erp_cookie_name();
	$fromCookie = '';
	if (!empty($_COOKIE[$cookieName])) {
		$fromCookie = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_COOKIE[$cookieName]));
	}
	$fromSession = '';
	if (session_status() === PHP_SESSION_NONE) {
		@session_start();
	}
	if (session_status() === PHP_SESSION_ACTIVE) {
		$fromSession = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_SESSION['epc_erp_tenant_bound'] ?? '')));
	}
	// Session binding wins; reject cookie spoofing that disagrees with the bound tenant.
	if ($fromSession !== '') {
		if ($fromCookie !== '' && $fromCookie !== $fromSession) {
			return '';
		}
		return $fromSession;
	}
	return $fromCookie;
}

function epc_portal_shared_erp_set_tenant_cookie(string $siteKey): void
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return;
	}
	$secure = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
		|| (!empty($_SERVER['HTTP_X_FORWARDED_PROTO']) && $_SERVER['HTTP_X_FORWARDED_PROTO'] === 'https');
	// HttpOnly cookie; Secure when HTTPS. Avoid array options for older PHP.
	setcookie(epc_portal_shared_erp_cookie_name(), $key, 0, '/; samesite=Lax', '', $secure, true);
	$_COOKIE[epc_portal_shared_erp_cookie_name()] = $key;
	if (session_status() === PHP_SESSION_NONE) {
		@session_start();
	}
	if (session_status() === PHP_SESSION_ACTIVE) {
		$_SESSION['epc_erp_tenant_bound'] = $key;
	}
}

function epc_portal_shared_erp_clear_tenant_cookie(): void
{
	$secure = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
		|| (!empty($_SERVER['HTTP_X_FORWARDED_PROTO']) && $_SERVER['HTTP_X_FORWARDED_PROTO'] === 'https');
	setcookie(epc_portal_shared_erp_cookie_name(), '', time() - 3600, '/; samesite=Lax', '', $secure, true);
	unset($_COOKIE[epc_portal_shared_erp_cookie_name()]);
	if (session_status() === PHP_SESSION_ACTIVE) {
		unset($_SESSION['epc_erp_tenant_bound']);
	}
}

function epc_portal_shared_erp_session_valid(?array $tenantRow = null): bool
{
	$session = isset($_COOKIE['admin_session']) ? (string) $_COOKIE['admin_session'] : '';
	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	if ($session === '' || $userId <= 0) {
		return false;
	}
	if ($tenantRow === null) {
		$siteKey = epc_portal_shared_erp_cookie_site_key();
		if ($siteKey === '') {
			return false;
		}
		$tenantRow = epc_portal_shared_erp_load_by_site_key($siteKey);
	}
	if ($tenantRow === null) {
		return false;
	}
	$pdo = epc_portal_shared_erp_tenant_pdo($tenantRow);
	if (!$pdo instanceof PDO) {
		return false;
	}
	try {
		$st = $pdo->prepare('SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?');
		$st->execute(array($session, $userId));
		return ((int) $st->fetchColumn()) === 1;
	} catch (Exception $e) {
		return false;
	}
}

/**
 * When tenant cookie is missing, find exactly one shared ERP tenant whose DB holds this CP session.
 */
function epc_portal_shared_erp_infer_tenant_from_session(): ?array
{
	$session = isset($_COOKIE['admin_session']) ? (string) $_COOKIE['admin_session'] : '';
	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	if ($session === '' || $userId <= 0) {
		return null;
	}
	$matches = array();
	foreach (epc_portal_shared_erp_list_tenants() as $row) {
		if (!epc_portal_shared_erp_session_valid($row)) {
			continue;
		}
		$matches[] = $row;
	}
	if (count($matches) !== 1) {
		return null;
	}
	epc_portal_shared_erp_set_tenant_cookie((string) $matches[0]['site_key']);
	return $matches[0];
}

/** First shared-ERP site_key matching a login contact (email/phone), or empty. */
function epc_portal_shared_erp_site_key_for_contact(string $authContact, string $contactType): string
{
	if ($contactType !== 'email' && $contactType !== 'phone') {
		return '';
	}
	foreach (epc_portal_shared_erp_list_tenants() as $row) {
		$pdo = epc_portal_shared_erp_tenant_pdo($row);
		if (!$pdo instanceof PDO) {
			continue;
		}
		try {
			$st = $pdo->prepare(
				'SELECT COUNT(*) FROM `users`
				 WHERE `' . $contactType . '` = ? AND `' . $contactType . '_confirmed` = 1 AND `unlocked` = 1'
			);
			$st->execute(array(htmlentities($authContact)));
			if ((int) $st->fetchColumn() > 0) {
				return preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
			}
		} catch (Exception $e) {
			continue;
		}
	}
	return '';
}

/** True when this login exists in any shared ERP tenant DB. */
function epc_portal_shared_erp_email_belongs_to_tenant(string $authContact, string $contactType): bool
{
	return epc_portal_shared_erp_site_key_for_contact($authContact, $contactType) !== '';
}

/** True when this email exists only in shared ERP tenant DB(s), not platform super-CP. */
function epc_portal_shared_erp_email_is_tenant_only(string $authContact, string $contactType): bool
{
	if (!epc_portal_shared_erp_email_belongs_to_tenant($authContact, $contactType)) {
		return false;
	}
	$platformPdo = epc_portal_platform_pdo();
	if (!$platformPdo instanceof PDO) {
		return true;
	}
	try {
		$st = $platformPdo->prepare(
			'SELECT COUNT(*) FROM `users`
			 WHERE `' . $contactType . '` = ? AND `' . $contactType . '_confirmed` = 1 AND `unlocked` = 1'
		);
		$st->execute(array(htmlentities($authContact)));
		return ((int) $st->fetchColumn()) === 0;
	} catch (Exception $e) {
		return true;
	}
}

function epc_portal_shared_erp_active_tenant(?PDO $platformPdo = null): ?array
{
	static $cached = null;
	static $resolved = false;
	if ($resolved) {
		return $cached;
	}
	$resolved = true;
	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		$cached = null;
		return null;
	}
	if (function_exists('epc_platform_erp_has_cookie') && epc_platform_erp_has_cookie()) {
		$cached = null;
		return null;
	}
	if (!function_exists('epc_portal_is_cp_request') || !epc_portal_is_cp_request()) {
		$cached = null;
		return null;
	}
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		$cached = null;
		return null;
	}
	if (function_exists('epc_client_erp_is_active') && !epc_client_erp_is_active()) {
		$cached = null;
		return null;
	}
	$siteKey = epc_portal_shared_erp_cookie_site_key();
	$row = null;
	if ($siteKey !== '') {
		$row = epc_portal_shared_erp_load_by_site_key($siteKey, $platformPdo);
		if ($row !== null && !epc_portal_shared_erp_session_valid($row)) {
			$row = null;
		}
	}
	if ($row === null) {
		$row = epc_portal_shared_erp_infer_tenant_from_session();
	}
	if ($row === null) {
		$cached = null;
		return null;
	}
	$cached = $row;
	return $cached;
}

function epc_portal_is_shared_erp_cp_session(): bool
{
	return epc_portal_shared_erp_active_tenant() !== null;
}

function epc_portal_shared_erp_cp_base_url(): string
{
	return 'https://' . epc_portal_shared_erp_platform_hostname() . '/cp/';
}

function epc_portal_shared_erp_shell_url(?string $siteKey = null): string
{
	if ($siteKey === null && function_exists('epc_client_erp_site_key')) {
		$siteKey = epc_client_erp_site_key();
	}
	if ($siteKey !== null && $siteKey !== '' && function_exists('epc_client_erp_shell_url')) {
		return epc_client_erp_shell_url($siteKey);
	}
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	return '/' . $backend . '/shop/finance/erp?epc_erp_shell=1';
}

/**
 * Bind DP_Config to a shared ERP tenant registry row (legal isolation).
 * Never binds docpart/ecomae — those are platform / storefront databases.
 */
function epc_portal_shared_erp_apply_row_config($DP_Config, array $row): bool
{
	if (!is_object($DP_Config)) {
		return false;
	}
	$host = epc_portal_shared_erp_platform_hostname();
	$expectedDb = trim((string) ($row['db_name'] ?? ''));
	if ($expectedDb === '' || in_array($expectedDb, array('docpart', 'ecomae'), true)) {
		if (function_exists('error_log')) {
			error_log(
				'epc_shared_erp: refused platform/storefront db_name=' . $expectedDb
				. ' site_key=' . ($row['site_key'] ?? '')
			);
		}
		return false;
	}
	if (!empty($row['db_name'])) {
		$DP_Config->db = (string) $row['db_name'];
	}
	if (!empty($row['db_user'])) {
		$DP_Config->user = (string) $row['db_user'];
	}
	if (isset($row['db_password']) && (string) $row['db_password'] !== '') {
		$DP_Config->password = (string) $row['db_password'];
	}
	$DP_Config->domain_path = 'https://' . $host . '/';
	if (!empty($row['industry_code'])) {
		$DP_Config->epc_portal_industry = (string) $row['industry_code'];
	}
	$DP_Config->epc_shared_erp_site_key = (string) ($row['site_key'] ?? '');
	$DP_Config->epc_shared_erp_trade_name = (string) ($row['trade_name'] ?? '');
	unset($DP_Config->epc_platform_erp);
	if (!empty($_GET['epc_db_debug']) && (string) $_GET['epc_db_debug'] === '1' && function_exists('error_log')) {
		error_log('epc_shared_erp: host=' . $host . ' db=' . ($DP_Config->db ?? '') . ' tenant=' . ($row['site_key'] ?? ''));
	}
	return true;
}

function epc_portal_shared_erp_apply_config($DP_Config): void
{
	if (!is_object($DP_Config)) {
		return;
	}
	$row = null;
	if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
		$row = $GLOBALS['epc_client_erp_tenant_row'] ?? null;
		if (!is_array($row) && function_exists('epc_client_erp_tenant_row')) {
			$row = epc_client_erp_tenant_row();
		}
	}
	if (!is_array($row)) {
		$row = epc_portal_shared_erp_active_tenant();
	}
	if ($row === null) {
		return;
	}
	epc_portal_shared_erp_apply_row_config($DP_Config, $row);
}

/**
 * Find shared ERP tenant(s) matching CP credentials. Returns list of tenant rows.
 *
 * @return list<array>
 */
function epc_portal_shared_erp_find_by_credentials(string $authContact, string $password, string $contactType): array
{
	if ($contactType !== 'email' && $contactType !== 'phone') {
		return array();
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$hash = md5($password . $cfg->secret_succession);
	$matches = array();
	foreach (epc_portal_shared_erp_list_tenants() as $row) {
		$pdo = epc_portal_shared_erp_tenant_pdo($row);
		if (!$pdo instanceof PDO) {
			continue;
		}
		try {
			// Dual auth: bcrypt first, fall back to legacy MD5
			$st = $pdo->prepare(
				'SELECT `user_id`, `password` FROM `users`
				 WHERE `' . $contactType . '` = ? AND `' . $contactType . '_confirmed` = 1 AND `unlocked` = 1
				 LIMIT 1'
			);
			$st->execute(array(htmlentities($authContact)));
			$uRow = $st->fetch(PDO::FETCH_ASSOC);
			if (!$uRow) {
				continue;
			}
			$userId = (int) $uRow['user_id'];
			$storedPw = (string) ($uRow['password'] ?? '');
			$pwOk = false;
			if (password_verify($password, $storedPw)) {
				$pwOk = true;
			} elseif ($hash === $storedPw) {
				$pwOk = true;
				$upgFile = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_password_upgrade.php';
				if (is_file($upgFile)) {
					require_once $upgFile;
					if (function_exists('epc_password_upgrade_if_needed') && function_exists('epc_password_is_legacy_md5') && epc_password_is_legacy_md5($storedPw)) {
						epc_password_upgrade_if_needed($pdo, $userId, $password, $storedPw);
					}
				}
			}
			if (!$pwOk || $userId <= 0) {
				continue;
			}
			$bg = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1')->fetch(PDO::FETCH_ASSOC);
			$rootGroup = $bg ? (int) $bg['id'] : 3;
			$gst = $pdo->prepare('SELECT COUNT(*) FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` = ?');
			$gst->execute(array($userId, $rootGroup));
			if ((int) $gst->fetchColumn() < 1) {
				continue;
			}
			$row['_shared_erp_user_id'] = $userId;
			$matches[] = $row;
		} catch (Exception $e) {
			continue;
		}
	}
	return $matches;
}

/**
 * Complete login for a shared ERP tenant — session in tenant DB + tenant cookie.
 *
 * @return array{ok:bool,redirect?:string,message?:string,pick?:list<array>}
 */
function epc_portal_shared_erp_complete_login(string $authContact, string $password, string $contactType, string $siteKey = ''): array
{
	$matches = epc_portal_shared_erp_find_by_credentials($authContact, $password, $contactType);
	if (count($matches) === 0) {
		if (epc_portal_shared_erp_email_belongs_to_tenant($authContact, $contactType)) {
			// Only probe every tenant DB (to distinguish "outage" from "wrong
			// password") after the normal credential check already failed —
			// doing this unconditionally on every login doubles the number of
			// synchronous tenant DB connection attempts for no benefit on the
			// common (successful) path.
			$credsOk = false;
			foreach (epc_portal_shared_erp_list_tenants() as $probeRow) {
				if (epc_portal_shared_erp_tenant_pdo($probeRow) instanceof PDO) {
					$credsOk = true;
					break;
				}
			}
			if (!$credsOk) {
				return array(
					'ok' => false,
					'message' => 'Company ERP database is temporarily unavailable. Contact platform support.',
				);
			}
			return array('ok' => false, 'message' => 'Wrong password for your company ERP account.');
		}
		return array('ok' => false);
	}
	if ($siteKey !== '') {
		$filtered = array();
		foreach ($matches as $m) {
			if ((string) $m['site_key'] === $siteKey) {
				$filtered[] = $m;
			}
		}
		$matches = $filtered;
	}
	if (count($matches) === 0) {
		return array('ok' => false, 'message' => 'Company not found for these credentials');
	}
	if (count($matches) > 1) {
		return array('ok' => false, 'pick' => $matches);
	}
	$row = $matches[0];
	$userId = (int) ($row['_shared_erp_user_id'] ?? 0);
	$pdo = epc_portal_shared_erp_tenant_pdo($row);
	if (!$pdo instanceof PDO || $userId <= 0) {
		return array('ok' => false, 'message' => 'Tenant database unavailable');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$time = time();
	$sessionSuccession = md5($authContact . $time . $cfg->secret_succession);
	$csrfGuardKey = sha1($cfg->secret_succession . $sessionSuccession . ($_SERVER['REMOTE_ADDR'] ?? '') . ($_SERVER['HTTP_USER_AGENT'] ?? ''));
	// Old-session cleanup is a maintenance task, not something every single
	// login needs to pay for synchronously — run it for roughly 1 in 20
	// logins so stale rows still get reaped without adding two DELETE scans
	// to the login critical path every time.
	if (mt_rand(1, 20) === 1) {
		$lastDel = time() - 2592000;
		$pdo->prepare('DELETE FROM `users_options` WHERE `session_id` IN (SELECT `id` FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?)')->execute(array($userId, $lastDel));
		$pdo->prepare('DELETE FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?')->execute(array($userId, $lastDel));
	}
	$pdo->prepare(
		'INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `type`, `contact_type`, `csrf_guard_key`) VALUES (?,?,?,?,?,?,?)'
	)->execute(array($sessionSuccession, $userId, $time, '', 1, $contactType, $csrfGuardKey));
	$cookietime = !empty($_POST['rememberme']) ? time() + 9999999 : 0;
	setcookie('admin_session', $sessionSuccession, $cookietime, '/', '', false, true);
	setcookie('admin_u_id', (string) $userId, $cookietime, '/', '', false, true);
	epc_portal_shared_erp_set_tenant_cookie((string) $row['site_key']);
	$redirect = epc_portal_shared_erp_shell_url((string) $row['site_key']);
	return array('ok' => true, 'redirect' => $redirect);
}

function epc_portal_shared_erp_login_picker_html(array $tenants): string
{
	$html = '<div class="alert alert-info" style="margin-bottom:12px"><strong>Select company</strong> — your login exists in more than one ERP tenant on ecomae.com.</div>';
	$html .= '<div class="form-group"><label>Company</label><select class="form-control" name="epc_erp_tenant_pick" required>';
	foreach ($tenants as $t) {
		$label = trim((string) ($t['trade_name'] ?? ''));
		if ($label === '') {
			$label = (string) $t['site_key'];
		}
		$html .= '<option value="' . htmlspecialchars((string) $t['site_key'], ENT_QUOTES, 'UTF-8') . '">'
			. htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</option>';
	}
	$html .= '</select></div>';
	return $html;
}
