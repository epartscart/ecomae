<?php
/**
 * Standalone Document Control AJAX endpoint.
 */
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($DP_Config);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';

if (!function_exists('epc_dc_ensure_db_link') || !epc_dc_ensure_db_link()) {
	echo json_encode(array('status' => false, 'message' => 'Database connection failed'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	echo json_encode(array('status' => false, 'message' => 'No action'));
	exit;
}

$epc_dc_backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
if ($epc_dc_backend === '') {
	$epc_dc_backend = 'cp';
}
require $_SERVER['DOCUMENT_ROOT'] . '/' . $epc_dc_backend . '/content/shop/document_control/ajax_document_control.php';
