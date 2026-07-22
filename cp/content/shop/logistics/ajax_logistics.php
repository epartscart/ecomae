<?php
/**
 * CP AJAX — logistics & carrier actions.
 * Callable via include from logistics_carriers.php OR direct POST to this file.
 */
$epcAjaxDirect = !defined('_ASTEXE_');
if ($epcAjaxDirect) {
	define('_ASTEXE_', 1);
	header('Content-Type: application/json; charset=utf-8');
	$root = isset($_SERVER['DOCUMENT_ROOT']) ? (string)$_SERVER['DOCUMENT_ROOT'] : '';
	if ($root === '' || !is_file($root . '/config.php')) {
		$root = dirname(__DIR__, 3);
		$_SERVER['DOCUMENT_ROOT'] = $root;
	}
	require_once $root . '/config.php';
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
	try {
		$dbHost = trim((string)($DP_Config->host ?? ''));
		if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
			$dbHost = '127.0.0.1';
		}
		$db_link = new PDO(
			'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
			(string)$DP_Config->user,
			(string)$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		echo json_encode(array('status' => false, 'message' => 'Database unavailable'));
		exit;
	}
} else {
	header('Content-Type: application/json; charset=utf-8');
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/logistics/epc_logistics_helpers.php';

if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	echo json_encode(array('status' => false, 'message' => 'Database link missing'));
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
if ($epcAjaxDirect) {
	exit;
}
