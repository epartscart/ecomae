<?php
/**
 * Client ERP-only entry on www.ecomae.com — separate URL space from Super CP.
 *
 * Super CP operator  → /cp/  → tenant hub / control portal
 * Client ERP (ASAP)  → /cp/client-erp/{site_key}/ → tenant DB + ERP shell
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_portal.php';

function epc_client_erp_backend_dir(): string
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	return $backend !== '' ? $backend : 'cp';
}

function epc_client_erp_path_prefix(): string
{
	return '/' . epc_client_erp_backend_dir() . '/client-erp/';
}

function epc_client_erp_is_platform_cp_host(): bool
{
	return function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()
		&& function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request();
}

function epc_client_erp_parse_request(?string $uri = null): ?array
{
	if ($uri === null) {
		$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	}
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return null;
	}
	$prefix = epc_client_erp_path_prefix();
	if (strpos($path, $prefix) !== 0) {
		return null;
	}
	$rest = substr($path, strlen($prefix));
	$segments = explode('/', trim($rest, '/'));
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($segments[0] ?? '')));
	if ($siteKey === '') {
		return null;
	}
	$subPath = implode('/', array_slice($segments, 1));
	return array(
		'site_key' => $siteKey,
		'sub_path' => $subPath,
		'is_login_root' => $subPath === '',
	);
}

function epc_client_erp_is_bare_request(?string $uri = null): bool
{
	if ($uri === null) {
		$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	}
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return false;
	}
	$bare = rtrim(epc_client_erp_path_prefix(), '/');
	return $path === $bare || $path === $bare . '/';
}

function epc_client_erp_list_active_tenants(): array
{
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (!is_file($sharedFile)) {
		return array();
	}
	require_once $sharedFile;
	if (!function_exists('epc_portal_shared_erp_list_tenants')) {
		return array();
	}
	require_once __DIR__ . '/epc_portal_tenant_control.php';
	$out = array();
	foreach (epc_portal_shared_erp_list_tenants() as $row) {
		if (epc_portal_tenant_control_row_is_active($row)) {
			$out[] = $row;
		}
	}
	return $out;
}

function epc_client_erp_render_bare_hub_page(array $tenants, string $emptyMessage = ''): void
{
	http_response_code(200);
	header('Content-Type: text/html; charset=utf-8');
	$backend = epc_client_erp_backend_dir();
	$superCp = '/' . $backend . '/';
	echo '<!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">';
	echo '<title>Client ERP — Choose company</title>';
	echo '<style>
body{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#f4f6f8;color:#1f2937;margin:0;padding:24px}
.wrap{max-width:640px;margin:0 auto}
.card{background:#fff;border:1px solid #e5e7eb;border-radius:12px;padding:24px;box-shadow:0 8px 24px rgba(15,23,42,.06)}
h1{margin:0 0 8px;font-size:1.5rem}
p.lead{margin:0 0 20px;color:#4b5563;line-height:1.5}
ul{list-style:none;margin:0;padding:0}
li{margin:0 0 12px}
a.tenant{display:block;padding:14px 16px;border:1px solid #dbeafe;border-radius:10px;text-decoration:none;color:#111827;background:#f8fbff}
a.tenant:hover{border-color:#3b82f6;background:#eff6ff}
.tenant strong{display:block;font-size:1.05rem}
.tenant span{display:block;margin-top:4px;color:#6b7280;font-size:.9rem}
.foot{margin-top:20px;font-size:.9rem;color:#6b7280}
.foot a{color:#2563eb}
</style></head><body><div class="wrap"><div class="card">';
	echo '<h1>Client ERP</h1>';
	if ($emptyMessage !== '') {
		echo '<p class="lead">' . htmlspecialchars($emptyMessage, ENT_QUOTES, 'UTF-8') . '</p>';
		echo '<p class="foot"><a href="' . htmlspecialchars($superCp, ENT_QUOTES, 'UTF-8') . '">Super CP login</a></p>';
	} else {
		echo '<p class="lead">Select your company to open its ERP login. Each company has its own URL and database.</p><ul>';
		foreach ($tenants as $row) {
			$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? '')));
			if ($key === '') {
				continue;
			}
			$trade = trim((string) ($row['trade_name'] ?? ''));
			if ($trade === '') {
				$trade = strtoupper($key);
			}
			$url = epc_client_erp_login_url($key);
			echo '<li><a class="tenant" href="' . htmlspecialchars($url, ENT_QUOTES, 'UTF-8') . '">';
			echo '<strong>' . htmlspecialchars($trade, ENT_QUOTES, 'UTF-8') . '</strong>';
			echo '<span>' . htmlspecialchars($url, ENT_QUOTES, 'UTF-8') . '</span></a></li>';
		}
		echo '</ul><p class="foot">Platform operator? <a href="' . htmlspecialchars($superCp, ENT_QUOTES, 'UTF-8') . '">Open Super CP</a></p>';
	}
	echo '</div></div></body></html>';
}

/**
 * /cp/client-erp/ without site_key — picker, single-tenant redirect, or Super CP hint (never 404).
 */
