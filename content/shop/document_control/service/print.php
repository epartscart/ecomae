<?php
/**
 * Document Control — print / preview renderer.
 * /content/shop/document_control/service/print.php?doc=fta_tax_invoice&order_id=123
 */
header('Content-Type: text/html; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	http_response_code(500);
	echo 'Database error';
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
define('_ASTEXE_', 1);

$epcDcPrintOk = DP_User::isAdmin() || DP_User::isBackendGroup();
if (!$epcDcPrintOk) {
	// ERP portal team (ERP-only tenants have no CP admin session).
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
		if (function_exists('epc_erp_user_can_access')) {
			$epcDcPrintOk = epc_erp_user_can_access($db_link);
		}
	} catch (Throwable $e) {
		$epcDcPrintOk = false;
	}
}
if (!$epcDcPrintOk) {
	http_response_code(403);
	echo 'Access denied — sign in to ERP or the control panel.';
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';

$doc = trim((string)($_GET['doc'] ?? 'fta_tax_invoice'));
$order_id = (int)($_GET['order_id'] ?? 0);
$invoice_id = (int)($_GET['invoice_id'] ?? 0);
$preview = !empty($_GET['preview']);

try {
	$extra = array();
	if (!$preview && $invoice_id > 0) {
		$extra['invoice_id'] = $invoice_id;
	}
	echo epc_dc_render_template($db_link, $doc, $preview ? 0 : $order_id, $extra);
} catch (Throwable $e) {
	http_response_code(400);
	echo '<p>' . htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</p>';
}
