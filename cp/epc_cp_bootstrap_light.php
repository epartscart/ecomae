<?php
/**
 * Lightweight CP bootstrap for login + unauthenticated GET — skips ERP routers, menu, full desktop shell.
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_request_route(): string
{
	global $DP_Config;
	$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '/';
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		$path = '/';
	}
	$backend = 'cp';
	if (isset($DP_Config) && is_object($DP_Config) && !empty($DP_Config->backend_dir)) {
		$backend = trim((string) $DP_Config->backend_dir, '/');
	}
	if ($backend === '') {
		$backend = 'cp';
	}
	$base = '/' . $backend;
	if ($path === $base || $path === $base . '/') {
		return '';
	}
	if (strpos($path, $base . '/') === 0) {
		return ltrim(substr($path, strlen($base) + 1), '/');
	}
	return ltrim($path, '/');
}

function epc_cp_has_admin_cookies(): bool
{
	$session = isset($_COOKIE['admin_session']) ? (string) $_COOKIE['admin_session'] : '';
	$userId = isset($_COOKIE['admin_u_id']) ? (int) $_COOKIE['admin_u_id'] : 0;
	return $session !== '' && $userId > 0;
}

/**
 * True for login form GET and POST authentication (pre-session).
 */
function epc_cp_is_login_request(): bool
{
	if (!empty($GLOBALS['epc_demo_cp_context']) && function_exists('epc_portal_demo_parse_cp_path')) {
		$demoParsed = epc_portal_demo_parse_cp_path();
		if (is_array($demoParsed) && !empty($demoParsed['is_login_root'])) {
			return true;
		}
	}
	if (function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request()) {
		return false;
	}
	$clientErpRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
	if (is_file($clientErpRouter)) {
		require_once $clientErpRouter;
		if (function_exists('epc_client_erp_parse_request')) {
			$clientParsed = epc_client_erp_parse_request();
			if (is_array($clientParsed) && !empty($clientParsed['is_login_root'])) {
				return true;
			}
		}
		if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
			$isAdmin = function_exists('epc_cp_auth_gate_is_admin') && epc_cp_auth_gate_is_admin();
			if (!$isAdmin) {
				return true;
			}
			$route = epc_cp_request_route();
			return $route === '' || $route === 'index.php';
		}
	}
	$method = isset($_SERVER['REQUEST_METHOD']) ? (string) $_SERVER['REQUEST_METHOD'] : 'GET';
	if ($method === 'POST' && !empty($_POST['authentication'])) {
		return true;
	}
	if ($method !== 'GET') {
		return false;
	}
	if (epc_cp_has_admin_cookies()) {
		if (function_exists('epc_cp_auth_gate_is_admin') && epc_cp_auth_gate_is_admin()) {
			return false;
		}
	}
	$route = epc_cp_request_route();
	return $route === '' || $route === 'control' || $route === 'index.php';
}

function epc_cp_bootstrap_light_active(): bool
{
	return !empty($GLOBALS['epc_cp_light_bootstrap']);
}

function epc_cp_bootstrap_light_init(): void
{
	if (epc_cp_has_admin_cookies()) {
		$sessionValid = function_exists('epc_cp_auth_gate_is_admin') && epc_cp_auth_gate_is_admin();
		if (!$sessionValid) {
			if (!headers_sent()) {
				setcookie('admin_session', '', time() - 3600, '/', '', !empty($_SERVER['HTTPS']), true);
				setcookie('admin_u_id', '', time() - 3600, '/', '', !empty($_SERVER['HTTPS']), true);
			}
			unset($_COOKIE['admin_session'], $_COOKIE['admin_u_id']);
		}
	}
	if (epc_cp_is_login_request()) {
		$GLOBALS['epc_cp_light_bootstrap'] = true;
	}
}
