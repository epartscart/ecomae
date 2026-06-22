<?php
/*
 * Docpart Frontend Loader
 */

require_once __DIR__ . '/epc-ecomae-legacy-path-guard.php';

if (!defined('_ASTEXE_')) {
    define('_ASTEXE_', 1);
}

if (PHP_SAPI !== 'cli') {
	@ini_set('display_errors', '0');
	@ini_set('display_startup_errors', '0');
	// Prevent PHP workers from hanging forever under load.
	// Workers that exceed 30s are killed, freeing the pool for other requests (BOS, CP, marketing).
	@set_time_limit(30);
}

// Server overload guard — serve lightweight "busy" page when load is critical.
require_once __DIR__ . '/epc-server-guard.php';
if (function_exists('epc_server_guard_check') && epc_server_guard_check()) {
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_static_serve.php';
if (function_exists('epc_static_serve_maybe_exit')) {
	epc_static_serve_maybe_exit();
}

// Always load config first — class DP_Config; instance is required before redirect logic (dp_core also instantiates later).
require_once $_SERVER["DOCUMENT_ROOT"] . "/config.php";

// Public REST API v1 — exit before marketing/ERP/storefront bootstrap.
$__epcApiPath = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
if (is_string($__epcApiPath) && preg_match('#^/epc-api/v1(?:/|$)#', $__epcApiPath)) {
	require $_SERVER['DOCUMENT_ROOT'] . '/epc-api-v1.php';
	exit;
}
if (is_string($__epcApiPath) && preg_match('#^/api/v1/catalog(?:\.php)?$#', $__epcApiPath)) {
	require $_SERVER['DOCUMENT_ROOT'] . '/api/v1/catalog.php';
	exit;
}
if (is_string($__epcApiPath) && preg_match('#^/api/v1/price/lookup(?:\.php)?$#', $__epcApiPath)) {
	require $_SERVER['DOCUMENT_ROOT'] . '/api/v1/price/lookup.php';
	exit;
}
$DP_Config = new DP_Config();
$epcTenantDbFile = $_SERVER['DOCUMENT_ROOT'] . '/config.tenant-db.php';
if (is_file($epcTenantDbFile)) {
	$epc_tenant_db = null;
	require $epcTenantDbFile;
	if (isset($epc_tenant_db) && is_array($epc_tenant_db)) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_db[$epcTk]) && property_exists($DP_Config, $epcTk)) {
				$DP_Config->$epcTk = $epc_tenant_db[$epcTk];
			}
		}
	}
}

// www.ecomae.com GET / — marketing HTML without portal/MySQL (avoids origin hang → Cloudflare 522).
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_router.php';
if (($_SERVER['REQUEST_METHOD'] ?? '') === 'GET' && function_exists('epc_ecomae_is_marketing_platform_host') && epc_ecomae_is_marketing_platform_host()) {
	$__epcLegacyPath = epc_ecomae_platform_normalize_path($_SERVER['REQUEST_URI'] ?? '/');
	if (preg_match('#^/(en|ru|ar)/cp(?:/|$)#i', $__epcLegacyPath)) {
		$__epcCpStrip = preg_replace('#^/(en|ru|ar)#i', '', $__epcLegacyPath);
		if (!is_string($__epcCpStrip) || $__epcCpStrip === '') {
			$__epcCpStrip = '/cp/';
		} elseif ($__epcCpStrip[0] !== '/') {
			$__epcCpStrip = '/' . $__epcCpStrip;
		}
		$__epcCpQs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== '' ? ('?' . $_SERVER['QUERY_STRING']) : '';
		header('Location: ' . $__epcCpStrip . $__epcCpQs, true, 301);
		exit;
	}
	$__epcLegacyHit = preg_match('#^/(en|ru|ar)/.+#i', $__epcLegacyPath)
		|| preg_match('#^/(akciya|aktsiya|akcia|promotion|promotions)(?:/|$)#i', $__epcLegacyPath);
	if ($__epcLegacyHit
		&& !preg_match('#^/platform(?:/|$)#', $__epcLegacyPath)
		&& !preg_match('#^/cp(?:/|$)#', $__epcLegacyPath)
		&& !preg_match('#^/(en|ru|ar)/cp(?:/|$)#i', $__epcLegacyPath)
		&& !preg_match('#^/demo(?:/|$)#', $__epcLegacyPath)
		&& !headers_sent()) {
		header('Location: /', true, 301);
		header('X-Robots-Tag: noindex');
		exit;
	}
}
if (function_exists('epc_ecomae_marketing_serve_seo_file') && epc_ecomae_marketing_serve_seo_file()) {
	exit;
}
if (function_exists('epc_is_ecomae_marketing_root_request') && epc_is_ecomae_marketing_root_request()) {
	epc_render_ecomae_marketing_home_and_exit();
}

