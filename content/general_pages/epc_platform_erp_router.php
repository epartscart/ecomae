<?php
/**
 * Platform ERP — ECOM AE company's own ERP on the ecomae registry DB.
 *
 * Super CP (tenant hub)  → /cp/
 * Platform ERP           → /cp/platform-erp/  → ecomae DB + ERP shell
 * Client ERP (ASAP, …)   → /cp/client-erp/{site_key}/ → tenant DB
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_portal.php';

function epc_platform_erp_cookie_name(): string
{
	return 'epc_platform_erp';
}

function epc_platform_erp_backend_dir(): string
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	return $backend !== '' ? $backend : 'cp';
}

function epc_platform_erp_path_prefix(): string
{
	return '/' . epc_platform_erp_backend_dir() . '/platform-erp/';
}

function epc_platform_erp_is_platform_cp_host(): bool
{
	return function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()
		&& function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request();
}

function epc_platform_erp_parse_request(?string $uri = null): ?array
{
	if ($uri === null) {
		$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	}
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return null;
	}
	$prefix = epc_platform_erp_path_prefix();
	if ($path !== rtrim($prefix, '/') && strpos($path, $prefix) !== 0) {
		return null;
	}
	$rest = '';
	if (strlen($path) > strlen(rtrim($prefix, '/'))) {
		$rest = ltrim(substr($path, strlen(rtrim($prefix, '/'))), '/');
	}
	return array(
		'sub_path' => $rest,
		'is_login_root' => $rest === '',
	);
}

function epc_platform_erp_is_active(): bool
{
	return !empty($GLOBALS['epc_platform_erp_context']);
}

/**
 * True when this request is Platform ERP (path prefix, bootstrap context, or cookie).
 * Use before tenant isolation — rewritten URIs no longer contain /platform-erp/.
 */
function epc_platform_erp_is_request(): bool
{
	if (!empty($GLOBALS['epc_demo_cp_context'])) {
		return false;
	}
	$demoPrefix = '';
	if (function_exists('epc_portal_demo_cp_path_prefix')) {
		$demoPrefix = epc_portal_demo_cp_path_prefix();
	} elseif (function_exists('epc_portal_demo_is_cp_request') && epc_portal_demo_is_cp_request()) {
		return false;
	}
	if ($demoPrefix !== '') {
		foreach (array(
			(string) ($GLOBALS['epc_demo_cp_original_uri'] ?? ''),
			isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '',
			isset($_SERVER['HTTP_REFERER']) ? (string) $_SERVER['HTTP_REFERER'] : '',
		) as $probe) {
			if ($probe !== '' && strpos($probe, $demoPrefix) !== false) {
				return false;
			}
		}
	}
	if (epc_platform_erp_is_active() || epc_platform_erp_has_cookie()) {
		return true;
	}
	$orig = (string) ($GLOBALS['epc_platform_erp_original_uri'] ?? '');
	if ($orig !== '' && strpos($orig, epc_platform_erp_path_prefix()) !== false) {
		return true;
	}
	$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	if ($uri !== '' && strpos($uri, epc_platform_erp_path_prefix()) !== false) {
		return true;
	}
	$ref = isset($_SERVER['HTTP_REFERER']) ? (string) $_SERVER['HTTP_REFERER'] : '';
	if ($ref !== '' && strpos($ref, epc_platform_erp_path_prefix()) !== false) {
		return true;
	}
	return false;
}

function epc_platform_erp_login_url(): string
{
	return epc_platform_erp_path_prefix();
}

function epc_platform_erp_shell_url(): string
{
	// On the platform hostname the standalone /erp/ portal is canonical.
	if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()) {
		return '/erp/';
	}
	return epc_platform_erp_path_prefix() . 'shop/finance/erp?epc_erp_shell=1';
}

/**
 * Map legacy CP ERP sub-paths onto the standalone /erp/ portal routes.
 * /erp only understands /, /guide, /ajax — not /shop/finance/erp/* or bare aliases.
 *
 * $subPath may be a full "shop/finance/erp/..." remnant or a short alias from
 * epc_erp_cp_route_subpath() (e.g. "guide", "uae-tax-compliance").
 */
function epc_platform_erp_portal_target_from_subpath(string $subPath): string
{
	$sub = trim(str_replace('\\', '/', $subPath), '/');
	if ($sub === '' || $sub === 'shop/finance/erp') {
		return '/erp/';
	}
	$leaf = $sub;
	if (strpos($sub, 'shop/finance/erp/') === 0) {
		$leaf = substr($sub, strlen('shop/finance/erp/'));
	} elseif ($sub === 'shop/finance/erp') {
		return '/erp/';
	}
	$leaf = trim($leaf, '/');
	if ($leaf === '' || $leaf === 'ajax') {
		return $leaf === 'ajax' ? '/erp/ajax' : '/erp/';
	}
	if ($leaf === 'guide' || substr($leaf, -5) === 'guide' || strpos($leaf, 'guide') !== false) {
		return '/erp/guide';
	}
	// Unknown CP ERP tabs (uae-tax-compliance, etc.) land on the portal home.
	return '/erp/';
}

function epc_platform_erp_original_uri(): string
{
	return (string) ($GLOBALS['epc_platform_erp_original_uri'] ?? '');
}

function epc_platform_erp_has_cookie(): bool
{
	return !empty($_COOKIE[epc_platform_erp_cookie_name()])
		&& (string) $_COOKIE[epc_platform_erp_cookie_name()] === '1';
}

function epc_platform_erp_set_cookie(): void
{
	setcookie(epc_platform_erp_cookie_name(), '1', 0, '/', '', !empty($_SERVER['HTTPS']), true);
	$_COOKIE[epc_platform_erp_cookie_name()] = '1';
}

