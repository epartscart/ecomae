<?php
/**
 * Jewellery ERP — Repair Delivery (RTD).
 * Deliver repaired items back to customer.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-gift"></i> Repair Delivery', 'Deliver repaired items to customer.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair delivery'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Delivery - (RTD)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Deliver</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find Repair</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_repair_delivery_save">

		<div class="ef-section">
			<span class="ef-section-title">Delivery Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Delivery Date</label><input name="delivery_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Delivery No.</label><input name="delivery_no" class="ef-readonly" readonly placeholder="Auto" style="width:80px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Repair No.</label><input name="repair_no" required placeholder="REP-xxxx"></div>
				<div class="ef-field"><label>Customer Code</label><input name="customer_code" class="ef-readonly" readonly></div>
				<div class="ef-field ef-field-wide"><label>Customer Name</label><input name="customer_name" class="ef-readonly" readonly></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Delivery Items</span>
			<table class="ef-grid">
				<thead><tr><th>No.</th><th>Item Desc</th><th>Metal</th><th>Gross Wt</th><th>Repair Done</th><th>Actual Cost</th><th>Customer OK</th></tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 4; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="items[<?php echo $r; ?>][description]" style="min-width:140px"></td>
					<td><input name="items[<?php echo $r; ?>][metal]" style="width:30px"></td>
					<td><input name="items[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0"></td>
					<td><input name="items[<?php echo $r; ?>][repair_done]" style="width:100px"></td>
					<td><input name="items[<?php echo $r; ?>][actual_cost]" type="number" step="0.01" value="0"></td>
					<td><select name="items[<?php echo $r; ?>][customer_ok]"><option value="">—</option><option value="Yes">Yes</option><option value="No">No</option></select></td>
				</tr>
				<?php endfor; ?>
				</tbody>
				<tfoot><tr><td colspan="5" style="text-align:right"><strong>Total:</strong></td><td><input name="total_cost" type="number" step="0.01" value="0" class="ef-readonly" readonly></td><td></td></tr></tfoot>
			</table>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Payment</span>
			<div class="ef-row">
				<div class="ef-field"><label>Total Charge</label><input name="total_charge" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Less Advance</label><input name="advance_paid" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Balance Due</label><input name="balance_due" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Pay Mode</label><select name="pay_mode"><option value="Cash">Cash</option><option value="Card">Card</option><option value="Bank">Bank</option></select></div>
				<div class="ef-field"><label>Amount Paid</label><input name="amount_paid" type="number" step="0.01" value="0"></div>
			</div>
			<div class="ef-checks">
				<label><input type="checkbox" name="customer_received" value="1"> Customer received & acknowledged</label>
			</div>
		</div>

		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-check"></i> Complete Delivery</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print Receipt</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Repair Delivery — RTD</span></div>
</div>