require_once $_SERVER["DOCUMENT_ROOT"] . "/content/general_pages/epc_portal.php";
epc_portal_apply_config($DP_Config);
$GLOBALS['epc_portal_config_applied'] = true;
$__epcTenantControl = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
if (is_file($__epcTenantControl)) {
	require_once $__epcTenantControl;
	if (function_exists('epc_portal_tenant_control_maybe_block')) {
		epc_portal_tenant_control_maybe_block();
	}
}

// Demo storefront: /demo/{site_key}/ → isolated tenant DB on www.ecomae.com
$__epcDemoBootstrap = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
if (is_file($__epcDemoBootstrap)) {
	require_once $__epcDemoBootstrap;
	if (function_exists('epc_portal_demo_try_bootstrap_storefront')) {
		epc_portal_demo_try_bootstrap_storefront($DP_Config);
	}
}
if (!empty($GLOBALS['epc_demo_storefront_context'])) {
	$GLOBALS['DP_Config'] = $DP_Config;
	epc_portal_apply_config($DP_Config);
	if (!empty($GLOBALS['epc_demo_storefront_site_key'])) {
		$DP_Config->domain_path = 'https://www.ecomae.com/demo/' . preg_replace('/[^a-z0-9_]/', '', (string) $GLOBALS['epc_demo_storefront_site_key']) . '/';
	}
}

epc_ecomae_platform_init_route();
if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST') {
	epc_ecomae_platform_process_demo_post();
}

