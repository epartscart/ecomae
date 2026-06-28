<?php
/**
 * Jewellery ERP — Metal Stock Master.
 * Full item management for gold, silver, platinum stock items.
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

erp_page_header('<i class="fa fa-cubes"></i> Metal Stock Master', 'Gold, silver, platinum item definitions with pricing, barcodes and stock control.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Metal stock master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Stock Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_ms_form').style.display=document.getElementById('jw_ms_form').style.display==='none'?'block':'none'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_ms_list').style.display='block';document.getElementById('jw_ms_form').style.display='none'"><i class="fa fa-list"></i> List</button>
	</div>
	<div class="ef-body">
		<!-- ENTRY FORM -->
		<div id="jw_ms_form" style="display:<?php echo empty($items)?'block':'none'; ?>;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_metal_stock_save">

			<div class="ef-section">
				<div class="ef-row">
					<div class="ef-field"><label>Metal</label>
						<select name="metal"><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($c); ?></option><?php endforeach; ?></select>
					</div>
					<div class="ef-field"><label>Item Code</label><input name="item_code" required placeholder="SLV"></div>
					<div class="ef-field ef-field-wide"><label>Description</label><input name="description" placeholder="SILVER BAR 999"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>CC Making</label><input name="cc_making" placeholder="SLMC"></div>
					<div class="ef-field"><label>CC Metal</label><input name="cc_metal" placeholder="SLVR"></div>
					<div class="ef-field"><label>Karat</label>
						<select name="karat"><option value="">—</option><?php foreach ($karats as $k): ?><option value="<?php echo epc_erp_h($k['karat_code']); ?>"><?php echo epc_erp_h($k['karat_code'].' — '.$k['description']); ?></option><?php endforeach; ?></select>
					</div>
					<div class="ef-field"><label>Purity</label><input name="purity" type="number" step="0.000001" value="1.000000"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Type</label><input name="type"></div>
					<div class="ef-field"><label>Brand</label><input name="brand"></div>
					<div class="ef-field"><label>Category</label><input name="category"></div>
					<div class="ef-field"><label>SubCategory</label><input name="sub_category"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Vendor</label><input name="vendor"></div>
					<div class="ef-field"><label>Vendor Ref</label><input name="vendor_ref"></div>
					<div class="ef-field"><label>Country</label><input name="country"></div>
					<div class="ef-field"><label>HS Code</label><input name="hs_code"></div>
				</div>
			</div>

			<div style="display:flex;gap:10px;flex-wrap:wrap;">
				<div class="ef-section" style="flex:1;min-width:280px;">
					<span class="ef-section-title">Details</span>
					<div class="ef-row"><div class="ef-field"><label>Price 1</label><input name="price1_code" value="GEN" style="width:50px"><input name="price1_label" value="General" style="width:80px"></div></div>
					<div class="ef-row"><div class="ef-field"><label>Price 2</label><input name="price2_code" style="width:50px"></div></div>
					<div class="ef-row"><div class="ef-field"><label>Price 3</label><input name="price3_code" style="width:50px"></div></div>
					<div class="ef-row"><div class="ef-field"><label>Price 4</label><input name="price4_code" style="width:50px"></div></div>
					<div class="ef-row"><div class="ef-field"><label>Price 5</label><input name="price5_code" style="width:50px"></div></div>
					<div class="ef-row">
						<div class="ef-field"><label>Pur Cost / Gms</label><input name="pur_cost_gms" type="number" step="0.01" value="0"></div>
						<div class="ef-field"><label>Sale Price / Gms</label><input name="sale_price_gms" type="number" step="0.01" value="0"></div>
					</div>
				</div>

				<div class="ef-section" style="flex:1;min-width:280px;">
					<span class="ef-section-title">Stock Control</span>
					<div class="ef-row">
						<div class="ef-field"><label>MC. Unit</label><select name="mc_unit"><option value="GMS">GMS</option><option value="GOZ">GOZ</option><option value="KB">KB</option></select></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Std Cost</label><input name="std_cost" type="number" step="0.01" value="0"></div>
						<div class="ef-field"><label>Discount %</label><input name="discount_pct" type="number" step="0.01" value="0"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Min. Qty</label><input name="min_qty" type="number" step="0.01" value="0"></div>
						<div class="ef-field"><label>Max. Qty</label><input name="max_qty" type="number" step="0.01" value="0"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>ReOrd-Lwl</label><input name="reorder_level" type="number" step="0.01" value="0"></div>
						<div class="ef-field"><label>ReOdr-Qty</label><input name="reorder_qty" type="number" step="0.01" value="0"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Conv. Factor OZ</label><input name="conv_factor_oz" type="number" step="0.00001" value="31.10347"></div>
						<div class="ef-field"><label>ABC Code</label><input name="abc_code" maxlength="1"></div>
					</div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Options</span>
				<div class="ef-checks">
					<label><input type="checkbox" name="include_stone_weight" value="1"> Include Stone Weight</label>
					<label><input type="checkbox" name="pass_purity_diff" value="1"> Pass Purity Difference Entries</label>
					<label><input type="checkbox" name="in_pieces" value="1" checked> In Pieces</label>
					<label><input type="checkbox" name="create_barcodes" value="1"> Create Barcodes</label>
					<label><input type="checkbox" name="block_gross_wt_sales" value="1"> Block Gross Wt. in Sales</label>
					<label><input type="checkbox" name="ask_supplier" value="1"> Ask Supplier</label>
					<label><input type="checkbox" name="ask_wastage" value="1"> Ask Wastage</label>
					<label><input type="checkbox" name="exclude_gst_trn" value="1"> Exclude GST/TRN</label>
					<label><input type="checkbox" name="allow_negative_stock" value="1"> Allow Negative Stock</label>
					<label><input type="checkbox" name="allow_less_than_cost" value="1"> Allow Less Than Cost</label>
					<label><input type="checkbox" name="gst_trn_on_making_stone" value="1" checked> GST/TRN on Making + Stone</label>
					<label><input type="checkbox" name="pop_stock_filter" value="1"> POP Stock Filter</label>
					<label><input type="checkbox" name="gst_trn_making_only" value="1"> GST/TRN on Making Only</label>
					<label><input type="checkbox" name="loyalty_item" value="1"> Loyalty Item</label>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>In Pieces: 1 PC =</label><input name="pc_weight_gms" type="number" step="0.00001" value="0" style="width:80px"><span style="font-size:11px">Gr. Gms</span></div>
					<div class="ef-field"><input name="pc_weight_oz" type="number" step="0.00001" value="0" style="width:80px"><span style="font-size:11px">Fine OZ</span></div>
					<div class="ef-field"><label>Prefix</label><input name="barcode_prefix" style="width:60px"></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-info btn-sm"><i class="fa fa-barcode"></i> View Barcodes</button>
			</div>
			</form>
		</div>

		<!-- LIST VIEW -->
		<div id="jw_ms_list" style="display:<?php echo empty($items)?'none':'block'; ?>;">
			<table class="ef-grid">
				<thead><tr><th>Item Code</th><th>Description</th><th>Metal</th><th>Karat</th><th>Purity</th><th>Stock Pcs</th><th>Stock Gms</th><th>Value</th></tr></thead>
				<tbody>
				<?php if (empty($items)): ?><tr><td colspan="8" style="text-align:center;color:#999">No items</td></tr>
				<?php else: foreach ($items as $i): ?>
				<tr>
					<td><strong><?php echo epc_erp_h($i['item_code']); ?></strong></td>
					<td><?php echo epc_erp_h($i['description']); ?></td>
					<td><?php echo epc_erp_h($i['metal']); ?></td>
					<td><?php echo epc_erp_h($i['karat']); ?></td>
					<td><?php echo number_format((float)$i['purity'], 6); ?></td>
					<td><?php echo (int)$i['stock_pcs']; ?></td>
					<td><?php echo number_format((float)$i['stock_gms'], 4); ?></td>
					<td><?php echo epc_erp_money((float)$i['stock_value'], 2); ?></td>
				</tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	</div>
	<div class="ef-status"><span>Mode:=VIEW</span><span>Header New Record → Function Key (F5)</span></div>
</div>
