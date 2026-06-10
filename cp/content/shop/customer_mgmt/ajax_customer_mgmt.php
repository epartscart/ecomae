<?php
/**
 * Customer management — AJAX actions.
 */
defined('_ASTEXE_') or die('No access');

header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/customer_mgmt/epc_customer_mgmt_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

function epc_cm_json($ok, $message, $extra = array())
{
	echo json_encode(array_merge(array('status' => (bool)$ok, 'message' => (string)$message), $extra));
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	epc_cm_json(false, 'No database');
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	epc_cm_json(false, 'No action');
}

$action = (string)$_POST['action'];

try {
	epc_einvoice_ensure_schema($db_link);

	switch ($action) {
		case 'save_customer':
			epc_cm_save_customer_profile($db_link, $_POST);
			epc_cm_json(true, 'Customer profile saved');

		case 'customer_advance':
			$r = epc_erp_customer_settlement($db_link, array_merge($_POST, array(
				'entry_kind' => 'advance',
				'direction' => 'credit',
			)));
			epc_cm_json(true, 'Customer advance recorded', $r);

		case 'einvoice_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
			$orderId = (int)($_POST['order_id'] ?? 0);
			$flags = array();
			foreach (array_keys(epc_einvoice_transaction_flags()) as $fk) {
				if (!empty($_POST['flag_' . $fk])) {
					$flags[$fk] = 1;
				}
			}
			$built = epc_einvoice_build_from_order($db_link, $orderId, array('transaction_flags' => $flags));
			$adminId = class_exists('DP_User') ? (int)DP_User::getAdminId() : 0;
			$docId = epc_einvoice_save_document($db_link, $built, $adminId);
			$doc = epc_einvoice_get_document($db_link, $docId);
			$cfg = $GLOBALS['DP_Config'] ?? new DP_Config();
			$redirect = '/' . $cfg->backend_dir . '/shop/finance/erp?tab=einvoice&einv_section=view&einv_doc=' . $docId;
			epc_cm_json(true, $doc['validation_ok'] ? 'E-invoice generated' : 'E-invoice draft — fix validation',
				array('document_id' => $docId, 'redirect' => $redirect));

		default:
			epc_cm_json(false, 'Unknown action');
	}
} catch (Throwable $e) {
	epc_cm_json(false, $e->getMessage());
}
