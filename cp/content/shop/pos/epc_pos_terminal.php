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
$csrf = isset($user_session['csrf_guard_key']) ? (string) $user_session['csrf_guard_key'] : '';
$stats = epc_pos_dashboard_stats($db_link);
$openSession = $stats['open_session'];
$taxCtx = epc_tax_toolkit_resolve($db_link, epc_pos_ensure_walkin_user($db_link));

require_once __DIR__ . '/epc_pos_terminal_markup.php';
epc_pos_terminal_render_markup(
	$stats,
	$openSession,
	$taxCtx,
	$settings,
	$ajaxUrl,
	$posUrl,
	$csrf,
	$erpUrl
);
