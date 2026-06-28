<?php
/**
 * ERP control-panel shell — dedicated layout without shop CP sidebar.
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_is_cp_erp_path($uri = null)
{
	if ($uri === null) {
		$uri = $_SERVER['REQUEST_URI'] ?? '';
	}
	if (!is_string($uri) || $uri === '') {
		return false;
	}
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path)) {
		return false;
	}
	return (bool) preg_match('#/shop/finance/erp(?:/|$|\?)#', $path);
}

/** Sub-path after shop/finance/erp/ — e.g. guide, custom-shipping-guide (empty for main module). */
function epc_erp_cp_route_subpath(?string $path = null): string
{
	if ($path === null) {
		$path = parse_url($_SERVER['REQUEST_URI'] ?? '', PHP_URL_PATH);
	}
	if (!is_string($path) || $path === '') {
		return '';
	}
	if (preg_match('#/shop/finance/erp/([^/?]+)#', $path, $m)) {
		return trim((string) $m[1], '/');
	}
	return '';
}

function epc_erp_cp_shell_url_with_subpath(string $baseShellUrl, string $subPath = ''): string
{
	$subPath = trim($subPath, '/');
	if ($subPath === '') {
		return $baseShellUrl;
	}
	$url = preg_replace('#\?.*$#', '', $baseShellUrl);
	$url = rtrim((string) $url, '/');
	if (substr($url, -strlen($subPath)) === $subPath) {
		return $baseShellUrl;
	}
	$dest = $url . '/' . $subPath;
	$shellQ = '';
	if (strpos($baseShellUrl, 'epc_erp_shell=1') !== false) {
		$shellQ = 'epc_erp_shell=1';
	} elseif (function_exists('epc_erp_shell_url_query')) {
		$shellQ = epc_erp_shell_url_query();
	}
	if ($shellQ !== '') {
		$dest .= (strpos($dest, '?') !== false ? '&' : '?') . $shellQ;
	}
	return $dest;
}

function epc_erp_demo_cp_blocks_shell(): bool
{
	if (!function_exists('epc_portal_demo_is_cp_context') || !epc_portal_demo_is_cp_context()) {
		return false;
	}
	$row = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
		// ERP-only sandbox demos use the professional ERP shell (same UX as client ERP).
		return false;
	}
	$key = function_exists('epc_portal_demo_cp_site_key') ? epc_portal_demo_cp_site_key() : '';
	if ($key === '') {
		return true;
	}
	// Commerce demo tenants use shop CP only — redirect ERP shell URLs back to demo CP login.
	if (function_exists('epc_erp_is_cp_erp_path') && epc_erp_is_cp_erp_path()
		&& function_exists('epc_portal_demo_cp_login_url') && !headers_sent()) {
		header('Location: ' . epc_portal_demo_cp_login_url($key), true, 302);
		exit;
	}
	return true;
}

function epc_erp_is_shell_request()
{
	if (epc_erp_demo_cp_blocks_shell()) {
		return false;
	}
	if (!empty($GLOBALS['epc_erp_standalone'])) {
		return true;
	}
	if (isset($_GET['epc_erp_shell']) && (string) $_GET['epc_erp_shell'] === '1') {
		return true;
	}
	if (isset($_COOKIE['epc_erp_shell']) && (string) $_COOKIE['epc_erp_shell'] === '1' && epc_erp_is_cp_erp_path()) {
		return true;
	}
	return false;
}

function epc_erp_shell_url_query()
{
	if (!empty($GLOBALS['epc_erp_standalone'])) {
		return '';
	}
	if (!empty($GLOBALS['epc_erp_shell_mode']) || epc_erp_is_shell_request()) {
		return 'epc_erp_shell=1';
	}
	return '';
}

function epc_erp_shell_append_query($url)
{
	$q = epc_erp_shell_url_query();
	if ($q === '') {
		return $url;
	}
	return $url . (strpos($url, '?') !== false ? '&' : '?') . $q;
}

/**
 * ERP CP redirect target — always preserve shell flag (AJAX + post-save navigation).
 */
function epc_erp_cp_redirect_url($url)
{
	$url = (string) $url;
	if ($url === '' || strpos($url, '/shop/finance/erp') === false) {
		return $url;
	}
	if (strpos($url, 'epc_erp_shell=') !== false) {
		return $url;
	}
	return $url . (strpos($url, '?') !== false ? '&' : '?') . 'epc_erp_shell=1';
}