function epc_platform_erp_clear_cookie(): void
{
	setcookie(epc_platform_erp_cookie_name(), '', time() - 3600, '/', '', !empty($_SERVER['HTTPS']), true);
	unset($_COOKIE[epc_platform_erp_cookie_name()]);
}

/**
 * Force platform registry DB (ecomae) — never docpart or tenant DBs.
 */
function epc_platform_erp_apply_config($DP_Config): void
{
	if (!is_object($DP_Config)) {
		return;
	}
	$sites = function_exists('epc_portal_sites') ? epc_portal_sites() : array();
	$profile = isset($sites['www.ecomae.com']) ? $sites['www.ecomae.com'] : array();
	foreach (array('db', 'user', 'password', 'domain_path') as $key) {
		if (!empty($profile[$key])) {
			$DP_Config->$key = $profile[$key];
		}
	}
	$cfgFile = $_SERVER['DOCUMENT_ROOT'] . '/config.local.php';
	if (is_file($cfgFile)) {
		$epc_config_local = null;
		require $cfgFile;
		if (isset($epc_config_local) && is_array($epc_config_local)) {
			foreach (array('db', 'user', 'password') as $key) {
				if (!empty($epc_config_local[$key])) {
					$DP_Config->$key = $epc_config_local[$key];
				}
			}
		}
	}
	$DP_Config->epc_portal_industry = 'platform_host';
	$DP_Config->epc_platform_erp = true;
	unset($DP_Config->epc_shared_erp_site_key, $DP_Config->epc_shared_erp_trade_name);
}

/**
 * Strip /cp/platform-erp/ prefix so normal CP routing handles ERP pages.
 *
 * On the ecomae.com platform host the standalone /erp/ portal is the
 * canonical ERP entry — redirect /cp/platform-erp/ there so operators
 * see only one link.
 */
function epc_platform_erp_bootstrap(): void
{
	if (!epc_platform_erp_is_platform_cp_host()) {
		return;
	}
	$parsed = epc_platform_erp_parse_request();
	if ($parsed === null) {
		return;
	}

	if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()) {
		$target = epc_platform_erp_portal_target_from_subpath((string) ($parsed['sub_path'] ?? ''));
		$query = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_QUERY);
		if (is_string($query) && $query !== '') {
			// Drop CP-shell flags — /erp/ does not use epc_erp_shell.
			$qs = array();
			parse_str($query, $qs);
			unset($qs['epc_erp_shell']);
			if ($qs !== array()) {
				$target .= (strpos($target, '?') !== false ? '&' : '?') . http_build_query($qs);
			}
		}
		if (!headers_sent()) {
			header('Location: ' . $target, true, 301);
		}
		exit;
	}

	$GLOBALS['epc_platform_erp_context'] = true;
	$GLOBALS['epc_platform_erp_original_uri'] = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';

	epc_platform_erp_set_cookie();
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedFile)) {
		require_once $sharedFile;
		if (function_exists('epc_portal_shared_erp_clear_tenant_cookie')) {
			epc_portal_shared_erp_clear_tenant_cookie();
		}
	}

	if (!$parsed['is_login_root']) {
		$backend = epc_platform_erp_backend_dir();
		$newPath = '/' . $backend . '/';
		if ($parsed['sub_path'] !== '') {
			$newPath .= ltrim($parsed['sub_path'], '/');
		}
		$query = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_QUERY);
		$_SERVER['REQUEST_URI'] = $newPath . ($query !== null && $query !== '' ? '?' . $query : '');
	} else {
		$backend = epc_platform_erp_backend_dir();
		$query = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_QUERY);
		$_SERVER['REQUEST_URI'] = '/' . $backend . '/' . ($query !== null && $query !== '' ? '?' . $query : '');
	}
}

/** Client ERP / tenant sessions must not use platform ERP URLs. */
function epc_platform_erp_block_client_session(): void
{
	if (!epc_platform_erp_is_active()) {
		return;
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (is_file($sharedFile)) {
		require_once $sharedFile;
		if (function_exists('epc_portal_is_shared_erp_cp_session') && epc_portal_is_shared_erp_cp_session()) {
			$siteKey = function_exists('epc_portal_shared_erp_cookie_site_key')
				? epc_portal_shared_erp_cookie_site_key()
				: '';
			if ($siteKey === '' && function_exists('epc_portal_shared_erp_infer_tenant_from_session')) {
				$inferred = epc_portal_shared_erp_infer_tenant_from_session();
				if (is_array($inferred) && !empty($inferred['site_key'])) {
					$siteKey = (string) $inferred['site_key'];
				}
			}
			$dest = function_exists('epc_client_erp_login_url') && $siteKey !== ''
				? epc_client_erp_login_url($siteKey)
				: '/cp/client-erp/';
			header('Location: ' . $dest . '?epc_msg=client_use_client_erp', true, 302);
			exit;
		}
	}
	if (function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()) {
		return;
	}
	$session = isset($_COOKIE['admin_session']) ? (string) $_COOKIE['admin_session'] : '';
	if ($session !== '' && function_exists('epc_cp_auth_gate_is_admin') && epc_cp_auth_gate_is_admin()) {
		$dest = '/cp/client-erp/';
		header('Location: ' . $dest . '?epc_msg=platform_erp_operators_only', true, 302);
		exit;
	}
}

/** Platform operators must not open client-erp paths (mirror client router). */
function epc_platform_erp_block_platform_operator_on_client_erp(): void
{
	// Handled in epc_client_erp_router — redirect to platform-erp.
}
