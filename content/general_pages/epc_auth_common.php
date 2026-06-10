<?php
/**
 * Shared CP modern auth — context, sessions, provisioning, handoff.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_auth_signing_secret(): string
{
	$authFile = $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
	if (is_file($authFile)) {
		require_once $authFile;
		if (function_exists('epc_deploy_token')) {
			return (string) epc_deploy_token();
		}
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	return (string) $cfg->secret_succession;
}

function epc_auth_require_https(): bool
{
	if (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') {
		return true;
	}
	if (!empty($_SERVER['HTTP_X_FORWARDED_PROTO']) && strtolower((string) $_SERVER['HTTP_X_FORWARDED_PROTO']) === 'https') {
		return true;
	}
	if (!empty($_SERVER['SERVER_PORT']) && (int) $_SERVER['SERVER_PORT'] === 443) {
		return true;
	}
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	if ($host !== '' && (strpos($host, 'localhost') !== false || $host === '127.0.0.1')) {
		return true;
	}
	return false;
}

function epc_auth_json_response(array $payload, int $code = 200): void
{
	http_response_code($code);
	header('Content-Type: application/json; charset=utf-8');
	header('Cache-Control: no-store');
	echo json_encode($payload);
	exit;
}

function epc_auth_bootstrap_config(): DP_Config
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$tenantDb = $_SERVER['DOCUMENT_ROOT'] . '/config.tenant-db.php';
	if (is_file($tenantDb)) {
		$epc_tenant_db = null;
		require $tenantDb;
		if (isset($epc_tenant_db) && is_array($epc_tenant_db)) {
			foreach (array('db', 'user', 'password') as $k) {
				if (!empty($epc_tenant_db[$k]) && property_exists($cfg, $k)) {
					$cfg->$k = $epc_tenant_db[$k];
				}
			}
		}
	}
	$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (is_file($portalFile)) {
		require_once $portalFile;
		if (function_exists('epc_portal_apply_config')) {
			epc_portal_apply_config($cfg);
		}
	}
	return $cfg;
}

function epc_auth_normalize_host(string $host): string
{
	$host = strtolower(trim($host));
	return preg_replace('/^www\./', '', $host);
}

/**
 * Resolve login target: super, demo, tenant, client_erp, platform_erp, tenant_local.
 *
 * @return array<string,mixed>
 */
function epc_auth_resolve_context(array $hints = array()): array
{
	$tenantKey = preg_replace(
		'/[^a-z0-9_]/',
		'',
		strtolower(trim((string) ($hints['tenant_key'] ?? $hints['site_key'] ?? '')))
	);

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	$demoFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	if (is_file($demoFile)) {
		require_once $demoFile;
	}
	$clientFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
	if (is_file($clientFile)) {
		require_once $clientFile;
	}
	$platformErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
	if (is_file($platformErpFile)) {
		require_once $platformErpFile;
	}

	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		return epc_auth_context_platform_erp($tenantKey);
	}
	if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
		return epc_auth_context_client_erp($tenantKey);
	}
	if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()) {
		$key = function_exists('epc_portal_demo_cp_site_key') ? epc_portal_demo_cp_site_key() : $tenantKey;
		return epc_auth_context_from_registry_key($key, 'demo');
	}
	if ($tenantKey !== '') {
		$fromKey = epc_auth_context_from_registry_key($tenantKey, '');
		if (!empty($fromKey['ok'])) {
			return $fromKey;
		}
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return epc_auth_context_super();
	}

	$host = function_exists('epc_portal_host') ? epc_portal_host() : strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$platformPdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
	if ($platformPdo instanceof PDO) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
		$row = epc_portal_tenant_control_get_row_by_hostname($platformPdo, $host);
		if (is_array($row)) {
			$kind = !empty($row['is_demo']) ? 'demo' : 'tenant';
			return epc_auth_context_from_row($row, $kind);
		}
	}

	return epc_auth_context_tenant_local($tenantKey);
}

function epc_auth_context_super(): array
{
	$cfg = epc_auth_bootstrap_config();
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? 'www.ecomae.com'));
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return array('ok' => false, 'message' => 'Database unavailable');
	}
	return array(
		'ok' => true,
		'kind' => 'super',
		'tenant_key' => '',
		'row' => null,
		'pdo' => $pdo,
		'return_host' => $host,
		'return_path' => '/' . $backend . '/control',
		'login_label' => 'ECOM AE Super CP',
		'allow_provision' => false,
	);
}