/** True on www.ecomae.com where nginx often 404s static /cp/* and /content/*.css|.js. */
function epc_erp_shell_use_asset_proxies(): bool
{
	return function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname();
}

function epc_erp_shell_asset_href(string $staticPath, string $phpProxyPath): string
{
	$ver = function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260609erpshell1';
	$path = epc_erp_shell_use_asset_proxies() ? $phpProxyPath : $staticPath;
	return $path . '?v=' . rawurlencode($ver);
}

function epc_erp_shell_nav_js_href(): string
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	return epc_erp_shell_asset_href(
		'/' . $backend . '/js/epc_erp_shell_nav.js',
		'/content/general_pages/epc_erp_shell_nav_js.php'
	);
}

/**
 * External ERP shell nav JS — must load in template head, not inline in eval'd CP pane.
 */
function epc_erp_shell_nav_script_tag(): string
{
	$src = epc_erp_shell_nav_js_href();
	return '<script src="' . htmlspecialchars($src, ENT_QUOTES, 'UTF-8') . '" id="epc-erp-shell-nav-js"></script>' . "\n";
}

function epc_erp_voice_command_js_href(): string
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	return epc_erp_shell_asset_href(
		'/' . $backend . '/js/epc_erp_voice_command.js',
		'/content/general_pages/epc_erp_voice_command_js.php'
	);
}

function epc_erp_voice_command_js_script_tag(): string
{
	$src = epc_erp_voice_command_js_href();
	return '<script src="' . htmlspecialchars($src, ENT_QUOTES, 'UTF-8') . '" id="epc-erp-voice-js"></script>' . "\n";
}

function epc_erp_ai_assistant_js_href(): string
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	return epc_erp_shell_asset_href(
		'/' . $backend . '/js/epc_erp_ai_assistant.js',
		'/content/general_pages/epc_erp_ai_assistant_js.php'
	);
}

function epc_erp_ai_assistant_js_script_tag(): string
{
	$src = epc_erp_ai_assistant_js_href();
	return '<script src="' . htmlspecialchars($src, ENT_QUOTES, 'UTF-8') . '" id="epc-erp-ai-assistant-js" defer></script>' . "\n";
}

function epc_erp_seed_form_js_href(): string
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	return epc_erp_shell_asset_href(
		'/' . $backend . '/js/epc_erp_seed_form.js',
		'/content/general_pages/epc_erp_seed_form_js.php'
	);
}

function epc_erp_seed_form_js_script_tag(): string
{
	$src = epc_erp_seed_form_js_href();
	return '<script src="' . htmlspecialchars($src, ENT_QUOTES, 'UTF-8') . '" id="epc-erp-seed-form-js" defer></script>' . "\n";
}

function epc_erp_shell_link_attrs(): string
{
	if (epc_erp_shell_url_query() === '') {
		return '';
	}
	return ' data-epc-erp-shell="1"';
}

function epc_erp_cp_shell_maybe_set_cookie()
{
	if (headers_sent()) {
		return;
	}
	if (isset($_GET['epc_erp_shell']) && (string) $_GET['epc_erp_shell'] === '0') {
		setcookie('epc_erp_shell', '', time() - 3600, '/');
		return;
	}
	if (!epc_erp_is_cp_erp_path()) {
		return;
	}
	if (isset($_GET['epc_erp_shell']) && (string) $_GET['epc_erp_shell'] === '1') {
		setcookie('epc_erp_shell', '1', time() + 86400 * 30, '/', '', !empty($_SERVER['HTTPS']), true);
	}
}

/**
 * Query keys preserved when forcing ERP shell redirect (sidebar/tab navigation).
 */
function epc_erp_cp_shell_preserve_query_keys(): array
{
	return array(
		'area', 'tab', 'from', 'to',
		'cs_view', 'cs_category', 'cs_type', 'cs_id', 'cs_report',
		'account_id', 'user_id', 'supplier_id', 'crm_tab',
	);
}