function epc_client_erp_maybe_handle_bare_path(): void
{
	if (!epc_client_erp_is_platform_cp_host() || !epc_client_erp_is_bare_request()) {
		return;
	}
	$backend = epc_client_erp_backend_dir();
	if (function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()) {
		header('Location: /' . $backend . '/?epc_msg=client_erp_pick_tenant', true, 302);
		exit;
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedFile)) {
		require_once $sharedFile;
		$cookieKey = function_exists('epc_portal_shared_erp_cookie_site_key')
			? epc_portal_shared_erp_cookie_site_key()
			: '';
		if ($cookieKey !== '' && function_exists('epc_portal_shared_erp_load_by_site_key')
			&& epc_portal_shared_erp_load_by_site_key($cookieKey) !== null) {
			header('Location: ' . epc_client_erp_login_url($cookieKey), true, 302);
			exit;
		}
	}
	$tenants = epc_client_erp_list_active_tenants();
	if (count($tenants) === 1 && !empty($tenants[0]['site_key'])) {
		header('Location: ' . epc_client_erp_login_url((string) $tenants[0]['site_key']), true, 302);
		exit;
	}
	if (count($tenants) === 0) {
		epc_client_erp_render_bare_hub_page(array(), 'No Client ERP tenants are currently available.');
		exit;
	}
	epc_client_erp_render_bare_hub_page($tenants);
	exit;
}

function epc_client_erp_is_active(): bool
{
	return !empty($GLOBALS['epc_client_erp_context']);
}

function epc_client_erp_site_key(): string
{
	if (!empty($GLOBALS['epc_client_erp_site_key'])) {
		return (string) $GLOBALS['epc_client_erp_site_key'];
	}
	return '';
}

function epc_client_erp_login_url(string $siteKey): string
{
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
	return epc_client_erp_path_prefix() . $key . '/';
}

function epc_client_erp_shell_url(string $siteKey): string
{
	return epc_client_erp_login_url($siteKey) . 'shop/finance/erp?epc_erp_shell=1';
}

function epc_client_erp_original_uri(): string
{
	return (string) ($GLOBALS['epc_client_erp_original_uri'] ?? '');
}