function epc_auth_context_platform_erp(string $tenantKey): array
{
	$ctx = epc_auth_context_super();
	if (empty($ctx['ok'])) {
		return $ctx;
	}
	$cfg = epc_auth_bootstrap_config();
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	$ctx['kind'] = 'platform_erp';
	$ctx['return_path'] = '/' . $backend . '/platform-erp/';
	$ctx['login_label'] = 'ECOM AE Platform ERP';
	return $ctx;
}

function epc_auth_context_client_erp(string $tenantKey): array
{
	$key = $tenantKey;
	if ($key === '' && function_exists('epc_client_erp_site_key')) {
		$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) epc_client_erp_site_key()));
	}
	if ($key === '') {
		return array('ok' => false, 'message' => 'Client ERP site key missing');
	}
	$platformPdo = epc_portal_platform_pdo();
	if (!$platformPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Platform database unavailable');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
	$row = epc_portal_tenant_control_get_row($platformPdo, $key);
	if (!$row) {
		return array('ok' => false, 'message' => 'Tenant not found');
	}
	$ctx = epc_auth_context_from_row($row, 'client_erp');
	if (!empty($ctx['ok']) && function_exists('epc_client_erp_shell_url')) {
		$ctx['return_path'] = (string) parse_url(epc_client_erp_shell_url($key), PHP_URL_PATH);
		if (empty($ctx['return_path'])) {
			$ctx['return_path'] = epc_client_erp_shell_url($key);
		}
	}
	return $ctx;
}

function epc_auth_context_from_registry_key(string $siteKey, string $forceKind): array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return array('ok' => false, 'message' => 'tenant_key required');
	}
	$platformPdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
	if (!$platformPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Platform database unavailable');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
	$row = epc_portal_tenant_control_get_row($platformPdo, $key);
	if (!$row) {
		return array('ok' => false, 'message' => 'Unknown tenant: ' . $key);
	}
	$kind = $forceKind !== '' ? $forceKind : (!empty($row['is_demo']) ? 'demo' : 'tenant');
	return epc_auth_context_from_row($row, $kind);
}

function epc_auth_context_from_row(array $row, string $kind): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Tenant database unavailable');
	}
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
	$host = strtolower(trim((string) ($row['hostname'] ?? 'www.ecomae.com')));
	$returnPath = '/cp/';
	if ($kind === 'demo') {
		$returnPath = function_exists('epc_portal_demo_cp_post_login_url')
			? (string) parse_url(epc_portal_demo_cp_post_login_url($key, $row), PHP_URL_PATH)
			: epc_portal_demo_cp_tenant_base($key) . 'shop/orders';
	} elseif ($kind === 'client_erp' && function_exists('epc_client_erp_shell_url')) {
		$returnPath = (string) parse_url(epc_client_erp_shell_url($key), PHP_URL_PATH) ?: '/cp/';
	} elseif (in_array($kind, array('tenant', 'tenant_local'), true) && function_exists('epc_cp_tenant_landing_url')) {
		$returnPath = epc_cp_tenant_landing_url();
	} else {
		$returnPath = function_exists('epc_cp_tenant_landing_url') ? epc_cp_tenant_landing_url() : '/cp/control';
	}
	$trade = trim((string) ($row['trade_name'] ?? $key));
	return array(
		'ok' => true,
		'kind' => $kind,
		'tenant_key' => $key,
		'row' => $row,
		'pdo' => $tenantPdo,
		'return_host' => $host,
		'return_path' => $returnPath,
		'login_label' => $trade !== '' ? $trade . ' CP' : 'Control Panel',
		'allow_provision' => in_array($kind, array('demo', 'tenant', 'tenant_local', 'client_erp'), true),
		'demo_contact_email' => strtolower(trim((string) ($row['demo_contact_email'] ?? ''))),
	);
}

function epc_auth_context_tenant_local(string $tenantKey): array
{
	$cfg = epc_auth_bootstrap_config();
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return array('ok' => false, 'message' => 'Database unavailable');
	}
	$key = $tenantKey;
	if ($key === '' && function_exists('epc_portal_site_profile')) {
		$profile = epc_portal_site_profile();
		$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($profile['site_key'] ?? '')));
	}
	$host = function_exists('epc_portal_host') ? epc_portal_host() : strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	return array(
		'ok' => true,
		'kind' => 'tenant_local',
		'tenant_key' => $key,
		'row' => null,
		'pdo' => $pdo,
		'return_host' => $host,
		'return_path' => function_exists('epc_cp_tenant_landing_url') ? epc_cp_tenant_landing_url($backend) : '/' . $backend . '/control',
		'login_label' => 'Control Panel',
		'allow_provision' => true,
		'demo_contact_email' => '',
	);
}

