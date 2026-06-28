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
	// Central audit hook: every successful mutating action is recorded with the
	// actor, IP and device plus a sanitised payload snapshot. Read-only lookups
	// are skipped to keep the trail signal-rich.
	if ($ok && isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
		$act = isset($GLOBALS['action']) ? (string)$GLOBALS['action'] : '';
		$readOnlyPrefixes = array('list_', 'load_', 'get_', 'fetch_', 'search_', 'lookup_', 'open_docs', 'preview_', 'report_', 'export_');
		$readOnlyExact = array('settlement_open_docs', 'gl_preview', 'aging', 'trial_balance');
		$skip = in_array($act, $readOnlyExact, true);
		foreach ($readOnlyPrefixes as $p) {
			if (strncmp($act, $p, strlen($p)) === 0) { $skip = true; break; }
		}
		if ($act !== '' && !$skip) {
			try {
				require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_audit.php';
				$snapshot = array();
				foreach ($_POST as $k => $v) {
					if (in_array($k, array('csrf_guard_key', 'action', 'password', 'pass', 'pwd'), true)) {
						continue;
					}
					if (is_scalar($v)) {
						$snapshot[$k] = mb_substr((string)$v, 0, 200);
					} elseif (is_array($v)) {
						$snapshot[$k] = '[' . count($v) . ' items]';
					}
				}
				$eid = 0;
				if (isset($extra['id'])) { $eid = (int)$extra['id']; }
				elseif (isset($extra['journal_id'])) { $eid = (int)$extra['journal_id']; }
				elseif (isset($extra['cash_entry_id'])) { $eid = (int)$extra['cash_entry_id']; }
				epc_erp_audit_log($GLOBALS['db_link'], $act, 'erp_action', $eid, mb_substr((string)$message, 0, 200), $snapshot);
			} catch (Throwable $auditErr) {
				// never let auditing break the actual response
			}
		}
	}
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
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
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
			$piWf = epc_bos_wf_maybe_raise($db_link, 'purchase_invoice', (int) $id, 'BILL #' . (int) $id, $_POST);
			epc_erp_json(true, 'Purchase invoice recorded' . $invMsg . $piWf, $extra);

		case 'supplier_payment':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$pv = epc_erp_payment_voucher($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'cash_entry', (int) ($pv['cash_entry_id'] ?? 0), $_POST);
			$pvMsg = 'Supplier payment ' . ($pv['voucher_no'] ?? '') . ' recorded';
			if (!empty($pv['allocated'])) {
				$pvMsg .= ' — ' . number_format((float) $pv['allocated'], 2) . ' settled against bills';
			}
			$pvMsg .= epc_bos_wf_maybe_raise($db_link, 'payment_voucher', (int) ($pv['cash_entry_id'] ?? 0), (string) ($pv['voucher_no'] ?? 'PV'), $_POST);
			epc_erp_json(true, $pvMsg, $pv);

		case 'payment_voucher':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$pv = epc_erp_payment_voucher($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'cash_entry', (int) ($pv['cash_entry_id'] ?? 0), $_POST);
			$pvMsg = 'Payment voucher ' . ($pv['voucher_no'] ?? '') . ' recorded';
			if (!empty($pv['allocated'])) {
				$pvMsg .= ' — ' . number_format((float) $pv['allocated'], 2) . ' settled against bills';
			}
			$pvMsg .= epc_bos_wf_maybe_raise($db_link, 'payment_voucher', (int) ($pv['cash_entry_id'] ?? 0), (string) ($pv['voucher_no'] ?? 'PV'), $_POST);
			epc_erp_json(true, $pvMsg, $pv);

		case 'settlement_open_docs':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_settlement.php';
			$sdType = (string) ($_POST['doc_type'] ?? 'ar');
			if ($sdType === 'ap') {
				$docs = epc_erp_open_supplier_bills($db_link, (int) ($_POST['counterparty_id'] ?? 0));
			} else {
				$docs = epc_erp_open_customer_invoices($db_link, (int) ($_POST['counterparty_id'] ?? 0));
			}
			epc_erp_json(true, 'OK', array('docs' => $docs));

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

		case 'command_center':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_command_center.php';
			$from = !empty($_POST['date_from']) ? strtotime($_POST['date_from'] . ' 00:00:00') : 0;
			$to = !empty($_POST['date_to']) ? strtotime($_POST['date_to'] . ' 23:59:59') : 0;
			$role = (string) ($_POST['role'] ?? '');
			epc_erp_json(true, 'OK', epc_erp_command_center($db_link, $role, $from, $to));

		case 'cc_kpi_tiles':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_command_center.php';
			$from = !empty($_POST['date_from']) ? strtotime($_POST['date_from'] . ' 00:00:00') : 0;
			$to = !empty($_POST['date_to']) ? strtotime($_POST['date_to'] . ' 23:59:59') : 0;
			epc_erp_json(true, 'OK', array('tiles' => epc_erp_cc_kpi_tiles($db_link, $from, $to)));

		case 'cc_approval_queue':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_command_center.php';
			epc_erp_json(true, 'OK', array('queue' => epc_erp_cc_approval_queue($db_link)));

		case 'create_coa':
			$id = epc_erp_gl_create_coa($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'coa_account', (int) $id, $_POST);
			epc_erp_json(true, 'COA account created', array('id' => $id));

		case 'gl_manual_entry':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$id = epc_erp_gl_manual_entry($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'gl_entry', (int) $id, $_POST);
			$glWf = epc_bos_wf_maybe_raise($db_link, 'gl_journal', (int) $id, 'JV #' . (int) $id, $_POST);
			epc_erp_json(true, 'GL journal posted' . $glWf, array('journal_id' => $id));

		case 'gl_reverse_journal':
			$revDate = !empty($_POST['reverse_date']) ? (int) strtotime((string) $_POST['reverse_date'] . ' 12:00:00') : 0;
			$revId = epc_erp_gl_reverse_journal($db_link, (int) ($_POST['journal_id'] ?? 0), $revDate, (string) ($_POST['note'] ?? ''));
			epc_erp_json(true, 'Journal reversed (new journal #' . $revId . ')', array('journal_id' => $revId));

		case 'fiscal_set_lock':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fiscal_periods.php';
			$lockDate = !empty($_POST['lock_date']) ? (int) strtotime((string) $_POST['lock_date'] . ' 23:59:59') : 0;
			epc_erp_fiscal_set_lock($db_link, $lockDate, (string) ($_POST['note'] ?? ''));
			epc_erp_json(true, $lockDate > 0 ? ('Periods locked up to ' . date('Y-m-d', $lockDate)) : 'Fiscal lock cleared', array('lock_date' => $lockDate));

		case 'period_list':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_period_close.php';
			$periods = epc_erp_period_list($db_link);
			epc_erp_json(true, 'OK', array('periods' => $periods));

		case 'period_checklist':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_period_close.php';
			$ym = (string) ($_POST['year_month'] ?? date('Y-m'));
			$checklist = epc_erp_period_checklist($db_link, $ym);
			epc_erp_json(true, 'OK', array('year_month' => $ym, 'checklist' => $checklist));

		case 'period_soft_close':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_period_close.php';
			$ym = (string) ($_POST['year_month'] ?? '');
			$note = (string) ($_POST['note'] ?? '');
			$adminId = function_exists('epc_erp_admin_id') ? epc_erp_admin_id() : 0;
			$res = epc_erp_period_soft_close($db_link, $ym, $adminId, $note);
			epc_erp_json($res['ok'], $res['error'] ?? 'Period soft-closed: ' . $ym, $res);

		case 'period_lock':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_period_close.php';
			$ym = (string) ($_POST['year_month'] ?? '');
			$note = (string) ($_POST['note'] ?? '');
			$adminId = function_exists('epc_erp_admin_id') ? epc_erp_admin_id() : 0;
			$res = epc_erp_period_lock($db_link, $ym, $adminId, $note);
			epc_erp_json($res['ok'], $res['error'] ?? 'Period locked: ' . $ym, $res);

		case 'period_reopen':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_period_close.php';
			$ym = (string) ($_POST['year_month'] ?? '');
			$note = (string) ($_POST['note'] ?? '');
			$adminId = function_exists('epc_erp_admin_id') ? epc_erp_admin_id() : 0;
			$res = epc_erp_period_reopen($db_link, $ym, $adminId, $note);
			epc_erp_json($res['ok'], $res['error'] ?? 'Period reopened: ' . $ym, $res);

		case 'period_summary':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_period_close.php';
			$summary = epc_erp_period_close_summary($db_link);
			epc_erp_json(true, 'OK', $summary);

		case 'period_log':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_period_close.php';
			$ym = (string) ($_POST['year_month'] ?? '');
			$log = epc_erp_period_close_log_list($db_link, $ym);
			epc_erp_json(true, 'OK', array('log' => $log));

		case 'ccy_set_rate':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_currency.php';
			$rFrom = strtoupper(trim((string) ($_POST['from'] ?? '')));
			$rTo = strtoupper(trim((string) ($_POST['to'] ?? '')));
			$rRate = (float) ($_POST['rate'] ?? 0);
			$rAsOf = !empty($_POST['as_of']) ? (int) strtotime((string) $_POST['as_of'] . ' 12:00:00') : time();
			if ($rFrom === '' || $rTo === '' || $rRate <= 0) {
				epc_erp_json(false, 'Provide from, to and a positive rate');
			}
			epc_ccy_set_rate($db_link, $rFrom, $rTo, $rRate, $rAsOf);
			epc_erp_json(true, 'Rate saved: 1 ' . $rFrom . ' = ' . $rRate . ' ' . $rTo . ' as of ' . date('Y-m-d', $rAsOf));

		case 'fx_revaluation_preview':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ccy_revaluation.php';
			$fxAsOf = !empty($_POST['as_of']) ? (int) strtotime((string) $_POST['as_of'] . ' 23:59:59') : time();
			$fxPrev = epc_erp_fx_revaluation_preview($db_link, $fxAsOf);
			epc_erp_json(true, 'Preview ready', array(
				'base' => $fxPrev['base'],
				'by_currency' => $fxPrev['by_currency'],
				'total_unrealised' => $fxPrev['total_unrealised'],
			));

		case 'fx_post_revaluation':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ccy_revaluation.php';
			$fxAsOf = !empty($_POST['as_of']) ? (int) strtotime((string) $_POST['as_of'] . ' 23:59:59') : time();
			$fxAuto = !isset($_POST['auto_reverse']) || (string) $_POST['auto_reverse'] === '1';
			$fxRes = epc_erp_fx_post_revaluation($db_link, $fxAsOf, $fxAuto);
			epc_erp_json((bool) $fxRes['status'], $fxRes['message'], array('journal_id' => (int) $fxRes['journal_id'], 'reverse_journal_id' => (int) $fxRes['reverse_journal_id']));

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

		// ---- BOS Compliance pillar ----
		case 'bos_compliance_file':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_compliance.php';
			epc_bos_compliance_set_filing(
				$db_link,
				(int)($_POST['obligation_id'] ?? 0),
				(string)($_POST['period_label'] ?? ''),
				(int)($_POST['period_end'] ?? 0),
				(int)($_POST['due_date'] ?? 0),
				(string)($_POST['status'] ?? 'filed'),
				(string)($_POST['reference'] ?? ''),
				(string)($_POST['notes'] ?? '')
			);
			epc_erp_json(true, 'Filing status saved');

		case 'bos_compliance_add_obligation':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_compliance.php';
			$oid = epc_bos_compliance_add_obligation($db_link, $_POST);
			epc_erp_json($oid > 0, $oid > 0 ? 'Obligation saved' : 'Title required', array('id' => $oid));

		case 'bos_compliance_disable_obligation':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_compliance.php';
			epc_bos_compliance_disable_obligation($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Obligation disabled');

		case 'bos_compliance_save_retention':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_compliance.php';
			$rid = epc_bos_retention_save($db_link, $_POST);
			epc_erp_json($rid > 0, $rid > 0 ? 'Retention rule saved' : 'Label required', array('id' => $rid));

		case 'bos_compliance_fetch':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_compliance.php';
			$fetch = epc_bos_compliance_fetch_updates($db_link);
			$fc = (int) $fetch['added_count'];
			$uc = (int) $fetch['updated_count'];
			if ($fc === 0 && $uc === 0) {
				$fmsg = 'Compliance catalog is up to date — no changes (version ' . $fetch['version'] . ').';
			} else {
				$parts = array();
				if ($fc > 0) { $parts[] = $fc . ' added (' . implode(', ', array_slice($fetch['added'], 0, 6)) . ($fc > 6 ? '…' : '') . ')'; }
				if ($uc > 0) { $parts[] = $uc . ' updated (' . implode(', ', array_slice($fetch['updated'], 0, 6)) . ($uc > 6 ? '…' : '') . ')'; }
				$fmsg = 'Fetched catalog ' . $fetch['version'] . ' — ' . implode('; ', $parts) . '.';
			}
			epc_erp_json(true, $fmsg, $fetch);

		// ---- BOS Workflow / Approvals pillar ----
		case 'bos_wf_save_rule':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$rid = epc_bos_wf_save_rule($db_link, $_POST);
			epc_erp_json($rid > 0, $rid > 0 ? 'Approval rule saved' : 'Name and document type required', array('id' => $rid));

		case 'bos_wf_disable_rule':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			epc_bos_wf_disable_rule($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Rule disabled');

		case 'bos_wf_decide':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$res = epc_bos_wf_decide($db_link, (int)($_POST['request_id'] ?? 0), (string)($_POST['decision'] ?? 'approve'), (string)($_POST['comment'] ?? ''));
			epc_erp_json(!empty($res['status']), (string)($res['message'] ?? 'Done'), $res);

		case 'bos_wf_raise_test':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$ref = (string)($_POST['entity_ref'] ?? 'TEST');
			$reqId = epc_bos_wf_raise(
				$db_link,
				(string)($_POST['entity_type'] ?? 'purchase_order'),
				(int)(time() % 1000000),
				$ref,
				(float)($_POST['amount'] ?? 0),
				$ref
			);
			epc_erp_json(true, $reqId > 0 ? 'Approval request raised (#' . $reqId . ')' : 'No rule matched — no approval needed for this amount', array('request_id' => $reqId));

		// ---- BOS Industry Intelligence pillar ----
		case 'bos_intel_toggle_control':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_intelligence.php';
			epc_bos_intel_set_control($db_link, (string)($_POST['code'] ?? ''), (int)($_POST['checked'] ?? 0) === 1);
			epc_erp_json(true, 'Control updated');

		// ---- BOS VAT / tourist refund register (country-aware) ----
		case 'bos_vat_refund_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_vat_refund.php';
			$rr = epc_bos_vat_refund_save($db_link, $_POST);
			epc_erp_json(true, 'Refund record saved (refund ' . number_format((float) $rr['refund'], 2) . ')', $rr);

		case 'bos_vat_refund_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_vat_refund.php';
			$ok = epc_bos_vat_refund_set_status($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['status'] ?? ''));
			epc_erp_json($ok, $ok ? 'Status updated' : 'Invalid status');

		case 'opl_params_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
			$oItem = (int)($_POST['item_id'] ?? 0); $oWh = (int)($_POST['warehouse_id'] ?? 0);
			if ($oItem <= 0 || $oWh <= 0) { throw new Exception('Item and warehouse required'); }
			epc_opl_params_save($db_link, $oItem, $oWh, $_POST);
			epc_erp_json(true, 'Parameters saved — recalculated');

		case 'opl_set_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
			$oItem = (int)($_POST['item_id'] ?? 0); $oWh = (int)($_POST['warehouse_id'] ?? 0);
			if ($oItem <= 0 || $oWh <= 0) { throw new Exception('Item and warehouse required'); }
			epc_opl_set_status($db_link, $oItem, $oWh, (string)($_POST['status'] ?? 'pending'), (float)($_POST['roq'] ?? 0), (float)($_POST['value'] ?? 0), (string)($_POST['supplier'] ?? ''));
			epc_erp_json(true, 'Recommendation ' . (string)($_POST['status'] ?? ''));

		case 'opl_confirm_all':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
			$n = epc_opl_confirm_all($db_link, (int)($_POST['warehouse_id'] ?? 0));
			epc_erp_json(true, $n . ' recommendation(s) confirmed');

		case 'opl_create_pos':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
			$poRes = epc_opl_create_draft_pos($db_link, (int)($_POST['warehouse_id'] ?? 0));
			epc_erp_json((int)$poRes['pos'] > 0, (string)$poRes['message']);

		case 'opl_autoplan':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
			$apWh = (int)($_POST['warehouse_id'] ?? 0);
			$apN = epc_opl_confirm_all($db_link, $apWh);
			$apRes = epc_opl_create_draft_pos($db_link, $apWh);
			epc_erp_json((int)$apRes['pos'] > 0, 'Confirmed ' . $apN . ' due line(s). ' . (string)$apRes['message']);

		case 'opl_seed_demo':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
			$res = epc_opl_seed_demo_demand($db_link, 12, (int)($_POST['warehouse_id'] ?? 0));
			epc_erp_json(true, 'Seeded ' . (int)$res['movements'] . ' demand movements across ' . (int)$res['items'] . ' items');

		case 'opl_clear_demo':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
			$cleared = epc_opl_clear_demo_demand($db_link);
			epc_erp_json(true, 'Cleared ' . (int)$cleared . ' seeded demand movements');

		case 'pf_process_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			$pfPid = epc_pf_process_save($db_link, $_POST);
			epc_erp_json(true, 'Process saved', array('id' => $pfPid));

		case 'pf_step_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			$pfSid = epc_pf_step_save($db_link, $_POST);
			epc_erp_json(true, 'Step added', array('id' => $pfSid));

		case 'pf_step_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			epc_pf_step_delete($db_link, (int)($_POST['step_id'] ?? 0));
			epc_erp_json(true, 'Step removed');

		case 'pf_set_dept_head':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			epc_pf_set_dept_head($db_link, (string)($_POST['department_code'] ?? ''), (int)($_POST['head_user_id'] ?? 0));
			epc_erp_json(true, 'Department head saved');

		case 'pf_case_start':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			$pfCid = epc_pf_case_start($db_link, $_POST);
			epc_erp_json(true, 'Case started — routed to first assignee', array('id' => $pfCid));

		case 'pf_case_act':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			$pfRes = epc_pf_case_act($db_link, (int)($_POST['case_id'] ?? 0), (string)($_POST['decision'] ?? 'approve'), (string)($_POST['comment'] ?? ''));
			epc_erp_json(true, (string)$pfRes['message'], $pfRes);

		case 'pf_case_reassign':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			epc_pf_case_reassign($db_link, (int)($_POST['case_id'] ?? 0), (int)($_POST['user_id'] ?? 0));
			epc_erp_json(true, 'Case reassigned');

		case 'pf_case_cancel':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			epc_pf_case_cancel($db_link, (int)($_POST['case_id'] ?? 0));
			epc_erp_json(true, 'Case cancelled');

		case 'pf_seed_demo':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			$pfSeed = epc_pf_seed_demo($db_link);
			epc_erp_json(true, 'Seeded ' . (int)$pfSeed['processes'] . ' processes, ' . (int)$pfSeed['cases'] . ' running cases and ' . (int)($pfSeed['employees'] ?? 0) . ' employees across multiple departments & locations');

		case 'pf_clear_demo':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			$pfClr = epc_pf_clear_demo($db_link);
			epc_erp_json(true, 'Cleared ' . (int)$pfClr . ' sample cases (and demo processes)');

		case 'pf_sync_orders':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
			$pfSyncAll = epc_pf_sync_all_tasks($db_link, 300);
			epc_erp_json(true, (int) $pfSyncAll['orders'] . ' customer order(s), ' . (int) $pfSyncAll['purchase_orders'] . ' purchase order(s), ' . (int) ($pfSyncAll['payments'] ?? 0) . ' supplier payment(s) and ' . (int) ($pfSyncAll['expenses'] ?? 0) . ' expense claim(s) tracked across the process flow');

		case 'demo_seed_sales':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_demo_sales.php';
			$sres = epc_demo_seed_sales($db_link, 6);
			epc_erp_json(true, 'Seeded ' . (int)$sres['orders'] . ' sample orders (' . (int)$sres['lines'] . ' lines, ' . number_format((float)$sres['revenue'], 0) . ' AED revenue)');

		case 'demo_clear_sales':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_demo_sales.php';
			$scleared = epc_demo_clear_sales($db_link);
			epc_erp_json(true, 'Cleared ' . (int)$scleared . ' sample orders');

		case 'sub_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_subscriptions.php';
			if (trim((string)($_POST['code'] ?? '')) === '' || trim((string)($_POST['customer'] ?? '')) === '') { throw new Exception('Code and customer are required'); }
			$sd = $_POST;
			if (!empty($_POST['start_date_str'])) { $sd['start_date'] = strtotime((string)$_POST['start_date_str']) ?: time(); }
			$subId = epc_sub_save($db_link, $sd, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Subscription saved', array('id' => $subId));

		case 'sub_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_subscriptions.php';
			epc_sub_set_status($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['status'] ?? 'active'));
			epc_erp_json(true, 'Subscription ' . (string)($_POST['status'] ?? ''));

		case 'sub_generate':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_subscriptions.php';
			$inv = epc_sub_generate_invoice($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Cycle invoice #' . $inv['id'] . ' generated — ' . number_format($inv['amount'], 2) . ' AED', $inv);

		case 'sub_invoice_paid':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_subscriptions.php';
			epc_sub_invoice_set_status($db_link, (int)($_POST['id'] ?? 0), 'paid');
			epc_erp_json(true, 'Invoice marked paid');

		case 'ctr_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_contracts.php';
			if (trim((string)($_POST['code'] ?? '')) === '' || trim((string)($_POST['title'] ?? '')) === '') { throw new Exception('Code and title are required'); }
			$cd = $_POST;
			if (!empty($_POST['start_date_str'])) { $cd['start_date'] = strtotime((string)$_POST['start_date_str']) ?: 0; }
			if (!empty($_POST['end_date_str'])) { $cd['end_date'] = strtotime((string)$_POST['end_date_str']) ?: 0; }
			$ctrId = epc_ctr_save($db_link, $cd, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Contract saved', array('id' => $ctrId));

		case 'ctr_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_contracts.php';
			epc_ctr_set_status($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['status'] ?? 'draft'));
			epc_erp_json(true, 'Contract ' . (string)($_POST['status'] ?? ''));

		case 'ctr_sign':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_contracts.php';
			if (trim((string)($_POST['signer_name'] ?? '')) === '') { throw new Exception('Signer name is required'); }
			$ip = (string)($_SERVER['REMOTE_ADDR'] ?? '');
			$sig = epc_ctr_sign($db_link, (int)($_POST['contract_id'] ?? 0), (string)$_POST['signer_name'], (string)($_POST['signer_email'] ?? ''), $ip);
			epc_erp_json(true, 'Signed — ' . substr($sig['hash'], 0, 16) . '…', $sig);

		case 'ctr_ocr':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_contracts.php';
			epc_ctr_ocr_store($db_link, (int)($_POST['contract_id'] ?? 0), (string)($_POST['text'] ?? ''));
			epc_erp_json(true, 'OCR text saved');

		case 'docx_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_doc_expiry.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			if (trim((string)($_POST['doc_type'] ?? '')) === '') { throw new Exception('Document type is required'); }
			if (empty($_POST['expiry_date_str'])) { throw new Exception('Expiry date is required'); }
			$dx = $_POST;
			$dx['company_id'] = epc_erp_active_company_id($db_link);
			$dx['issue_date'] = !empty($_POST['issue_date_str']) ? (strtotime((string)$_POST['issue_date_str']) ?: 0) : 0;
			$dx['expiry_date'] = strtotime((string)$_POST['expiry_date_str']) ?: 0;
			$dx['active'] = 1;
			$dxId = epc_docx_save($db_link, $dx, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Document saved to register', array('id' => $dxId));

		case 'docx_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_doc_expiry.php';
			epc_docx_delete($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Document removed from register');

		case 'docx_run_reminders':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_doc_expiry.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
			$dxRun = epc_docx_run_reminders($db_link, epc_erp_active_company_id($db_link));
			epc_erp_json(true, 'Reminders: ' . (int)$dxRun['sent'] . ' sent, ' . (int)$dxRun['checked'] . ' checked, ' . (int)$dxRun['skipped'] . ' not due', $dxRun);

		case 'ins_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_insurance.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			if (trim((string)($_POST['policy_no'] ?? '')) === '') { throw new Exception('Policy number is required'); }
			if (empty($_POST['expiry_date_str'])) { throw new Exception('Expiry date is required'); }
			$ip = $_POST;
			$ip['company_id'] = epc_erp_active_company_id($db_link);
			$ip['start_date'] = !empty($_POST['start_date_str']) ? (strtotime((string)$_POST['start_date_str']) ?: 0) : 0;
			$ip['expiry_date'] = strtotime((string)$_POST['expiry_date_str']) ?: 0;
			$insId = epc_ins_save($db_link, $ip, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Policy saved', array('id' => $insId));

		case 'ins_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_insurance.php';
			epc_ins_delete($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Policy deleted');

		case 'ins_doc_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_insurance.php';
			$insDocId = epc_ins_doc_add($db_link, (int)($_POST['policy_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Document added', array('id' => $insDocId));

		case 'ins_doc_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_insurance.php';
			epc_ins_doc_delete($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Document removed');

		case 'ins_claim_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_insurance.php';
			$ic = $_POST;
			$ic['loss_date'] = !empty($_POST['loss_date_str']) ? (strtotime((string)$_POST['loss_date_str']) ?: 0) : 0;
			$ic['notified_date'] = !empty($_POST['notified_date_str']) ? (strtotime((string)$_POST['notified_date_str']) ?: 0) : time();
			$ic['deadline_date'] = !empty($_POST['deadline_date_str']) ? (strtotime((string)$_POST['deadline_date_str']) ?: 0) : 0;
			$claimId = epc_ins_claim_save($db_link, $ic, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Claim logged', array('id' => $claimId));

		case 'wms_location_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_wms.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$wl = $_POST;
			$wl['company_id'] = epc_erp_active_company_id($db_link);
			$wlId = epc_wms_location_save($db_link, $wl, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Location saved', array('id' => $wlId));

		case 'wms_location_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_wms.php';
			epc_wms_location_delete($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Location deleted');

		case 'wms_receive':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_wms.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			if (trim((string)($_POST['item'] ?? '')) === '') { throw new Exception('Item is required'); }
			$wr = epc_wms_receive($db_link, epc_erp_active_company_id($db_link), (string)$_POST['item'], (float)($_POST['qty'] ?? 0), (int)($_POST['receive_location_id'] ?? 0), (int)($_POST['putaway_location_id'] ?? 0), (string)($_POST['reference'] ?? ''), (string)($_POST['lp_code'] ?? ''));
			epc_erp_json(true, 'Received — put-away work raised', $wr);

		case 'wms_wave_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_wms.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$wmsCo = epc_erp_active_company_id($db_link);
			$waveId = epc_wms_wave_create($db_link, $wmsCo, (string)($_POST['reference'] ?? ''));
			epc_wms_wave_add_pick($db_link, $waveId, (string)($_POST['item'] ?? ''), (float)($_POST['qty'] ?? 0), (int)($_POST['from_location_id'] ?? 0), (int)($_POST['to_location_id'] ?? 0));
			epc_erp_json(true, 'Wave created with pick work', array('id' => $waveId));

		case 'wms_wave_release':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_wms.php';
			epc_wms_wave_release($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Wave released');

		case 'wms_work_complete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_wms.php';
			epc_wms_work_complete($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Work confirmed');

		case 'mfgr_wc_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_mfg_routing.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$mw = $_POST;
			$mw['company_id'] = epc_erp_active_company_id($db_link);
			$mwId = epc_mfgr_wc_save($db_link, $mw, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Work center saved', array('id' => $mwId));

		case 'mfgr_route_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_mfg_routing.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$rOps = array();
			$opNos = isset($_POST['op_no']) && is_array($_POST['op_no']) ? $_POST['op_no'] : array();
			foreach ($opNos as $i => $opNo) {
				$wcId = (int)($_POST['workcenter_id'][$i] ?? 0);
				$runv = (float)($_POST['run_min_per_unit'][$i] ?? 0);
				$setv = (float)($_POST['setup_min'][$i] ?? 0);
				if ($wcId <= 0 && $runv == 0 && $setv == 0) { continue; }
				$rOps[] = array('op_no' => (int)$opNo, 'workcenter_id' => $wcId, 'setup_min' => $setv, 'run_min_per_unit' => $runv, 'description' => (string)($_POST['op_desc'][$i] ?? ''));
			}
			$rData = array('company_id' => epc_erp_active_company_id($db_link), 'product_item_id' => (int)($_POST['product_item_id'] ?? 0), 'name' => (string)($_POST['name'] ?? ''));
			$rId = epc_mfgr_route_save($db_link, $rData, $rOps, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Route saved', array('id' => $rId));

		case 'mfgr_mrp_run':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_mfg_routing.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$demand = array();
			foreach (preg_split('/[\r\n]+/', (string)($_POST['demand'] ?? '')) as $line) {
				$line = trim($line);
				if ($line === '' || strpos($line, '=') === false) { continue; }
				list($k, $v) = explode('=', $line, 2);
				$demand[(int)trim($k)] = (float)trim($v);
			}
			$onhand = array();
			foreach (preg_split('/[\r\n]+/', (string)($_POST['onhand'] ?? '')) as $line) {
				$line = trim($line);
				if ($line === '' || strpos($line, ':') === false) { continue; }
				list($k, $v) = explode(':', $line, 2);
				$onhand[(int)trim($k)] = (float)trim($v);
			}
			if (empty($demand)) { throw new Exception('Enter at least one demand line (itemId=qty)'); }
			$mrp = epc_mfgr_mrp_run($db_link, epc_erp_active_company_id($db_link), $demand, $onhand, time());
			epc_erp_json(true, 'MRP regenerated — ' . count($mrp) . ' planned order(s)', array('count' => count($mrp)));

		case 'mfgr_planned_firm':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_mfg_routing.php';
			epc_mfgr_planned_firm($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Planned order firmed');

		case 'fin_periods_generate':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fin_advanced.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$fn = epc_fin_periods_generate($db_link, epc_erp_active_company_id($db_link), (int)($_POST['fy'] ?? 0), (int)($_POST['start_month'] ?? 1));
			epc_erp_json(true, 'Generated ' . $fn . ' periods');

		case 'fin_period_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fin_advanced.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			epc_fin_period_set_status($db_link, epc_erp_active_company_id($db_link), (int)($_POST['fy'] ?? 0), (int)($_POST['period_no'] ?? 0), (string)($_POST['status'] ?? 'open'));
			epc_erp_json(true, 'Period status updated');

		case 'fin_fx_revalue':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fin_advanced.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$bal = array();
			foreach (preg_split('/[\r\n]+/', (string)($_POST['balances'] ?? '')) as $line) {
				$line = trim($line);
				if ($line === '' || strpos($line, '|') === false) { continue; }
				$p = array_map('trim', explode('|', $line));
				if (count($p) < 5) { continue; }
				$bal[] = array('account' => $p[0], 'currency' => $p[1], 'fc_amount' => (float)$p[2], 'book_lc' => (float)$p[3], 'rate' => (float)$p[4]);
			}
			if (empty($bal)) { throw new Exception('Enter at least one balance line (account|currency|fc_amount|book_lc|rate)'); }
			$fxr = epc_fin_fx_revalue($db_link, epc_erp_active_company_id($db_link), $bal, time());
			epc_erp_json(true, 'Revaluation run #' . $fxr['run_id'] . ' — net delta ' . number_format($fxr['total_delta'], 2), array('run_id' => $fxr['run_id']));

		case 'fin_alloc_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fin_advanced.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$basis = array();
			foreach (preg_split('/[\r\n]+/', (string)($_POST['basis'] ?? '')) as $line) {
				$line = trim($line);
				if ($line === '' || strpos($line, '|') === false) { continue; }
				list($d, $w) = array_map('trim', explode('|', $line, 2));
				if ($d !== '') { $basis[$d] = (float)$w; }
			}
			$arId = epc_fin_alloc_rule_save($db_link, array('company_id' => epc_erp_active_company_id($db_link), 'code' => (string)($_POST['code'] ?? ''), 'name' => (string)($_POST['name'] ?? ''), 'source_account' => (string)($_POST['source_account'] ?? ''), 'basis' => $basis), (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Allocation rule saved', array('id' => $arId));

		case 'fin_alloc_run':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fin_advanced.php';
			$alLines = epc_fin_alloc_run($db_link, (int)($_POST['rule_id'] ?? 0), (float)($_POST['amount'] ?? 0));
			epc_erp_json(true, 'Allocated across ' . count($alLines) . ' destination(s)', array('lines' => $alLines));

		case 'fin_accrual_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fin_advanced.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$acId = epc_fin_accrual_save($db_link, array('company_id' => epc_erp_active_company_id($db_link), 'code' => (string)($_POST['code'] ?? ''), 'description' => (string)($_POST['description'] ?? ''), 'total_amount' => (float)($_POST['total_amount'] ?? 0), 'periods' => (int)($_POST['periods'] ?? 1), 'start_fy' => (int)($_POST['start_fy'] ?? 0), 'start_period' => (int)($_POST['start_period'] ?? 1)), (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Accrual scheme created', array('id' => $acId));

		case 'coll_case_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_collections.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$cc = $_POST;
			$cc['company_id'] = epc_erp_active_company_id($db_link);
			$ccId = epc_coll_case_save($db_link, $cc, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Case saved', array('id' => $ccId));

		case 'coll_case_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_collections.php';
			epc_coll_case_set_status($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['status'] ?? 'new'));
			epc_erp_json(true, 'Case status updated');

		case 'coll_case_promise':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_collections.php';
			$pd = (string)($_POST['promise_date'] ?? '');
			$pts = $pd !== '' ? (int)strtotime($pd) : 0;
			epc_coll_case_promise($db_link, (int)($_POST['id'] ?? 0), (float)($_POST['amount'] ?? 0), $pts);
			epc_erp_json(true, 'Promise to pay recorded');

		case 'coll_activity_log':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_collections.php';
			epc_coll_activity_log($db_link, (int)($_POST['case_id'] ?? 0), (string)($_POST['type'] ?? 'note'), (string)($_POST['outcome'] ?? ''), (float)($_POST['amount'] ?? 0), 0);
			epc_erp_json(true, 'Activity logged');

		case 'coll_dunning_run':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_collections.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$custs = array();
			foreach (preg_split('/[\r\n]+/', (string)($_POST['customers'] ?? '')) as $line) {
				$line = trim($line);
				if ($line === '' || strpos($line, '|') === false) { continue; }
				$p = array_map('trim', explode('|', $line));
				if (count($p) < 5) { continue; }
				$custs[] = array('customer_id' => (int)$p[0], 'buckets' => array('current' => 0.0, 'd1_30' => (float)$p[1], 'd31_60' => (float)$p[2], 'd61_90' => (float)$p[3], 'd90_plus' => (float)$p[4]));
			}
			if (empty($custs)) { throw new Exception('Enter at least one customer line (customerId|d1_30|d31_60|d61_90|d90_plus)'); }
			$dr = epc_coll_dunning_run($db_link, epc_erp_active_company_id($db_link), $custs);
			epc_erp_json(true, 'Dunning run #' . $dr['run_id'] . ' — ' . count($dr['entries']) . ' notice(s)', array('run_id' => $dr['run_id']));

		case 'coll_hold_set':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_collections.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			epc_coll_hold_set($db_link, epc_erp_active_company_id($db_link), (int)($_POST['customer_id'] ?? 0), (int)($_POST['place'] ?? 1) === 1, (string)($_POST['reason'] ?? ''), (string)($_SESSION['admin_username'] ?? ''));
			epc_erp_json(true, 'Credit hold updated');

		case 'proc_category_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$pcData = $_POST;
			$pcData['company_id'] = epc_erp_active_company_id($db_link);
			$pcData['active'] = isset($_POST['active']) ? $_POST['active'] : 1;
			$pcId = epc_proc_category_save($db_link, $pcData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Category saved', array('id' => $pcId));

		case 'proc_policy_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$ppData = $_POST;
			$ppData['company_id'] = epc_erp_active_company_id($db_link);
			$ppData['active'] = isset($_POST['active']) ? $_POST['active'] : 1;
			$ppId = epc_proc_policy_save($db_link, $ppData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Policy saved', array('id' => $ppId));

		case 'proc_req_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$prData = $_POST;
			$prData['company_id'] = epc_erp_active_company_id($db_link);
			$prId = epc_proc_req_save($db_link, $prData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Requisition saved', array('id' => $prId));

		case 'proc_req_add_line':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
			$prLineId = epc_proc_req_add_line($db_link, (int)($_POST['req_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Line added', array('id' => $prLineId));

		case 'proc_req_submit':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
			$prSt = epc_proc_req_submit($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, $prSt === 'approved' ? 'Requisition approved (within policy)' : 'Requisition submitted for approval');

		case 'proc_req_decision':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
			epc_proc_req_decision($db_link, (int)($_POST['id'] ?? 0), (int)($_POST['approve'] ?? 0) === 1, (string)($_SESSION['admin_username'] ?? ''), (string)($_POST['note'] ?? ''));
			epc_erp_json(true, (int)($_POST['approve'] ?? 0) === 1 ? 'Requisition approved' : 'Requisition rejected');

		case 'proc_req_convert':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
			$prPo = epc_proc_req_convert($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Converted to ' . $prPo, array('po_ref' => $prPo));

		case 'bplan_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_budget_planning.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$bpData = $_POST;
			$bpData['company_id'] = epc_erp_active_company_id($db_link);
			$bpId = epc_bplan_save($db_link, $bpData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Budget plan saved', array('id' => $bpId));

		case 'bplan_line_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_budget_planning.php';
			$bpLineId = epc_bplan_line_add($db_link, (int)($_POST['plan_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Worksheet line added', array('id' => $bpLineId));

		case 'bplan_position_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_budget_planning.php';
			$bpPosId = epc_bplan_position_add($db_link, (int)($_POST['plan_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Forecast position added', array('id' => $bpPosId));

		case 'bplan_advance':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_budget_planning.php';
			$bpStage = epc_bplan_advance_stage($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, $bpStage === 'published' ? 'Budget plan published' : 'Budget plan advanced to ' . $bpStage);

		case 'hrt_job_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$htData = $_POST;
			$htData['company_id'] = epc_erp_active_company_id($db_link);
			$htJob = epc_hrt_job_save($db_link, $htData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Job requisition saved', array('id' => $htJob));

		case 'hrt_applicant_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
			$htApp = epc_hrt_applicant_add($db_link, (int)($_POST['job_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Applicant added', array('id' => $htApp));

		case 'hrt_applicant_stage':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
			epc_hrt_applicant_set_stage($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['stage'] ?? 'applied'));
			epc_erp_json(true, 'Applicant moved to ' . (string)($_POST['stage'] ?? ''));

		case 'hrt_review_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$hrData = $_POST;
			$hrData['company_id'] = epc_erp_active_company_id($db_link);
			$htRev = epc_hrt_review_save($db_link, $hrData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Review saved', array('id' => $htRev));

		case 'hrt_goal_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
			$htGoal = epc_hrt_goal_add($db_link, (int)($_POST['review_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Goal added', array('id' => $htGoal));

		case 'hrt_review_finalize':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
			$htOverall = epc_hrt_review_finalize($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Review finalized — overall ' . number_format($htOverall, 2), array('overall' => $htOverall));

		case 'cft_forecast_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cash_treasury.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$cfData = $_POST;
			$cfData['company_id'] = epc_erp_active_company_id($db_link);
			$cfId = epc_cft_forecast_save($db_link, $cfData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Forecast saved', array('id' => $cfId));

		case 'cft_line_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cash_treasury.php';
			$cfLine = epc_cft_line_add($db_link, (int)($_POST['forecast_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Forecast line added', array('id' => $cfLine));

		case 'cft_instrument_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cash_treasury.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$ciData = $_POST;
			$ciData['company_id'] = epc_erp_active_company_id($db_link);
			$ciId = epc_cft_instrument_save($db_link, $ciData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Instrument saved', array('id' => $ciId));

		case 'cft_instrument_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cash_treasury.php';
			epc_cft_instrument_set_status($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['status'] ?? ''), (string)($_POST['detail'] ?? ''), (float)($_POST['amount'] ?? 0));
			epc_erp_json(true, 'Instrument moved to ' . (string)($_POST['status'] ?? ''));

		case 'wht_code_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_withholding.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$whtData = $_POST;
			$whtData['company_id'] = epc_erp_active_company_id($db_link);
			$whtCode = epc_wht_code_save($db_link, $whtData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Withholding code saved', array('id' => $whtCode));

		case 'wht_record':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_withholding.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$whtTx = $_POST;
			$whtTx['company_id'] = epc_erp_active_company_id($db_link);
			$whtTxId = epc_wht_record($db_link, $whtTx);
			epc_erp_json(true, 'Withholding applied', array('id' => $whtTxId));

		case 'wht_certificate':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_withholding.php';
			$whtCert = epc_wht_certificate_issue($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['certificate_no'] ?? ''));
			epc_erp_json(true, 'Certificate issued: ' . $whtCert, array('certificate_no' => $whtCert));

		case 'wht_settle':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_withholding.php';
			epc_wht_settle($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Withholding settled to authority');

		case 'er_format_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_elec_reporting.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$erData = $_POST;
			$erData['company_id'] = epc_erp_active_company_id($db_link);
			$erFmt = epc_er_format_save($db_link, $erData, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Format saved', array('id' => $erFmt));

		case 'er_field_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_elec_reporting.php';
			$erField = epc_er_field_add($db_link, (int)($_POST['format_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Field added', array('id' => $erField));

		case 'customer_master_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_credit.php';
			$cmCust = (int) ($_POST['customer_id'] ?? 0);
			if ($cmCust <= 0) {
				epc_erp_json(false, 'A customer ID is required');
			}
			epc_credit_set_master($db_link, $cmCust, $_POST);
			epc_erp_dim_save_from_post($db_link, 'customer', $cmCust, $_POST);
			epc_erp_json(true, 'Customer master saved', array('customer_id' => $cmCust));

		case 'prja_budget_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_project_accounting.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$pb = $_POST;
			$pb['company_id'] = epc_erp_active_company_id($db_link);
			$pbId = epc_prja_budget_save($db_link, $pb, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Budget line saved', array('id' => $pbId));

		case 'prja_txn_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_project_accounting.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$pt = $_POST;
			$pt['company_id'] = epc_erp_active_company_id($db_link);
			$pt['txn_date'] = time();
			$ptId = epc_prja_txn_add($db_link, $pt);
			epc_erp_json(true, 'Transaction posted', array('id' => $ptId));

		case 'prja_recognize':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_project_accounting.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$prRec = epc_prja_recognition_run($db_link, epc_erp_active_company_id($db_link), (int)($_POST['project_id'] ?? 0), (string)($_POST['method'] ?? 'poc'), (float)($_POST['fraction'] ?? 0), time());
			epc_erp_json(true, 'Recognized revenue ' . number_format($prRec['recognized_revenue'], 2) . ' · WIP ' . number_format($prRec['wip'], 2), array('rec' => $prRec));

		case 'costm_item_set':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cost_models.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			epc_costm_item_set($db_link, epc_erp_active_company_id($db_link), (int)($_POST['item_id'] ?? 0), (string)($_POST['model'] ?? 'moving_avg'), (float)($_POST['std_cost'] ?? 0));
			epc_erp_json(true, 'Costing model saved');

		case 'costm_txn_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cost_models.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$ctId = epc_costm_txn_add($db_link, epc_erp_active_company_id($db_link), (int)($_POST['item_id'] ?? 0), (string)($_POST['txn_type'] ?? 'receipt'), (float)($_POST['qty'] ?? 0), (float)($_POST['unit_cost'] ?? 0), time());
			epc_erp_json(true, 'Transaction added', array('id' => $ctId));

		case 'costm_close_run':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cost_models.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$ccRes = epc_costm_close_run($db_link, epc_erp_active_company_id($db_link), (int)($_POST['item_id'] ?? 0), (string)($_POST['label'] ?? ''));
			epc_erp_json(true, 'Closing: COGS ' . number_format($ccRes['cogs'], 2) . ' · closing value ' . number_format($ccRes['closing_value'], 2), array('res' => $ccRes));

		case 'intg_entity_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_integration.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$ieId = epc_intg_entity_save($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Data entity saved', array('id' => $ieId));

		case 'intg_sub_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_integration.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$isId = epc_intg_sub_save($db_link, epc_erp_active_company_id($db_link), (string)($_POST['event'] ?? ''), (string)($_POST['target_type'] ?? 'webhook'), (string)($_POST['target'] ?? ''), true);
			epc_erp_json(true, 'Subscription saved', array('id' => $isId));

		case 'intg_event_raise':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_integration.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$iePayload = json_decode((string)($_POST['payload'] ?? ''), true);
			if (!is_array($iePayload)) { $iePayload = array('raw' => (string)($_POST['payload'] ?? '')); }
			$ieRes = epc_intg_event_raise($db_link, epc_erp_active_company_id($db_link), (string)($_POST['event'] ?? ''), $iePayload);
			epc_erp_json(true, 'Event raised · ' . (int)$ieRes['deliveries'] . ' delivery(ies) queued', array('res' => $ieRes));

		case 'fy_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_closing.php';
			$fyStart = strtotime((string)($_POST['start_date'] ?? '')) ?: 0;
			$fyEnd = strtotime((string)($_POST['end_date'] ?? '')) ?: 0;
			if ($fyStart <= 0 || $fyEnd <= 0 || $fyEnd < $fyStart) { epc_erp_json(false, 'Valid start and end dates are required'); }
			$fyId = epc_fy_create_year($db_link, (string)($_POST['label'] ?? ''), $fyStart, $fyEnd, !empty($_POST['monthly']));
			epc_erp_json(true, 'Fiscal year created', array('id' => $fyId));

		case 'fy_close':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_closing.php';
			$fyRes = epc_fy_close_year($db_link, (int)($_POST['year_id'] ?? 0));
			epc_erp_json(true, 'Year closed · ' . $fyRes['result'] . ' net P&L ' . number_format((float)$fyRes['net_pl'], 2), array('res' => $fyRes));

		case 'fy_reopen':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_closing.php';
			epc_fy_reopen_year($db_link, (int)($_POST['year_id'] ?? 0));
			epc_erp_json(true, 'Year reopened');

		case 'fy_period_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_closing.php';
			epc_fy_set_period_status($db_link, (int)($_POST['year_id'] ?? 0), (int)($_POST['period_no'] ?? 0), (string)($_POST['status'] ?? 'open'));
			epc_erp_json(true, 'Period status updated');

		case 'plt_job_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_platform.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$pljId = epc_plt_batch_job_save($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Batch job saved', array('id' => $pljId));

		case 'plt_job_run':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_platform.php';
			$plrRes = epc_plt_batch_run($db_link, (int)($_POST['job_id'] ?? 0), (string)($_POST['status'] ?? 'ended'), (string)($_POST['message'] ?? 'Manual run'));
			epc_erp_json(true, 'Batch job executed (' . $plrRes['status'] . ')', array('res' => $plrRes));

		case 'plt_feature_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_platform.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$plfId = epc_plt_feature_save($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Feature saved', array('id' => $plfId));

		case 'oa_party_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_orgadmin.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$oapId = epc_oa_party_save($db_link, epc_erp_active_company_id($db_link), $_POST, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Party saved', array('id' => $oapId));

		case 'oa_address_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_orgadmin.php';
			$oaaId = epc_oa_address_save($db_link, (int)($_POST['party_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Address saved', array('id' => $oaaId));

		case 'oa_contact_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_orgadmin.php';
			$oacId = epc_oa_contact_save($db_link, (int)($_POST['party_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Contact saved', array('id' => $oacId));

		case 'oa_calendar_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_orgadmin.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$oaclId = epc_oa_calendar_save($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Calendar saved', array('id' => $oaclId));

		case 'oa_holiday_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_orgadmin.php';
			epc_oa_holiday_add($db_link, (int)($_POST['calendar_id'] ?? 0), (string)($_POST['holiday_date'] ?? ''), (string)($_POST['name'] ?? ''));
			epc_erp_json(true, 'Holiday added');

		case 'rbac_priv_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_rbac.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$rbpId = epc_rbac_privilege_save($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Privilege saved', array('id' => $rbpId));

		case 'rbac_duty_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_rbac.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$rbdId = epc_rbac_duty_save($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Duty saved', array('id' => $rbdId));

		case 'rbac_duty_priv':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_rbac.php';
			epc_rbac_duty_attach_priv($db_link, (int)($_POST['duty_id'] ?? 0), (int)($_POST['privilege_id'] ?? 0), (int)($_POST['attach'] ?? 1) === 1);
			epc_erp_json(true, 'Duty privileges updated');

		case 'rbac_role_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_rbac.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$rbrId = epc_rbac_role_save($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Role saved', array('id' => $rbrId));

		case 'rbac_role_duty':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_rbac.php';
			epc_rbac_role_attach_duty($db_link, (int)($_POST['role_id'] ?? 0), (int)($_POST['duty_id'] ?? 0), (int)($_POST['attach'] ?? 1) === 1);
			epc_erp_json(true, 'Role duties updated');

		case 'rbac_user_role':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_rbac.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			epc_rbac_user_assign_role($db_link, epc_erp_active_company_id($db_link), (int)($_POST['user_id'] ?? 0), (int)($_POST['role_id'] ?? 0), (int)($_POST['assign'] ?? 1) === 1);
			epc_erp_json(true, 'User role updated');

		case 'rtl_channel_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_retail.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$rcId = epc_rtl_channel_save($db_link, epc_erp_active_company_id($db_link), $_POST, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Channel saved', array('id' => $rcId));

		case 'rtl_assortment_set':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_retail.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			epc_rtl_assortment_set($db_link, epc_erp_active_company_id($db_link), (int)($_POST['channel_id'] ?? 0), (int)($_POST['item_id'] ?? 0), (int)($_POST['active'] ?? 1) === 1);
			epc_erp_json(true, 'Assortment updated');

		case 'rtl_discount_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_retail.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$rdId = epc_rtl_discount_save($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Discount saved', array('id' => $rdId));

		case 'rtl_pos_sale':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_retail.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$rsLines = array(array('item_id' => (int)($_POST['item_id'] ?? 0), 'qty' => (float)($_POST['qty'] ?? 0), 'unit_price' => (float)($_POST['unit_price'] ?? 0)));
			$rsRes = epc_rtl_pos_sale($db_link, epc_erp_active_company_id($db_link), (int)($_POST['channel_id'] ?? 0), $rsLines, (string)($_POST['tender'] ?? 'cash'), (float)($_POST['tax_rate'] ?? 0));
			epc_erp_json(true, 'Sale recorded · total ' . number_format($rsRes['total'], 2), array('res' => $rsRes));

		case 'qm_plan_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_quality.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$qpId = epc_qm_plan_save($db_link, epc_erp_active_company_id($db_link), $_POST, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Test plan saved', array('id' => $qpId));

		case 'qm_test_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_quality.php';
			$qtId = epc_qm_test_add($db_link, (int)($_POST['plan_id'] ?? 0), $_POST);
			epc_erp_json(true, 'Test added', array('id' => $qtId));

		case 'qm_order_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_quality.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$qoId = epc_qm_order_create($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Quality order created', array('id' => $qoId));

		case 'qm_order_record':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_quality.php';
			$qmVals = isset($_POST['v']) && is_array($_POST['v']) ? $_POST['v'] : array();
			$qmRec = epc_qm_order_record($db_link, (int)($_POST['order_id'] ?? 0), $qmVals);
			epc_erp_json(true, 'Results saved · verdict: ' . ($qmRec['verdict'] ?: 'n/a'), array('rec' => $qmRec));

		case 'qm_ncr_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_quality.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
			$qnId = epc_qm_ncr_create($db_link, epc_erp_active_company_id($db_link), $_POST);
			epc_erp_json(true, 'Non-conformance raised', array('id' => $qnId));

		case 'qm_ncr_update':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_quality.php';
			epc_qm_ncr_update($db_link, (int)($_POST['id'] ?? 0), $_POST);
			epc_erp_json(true, 'Non-conformance updated');

		case 'ins_claim_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_insurance.php';
			epc_ins_claim_set_status($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['status'] ?? 'notified'));
			epc_erp_json(true, 'Claim status updated');

		case 'hr_emp_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr.php';
			if (trim((string)($_POST['code'] ?? '')) === '' || trim((string)($_POST['name'] ?? '')) === '') { throw new Exception('Code and name are required'); }
			$data = $_POST;
			if (!empty($_POST['join_date_str'])) { $data['join_date'] = strtotime((string)$_POST['join_date_str']) ?: time(); }
			$empId = epc_hr_employee_save($db_link, $data, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Employee saved', array('id' => $empId));

		case 'hr_attendance':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr.php';
			$emp = (int)($_POST['employee_id'] ?? 0);
			if ($emp <= 0) { throw new Exception('Select an employee'); }
			$wd = !empty($_POST['work_date_str']) ? (strtotime((string)$_POST['work_date_str']) ?: time()) : time();
			$aId = epc_hr_attendance_log($db_link, $emp, $wd, (float)($_POST['hours'] ?? 0), (string)($_POST['status'] ?? 'present'));
			epc_erp_json(true, 'Attendance recorded', array('id' => $aId));

		case 'hr_leave_request':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr.php';
			$emp = (int)($_POST['employee_id'] ?? 0);
			if ($emp <= 0) { throw new Exception('Select an employee'); }
			$from = !empty($_POST['date_from_str']) ? (strtotime((string)$_POST['date_from_str']) ?: 0) : 0;
			$to = !empty($_POST['date_to_str']) ? (strtotime((string)$_POST['date_to_str']) ?: 0) : 0;
			$lId = epc_hr_leave_request($db_link, $emp, (string)($_POST['type'] ?? 'annual'), (float)($_POST['days'] ?? 0), $from, $to);
			epc_erp_json(true, 'Leave request submitted', array('id' => $lId));

		case 'hr_leave_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr.php';
			epc_hr_leave_set_status($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['status'] ?? 'pending'));
			epc_erp_json(true, 'Leave ' . (string)($_POST['status'] ?? ''));

		case 'hr_expense_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr.php';
			$emp = (int)($_POST['employee_id'] ?? 0);
			if ($emp <= 0) { throw new Exception('Select an employee'); }
			$hrLines = array();
			if (isset($_POST['lines']) && is_array($_POST['lines'])) {
				foreach ($_POST['lines'] as $ln) {
					if (!is_array($ln) || (float)($ln['amount'] ?? 0) == 0.0) { continue; }
					$hrLines[] = array('label' => (string)($ln['label'] ?? ''), 'amount' => (float)($ln['amount'] ?? 0));
				}
			}
			if (empty($hrLines)) { throw new Exception('Add at least one expense line'); }
			$res = epc_hr_expense_save($db_link, $emp, (string)($_POST['title'] ?? 'Expense claim'), $hrLines);
			epc_erp_json(true, 'Expense claim saved — ' . number_format((float)$res['amount'], 2) . ' AED', $res);

		case 'hr_expense_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr.php';
			epc_hr_expense_set_status($db_link, (int)($_POST['id'] ?? 0), (string)($_POST['status'] ?? 'draft'));
			epc_erp_json(true, 'Expense ' . (string)($_POST['status'] ?? ''));

		case 'prj_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_projects.php';
			if (trim((string)($_POST['code'] ?? '')) === '') { throw new Exception('Project code is required'); }
			$prjId = epc_prj_save($db_link, $_POST, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Project saved', array('id' => $prjId));

		case 'prj_task_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_projects.php';
			$prjPid = (int)($_POST['project_id'] ?? 0);
			if ($prjPid <= 0) { throw new Exception('Select a project'); }
			$tId = epc_prj_task_save($db_link, $prjPid, $_POST, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Task saved', array('id' => $tId));

		case 'prj_log_time':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_projects.php';
			$prjPid2 = (int)($_POST['project_id'] ?? 0);
			if ($prjPid2 <= 0) { throw new Exception('Select a project'); }
			$data = $_POST;
			$data['work_date'] = time();
			$data['billable'] = !empty($_POST['billable']) ? 1 : 0;
			$logId = epc_prj_log_time($db_link, $prjPid2, $data);
			epc_erp_json(true, 'Time logged', array('id' => $logId));

		case 'cons_entity_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_consolidation.php';
			$cid = epc_cons_entity_save($db_link, $_POST, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Group entity saved', array('id' => $cid));

		case 'cons_entity_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_consolidation.php';
			epc_cons_entity_delete($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Entity removed');

		case 'cons_figures_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_consolidation.php';
			epc_cons_figures_save($db_link, $_POST);
			epc_erp_json(true, 'Financials saved for ' . strtoupper((string)($_POST['entity_code'] ?? '')));

		case 'cons_ic_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_consolidation.php';
			$icid = epc_cons_ic_save($db_link, $_POST);
			epc_erp_json(true, 'Intercompany transaction recorded', array('id' => $icid));

		case 'cons_ic_delete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_consolidation.php';
			epc_cons_ic_delete($db_link, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'Intercompany transaction removed');

		case 'mfg_bom_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_manufacturing.php';
			$mfgLines = array();
			if (isset($_POST['lines']) && is_array($_POST['lines'])) {
				foreach ($_POST['lines'] as $ln) {
					if (!is_array($ln) || (int)($ln['component_item_id'] ?? 0) <= 0) { continue; }
					$mfgLines[] = array(
						'component_item_id' => (int) $ln['component_item_id'],
						'qty_per' => (float) ($ln['qty_per'] ?? 0),
						'scrap_percent' => (float) ($ln['scrap_percent'] ?? 0),
					);
				}
			}
			if ((int)($_POST['product_item_id'] ?? 0) <= 0) { throw new Exception('Select a finished product'); }
			if (empty($mfgLines)) { throw new Exception('Add at least one component'); }
			$bomId = epc_mfg_bom_save($db_link, $_POST, $mfgLines, (int)($_POST['id'] ?? 0));
			epc_erp_json(true, 'BOM saved (#' . $bomId . ') with ' . count($mfgLines) . ' component(s)', array('id' => $bomId));

		case 'mfg_wo_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_manufacturing.php';
			$woId = epc_mfg_wo_create($db_link, $_POST);
			$wo = epc_mfg_wo_get($db_link, $woId);
			epc_erp_json(true, 'Work order ' . ($wo['wo_no'] ?? ('#' . $woId)) . ' created', array('id' => $woId));

		case 'mfg_wo_issue':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_manufacturing.php';
			$res = epc_mfg_wo_issue_materials($db_link, (int)($_POST['wo_id'] ?? 0));
			epc_erp_json(true, 'Materials issued — cost ' . number_format((float)$res['material_cost'], 2) . ' AED', $res);

		case 'mfg_wo_complete':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_manufacturing.php';
			$res = epc_mfg_wo_complete($db_link, (int)($_POST['wo_id'] ?? 0), (float)($_POST['qty_produced'] ?? 0));
			epc_erp_json(true, 'Work order completed — unit cost ' . number_format((float)$res['unit_cost'], 4) . ' AED', $res);

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
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$adminId = class_exists('DP_User') ? (int)DP_User::getAdminId() : 0;
			$id = epc_erp_invoice_save($db_link, $_POST, $adminId);
			epc_erp_dim_save_from_post($db_link, 'invoice', (int) $id, $_POST);
			$siWf = epc_bos_wf_maybe_raise($db_link, 'sales_invoice', (int) $id, 'INV #' . (int) $id, $_POST);
			$doc = epc_einvoice_get_document($db_link, $id);
			$cfg = $GLOBALS['DP_Config'] ?? new DP_Config();
			$redirect = epc_erp_cp_redirect_url('/' . $cfg->backend_dir . '/shop/finance/erp?area=sales&tab=invoices&inv_id=' . $id);
			epc_erp_json(true, (!empty($doc['validation_ok']) ? 'Invoice saved and validated' : 'Invoice saved as draft — review validation') . $siWf,
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
				'serial_no' => (string) ($_POST['serial_no'] ?? ''),
				'reference' => (string) ($_POST['reference'] ?? ''),
			));
			epc_erp_json(true, 'Inventory movement recorded', array('movement_id' => $id));

		case 'ai_query':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_ai.php';
			$aiFrom = !empty($_POST['date_from']) ? (int) strtotime((string) $_POST['date_from'] . ' 00:00:00') : strtotime(date('Y-m-01'));
			$aiTo = !empty($_POST['date_to']) ? (int) strtotime((string) $_POST['date_to'] . ' 23:59:59') : time();
			$aiAns = epc_bos_ai_answer($db_link, (string) ($_POST['q'] ?? ''), $aiFrom, $aiTo);
			epc_erp_json(true, 'ok', $aiAns);

		case 'integrity_scan':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_db_integrity.php';
			epc_erp_json(true, 'Scan complete', array('relationships' => epc_erp_integrity_scan($db_link)));

		case 'integrity_apply_fks':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_db_integrity.php';
			$fkRes = epc_erp_integrity_apply_fks($db_link);
			$msg = count($fkRes['applied']) . ' foreign key(s) applied, '
				. count($fkRes['skipped']) . ' skipped'
				. (count($fkRes['errors']) ? ', ' . count($fkRes['errors']) . ' error(s)' : '');
			epc_erp_json(empty($fkRes['errors']), $msg, $fkRes);

		case 'inv_scan_lookup':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
			$item = epc_erp_inventory_find_by_barcode($db_link, (string) ($_POST['code'] ?? ''));
			if (!$item) {
				epc_erp_json(false, 'No item matches that barcode/SKU');
			}
			$onHandMap = epc_erp_inventory_on_hand_as_of($db_link, time(), 0);
			$onHand = isset($onHandMap[(int) $item['id']]) ? (float) $onHandMap[(int) $item['id']]['qty'] : 0.0;
			epc_erp_json(true, 'Item found', array(
				'item' => array(
					'id' => (int) $item['id'],
					'sku' => (string) $item['sku'],
					'name' => (string) $item['name'],
					'barcode' => (string) ($item['barcode'] ?? ''),
					'item_type' => (string) ($item['item_type'] ?? 'standard'),
				),
				'on_hand' => $onHand,
			));

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
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$id = epc_erp_expense_report_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'expense_report', (int) $id, $_POST);
			$exWf = epc_bos_wf_maybe_raise($db_link, 'expense', (int) $id, 'EXP #' . (int) $id, $_POST);
			epc_erp_json(true, 'Expense report submitted' . $exWf, array('id' => $id));

		case 'po_save':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$id = epc_erp_po_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'purchase_order', (int) $id, $_POST);
			$poWf = epc_bos_wf_maybe_raise($db_link, 'purchase_order', (int) $id, 'PO #' . (int) $id, $_POST);
			epc_erp_json(true, 'Purchase order created' . $poWf, array('id' => $id));

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
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$id = epc_erp_sales_order_save($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'sales_order', (int) $id, $_POST);
			$soWf = epc_bos_wf_maybe_raise($db_link, 'sales_order', (int) $id, 'SO #' . (int) $id, $_POST);
			epc_erp_json(true, 'Sales order saved' . $soWf, array('id' => $id));

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
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
			$r = epc_erp_receipt_voucher($db_link, $_POST);
			epc_erp_dim_save_from_post($db_link, 'cash_entry', (int) ($r['cash_entry_id'] ?? 0), $_POST);
			$rvWf = epc_bos_wf_maybe_raise($db_link, 'receipt_voucher', (int) ($r['cash_entry_id'] ?? 0), (string) ($r['voucher_no'] ?? 'RV'), $_POST);
			epc_erp_json(true, 'Receipt voucher ' . $r['voucher_no'] . ' recorded' . $rvWf, $r);

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

		/* Favourites — add / remove */
		case 'erp_fav_add':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/finance/erp/erp_nav_areas.php';
			epc_erp_ensure_favourites_table($db_link);
			$uid = 0;
			if (isset($_SESSION['user_id'])) $uid = (int)$_SESSION['user_id'];
			elseif (isset($_SESSION['admin_id'])) $uid = (int)$_SESSION['admin_id'];
			if ($uid <= 0) epc_erp_json(false, 'No user session');
			$favTab = trim((string)($_POST['tab_key'] ?? ''));
			$favArea = trim((string)($_POST['area_key'] ?? ''));
			if ($favTab === '') epc_erp_json(false, 'Missing tab_key');
			$db_link->prepare('INSERT IGNORE INTO epc_erp_favourites (user_id, area_key, tab_key, created_at) VALUES (?,?,?,?)')
				->execute(array($uid, $favArea, $favTab, time()));
			epc_erp_json(true, 'Added to favourites');

		case 'erp_fav_remove':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/finance/erp/erp_nav_areas.php';
			epc_erp_ensure_favourites_table($db_link);
			$uid = 0;
			if (isset($_SESSION['user_id'])) $uid = (int)$_SESSION['user_id'];
			elseif (isset($_SESSION['admin_id'])) $uid = (int)$_SESSION['admin_id'];
			if ($uid <= 0) epc_erp_json(false, 'No user session');
			$favTab = trim((string)($_POST['tab_key'] ?? ''));
			if ($favTab === '') epc_erp_json(false, 'Missing tab_key');
			$db_link->prepare('DELETE FROM epc_erp_favourites WHERE user_id = ? AND tab_key = ?')
				->execute(array($uid, $favTab));
			epc_erp_json(true, 'Removed from favourites');

		/* Integrated jewellery actions */
		case 'jw_repair_create':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
			epc_jw_ensure_integration_schema($db_link);
			$repairId = epc_jw_repair_save($db_link, $_POST);
			epc_erp_json($repairId > 0, $repairId > 0 ? 'Repair created' : 'Failed', array('id' => $repairId));

		case 'jw_repair_update_status':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
			epc_jw_ensure_integration_schema($db_link);
			$repId = (int)($_POST['repair_id'] ?? 0);
			$newSt = trim((string)($_POST['new_status'] ?? ''));
			if ($repId <= 0 || $newSt === '') epc_erp_json(false, 'Invalid parameters');
			$allowed = array('received', 'in_progress', 'ready', 'delivered', 'invoiced');
			if (!in_array($newSt, $allowed, true)) epc_erp_json(false, 'Invalid status');
			$db_link->prepare('UPDATE epc_erp_jw_repairs SET status = ?, updated_at = ? WHERE id = ?')
				->execute(array($newSt, time(), $repId));
			epc_erp_json(true, 'Status updated to ' . $newSt);

		case 'jw_seed_sample_data':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
			epc_jw_ensure_integration_schema($db_link);
			$adminId = 0;
			if (isset($_SESSION['user_id'])) $adminId = (int)$_SESSION['user_id'];
			elseif (isset($_SESSION['admin_id'])) $adminId = (int)$_SESSION['admin_id'];
			$seeded = epc_jw_seed_sample_data($db_link, $adminId);
			epc_erp_json(true, 'Sample data seeded', array('seeded' => $seeded));

		case 'ai_assistant_query':
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ai_assistant.php';
			$question = trim((string)($_POST['question'] ?? ''));
			if ($question === '') epc_erp_json(false, 'No question provided');
			$result = epc_ai_assistant_query($db_link, $question);
			epc_erp_json(true, 'OK', $result);

		default:
			/* Jewellery ERP actions — jw_* prefix */
			if (strpos($action, 'jw_') === 0) {
				require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
				require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
				epc_jewel_ensure_schema($db_link);
				$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
				$r = epc_jewel_handle_ajax($db_link, $action, $_POST, $companyId);
				$isXhr = isset($_SERVER['HTTP_X_REQUESTED_WITH'])
					&& strtolower((string)$_SERVER['HTTP_X_REQUESTED_WITH']) === 'xmlhttprequest';
				if (!$isXhr && !empty($_SERVER['HTTP_REFERER'])) {
					header('Location: ' . $_SERVER['HTTP_REFERER'], true, 303);
					exit;
				}
				epc_erp_json((bool)$r['ok'], $r['message'] ?? 'OK', $r);
			}
			/* CRM actions */
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
