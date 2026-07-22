<?php
/**
 * CP AJAX — marketplace channel actions.
 * Direct URL: /{backend}/content/shop/channels/ajax_channels.php
 * Also includable from channels_main.php (POST on the CMS page).
 */
if (!headers_sent()) {
	header('Content-Type: application/json; charset=utf-8');
}

$epcAjaxDirect = !isset($db_link) || !($db_link instanceof PDO);

if ($epcAjaxDirect) {
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$GLOBALS['db_link'] = $db_link;
	} catch (Throwable $e) {
		echo json_encode(array('status' => false, 'message' => 'Database unavailable'));
		exit;
	}
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/channels/epc_channel_helpers.php';

$action = isset($_POST['action']) ? (string)$_POST['action'] : '';
try {
	switch ($action) {
		case 'seed_channels':
			epc_channel_seed_defaults($db_link);
			$n = count(epc_channel_marketplaces_catalog());
			epc_channel_log($db_link, 'seed', 'Worldwide marketplace partners seeded (' . $n . ')', 'system');
			echo json_encode(array(
				'status' => true,
				'message' => 'Seeded ' . $n . ' worldwide marketplace partners',
				'seeded' => $n,
			));
			break;

		case 'seed_sample':
			epc_channel_seed_sample_data($db_link);
			echo json_encode(array(
				'status' => true,
				'message' => 'Sample marketplace data loaded (Amazon, eBay, noon)',
			));
			break;

		case 'toggle_channel':
			$code = preg_replace('/[^a-z0-9_]/', '', strtolower((string)($_POST['channel_code'] ?? $_POST['code'] ?? '')));
			if ($code === '') {
				echo json_encode(array('status' => false, 'message' => 'Missing channel code'));
				break;
			}
			$row = $db_link->prepare('SELECT `id`, `active` FROM `epc_marketplace_channels` WHERE `code` = ? LIMIT 1');
			$row->execute(array($code));
			$cur = $row->fetch(PDO::FETCH_ASSOC);
			if (!$cur) {
				echo json_encode(array('status' => false, 'message' => 'Channel not found — sync partners first'));
				break;
			}
			$forced = isset($_POST['enabled']) ? (string)$_POST['enabled'] : '';
			if ($forced === '0' || $forced === '1') {
				$next = (int)$forced;
			} else {
				$next = ((int)$cur['active'] === 1) ? 0 : 1;
			}
			$db_link->prepare('UPDATE `epc_marketplace_channels` SET `active` = ? WHERE `code` = ?')->execute(array($next, $code));
			epc_channel_log($db_link, 'channel', ($next ? 'Enabled' : 'Disabled') . ' channel ' . $code, $code);
			echo json_encode(array(
				'status' => true,
				'message' => ($next ? 'Enabled' : 'Disabled') . ' ' . $code,
				'active' => $next,
			));
			break;

		case 'sync_inventory':
			$ch = isset($_POST['channel']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string)$_POST['channel'])) : 'amazon';
			if ($ch === '') {
				$ch = 'amazon';
			}
			$res = epc_channel_sync_inventory_demo($db_link, $ch);
			echo json_encode(array(
				'status' => true,
				'message' => 'Pushed ' . (int)$res['skus_pushed'] . ' SKUs to ' . $ch,
				'updated' => (int)$res['skus_pushed'],
				'data' => $res,
			));
			break;

		case 'import_order':
			$id = isset($_POST['marketplace_order_id']) ? (int)$_POST['marketplace_order_id'] : 0;
			$res = epc_channel_import_order_demo($db_link, $id);
			echo json_encode(array(
				'status' => true,
				'message' => isset($res['message']) ? $res['message'] : 'Import complete',
				'external_id' => isset($res['external_order_id']) ? $res['external_order_id'] : '',
				'order_id' => isset($res['shop_order_id']) ? (int)$res['shop_order_id'] : 0,
				'data' => $res,
			));
			break;

		default:
			echo json_encode(array('status' => false, 'message' => 'Unknown action'));
	}
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}

die();