function epc_erp_cp_shell_preserve_query_string(): string
{
	$parts = array();
	foreach (epc_erp_cp_shell_preserve_query_keys() as $key) {
		if (!isset($_GET[$key])) {
			continue;
		}
		$val = (string) $_GET[$key];
		if ($val === '') {
			continue;
		}
		$parts[] = rawurlencode($key) . '=' . rawurlencode($val);
	}
	return $parts !== array() ? implode('&', $parts) : '';
}

/**
 * Build shell URL for current ERP route (incl. guide/sub-pages) + preserved tab query.
 */
function epc_erp_cp_shell_redirect_destination(): string
{
	$sub = epc_erp_cp_route_subpath();
	$dest = epc_erp_cp_shell_url_with_subpath(epc_erp_cp_shell_launcher_url(), $sub);
	if (strpos($dest, 'epc_erp_shell=') === false) {
		$dest .= (strpos($dest, '?') !== false ? '&' : '?') . 'epc_erp_shell=1';
	}
	$extra = epc_erp_cp_shell_preserve_query_string();
	if ($extra === '') {
		return $dest;
	}
	return $dest . (strpos($dest, '?') !== false ? '&' : '?') . $extra;
}

/**
 * HTTP redirect before CP template pick — avoids commerce desktop.php when shell flag drops off links.
 */
function epc_erp_cp_shell_maybe_redirect()
{
	if (headers_sent()) {
		return;
	}
	if (!epc_erp_is_cp_erp_path()) {
		return;
	}
	if (epc_erp_is_shell_request()) {
		return;
	}
	if (($_SERVER['REQUEST_METHOD'] ?? 'GET') !== 'GET') {
		return;
	}
	if (isset($_GET['action']) && (string) $_GET['action'] !== '') {
		return;
	}
	$dest = epc_erp_cp_shell_redirect_destination();
	if (!headers_sent()) {
		setcookie('epc_erp_shell', '1', time() + 86400 * 30, '/', '', !empty($_SERVER['HTTPS']), true);
		$_COOKIE['epc_erp_shell'] = '1';
	}
	header('Location: ' . $dest, true, 302);
	exit;
}

/**
 * Page-level shell redirect (eval-safe).
 * Never put <script> tags in CP page PHP source — epc_cp_prepare_cp_page_content()
 * strips them before eval and leaks literal . json_encode($url) . into relocated scripts.
 */
function epc_erp_cp_shell_page_redirect(): void
{
	$dest = epc_erp_cp_shell_redirect_destination();
	if (!headers_sent()) {
		setcookie('epc_erp_shell', '1', time() + 86400 * 30, '/', '', !empty($_SERVER['HTTPS']), true);
		$_COOKIE['epc_erp_shell'] = '1';
		header('Location: ' . $dest, true, 302);
		exit;
	}
	$relocate = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_script_relocate.php';
	if (is_file($relocate)) {
		require_once $relocate;
		if (function_exists('epc_cp_footer_scripts_append')) {
			epc_cp_footer_scripts_append(
				'<script>location.replace(' . json_encode($dest) . ');</script>'
			);
		}
	}
}

function epc_erp_cp_shell_use_template()
{
	return epc_erp_is_shell_request() && epc_erp_is_cp_erp_path();
}

/**
 * CP URL prefix for ERP navigation — platform-erp, client-erp, demo CP, or /cp/.
 */
function epc_erp_cp_route_prefix(): string
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	$platformRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
	if (is_file($platformRouter)) {
		require_once $platformRouter;
		if (function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request()
			&& function_exists('epc_platform_erp_path_prefix')) {
			return epc_platform_erp_path_prefix();
		}
	}
	$clientRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
	if (is_file($clientRouter)) {
		require_once $clientRouter;
		if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()
			&& function_exists('epc_client_erp_site_key') && function_exists('epc_client_erp_path_prefix')) {
			$key = epc_client_erp_site_key();
			if ($key !== '') {
				return epc_client_erp_path_prefix() . $key . '/';
			}
		}
	}
	$demoRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	if (is_file($demoRouter)) {
		require_once $demoRouter;
		if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
			&& function_exists('epc_portal_demo_cp_tenant_base')) {
			$demoBase = epc_portal_demo_cp_tenant_base();
			if ($demoBase !== '') {
				return $demoBase;
			}
		}
	}
	return '/' . $backend . '/';
}

