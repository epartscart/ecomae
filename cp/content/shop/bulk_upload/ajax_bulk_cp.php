<?php
/**
 * CP AJAX — bulk upload hub actions.
 * Direct URL: /{backend}/content/shop/bulk_upload/ajax_bulk_cp.php
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

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/bulk_upload/epc_bulk_helpers.php';

$admin_session = DP_User::getAdminSession();
$admin_id = 0;
if (is_array($admin_session) && !empty($admin_session['user_id'])) {
	$admin_id = (int)$admin_session['user_id'];
} elseif (method_exists('DP_User', 'getAdminId')) {
	$admin_id = (int)DP_User::getAdminId();
}

$action = isset($_POST['action']) ? (string)$_POST['action'] : '';

try {
	switch ($action) {
		case 'dashboard':
			echo json_encode(array('status' => true, 'dashboard' => epc_bulk_dashboard($db_link)));
			break;

		case 'list_history':
			$filters = array(
				'user_id' => isset($_POST['user_id']) ? (int)$_POST['user_id'] : 0,
				'source' => isset($_POST['source']) ? (string)$_POST['source'] : '',
				'unreviewed' => !empty($_POST['unreviewed']),
				'q' => isset($_POST['q']) ? (string)$_POST['q'] : '',
			);
			$rows = epc_bulk_list_history($db_link, $filters, 50, 0);
			echo json_encode(array('status' => true, 'rows' => $rows));
			break;

		case 'get_upload':
			$id = isset($_POST['upload_id']) ? (int)$_POST['upload_id'] : 0;
			$row = epc_bulk_get_upload($db_link, $id);
			if (!$row) {
				echo json_encode(array('status' => false, 'message' => 'Upload not found'));
				break;
			}
			echo json_encode(array('status' => true, 'upload' => $row));
			break;

		case 'search_customers':
			$q = isset($_POST['q']) ? (string)$_POST['q'] : '';
			$found = epc_bulk_search_customers($db_link, $q, 20);
			foreach ($found as &$c) {
				$c['label'] = epc_bulk_customer_label($db_link, (int)$c['user_id']);
				$c['group_id'] = epc_bulk_customer_group_id($db_link, (int)$c['user_id']);
			}
			unset($c);
			echo json_encode(array('status' => true, 'customers' => $found));
			break;

		case 'process_upload':
			$customer_id = isset($_POST['customer_user_id']) ? (int)$_POST['customer_user_id'] : 0;
			if ($customer_id <= 0) {
				echo json_encode(array('status' => false, 'message' => 'Select a customer first'));
				break;
			}
			$group_id = isset($_POST['group_id']) ? (int)$_POST['group_id'] : 0;
			if ($group_id <= 0) {
				$group_id = epc_bulk_customer_group_id($db_link, $customer_id);
			}
			if ($group_id <= 0 && !empty($_POST['admin_group_id'])) {
				$group_id = (int)$_POST['admin_group_id'];
			}
			if ($group_id <= 0) {
				echo json_encode(array('status' => false, 'message' => 'Customer has no price group — pick a price profile'));
				break;
			}
			$priority = (isset($_POST['priority']) && $_POST['priority'] === 'delivery') ? 'delivery' : 'price';
			if (empty($_FILES['bulk_file']) || $_FILES['bulk_file']['error'] !== UPLOAD_ERR_OK) {
				echo json_encode(array('status' => false, 'message' => 'Upload file is required'));
				break;
			}
			$items = epc_bulk_read_input_lines($_FILES['bulk_file']['tmp_name'], $_FILES['bulk_file']['name']);
			if (empty($items)) {
				echo json_encode(array('status' => false, 'message' => 'No valid rows found'));
				break;
			}
			$processed = epc_bulk_process_items($db_link, $DP_Config, $group_id, $items, $priority, false);
			$upload_id = epc_bulk_save_history(
				$db_link,
				$customer_id,
				true,
				$group_id,
				$_FILES['bulk_file']['name'],
				$priority,
				$processed['summary'],
				$processed['rows'],
				$processed['csv'],
				'cp'
			);
			echo json_encode(array(
				'status' => true,
				'upload_id' => $upload_id,
				'rows' => $processed['rows'],
				'summary' => $processed['summary'],
				'csv' => $processed['csv'],
				'customer_label' => epc_bulk_customer_label($db_link, $customer_id),
			));
			break;

		case 'mark_reviewed':
			$id = isset($_POST['upload_id']) ? (int)$_POST['upload_id'] : 0;
			$notes = isset($_POST['notes']) ? (string)$_POST['notes'] : '';
			$ok = epc_bulk_mark_reviewed($db_link, $id, $admin_id, $notes);
			echo json_encode(array('status' => $ok, 'message' => $ok ? 'Marked reviewed' : 'Update failed'));
			break;

		case 'add_to_cart':
		case 'create_shop_quote':
		case 'create_crm_quote':
			$id = isset($_POST['upload_id']) ? (int)$_POST['upload_id'] : 0;
			$upload = epc_bulk_get_upload($db_link, $id);
			if (!$upload) {
				echo json_encode(array('status' => false, 'message' => 'Upload not found'));
				break;
			}
			$customer_id = (int)$upload['user_id'];
			if (!empty($_POST['customer_user_id'])) {
				$customer_id = (int)$_POST['customer_user_id'];
			}
			$indexes = null;
			if (isset($_POST['indexes']) && $_POST['indexes'] !== '') {
				$decoded = json_decode((string)$_POST['indexes'], true);
				if (is_array($decoded)) {
					$indexes = array_map('intval', $decoded);
				}
			}
			$products = epc_bulk_collect_product_objects($upload['rows'], $indexes);
			if (empty($products)) {
				echo json_encode(array('status' => false, 'message' => 'No available lines selected'));
				break;
			}

			if ($action === 'add_to_cart') {
				$res = epc_bulk_add_to_customer_cart($db_link, $customer_id, $products);
				$db_link->prepare(
					'UPDATE `epc_bulk_upload_history` SET `cart_added_count` = `cart_added_count` + ?, `updated_at` = NOW() WHERE `id` = ?'
				)->execute(array((int)$res['added'], $id));
				epc_bulk_mark_reviewed($db_link, $id, $admin_id, 'Added ' . (int)$res['added'] . ' lines to customer cart');
				echo json_encode(array(
					'status' => true,
					'message' => 'Added ' . (int)$res['added'] . ' to cart' . ((int)$res['skipped'] ? ' (' . (int)$res['skipped'] . ' skipped)' : ''),
					'data' => $res,
				));
				break;
			}

			if ($action === 'create_shop_quote') {
				$res = epc_bulk_create_shop_quote(
					$db_link,
					$customer_id,
					$products,
					'From bulk upload #' . $id,
					'quoted'
				);
				$db_link->prepare(
					'UPDATE `epc_bulk_upload_history` SET `shop_quote_id` = ?, `updated_at` = NOW() WHERE `id` = ?'
				)->execute(array((int)$res['quote_id'], $id));
				epc_bulk_mark_reviewed($db_link, $id, $admin_id, 'Shop quote #' . (int)$res['quote_id']);
				$backend = isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp';
				echo json_encode(array(
					'status' => true,
					'message' => 'Shop quote #' . (int)$res['quote_id'] . ' created (' . (int)$res['lines'] . ' lines)',
					'quote_id' => (int)$res['quote_id'],
					'quote_url' => '/' . $backend . '/shop/quote-requests?quote_id=' . (int)$res['quote_id'],
				));
				break;
			}

			$res = epc_bulk_create_crm_quote(
				$db_link,
				$customer_id,
				$products,
				'From CP bulk upload #' . $id
			);
			$db_link->prepare(
				'UPDATE `epc_bulk_upload_history` SET `crm_quote_id` = ?, `updated_at` = NOW() WHERE `id` = ?'
			)->execute(array((int)$res['crm_quote_id'], $id));
			epc_bulk_mark_reviewed($db_link, $id, $admin_id, 'ERP CRM quote #' . (int)$res['crm_quote_id']);
			$backend = isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp';
			echo json_encode(array(
				'status' => true,
				'message' => 'ERP quote #' . (int)$res['crm_quote_id'] . ' created (' . (int)$res['lines'] . ' lines)',
				'crm_quote_id' => (int)$res['crm_quote_id'],
				'crm_url' => '/' . $backend . '/shop/finance/erp?tab=crm&crm_tab=quotes',
			));
			break;

		default:
			echo json_encode(array('status' => false, 'message' => 'Unknown action'));
	}
} catch (Throwable $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage()));
}

die();
