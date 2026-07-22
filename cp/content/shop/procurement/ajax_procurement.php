<?php
/**
 * Procurement — AJAX / POST actions.
 */
defined('_ASTEXE_') or die('No access');

header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/procurement/epc_procurement_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

function epc_proc_json($ok, $message, $extra = array())
{
	echo json_encode(array_merge(array('status' => (bool)$ok, 'message' => (string)$message), $extra));
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	epc_proc_json(false, 'No database');
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	epc_proc_json(false, 'No action');
}

$action = (string)$_POST['action'];

try {
	epc_procurement_ensure_schema($db_link);

	switch ($action) {
		case 'create_supplier':
			$id = epc_procurement_create_supplier($db_link, $_POST);
			epc_proc_json(true, 'Supplier created', array('id' => $id));

		case 'update_supplier':
			epc_procurement_update_supplier($db_link, (int)($_POST['supplier_id'] ?? 0), $_POST);
			epc_proc_json(true, 'Supplier profile saved');

		case 'sync_suppliers':
			$r = epc_procurement_sync_suppliers_from_warehouses($db_link);
			epc_proc_json(
				true,
				'Warehouses synced: ' . (int)$r['created'] . ' new supplier(s), ' . (int)$r['updated'] . ' name/code refresh(es)',
				$r
			);

		case 'create_purchase':
			$id = epc_erp_create_purchase($db_link, $_POST);
			$extra = array('id' => $id);
			$bcMsg = '';
			if (!empty($_POST['receive_inventory'])) {
				$pst = $db_link->prepare('SELECT `id`, `inv_receipt_posted`, `invoice_number` FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1');
				$pst->execute(array($id));
				$prow = $pst->fetch(PDO::FETCH_ASSOC) ?: array();
				if (!empty($prow['inv_receipt_posted'])) {
					$bcFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
					if (is_file($bcFile)) {
						require_once $bcFile;
						$flash = epc_bc_bos_grn_flash_for_purchase($prow);
						$bcMsg = (string) ($flash['message'] ?? '');
						if (!empty($flash['extra']) && is_array($flash['extra'])) {
							$extra = array_merge($extra, $flash['extra']);
						}
					}
				}
			}
			epc_proc_json(true, 'Purchase bill recorded' . $bcMsg, $extra);

		case 'supplier_payment':
			$id = epc_erp_supplier_payment($db_link, $_POST);
			epc_proc_json(true, 'Supplier payment recorded', array('cash_entry_id' => $id));

		case 'record_advance':
			$id = epc_procurement_record_advance($db_link, $_POST);
			epc_proc_json(true, 'Advance payment recorded', array('id' => $id));

		case 'purchase_from_order':
			$r = epc_erp_purchase_from_order(
				$db_link,
				(int)($_POST['order_id'] ?? 0),
				(int)($_POST['supplier_id'] ?? 0)
			);
			$msg = 'Purchase #' . (int)$r['purchase_id'] . ' from order #' . (int)$r['order_id'];
			if (!empty($r['inventory_line_count'])) {
				$msg .= '; ' . (int)$r['inventory_line_count'] . ' stock line(s)';
				$msg .= !empty($r['inventory_receipt_posted']) ? ' posted to inventory' : ' (warehouse link required for stock receipt)';
			}
			if (!empty($r['inventory_receipt_posted'])) {
				$bcFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
				if (is_file($bcFile)) {
					require_once $bcFile;
					$flashPurchase = array(
						'id' => (int) ($r['purchase_id'] ?? 0),
						'invoice_number' => (string) ($r['invoice_number'] ?? ''),
						'inventory_receipt_posted' => 1,
						'inv_receipt_posted' => 1,
					);
					if ($flashPurchase['invoice_number'] === '' && !empty($r['purchase_id'])) {
						$pst = $db_link->prepare('SELECT `invoice_number` FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1');
						$pst->execute(array((int) $r['purchase_id']));
						$flashPurchase['invoice_number'] = (string) ($pst->fetchColumn() ?: '');
					}
					$flash = epc_bc_bos_grn_flash_for_purchase($flashPurchase);
					$msg .= (string) ($flash['message'] ?? '');
					if (!empty($flash['extra']) && is_array($flash['extra'])) {
						$r = array_merge($r, $flash['extra']);
					}
				}
			}
			epc_proc_json(true, $msg, $r);

		case 'supplier_settlement':
			$r = epc_erp_supplier_settlement($db_link, $_POST);
			epc_proc_json(true, 'Supplier adjustment posted', $r);

		case 'purchase_adjustment':
			$r = epc_erp_purchase_adjustment($db_link, $_POST);
			epc_proc_json(true, 'Purchase adjusted', $r);

		default:
			epc_proc_json(false, 'Unknown action');
	}
} catch (Throwable $e) {
	epc_proc_json(false, $e->getMessage());
}
