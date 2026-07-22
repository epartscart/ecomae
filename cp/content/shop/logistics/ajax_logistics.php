<?php
/**
 * CP AJAX — logistics & carrier actions.
 */
defined('_ASTEXE_') or die('No access');

header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/logistics/epc_logistics_helpers.php';

if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

$action = isset($_POST['action']) ? (string)$_POST['action'] : '';
try {
	switch ($action) {
		case 'seed_sample':
			epc_logistics_seed_sample_data($db_link);
			echo json_encode(array('status' => true, 'message' => 'Sample carrier shipment loaded'));
			break;
		case 'seed_carriers':
			epc_logistics_seed_defaults($db_link);
			$n = count(epc_channel_carriers_catalog());
			epc_channel_log($db_link, 'seed', 'Worldwide carrier partners seeded (' . $n . ')', 'logistics');
			echo json_encode(array('status' => true, 'message' => 'Seeded ' . $n . ' worldwide carrier partners'));
			break;
		case 'toggle_carrier':
			$code = preg_replace('/[^a-z0-9_]/', '', strtolower((string)($_POST['carrier_code'] ?? '')));
			if ($code === '') {
				echo json_encode(array('status' => false, 'message' => 'Missing carrier code'));
				break;
			}
			$row = $db_link->prepare('SELECT `id`, `active` FROM `epc_carrier_accounts` WHERE `code` = ? LIMIT 1');
			$row->execute(array($code));
			$cur = $row->fetch(PDO::FETCH_ASSOC);
			if (!$cur) {
				echo json_encode(array('status' => false, 'message' => 'Carrier not found — seed partners first'));
				break;
			}
			$next = ((int)$cur['active'] === 1) ? 0 : 1;
			$db_link->prepare('UPDATE `epc_carrier_accounts` SET `active` = ? WHERE `code` = ?')->execute(array($next, $code));
			epc_channel_log($db_link, 'carrier', ($next ? 'Enabled' : 'Disabled') . ' carrier ' . $code, $code);
			echo json_encode(array('status' => true, 'message' => ($next ? 'Enabled' : 'Disabled') . ' ' . $code, 'active' => $next));
			break;
		case 'create_shipment':
			$oid = isset($_POST['order_id']) ? (int)$_POST['order_id'] : 0;
			$carrier = isset($_POST['carrier_code']) ? (string)$_POST['carrier_code'] : 'dhl';
			$weight = isset($_POST['weight_kg']) ? (float)$_POST['weight_kg'] : 1.5;
			$svc = 'EXPRESS';
			$catalog = epc_channel_carriers_catalog();
			if (isset($catalog[$carrier]['services'])) {
				$keys = array_keys($catalog[$carrier]['services']);
				$svc = $keys[0];
			}
			$res = epc_channel_create_shipment_demo($db_link, $oid, $carrier, $svc, $weight);
			echo json_encode(array('status' => true, 'message' => 'Label created: ' . $res['tracking_number'], 'data' => $res));
			break;
		default:
			echo json_encode(array('status' => false, 'message' => 'Unknown action'));
	}
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}