function epc_erp_cp_shell_erp_module_url(): string
{
	return rtrim(epc_erp_cp_route_prefix(), '/') . '/shop/finance/erp';
}

function epc_erp_cp_shell_launcher_url()
{
	global $DP_Config;
	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()
		&& function_exists('epc_platform_erp_shell_url')) {
		return epc_platform_erp_shell_url();
	}
	if (function_exists('epc_platform_erp_has_cookie') && epc_platform_erp_has_cookie()
		&& function_exists('epc_platform_erp_shell_url')) {
		return epc_platform_erp_shell_url();
	}
	if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()
		&& function_exists('epc_client_erp_shell_url') && function_exists('epc_client_erp_site_key')) {
		$key = epc_client_erp_site_key();
		if ($key !== '') {
			return epc_client_erp_shell_url($key);
		}
	}
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()
		&& function_exists('epc_portal_demo_erp_shell_url') && function_exists('epc_portal_demo_cp_site_key')) {
		$key = epc_portal_demo_cp_site_key();
		if ($key !== '') {
			return epc_portal_demo_erp_shell_url($key);
		}
	}
	$backend = isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp';
	return '/' . $backend . '/shop/finance/erp?epc_erp_shell=1';
}

function epc_erp_cp_shell_ecom_url()
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp';
	return '/' . $backend . '/?epc_erp_shell=0';
}

/**
 * Inline ERP styles when static /content/*.css URLs are blocked or cached 404.
 */
function epc_erp_shell_inline_style_block()
{
	global $DP_Config;
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	if ($root === '') {
		return '';
	}
	$backend = isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp';
	$files = array(
		'/' . $backend . '/templates/bootstrap_admin/css/epc_cp_ui.css',
		'/' . $backend . '/templates/bootstrap_admin/css/epc_cp_professional.css',
		'/content/shop/finance/epc_erp_portal.css',
		'/content/shop/finance/epc_erp_ui.css',
		'/content/shop/finance/epc_erp_professional.css',
	);
	$css = '';
	foreach ($files as $rel) {
		$path = $root . $rel;
		if (!is_file($path)) {
			continue;
		}
		$chunk = file_get_contents($path);
		if ($chunk !== false && $chunk !== '') {
			$css .= $chunk . "\n";
		}
	}
	if ($css === '') {
		return '';
	}
	return '<style id="epc-erp-inline-css">' . $css . '</style>' . "\n";
}

function epc_erp_cp_shell_portal_url()
{
	$lang = function_exists('epc_erp_lang_href') ? epc_erp_lang_href() : '';
	if (function_exists('epc_erp_portal_canonical_base')) {
		return epc_erp_portal_canonical_base($lang);
	}
	return ($lang !== '' ? $lang : '') . '/erp';
}

/**
 * Render a visible ERP shell error (avoid blank main area / CDN 403 pages).
 */
