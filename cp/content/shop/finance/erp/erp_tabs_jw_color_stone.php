<?php
/**
 * Jewellery ERP — Colour Stone Master.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-star"></i> Colour Stone Master', 'Semi-precious & precious colour stones with dimensions and grading.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Colour stone master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Colour Stone Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_cs_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
	</div>
	<div class="ef-body">
		<div id="jw_cs_form" style="display:block;">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_color_stone_save">

		<div class="ef-section">
			<div class="ef-row">
				<div class="ef-field"><label>Item Code</label><input name="item_code" required></div>
				<div class="ef-field ef-field-wide"><label>Description</label><input name="description"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Stone Type</label><select name="stone_type"><option>Ruby</option><option>Emerald</option><option>Sapphire</option><option>Topaz</option><option>Garnet</option><option>Amethyst</option><option>Opal</option><option>Tourmaline</option><option>Tanzanite</option><option>Zircon</option><option>Aquamarine</option></select></div>
				<div class="ef-field"><label>Shape</label><select name="shape"><option>Round</option><option>Oval</option><option>Pear</option><option>Cushion</option><option>Princess</option><option>Marquise</option><option>Heart</option><option>Emerald Cut</option><option>Cabochon</option></select></div>
				<div class="ef-field"><label>Clarity</label><select name="clarity"><option>Eye Clean</option><option>VVS</option><option>VS</option><option>SI</option><option>Included</option></select></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Size (mm)</label><input name="size_mm" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Length</label><input name="length_mm" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Width</label><input name="width_mm" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Depth</label><input name="depth_mm" type="number" step="0.01" value="0"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Weight (ct)</label><input name="weight_ct" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Pcs</label><input name="pcs" type="number" value="1"></div>
				<div class="ef-field"><label>Color Grade</label><input name="color_grade"></div>
				<div class="ef-field"><label>Treatment</label><select name="treatment"><option value="None">None</option><option value="Heated">Heated</option><option value="Unheated">Unheated</option><option value="Filled">Filled</option><option value="Oiled">Oiled</option><option value="Irradiated">Irradiated</option></select></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Origin</label><input name="origin" placeholder="Myanmar / Colombia / Sri Lanka"></div>
				<div class="ef-field"><label>Certificate #</label><input name="certificate_no"></div>
				<div class="ef-field"><label>Vendor</label><input name="vendor"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Cost/ct</label><input name="cost_per_ct" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Sell/ct</label><input name="sell_per_ct" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Cost Centre</label><input name="cost_centre" value="CSTN"></div>
			</div>
		</div>

		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
		</div>
		</form>
		</div>
	</div>
	<div class="ef-status"><span>Mode:=VIEW</span><span>Header New Record → Function Key (F5)</span></div>
</div>
