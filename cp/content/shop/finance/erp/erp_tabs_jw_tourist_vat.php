<?php
/**
 * Jewellery ERP — Tourist VAT Refund (VRV).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-plane"></i> Tourist VAT Refund', 'Tourist VAT refund verification and processing.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Tourist VAT refund'),
));
?>
<div class="ef-window">
	<div class="ef-title">Tourist VAT Refund - (VRV)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-check"></i> Submit to FTA</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_tourist_vat_save">

		<div class="ef-section">
			<span class="ef-section-title">Tourist Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Refund Date</label><input name="refund_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Refund No.</label><input name="refund_no" class="ef-readonly" readonly placeholder="Auto" style="width:80px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field ef-field-wide"><label>Tourist Name</label><input name="tourist_name" required></div>
				<div class="ef-field"><label>Passport #</label><input name="passport_no" required></div>
				<div class="ef-field"><label>Nationality</label><input name="nationality"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Flight / Departure</label><input name="flight_no"></div>
				<div class="ef-field"><label>Departure Date</label><input name="departure_date" type="date"></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Eligible Invoices</span>
			<table class="ef-grid">
				<thead><tr><th>No.</th><th>Invoice No.</th><th>Date</th><th>Net Amount</th><th>VAT Amount</th><th>Refund Eligible</th><th>Status</th></tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 5; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="invoices[<?php echo $r; ?>][invoice_no]"></td>
					<td><input name="invoices[<?php echo $r; ?>][date]" type="date"></td>
					<td><input name="invoices[<?php echo $r; ?>][net_amount]" type="number" step="0.01" value="0"></td>
					<td><input name="invoices[<?php echo $r; ?>][vat_amount]" type="number" step="0.01" value="0"></td>
					<td><input name="invoices[<?php echo $r; ?>][refund_amt]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td><select name="invoices[<?php echo $r; ?>][status]"><option value="">—</option><option value="Pending">Pending</option><option value="Verified">Verified</option><option value="Submitted">Submitted</option><option value="Refunded">Refunded</option></select></td>
				</tr>
				<?php endfor; ?>
				</tbody>
				<tfoot><tr><td colspan="4" style="text-align:right;font-weight:700">Total Refund:</td><td><input name="total_vat" type="number" step="0.01" value="0" class="ef-readonly" readonly></td><td><input name="total_refund" type="number" step="0.01" value="0" class="ef-readonly" readonly></td><td></td></tr></tfoot>
			</table>
		</div>

		<div class="ef-section"><span class="ef-section-title">Narration</span><textarea name="narration" class="ef-narration" placeholder="Refund processing notes"></textarea></div>
		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-success btn-sm"><i class="fa fa-check"></i> Submit to FTA</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Tourist VAT Refund — VRV</span></div>
</div>
