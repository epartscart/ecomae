<?php
/**
 * Jewellery ERP — Diamond Jewellery Master.
 * Per-piece diamond items with certificates, 4C grading, pricing tiers.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$items = epc_jewel_diamond_list($db_link, $companyId);

erp_page_header('<i class="fa fa-diamond"></i> Diamond Jewellery Master', 'Diamond items with RFID, certificates, charges, metal & stone composition.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Diamond master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Diamond Jewellery Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_dm_form').style.display=document.getElementById('jw_dm_form').style.display==='none'?'block':'none'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-info btn-xs"><i class="fa fa-search"></i> Stock Enquiry</button>
		<span style="margin-left:auto;font-size:11px;">
			<label style="color:#090;cursor:pointer"><input type="radio" name="dm_status" checked> ON</label>
			<label style="color:#c00;cursor:pointer"><input type="radio" name="dm_status"> OFF</label>
		</span>
	</div>
	<div class="ef-body">
		<div id="jw_dm_form" style="display:<?php echo empty($items)?'block':'none'; ?>;">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_diamond_save">

		<div style="display:flex;gap:10px;flex-wrap:wrap;">
			<div style="flex:2;min-width:400px;">
				<div class="ef-section">
					<div class="ef-row">
						<div class="ef-field"><label>Item Code</label><input name="item_code" required placeholder="ZCRG052223A"></div>
						<div class="ef-field"><label>RFID #</label><input name="rfid"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Design</label><input name="design"></div>
						<div class="ef-field"><label><input type="checkbox" name="promotional" value="1"> Promotional Item</label></div>
					</div>
					<div class="ef-row">
						<div class="ef-field ef-field-wide"><label>Description</label><input name="description" placeholder="zircon display big ring"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Currency</label><input name="currency" value="AED" style="width:50px"></div>
						<div class="ef-field"><input name="currency_rate" type="number" step="0.00001" value="1.00000" style="width:80px"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Cost Centre</label><input name="cost_centre" value="DJEW"></div>
						<div class="ef-field"><label>Type</label><input name="type"></div>
						<div class="ef-field"><label>Brand</label><input name="brand"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Category</label><input name="category" placeholder="DPT"></div>
						<div class="ef-field"><label>Sub-Cat</label><input name="sub_category"></div>
						<div class="ef-field"><label>Country</label><input name="country"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Color</label><input name="color"></div>
						<div class="ef-field"><label>Clarity</label><input name="clarity"></div>
						<div class="ef-field"><label>Style</label><input name="style"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Fluorescence</label><input name="fluorescence"></div>
						<div class="ef-field"><label>Set. Ref</label><input name="set_ref"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Vendor</label><input name="vendor" placeholder="AS5008"></div>
						<div class="ef-field"><label>Vendor Ref</label><input name="vendor_ref"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field"><label>Item GrWt.</label><input name="item_gr_wt" type="number" step="0.0001" value="0"></div>
					</div>
				</div>
			</div>

			<div style="flex:1;min-width:260px;">
				<div class="ef-section">
					<span class="ef-section-title">Pricing</span>
					<table class="ef-price-matrix" style="width:100%">
						<tr><th></th><th>Code</th><th>%</th><th>FC(AED)</th><th>LC(AED)</th></tr>
						<tr><td>Cost</td><td></td><td></td><td><input name="cost_amount" type="number" step="0.01" value="0" style="width:70px"></td><td></td></tr>
						<tr><td>Price 1</td><td><input name="price1_code" value="TAG" style="width:40px"></td><td><input name="price1_pct" type="number" step="0.01" value="0" style="width:50px"></td><td><input name="price1_fc" type="number" step="0.01" value="0" style="width:70px"></td><td></td></tr>
						<tr><td>Price 2</td><td><input name="price2_code" value="GEN" style="width:40px"></td><td><input name="price2_pct" type="number" step="0.01" value="0" style="width:50px"></td><td><input name="price2_fc" type="number" step="0.01" value="0" style="width:70px"></td><td></td></tr>
						<tr><td>Price 3</td><td></td><td></td><td><input name="price3_fc" type="number" step="0.01" value="0" style="width:70px"></td><td></td></tr>
						<tr><td>Price 4</td><td></td><td></td><td><input name="price4_fc" type="number" step="0.01" value="0" style="width:70px"></td><td></td></tr>
						<tr><td>Price 5</td><td></td><td></td><td><input name="price5_fc" type="number" step="0.01" value="0" style="width:70px"></td><td></td></tr>
					</table>
					<div class="ef-row" style="margin-top:6px">
						<div class="ef-field"><label>Landed Cost</label><input name="landed_cost" type="number" step="0.01" value="0"></div>
						<div class="ef-field"><label>Foreign Cost</label><input name="foreign_cost" type="number" step="0.01" value="0"></div>
					</div>
				</div>
				<div class="ef-section">
					<span class="ef-section-title">Certificate</span>
					<div class="ef-row"><div class="ef-field"><label>Certificate No</label><input name="certificate_no"></div></div>
					<div class="ef-row"><div class="ef-field"><label>Dated</label><input name="certificate_date" type="date"></div><div class="ef-field"><label>By</label><input name="certificate_by"></div></div>
					<div class="ef-row"><div class="ef-field"><label>No Of Certificate</label><input name="no_of_certificates" type="number" value="0" style="width:40px"></div></div>
					<div class="ef-image-box">Certificate Image</div>
				</div>
			</div>
		</div>

		<!-- Tabs: Metals | Stones | Others/Info -->
		<div class="ef-tabs">
			<ul class="nav nav-tabs" role="tablist">
				<li class="active"><a href="#dm_metals" data-toggle="tab">1. Metals</a></li>
				<li><a href="#dm_stones" data-toggle="tab">2. Stones</a></li>
				<li><a href="#dm_others" data-toggle="tab">3. Others / Info</a></li>
			</ul>
			<div class="tab-content">
				<div class="tab-pane active" id="dm_metals">
					<p style="font-size:11px;color:#666;">Metal composition of this diamond jewellery item.</p>
				</div>
				<div class="tab-pane" id="dm_stones">
					<p style="font-size:11px;color:#666;">Stone details — type, shape, clarity, carat.</p>
				</div>
				<div class="tab-pane" id="dm_others">
					<div class="ef-section">
						<span class="ef-section-title">Charges</span>
						<div class="ef-row">
							<div class="ef-field"><label>Setting</label><input name="setting_charge" type="number" step="0.01" value="0" style="width:70px"><span style="font-size:10px">FC(AED)</span></div>
							<div class="ef-field"><label>Polishing</label><input name="polishing_charge" type="number" step="0.01" value="0" style="width:70px"></div>
							<div class="ef-field"><label>Rhodium</label><input name="rhodium_charge" type="number" step="0.01" value="0" style="width:70px"></div>
						</div>
						<div class="ef-row">
							<div class="ef-field"><label>Labour</label><input name="labour_charge" type="number" step="0.01" value="0" style="width:70px"></div>
							<div class="ef-field"><label>MISC</label><input name="misc_charge" type="number" step="0.01" value="0" style="width:70px"></div>
						</div>
					</div>
					<div class="ef-section">
						<span class="ef-section-title">Options</span>
						<div class="ef-checks">
							<label><input type="checkbox" name="exclude_gst_metal" value="1"> Exclude GST/TRN of Metal Amt</label>
							<label><input type="checkbox" name="trn_on_margin" value="1"> TRN ON Margin</label>
							<label><input type="checkbox" name="uae_trn_item" value="1"> UAE TRN Item</label>
						</div>
						<div class="ef-row">
							<div class="ef-field"><label>Pure Wt.</label><input name="pure_wt" type="number" step="0.01" value="0"></div>
						</div>
					</div>
					<div class="ef-section">
						<span class="ef-section-title">TAG DETAILS</span>
						<textarea name="tag_details" class="ef-narration" placeholder="18KG : 29.00&#10;C 12.50&#10;D 2.12[VS2/G-H]"></textarea>
					</div>
				</div>
			</div>
		</div>

		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-default btn-sm"><i class="fa fa-picture-o"></i> Picture Path</button>
		</div>
		</form>
		</div>

		<!-- LIST VIEW -->
		<div id="jw_dm_list" style="display:<?php echo empty($items)?'none':'block'; ?>;">
			<table class="ef-grid">
				<thead><tr><th>Item Code</th><th>Description</th><th>Cost Centre</th><th>Category</th><th>Vendor</th><th>Cost</th><th>Price 1</th></tr></thead>
				<tbody>
				<?php foreach ($items as $i): ?>
				<tr>
					<td><strong><?php echo epc_erp_h($i['item_code']); ?></strong></td>
					<td><?php echo epc_erp_h($i['description']); ?></td>
					<td><?php echo epc_erp_h($i['cost_centre']); ?></td>
					<td><?php echo epc_erp_h($i['category']); ?></td>
					<td><?php echo epc_erp_h($i['vendor']); ?></td>
					<td><?php echo epc_erp_money((float)$i['cost_amount'], 2); ?></td>
					<td><?php echo epc_erp_money((float)$i['price1_fc'], 2); ?></td>
				</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>
	</div>
	<div class="ef-status"><span>Mode:=VIEW</span><span>Header New Record → Function Key (F5)</span></div>
</div>
