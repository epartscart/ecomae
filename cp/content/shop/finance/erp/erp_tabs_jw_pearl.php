<?php
/**
 * Jewellery ERP — Pearl Master.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-circle-o"></i> Pearl Master', 'Natural and cultured pearl inventory with grading.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Pearl master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Pearl Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_pl_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
	</div>
	<div class="ef-body">
		<div id="jw_pl_form" style="display:block;">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_pearl_save">

		<div class="ef-section">
			<div class="ef-row">
				<div class="ef-field"><label>Item Code</label><input name="item_code" required></div>
				<div class="ef-field ef-field-wide"><label>Description</label><input name="description"></div>
				<div class="ef-field"><label>Cost Centre</label><input name="cost_centre" value="PERL"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Pearl Type</label><select name="pearl_type"><option value="Natural">Natural</option><option value="Cultured">Cultured</option><option value="FW">Freshwater</option><option value="SW">South Sea</option><option value="Tahitian">Tahitian</option><option value="Akoya">Akoya</option></select></div>
				<div class="ef-field"><label>Shape</label><select name="shape"><option value="Round">Round</option><option value="Near Round">Near Round</option><option value="Oval">Oval</option><option value="Button">Button</option><option value="Drop">Drop</option><option value="Baroque">Baroque</option></select></div>
				<div class="ef-field"><label>Luster</label><select name="luster"><option value="Excellent">Excellent</option><option value="Very Good">Very Good</option><option value="Good">Good</option><option value="Fair">Fair</option></select></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Size (mm)</label><input name="size_mm" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Weight (ct)</label><input name="weight_ct" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Color</label><input name="color" placeholder="White / Cream / Pink"></div>
				<div class="ef-field"><label>Grade</label><select name="grade"><option value="AAA">AAA</option><option value="AA+">AA+</option><option value="AA">AA</option><option value="A+">A+</option><option value="A">A</option><option value="B">B</option></select></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Pcs</label><input name="pcs" type="number" value="1"></div>
				<div class="ef-field"><label>Cost/pc</label><input name="cost_per_pc" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Sell/pc</label><input name="sell_per_pc" type="number" step="0.01" value="0"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Vendor</label><input name="vendor"></div>
				<div class="ef-field"><label>Country</label><input name="country"></div>
				<div class="ef-field"><label>Certificate #</label><input name="certificate_no"></div>
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
