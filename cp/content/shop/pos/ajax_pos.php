<?php
/**
 * POS AJAX actions.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pos/epc_pos_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

header('Content-Type: application/json; charset=utf-8');

if (!isset($db_link) || !($db_link instanceof PDO)) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'));
	exit;
}

if (!DP_User::isAdmin() && !DP_User::isBackendGroup()) {
	echo json_encode(array('status' => false, 'message' => 'Access denied'));
	exit;
}

$action = (string) ($_POST['action'] ?? '');
$adminId = 0;
if (class_exists('DP_User')) {
	$adminId = (int) (DP_User::getAdminId() ?? 0);
}

try {
	switch ($action) {
		case 'search_products':
			$q = trim((string) ($_POST['q'] ?? ''));
			$products = epc_pos_search_products($db_link, $q, 30);
			echo json_encode(array('status' => true, 'products' => $products));
			break;

		case 'search_customers':
			$q = trim((string) ($_POST['q'] ?? ''));
			$customers = epc_pos_search_customers($db_link, $q, 15);
			echo json_encode(array('status' => true, 'customers' => $customers));
			break;

		case 'calc_cart':
			$linesRaw = $_POST['lines'] ?? '[]';
			if (is_string($linesRaw)) {
				$linesRaw = json_decode($linesRaw, true) ?: array();
			}
			$lines = epc_pos_parse_cart_lines(is_array($linesRaw) ? $linesRaw : array());
			$userId = (int) ($_POST['customer_user_id'] ?? 0);
			$contactId = (int) ($_POST['contact_id'] ?? 0);
			if ($userId <= 0 && $contactId <= 0) {
				$userId = epc_pos_ensure_walkin_user($db_link);
			}
			$totals = epc_pos_calc_cart_totals($db_link, $lines, $userId, $contactId);
			echo json_encode(array('status' => true, 'totals' => $totals));
			break;

		case 'open_session':
			$float = (float) ($_POST['opening_float'] ?? 0);
			$sess = epc_pos_open_session($db_link, $float, $adminId);
			echo json_encode(array('status' => true, 'session' => $sess));
			break;

		case 'close_session':
			$sid = (int) ($_POST['session_id'] ?? 0);
			$cash = (float) ($_POST['closing_cash'] ?? 0);
			$notes = trim((string) ($_POST['notes'] ?? ''));
			$result = epc_pos_close_session($db_link, $sid, $cash, $notes);
			echo json_encode(array('status' => true, 'result' => $result));
			break;

		case 'complete_sale':
			$payload = $_POST;
			if (!empty($payload['lines']) && is_string($payload['lines'])) {
				$payload['lines'] = json_decode($payload['lines'], true) ?: array();
			}
			$result = epc_pos_complete_sale($db_link, $payload, $adminId);
			echo json_encode(array_merge(array('status' => true), $result));
			break;

		case 'session_status':
			$sess = epc_pos_get_open_session($db_link);
			echo json_encode(array('status' => true, 'session' => $sess, 'stats' => epc_pos_dashboard_stats($db_link)));
			break;

		case 'save_settings':
			epc_pos_save_settings($db_link, $_POST);
			echo json_encode(array('status' => true));
			break;

		default:
			echo json_encode(array('status' => false, 'message' => 'Unknown action'));
	}
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}
