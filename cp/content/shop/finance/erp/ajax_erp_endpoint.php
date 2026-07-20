<?php
/**
 * Standalone ERP AJAX endpoint — returns JSON only (no CP HTML wrapper).
 * URL: /cp/content/shop/finance/erp/ajax_erp_endpoint.php
 * Also serves GET document exports (print HTML / XML / JSON) before JSON mode.
 */
$epcErpDocGetAction = (
	($_SERVER['REQUEST_METHOD'] ?? '') === 'GET'
	&& isset($_GET['action'])
	&& in_array((string) $_GET['action'], array('invoice_print', 'invoice_download_json', 'einvoice_download_xml'), true)
);
if (!$epcErpDocGetAction) {
	header('Content-Type: application/json; charset=utf-8');
}

if (ob_get_level()) {
	ob_end_clean();
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($DP_Config);
}

$epc_erp_sec = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_security.php';
if (is_file($epc_erp_sec)) {
	require_once $epc_erp_sec;
	epc_erp_send_security_headers(false);
}

$platformErpRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
if (is_file($platformErpRouter)) {
	require_once $platformErpRouter;
}
$clientErpRouter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_client_erp_router.php';
if (is_file($clientErpRouter)) {
	require_once $clientErpRouter;
}
$ajaxReferer = isset($_SERVER['HTTP_REFERER']) ? (string) $_SERVER['HTTP_REFERER'] : '';
if ($ajaxReferer !== '') {
	$refPath = parse_url($ajaxReferer, PHP_URL_PATH);
	$refQuery = parse_url($ajaxReferer, PHP_URL_QUERY);
	if (is_string($refPath) && $refPath !== '') {
		$_SERVER['REQUEST_URI'] = $refPath . ($refQuery !== null && $refQuery !== '' ? '?' . $refQuery : '');
	}
}
$demoBootstrap = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
if (is_file($demoBootstrap)) {
	require_once $demoBootstrap;
	if (function_exists('epc_portal_demo_parse_cp_path')
		&& function_exists('epc_portal_demo_load_live_row')
		&& function_exists('epc_portal_demo_apply_cp_config')
	) {
		$demoParsed = epc_portal_demo_parse_cp_path();
		if ($demoParsed !== null) {
			$demoRow = epc_portal_demo_load_live_row($demoParsed['site_key']);
			if (is_array($demoRow)) {
				$GLOBALS['epc_demo_cp_context'] = true;
				$GLOBALS['epc_demo_cp_site_key'] = $demoParsed['site_key'];
				$GLOBALS['epc_demo_cp_tenant_row'] = $demoRow;
				epc_portal_demo_apply_cp_config($DP_Config, $demoRow, $demoParsed['site_key']);
			}
		}
	}
}
if (is_file($platformErpRouter) && function_exists('epc_platform_erp_bootstrap')) {
	epc_platform_erp_bootstrap();
}
if (is_file($clientErpRouter) && function_exists('epc_client_erp_bootstrap')) {
	epc_client_erp_bootstrap();
}
$sharedErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
if (is_file($sharedErpFile)) {
	require_once $sharedErpFile;
	if (function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request()) {
		if (function_exists('epc_platform_erp_apply_config')) {
			epc_platform_erp_apply_config($DP_Config);
		}
	} elseif (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()) {
		$tenantRow = $GLOBALS['epc_client_erp_tenant_row'] ?? null;
		if (!is_array($tenantRow) && function_exists('epc_client_erp_tenant_row')) {
			$tenantRow = epc_client_erp_tenant_row();
		}
		if (is_array($tenantRow) && function_exists('epc_portal_shared_erp_apply_row_config')) {
			epc_portal_shared_erp_apply_row_config($DP_Config, $tenantRow);
		} elseif (function_exists('epc_portal_shared_erp_apply_config')) {
			epc_portal_shared_erp_apply_config($DP_Config);
		}
	}
}

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => 'Database connection failed'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
if (function_exists('epc_erp_assert_tenant_db_context')) {
	epc_erp_assert_tenant_db_context($db_link);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

if (!epc_erp_user_can_access($db_link)) {
	if (!empty($epcErpDocGetAction)) {
		http_response_code(403);
		header('Content-Type: text/plain; charset=utf-8');
		echo 'Access denied';
		exit;
	}
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

if (!empty($epcErpDocGetAction)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_invoices.php';
	epc_erp_handle_document_export_get($db_link);
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	echo json_encode(array('status' => false, 'message' => 'No action'));
	exit;
}

require __DIR__ . '/ajax_erp.php';
