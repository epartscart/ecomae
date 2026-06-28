<?php
/**
 * Jewellery ERP — Metal Sales Analysis Report.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$divisions = epc_jewel_divisions();

erp_page_header('<i class="fa fa-area-chart"></i> Metal Sales Analysis', 'Sales trends by date, salesman, division.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Sales analysis'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Sales Analysis</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-refresh"></i> Refresh</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-download"></i> Export</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print</button>
		<div style="margin-left:auto;display:flex;gap:6px;align-items:center;font-size:11px;">
			<label>From</label><input type="date" value="<?php echo date('Y-m-01'); ?>" style="font-size:11px">
			<label>To</label><input type="date" value="<?php echo date('Y-m-d'); ?>" style="font-size:11px">
			<label>Group By</label><select style="font-size:11px"><option>Date</option><option>Salesman</option><option>Division</option><option>Metal</option><option>Karat</option><option>Customer</option></select>
			<label>Voc Type</label><select style="font-size:11px"><option value="">All</option><option>RIN</option><option>MSL</option><option>SRT</option></select>
		</div>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>Group</th><th>Invoices</th><th>Total Pcs</th><th>Total Gms</th>
				<th>Metal Amt</th><th>Making Amt</th><th>Stone Amt</th><th>VAT</th><th>Gross Total</th>
			</tr></thead>
			<tbody>
				<tr><td colspan="9" style="text-align:center;color:#999;padding:20px">Select date range and click Refresh to generate the report.</td></tr>
			</tbody>
		</table>
	</div>
	<div class="ef-status"><span>Report view</span><span>Period: Current month</span></div>
</div>