function epc_erp_isolation_block(string $message): void
{
	$logout = function_exists('epc_cp_logout_redirect_url')
		? epc_cp_logout_redirect_url() . (strpos(epc_cp_logout_redirect_url(), '?') !== false ? '&' : '?') . 'logout=1'
		: '/cp/?logout=1';
	if (isset($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config']) && !empty($GLOBALS['DP_Config']->backend_dir)) {
		if (!function_exists('epc_cp_logout_redirect_url')) {
			$logout = '/' . trim((string) $GLOBALS['DP_Config']->backend_dir, '/') . '/?logout=1';
		}
	}
	echo '<div class="alert alert-danger epc-erp-isolation-block" style="margin:16px;max-width:960px">';
	echo '<strong><i class="fa fa-exclamation-triangle"></i> ERP unavailable</strong>';
	echo '<p style="margin:10px 0 0">' . htmlspecialchars($message, ENT_QUOTES, 'UTF-8') . '</p>';
	echo '<p style="margin:12px 0 0"><a class="btn btn-warning btn-sm" href="'
		. htmlspecialchars($logout, ENT_QUOTES, 'UTF-8') . '"><i class="fa fa-sign-out"></i> Log out and sign in again</a></p>';
	echo '</div>';
	exit;
}

/**
 * ERP tenant DB isolation (legal/data boundary):
 * - /cp/client-erp/{site_key}/ → registry tenant DB only (e.g. asapc), never ecomae/docpart
 * - /cp/platform-erp/ → ecomae only (ECOM AE company)
 * - Client hostname /cp/shop/finance/erp → that tenant's storefront DB (e.g. docpart)
 * Call epc_erp_assert_tenant_db_context() before any financial KPI/query on shared ERP hosts.
 */
function epc_erp_assert_platform_db_isolation(PDO $db_link): void
{
	$expectedDb = 'ecomae';
	$linkedDb = '';
	try {
		$linkedDb = strtolower(trim((string) $db_link->query('SELECT DATABASE()')->fetchColumn()));
	} catch (Exception $e) {
		$linkedDb = '';
	}
	global $DP_Config;
	$activeDb = is_object($DP_Config) ? strtolower(trim((string) ($DP_Config->db ?? ''))) : '';
	if ($linkedDb !== '' && $linkedDb !== $expectedDb) {
		epc_erp_isolation_block(
			'Platform ERP must use the ecomae registry database (expected ecomae, got ' . $linkedDb . '). '
			. 'Log out and sign in again at /cp/platform-erp/.'
		);
	}
	if ($activeDb !== '' && $activeDb !== $expectedDb) {
		epc_erp_isolation_block(
			'Platform ERP configuration mismatch (expected ecomae, active ' . $activeDb . '). '
			. 'Log out and sign in again at /cp/platform-erp/.'
		);
	}
	if (!function_exists('epc_portal_is_platform_operator') || !epc_portal_is_platform_operator()) {
		$login = function_exists('epc_platform_erp_login_url') ? epc_platform_erp_login_url() : '/cp/platform-erp/';
		epc_erp_isolation_block(
			'Platform ERP is for ECOM AE operators only. Log in at ' . $login . ' with your Super CP account.'
		);
	}
	if (function_exists('epc_portal_is_shared_erp_cp_session') && epc_portal_is_shared_erp_cp_session()) {
		$login = function_exists('epc_client_erp_login_url') ? epc_client_erp_login_url('asap') : '/cp/client-erp/asap/';
		epc_erp_isolation_block(
			'Client company ERP accounts must use the client ERP login (for example ' . $login . '), not Platform ERP.'
		);
	}
}

/**
 * Recreate PDO from DP_Config after tenant/platform ERP config is applied.
 */
function epc_erp_rebind_db_link(): ?PDO
{
	global $DP_Config, $db_link;
	if (!is_object($DP_Config)) {
		return null;
	}
	unset($GLOBALS['epc_db_link']);
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$db_link->query('SET NAMES utf8;');
		$GLOBALS['epc_db_link'] = $db_link;
		$GLOBALS['db_link'] = $db_link;
		return $db_link;
	} catch (Exception $e) {
		if (function_exists('error_log')) {
			error_log('ERP rebind failed: ' . $e->getMessage());
		}
		return null;
	}
}

/** Guard before financial queries — alias for epc_erp_assert_tenant_db_isolation(). */
function epc_erp_assert_tenant_db_context(PDO $db_link): void
{
	epc_erp_assert_tenant_db_isolation($db_link);
}

