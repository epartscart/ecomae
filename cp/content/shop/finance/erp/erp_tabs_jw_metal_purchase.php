<?php
/**
 * Jewellery ERP — Metal Purchase (RMP).
 * Purchase voucher for raw metal (gold/silver/platinum) from suppliers.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$divisions = epc_jewel_divisions();

erp_page_header('<i class="fa fa-arrow-down"></i> Metal Purchase', 'Raw metal purchase with fixed/floating rate, supplier invoice and VAT.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Metal purchase'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Purchase - (RMP)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-check-square"></i> Approve</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_voucher_save">
		<input type="hidden" name="voc_type" value="RMP">

		<!-- Header Details -->
		<div class="ef-section">
			<span class="ef-section-title">Header Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option><option value="B1">B1</option><option value="B2">B2</option></select></div>
				<div class="ef-field"><label>Voc Type</label><input name="voc_type_display" value="RMP" class="ef-readonly" readonly style="width:50px;background:#e8e8e8"></div>
				<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Voc No.</label><input name="voc_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Party Code</label><input name="party_code" required placeholder="SUP001"></div>
				<div class="ef-field ef-field-wide"><label>Party Name</label><input name="party_name" placeholder="Supplier name"></div>
				<div class="ef-field"><label>Currency</label><select name="currency"><option value="AED">AED</option><option value="USD">USD</option></select></div>
				<div class="ef-field ef-field-narrow"><label>Rate</label><input name="currency_rate" type="number" step="0.000001" value="1.000000"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Salesman</label><input name="salesman"></div>
				<div class="ef-field"><label>Ref / Inv#</label><input name="ref_invoice_no" placeholder="Supplier invoice"></div>
				<div class="ef-field"><label>Credit Days</label><input name="credit_days" type="number" value="0" style="width:50px"></div>
				<div class="ef-field"><label>Due Date</label><input name="due_date" type="date"></div>
			</div>
			<div class="ef-checks">
				<label><input type="checkbox" name="rate_fixed" value="1" checked> Fixed Rate</label>
				<label><input type="checkbox" name="rate_floating" value="1"> Floating Rate</label>
				<label><input type="checkbox" name="apply_vat" value="1" checked> Apply VAT</label>
			</div>
		</div>

		<!-- Line Items -->
		<div class="ef-section">
			<span class="ef-section-title">Purchase Lines</span>
			<table class="ef-grid">
				<thead><tr>
					<th>No.</th><th>Metal</th><th>Karat</th><th>Purity</th>
					<th>Gross Wt</th><th>Stone Wt</th><th>Net Wt</th><th>Pure Wt</th>
					<th>Rate/Gm</th><th>Metal Amt</th><th>Making/Gm</th><th>Making Amt</th>
					<th>Stone Amt</th><th>Wastage%</th><th>Line Total</th>
				</tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 5; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><select name="lines[<?php echo $r; ?>][metal]"><option value="">—</option><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($c); ?></option><?php endforeach; ?></select></td>
					<td><input name="lines[<?php echo $r; ?>][karat]" style="width:30px"></td>
					<td><input name="lines[<?php echo $r; ?>][purity]" type="number" step="0.000001" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][stone_wt]" type="number" step="0.001" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][net_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][pure_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][rate_gm]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][metal_amt]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][making_gm]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][making_amt]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][stone_amt]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][wastage_pct]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][line_total]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
				</tr>
				<?php endfor; ?>
				</tbody>
				<tfoot><tr>
					<td colspan="4" style="text-align:right"><strong>Totals:</strong></td>
					<td><input name="total_gross_wt" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="total_stone_wt" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="total_net_wt" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="total_pure_wt" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td></td>
					<td><input name="total_metal_amt" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td></td>
					<td><input name="total_making_amt" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td><input name="total_stone_amt" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td></td>
					<td><input name="sub_total" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
				</tr></tfoot>
			</table>
		</div>

		<!-- Totals -->
		<div style="display:flex;justify-content:flex-end;">
			<div class="ef-section" style="min-width:280px;">
				<span class="ef-section-title">Summary</span>
				<div class="ef-totals">
					<div class="ef-tot-row"><label>Net Amount</label><input name="net_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>VAT (5%)</label><input name="vat_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Round Off</label><input name="round_off" type="number" step="0.01" value="0"></div>
					<div class="ef-tot-row" style="border-bottom:2px solid #333;font-size:14px"><label><strong>GROSS TOTAL</strong></label><input name="gross_total" type="number" step="0.01" value="0" class="ef-readonly" readonly style="font-weight:700;font-size:14px"></div>
				</div>
			</div>
		</div>

		<!-- Narration -->
		<div class="ef-section">
			<span class="ef-section-title">Narration</span>
			<textarea name="narration" class="ef-narration" placeholder="Purchase remarks"></textarea>
		</div>

		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Voc Type: RMP — Metal Purchase</span></div>
</div>
