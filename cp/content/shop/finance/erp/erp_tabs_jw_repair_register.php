<?php
/**
 * Jewellery ERP — Repair Register (Report).
 * List all repair jobs with status tracking.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;

erp_page_header('<i class="fa fa-list-alt"></i> Repair Register', 'All repair jobs with status tracking.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair register'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Register</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-refresh"></i> Refresh</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-download"></i> Export</button>
		<div style="margin-left:auto;display:flex;gap:6px;align-items:center;font-size:11px;">
			<label>From</label><input type="date" name="from" value="<?php echo date('Y-m-01'); ?>" style="font-size:11px">
			<label>To</label><input type="date" name="to" value="<?php echo date('Y-m-d'); ?>" style="font-size:11px">
			<label>Status</label><select style="font-size:11px"><option value="">All</option><option>Received</option><option>In Workshop</option><option>Ready</option><option>Delivered</option><option>Invoiced</option></select>
		</div>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>Repair No.</th><th>Date</th><th>Customer</th><th>Mobile</th>
				<th>Items</th><th>Metal</th><th>Gross Wt</th><th>Est. Cost</th>
				<th>Status</th><th>Promise Date</th><th>Days Open</th>
			</tr></thead>
			<tbody>
				<tr><td colspan="11" style="text-align:center;color:#999;padding:20px">No repair records found. Use the date filter above to search.</td></tr>
			</tbody>
		</table>
	</div>
	<div class="ef-status"><span>Report view</span><span>Filtered: Current month</span></div>
</div>
