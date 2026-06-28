<?php
/**
 * Jewellery ERP — POS Advance.
 * Ref: Suntech POS Advance screenshot (header, line items, receipt detail section).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$advances = epc_jewel_advance_list($db_link, $companyId);

erp_page_header('<i class="fa fa-money"></i> POS Advance', 'Customer advance receipts and order bookings.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'POS advance'),
));
?>
<div class="ef-window">
	<div class="ef-title">POS Advance</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_adv_form').style.display='block'"><i class="fa fa-plus"></i> New Advance</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Rcpt No</th><th>Rcpt Date</th><th>Customer</th>
				<th>Mobile</th><th>Amount</th><th>Balance</th><th>Status</th>
			</tr></thead>
			<tbody>
			<?php if (empty($advances)): ?>
				<tr><td colspan="8" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($advances as $a): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($a['voc_no']); ?></strong></td>
					<td><?php echo epc_erp_h($a['voc_date']); ?></td>
					<td><?php echo epc_erp_h($a['party_code']); ?></td>
					<td><?php echo epc_erp_h($a['customer_mobile'] ?? ''); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$a['net_amount'], 2); ?></td>
					<td style="text-align:right"><?php echo number_format((float)($a['balance'] ?? $a['net_amount']), 2); ?></td>
					<td><?php echo epc_erp_h($a['status'] ?? 'OPEN'); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_adv_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_pos_advance_save">

			<div class="ef-section">
				<span class="ef-section-title">Advance Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Voc Type</label><input name="voc_type" maxlength="5" value="ADV" readonly></div>
					<div class="ef-field"><label>Rcpt Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Rcpt No</label><input name="voc_no" maxlength="20" placeholder="Auto"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Customer Code</label><input name="party_code" maxlength="20" required></div>
					<div class="ef-field"><label>Customer Name</label><input name="party_name" maxlength="80"></div>
					<div class="ef-field"><label>Mobile</label><input name="customer_mobile" maxlength="20"></div>
					<div class="ef-field"><label>Salesman</label><input name="salesman" maxlength="20"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Order Items</span>
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Item Description</th><th>Metal</th><th>Karat</th>
						<th>Est Wt</th><th>Est Amount</th><th>Remarks</th>
					</tr></thead>
					<tbody>
						<tr>
							<td>1</td>
							<td><input name="li_description" maxlength="120" style="width:160px"></td>
							<td><input name="li_metal" maxlength="2" value="G" style="width:30px"></td>
							<td><input name="li_karat" maxlength="10" style="width:40px"></td>
							<td><input name="li_est_wt" type="number" step="0.001" value="0.000" style="width:70px"></td>
							<td><input name="li_est_amount" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="li_remarks" maxlength="120" style="width:120px"></td>
						</tr>
					</tbody>
				</table>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Receipt Detail</span>
				<div class="ef-row">
					<div class="ef-field"><label>Payment Mode</label>
						<select name="payment_mode"><option value="CASH">Cash</option><option value="CARD">Card</option><option value="BANK">Bank Transfer</option><option value="CHEQUE">Cheque</option></select>
					</div>
					<div class="ef-field"><label>Amount Received</label><input name="amount_received" type="number" step="0.01" value="0.00" required></div>
					<div class="ef-field"><label>Currency</label><input name="currency" maxlength="5" value="AED"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Card No (Last 4)</label><input name="card_no" maxlength="4"></div>
					<div class="ef-field"><label>Cheque No</label><input name="cheque_no" maxlength="20"></div>
					<div class="ef-field"><label>Bank Ref</label><input name="bank_ref" maxlength="30"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Narration</span>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><textarea name="narration" rows="2" maxlength="500" style="width:100%"></textarea></div>
				</div>
			</div>

			<div class="ef-totals">
				<div class="ef-row">
					<div class="ef-field"><label>Estimated Total</label><input name="est_total" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>Amount Received</label><input name="total_received" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>Balance Due</label><input name="balance_due" type="number" step="0.01" value="0.00" readonly></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_adv_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record &rarr; Function Key (F5)</span>
	</div>
</div>