function epc_auth_backend_group_ids(PDO $pdo): array
{
	if (function_exists('epc_portal_demo_cp_backend_group_ids')) {
		return epc_portal_demo_cp_backend_group_ids($pdo);
	}
	$ids = array();
	try {
		$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 ORDER BY `id` ASC');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$ids[] = (int) $row['id'];
		}
	} catch (Throwable $e) {
	}
	return $ids !== array() ? $ids : array(3);
}

function epc_auth_user_has_backend_access(PDO $pdo, int $userId): bool
{
	$backendGroups = epc_auth_backend_group_ids($pdo);
	$st = $pdo->prepare('SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?');
	$st->execute(array($userId));
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		if (in_array((int) $row['group_id'], $backendGroups, true)) {
			return true;
		}
	}
	return false;
}

function epc_auth_find_or_provision_cp_user(array $context, string $email, string $displayName): int
{
	$email = strtolower(trim($email));
	if ($email === '') {
		return 0;
	}
	$pdo = $context['pdo'] ?? null;
	if (!$pdo instanceof PDO) {
		return 0;
	}

	$st = $pdo->prepare('SELECT `user_id`, `unlocked`, `email_confirmed` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($email));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		$userId = (int) $row['user_id'];
		if ((int) ($row['unlocked'] ?? 0) !== 1) {
			return 0;
		}
		if (function_exists('epc_portal_demo_ensure_cp_user_backend_groups')) {
			epc_portal_demo_ensure_cp_user_backend_groups($pdo, $userId);
		}
		return epc_auth_user_has_backend_access($pdo, $userId) ? $userId : 0;
	}

	$kind = (string) ($context['kind'] ?? '');
	if ($kind === 'super' || $kind === 'platform_erp') {
		return 0;
	}
	if (empty($context['allow_provision'])) {
		return 0;
	}

	if ($kind === 'demo') {
		$demoEmail = (string) ($context['demo_contact_email'] ?? '');
		if ($demoEmail !== '' && $email !== $demoEmail) {
			// Allow new users on demo sandboxes (social-style first login).
		}
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$randPass = bin2hex(random_bytes(8)) . 'Aa1!';
	$hash = md5($randPass . $cfg->secret_succession);
	$name = $displayName !== '' ? $displayName : explode('@', $email)[0];
	$pdo->prepare(
		'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `admin_created`)
		 VALUES (?, 1, ?, 1, 1, ?, 1)'
	)->execute(array($email, $hash, (string) time()));
	$userId = (int) $pdo->lastInsertId();
	@$pdo->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')
		->execute(array($userId, 'name', $name));
	if (function_exists('epc_portal_demo_ensure_cp_user_backend_groups')) {
		epc_portal_demo_ensure_cp_user_backend_groups($pdo, $userId);
	} else {
		$ins = $pdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
		foreach (epc_auth_backend_group_ids($pdo) as $gid) {
			$ins->execute(array($userId, (int) $gid));
		}
	}
	return epc_auth_user_has_backend_access($pdo, $userId) ? $userId : 0;
}

function epc_auth_create_cp_session_record(array $context, int $userId, string $contactType = 'email'): string
{
	$pdo = $context['pdo'] ?? null;
	if (!$pdo instanceof PDO) {
		return '';
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$st = $pdo->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$st->execute(array($userId));
	$user = $st->fetch(PDO::FETCH_ASSOC);
	if (!$user) {
		return '';
	}
	$authContact = $contactType === 'phone' ? (string) ($user['phone'] ?? '') : (string) ($user['email'] ?? '');
	if ($authContact === '') {
		$authContact = (string) ($user['email'] ?? '');
		$contactType = 'email';
	}
	$time = time();
	$sessionSuccession = md5($authContact . $time . $cfg->secret_succession);
	$csrfGuardKey = sha1($cfg->secret_succession . $sessionSuccession . ($_SERVER['REMOTE_ADDR'] ?? '') . ($_SERVER['HTTP_USER_AGENT'] ?? ''));
	$pdo->prepare(
		'INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `type`, `contact_type`, `csrf_guard_key`) VALUES (?,?,?,?,?,?,?)'
	)->execute(array($sessionSuccession, $userId, $time, '', 1, $contactType, $csrfGuardKey));
	return $sessionSuccession;
}

function epc_auth_set_cp_session_cookies(int $userId, string $sessionToken): void
{
	$secure = epc_auth_require_https();
	setcookie('admin_session', $sessionToken, 0, '/', '', $secure, true);
	setcookie('admin_u_id', (string) $userId, 0, '/', '', $secure, true);
	$_COOKIE['admin_session'] = $sessionToken;
	$_COOKIE['admin_u_id'] = (string) $userId;
	if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
		epc_portal_shared_erp_clear_tenant_cookie();
	}
	if (function_exists('epc_platform_erp_clear_cookie')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
		epc_platform_erp_clear_cookie();
	}
}

function epc_auth_establish_cp_session(array $context, int $userId, string $contactType = 'email'): string
{
	$session = epc_auth_create_cp_session_record($context, $userId, $contactType);
	if ($session === '') {
		return '';
	}
	epc_auth_set_cp_session_cookies($userId, $session);
	return $session;
}

function epc_auth_should_handoff(array $context): bool
{
	$currentHost = epc_auth_normalize_host((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$targetHost = epc_auth_normalize_host((string) ($context['return_host'] ?? ''));
	return $targetHost !== '' && $targetHost !== $currentHost;
}

function epc_auth_finish_login(array $context, int $userId, string $contactType = 'email', string $returnUrl = ''): array
{
	$mode = epc_auth_normalize_mode((string) ($context['auth_mode'] ?? 'cp'));
	if ($mode === 'storefront') {
		if (epc_auth_should_handoff($context)) {
			$session = epc_auth_create_storefront_session_record($context, $userId);
			if ($session === '') {
				return array('ok' => false, 'message' => 'Could not create session');
			}
			return array(
				'ok' => true,
				'redirect' => epc_auth_handoff_build($userId, $session, $context, 'storefront'),
			);
		}
		$session = epc_auth_establish_storefront_session($context, $userId);
		if ($session === '') {
			return array('ok' => false, 'message' => 'Could not create session');
		}
		return array(
			'ok' => true,
			'redirect' => epc_auth_storefront_post_login_redirect($context, $returnUrl),
		);
	}

	$session = epc_auth_create_cp_session_record($context, $userId, $contactType);
	if ($session === '') {
		return array('ok' => false, 'message' => 'Could not create session');
	}
	if (epc_auth_should_handoff($context)) {
		return array(
			'ok' => true,
			'redirect' => epc_auth_handoff_build($userId, $session, $context, 'cp'),
		);
	}
	epc_auth_set_cp_session_cookies($userId, $session);
	return array('ok' => true, 'redirect' => epc_auth_post_login_redirect($context));
}

function epc_auth_post_login_redirect(array $context): string
{
	$kind = (string) ($context['kind'] ?? '');
	$key = (string) ($context['tenant_key'] ?? '');
	if ($kind === 'platform_erp' && function_exists('epc_platform_erp_shell_url')) {
		return epc_platform_erp_shell_url();
	}
	if ($kind === 'client_erp' && function_exists('epc_client_erp_shell_url') && $key !== '') {
		return epc_client_erp_shell_url($key);
	}
	if ($kind === 'demo' && $key !== '' && function_exists('epc_portal_demo_cp_post_login_url')) {
		return epc_portal_demo_cp_post_login_url($key, $context['row'] ?? null);
	}
	if (in_array($kind, array('tenant', 'tenant_local'), true) && function_exists('epc_cp_tenant_landing_url')) {
		$cfg = epc_auth_bootstrap_config();
		$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
		$path = epc_cp_tenant_landing_url($backend);
		$host = (string) ($context['return_host'] ?? '');
		if ($host === '') {
			return $path;
		}
		return 'https://' . $host . $path;
	}
	if ($kind === 'super') {
		$cfg = epc_auth_bootstrap_config();
		$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
		return 'https://' . ($context['return_host'] ?? 'www.ecomae.com') . '/' . $backend . '/control';
	}
	$host = (string) ($context['return_host'] ?? '');
	$path = (string) ($context['return_path'] ?? '/cp/');
	if ($path !== '' && $path[0] !== '/') {
		$path = '/' . $path;
	}
	if ($host === '') {
		return $path;
	}
	return 'https://' . $host . $path;
}

function epc_auth_handoff_build(int $userId, string $sessionToken, array $context, string $mode = 'cp'): string
{
	$exp = time() + 120;
	$payload = array(
		'uid' => $userId,
		'sess' => $sessionToken,
		'host' => (string) ($context['return_host'] ?? ''),
		'path' => (string) ($context['return_path'] ?? '/cp/'),
		'exp' => $exp,
		'tk' => (string) ($context['tenant_key'] ?? ''),
		'mode' => epc_auth_normalize_mode($mode !== '' ? $mode : (string) ($context['auth_mode'] ?? 'cp')),
	);
	$json = json_encode($payload);
	$p = rtrim(strtr(base64_encode($json), '+/', '-_'), '=');
	$sig = hash_hmac('sha256', $p, epc_auth_signing_secret());
	$host = (string) ($context['return_host'] ?? '');
	if ($host === '') {
		if (epc_auth_normalize_mode((string) ($context['auth_mode'] ?? $mode)) === 'storefront') {
			return epc_auth_storefront_post_login_redirect($context);
		}
		return epc_auth_post_login_redirect($context);
	}
	return 'https://' . $host . '/epc-auth-handoff.php?p=' . rawurlencode($p) . '&s=' . rawurlencode($sig);
}

function epc_auth_handoff_verify(string $p, string $sig): ?array
{
	$expected = hash_hmac('sha256', $p, epc_auth_signing_secret());
	if (!hash_equals($expected, $sig)) {
		return null;
	}
	$json = base64_decode(strtr($p, '-_', '+/') . str_repeat('=', (4 - strlen($p) % 4) % 4));
	$data = json_decode((string) $json, true);
	if (!is_array($data) || empty($data['exp']) || (int) $data['exp'] < time()) {
		return null;
	}
	return $data;
}

function epc_auth_normalize_mode(string $mode): string
{
	return strtolower(trim($mode)) === 'storefront' ? 'storefront' : 'cp';
}

/** @return array<string,mixed> */
function epc_auth_read_json_body(): array
{
	$raw = file_get_contents('php://input');
	if (!is_string($raw) || trim($raw) === '') {
		return is_array($_POST) ? $_POST : array();
	}
	$decoded = json_decode($raw, true);
	return is_array($decoded) ? $decoded : array();
}

function epc_auth_storefront_lang_prefix(): string
{
	if (function_exists('epc_portal_demo_path_prefix')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
		$demoPrefix = epc_portal_demo_path_prefix();
		if ($demoPrefix !== '') {
			return $demoPrefix . '/en';
		}
	}
	global $multilang_params;
	if (!empty($multilang_params['lang_href']) && is_string($multilang_params['lang_href'])) {
		return rtrim((string) $multilang_params['lang_href'], '/');
	}
	return '/en';
}

/**
 * Storefront B2C context — tenant DB, demo path, return URL after login.
 *
 * @return array<string,mixed>
 */
function epc_auth_resolve_storefront_context(array $hints = array()): array
{
	$tenantKey = preg_replace(
		'/[^a-z0-9_]/',
		'',
		strtolower(trim((string) ($hints['tenant_key'] ?? $hints['site_key'] ?? '')))
	);

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	$demoFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	if (is_file($demoFile)) {
		require_once $demoFile;
	}

	if (function_exists('epc_portal_is_demo_storefront_context') && epc_portal_is_demo_storefront_context()) {
		$row = $GLOBALS['epc_demo_storefront_tenant_row'] ?? null;
		if (!is_array($row)) {
			$key = !empty($GLOBALS['epc_demo_storefront_site_key'])
				? preg_replace('/[^a-z0-9_]/', '', (string) $GLOBALS['epc_demo_storefront_site_key'])
				: $tenantKey;
			if ($key !== '') {
				return epc_auth_storefront_from_registry_key($key, 'storefront_demo');
			}
			return array('ok' => false, 'message' => 'Demo storefront context missing');
		}
		return epc_auth_storefront_from_row($row, 'storefront_demo');
	}

	if ($tenantKey !== '') {
		$fromKey = epc_auth_storefront_from_registry_key($tenantKey, 'storefront_tenant');
		if (!empty($fromKey['ok'])) {
			return $fromKey;
		}
	}

	$host = function_exists('epc_portal_host') ? epc_portal_host() : strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$platformPdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
	if ($platformPdo instanceof PDO) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
		$row = epc_portal_tenant_control_get_row_by_hostname($platformPdo, $host);
		if (is_array($row)) {
			$kind = !empty($row['is_demo']) ? 'storefront_demo' : 'storefront_tenant';
			return epc_auth_storefront_from_row($row, $kind);
		}
	}

	return epc_auth_storefront_local($tenantKey);
}

function epc_auth_storefront_from_registry_key(string $siteKey, string $kind): array
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	if ($key === '') {
		return array('ok' => false, 'message' => 'tenant_key required');
	}
	$platformPdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
	if (!$platformPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Platform database unavailable');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
	$row = epc_portal_tenant_control_get_row($platformPdo, $key);
	if (!$row) {
		return array('ok' => false, 'message' => 'Unknown tenant: ' . $key);
	}
	if ($kind === '' || $kind === 'storefront_tenant') {
		$kind = !empty($row['is_demo']) ? 'storefront_demo' : 'storefront_tenant';
	}
	return epc_auth_storefront_from_row($row, $kind);
}

function epc_auth_storefront_from_row(array $row, string $kind): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	$tenantPdo = epc_portal_demo_tenant_pdo($row);
	if (!$tenantPdo instanceof PDO) {
		return array('ok' => false, 'message' => 'Tenant database unavailable');
	}
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
	$host = strtolower(trim((string) ($row['hostname'] ?? '')));
	if ($host === '') {
		$host = function_exists('epc_portal_host') ? epc_portal_host() : '';
	}
	$langPrefix = '/en';
	$returnPath = $langPrefix . '/';
	if ($kind === 'storefront_demo' && function_exists('epc_portal_demo_path_prefix')) {
		$dp = epc_portal_demo_path_prefix();
		if ($dp !== '') {
			$langPrefix = $dp . '/en';
			$returnPath = $langPrefix . '/';
			if ($host === '' || strpos($host, 'ecomae.com') !== false) {
				$host = 'www.ecomae.com';
			}
		}
	} elseif ($host === '') {
		$host = function_exists('epc_portal_host') ? epc_portal_host() : '';
	}
	$trade = trim((string) ($row['trade_name'] ?? $key));
	return array(
		'ok' => true,
		'auth_mode' => 'storefront',
		'kind' => $kind,
		'tenant_key' => $key,
		'row' => $row,
		'pdo' => $tenantPdo,
		'return_host' => $host,
		'return_path' => $returnPath,
		'lang_prefix' => $langPrefix,
		'login_label' => $trade !== '' ? $trade : 'Shop',
		'allow_provision' => true,
	);
}

function epc_auth_storefront_local(string $tenantKey): array
{
	$cfg = epc_auth_bootstrap_config();
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return array('ok' => false, 'message' => 'Database unavailable');
	}
	$key = $tenantKey;
	if ($key === '' && function_exists('epc_portal_site_profile')) {
		$profile = epc_portal_site_profile();
		$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($profile['site_key'] ?? '')));
	}
	$host = function_exists('epc_portal_host') ? epc_portal_host() : strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$langPrefix = epc_auth_storefront_lang_prefix();
	return array(
		'ok' => true,
		'auth_mode' => 'storefront',
		'kind' => 'storefront_local',
		'tenant_key' => $key,
		'row' => null,
		'pdo' => $pdo,
		'return_host' => $host,
		'return_path' => rtrim($langPrefix, '/') . '/',
		'lang_prefix' => rtrim($langPrefix, '/'),
		'login_label' => function_exists('epc_site_trade_name') ? epc_site_trade_name() : 'Shop',
		'allow_provision' => true,
	);
}

