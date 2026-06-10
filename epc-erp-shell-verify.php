<?php
/**
 * Regression guard — ERP CP shell sidebar accordion + shell navigation.
 * GET: token=epartscart-deploy-2026
 * Optional: host=www.epartscart.com|www.electronicae.com
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	echo json_encode(array('status' => false, 'message' => 'Forbidden'));
	exit;
}

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_cp_shell.php';

$host = preg_replace('/[^a-z0-9._-]/', '', strtolower((string) ($_GET['host'] ?? 'local')));
if ($host === '') {
	$host = 'local';
}

$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__), '/\\');
$backend = 'cp';
$cfg = new DP_Config();
if (is_object($cfg) && !empty($cfg->backend_dir)) {
	$backend = trim((string) $cfg->backend_dir, '/');
}

$out = array(
	'status' => true,
	'host' => $host,
	'checks' => array(),
);

$paths = array(
	'erp_shell_php' => $root . '/content/shop/finance/epc_erp_cp_shell.php',
	'dp_core' => $root . '/core/dp_core.php',
	'erp_desktop_tpl' => $root . '/' . $backend . '/templates/bootstrap_admin/erp_desktop.php',
	'erp_nav_js' => $root . '/' . $backend . '/js/epc_erp_shell_nav.js',
	'erp_nav_areas' => $root . '/' . $backend . '/content/shop/finance/erp/erp_nav_areas.php',
	'erp_main_page' => $root . '/' . $backend . '/content/shop/finance/erp/erp_main_page.php',
	'erp_main' => $root . '/' . $backend . '/content/shop/finance/erp/erp_main.php',
	'erp_ui' => $root . '/content/shop/finance/epc_erp_ui.php',
	'page_assets' => $root . '/content/general_pages/epc_cp_page_assets.php',
);

foreach ($paths as $key => $path) {
	$out['checks'][$key] = array(
		'path' => $path,
		'exists' => is_file($path),
	);
}

$desktopSrc = is_file($paths['erp_desktop_tpl']) ? (string) file_get_contents($paths['erp_desktop_tpl']) : '';
$dpCoreSrc = is_file($paths['dp_core']) ? (string) file_get_contents($paths['dp_core']) : '';
$shellPhpSrc = is_file($paths['erp_shell_php']) ? (string) file_get_contents($paths['erp_shell_php']) : '';
$navJsSrc = is_file($paths['erp_nav_js']) ? (string) file_get_contents($paths['erp_nav_js']) : '';
$pageAssetsSrc = is_file($paths['page_assets']) ? (string) file_get_contents($paths['page_assets']) : '';
$mainPageSrc = is_file($paths['erp_main_page']) ? (string) file_get_contents($paths['erp_main_page']) : '';
$mainSrc = is_file($paths['erp_main']) ? (string) file_get_contents($paths['erp_main']) : '';
$uiSrc = is_file($paths['erp_ui']) ? (string) file_get_contents($paths['erp_ui']) : '';

$out['checks']['desktop_head_nav_js'] = stripos($desktopSrc, 'epc_erp_shell_nav_script_tag') !== false
	|| stripos($desktopSrc, 'epc_erp_shell_nav.js') !== false;
$out['checks']['desktop_no_inline_css'] = stripos($desktopSrc, 'epc_erp_shell_inline_style_block') === false
	&& stripos($desktopSrc, 'epc_cp_shell_inline_style_block') === false;
$out['checks']['dp_core_shell_redirect'] = stripos($dpCoreSrc, 'epc_erp_cp_shell_maybe_redirect') !== false;
$out['checks']['shell_redirect_fn'] = stripos($shellPhpSrc, 'function epc_erp_cp_shell_maybe_redirect') !== false
	&& stripos($shellPhpSrc, 'function epc_erp_cp_redirect_url') !== false;
$out['checks']['desktop_no_inline_accordion'] = stripos($desktopSrc, 'epc_erp_sidebar_accordion_script') === false;
$out['checks']['nav_js_accordion_init'] = stripos($navJsSrc, 'epcErpMenuSectionsInit') !== false;
$out['checks']['nav_js_shell_guard'] = stripos($navJsSrc, 'epc_erp_shell=1') !== false
	&& stripos($navJsSrc, 'data-epc-erp-shell') !== false;
$out['checks']['nav_js_force_assign'] = stripos($navJsSrc, 'window.location.assign(fixed)') !== false
	&& stripos($navJsSrc, 'e.preventDefault()') !== false;
$out['checks']['page_assets_erp_route'] = stripos($pageAssetsSrc, 'shop/finance/erp') !== false
	&& (stripos($pageAssetsSrc, 'epc_erp_shell_nav_js_href') !== false
		|| stripos($pageAssetsSrc, 'epc_erp_shell_nav.js') !== false);
$quickPos = stripos($mainSrc, 'epc_erp_render_dashboard_quick_actions');
$financePos = stripos($mainSrc, 'Finance overview');
$out['checks']['dashboard_quick_before_finance'] = ($quickPos !== false && $financePos !== false && $quickPos < $financePos);
$out['checks']['main_page_no_inline_shell_redirect'] = stripos($mainPageSrc, "json_encode(\$shellUrl)") === false
	&& stripos($mainPageSrc, '<script>location.replace') === false
	&& stripos($mainPageSrc, 'epc_erp_cp_shell_page_redirect') !== false;
$out['checks']['erp_ui_quick_actions'] = stripos($uiSrc, 'epc_erp_render_dashboard_quick_actions') !== false
	&& stripos($uiSrc, 'Quick actions') !== false;
$accessSrc = is_file($root . '/content/shop/finance/epc_erp_access.php')
	? (string) file_get_contents($root . '/content/shop/finance/epc_erp_access.php')
	: '';
$out['checks']['shell_route_prefix_fn'] = stripos($shellPhpSrc, 'function epc_erp_cp_route_prefix') !== false
	&& stripos($shellPhpSrc, 'function epc_erp_cp_shell_erp_module_url') !== false;
$out['checks']['access_uses_route_prefix'] = stripos($accessSrc, 'epc_erp_cp_shell_erp_module_url') !== false
	&& stripos($accessSrc, 'epc_erp_cp_route_prefix') !== false;
$out['checks']['nav_js_platform_prefix'] = stripos($navJsSrc, 'platform-erp') !== false
	&& stripos($navJsSrc, 'fixErpHrefRoutePrefix') !== false;
$out['checks']['desktop_platform_guide_url'] = stripos($desktopSrc, 'epc_erp_cp_shell_url_with_subpath') !== false
	&& stripos($desktopSrc, 'epc_erp_cp_shell_launcher_url') !== false;
$out['checks']['shell_asset_proxies'] = stripos($shellPhpSrc, 'epc_erp_shell_use_asset_proxies') !== false
	&& is_file($root . '/content/shop/finance/epc_erp_ui_css.php')
	&& is_file($root . '/content/general_pages/epc_erp_shell_nav_js.php');
$out['checks']['desktop_uses_asset_proxies'] = stripos($desktopSrc, 'epc_erp_shell_asset_href') !== false;

$html = '';
$renderErr = null;
$platformHtml = '';
$platformRenderErr = null;
if (is_file($paths['erp_nav_areas'])) {
	require_once $paths['erp_shell_php'];
	$helpers = $root . '/content/shop/finance/epc_erp_helpers.php';
	if (is_file($helpers)) {
		require_once $helpers;
	}
	$GLOBALS['epc_erp_shell_mode'] = true;
	$_GET['epc_erp_shell'] = '1';
	define('_ASTEXE_', 1);
	$allowed = array('dashboard', 'workflow', 'crm', 'sales_orders', 'revenue', 'cash_bank', 'gl');
	$erpUrl = '/' . $backend . '/shop/finance/erp';
	ob_start();
	try {
		require_once $paths['erp_nav_areas'];
		epc_erp_render_sidebar_nav($erpUrl, 'sales', 'dashboard', date('Y-m-01'), date('Y-m-d'), $allowed);
	} catch (Throwable $e) {
		$renderErr = $e->getMessage();
	}
	$html = (string) ob_get_clean();

	$platformRouter = $root . '/content/general_pages/epc_platform_erp_router.php';
	if (is_file($platformRouter)) {
		require_once $platformRouter;
		$GLOBALS['epc_platform_erp_context'] = true;
		$platformErpUrl = function_exists('epc_platform_erp_path_prefix')
			? epc_platform_erp_path_prefix() . 'shop/finance/erp'
			: '/' . $backend . '/platform-erp/shop/finance/erp';
		ob_start();
		try {
			epc_erp_render_sidebar_nav($platformErpUrl, 'sales', 'dashboard', date('Y-m-01'), date('Y-m-d'), $allowed);
		} catch (Throwable $e) {
			$platformRenderErr = $e->getMessage();
		}
		$platformHtml = (string) ob_get_clean();
		unset($GLOBALS['epc_platform_erp_context']);
	}
}

$out['checks']['render'] = array(
	'error' => $renderErr,
	'bytes' => strlen($html),
	'has_sidebar' => stripos($html, 'epc_erp_sidebar') !== false || stripos($html, 'epc-erp-sidebar') !== false,
	'has_data_shell_links' => stripos($html, 'data-epc-erp-shell="1"') !== false,
	'sample_href_has_shell' => (bool) preg_match('#/shop/finance/erp[^"\']*epc_erp_shell=1#', $html),
	'has_sales_group' => stripos($html, 'data-area="sales"') !== false,
);
$out['checks']['platform_render'] = array(
	'error' => $platformRenderErr,
	'bytes' => strlen($platformHtml),
	'has_platform_prefix' => stripos($platformHtml, '/platform-erp/shop/finance/erp') !== false,
	'sample_href_has_shell' => (bool) preg_match('#/platform-erp/shop/finance/erp[^"\']*epc_erp_shell=1#', $platformHtml),
);

$failures = array();
foreach ($out['checks'] as $key => $val) {
	if ($key === 'render') {
		if (empty($val['has_sidebar'])) {
			$failures[] = 'render.has_sidebar';
		}
		if (empty($val['has_data_shell_links'])) {
			$failures[] = 'render.has_data_shell_links';
		}
		if (empty($val['sample_href_has_shell'])) {
			$failures[] = 'render.sample_href_has_shell';
		}
		continue;
	}
	if ($key === 'platform_render') {
		if (empty($val['has_platform_prefix'])) {
			$failures[] = 'platform_render.has_platform_prefix';
		}
		if (empty($val['sample_href_has_shell'])) {
			$failures[] = 'platform_render.sample_href_has_shell';
		}
		continue;
	}
	if (is_array($val) && array_key_exists('exists', $val) && empty($val['exists'])) {
		$failures[] = $key . '.exists';
		continue;
	}
	if (is_bool($val) && $val === false) {
		$failures[] = (string) $key;
	}
}

if ($failures !== array()) {
	$out['status'] = false;
	$out['failures'] = $failures;
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
