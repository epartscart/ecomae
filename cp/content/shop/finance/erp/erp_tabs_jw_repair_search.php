<?php
/**
 * Jewellery ERP — Repair Item Search.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);

erp_page_header('<i class="fa fa-search"></i> Repair Search', 'Search repair items by repair no, customer, mobile, or item description.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair search'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Item Search</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title">Search Criteria</span>
			<div class="ef-row">
				<div class="ef-field ef-field-wide"><label>Repair No.</label><input name="search_repair_no" placeholder="REP-xxxx"></div>
				<div class="ef-field ef-field-wide"><label>Customer Name / Code</label><input name="search_customer"></div>
				<div class="ef-field"><label>Mobile</label><input name="search_mobile"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>From Date</label><input name="search_from" type="date" value="<?php echo date('Y-m-01'); ?>"></div>
				<div class="ef-field"><label>To Date</label><input name="search_to" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Status</label><select name="search_status"><option value="">All</option><option>Received</option><option>In Workshop</option><option>Ready</option><option>Delivered</option></select></div>
				<div class="ef-field"><label>Metal</label><select name="search_metal"><option value="">All</option><option value="G">Gold</option><option value="S">Silver</option><option value="T">Platinum</option></select></div>
			</div>
			<div class="ef-actions" style="justify-content:flex-start">
				<button type="button" class="btn btn-primary btn-sm"><i class="fa fa-search"></i> Search</button>
				<button type="button" class="btn btn-default btn-sm"><i class="fa fa-eraser"></i> Clear</button>
			</div>
		</div>
		<div id="repair_search_results">
			<table class="ef-grid">
				<thead><tr><th>Repair No.</th><th>Date</th><th>Customer</th><th>Item</th><th>Metal</th><th>Wt</th><th>Repair Type</th><th>Status</th><th>Action</th></tr></thead>
				<tbody><tr><td colspan="9" style="text-align:center;color:#999;padding:20px">Enter search criteria and click Search.</td></tr></tbody>
			</table>
		</div>
	</div>
</div>