function epc_auth_default_reg_variant_id(PDO $pdo): int
{
	try {
		$st = $pdo->query('SELECT `id` FROM `reg_variants` ORDER BY `order`, `id` ASC LIMIT 1');
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return (int) $row['id'];
		}
	} catch (Throwable $e) {
	}
	return 1;
}

function epc_auth_find_or_provision_storefront_customer(array $context, string $email, string $displayName): int
{
	$email = strtolower(trim($email));
	if ($email === '') {
		return 0;
	}
	$pdo = $context['pdo'] ?? null;
	if (!$pdo instanceof PDO) {
		return 0;
	}

	$st = $pdo->prepare('SELECT `user_id`, `unlocked`, `email_confirmed` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($email));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		$userId = (int) $row['user_id'];
		if ((int) ($row['unlocked'] ?? 0) !== 1) {
			return 0;
		}
		if ((int) ($row['email_confirmed'] ?? 0) !== 1) {
			$pdo->prepare('UPDATE `users` SET `email_confirmed` = 1 WHERE `user_id` = ?')->execute(array($userId));
		}
		return $userId;
	}

	if (empty($context['allow_provision'])) {
		return 0;
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$randPass = bin2hex(random_bytes(8)) . 'Aa1!';
	$hash = md5($randPass . $cfg->secret_succession);
	$name = $displayName !== '' ? $displayName : explode('@', $email)[0];
	$regVariant = epc_auth_default_reg_variant_id($pdo);
	$pdo->prepare(
		'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `ip_address`)
		 VALUES (?, 1, ?, 1, ?, ?, ?)'
	)->execute(array($email, $hash, $regVariant, (string) time(), (string) ($_SERVER['REMOTE_ADDR'] ?? '')));
	$userId = (int) $pdo->lastInsertId();
	@$pdo->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')
		->execute(array($userId, 'name', $name));

	$groupId = 0;
	try {
		$gq = $pdo->query('SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1');
		$gr = $gq->fetch(PDO::FETCH_ASSOC);
		if ($gr) {
			$groupId = (int) $gr['id'];
		}
	} catch (Throwable $e) {
	}
	if ($groupId > 0) {
		@$pdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)')
			->execute(array($userId, $groupId));
	}

	$tradeFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
	if (is_file($tradeFile)) {
		require_once $tradeFile;
		if (function_exists('epc_trade_save_registration')) {
			// Social / OTP first sign-in: retail auto-approved; contact CP for wholesale upgrade.
			epc_trade_save_registration($pdo, $userId, 'retail');
		}
	}

	return $userId;
}

