<?php
/**
 * Jewellery ERP — Design Master.
 * Ref: Suntech Design Master screenshots (tabbed: Metals / Stones / Others).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$designs = epc_jewel_design_list($db_link, $companyId);
$divisions = epc_jewel_divisions();
$karats = epc_jewel_karat_list($db_link, $companyId);

erp_page_header('<i class="fa fa-paint-brush"></i> Design Master', 'Jewellery designs with metal/stone compositions and pricing.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Design master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Design Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_des_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Design Code</th><th>Description</th><th>Currency</th>
				<th>Category</th><th>Cost Amt</th><th>Price 1</th>
			</tr></thead>
			<tbody>
			<?php if (empty($designs)): ?>
				<tr><td colspan="7" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($designs as $d): ?>
				<tr class="ef-grid-row" data-id="<?php echo (int)$d['id']; ?>"
					onclick="jwDesSelect(this)" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($d['design_code']); ?></strong></td>
					<td><?php echo epc_erp_h($d['description']); ?></td>
					<td><?php echo epc_erp_h($d['currency']); ?></td>
					<td><?php echo epc_erp_h($d['category']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$d['cost_amount'], 2); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$d['price1_fc'], 2); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_des_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_design_save">

			<div class="ef-section">
				<span class="ef-section-title">Design Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Design Code</label><input name="design_code" maxlength="20" required></div>
					<div class="ef-field ef-field-wide"><label>Description</label><input name="description" maxlength="120"></div>
					<div class="ef-field"><label>Currency</label><input name="currency" maxlength="5" value="AED"></div>
					<div class="ef-field"><label>Currency Rate</label><input name="currency_rate" type="number" step="0.00001" value="1.00000"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Cost Centre</label><input name="cost_centre" maxlength="20"></div>
					<div class="ef-field"><label>Type</label><input name="type" maxlength="30"></div>
					<div class="ef-field"><label>Set Ref</label><input name="set_ref" maxlength="30"></div>
					<div class="ef-field"><label>Pair Ref</label><input name="pair_ref" maxlength="30"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Category</label><input name="category" maxlength="30" placeholder="RING"></div>
					<div class="ef-field"><label>Sub Category</label><input name="sub_category" maxlength="30"></div>
					<div class="ef-field"><label>Brand</label><input name="brand" maxlength="60"></div>
					<div class="ef-field"><label>Color</label><input name="color" maxlength="30"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Country</label><input name="country" maxlength="30" placeholder="UAE"></div>
					<div class="ef-field"><label>Vendor</label><input name="vendor" maxlength="20"></div>
					<div class="ef-field"><label>Vendor Ref</label><input name="vendor_ref" maxlength="20"></div>
					<div class="ef-field"><label><input type="checkbox" name="metal_and_stone" value="1" checked> Metal &amp; Stone</label></div>
				</div>
			</div>

			<div class="ef-tabs">
				<button type="button" class="ef-tab active" onclick="jwDesTab(this,'metals')">1. Metals</button>
				<button type="button" class="ef-tab" onclick="jwDesTab(this,'stones')">2. Stones</button>
				<button type="button" class="ef-tab" onclick="jwDesTab(this,'pricing')">3. Pricing / Info</button>
			</div>

			<div id="jw_des_metals" class="ef-tab-pane">
				<div class="ef-section">
					<span class="ef-section-title">Metal Details</span>
					<table class="ef-grid">
						<thead><tr>
							<th>No.</th><th>Division</th><th>Karat</th><th>Gross Wt</th>
							<th>Rate Type</th><th>Metal Rate</th><th>Amount FC</th><th>Amount LC</th>
						</tr></thead>
						<tbody>
							<tr>
								<td>1</td>
								<td><select name="m_division"><option value="G">Gold</option><option value="S">Silver</option><option value="T">Platinum</option></select></td>
								<td><select name="m_karat"><?php foreach ($karats as $k): ?><option value="<?php echo epc_erp_h($k['karat_code']); ?>"><?php echo epc_erp_h($k['karat_code']); ?></option><?php endforeach; ?></select></td>
								<td><input name="m_gross_wt" type="number" step="0.0001" value="0.0000" style="width:80px"></td>
								<td><input name="m_rate_type" maxlength="10" value="GMS" style="width:60px"></td>
								<td><input name="m_metal_rate" type="number" step="0.00001" value="0.00000" style="width:90px"></td>
								<td><input name="m_amount_fc" type="number" step="0.01" value="0.00" style="width:80px"></td>
								<td><input name="m_amount_lc" type="number" step="0.01" value="0.00" style="width:80px"></td>
							</tr>
						</tbody>
					</table>
				</div>
			</div>

			<div id="jw_des_stones" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Stone Details</span>
					<table class="ef-grid">
						<thead><tr>
							<th>No.</th><th>Stone Type</th><th>Shape</th><th>Size</th>
							<th>Color</th><th>Clarity</th><th>Pcs</th><th>Carat</th>
							<th>Rate</th><th>Amount FC</th><th>Amount LC</th>
						</tr></thead>
						<tbody>
							<tr>
								<td>1</td>
								<td><input name="s_stone_type" maxlength="30" style="width:80px"></td>
								<td><input name="s_shape" maxlength="20" style="width:60px"></td>
								<td><input name="s_size" maxlength="20" style="width:50px"></td>
								<td><input name="s_color" maxlength="20" style="width:50px"></td>
								<td><input name="s_clarity" maxlength="20" style="width:50px"></td>
								<td><input name="s_pcs" type="number" value="0" style="width:50px"></td>
								<td><input name="s_carat" type="number" step="0.0001" value="0.0000" style="width:70px"></td>
								<td><input name="s_rate" type="number" step="0.0001" value="0.0000" style="width:70px"></td>
								<td><input name="s_amount_fc" type="number" step="0.01" value="0.00" style="width:80px"></td>
								<td><input name="s_amount_lc" type="number" step="0.01" value="0.00" style="width:80px"></td>
							</tr>
						</tbody>
					</table>
				</div>
			</div>

			<div id="jw_des_pricing" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Pricing</span>
					<div class="ef-row">
						<div class="ef-field"><label>Cost Amount</label><input name="cost_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Price 1 Code</label><input name="price1_code" maxlength="5" value="GEN"></div>
						<div class="ef-field"><label>Price 1 %</label><input name="price1_pct" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Price 1 FC</label><input name="price1_fc" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Price 1 LC</label><input name="price1_lc" type="number" step="0.01" value="0.00"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Price 2 Code</label><input name="price2_code" maxlength="5"></div>
						<div class="ef-field"><label>Price 2 %</label><input name="price2_pct" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Price 2 FC</label><input name="price2_fc" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Price 2 LC</label><input name="price2_lc" type="number" step="0.01" value="0.00"></div>
					</div>
				</div>
				<div class="ef-section">
					<span class="ef-section-title">Totals</span>
					<div class="ef-row">
						<div class="ef-field"><label>Metal Total Qty</label><input name="metal_total_qty" type="number" step="0.0001" value="0.0000" readonly></div>
						<div class="ef-field"><label>Stone Total Qty</label><input name="stone_total_qty" type="number" step="0.0001" value="0.0000" readonly></div>
					</div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_des_form').style.display='none'">Cancel</button>
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
function jwDesSelect(row){
	document.querySelectorAll('.ef-grid-row').forEach(function(r){r.style.background='';});
	row.style.background='#d0e8ff';
}
function jwDesTab(btn, pane){
	document.querySelectorAll('.ef-tab').forEach(function(t){t.classList.remove('active');});
	btn.classList.add('active');
	['metals','stones','pricing'].forEach(function(p){
		document.getElementById('jw_des_'+p).style.display=(p===pane)?'block':'none';
	});
}
</script>