function epc_client_erp_tenant_row(?string $siteKey = null): ?array
{
	$key = $siteKey !== null ? $siteKey : epc_client_erp_site_key();
	if ($key === '') {
		return null;
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (!is_file($sharedFile)) {
		return null;
	}
	require_once $sharedFile;
	if (!function_exists('epc_portal_shared_erp_load_by_site_key')) {
		return null;
	}
	return epc_portal_shared_erp_load_by_site_key($key);
}

function epc_client_erp_branding_label(?array $tenantRow = null): string
{
	if ($tenantRow === null) {
		$tenantRow = epc_client_erp_tenant_row();
	}
	$trade = $tenantRow ? trim((string) ($tenantRow['trade_name'] ?? '')) : '';
	if ($trade === '') {
		$key = epc_client_erp_site_key();
		$trade = $key !== '' ? strtoupper($key) : 'Client ERP';
	}
	return $trade . ' — ERP Login';
}

/**
 * Strip /cp/client-erp/{site_key}/ prefix so normal CP routing handles ERP pages.
 */
function epc_client_erp_bootstrap(): void
{
	if (epc_client_erp_is_active()) {
		return;
	}
	if (!epc_client_erp_is_platform_cp_host()) {
		return;
	}
	$parsed = epc_client_erp_parse_request();
	if ($parsed === null) {
		return;
	}

	$demoFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	if (is_file($demoFile)) {
		require_once $demoFile;
		if (function_exists('epc_portal_demo_load_live_row')) {
			$demoRow = epc_portal_demo_load_live_row($parsed['site_key']);
			if ($demoRow !== null) {
				$prefix = function_exists('epc_portal_demo_cp_path_prefix')
					? epc_portal_demo_cp_path_prefix()
					: '/cp/demo/';
				$dest = $prefix . $parsed['site_key'] . '/';
				if ($parsed['sub_path'] !== '') {
					$dest .= ltrim($parsed['sub_path'], '/');
				}
				$query = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_QUERY);
				if ($query !== null && $query !== '') {
					$dest .= (strpos($dest, '?') !== false ? '&' : '?') . $query;
				}
				header('Location: ' . $dest, true, 301);
				exit;
			}
		}
	}

	$GLOBALS['epc_client_erp_context'] = true;
	$GLOBALS['epc_client_erp_site_key'] = $parsed['site_key'];
	$GLOBALS['epc_client_erp_original_uri'] = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';

	$tenantRow = epc_client_erp_tenant_row($parsed['site_key']);
	if ($tenantRow === null) {
		http_response_code(404);
		header('Content-Type: text/html; charset=utf-8');
		echo '<!DOCTYPE html><html><head><title>Client ERP not found</title></head><body style="font-family:sans-serif;padding:24px">';
		echo '<h1>Client ERP not found</h1>';
		echo '<p>No shared ERP tenant is registered for site key <code>' . htmlspecialchars($parsed['site_key'], ENT_QUOTES, 'UTF-8') . '</code>.</p>';
		echo '<p><a href="/cp/">Super CP login</a></p></body></html>';
		exit;
	}
	require_once __DIR__ . '/epc_portal_tenant_control.php';
	if (!epc_portal_tenant_control_row_is_active($tenantRow)) {
		epc_portal_tenant_control_render_blocked(
			(string) ($tenantRow['trade_name'] ?? 'Client ERP') . ' — disabled',
			'This ERP tenant has been disabled by the platform operator.',
			503
		);
	}
	$GLOBALS['epc_client_erp_tenant_row'] = $tenantRow;

	if (function_exists('epc_platform_erp_clear_cookie')) {
		require_once __DIR__ . '/epc_platform_erp_router.php';
		epc_platform_erp_clear_cookie();
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedFile)) {
		require_once $sharedFile;
		if (function_exists('epc_portal_shared_erp_set_tenant_cookie')) {
			epc_portal_shared_erp_set_tenant_cookie($parsed['site_key']);
		}
	}

	if (!$parsed['is_login_root']) {
		$backend = epc_client_erp_backend_dir();
		$newPath = '/' . $backend . '/';
		if ($parsed['sub_path'] !== '') {
			$newPath .= ltrim($parsed['sub_path'], '/');
		}
		$query = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_QUERY);
		$_SERVER['REQUEST_URI'] = $newPath . ($query !== null && $query !== '' ? '?' . $query : '');
	} else {
		$backend = epc_client_erp_backend_dir();
		$query = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_QUERY);
		$_SERVER['REQUEST_URI'] = '/' . $backend . '/' . ($query !== null && $query !== '' ? '?' . $query : '');
	}
}

function epc_client_erp_block_platform_operator(): void
{
	if (!epc_client_erp_is_active()) {
		return;
	}
	if (!function_exists('epc_portal_is_platform_operator') || !epc_portal_is_platform_operator()) {
		return;
	}
	$backend = epc_client_erp_backend_dir();
	if (function_exists('epc_platform_erp_login_url')) {
		header('Location: ' . epc_platform_erp_login_url() . '?epc_msg=platform_no_client_erp', true, 302);
	} else {
		header('Location: /' . $backend . '/platform-erp/?epc_msg=platform_no_client_erp', true, 302);
	}
	exit;
}

/**
 * Shared ERP tenant sessions must stay on /cp/client-erp/{site_key}/ — never bare Super CP /cp/.
 */
