<?php
/**
 * Data Migration Tool — transfer data from existing systems into ERP.
 * Supports: open balance transfer, full transaction transfer, Excel upload.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-exchange"></i> Data Migration',
	'Import data from previous systems — open balances, transactions, customer/supplier masters, inventory. Excel-based upload with validation.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Data Migration'),
	),
	array(array('label' => 'New import', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-upload'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-road"></i> Migration approach</h4>
	<div class="row">
		<div class="col-md-6">
			<div class="panel panel-primary">
				<div class="panel-heading"><strong><i class="fa fa-balance-scale"></i> Open Balance Transfer</strong></div>
				<div class="panel-body">
					<p>Transfer only outstanding balances (AR, AP, Bank, GL) as of a cut-off date. Historical transactions remain in the old system.</p>
					<ul class="list-unstyled" style="font-size:13px;">
						<li><i class="fa fa-check text-success"></i> Customer opening balances</li>
						<li><i class="fa fa-check text-success"></i> Supplier opening balances</li>
						<li><i class="fa fa-check text-success"></i> Bank account balances</li>
						<li><i class="fa fa-check text-success"></i> GL trial balance</li>
						<li><i class="fa fa-check text-success"></i> Inventory quantities &amp; values</li>
					</ul>
					<button class="btn btn-primary btn-sm"><i class="fa fa-play"></i> Start open balance import</button>
				</div>
			</div>
		</div>
		<div class="col-md-6">
			<div class="panel panel-success">
				<div class="panel-heading"><strong><i class="fa fa-database"></i> Full Transaction Transfer</strong></div>
				<div class="panel-body">
					<p>Transfer complete transaction history — invoices, payments, journals, stock movements. Preserves full audit trail.</p>
					<ul class="list-unstyled" style="font-size:13px;">
						<li><i class="fa fa-check text-success"></i> All invoices &amp; credit notes</li>
						<li><i class="fa fa-check text-success"></i> Payment receipts &amp; vouchers</li>
						<li><i class="fa fa-check text-success"></i> Journal entries</li>
						<li><i class="fa fa-check text-success"></i> Purchase orders &amp; GRN</li>
						<li><i class="fa fa-check text-success"></i> Stock movements &amp; adjustments</li>
					</ul>
					<button class="btn btn-success btn-sm"><i class="fa fa-play"></i> Start full transfer</button>
				</div>
			</div>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-file-excel-o"></i> Excel upload templates</h4>
	<p class="text-muted">Download the template, fill in your data, then upload. The system validates all rows before importing.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Template</th><th>Description</th><th>Required fields</th><th></th></tr></thead>
		<tbody>
			<tr><td><strong>Customer Master</strong></td><td>Customer names, contacts, groups, credit limits</td><td>Name, Phone, Email</td><td><a class="btn btn-xs btn-default"><i class="fa fa-download"></i> Download</a> <a class="btn btn-xs btn-primary"><i class="fa fa-upload"></i> Upload</a></td></tr>
			<tr><td><strong>Supplier Master</strong></td><td>Supplier details, payment terms, tax registration</td><td>Name, TRN, Terms</td><td><a class="btn btn-xs btn-default"><i class="fa fa-download"></i> Download</a> <a class="btn btn-xs btn-primary"><i class="fa fa-upload"></i> Upload</a></td></tr>
			<tr><td><strong>Chart of Accounts</strong></td><td>GL account codes, names, types, groups</td><td>Code, Name, Type</td><td><a class="btn btn-xs btn-default"><i class="fa fa-download"></i> Download</a> <a class="btn btn-xs btn-primary"><i class="fa fa-upload"></i> Upload</a></td></tr>
			<tr><td><strong>Opening Balances</strong></td><td>Trial balance as of cut-off date</td><td>Account, Debit, Credit</td><td><a class="btn btn-xs btn-default"><i class="fa fa-download"></i> Download</a> <a class="btn btn-xs btn-primary"><i class="fa fa-upload"></i> Upload</a></td></tr>
			<tr><td><strong>Inventory</strong></td><td>Product SKUs, quantities, costs, locations</td><td>SKU, Qty, Unit cost</td><td><a class="btn btn-xs btn-default"><i class="fa fa-download"></i> Download</a> <a class="btn btn-xs btn-primary"><i class="fa fa-upload"></i> Upload</a></td></tr>
			<tr><td><strong>Invoices</strong></td><td>Historical sales invoices with line items</td><td>Date, Customer, Amount</td><td><a class="btn btn-xs btn-default"><i class="fa fa-download"></i> Download</a> <a class="btn btn-xs btn-primary"><i class="fa fa-upload"></i> Upload</a></td></tr>
			<tr><td><strong>Fixed Assets</strong></td><td>Asset register with depreciation schedules</td><td>Asset, Cost, Date, Life</td><td><a class="btn btn-xs btn-default"><i class="fa fa-download"></i> Download</a> <a class="btn btn-xs btn-primary"><i class="fa fa-upload"></i> Upload</a></td></tr>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-history"></i> Import history</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="dm_history">
		<thead><tr><th>Date</th><th>Type</th><th>File</th><th>Records</th><th>Success</th><th>Errors</th><th>Status</th><th></th></tr></thead>
		<tbody>
			<tr><td>2026-06-20</td><td>Customer Master</td><td>customers_june.xlsx</td><td>245</td><td>240</td><td>5</td><td><span class="label label-warning">Partial</span></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i> View errors</a></td></tr>
			<tr><td>2026-06-18</td><td>Opening Balances</td><td>trial_balance.xlsx</td><td>89</td><td>89</td><td>0</td><td><span class="label label-success">Complete</span></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-15</td><td>Inventory</td><td>stock_items.xlsx</td><td>1,204</td><td>1,198</td><td>6</td><td><span class="label label-warning">Partial</span></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i> View errors</a></td></tr>
		</tbody>
	</table>
</div>
<?php
erp_section_card('Data Migration', ob_get_clean(), array('icon' => 'fa-exchange'));
