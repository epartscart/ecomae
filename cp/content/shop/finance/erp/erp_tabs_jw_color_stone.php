<?php
/**
 * Jewellery ERP — Colour Stone Master.
 * Ref: Suntech Colour Stone Master screenshot (proportion/measurement sections).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$stones = epc_jewel_color_stone_list($db_link, $companyId);

erp_page_header('<i class="fa fa-diamond"></i> Colour Stone Master', 'Coloured gemstones with proportions, measurements and pricing.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Colour stone master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Colour Stone Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_cs_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Item Code</th><th>Description</th><th>Stone Type</th>
				<th>Shape</th><th>Color</th><th>Carat</th><th>Cost</th>
			</tr></thead>
			<tbody>
			<?php if (empty($stones)): ?>
				<tr><td colspan="8" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($stones as $s): ?>
				<tr class="ef-grid-row" data-id="<?php echo (int)$s['id']; ?>"
					onclick="jwCsSelect(this)" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($s['item_code']); ?></strong></td>
					<td><?php echo epc_erp_h($s['description']); ?></td>
					<td><?php echo epc_erp_h($s['stone_type']); ?></td>
					<td><?php echo epc_erp_h($s['shape']); ?></td>
					<td><?php echo epc_erp_h($s['color']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$s['carat'], 4); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$s['cost_amount'], 2); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_cs_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_color_stone_save">

			<div class="ef-section">
				<span class="ef-section-title">Stone Identification</span>
				<div class="ef-row">
					<div class="ef-field"><label>Item Code</label><input name="item_code" maxlength="20" required></div>
					<div class="ef-field ef-field-wide"><label>Description</label><input name="description" maxlength="120"></div>
					<div class="ef-field"><label>Currency</label><input name="currency" maxlength="5" value="AED"></div>
					<div class="ef-field"><label>Currency Rate</label><input name="currency_rate" type="number" step="0.00001" value="1.00000"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Stone Type</label>
						<select name="stone_type"><option value="Ruby">Ruby</option><option value="Sapphire">Sapphire</option><option value="Emerald">Emerald</option><option value="Topaz">Topaz</option><option value="Amethyst">Amethyst</option><option value="Garnet">Garnet</option><option value="Tanzanite">Tanzanite</option><option value="Other">Other</option></select>
					</div>
					<div class="ef-field"><label>Shape</label>
						<select name="shape"><option value="Round">Round</option><option value="Oval">Oval</option><option value="Cushion">Cushion</option><option value="Pear">Pear</option><option value="Emerald">Emerald</option><option value="Marquise">Marquise</option><option value="Heart">Heart</option><option value="Princess">Princess</option></select>
					</div>
					<div class="ef-field"><label>Color</label><input name="color" maxlength="20" placeholder="Red"></div>
					<div class="ef-field"><label>Clarity</label>
						<select name="clarity"><option value="Eye Clean">Eye Clean</option><option value="Slightly Included">Slightly Included</option><option value="Moderately Included">Moderately Included</option><option value="Heavily Included">Heavily Included</option></select>
					</div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Category</label><input name="category" maxlength="30"></div>
					<div class="ef-field"><label>Sub Category</label><input name="sub_category" maxlength="30"></div>
					<div class="ef-field"><label>Vendor</label><input name="vendor" maxlength="20"></div>
					<div class="ef-field"><label>Vendor Ref</label><input name="vendor_ref" maxlength="20"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Origin</label><input name="origin" maxlength="30" placeholder="Myanmar"></div>
					<div class="ef-field"><label>Treatment</label>
						<select name="treatment"><option value="None">None</option><option value="Heat">Heat</option><option value="Oil">Oil</option><option value="Diffusion">Diffusion</option><option value="Irradiation">Irradiation</option></select>
					</div>
					<div class="ef-field"><label>Certificate No</label><input name="certificate_no" maxlength="40"></div>
					<div class="ef-field"><label>Certificate By</label><input name="certificate_by" maxlength="40" placeholder="GIA"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Proportions &amp; Measurements</span>
				<div class="ef-row">
					<div class="ef-field"><label>Carat</label><input name="carat" type="number" step="0.0001" value="0.0000"></div>
					<div class="ef-field"><label>Length (mm)</label><input name="length_mm" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Width (mm)</label><input name="width_mm" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Depth (mm)</label><input name="depth_mm" type="number" step="0.01" value="0.00"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Depth %</label><input name="depth_pct" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Table %</label><input name="table_pct" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Symmetry</label>
						<select name="symmetry"><option value="Excellent">Excellent</option><option value="Very Good">Very Good</option><option value="Good">Good</option><option value="Fair">Fair</option></select>
					</div>
					<div class="ef-field"><label>Polish</label>
						<select name="polish"><option value="Excellent">Excellent</option><option value="Very Good">Very Good</option><option value="Good">Good</option><option value="Fair">Fair</option></select>
					</div>
				</div>
			</div>

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

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_cs_form').style.display='none'">Cancel</button>
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
function jwCsSelect(row){
	document.querySelectorAll('.ef-grid-row').forEach(function(r){r.style.background='';});
	row.style.background='#d0e8ff';
}
</script>