function epc_erp_assert_tenant_db_isolation(PDO $db_link): void
{
	if (function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request()) {
		epc_erp_assert_platform_db_isolation($db_link);
		return;
	}
	if (!function_exists('epc_portal_is_platform_hostname') || !epc_portal_is_platform_hostname()) {
		return;
	}
	if (!function_exists('epc_portal_is_cp_request') || !epc_portal_is_cp_request()) {
		return;
	}
	$sharedFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
	if (!is_file($sharedFile)) {
		epc_erp_isolation_block('Shared ERP tenant module missing on this host.');
	}
	require_once $sharedFile;
	if (function_exists('epc_portal_shared_erp_infer_tenant_from_session')) {
		epc_portal_shared_erp_infer_tenant_from_session();
	}
	$row = null;
	if (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
		$row = $GLOBALS['epc_client_erp_tenant_row'] ?? null;
		if (!is_array($row) && function_exists('epc_client_erp_tenant_row')) {
			$row = epc_client_erp_tenant_row();
		}
	}
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
		$demoRow = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
		if (!is_array($demoRow)) {
			epc_erp_isolation_block('ERP-only demo session is invalid. Log out and sign in again at your demo CP URL.');
		}
		$expectedDb = trim((string) ($demoRow['db_name'] ?? ''));
		$linkedDb = '';
		try {
			$linkedDb = strtolower(trim((string) $db_link->query('SELECT DATABASE()')->fetchColumn()));
		} catch (Exception $e) {
			$linkedDb = '';
		}
		$activeDb = is_object($DP_Config) ? strtolower(trim((string) ($DP_Config->db ?? ''))) : '';
		if ($expectedDb !== '' && $linkedDb !== '' && strcasecmp($linkedDb, $expectedDb) !== 0) {
			epc_erp_isolation_block(
				'Demo ERP database mismatch (expected ' . $expectedDb . ', got ' . $linkedDb . '). Log out and sign in again.'
			);
		}
		if ($expectedDb !== '' && $activeDb !== '' && strcasecmp($activeDb, $expectedDb) !== 0) {
			epc_erp_isolation_block('Demo ERP configuration mismatch. Log out and sign in again at your demo CP URL.');
		}
		return;
	}
	if ($row === null && (!function_exists('epc_portal_is_shared_erp_cp_session') || !epc_portal_is_shared_erp_cp_session())) {
		$loginHint = function_exists('epc_client_erp_login_url')
			? epc_client_erp_login_url('asapcustom')
			: '/cp/client-erp/asapcustom/';
		epc_erp_isolation_block(
			'Company ERP session required. Log in at the client ERP URL (for example '
			. $loginHint
			. ') with your company email. Super CP platform operators cannot open tenant ERP here.'
		);
	}
	if ($row === null) {
		$row = epc_portal_shared_erp_active_tenant();
	}
	if ($row === null) {
		epc_erp_isolation_block('ERP tenant session is invalid or expired. Log out, then sign in again with your company CP email.');
	}
	global $DP_Config;
	$activeDb = is_object($DP_Config) ? trim((string) ($DP_Config->db ?? '')) : '';
	$expectedDb = trim((string) ($row['db_name'] ?? ''));
	$tenantLabel = trim((string) ($row['trade_name'] ?? $row['site_key'] ?? 'tenant'));
	if ($expectedDb === '' || in_array($expectedDb, array('docpart', 'ecomae'), true)) {
		epc_erp_isolation_block('Tenant database misconfigured for ' . $tenantLabel . ' — contact platform operator.');
	}
	$linkedDb = '';
	try {
		$linkedDb = strtolower(trim((string) $db_link->query('SELECT DATABASE()')->fetchColumn()));
	} catch (Exception $e) {
		$linkedDb = '';
	}
	if ($linkedDb !== '' && strcasecmp($linkedDb, $expectedDb) !== 0) {
		if (function_exists('error_log')) {
			error_log('ERP PDO isolation breach: expected=' . $expectedDb . ' linked=' . $linkedDb . ' tenant=' . ($row['site_key'] ?? ''));
		}
		epc_erp_isolation_block(
			'Database connection mismatch for ' . $tenantLabel . ' (expected ' . $expectedDb . ', got ' . $linkedDb . '). '
			. 'Log out and sign in again so your session binds to the correct company database.'
		);
	}
	if ($activeDb !== '' && $expectedDb !== '' && strcasecmp($activeDb, $expectedDb) !== 0) {
		if (function_exists('error_log')) {
			error_log('ERP DB isolation breach: expected=' . $expectedDb . ' active=' . $activeDb . ' tenant=' . ($row['site_key'] ?? ''));
		}
		epc_erp_isolation_block(
			'ERP configuration mismatch for ' . $tenantLabel . '. Log out and sign in again at /cp/.'
		);
	}
	if (in_array($linkedDb, array('docpart', 'ecomae'), true)) {
		if (function_exists('error_log')) {
			error_log('ERP blocked platform/storefront DB: ' . $linkedDb . ' tenant=' . ($row['site_key'] ?? ''));
		}
		epc_erp_isolation_block('ERP must use the dedicated tenant database for ' . $tenantLabel . ', not the platform catalog.');
	}
	if (!empty($_GET['epc_db_debug']) && (string) $_GET['epc_db_debug'] === '1' && function_exists('error_log')) {
		error_log('ERP tenant OK: host=' . (epc_portal_host() ?? '') . ' db=' . $activeDb . ' tenant=' . ($row['site_key'] ?? ''));
	}
}
