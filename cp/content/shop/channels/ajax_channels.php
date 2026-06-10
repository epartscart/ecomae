<?php
/**
 * CP AJAX — marketplace channel actions only.
 */
defined('_ASTEXE_') or die('No access');

header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/channels/epc_channel_helpers.php';

if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

$action = isset($_POST['action']) ? (string)$_POST['action'] : '';
try {
	switch ($action) {
		case 'seed_sample':
			epc_channel_seed_sample_data($db_link);
			echo json_encode(array('status' => true, 'message' => 'Sample marketplace data loaded (Amazon, eBay)'));
			break;
		case 'sync_inventory':
			$ch = isset($_POST['channel']) ? (string)$_POST['channel'] : 'amazon';
			$res = epc_channel_sync_inventory_demo($db_link, $ch);
			echo json_encode(array('status' => true, 'message' => 'Pushed ' . $res['skus_pushed'] . ' SKUs to ' . $ch, 'data' => $res));
			break;
		case 'import_order':
			$id = isset($_POST['marketplace_order_id']) ? (int)$_POST['marketplace_order_id'] : 0;
			$res = epc_channel_import_order_demo($db_link, $id);
			echo json_encode(array('status' => true, 'message' => $res['message'], 'data' => $res));
			break;
		default:
			echo json_encode(array('status' => false, 'message' => 'Unknown action'));
	}
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}
