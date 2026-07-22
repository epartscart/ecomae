<?php
/**
 * CP — POS Terminal (touch-friendly register).
 * URL: /cp/shop/pos/terminal
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pos/epc_pos_helpers.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_pos.php';
	exit;
}

if ($_SERVER['REQUEST_METHOD'] === 'GET' && isset($_GET['action']) && $_GET['action'] === 'receipt') {
	if (!isset($db_link) || !($db_link instanceof PDO)) {
		http_response_code(500);
		exit('DB unavailable');
	}
	$saleId = (int) ($_GET['sale_id'] ?? 0);
	header('Content-Type: text/html; charset=utf-8');
	echo epc_pos_receipt_html($db_link, $saleId);
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database connection failed.</div>';
		return;
	}
}

epc_pos_ensure_schema($db_link);
$settings = epc_pos_get_settings($db_link);
if (empty($settings['pos_enabled'])) {
	echo '<div class="alert alert-warning">POS is disabled for this tenant. Enable it in Super CP → POS overview or contact your administrator.</div>';
	return;
}

$backend = (string) $DP_Config->backend_dir;
$posUrl = '/' . $backend . '/shop/pos/terminal';
$ajaxUrl = '/' . $backend . '/content/shop/pos/ajax_pos_endpoint.php';
$erpUrl = '/' . $backend . '/shop/finance/erp?epc_erp_shell=1&tab=sales_orders';
$whUrl = '/' . $backend . '/shop/finance/erp?epc_erp_shell=1&tab=inventory';
$settingsUrl = '/' . $backend . '/control/portal/epc_pos_tenant_manage';
$csrf = isset($user_session['csrf_guard_key']) ? (string) $user_session['csrf_guard_key'] : '';
$stats = epc_pos_dashboard_stats($db_link);
$openSession = $stats['open_session'];
$taxCtx = epc_tax_toolkit_resolve($db_link, epc_pos_ensure_walkin_user($db_link));

$currency = 'AED';
$countryCode = strtoupper((string) ($taxCtx['country_code'] ?? 'AE'));
$worldPath = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit_world.php';
if (is_file($worldPath)) {
	require_once $worldPath;
	if (function_exists('epc_tax_toolkit_world_rate_overrides')) {
		$world = epc_tax_toolkit_world_rate_overrides();
		if (!empty($world[$countryCode]['currency'])) {
			$currency = (string) $world[$countryCode]['currency'];
		}
	}
}

$warehouses = array();
$warehouseId = (int) ($settings['default_warehouse_id'] ?? 0);
$warehouseName = 'Default warehouse';
try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
	if (function_exists('epc_erp_inventory_ensure_schema')) {
		epc_erp_inventory_ensure_schema($db_link);
	}
	if (function_exists('epc_erp_inventory_list_warehouses')) {
		$warehouses = epc_erp_inventory_list_warehouses($db_link) ?: array();
	}
	if ($warehouseId <= 0 && !empty($warehouses[0]['id'])) {
		$warehouseId = (int) $warehouses[0]['id'];
	}
	foreach ($warehouses as $wh) {
		if ((int) ($wh['id'] ?? 0) === $warehouseId) {
			$warehouseName = (string) (($wh['name'] ?? '') !== '' ? $wh['name'] : ($wh['code'] ?? 'Warehouse'));
			break;
		}
	}
} catch (Throwable $e) {
	$warehouses = array();
}

require_once __DIR__ . '/epc_pos_terminal_markup.php';
epc_pos_terminal_render_markup(array(
	'stats' => $stats,
	'open_session' => $openSession,
	'tax_ctx' => $taxCtx,
	'settings' => $settings,
	'warehouses' => $warehouses,
	'warehouse_id' => $warehouseId,
	'warehouse_name' => $warehouseName,
	'currency' => $currency,
	'country_code' => $countryCode,
	'ajax_url' => $ajaxUrl,
	'pos_url' => $posUrl,
	'csrf' => $csrf,
	'erp_url' => $erpUrl,
	'warehouse_url' => $whUrl,
	'settings_url' => $settingsUrl,
));
