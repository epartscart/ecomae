<?php
/**
 * Jewellery ERP — Diamond Jewellery Master.
 * Ref: Suntech Diamond Jewellery Master screenshots (detailed form with charges, certificates).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$diamonds = epc_jewel_diamond_list($db_link, $companyId);
$divisions = epc_jewel_divisions();
$karats = epc_jewel_karat_list($db_link, $companyId);

erp_page_header('<i class="fa fa-diamond"></i> Diamond Jewellery Master', 'Diamond items with certificates, charges and metal/stone composition.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Diamond master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Diamond Jewellery Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_dia_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Item Code</th><th>RFID</th><th>Design</th><th>Description</th>
				<th>Color</th><th>Clarity</th><th>Gr.Wt</th><th>Cost</th><th>Price 1</th>
			</tr></thead>
			<tbody>
			<?php if (empty($diamonds)): ?>
				<tr><td colspan="10" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($diamonds as $d): ?>
				<tr class="ef-grid-row" data-id="<?php echo (int)$d['id']; ?>"
					onclick="jwDiaSelect(this)" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($d['item_code']); ?></strong></td>
					<td><?php echo epc_erp_h($d['rfid']); ?></td>
					<td><?php echo epc_erp_h($d['design']); ?></td>
					<td><?php echo epc_erp_h($d['description']); ?></td>
					<td><?php echo epc_erp_h($d['color']); ?></td>
					<td><?php echo epc_erp_h($d['clarity']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$d['item_gr_wt'], 4); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$d['cost_amount'], 2); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$d['price1_fc'], 2); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_dia_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_diamond_save">

			<div class="ef-section">
				<span class="ef-section-title">Item Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Item Code</label><input name="item_code" maxlength="20" required></div>
					<div class="ef-field"><label>RFID</label><input name="rfid" maxlength="30"></div>
					<div class="ef-field"><label>Design</label><input name="design" maxlength="30"></div>
					<div class="ef-field ef-field-wide"><label>Description</label><input name="description" maxlength="120"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Currency</label><input name="currency" maxlength="5" value="AED"></div>
					<div class="ef-field"><label>Currency Rate</label><input name="currency_rate" type="number" step="0.00001" value="1.00000"></div>
					<div class="ef-field"><label>Cost Centre</label><input name="cost_centre" maxlength="20"></div>
					<div class="ef-field"><label><input type="checkbox" name="promotional" value="1"> Promotional</label></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Category</label><input name="category" maxlength="30" placeholder="RING"></div>
					<div class="ef-field"><label>Sub Category</label><input name="sub_category" maxlength="30"></div>
					<div class="ef-field"><label>Type</label><input name="type" maxlength="30"></div>
					<div class="ef-field"><label>Brand</label><input name="brand" maxlength="60"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Color</label>
						<select name="color"><option value="">--</option><option value="D">D</option><option value="E">E</option><option value="F">F</option><option value="G">G</option><option value="H">H</option><option value="I">I</option><option value="J">J</option><option value="K">K</option></select>
					</div>
					<div class="ef-field"><label>Clarity</label>
						<select name="clarity"><option value="">--</option><option value="IF">IF</option><option value="VVS1">VVS1</option><option value="VVS2">VVS2</option><option value="VS1">VS1</option><option value="VS2">VS2</option><option value="SI1">SI1</option><option value="SI2">SI2</option></select>
					</div>
					<div class="ef-field"><label>Fluorescence</label>
						<select name="fluorescence"><option value="">--</option><option value="None">None</option><option value="Faint">Faint</option><option value="Medium">Medium</option><option value="Strong">Strong</option></select>
					</div>
					<div class="ef-field"><label>Style</label><input name="style" maxlength="20"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Country</label><input name="country" maxlength="30" placeholder="UAE"></div>
					<div class="ef-field"><label>Set Ref</label><input name="set_ref" maxlength="30"></div>
					<div class="ef-field"><label>Vendor</label><input name="vendor" maxlength="20"></div>
					<div class="ef-field"><label>Vendor Ref</label><input name="vendor_ref" maxlength="20"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Item Gr. Wt</label><input name="item_gr_wt" type="number" step="0.0001" value="0.0000"></div>
					<div class="ef-field"><label>Pure Wt</label><input name="pure_wt" type="number" step="0.0001" value="0.0000"></div>
					<div class="ef-field"><label>Cust SKU</label><input name="cust_sku" maxlength="30"></div>
					<div class="ef-field"><label>Ageing Date</label><input name="ageing_date" type="date"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Certificates</span>
				<div class="ef-row">
					<div class="ef-field"><label>Certificate No</label><input name="certificate_no" maxlength="40"></div>
					<div class="ef-field"><label>Certificate Date</label><input name="certificate_date" type="date"></div>
					<div class="ef-field"><label>Certificate By</label><input name="certificate_by" maxlength="40" placeholder="GIA"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Certificate No 1</label><input name="certificate_no_1" maxlength="40"></div>
					<div class="ef-field"><label>Certificate Date 1</label><input name="certificate_date_1" type="date"></div>
					<div class="ef-field"><label>No. of Certificates</label><input name="no_of_certificates" type="number" value="0"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Charges</span>
				<div class="ef-row">
					<div class="ef-field"><label>Setting Charge</label><input name="setting_charge" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Polishing Charge</label><input name="polishing_charge" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Rhodium Charge</label><input name="rhodium_charge" type="number" step="0.01" value="0.00"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Labour Charge</label><input name="labour_charge" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Misc Charge</label><input name="misc_charge" type="number" step="0.01" value="0.00"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Pricing</span>
				<div class="ef-row">
					<div class="ef-field"><label>Cost Amount</label><input name="cost_amount" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Landed Cost</label><input name="landed_cost" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Foreign Cost</label><input name="foreign_cost" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Cost Difference</label><input name="cost_difference" type="number" step="0.01" value="0.00"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Price 1 (TAG)</label><input name="price1_code" maxlength="5" value="TAG"></div>
					<div class="ef-field"><label>Price 1 %</label><input name="price1_pct" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 1 FC</label><input name="price1_fc" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 1 LC</label><input name="price1_lc" type="number" step="0.01" value="0.00"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Price 2 (GEN)</label><input name="price2_code" maxlength="5" value="GEN"></div>
					<div class="ef-field"><label>Price 2 %</label><input name="price2_pct" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 2 FC</label><input name="price2_fc" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 2 LC</label><input name="price2_lc" type="number" step="0.01" value="0.00"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Tax &amp; Compliance</span>
				<div class="ef-row">
					<div class="ef-field"><label><input type="checkbox" name="exclude_gst_metal" value="1"> Exclude GST Metal</label></div>
					<div class="ef-field"><label><input type="checkbox" name="trn_on_margin" value="1"> TRN on Margin</label></div>
					<div class="ef-field"><label><input type="checkbox" name="uae_trn_item" value="1"> UAE TRN Item</label></div>
				</div>
			</div>

			<div class="ef-tabs">
				<button type="button" class="ef-tab active" onclick="jwDiaTab(this,'metals')">1. Metal Details</button>
				<button type="button" class="ef-tab" onclick="jwDiaTab(this,'stones')">2. Stone Details</button>
				<button type="button" class="ef-tab" onclick="jwDiaTab(this,'labour')">3. Labour / Summary</button>
			</div>

			<div id="jw_dia_metals" class="ef-tab-pane">
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Division</th><th>Karat</th><th>Gross Wt</th>
						<th>Rate Type</th><th>Metal Rate</th><th>Amount FC</th><th>Amount LC</th>
					</tr></thead>
					<tbody>
						<tr>
							<td>1</td>
							<td><select name="dm_division"><option value="G">Gold</option><option value="S">Silver</option><option value="T">Platinum</option></select></td>
							<td><select name="dm_karat"><?php foreach ($karats as $k): ?><option value="<?php echo epc_erp_h($k['karat_code']); ?>"><?php echo epc_erp_h($k['karat_code']); ?></option><?php endforeach; ?></select></td>
							<td><input name="dm_gross_wt" type="number" step="0.0001" value="0.0000" style="width:80px"></td>
							<td><input name="dm_rate_type" maxlength="10" value="GMS" style="width:60px"></td>
							<td><input name="dm_metal_rate" type="number" step="0.00001" value="0.00000" style="width:90px"></td>
							<td><input name="dm_amount_fc" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="dm_amount_lc" type="number" step="0.01" value="0.00" style="width:80px"></td>
						</tr>
					</tbody>
				</table>
			</div>

			<div id="jw_dia_stones" class="ef-tab-pane" style="display:none">
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Stone Type</th><th>Shape</th><th>Size</th><th>Color</th>
						<th>Clarity</th><th>Pcs</th><th>Carat</th><th>Rate</th><th>Amt FC</th><th>Amt LC</th>
					</tr></thead>
					<tbody>
						<tr>
							<td>1</td>
							<td><input name="ds_stone_type" maxlength="30" style="width:80px"></td>
							<td><input name="ds_shape" maxlength="20" style="width:60px"></td>
							<td><input name="ds_size" maxlength="20" style="width:50px"></td>
							<td><input name="ds_color" maxlength="20" style="width:50px"></td>
							<td><input name="ds_clarity" maxlength="20" style="width:50px"></td>
							<td><input name="ds_pcs" type="number" value="0" style="width:50px"></td>
							<td><input name="ds_carat" type="number" step="0.0001" value="0.0000" style="width:70px"></td>
							<td><input name="ds_rate" type="number" step="0.0001" value="0.0000" style="width:70px"></td>
							<td><input name="ds_amount_fc" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="ds_amount_lc" type="number" step="0.01" value="0.00" style="width:80px"></td>
						</tr>
					</tbody>
				</table>
			</div>

			<div id="jw_dia_labour" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Labour Amounts</span>
					<div class="ef-row">
						<div class="ef-field"><label>Labour Amount LC</label><input name="labour_amount_lc" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Labour Amount FC</label><input name="labour_amount_fc" type="number" step="0.01" value="0.00"></div>
					</div>
				</div>
				<div class="ef-section">
					<span class="ef-section-title">Composition Summary</span>
					<div class="ef-row">
						<div class="ef-field"><label>Metal Qty</label><input name="metal_qty" type="number" step="0.0001" value="0.0000" readonly></div>
						<div class="ef-field"><label>Stone Qty</label><input name="stone_qty" type="number" step="0.0001" value="0.0000" readonly></div>
					</div>
				</div>
			</div>

			<div class="ef-actions" style="margin-top:8px">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_dia_form').style.display='none'">Cancel</button>
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
function jwDiaSelect(row){
	document.querySelectorAll('.ef-grid-row').forEach(function(r){r.style.background='';});
	row.style.background='#d0e8ff';
}
function jwDiaTab(btn, pane){
	document.querySelectorAll('.ef-tab').forEach(function(t){t.classList.remove('active');});
	btn.classList.add('active');
	['metals','stones','labour'].forEach(function(p){
		document.getElementById('jw_dia_'+p).style.display=(p===pane)?'block':'none';
	});
}
</script>