function epc_client_erp_block_tenant_on_bare_cp(): void
{
	if (!epc_client_erp_is_platform_cp_host()) {
		return;
	}
	if (epc_client_erp_is_active()) {
		return;
	}
	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		return;
	}
	if (!empty($GLOBALS['epc_demo_cp_context'])) {
		return;
	}
	if (function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()) {
		return;
	}
	$requestMethod = isset($_SERVER['REQUEST_METHOD']) ? (string) $_SERVER['REQUEST_METHOD'] : 'GET';
	if ($requestMethod === 'POST' && !empty($_POST['authentication'])) {
		return;
	}

	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (!is_file($sharedFile)) {
		return;
	}
	require_once $sharedFile;

	$siteKey = '';
	$cookieKey = function_exists('epc_portal_shared_erp_cookie_site_key')
		? epc_portal_shared_erp_cookie_site_key()
		: '';
	if ($cookieKey !== '' && function_exists('epc_portal_shared_erp_load_by_site_key')) {
		$row = epc_portal_shared_erp_load_by_site_key($cookieKey);
		if ($row !== null) {
			if (function_exists('epc_portal_shared_erp_session_valid') && epc_portal_shared_erp_session_valid($row)) {
				$siteKey = $cookieKey;
			} elseif ($cookieKey !== '') {
				$siteKey = $cookieKey;
			}
		}
	}
	if ($siteKey === '' && function_exists('epc_portal_shared_erp_infer_tenant_from_session')) {
		$inferred = epc_portal_shared_erp_infer_tenant_from_session();
		if (is_array($inferred) && !empty($inferred['site_key'])) {
			$siteKey = (string) $inferred['site_key'];
		}
	}
	if ($siteKey === '') {
		return;
	}

	$dest = epc_client_erp_shell_url($siteKey);
	if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
		epc_portal_shared_erp_clear_tenant_cookie();
	}
	header('Location: ' . $dest . '&epc_msg=use_client_erp', true, 302);
	exit;
}

/**
 * Legacy shared ERP shell on platform host — redirect to client-erp URL or Super CP hub.
 */
function epc_client_erp_redirect_legacy_shell(): void
{
	if (!epc_client_erp_is_platform_cp_host() || epc_client_erp_is_active()) {
		return;
	}
	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		return;
	}
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
		return;
	}
	$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || !preg_match('#/shop/finance/erp(?:/|$)#', $path)) {
		return;
	}
	$isShell = isset($_GET['epc_erp_shell']) && (string) $_GET['epc_erp_shell'] === '1';
	if (!$isShell && !function_exists('epc_erp_is_shell_request')) {
		return;
	}
	if (!$isShell && function_exists('epc_erp_is_shell_request')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
		$isShell = epc_erp_is_shell_request();
	}
	if (!$isShell) {
		return;
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
	$subPath = function_exists('epc_erp_cp_route_subpath') ? epc_erp_cp_route_subpath($path) : '';

	if (function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()) {
		$base = function_exists('epc_platform_erp_path_prefix')
			? epc_platform_erp_path_prefix() . 'shop/finance/erp?epc_erp_shell=1'
			: '/' . epc_client_erp_backend_dir() . '/platform-erp/shop/finance/erp?epc_erp_shell=1';
		$dest = function_exists('epc_erp_cp_shell_url_with_subpath')
			? epc_erp_cp_shell_url_with_subpath($base, $subPath)
			: $base;
		header('Location: ' . $dest, true, 302);
		exit;
	}

	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (!is_file($sharedFile)) {
		return;
	}
	require_once $sharedFile;
	$siteKey = function_exists('epc_portal_shared_erp_cookie_site_key')
		? epc_portal_shared_erp_cookie_site_key()
		: '';
	if ($siteKey !== '' && function_exists('epc_portal_shared_erp_load_by_site_key')
		&& epc_portal_shared_erp_load_by_site_key($siteKey) !== null) {
		$base = epc_client_erp_shell_url($siteKey);
		$dest = function_exists('epc_erp_cp_shell_url_with_subpath')
			? epc_erp_cp_shell_url_with_subpath($base, $subPath)
			: $base;
		header('Location: ' . $dest, true, 301);
		exit;
	}

	$loginKey = function_exists('epc_portal_shared_erp_cookie_site_key')
		? epc_portal_shared_erp_cookie_site_key()
		: '';
	if ($loginKey === '' && function_exists('epc_portal_shared_erp_list_tenants')) {
		$tenants = epc_portal_shared_erp_list_tenants();
		if (count($tenants) === 1 && !empty($tenants[0]['site_key'])) {
			$loginKey = (string) $tenants[0]['site_key'];
		}
	}
	if ($loginKey === '') {
		header('Location: /' . epc_client_erp_backend_dir() . '/?epc_msg=client_erp_login_required', true, 302);
		exit;
	}
	$dest = epc_client_erp_login_url($loginKey) . 'shop/finance/erp';
	if ($subPath !== '') {
		$dest .= '/' . $subPath;
	}
	$dest .= '?epc_erp_shell=1';
	header('Location: ' . $dest, true, 302);
	exit;
}

