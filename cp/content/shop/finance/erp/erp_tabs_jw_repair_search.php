<?php
/**
 * Jewellery ERP — Repair Search.
 * Ref: Suntech — search repair jobs by various criteria.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-search"></i> Repair Search', 'Search repair jobs by job number, customer, mobile, status.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair search'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Search</div>
	<div class="ef-toolbar">
		<button class="btn btn-primary btn-xs" onclick="jwRsSearch()"><i class="fa fa-search"></i> Search</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Clear</button>
	</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title">Search Criteria</span>
			<div class="ef-row">
				<div class="ef-field"><label>Job No</label><input id="rps_job_no" maxlength="20"></div>
				<div class="ef-field"><label>Customer Name</label><input id="rps_customer" maxlength="80"></div>
				<div class="ef-field"><label>Mobile</label><input id="rps_mobile" maxlength="20"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>From Date</label><input id="rps_from" type="date"></div>
				<div class="ef-field"><label>To Date</label><input id="rps_to" type="date"></div>
				<div class="ef-field"><label>Status</label>
					<select id="rps_status"><option value="">All</option><option value="Received">Received</option><option value="In Progress">In Progress</option><option value="Workshop">Workshop</option><option value="Ready">Ready</option><option value="Delivered">Delivered</option></select>
				</div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Repair Type</label>
					<select id="rps_type"><option value="">All</option><option value="Resize">Resize</option><option value="Polish">Polish</option><option value="Rhodium">Rhodium</option><option value="Solder">Solder</option><option value="Stone Setting">Stone Setting</option><option value="Engraving">Engraving</option><option value="Other">Other</option></select>
				</div>
				<div class="ef-field"><label>Metal</label><input id="rps_metal" maxlength="10"></div>
				<div class="ef-field"><label>Branch</label>
					<select id="rps_branch"><option value="">All</option><option value="HO">HO</option></select>
				</div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Search Results</span>
			<table class="ef-grid" id="rps_results">
				<thead><tr>
					<th>No.</th><th>Job No</th><th>Date</th><th>Customer</th>
					<th>Mobile</th><th>Item</th><th>Repair Type</th><th>Metal</th>
					<th>Gross Wt</th><th>Est. Charge</th><th>Status</th><th>Promise</th>
				</tr></thead>
				<tbody>
					<tr><td colspan="12" style="text-align:center;color:#999">Enter search criteria and click "Search"</td></tr>
				</tbody>
			</table>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=SEARCH</span>
		<span>Repair Search</span>
	</div>
</div>
<script>
function jwRsSearch(){
	document.getElementById('rps_results').querySelector('tbody').innerHTML='<tr><td colspan="12" style="text-align:center"><i class="fa fa-spinner fa-spin"></i> Searching...</td></tr>';
}
</script>
