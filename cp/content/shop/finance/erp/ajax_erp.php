<?php
/**
 * ERP module — AJAX / POST actions.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($GLOBALS['DP_Config'])) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$GLOBALS['DP_Config'] = new DP_Config();
}
global $DP_Config;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_dimensions.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

header('Content-Type: application/json; charset=utf-8');

function epc_erp_json($ok, $message, $extra = array())
{
	echo json_encode(array_merge(array('status' => (bool)$ok, 'message' => (string)$message), $extra));
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	epc_erp_json(false, 'No database');
}

if (!epc_erp_user_can_access($db_link)) {
	epc_erp_json(false, 'Access denied');
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST' || empty($_POST['action'])) {
	epc_erp_json(false, 'No action');
}

$action = (string)$_POST['action'];

try {
	epc_erp_full_ensure_schema($db_link);

	switch ($action) {
		case 'create_supplier':
			$id = epc_erp_create_supplier($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'vendor', (int) $id, $_POST);
			epc_erp_json(true, 'Supplier created', array('id' => $id));

		case 'sync_suppliers':
			$n = epc_erp_sync_suppliers_from_storages($db_link);
			epc_erp_json(true, 'Synced ' . $n . ' supplier(s) from warehouses', array('created' => $n));

		case 'create_purchase':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$invMsg = '';
			if (!empty($_POST['receive_inventory']) || !empty($_POST['inventory_lines']) || !empty($_POST['inventory_csv'])) {
				$invMsg = ' (inventory receipt will post with invoice)';
			}
			$id = epc_erp_create_purchase($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'purchase', (int) $id, $_POST);
			$extra = array('id' => $id);
			if (!empty($_POST['receive_inventory'])) {
				$pst = $db_link->prepare('SELECT `inv_receipt_posted` FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1');
				$pst->execute(array($id));
				$extra['inv_receipt_posted'] = (int) $pst->fetchColumn();
			}
			epc_erp_json(true, 'Purchase invoice recorded' . $invMsg, $extra);

		case 'supplier_payment':
			$id = epc_erp_supplier_payment($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'cash_entry', (int) $id, $_POST);
			epc_erp_json(true, 'Supplier payment recorded', array('cash_entry_id' => $id));

		case 'cash_entry':
			$id = epc_erp_cash_entry($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'cash_entry', (int) $id, $_POST);
			epc_erp_json(true, 'Cash/bank entry saved', array('id' => $id));

		case 'create_account':
			$id = epc_erp_create_cash_account($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'cash_account', (int) $id, $_POST);
			epc_erp_json(true, 'Account created', array('id' => $id));

		case 'purchase_from_order':
			$r = epc_erp_purchase_from_order(
				$db_link,
				(int)($_POST['order_id'] ?? 0),
				(int)($_POST['supplier_id'] ?? 0)
			);
			$msg = 'Purchase #' . (int)$r['purchase_id'] . ' created from order #' . (int)$r['order_id'];
			if (!empty($r['inventory_line_count'])) {
				$msg .= '; inventory: ' . (int)$r['inventory_line_count'] . ' line(s)';
				$msg .= !empty($r['inventory_receipt_posted']) ? ' received' : ' (receipt pending — check warehouse link)';
			}
			epc_erp_json(true, $msg, $r);

		case 'dashboard':
			$from = !empty($_POST['date_from']) ? strtotime($_POST['date_from'] . ' 00:00:00') : 0;
			$to = !empty($_POST['date_to']) ? strtotime($_POST['date_to'] . ' 23:59:59') : 0;
			epc_erp_json(true, 'OK', array('data' => epc_erp_dashboard($db_link, $from, $to)));

		case 'create_coa':
			$id = epc_erp_gl_create_coa($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'coa_account', (int) $id, $_POST);
			epc_erp_json(true, 'COA account created', array('id' => $id));

		case 'gl_manual_entry':
			$id = epc_erp_gl_manual_entry($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'gl_entry', (int) $id, $_POST);
			epc_erp_json(true, 'GL journal posted', array('journal_id' => $id));

		case 'gl_post_sales':
			$from = !empty($_POST['date_from']) ? strtotime($_POST['date_from'] . ' 00:00:00') : strtotime(date('Y-m-01'));
			$to = !empty($_POST['date_to']) ? strtotime($_POST['date_to'] . ' 23:59:59') : time();
			$n = epc_erp_gl_post_sales_orders($db_link, $from, $to);
			epc_erp_json(true, 'Posted ' . $n . ' sales journal(s) to GL', array('posted' => $n));

		case 'gl_sync_unposted':
			$n = epc_erp_gl_sync_unposted($db_link);
			epc_erp_json(true, 'Synced ' . $n . ' sub-ledger entry(ies) to GL', array('synced' => $n));

		case 'customer_settlement':
			$r = epc_erp_customer_settlement($db_link, $_POST);
			epc_erp_json(true, 'Customer adjustment/settlement posted', $r);

		case 'supplier_settlement':
			$r = epc_erp_supplier_settlement($db_link, $_POST);
			epc_erp_json(true, 'Supplier adjustment/settlement posted', $r);

		case 'purchase_adjustment':
			$r = epc_erp_purchase_adjustment($db_link, $_POST);
			epc_erp_json(true, 'Purchase adjusted', $r);

		case 'order_settlement':
			$r = epc_erp_order_revenue_settlement($db_link, $_POST);
			epc_erp_json(true, 'Order revenue settlement posted', $r);

		case 'workflow_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
			$tid = (int)($_POST['task_id'] ?? 0);
			$st = (string)($_POST['status'] ?? 'done');
			epc_erp_workflow_update_status($db_link, $tid, $st);
			epc_erp_json(true, 'Workflow task updated', array('task_id' => $tid, 'status' => $st));

		case 'workflow_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
			$tid = epc_erp_workflow_create($db_link, $_POST, epc_erp_admin_id());
			epc_erp_json(true, 'Workflow task created', array('task_id' => $tid));

		case 'marketing_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
			$cid = epc_erp_marketing_create($db_link, $_POST);
			epc_erp_json(true, 'Campaign created', array('id' => $cid));

		case 'payroll_generate':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_payroll.php';
			$label = trim((string)($_POST['period_label'] ?? date('Y-m')));
			if (!preg_match('/^\d{4}-\d{2}$/', $label)) {
				throw new Exception('Invalid period (use YYYY-MM)');
			}
			$start = strtotime($label . '-01 00:00:00');
			$end = strtotime(date('Y-m-t 23:59:59', $start));
			$rid = epc_erp_payroll_generate_run($db_link, $label, $start, $end, epc_erp_admin_id());
			epc_erp_json(true, 'Payroll generated for ' . $label, array('run_id' => $rid));

		case 'payroll_approve':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_payroll.php';
			$rid = epc_erp_payroll_approve_run($db_link, (int)($_POST['run_id'] ?? 0));
			epc_erp_json(true, 'Payroll run approved', array('run_id' => $rid));

		case 'payroll_pay':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_payroll.php';
			$res = epc_erp_payroll_pay_run($db_link, (int)($_POST['run_id'] ?? 0), (int)($_POST['cash_account_id'] ?? 0));
			epc_erp_json(true, 'Salaries paid — ' . number_format($res['total_net'], 2) . ' AED', $res);

		case 'payroll_update_days':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_payroll.php';
			$calc = epc_erp_payroll_update_line_days($db_link, (int)($_POST['line_id'] ?? 0), (float)($_POST['days_worked'] ?? 30));
			epc_erp_json(true, 'Days updated — net ' . number_format($calc['net_pay'], 2) . ' AED', array('calc' => $calc));

		case 'hr_update_days':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_payroll.php';
			epc_erp_payroll_ensure_schema($db_link);
			$profileId = (int)($_POST['staff_profile_id'] ?? 0);
			$days = max(0, round((float)($_POST['days_worked'] ?? 30), 1));
			$db_link->prepare('UPDATE `epc_erp_hr_records` SET `days_worked` = ?, `time_updated` = ? WHERE `staff_profile_id` = ?')
				->execute(array($days, time(), $profileId));
			epc_erp_json(true, 'Days worked saved for next payroll run', array('days_worked' => $days));

		case 'uae_tax_fta_fetch':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
			$force = !empty($_POST['force']);
			$payload = epc_uae_fta_fetch_legislation_updates($db_link, $force);
			epc_erp_json((bool)($payload['status'] ?? $payload['ok'] ?? false), (string)($payload['message'] ?? 'Done'), $payload);

		case 'uae_tax_legislation_regen_summaries':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
			$fetchPdf = !empty($_POST['fetch_pdf']);
			$result = epc_uae_tax_legislation_backfill_summaries($db_link, $fetchPdf);
			epc_erp_json((bool)($result['status'] ?? $result['ok'] ?? false), (string)($result['message'] ?? 'Summaries regenerated'), $result);

		case 'uae_tax_legislation_ask':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
			$question = trim((string)($_POST['question'] ?? ''));
			$result = epc_uae_tax_legislation_ask($db_link, $question);
			epc_erp_json((bool)($result['ok'] ?? false), (string)($result['message'] ?? 'Answer from legislation library'), $result);

		case 'uae_tax_save_ct_adjustments':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
			$df = strtotime((string)($_POST['date_from'] ?? ''));
			$dt = strtotime((string)($_POST['date_to'] ?? ''));
			if (!$df || !$dt) {
				epc_erp_json(false, 'Invalid period dates');
			}
			$defs = epc_uae_ct_adjustment_field_defs();
			$amounts = array();
			foreach (array_keys($defs) as $k) {
				$amounts[$k] = (float)($_POST['ct_' . $k] ?? 0);
			}
			epc_uae_ct_save_adjustments($db_link, $df, $dt, $amounts);
			epc_erp_json(true, 'Corporate Tax adjustments saved for this period');

		case 'einvoice_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
			$orderId = (int)($_POST['order_id'] ?? 0);
			$flags = array();
			if (!empty($_POST['transaction_flags'])) {
				$flags = json_decode((string)$_POST['transaction_flags'], true) ?: array();
			}
			foreach (array_keys(epc_einvoice_transaction_flags()) as $fk) {
				if (!empty($_POST['flag_' . $fk])) {
					$flags[$fk] = 1;
				}
			}
			$built = epc_einvoice_build_from_order($db_link, $orderId, array('transaction_flags' => $flags));
			$adminId = class_exists('DP_User') ? (int)DP_User::getAdminId() : 0;
			$docId = epc_einvoice_save_document($db_link, $built, $adminId);
			$doc = epc_einvoice_get_document($db_link, $docId);
			$adjMsg = '';
			if ((float)($doc['advance_vat_credit'] ?? 0) > 0) {
				$adjMsg = ' Advance VAT credited: ' . number_format((float)$doc['advance_vat_credit'], 2) . ' AED.';
			}
			$cfg = $GLOBALS['DP_Config'] ?? new DP_Config();
			$redirect = epc_erp_cp_redirect_url('/' . $cfg->backend_dir . '/shop/finance/erp?tab=einvoice&einv_section=view&einv_doc=' . $docId);
			epc_erp_json(true, ($doc['validation_ok'] ? 'E-invoice generated and validated' : 'E-invoice saved as draft — fix validation errors') . $adjMsg,
				array('document_id' => $docId, 'redirect' => $redirect));

		case 'einvoice_save_seller':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_vat.php';
			$country = epc_uae_vat_normalize_country((string)($_POST['seller_country_code'] ?? 'AE'));
			if ($country !== 'AE') {
				epc_erp_json(false, 'Seller country must be AE for UAE FTA e-invoicing');
			}
			$trn = preg_replace('/\D/', '', (string)($_POST['seller_trn'] ?? ''));
			if (!epc_uae_company_trn_valid($trn)) {
				epc_erp_json(false, 'Seller TRN must be exactly 15 digits (FTA)');
			}
			$vatRegistered = !empty($_POST['company_vat_registered']) && (string)$_POST['company_vat_registered'] !== '0' ? '1' : '0';
			epc_einvoice_save_settings($db_link, $_POST);
			epc_uae_company_save_profile($db_link, array(
				'company_country_code' => $country,
				'company_trn' => $trn,
				'company_legal_name' => trim((string)($_POST['seller_name'] ?? '')),
				'company_vat_registered' => $vatRegistered,
			));
			epc_erp_json(true, 'Seller profile saved — FTA company registration updated');

		case 'einvoice_save_buyer':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
			epc_einvoice_save_buyer_profile($db_link, $_POST);
			epc_erp_json(true, 'Buyer profile saved');

		case 'einvoice_save_asp':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
			epc_einvoice_save_settings($db_link, $_POST);
			epc_erp_json(true, 'ASP settings saved');

		case 'einvoice_submit':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
			$adminId = class_exists('DP_User') ? (int)DP_User::getAdminId() : 0;
			$res = epc_einvoice_submit_to_asp($db_link, (int)($_POST['document_id'] ?? 0), $adminId);
			epc_erp_json(true, 'Submitted to ASP — reference ' . $res['asp_reference'], $res);

		case 'invoice_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_invoices.php';
			$adminId = class_exists('DP_User') ? (int)DP_User::getAdminId() : 0;
			$id = epc_erp_invoice_save($db_link, $_POST, $adminId);
			epc_erp_dim_save_from_post($db_link, 'invoice', (int) $id, $_POST);
			$doc = epc_einvoice_get_document($db_link, $id);
			$cfg = $GLOBALS['DP_Config'] ?? new DP_Config();
			$redirect = epc_erp_cp_redirect_url('/' . $cfg->backend_dir . '/shop/finance/erp?area=sales&tab=invoices&inv_id=' . $id);
			epc_erp_json(true, !empty($doc['validation_ok']) ? 'Invoice saved and validated' : 'Invoice saved as draft — review validation',
				array('invoice_id' => $id, 'redirect' => $redirect));

		case 'invoice_list':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_invoices.php';
			$from = !empty($_POST['from']) ? strtotime($_POST['from'] . ' 00:00:00') : strtotime(date('Y-m-01'));
			$to = !empty($_POST['to']) ? strtotime($_POST['to'] . ' 23:59:59') : time();
			$rows = epc_erp_invoice_list($db_link, $from, $to, array(
				'status' => (string)($_POST['status'] ?? ''),
				'q' => (string)($_POST['q'] ?? ''),
			), 200);
			epc_erp_json(true, 'OK', array('invoices' => $rows));

		case 'invoice_from_order':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_invoices.php';
			$adminId = class_exists('DP_User') ? (int)DP_User::getAdminId() : 0;
			$id = epc_erp_invoice_from_order($db_link, (int)($_POST['order_id'] ?? 0), array(), $adminId);
			$cfg = $GLOBALS['DP_Config'] ?? new DP_Config();
			$redirect = epc_erp_cp_redirect_url('/' . $cfg->backend_dir . '/shop/finance/erp?area=sales&tab=invoices&inv_id=' . $id);
			epc_erp_json(true, 'Invoice generated from order', array('invoice_id' => $id, 'redirect' => $redirect));

		case 'inv_sync_warehouses':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$n = epc_erp_inventory_sync_warehouses($db_link);
			epc_erp_json(true, 'Synced ' . $n . ' warehouse(s) from shop storages', array('created' => $n));

		case 'inv_create_warehouse':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$id = epc_erp_inventory_create_warehouse($db_link, $_POST);
			epc_erp_json(true, 'Warehouse created', array('id' => $id));

		case 'inv_create_item':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$custom = array();
			foreach ($_POST as $k => $v) {
				if (strpos($k, 'custom_') === 0) {
					$custom[substr($k, 7)] = $v;
				}
			}
			$data = $_POST;
			$data['custom_fields'] = $custom;
			$id = epc_erp_inventory_create_item($db_link, $data);
			epc_erp_dim_save_from_post($db_link, 'inventory_item', (int) $id, $_POST);
			epc_erp_json(true, 'Inventory item created', array('id' => $id));

		case 'inv_record_movement':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$qty = (float) ($_POST['qty'] ?? 0);
			$type = (string) ($_POST['movement_type'] ?? 'adjustment');
			if ($type === 'adjustment' && $qty < 0) {
				$qty = abs($qty);
			}
			$id = epc_erp_inventory_record_movement($db_link, array(
				'movement_type' => $type,
				'warehouse_id' => (int) ($_POST['warehouse_id'] ?? 0),
				'item_id' => (int) ($_POST['item_id'] ?? 0),
				'qty' => $qty,
				'unit_cost' => (float) ($_POST['unit_cost'] ?? 0),
				'batch_no' => (string) ($_POST['batch_no'] ?? ''),
				'expiry_date' => (string) ($_POST['expiry_date'] ?? ''),
				'reference' => (string) ($_POST['reference'] ?? ''),
			));
			epc_erp_json(true, 'Inventory movement recorded', array('movement_id' => $id));

		case 'inv_transfer':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$res = epc_erp_inventory_transfer($db_link, $_POST);
			epc_erp_json(true, 'Warehouse transfer completed at avg cost ' . number_format($res['unit_cost'], 4), $res);

		case 'inv_import_csv':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$csv = (string) ($_POST['csv_text'] ?? '');
			if ($csv === '' && !empty($_FILES['csv_file']['tmp_name']) && is_uploaded_file($_FILES['csv_file']['tmp_name'])) {
				$csv = (string) file_get_contents($_FILES['csv_file']['tmp_name']);
			}
			$res = epc_erp_inventory_import_csv(
				$db_link,
				$csv,
				(int) ($_POST['warehouse_id'] ?? 0),
				(string) ($_POST['default_movement_type'] ?? 'purchase_in')
			);
			$msg = 'Posted ' . $res['posted'] . ' movement(s)';
			if (!empty($res['errors'])) {
				$msg .= '; ' . count($res['errors']) . ' error(s): ' . implode('; ', array_slice($res['errors'], 0, 5));
			}
			epc_erp_json($res['posted'] > 0 || empty($res['errors']), $msg, $res);

		case 'inv_run_closing':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$n = epc_erp_inventory_run_closing(
				$db_link,
				(string) ($_POST['period_end'] ?? date('Y-m-t')),
				(int) ($_POST['warehouse_id'] ?? 0)
			);
			epc_erp_json(true, 'Closing snapshot saved for ' . $n . ' line(s)', array('lines' => $n));

		case 'fa_create_asset':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fixed_assets.php';
			$id = epc_erp_fa_create_asset($db_link, $_POST);
			epc_erp_json(true, 'Fixed asset registered', array('id' => $id));

		case 'fa_run_depreciation':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fixed_assets.php';
			$res = epc_erp_fa_run_depreciation($db_link, (string) ($_POST['period_month'] ?? date('Y-m')), (string) ($_POST['note'] ?? ''));
			epc_erp_json(true, 'Depreciation posted — ' . number_format($res['total'], 2) . ' AED', $res);

		case 'opening_create_batch':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_opening.php';
			$id = epc_erp_opening_create_batch($db_link, $_POST);
			epc_erp_json(true, 'Opening batch created (draft)', array('batch_id' => $id));

		case 'opening_add_coa_line':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_opening.php';
			epc_erp_opening_add_line($db_link, (int) ($_POST['batch_id'] ?? 0), array(
				'line_type' => 'coa',
				'entity_id' => (int) ($_POST['entity_id'] ?? 0),
				'debit' => (float) ($_POST['debit'] ?? 0),
				'credit' => (float) ($_POST['credit'] ?? 0),
			));
			epc_erp_json(true, 'COA opening line added');

		case 'opening_add_inv_line':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_opening.php';
			epc_erp_opening_add_line($db_link, (int) ($_POST['batch_id'] ?? 0), array(
				'line_type' => 'inventory',
				'entity_id' => (int) ($_POST['item_id'] ?? 0),
				'qty' => (float) ($_POST['qty'] ?? 0),
				'unit_cost' => (float) ($_POST['unit_cost'] ?? 0),
				'meta' => array(
					'warehouse_id' => (int) ($_POST['warehouse_id'] ?? 0),
					'batch_no' => (string) ($_POST['batch_no'] ?? ''),
					'expiry_date' => (string) ($_POST['expiry_date'] ?? ''),
				),
			));
			epc_erp_json(true, 'Inventory opening line added');

		case 'opening_post_batch':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_opening.php';
			epc_erp_opening_post_batch($db_link, (int) ($_POST['batch_id'] ?? 0));
			epc_erp_json(true, 'Opening batch posted');

		case 'save_rfq':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$id = epc_erp_rfq_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'rfq', (int) $id, $_POST);
			epc_erp_json(true, 'RFQ saved', array('id' => $id));

		case 'delivery_note_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$r = epc_erp_delivery_note_create($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'delivery_note', (int) ($r['id'] ?? 0), $_POST);
			epc_erp_json(true, 'Delivery note ' . $r['note_no'] . ' created', $r);

		case 'bank_import':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$r = epc_erp_bank_statement_import($db_link, (int) ($_POST['account_id'] ?? 0), (string) ($_POST['csv_text'] ?? ''));
			epc_erp_json(true, 'Imported ' . (int) $r['imported'] . ' statement line(s)', $r);

		case 'bank_reconcile':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			epc_erp_bank_reconcile_match($db_link, (int) ($_POST['line_id'] ?? 0), (int) ($_POST['entry_id'] ?? 0));
			epc_erp_mark_entry_reconciled($db_link, (int) ($_POST['entry_id'] ?? 0), 1);
			epc_erp_json(true, 'Bank line matched to cash entry');

		case 'save_contact':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$id = epc_erp_contact_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'contact', (int) $id, $_POST);
			epc_erp_json(true, 'Contact saved', array('id' => $id));

		case 'sync_contacts':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$n = epc_erp_contacts_sync_from_masters($db_link);
			epc_erp_json(true, 'Synced ' . $n . ' contact(s)', array('created' => $n));

		case 'document_upload':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$file = $_FILES['document_file'] ?? $_FILES['file'] ?? array();
			$id = epc_erp_document_upload($db_link, $_POST, $file);
			epc_erp_json(true, 'Document uploaded', array('id' => $id));

		case 'document_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$allowed = epc_erp_user_can_access($db_link);
			epc_erp_document_delete($db_link, (int)($_POST['doc_id'] ?? 0), $allowed);
			epc_erp_json(true, 'Document deleted');

		case 'expense_report_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$id = epc_erp_expense_report_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'expense_report', (int) $id, $_POST);
			epc_erp_json(true, 'Expense report submitted', array('id' => $id));

		case 'po_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
			$id = epc_erp_po_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'purchase_order', (int) $id, $_POST);
			epc_erp_json(true, 'Purchase order created', array('id' => $id));

		case 'po_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
			epc_erp_po_set_status($db_link, (int) ($_POST['po_id'] ?? 0), (string) ($_POST['status'] ?? ''));
			epc_erp_json(true, 'PO status updated');

		case 'po_to_invoice':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			$r = epc_erp_po_convert_to_purchase($db_link, (int) ($_POST['po_id'] ?? 0));
			epc_erp_json(true, 'Purchase invoice ' . $r['voucher_no'] . ' created', $r);

		case 'customer_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
			$cid = epc_erp_customer_provision($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'customer', (int) $cid, $_POST);
			epc_erp_json(true, 'Customer created', array('user_id' => $cid));

		case 'so_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			$id = epc_erp_sales_order_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'sales_order', (int) $id, $_POST);
			epc_erp_json(true, 'Sales order saved', array('id' => $id));

		case 'so_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			epc_erp_sales_order_set_status($db_link, (int) ($_POST['so_id'] ?? 0), (string) ($_POST['status'] ?? ''));
			epc_erp_json(true, 'Sales order status updated');

		case 'so_to_invoice':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			$r = epc_erp_so_convert_to_invoice($db_link, (int) ($_POST['so_id'] ?? 0));
			epc_erp_json(true, 'Sales invoice ' . $r['invoice_number'] . ' posted', $r);

		case 'so_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			epc_erp_sales_order_delete($db_link, (int) ($_POST['so_id'] ?? 0));
			epc_erp_json(true, 'Sales order deleted');

		case 'receipt_voucher':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			$r = epc_erp_receipt_voucher($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'cash_entry', (int) ($r['cash_entry_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Receipt voucher ' . $r['voucher_no'] . ' recorded', $r);

		case 'transfer_voucher':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			$r = epc_erp_transfer_voucher($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'cash_entry', (int) ($r['out_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Transfer voucher ' . $r['voucher_no'] . ' recorded', $r);

		case 'payment_batch_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
			$id = epc_erp_payment_batch_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'payment_batch', (int) $id, $_POST);
			epc_erp_json(true, 'Payment batch draft created', array('id' => $id));

		case 'petty_cash_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
			$id = epc_erp_petty_cash_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'petty_cash', (int) $id, $_POST);
			epc_erp_json(true, 'Petty cash float created', array('id' => $id));

		case 'agenda_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
			$id = epc_erp_agenda_save($db_link, $_POST);
			epc_erp_json(true, 'Agenda event added', array('id' => $id));

		case 'kb_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
			$id = epc_erp_kb_save($db_link, $_POST);
			epc_erp_json(true, 'Knowledge article published', array('id' => $id));

		case 'multi_entity_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
			epc_erp_multi_entity_set($db_link, !empty($_POST['enabled']));
			epc_erp_json(true, 'Multi-entity preference saved');

		case 'cs_save_declaration':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_shipping.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
			$adminId = class_exists('DP_User') ? (int) DP_User::getAdminId() : 0;
			$id = epc_cs_save_declaration($db_link, $_POST, $adminId);
			epc_cs_attach_pdf_to_declaration($db_link, $id, $_POST);
			$redirect = epc_cs_redirect_after_save(
				$id,
				(string) ($_POST['from'] ?? ''),
				(string) ($_POST['to'] ?? '')
			);
			epc_erp_json(true, 'Declaration saved', array('id' => $id, 'redirect' => $redirect));

		case 'cs_submit_declaration':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_shipping.php';
			epc_cs_submit_declaration($db_link, (int) ($_POST['id'] ?? 0));
			epc_erp_json(true, 'Declaration submitted');

		case 'cs_delete_declaration':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_shipping.php';
			$delId = (int) ($_POST['id'] ?? 0);
			$delFrom = (string) ($_POST['from'] ?? '');
			$delTo = (string) ($_POST['to'] ?? '');
			$delCategory = (string) ($_POST['category'] ?? '');
			epc_cs_delete_declaration($db_link, $delId);
			$extra = $delCategory !== '' ? array('cs_view' => 'list', 'cs_category' => $delCategory) : array();
			$redirect = epc_cs_tab_url(
				epc_cs_resolve_erp_url(),
				$delFrom !== '' ? $delFrom : date('Y-m-01'),
				$delTo !== '' ? $delTo : date('Y-m-d'),
				$extra
			);
			epc_erp_json(true, 'Declaration deleted', array('redirect' => $redirect));

		case 'cs_list_declarations':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_shipping.php';
			$rows = epc_cs_list_declarations($db_link, array(
				'category' => (string) ($_POST['category'] ?? ''),
				'status' => (string) ($_POST['status'] ?? ''),
				'from' => (string) ($_POST['from'] ?? ''),
				'to' => (string) ($_POST['to'] ?? ''),
			), 200);
			epc_erp_json(true, 'OK', array('declarations' => $rows));

		case 'order_fulfillment_bootstrap':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
			$r = epc_erp_order_fulfillment_bootstrap($db_link, (int) ($_POST['order_id'] ?? 0));
			epc_erp_json(true, 'ERP sales order and supplier POs linked', $r);

		case 'order_fulfillment_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
			$r = epc_erp_order_fulfillment_status($db_link, (int) ($_POST['order_id'] ?? 0));
			epc_erp_json(true, 'OK', $r);

		case 'order_fulfillment_sync':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
			$orderId = (int) ($_POST['order_id'] ?? 0);
			$poUpdates = epc_erp_order_fulfillment_sync_po_statuses($db_link, $orderId);
			$fulfillment = epc_erp_order_fulfillment_sync_sales_status($db_link, $orderId);
			epc_erp_json(true, 'Fulfillment status synced', array(
				'po_updates' => $poUpdates,
				'fulfillment_status' => $fulfillment,
				'status' => epc_erp_order_fulfillment_status($db_link, $orderId),
			));

		case 'order_fulfillment_post_po':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
			$r = epc_erp_order_fulfillment_post_po_invoice($db_link, (int) ($_POST['po_id'] ?? 0));
			epc_erp_json(true, 'Purchase invoice ' . $r['voucher_no'] . ' posted (cost)', $r);

		case 'order_fulfillment_post_sales':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
			$adminId = class_exists('DP_User') ? (int) DP_User::getAdminId() : 0;
			$r = epc_erp_order_fulfillment_post_sales_invoice($db_link, (int) ($_POST['order_id'] ?? 0), $adminId);
			epc_erp_json(true, 'Sales invoice posted (revenue)', $r);

		case 'order_fulfillment_auto_post':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
			$adminId = class_exists('DP_User') ? (int) DP_User::getAdminId() : 0;
			$r = epc_erp_order_fulfillment_auto_post($db_link, (int) ($_POST['order_id'] ?? 0), $adminId);
			epc_erp_json(true, 'Auto-post complete', $r);

		case 'order_fulfillment_swap_supplier':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_fulfillment.php';
			$r = epc_erp_order_fulfillment_swap_line_supplier(
				$db_link,
				(int) ($_POST['order_id'] ?? 0),
				(int) ($_POST['order_item_id'] ?? 0),
				(int) ($_POST['new_storage_id'] ?? 0)
			);
			epc_erp_json(true, 'Supplier swapped on order line', $r);

		case 'cs_import_declaration_pdf':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_shipping.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_declaration_pdf_import.php';
			if (empty($_FILES['declaration_pdf'])) {
				throw new Exception('No PDF file uploaded');
			}
			$hint = (string) ($_POST['declaration_type_hint'] ?? '');
			$parsed = epc_cs_pdf_import_from_upload($_FILES['declaration_pdf'], $hint);
			$declNo = epc_cs_declaration_number_from_data(array(
				'declaration_number' => $parsed['core']['declaration_number'] ?? '',
				'box_data' => array('boxes' => $parsed['boxes'] ?? array()),
			));
			if ($declNo !== '') {
				epc_cs_assert_unique_declaration_number($db_link, $declNo, (int) ($_POST['exclude_id'] ?? 0));
			}
			$staged = epc_cs_stage_pdf_upload($_FILES['declaration_pdf']);
			$formData = epc_cs_apply_parsed_to_form_data($parsed);
			$pdftotext = $parsed['pdftotext'] ?? epc_cs_pdf_pdftotext_diagnostics();
			unset($parsed['pdftotext']);
			$msg = 'PDF parsed — review auto-filled fields';
			if (!empty($parsed['parse_warning'])) {
				$msg = $parsed['parse_warning'] . ' Review highlighted fields before saving.';
			}
			epc_erp_json(true, $msg, array(
				'ok' => true,
				'parsed' => $parsed,
				'form' => $formData,
				'boxes_mapped' => (int) ($parsed['boxes_mapped'] ?? 0),
				'declaration_type' => (string) ($parsed['declaration_type'] ?? ''),
				'category' => (string) ($parsed['category'] ?? ''),
				'line_items_count' => count($parsed['line_items'] ?? array()),
				'parse_warning' => (string) ($parsed['parse_warning'] ?? ''),
				'partial' => !empty($parsed['partial']),
				'pdftotext_available' => !empty($pdftotext['available']),
				'pdftotext_path' => (string) ($pdftotext['path'] ?? ''),
				'pdftotext_diag_url' => '/epc-custom-shipping-pdf-test.php?token=epartscart-deploy-2026',
				'pdf_token' => (string) ($staged['token'] ?? ''),
				'pdf_preview_url' => (string) ($staged['preview_url'] ?? ''),
				'pdf_file_name' => (string) ($staged['file_name'] ?? ''),
				'declaration_number' => $declNo,
			));

		case 'pm_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';
			$pmTable = (string) ($_POST['pm_table'] ?? '');
			$pmId = epc_erp_pm_save($db_link, $pmTable, $_POST);
			epc_erp_json(true, 'Saved', array('id' => $pmId));

		case 'pm_toggle':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';
			epc_erp_pm_toggle($db_link, (string) ($_POST['pm_table'] ?? ''), (int) ($_POST['id'] ?? 0), (int) ($_POST['active'] ?? 0));
			epc_erp_json(true, 'Updated');

		case 'pm_budget_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';
			$bId = epc_erp_pm_budget_save($db_link, $_POST);
			epc_erp_json(true, 'Budget saved', array('id' => $bId));

		case 'pm_budget_line_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';
			$blId = epc_erp_pm_budget_line_save($db_link, $_POST);
			epc_erp_json(true, 'Budget line added', array('id' => $blId));

		case 'pm_listing_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';
			$lId = epc_erp_pm_listing_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'listing', (int) $lId, $_POST);
			epc_erp_json(true, 'Listing saved', array('id' => $lId));

		case 'pm_listing_attach':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';
			epc_erp_pm_listing_attach($db_link, (int) ($_POST['id'] ?? 0), (string) ($_POST['voucher_ref'] ?? ''));
			epc_erp_json(true, 'Listing attached to voucher');

		case 'pm_cheque_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';
			$chId = epc_erp_pm_cheque_save($db_link, $_POST);
			epc_erp_json(true, 'Cheque recorded', array('id' => $chId));

		default:
			if (strpos($action, 'crm_') === 0 || in_array($action, array(
				'save_lead', 'delete_lead', 'save_opportunity', 'update_stage', 'convert_lead',
				'won_hint', 'save_activity', 'toggle_activity', 'dashboard', 'pipeline',
			), true)) {
				require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_access.php';
				require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_helpers.php';
				$r = epc_crm_handle_ajax_action($db_link, $action, $_POST);
				$msg = $r['message'] ?? 'OK';
				unset($r['message']);
				epc_erp_json(true, $msg, $r);
			}
			epc_erp_json(false, 'Unknown action');
	}
} catch (\Throwable $e) {
	$extra = array('ok' => false);
	if ($action === 'cs_import_declaration_pdf' && function_exists('epc_cs_pdf_pdftotext_diagnostics')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_custom_declaration_pdf_import.php';
		$diag = epc_cs_pdf_pdftotext_diagnostics();
		$extra['pdftotext_available'] = !empty($diag['available']);
		$extra['pdftotext_path'] = (string) ($diag['path'] ?? '');
		$extra['pdftotext_diag_url'] = '/epc-custom-shipping-pdf-test.php?token=epartscart-deploy-2026';
	}
	epc_erp_json(false, $e->getMessage(), $extra);
}
