<?php
/**
 * Jewellery ERP — Repair Sale / Invoice (RSL).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-file-text-o"></i> Repair Sale', 'Invoice repair charges to customer.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair sale'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Sale / Invoice - (RSL)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print Invoice</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_voucher_save">
		<input type="hidden" name="voc_type" value="RSL">

		<div class="ef-section">
			<span class="ef-section-title">Invoice Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Inv. Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Inv. No.</label><input name="voc_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Repair No.</label><input name="repair_ref" required placeholder="REP-xxxx"></div>
				<div class="ef-field"><label>Customer Code</label><input name="party_code"></div>
				<div class="ef-field ef-field-wide"><label>Customer Name</label><input name="party_name"></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Charge Lines</span>
			<table class="ef-grid">
				<thead><tr><th>No.</th><th>Description</th><th>Charge Type</th><th>Qty</th><th>Rate</th><th>Amount</th><th>VAT</th><th>Total</th></tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 5; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="lines[<?php echo $r; ?>][description]" style="min-width:140px"></td>
					<td><select name="lines[<?php echo $r; ?>][charge_type]"><option value="">—</option><option>Labour</option><option>Metal Added</option><option>Stone Setting</option><option>Polish</option><option>Rhodium</option><option>Engrave</option><option>Other</option></select></td>
					<td><input name="lines[<?php echo $r; ?>][qty]" type="number" value="1" style="width:40px"></td>
					<td><input name="lines[<?php echo $r; ?>][rate]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][amount]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][vat]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][total]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
				</tr>
				<?php endfor; ?>
				</tbody>
			</table>
		</div>

		<div style="display:flex;justify-content:flex-end;">
			<div class="ef-section" style="min-width:280px;">
				<span class="ef-section-title">Summary</span>
				<div class="ef-totals">
					<div class="ef-tot-row"><label>Net Amount</label><input name="net_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>VAT (5%)</label><input name="vat_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Less Advance</label><input name="advance_adj" type="number" step="0.01" value="0"></div>
					<div class="ef-tot-row" style="border-bottom:2px solid #333"><label><strong>BALANCE DUE</strong></label><input name="balance_due" type="number" step="0.01" value="0" class="ef-readonly" readonly style="font-weight:700"></div>
				</div>
			</div>
		</div>

		<div class="ef-section"><span class="ef-section-title">Narration</span><textarea name="narration" class="ef-narration"></textarea></div>
		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print Invoice</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Voc Type: RSL — Repair Sale</span></div>
</div>
