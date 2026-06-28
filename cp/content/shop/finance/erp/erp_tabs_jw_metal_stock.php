<?php
/**
 * Jewellery ERP — Metal Stock Master.
 * Ref: Suntech Metal Stock Master screenshots.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$items = epc_jewel_metal_stock_list($db_link, $companyId);
$divisions = epc_jewel_divisions();
$karats = epc_jewel_karat_list($db_link, $companyId);

erp_page_header('<i class="fa fa-cubes"></i> Metal Stock Master', 'Metal items with pricing, barcode and stock details.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Metal stock master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Stock Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_ms_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Metal</th><th>Item Code</th><th>Description</th>
				<th>Karat</th><th>Purity</th><th>Type</th><th>Stock Pcs</th><th>Stock Gms</th>
			</tr></thead>
			<tbody>
			<?php if (empty($items)): ?>
				<tr><td colspan="9" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($items as $it): ?>
				<tr class="ef-grid-row" data-id="<?php echo (int)$it['id']; ?>"
					onclick="jwMsSelect(this)" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><?php echo epc_erp_h($divisions[$it['metal']] ?? $it['metal']); ?></td>
					<td><strong><?php echo epc_erp_h($it['item_code']); ?></strong></td>
					<td><?php echo epc_erp_h($it['description']); ?></td>
					<td><?php echo epc_erp_h($it['karat']); ?></td>
					<td><?php echo number_format((float)$it['purity'], 6); ?></td>
					<td><?php echo epc_erp_h($it['type']); ?></td>
					<td><?php echo (int)$it['stock_pcs']; ?></td>
					<td><?php echo number_format((float)$it['stock_gms'], 4); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_ms_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_metal_stock_save">

			<div class="ef-section">
				<span class="ef-section-title">Item Identification</span>
				<div class="ef-row">
					<div class="ef-field"><label>Metal</label>
						<select name="metal"><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($l); ?></option><?php endforeach; ?></select>
					</div>
					<div class="ef-field"><label>Item Code</label><input name="item_code" maxlength="20" required placeholder="ITM001"></div>
					<div class="ef-field ef-field-wide"><label>Description</label><input name="description" maxlength="120"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>CC Making</label><input name="cc_making" maxlength="20" placeholder="Cost Centre"></div>
					<div class="ef-field"><label>CC Metal</label><input name="cc_metal" maxlength="20"></div>
					<div class="ef-field"><label>Karat</label>
						<select name="karat"><option value="">--</option><?php foreach ($karats as $k): ?><option value="<?php echo epc_erp_h($k['karat_code']); ?>"><?php echo epc_erp_h($k['karat_code']); ?></option><?php endforeach; ?></select>
					</div>
					<div class="ef-field"><label>Purity</label><input name="purity" type="number" step="0.000001" value="0.000000"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Type</label><input name="type" maxlength="30" placeholder="JEWELLERY"></div>
					<div class="ef-field"><label>Brand</label><input name="brand" maxlength="60"></div>
					<div class="ef-field"><label>Category</label><input name="category" maxlength="30" placeholder="RING"></div>
					<div class="ef-field"><label>Sub Category</label><input name="sub_category" maxlength="30"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Vendor</label><input name="vendor" maxlength="20"></div>
					<div class="ef-field"><label>Vendor Ref</label><input name="vendor_ref" maxlength="20"></div>
					<div class="ef-field"><label>Country</label><input name="country" maxlength="30" placeholder="UAE"></div>
					<div class="ef-field"><label>HS Code</label><input name="hs_code" maxlength="20" placeholder="7113.19"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>M.C. Unit</label>
						<select name="mc_unit"><option value="GMS">GMS</option><option value="OZ">OZ</option><option value="PCS">PCS</option></select>
					</div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Pricing</span>
				<div class="ef-row">
					<div class="ef-field"><label>Price 1 Code</label><input name="price1_code" maxlength="5" value="GEN"></div>
					<div class="ef-field"><label>Price 1 Label</label><input name="price1_label" maxlength="20" value="General"></div>
					<div class="ef-field"><label>Price 2</label><input name="price2_code" maxlength="5"></div>
					<div class="ef-field"><label>Price 3</label><input name="price3_code" maxlength="5"></div>
					<div class="ef-field"><label>Price 4</label><input name="price4_code" maxlength="5"></div>
					<div class="ef-field"><label>Price 5</label><input name="price5_code" maxlength="5"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Pur Cost/Gms</label><input name="pur_cost_gms" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Sale Price/Gms</label><input name="sale_price_gms" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Discount %</label><input name="discount_pct" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Std Cost</label><input name="std_cost" type="number" step="0.01" value="0.00"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Stock / Reorder</span>
				<div class="ef-row">
					<div class="ef-field"><label>Min Qty</label><input name="min_qty" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Max Qty</label><input name="max_qty" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>ReOrd Level</label><input name="reorder_level" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>ReOrd Qty</label><input name="reorder_qty" type="number" step="0.01" value="0.00"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Pc Wt (Gms)</label><input name="pc_weight_gms" type="number" step="0.00001" value="0.00000"></div>
					<div class="ef-field"><label>Pc Wt (OZ)</label><input name="pc_weight_oz" type="number" step="0.00001" value="0.00000"></div>
					<div class="ef-field"><label>Conv Factor OZ</label><input name="conv_factor_oz" type="number" step="0.00001" value="31.10347"></div>
					<div class="ef-field"><label>ABC Code</label>
						<select name="abc_code"><option value="">--</option><option value="A">A</option><option value="B">B</option><option value="C">C</option></select>
					</div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Options &amp; Flags</span>
				<div class="ef-row">
					<div class="ef-field"><label><input type="checkbox" name="include_stone_weight" value="1"> Include Stone Weight</label></div>
					<div class="ef-field"><label><input type="checkbox" name="pass_purity_diff" value="1"> Pass Purity Difference</label></div>
					<div class="ef-field"><label><input type="checkbox" name="in_pieces" value="1" checked> In Pieces</label></div>
					<div class="ef-field"><label><input type="checkbox" name="create_barcodes" value="1"> Create Barcodes</label></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label><input type="checkbox" name="block_gross_wt_sales" value="1"> Block Gross Wt in Sales</label></div>
					<div class="ef-field"><label><input type="checkbox" name="ask_supplier" value="1"> Ask Supplier</label></div>
					<div class="ef-field"><label><input type="checkbox" name="ask_wastage" value="1"> Ask Wastage</label></div>
					<div class="ef-field"><label><input type="checkbox" name="allow_negative_stock" value="1"> Allow Negative Stock</label></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label><input type="checkbox" name="exclude_gst_trn" value="1"> Exclude GST/TRN</label></div>
					<div class="ef-field"><label><input type="checkbox" name="gst_trn_on_making_stone" value="1" checked> GST/TRN on Making+Stone</label></div>
					<div class="ef-field"><label><input type="checkbox" name="allow_less_than_cost" value="1"> Allow Less Than Cost</label></div>
					<div class="ef-field"><label><input type="checkbox" name="loyalty_item" value="1"> Loyalty Item</label></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Barcode Prefix</label><input name="barcode_prefix" maxlength="10"></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_ms_form').style.display='none'">Cancel</button>
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
function jwMsSelect(row){
	document.querySelectorAll('.ef-grid-row').forEach(function(r){r.style.background='';});
	row.style.background='#d0e8ff';
}
</script>
