<?php
/**
 * Jewellery ERP — Repair Receipt (REP).
 * Customer brings jewellery for repair — receive, log, estimate.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-wrench"></i> Repair Receipt', 'Customer repair reception with item details and estimate.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair receipt'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Receipt - (REP)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print Job Card</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-exchange"></i> Transfer to Workshop</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_repair_save">

		<div class="ef-section">
			<span class="ef-section-title">Receipt Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Receipt Date</label><input name="receipt_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Repair No.</label><input name="repair_no" class="ef-readonly" readonly placeholder="Auto" style="width:80px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Customer Code</label><input name="customer_code" required></div>
				<div class="ef-field ef-field-wide"><label>Customer Name</label><input name="customer_name"></div>
				<div class="ef-field"><label>Mobile</label><input name="mobile"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Salesman</label><input name="salesman"></div>
				<div class="ef-field"><label>Promise Date</label><input name="promise_date" type="date"></div>
				<div class="ef-field"><label>Priority</label><select name="priority"><option value="Normal">Normal</option><option value="Urgent">Urgent</option><option value="Express">Express</option></select></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Repair Items</span>
			<table class="ef-grid">
				<thead><tr>
					<th>No.</th><th>Item Description</th><th>Metal</th><th>Karat</th>
					<th>Gross Wt</th><th>Repair Type</th><th>Est. Cost</th><th>Remarks</th>
				</tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 4; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="items[<?php echo $r; ?>][description]" placeholder="Ring / Bracelet / Chain" style="min-width:140px"></td>
					<td><select name="items[<?php echo $r; ?>][metal]"><option value="">—</option><option value="G">G</option><option value="S">S</option><option value="T">T</option></select></td>
					<td><input name="items[<?php echo $r; ?>][karat]" style="width:30px"></td>
					<td><input name="items[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0"></td>
					<td><select name="items[<?php echo $r; ?>][repair_type]"><option value="">—</option><option>Resize</option><option>Polish</option><option>Rhodium</option><option>Stone Setting</option><option>Solder</option><option>Chain Repair</option><option>Clasp</option><option>Engrave</option><option>Other</option></select></td>
					<td><input name="items[<?php echo $r; ?>][est_cost]" type="number" step="0.01" value="0"></td>
					<td><input name="items[<?php echo $r; ?>][remarks]" style="min-width:100px"></td>
				</tr>
				<?php endfor; ?>
				</tbody>
				<tfoot><tr><td colspan="6" style="text-align:right"><strong>Total Est.:</strong></td><td><input name="total_est_cost" type="number" step="0.01" value="0" class="ef-readonly" readonly></td><td></td></tr></tfoot>
			</table>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Customer Acknowledgement</span>
			<div class="ef-checks">
				<label><input type="checkbox" name="photo_taken" value="1"> Photo taken on receipt</label>
				<label><input type="checkbox" name="customer_signed" value="1"> Customer signed</label>
				<label><input type="checkbox" name="advance_received" value="1"> Advance received</label>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Advance Amt</label><input name="advance_amt" type="number" step="0.01" value="0"></div>
			</div>
			<textarea name="narration" class="ef-narration" placeholder="Special instructions / customer notes"></textarea>
		</div>

		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print Job Card</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Repair Receipt — REP</span></div>
</div>
