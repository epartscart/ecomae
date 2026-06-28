<?php
/**
 * Jewellery ERP — Purchase Fixing (PFX).
 * Fix metal rate for future purchase against a floating agreement.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-lock"></i> Purchase Fixing', 'Lock metal rate on floating purchases.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Purchase fixing'),
));
?>
<div class="ef-window">
	<div class="ef-title">Purchase Fixing - (PFX)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_fixing_save">
		<input type="hidden" name="fix_direction" value="purchase">

		<div class="ef-section">
			<span class="ef-section-title">Fixing Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Fix Date</label><input name="fix_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Fix No.</label><input name="fix_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Party Code</label><input name="party_code" required></div>
				<div class="ef-field ef-field-wide"><label>Party Name</label><input name="party_name"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Metal</label><select name="metal"><option value="G">Gold</option><option value="S">Silver</option><option value="T">Platinum</option></select></div>
				<div class="ef-field"><label>Karat</label><input name="karat" placeholder="24"></div>
				<div class="ef-field"><label>Rate Type</label><select name="rate_type"><option value="GMS">GMS</option><option value="GOZ">GOZ</option><option value="KB">KB</option></select></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Fixing Wt (Gms)</label><input name="fixing_wt" type="number" step="0.001" value="0"></div>
				<div class="ef-field"><label>Fixing Rate</label><input name="fixing_rate" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Fixing Amount</label><input name="fixing_amount" type="number" step="0.01" value="0" class="ef-readonly" readonly></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Ref. Voucher</label><input name="ref_voucher" placeholder="Original floating purchase voc"></div>
				<div class="ef-field"><label>Currency</label><select name="currency"><option value="AED">AED</option><option value="USD">USD</option></select></div>
			</div>
		</div>
		<div class="ef-section">
			<span class="ef-section-title">Narration</span>
			<textarea name="narration" class="ef-narration"></textarea>
		</div>
		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Voc Type: PFX — Purchase Fixing</span></div>
</div>
