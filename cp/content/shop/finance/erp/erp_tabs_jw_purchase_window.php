<?php
/**
 * Jewellery ERP — Purchase Window / Inquiry (PWN).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-binoculars"></i> Purchase Window', 'Purchase inquiry / quotation management.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Purchase window'),
));
?>
<div class="ef-window">
	<div class="ef-title">Purchase Window - (PWN)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-exchange"></i> Convert to Purchase</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_voucher_save">
		<input type="hidden" name="voc_type" value="PWN">

		<div class="ef-section">
			<span class="ef-section-title">Inquiry Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Inq. No.</label><input name="voc_no" class="ef-readonly" readonly placeholder="Auto" style="width:60px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Supplier Code</label><input name="party_code" required></div>
				<div class="ef-field ef-field-wide"><label>Supplier Name</label><input name="party_name"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Metal</label><select name="metal"><option value="G">Gold</option><option value="S">Silver</option><option value="T">Platinum</option></select></div>
				<div class="ef-field"><label>Karat</label><input name="karat" placeholder="24"></div>
				<div class="ef-field"><label>Quantity (Gms)</label><input name="quantity_gms" type="number" step="0.001" value="0"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Quoted Rate</label><input name="quoted_rate" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Rate Type</label><select name="rate_type"><option value="GMS">GMS</option><option value="GOZ">GOZ</option></select></div>
				<div class="ef-field"><label>Currency</label><input name="currency" value="AED" style="width:50px"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Valid Until</label><input name="valid_until" type="date"></div>
				<div class="ef-field"><label>Status</label><select name="status"><option value="Open">Open</option><option value="Approved">Approved</option><option value="Converted">Converted</option><option value="Cancelled">Cancelled</option></select></div>
			</div>
		</div>
		<div class="ef-section">
			<span class="ef-section-title">Remarks</span>
			<textarea name="narration" class="ef-narration"></textarea>
		</div>
		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Voc Type: PWN — Purchase Window</span></div>
</div>
