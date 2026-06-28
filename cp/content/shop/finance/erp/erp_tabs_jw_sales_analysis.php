<?php
/**
 * Jewellery ERP — Metal Sales Analysis.
 * Ref: Suntech Metal Sales Analysis screenshot (filters + results grid with summary).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$divisions = epc_jewel_divisions();

erp_page_header('<i class="fa fa-bar-chart"></i> Metal Sales Analysis', 'Sales analysis by metal, karat, salesman, and date range.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Sales analysis'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Sales Analysis</div>
	<div class="ef-toolbar">
		<button class="btn btn-primary btn-xs" onclick="jwSaRun()"><i class="fa fa-play"></i> Run Analysis</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-file-excel-o"></i> Export</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title">Filter Criteria</span>
			<div class="ef-row">
				<div class="ef-field"><label>From Date</label><input id="sa_from" type="date" value="<?php echo date('Y-m-01'); ?>"></div>
				<div class="ef-field"><label>To Date</label><input id="sa_to" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Branch</label>
					<select id="sa_branch"><option value="">All Branches</option><option value="HO">HO</option></select>
				</div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Division</label>
					<select id="sa_division"><option value="">All Metals</option><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($l); ?></option><?php endforeach; ?></select>
				</div>
				<div class="ef-field"><label>Karat</label><input id="sa_karat" maxlength="10" placeholder="All"></div>
				<div class="ef-field"><label>Salesman</label><input id="sa_salesman" maxlength="20" placeholder="All"></div>
				<div class="ef-field"><label>Category</label><input id="sa_category" maxlength="30" placeholder="All"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Customer</label><input id="sa_customer" maxlength="20" placeholder="All"></div>
				<div class="ef-field"><label>Group By</label>
					<select id="sa_group"><option value="date">Date</option><option value="metal">Metal</option><option value="karat">Karat</option><option value="salesman">Salesman</option><option value="category">Category</option><option value="customer">Customer</option></select>
				</div>
				<div class="ef-field"><label>Sort By</label>
					<select id="sa_sort"><option value="date_desc">Date (Latest)</option><option value="amount_desc">Amount (High-Low)</option><option value="qty_desc">Qty (High-Low)</option></select>
				</div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Analysis Results</span>
			<table class="ef-grid" id="sa_results">
				<thead><tr>
					<th>No.</th><th>Group</th><th>Inv Count</th><th>Total Pcs</th>
					<th>Total Gross Wt</th><th>Total Net Wt</th><th>Metal Amt</th>
					<th>MC Amt</th><th>Stone Amt</th><th>Net Amt</th><th>VAT</th><th>Gross Total</th>
				</tr></thead>
				<tbody>
					<tr><td colspan="12" style="text-align:center;color:#999">Click "Run Analysis" to generate results</td></tr>
				</tbody>
			</table>
		</div>

		<div class="ef-totals">
			<div class="ef-row">
				<div class="ef-field"><label>Total Invoices</label><input id="sa_total_inv" value="0" readonly></div>
				<div class="ef-field"><label>Total Pcs</label><input id="sa_total_pcs" value="0" readonly></div>
				<div class="ef-field"><label>Total Gross Wt</label><input id="sa_total_gwt" value="0.000" readonly></div>
				<div class="ef-field"><label>Total Net Wt</label><input id="sa_total_nwt" value="0.000" readonly></div>
				<div class="ef-field"><label>Grand Total</label><input id="sa_grand_total" value="0.00" readonly style="font-weight:bold"></div>
			</div>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=ANALYSIS</span>
		<span>Metal Sales Analysis Report</span>
	</div>
</div>
<script>
function jwSaRun(){
	document.getElementById('sa_results').querySelector('tbody').innerHTML='<tr><td colspan="12" style="text-align:center"><i class="fa fa-spinner fa-spin"></i> Generating analysis...</td></tr>';
}
</script>
