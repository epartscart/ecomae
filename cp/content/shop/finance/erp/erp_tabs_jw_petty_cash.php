<?php
/**
 * Jewellery ERP — Petty Cash.
 * Ref: Suntech Petty Cash screenshot (voucher header + line items + narration + balance).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$vouchers = epc_jewel_petty_cash_list($db_link, $companyId, date('Y-01-01'), date('Y-m-d'));

erp_page_header('<i class="fa fa-money"></i> Petty Cash', 'Petty cash vouchers with expense line items.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Petty cash'),
));
?>
<div class="ef-window">
	<div class="ef-title">Petty Cash</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_pc_form').style.display='block'"><i class="fa fa-plus"></i> New Voucher</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Voc No</th><th>Voc Date</th><th>Paid To</th>
				<th>Account</th><th>Amount</th><th>Type</th>
			</tr></thead>
			<tbody>
			<?php if (empty($vouchers)): ?>
				<tr><td colspan="7" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($vouchers as $v): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($v['voc_no']); ?></strong></td>
					<td><?php echo epc_erp_h($v['voc_date']); ?></td>
					<td><?php echo epc_erp_h($v['paid_to']); ?></td>
					<td><?php echo epc_erp_h($v['account_code']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$v['amount'], 2); ?></td>
					<td><?php echo epc_erp_h($v['voc_type']); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_pc_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_petty_cash_save">

			<div class="ef-section">
				<span class="ef-section-title">Voucher Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Voc Type</label>
						<select name="voc_type"><option value="PAY">Payment</option><option value="RCV">Receipt</option></select>
					</div>
					<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Voc No</label><input name="voc_no" maxlength="20" placeholder="Auto"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Paid To / Received From</label><input name="paid_to" maxlength="80" required></div>
					<div class="ef-field"><label>Account Code</label><input name="account_code" maxlength="20" required placeholder="Expense A/C"></div>
					<div class="ef-field"><label>Account Name</label><input name="account_name" maxlength="80" readonly></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Expense Lines</span>
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Expense Type</th><th>Description</th><th>Amount</th><th>VAT</th><th>Total</th>
					</tr></thead>
					<tbody>
						<tr>
							<td>1</td>
							<td><select name="li_expense_type" style="width:100px"><option value="Office">Office</option><option value="Travel">Travel</option><option value="Repair">Repair</option><option value="Supplies">Supplies</option><option value="Misc">Misc</option></select></td>
							<td><input name="li_description" maxlength="200" style="width:200px"></td>
							<td><input name="li_amount" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="li_vat" type="number" step="0.01" value="0.00" style="width:60px"></td>
							<td><input name="li_total" type="number" step="0.01" value="0.00" style="width:80px" readonly></td>
						</tr>
						<tr>
							<td>2</td>
							<td><select name="li_expense_type_2" style="width:100px"><option value="">--</option><option value="Office">Office</option><option value="Travel">Travel</option><option value="Repair">Repair</option><option value="Supplies">Supplies</option><option value="Misc">Misc</option></select></td>
							<td><input name="li_description_2" maxlength="200" style="width:200px"></td>
							<td><input name="li_amount_2" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="li_vat_2" type="number" step="0.01" value="0.00" style="width:60px"></td>
							<td><input name="li_total_2" type="number" step="0.01" value="0.00" style="width:80px" readonly></td>
						</tr>
					</tbody>
				</table>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Narration</span>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><textarea name="narration" rows="2" maxlength="500" style="width:100%"></textarea></div>
				</div>
			</div>

			<div class="ef-totals">
				<div class="ef-row">
					<div class="ef-field"><label>Total Amount</label><input name="total_amount" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>Total VAT</label><input name="total_vat" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>Grand Total</label><input name="grand_total" type="number" step="0.01" value="0.00" readonly style="font-weight:bold"></div>
					<div class="ef-field"><label>Cash Balance</label><input name="cash_balance" type="number" step="0.01" value="0.00" readonly></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_pc_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Petty Cash Voucher</span>
	</div>
</div>
