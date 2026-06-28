<?php
/**
 * Jewellery ERP — Metal Sales.
 * Ref: Suntech Metal Sales screenshot (header, line items grid, narration, other amounts).
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
$sales = epc_jewel_sale_list($db_link, $companyId, 'METAL');

erp_page_header('<i class="fa fa-truck"></i> Metal Sales', 'Metal sales vouchers with line items and other amounts.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Metal sales'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Sales</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_msl_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Voc No</th><th>Voc Date</th><th>Party Code</th>
				<th>Metal</th><th>Net Amt</th>
			</tr></thead>
			<tbody>
			<?php if (empty($sales)): ?>
				<tr><td colspan="6" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($sales as $s): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($s['voc_no']); ?></strong></td>
					<td><?php echo epc_erp_h($s['voc_date']); ?></td>
					<td><?php echo epc_erp_h($s['party_code']); ?></td>
					<td><?php echo epc_erp_h($divisions[$s['metal']] ?? $s['metal']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$s['net_amount'], 2); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_msl_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_metal_sale_save">

			<div class="ef-section">
				<span class="ef-section-title">Voucher Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Voc Type</label><input name="voc_type" maxlength="5" value="MSL" readonly></div>
					<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Voc No</label><input name="voc_no" maxlength="20" placeholder="Auto"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Party Code</label><input name="party_code" maxlength="20" required></div>
					<div class="ef-field"><label>Party Name</label><input name="party_name" maxlength="80" readonly></div>
					<div class="ef-field"><label>Party Curr</label><input name="party_currency" maxlength="5" value="AED"></div>
					<div class="ef-field"><label>Salesman</label><input name="salesman" maxlength="20"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label><input type="checkbox" name="fixed_rate" value="1"> Fixed Rate</label></div>
					<div class="ef-field"><label>Metal Rate (GMS)</label><input name="metal_rate_gms" type="number" step="0.00001" value="0.00000"></div>
					<div class="ef-field"><label>Cr. Days</label><input name="cr_days" type="number" value="0"></div>
					<div class="ef-field"><label>Metal</label>
						<select name="metal"><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($l); ?></option><?php endforeach; ?></select>
					</div>
				</div>
			</div>

			<div class="ef-tabs">
				<button type="button" class="ef-tab active" onclick="jwMslTab(this,'items')">1. Line Items</button>
				<button type="button" class="ef-tab" onclick="jwMslTab(this,'other')">2. Other Amounts</button>
			</div>

			<div id="jw_msl_items" class="ef-tab-pane">
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Item Code</th><th>Karat</th><th>Purity</th>
						<th>Pcs</th><th>Gross Wt</th><th>Net Wt</th><th>Rate Type</th>
						<th>Metal Rate</th><th>MC Type</th><th>MC Rate</th><th>MC Amount</th><th>Amount</th>
					</tr></thead>
					<tbody>
						<tr>
							<td>1</td>
							<td><input name="li_item_code" maxlength="20" style="width:70px"></td>
							<td><input name="li_karat" maxlength="10" style="width:40px"></td>
							<td><input name="li_purity" type="number" step="0.000001" value="0.000000" style="width:70px"></td>
							<td><input name="li_pcs" type="number" value="0" style="width:40px"></td>
							<td><input name="li_gross_wt" type="number" step="0.001" value="0.000" style="width:70px"></td>
							<td><input name="li_net_wt" type="number" step="0.001" value="0.000" style="width:70px"></td>
							<td><input name="li_rate_type" maxlength="10" value="GMS" style="width:50px"></td>
							<td><input name="li_metal_rate" type="number" step="0.01" value="0.00" style="width:70px"></td>
							<td><select name="li_mc_type" style="width:50px"><option value="FIX">FIX</option><option value="PCT">PCT</option><option value="PGM">PGM</option></select></td>
							<td><input name="li_mc_rate" type="number" step="0.01" value="0.00" style="width:60px"></td>
							<td><input name="li_mc_amount" type="number" step="0.01" value="0.00" style="width:70px"></td>
							<td><input name="li_amount" type="number" step="0.01" value="0.00" style="width:80px"></td>
						</tr>
					</tbody>
				</table>
			</div>

			<div id="jw_msl_other" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Other Amounts</span>
					<div class="ef-row">
						<div class="ef-field"><label>Discount</label><input name="discount_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Freight</label><input name="freight_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Insurance</label><input name="insurance_amount" type="number" step="0.01" value="0.00"></div>
					</div>
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
					<div class="ef-field"><label>Total Pcs</label><input name="total_pcs" type="number" value="0" readonly></div>
					<div class="ef-field"><label>Total Gross Wt</label><input name="total_gross_wt" type="number" step="0.001" value="0.000" readonly></div>
					<div class="ef-field"><label>Net Amount</label><input name="net_amount" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>VAT</label><input name="vat_amount" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Gross Total</label><input name="gross_total" type="number" step="0.01" value="0.00" readonly></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_msl_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record &rarr; Function Key (F5)</span>
	</div>
</div>
<script>
function jwMslTab(btn, pane){
	document.querySelectorAll('.ef-tab').forEach(function(t){t.classList.remove('active');});
	btn.classList.add('active');
	['items','other'].forEach(function(p){
		document.getElementById('jw_msl_'+p).style.display=(p===pane)?'block':'none';
	});
}
</script>