function epc_auth_merge_guest_cart(PDO $pdo, int $userId, string $oldSessionCookie): void
{
	if ($oldSessionCookie === '' || $userId <= 0) {
		return;
	}
	try {
		$userCartQuery = $pdo->prepare(
			'SELECT `id` FROM `shop_carts` WHERE `user_id` = 0 AND `session_id` = (SELECT `id` FROM `sessions` WHERE `session` = ? LIMIT 1)'
		);
		$userCartQuery->execute(array(str_replace(' ', '', $oldSessionCookie)));
		while ($shopCartsId = $userCartQuery->fetch(PDO::FETCH_ASSOC)) {
			if ((int) ($shopCartsId['id'] ?? 0) > 0) {
				$pdo->prepare('UPDATE `shop_carts` SET `user_id` = ?, `session_id` = 0 WHERE `id` = ?')
					->execute(array($userId, (int) $shopCartsId['id']));
			}
		}
	} catch (Throwable $e) {
	}
}

function epc_auth_create_storefront_session_record(array $context, int $userId): string
{
	$pdo = $context['pdo'] ?? null;
	if (!$pdo instanceof PDO || $userId <= 0) {
		return '';
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$st = $pdo->prepare('SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$st->execute(array($userId));
	$user = $st->fetch(PDO::FETCH_ASSOC);
	if (!$user) {
		return '';
	}
	$authContact = (string) ($user['email'] ?? '');
	$time = time();
	$sessionSuccession = md5($authContact . $userId . $time . $cfg->secret_succession);
	$csrfGuardKey = sha1($cfg->secret_succession . $sessionSuccession . ($_SERVER['REMOTE_ADDR'] ?? '') . ($_SERVER['HTTP_USER_AGENT'] ?? ''));
	$lastActivity = time();
	$pdo->prepare(
		'INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `last_activiti_time`, `csrf_guard_key`) VALUES (?, ?, ?, ?, ?, ?)'
	)->execute(array($sessionSuccession, $userId, $time, '', $lastActivity, $csrfGuardKey));
	@$pdo->prepare('UPDATE `users` SET `time_last_visit` = ? WHERE `user_id` = ?')->execute(array($time, $userId));
	return $sessionSuccession;
}

function epc_auth_set_storefront_session_cookies(int $userId, string $sessionToken, ?PDO $pdo = null): void
{
	$secure = epc_auth_require_https();
	setcookie('session', $sessionToken, 0, '/', '', $secure, true);
	setcookie('u_id', (string) $userId, 0, '/', '', $secure, true);
	$_COOKIE['session'] = $sessionToken;
	$_COOKIE['u_id'] = (string) $userId;
	if ($pdo instanceof PDO) {
		$oldGuestSession = isset($_COOKIE['session']) ? (string) $_COOKIE['session'] : '';
		if ($oldGuestSession !== '' && $oldGuestSession !== $sessionToken) {
			epc_auth_merge_guest_cart($pdo, $userId, $oldGuestSession);
		}
	}
}

function epc_auth_establish_storefront_session(array $context, int $userId): string
{
	$session = epc_auth_create_storefront_session_record($context, $userId);
	if ($session === '') {
		return '';
	}
	$pdo = $context['pdo'] ?? null;
	$oldGuestSession = isset($_COOKIE['session']) ? (string) $_COOKIE['session'] : '';
	epc_auth_set_storefront_session_cookies($userId, $session);
	if ($pdo instanceof PDO && $oldGuestSession !== '') {
		epc_auth_merge_guest_cart($pdo, $userId, $oldGuestSession);
	}
	return $session;
}

function epc_auth_storefront_post_login_redirect(array $context, string $returnUrl = ''): string
{
	$returnUrl = trim($returnUrl);
	if ($returnUrl !== '' && $returnUrl[0] === '/' && strpos($returnUrl, '//') === false) {
		$host = (string) ($context['return_host'] ?? '');
		if ($host !== '') {
			return 'https://' . $host . $returnUrl;
		}
		global $DP_Config;
		if (isset($DP_Config) && is_object($DP_Config) && !empty($DP_Config->domain_path)) {
			return rtrim((string) $DP_Config->domain_path, '/') . $returnUrl;
		}
		return $returnUrl;
	}
	$host = (string) ($context['return_host'] ?? '');
	$path = (string) ($context['return_path'] ?? '/en/');
	if ($path !== '' && $path[0] !== '/') {
		$path = '/' . $path;
	}
	if ($host === '') {
		global $DP_Config;
		if (isset($DP_Config) && is_object($DP_Config) && !empty($DP_Config->domain_path)) {
			return rtrim((string) $DP_Config->domain_path, '/') . $path;
		}
		return $path;
	}
	return 'https://' . $host . $path;
}

function epc_auth_resolve_for_mode(string $mode, array $hints = array()): array
{
	if (epc_auth_normalize_mode($mode) === 'storefront') {
		return epc_auth_resolve_storefront_context($hints);
	}
	return epc_auth_resolve_context($hints);
}

function epc_auth_login_context_for_ui(string $mode = 'cp'): array
{
	$mode = epc_auth_normalize_mode($mode);
	$ctx = $mode === 'storefront'
		? epc_auth_resolve_storefront_context(array())
		: epc_auth_resolve_context(array());
	$oauth = epc_auth_oauth_config();
	$google = $oauth['google'] ?? array();
	$googleReady = trim((string) ($google['client_id'] ?? '')) !== ''
		&& trim((string) ($google['client_secret'] ?? '')) !== '';
	$policy = epc_cp_modern_auth_policy();
	$langPrefix = $mode === 'storefront'
		? (string) ($ctx['lang_prefix'] ?? epc_auth_storefront_lang_prefix())
		: '';
	return array(
		'context' => $mode,
		'tenant_key' => (string) ($ctx['tenant_key'] ?? ''),
		'kind' => (string) ($ctx['kind'] ?? ''),
		'login_label' => (string) ($ctx['login_label'] ?? ($mode === 'storefront' ? 'Shop' : 'Control Panel')),
		'lang_prefix' => $langPrefix,
		'password_enabled' => !empty($policy['password']),
		'email_otp_enabled' => !empty($policy['email_otp']),
		'google_enabled' => !empty($policy['google_oauth']) && $googleReady,
		'google_start_url' => '/epc-auth-google-start.php',
		'send_code_url' => '/epc-auth-send-code.php',
		'verify_code_url' => '/epc-auth-verify-code.php',
	);
}

/** Default CP login method toggles (platform-wide policy on Super CP). */
function epc_cp_modern_auth_defaults(): array
{
	return array(
		'password' => true,
		'email_otp' => true,
		'google_oauth' => true,
	);
}

/** Load merged modern-auth toggles from platform site settings. */
function epc_cp_modern_auth_policy(?PDO $pdo = null): array
{
	$defaults = epc_cp_modern_auth_defaults();
	$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (!is_file($portalFile)) {
		return $defaults;
	}
	require_once $portalFile;
	if (!function_exists('epc_portal_load_site_settings')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	}
	$settings = epc_portal_load_site_settings($pdo);
	$menu = isset($settings['cp_menu']) && is_array($settings['cp_menu']) ? $settings['cp_menu'] : array();
	$auth = isset($menu['modern_auth']) && is_array($menu['modern_auth']) ? $menu['modern_auth'] : array();
	foreach ($defaults as $key => $val) {
		if (array_key_exists($key, $auth)) {
			$defaults[$key] = !empty($auth[$key]);
		}
	}
	return $defaults;
}

/** Persist modern-auth toggles on www.ecomae.com platform settings. */
function epc_cp_modern_auth_save(PDO $pdo, array $toggles): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
	$host = 'www.ecomae.com';
	$settings = epc_portal_load_site_settings_for_host($pdo, $host);
	$menu = isset($settings['cp_menu']) && is_array($settings['cp_menu']) ? $settings['cp_menu'] : epc_portal_cp_menu_defaults();
	$merged = epc_cp_modern_auth_defaults();
	foreach ($merged as $key => $_) {
		$merged[$key] = !empty($toggles[$key]);
	}
	$menu['modern_auth'] = $merged;
	$settings['cp_menu'] = $menu;
	$settings['host'] = $host;
	epc_portal_save_site_settings($pdo, $settings);
	return $merged;
}

function epc_auth_oauth_config(): array
{
	$file = $_SERVER['DOCUMENT_ROOT'] . '/config.epc-oauth.php';
	if (!is_file($file)) {
		return array(
			'google' => array(
				'client_id' => '',
				'client_secret' => '',
				'redirect_uri' => 'https://www.ecomae.com/epc-auth-google-callback.php',
			),
		);
	}
	$cfg = require $file;
	return is_array($cfg) ? $cfg : array();
}
