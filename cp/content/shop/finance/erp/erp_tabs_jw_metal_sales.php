<?php
/**
 * Jewellery ERP — Metal Sales (MSL).
 * Bulk metal sales to dealers/wholesalers.
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

erp_page_header('<i class="fa fa-arrow-up"></i> Metal Sales', 'Bulk metal sales to dealers/wholesalers.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Metal sales'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Sales - (MSL)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_voucher_save">
		<input type="hidden" name="voc_type" value="MSL">

		<div class="ef-section">
			<span class="ef-section-title">Header Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Voc Type</label><input value="MSL" class="ef-readonly" readonly style="width:50px;background:#e8e8e8"></div>
				<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Voc No.</label><input name="voc_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Party Code</label><input name="party_code" required></div>
				<div class="ef-field ef-field-wide"><label>Party Name</label><input name="party_name"></div>
				<div class="ef-field"><label>Currency</label><select name="currency"><option value="AED">AED</option><option value="USD">USD</option></select></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Salesman</label><input name="salesman"></div>
				<div class="ef-field"><label>Ref / Inv#</label><input name="ref_invoice_no"></div>
				<div class="ef-field"><label>Credit Days</label><input name="credit_days" type="number" value="0" style="width:50px"></div>
			</div>
			<div class="ef-checks">
				<label><input type="checkbox" name="rate_fixed" value="1" checked> Fixed Rate</label>
				<label><input type="checkbox" name="rate_floating" value="1"> Floating Rate</label>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Sales Lines</span>
			<table class="ef-grid">
				<thead><tr>
					<th>No.</th><th>Metal</th><th>Karat</th><th>Purity</th>
					<th>Gross Wt</th><th>Net Wt</th><th>Pure Wt</th>
					<th>Rate/Gm</th><th>Metal Amt</th><th>Making Amt</th><th>Line Total</th>
				</tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 5; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><select name="lines[<?php echo $r; ?>][metal]"><option value="">—</option><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($c); ?></option><?php endforeach; ?></select></td>
					<td><input name="lines[<?php echo $r; ?>][karat]" style="width:30px"></td>
					<td><input name="lines[<?php echo $r; ?>][purity]" type="number" step="0.000001" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][net_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][pure_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][rate_gm]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][metal_amt]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][making_amt]" type="number" step="0.01" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][line_total]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
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
					<div class="ef-tot-row"><label>VAT</label><input name="vat_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
					<div class="ef-tot-row"><label>Round Off</label><input name="round_off" type="number" step="0.01" value="0"></div>
					<div class="ef-tot-row" style="border-bottom:2px solid #333"><label><strong>GROSS TOTAL</strong></label><input name="gross_total" type="number" step="0.01" value="0" class="ef-readonly" readonly style="font-weight:700"></div>
				</div>
			</div>
		</div>

		<div class="ef-section"><span class="ef-section-title">Narration</span><textarea name="narration" class="ef-narration"></textarea></div>
		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Print</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Voc Type: MSL — Metal Sales</span></div>
</div>