function epc_cp_logout_redirect_url(): string
{
	$backend = epc_client_erp_backend_dir();
	if (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()
		&& function_exists('epc_portal_demo_cp_site_key') && function_exists('epc_portal_demo_cp_login_url')) {
		$key = epc_portal_demo_cp_site_key();
		if ($key !== '') {
			return epc_portal_demo_cp_login_url($key);
		}
	}
	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		return function_exists('epc_platform_erp_login_url')
			? epc_platform_erp_login_url()
			: '/' . $backend . '/platform-erp/';
	}
	if (function_exists('epc_platform_erp_has_cookie') && epc_platform_erp_has_cookie()) {
		return function_exists('epc_platform_erp_login_url')
			? epc_platform_erp_login_url()
			: '/' . $backend . '/platform-erp/';
	}
	if (epc_client_erp_is_active()) {
		$key = epc_client_erp_site_key();
		if ($key !== '') {
			return epc_client_erp_login_url($key);
		}
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedFile)) {
		require_once $sharedFile;
		$cookieKey = function_exists('epc_portal_shared_erp_cookie_site_key')
			? epc_portal_shared_erp_cookie_site_key()
			: '';
		if ($cookieKey !== '' && function_exists('epc_client_erp_login_url')) {
			return epc_client_erp_login_url($cookieKey);
		}
	}
	return '/' . $backend . '/';
}

function epc_cp_perform_logout(?PDO $pdo = null): void
{
	$session = isset($_COOKIE['admin_session']) ? (string) $_COOKIE['admin_session'] : '';
	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	if ($pdo instanceof PDO && $session !== '' && $userId > 0) {
		try {
			$st = $pdo->prepare('DELETE FROM `sessions` WHERE `session` = ? AND `user_id` = ?');
			$st->execute(array($session, $userId));
		} catch (Exception $e) {
			// continue cookie clear
		}
	}
	setcookie('admin_session', '', time() - 3600, '/', '', !empty($_SERVER['HTTPS']), true);
	setcookie('admin_u_id', '', time() - 3600, '/', '', !empty($_SERVER['HTTPS']), true);
	setcookie('epc_erp_shell', '', time() - 3600, '/');
	if (function_exists('epc_platform_erp_clear_cookie')) {
		epc_platform_erp_clear_cookie();
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedFile)) {
		require_once $sharedFile;
		if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
			epc_portal_shared_erp_clear_tenant_cookie();
		}
	}
	unset($_COOKIE['admin_session'], $_COOKIE['admin_u_id'], $_COOKIE['epc_erp_shell']);
}

function epc_cp_logout_if_requested(): bool
{
	$method = isset($_SERVER['REQUEST_METHOD']) ? (string) $_SERVER['REQUEST_METHOD'] : 'GET';
	if ($method === 'POST' && !empty($_POST['logout']) && (string) $_POST['logout'] === 'logout') {
		return false;
	}
	$wantLogout = !empty($_GET['logout']) && (string) $_GET['logout'] === '1';
	if (!$wantLogout) {
		return false;
	}
	global $DP_Config;
	$pdo = null;
	if (isset($DP_Config) && is_object($DP_Config)) {
		try {
			$pdo = new PDO(
				'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
				$DP_Config->user,
				$DP_Config->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
		} catch (Exception $e) {
			$pdo = null;
		}
	}
	epc_cp_perform_logout($pdo);
	header('Location: ' . epc_cp_logout_redirect_url(), true, 302);
	exit;
}
