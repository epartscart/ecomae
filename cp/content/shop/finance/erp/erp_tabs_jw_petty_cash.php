<?php
/**
 * Jewellery ERP — Petty Cash Voucher (PCV).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-money"></i> Petty Cash', 'Petty cash journal entries.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Petty cash'),
));
?>
<div class="ef-window">
	<div class="ef-title">Petty Cash Voucher - (PCV)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_petty_cash_save">

		<div class="ef-section">
			<span class="ef-section-title">Voucher Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Voc No.</label><input name="voc_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Cash Account</label><input name="cash_account" value="PETTY-CASH"></div>
				<div class="ef-field"><label>Pay To</label><input name="pay_to" placeholder="Paid to person"></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Expense Lines</span>
			<table class="ef-grid">
				<thead><tr><th>No.</th><th>Expense Account</th><th>Description</th><th>Amount</th></tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 5; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="lines[<?php echo $r; ?>][account]" placeholder="Stationery / Transport / etc"></td>
					<td><input name="lines[<?php echo $r; ?>][description]" style="min-width:200px"></td>
					<td><input name="lines[<?php echo $r; ?>][amount]" type="number" step="0.01" value="0"></td>
				</tr>
				<?php endfor; ?>
				</tbody>
				<tfoot><tr><td colspan="3" style="text-align:right;font-weight:700">Total:</td><td><input name="total" type="number" step="0.01" value="0" class="ef-readonly" readonly></td></tr></tfoot>
			</table>
		</div>

		<div class="ef-section"><span class="ef-section-title">Narration</span><textarea name="narration" class="ef-narration"></textarea></div>
		<div class="ef-actions"><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button></div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Petty Cash — PCV</span></div>
</div>
