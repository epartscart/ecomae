<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$exportBase = epc_erp_tab_url($erpUrl, 'reports', $date_from_str, $date_to_str);

erp_page_header(
	'<i class="fa fa-table"></i> Reporting center',
	'Export CSV snapshots for GL trial balance, completed sales, and stock valuation.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Reports'),
	)
);
erp_filter_bar($erpUrl, 'reports', $date_from_str, $date_to_str);
?>
<div class="epc-erp-report-grid">
	<div class="epc-erp-report-tile">
		<h5><i class="fa fa-book"></i> General ledger — trial balance</h5>
		<p class="text-muted">COA balances as of the period end date.</p>
		<a class="btn btn-primary btn-sm" href="<?php echo epc_erp_h($exportBase . '&export=gl'); ?>"><i class="fa fa-download"></i> Export CSV</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'gl', $date_from_str, $date_to_str)); ?>">Open GL</a>
	</div>
	<div class="epc-erp-report-tile">
		<h5><i class="fa fa-line-chart"></i> Sales (completed orders)</h5>
		<p class="text-muted">Revenue lines for completed orders in the date range.</p>
		<a class="btn btn-primary btn-sm" href="<?php echo epc_erp_h($exportBase . '&export=sales'); ?>"><i class="fa fa-download"></i> Export CSV</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'revenue', $date_from_str, $date_to_str)); ?>">Open Revenue</a>
	</div>
	<div class="epc-erp-report-tile">
		<h5><i class="fa fa-cubes"></i> Stock valuation</h5>
		<p class="text-muted">On-hand quantities and weighted average value by warehouse.</p>
		<a class="btn btn-primary btn-sm" href="<?php echo epc_erp_h($exportBase . '&export=stock'); ?>"><i class="fa fa-download"></i> Export CSV</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str)); ?>">Open Inventory</a>
	</div>
	<div class="epc-erp-report-tile">
		<h5><i class="fa fa-percent"></i> UAE VAT return</h5>
		<p class="text-muted">Output / input VAT for the selected period.</p>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'vat_return', $date_from_str, $date_to_str)); ?>">Open VAT tab</a>
	</div>
</div>