// Platform marketing (www.ecomae.com) — MUST exit before ERP router (erp_only would 302 / → /erp).
if (function_exists('epc_ecomae_platform_try_exit_standalone')) {
	epc_ecomae_platform_try_exit_standalone();
}
if (function_exists('epc_ecomae_marketing_redirect_legacy_cms_path') && epc_ecomae_marketing_redirect_legacy_cms_path()) {
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_portal_router.php';
if (epc_erp_portal_try_route()) {
	exit;
}
if (epc_erp_portal_maybe_redirect_home()) {
	exit;
}
if (epc_erp_portal_block_storefront_path()) {
	exit;
}
if (function_exists('epc_erp_portal_block_consultancy_commerce_path') && epc_erp_portal_block_consultancy_commerce_path()) {
	exit;
}

// cp.ecomae.com — send / to Super CP login (tenant hub after auth).
if (function_exists('epc_portal_host') && epc_portal_host() === 'cp.ecomae.com'
	&& ($_SERVER['REQUEST_METHOD'] ?? '') === 'GET' && isset($_SERVER['REQUEST_URI'])) {
	$epcCpHostPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	if ($epcCpHostPath === '/' || $epcCpHostPath === '' || $epcCpHostPath === false) {
		header('Location: /' . trim((string) $DP_Config->backend_dir, '/') . '/', true, 302);
		exit;
	}
}

// Nginx try_files sends /cp/* subpaths here — hand off to backend CP (cp/index.php).
$epcBackendDir = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($epcBackendDir !== '' && isset($_SERVER['REQUEST_URI'])) {
	$epcPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	if (!is_string($epcPath) || $epcPath === '') {
		$epcPath = '/';
	}
	$epcCpBase = '/' . $epcBackendDir;
	// Storefront multilang uses /{lang}/… but CP is always /cp/ (lang_cp cookie). Redirect mistaken locale CP URLs.
	if (preg_match('#^/(en|ru|ar)/' . preg_quote($epcBackendDir, '#') . '(?:/|$)#i', $epcPath)) {
		$epcCpStripped = preg_replace('#^/(en|ru|ar)#i', '', $epcPath);
		if (!is_string($epcCpStripped) || $epcCpStripped === '') {
			$epcCpStripped = $epcCpBase . '/';
		} elseif ($epcCpStripped[0] !== '/') {
			$epcCpStripped = '/' . $epcCpStripped;
		}
		$epcCpQs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== '' ? ('?' . $_SERVER['QUERY_STRING']) : '';
		header('Location: ' . $epcCpStripped . $epcCpQs, true, 301);
		exit;
	}
	if ($epcPath === $epcCpBase || $epcPath === $epcCpBase . '/'
		|| (strlen($epcPath) > strlen($epcCpBase) && strpos($epcPath, $epcCpBase . '/') === 0)) {
		$epcCpAjaxDirect = array(
			'/' . $epcBackendDir . '/content/control/portal/ajax_portal.php',
			'/' . $epcBackendDir . '/content/control/portal/ajax_auto_price.php',
			'/' . $epcBackendDir . '/content/control/portal/ajax_platform_governance.php',
			'/' . $epcBackendDir . '/content/control/portal/ajax_visual_page_editor.php',
			'/' . $epcBackendDir . '/content/control/portal/ajax_integrations.php',
			'/' . $epcBackendDir . '/content/control/portal/ajax_marketing_broadcast.php',
		);
		foreach ($epcCpAjaxDirect as $ajaxRel) {
			if ($epcPath === $ajaxRel && is_file($_SERVER['DOCUMENT_ROOT'] . $ajaxRel)) {
				require $_SERVER['DOCUMENT_ROOT'] . $ajaxRel;
				exit;
			}
		}
		$demoCpBootstrap = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
		if (is_file($demoCpBootstrap)) {
			require_once $demoCpBootstrap;
			if (function_exists('epc_portal_demo_cp_maybe_redirect_bare_path')) {
				epc_portal_demo_cp_maybe_redirect_bare_path();
			}
			if (function_exists('epc_portal_demo_try_bootstrap_cp')) {
				epc_portal_demo_try_bootstrap_cp($DP_Config);
			}
		}
		if (!empty($GLOBALS['epc_demo_cp_context'])) {
			$GLOBALS['DP_Config'] = $DP_Config;
			epc_portal_apply_config($DP_Config);
			if (function_exists('epc_portal_demo_reapply_cp_config')) {
				epc_portal_demo_reapply_cp_config($DP_Config);
			}
		}
		$platformErpRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
		if (is_file($platformErpRouter)) {
			require_once $platformErpRouter;
			if (function_exists('epc_platform_erp_bootstrap')) {
				epc_platform_erp_bootstrap();
			}
		}
		$clientErpRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
		if (is_file($clientErpRouter)) {
			require_once $clientErpRouter;
			if (function_exists('epc_client_erp_maybe_handle_bare_path')) {
				epc_client_erp_maybe_handle_bare_path();
			}
			if (function_exists('epc_client_erp_bootstrap')) {
				epc_client_erp_bootstrap();
			}
		}
		if (is_file($platformErpRouter)) {
			if (function_exists('epc_cp_logout_if_requested')) {
				epc_cp_logout_if_requested();
			}
			if (function_exists('epc_platform_erp_block_client_session')) {
				epc_platform_erp_block_client_session();
			}
		}
		if (is_file($clientErpRouter) && function_exists('epc_cp_logout_if_requested')) {
			epc_cp_logout_if_requested();
		}
		require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $epcBackendDir . '/epc_cp_auth_gate.php';
		epc_cp_auth_gate_run();
		$cpEntry = $_SERVER['DOCUMENT_ROOT'] . '/' . $epcBackendDir . '/index.php';
		if (is_file($cpEntry)) {
			require $cpEntry;
			exit;
		}
	}
}

if (function_exists('epc_portal_home_mode') && epc_portal_home_mode() === 'platform'
	&& ($_SERVER['REQUEST_METHOD'] ?? '') === 'GET' && isset($_SERVER['REQUEST_URI'])) {
	$langPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	if ($langPath !== false && $langPath !== null && preg_match('#^/(en|ru|ar)(/|$)#i', $langPath)) {
		$rest = preg_replace('#^/(en|ru|ar)#i', '', $langPath);
		if ($rest === '' || $rest === '/') {
			header('Location: ' . rtrim($DP_Config->domain_path, '/') . '/', true, 301);
			exit;
		}
		if (preg_match('#^/platform(?:/|$)#', $rest)) {
			header('Location: ' . rtrim($DP_Config->domain_path, '/') . $rest, true, 301);
			exit;
		}
		$epcPlatformBackend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
		if ($epcPlatformBackend === '') {
			$epcPlatformBackend = 'cp';
		}
		if (preg_match('#^/' . preg_quote($epcPlatformBackend, '#') . '(?:/|$)#', $rest)) {
			$epcPlatformCpQs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== '' ? ('?' . $_SERVER['QUERY_STRING']) : '';
			header('Location: ' . $rest . $epcPlatformCpQs, true, 301);
			exit;
		}
		header('Location: ' . rtrim($DP_Config->domain_path, '/') . '/', true, 301);
		header('X-Robots-Tag: noindex');
		exit;
	}
}

if (epc_portal_is_super_cp_host() && isset($_SERVER['REQUEST_URI'])) {
	$path = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	if ($path === '/' || $path === '') {
		header('Location: /' . $DP_Config->backend_dir . '/shop/tenant_hub/tenant_hub', true, 302);
		exit;
	}
}

// Full-page output cache — serve cached HTML for anonymous visitors (skip full PHP render)
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_page_cache.php';
if (epc_page_cache_try_serve()) {
	exit;
}

// Frontend mode flag
$isFrontMode = 1;

// Platform marketing host serves / as homepage; client storefronts redirect / → /en/.
$epcSkipMultilangRoot = epc_portal_is_super_cp_host()
	|| (function_exists('epc_portal_home_mode') && epc_portal_home_mode() === 'platform')
	|| (function_exists('epc_portal_storefront_enabled') && !epc_portal_storefront_enabled());

// With multilang on, send bare domain root to /{default_language}/ (e.g. /en/) so the storefront matches prefixed URLs and default DB language.
if (!$epcSkipMultilangRoot && (string) $DP_Config->multilang === '1' && (int) $isFrontMode === 1 && isset($_SERVER['REQUEST_METHOD']) && $_SERVER['REQUEST_METHOD'] === 'GET') {
	$uriPath = parse_url(isset($_SERVER['REQUEST_URI']) ? $_SERVER['REQUEST_URI'] : '/', PHP_URL_PATH);
	if ($uriPath === false || $uriPath === null) {
		$uriPath = '/';
	}
	$uriPath = '/' . trim(str_replace('\\', '/', $uriPath), '/');
	if ($uriPath === '//') {
		$uriPath = '/';
	}
	$dpPath = parse_url($DP_Config->domain_path, PHP_URL_PATH);
	$dpPath = ($dpPath === false || $dpPath === null || $dpPath === '' || $dpPath === '/') ? '' : rtrim(str_replace('\\', '/', $dpPath), '/');
	$rel = $uriPath;
	if ($dpPath !== '' && strpos($uriPath, $dpPath) === 0) {
		$rel = substr($uriPath, strlen($dpPath));
		if ($rel === '' || (isset($rel[0]) && $rel[0] !== '/')) {
			$rel = '/' . $rel;
		}
	}
	$rel = rtrim($rel, '/');
	if ($rel === '') {
		$rel = '/';
	}
	if ($rel === '/' || $rel === '/index.php') {
		$defLang = trim((string) ($DP_Config->frontend_ui_lang ?? ''));
		if ($defLang === '') {
			try {
				$pdo = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
				$pdo->query('SET NAMES utf8');
				$st = $pdo->prepare('SELECT `lang_code` FROM `lang_languages` WHERE `active` = 1 AND `is_default` = 1 LIMIT 1');
				$st->execute();
				$defLang = (string) $st->fetchColumn();
			} catch (Exception $e) {
				$defLang = '';
			}
		}
		if ($defLang !== '') {
			$qs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== '' ? ('?' . $_SERVER['QUERY_STRING']) : '';
			$base = rtrim($DP_Config->domain_path, '/') . '/';
			header('Location: ' . $base . strtolower($defLang) . '/' . $qs, true, 302);
			exit;
		}
	}
}

// ePartsCart: never expose APAI presentation catalog tree on storefront (legacy left-panel catalog only).
$__epcHost = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
$__epcPath = parse_url((string) ($_SERVER['REQUEST_URI'] ?? '/'), PHP_URL_PATH);
if (strpos($__epcHost, 'epartscart') !== false && is_string($__epcPath)) {
	$__epcPathLower = strtolower($__epcPath);
	$__epcIsApaiCatalog = (strpos($__epcPathLower, '/apai-') !== false || strpos($__epcPathLower, '/apai_') !== false)
		&& !preg_match('#---[a-z0-9][a-z0-9\-]*$#', $__epcPathLower);
	if ($__epcIsApaiCatalog) {
		$__epcLang = trim((string) ($DP_Config->frontend_ui_lang ?? 'en'), '/');
		if ($__epcLang === '') {
			$__epcLang = 'en';
		}
		header('Location: ' . rtrim((string) $DP_Config->domain_path, '/') . '/' . $__epcLang . '/', true, 301);
		exit;
	}
}

// Platform marketing fallback for /platform/* before dp_core.
if (function_exists('epc_ecomae_platform_try_exit_standalone')) {
	epc_ecomae_platform_try_exit_standalone();
}

// Start page cache capture for anonymous storefront visitors (5 min TTL)
if (function_exists('epc_page_cache_start_capture')) {
	epc_page_cache_start_capture(300);
}

// Load core system
if (function_exists('epc_portal_demo_start_output_buffer')) {
	epc_portal_demo_start_output_buffer();
}
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_helper.php";
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_content.php";
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_module.php";
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_template.php";
require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_core.php";

// Optional: automatically load requested page content
if (isset($_GET['page'])) {
    $page = $_GET['page'];
    include $_SERVER["DOCUMENT_ROOT"] . "/content/" . $page . ".php";
}
?>