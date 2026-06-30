<?php
/**
 * Drill-Down Reporting — click any report figure to reach transaction details.
 * Every report line is a hyperlink: Report → Account → Journal → Source Transaction.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-search-plus"></i> Drill-Down Reports',
	'Click any report figure to drill down to the underlying transactions. From summary → detail → source document in one click.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Drill-Down Reports'),
	),
	array()
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-sitemap"></i> Drill-down navigation</h4>
	<p class="text-muted">Every clickable amount in reports follows this drill path:</p>
	<div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin:16px 0;">
		<span class="label label-primary" style="font-size:13px;padding:6px 12px;">Report total</span>
		<i class="fa fa-arrow-right text-muted"></i>
		<span class="label label-info" style="font-size:13px;padding:6px 12px;">Account breakdown</span>
		<i class="fa fa-arrow-right text-muted"></i>
		<span class="label label-success" style="font-size:13px;padding:6px 12px;">Journal entries</span>
		<i class="fa fa-arrow-right text-muted"></i>
		<span class="label label-warning" style="font-size:13px;padding:6px 12px;">Source transaction</span>
	</div>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-list-alt"></i> Available drill-down reports</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Report</th><th>Drill levels</th><th>Source documents</th><th></th></tr></thead>
		<tbody>
			<tr><td><strong>Trial Balance</strong></td><td>Account → Journals</td><td>Invoices, Payments, JVs</td><td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'gl', $date_from_str, $date_to_str, 'finance')); ?>"><i class="fa fa-external-link"></i></a></td></tr>
			<tr><td><strong>Profit &amp; Loss</strong></td><td>Category → Account → Journals</td><td>Revenue invoices, Expense bills</td><td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'pl', $date_from_str, $date_to_str, 'finance')); ?>"><i class="fa fa-external-link"></i></a></td></tr>
			<tr><td><strong>Balance Sheet</strong></td><td>Category → Account → Journals</td><td>All document types</td><td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'balance_sheet', $date_from_str, $date_to_str, 'finance')); ?>"><i class="fa fa-external-link"></i></a></td></tr>
			<tr><td><strong>Aging Report</strong></td><td>Customer → Invoices</td><td>AR Invoices, Credit notes</td><td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'aging', $date_from_str, $date_to_str, 'finance')); ?>"><i class="fa fa-external-link"></i></a></td></tr>
			<tr><td><strong>VAT Return</strong></td><td>Box → Accounts → Invoices</td><td>Tax invoices, Imports</td><td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'vat_return', $date_from_str, $date_to_str, 'finance')); ?>"><i class="fa fa-external-link"></i></a></td></tr>
			<tr><td><strong>Inventory Valuation</strong></td><td>Category → SKU → Movements</td><td>GRN, DN, Adjustments</td><td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str, 'operations')); ?>"><i class="fa fa-external-link"></i></a></td></tr>
			<tr><td><strong>Sales Analysis</strong></td><td>Period → Customer → Orders</td><td>Sales orders, Invoices</td><td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'revenue', $date_from_str, $date_to_str, 'sales')); ?>"><i class="fa fa-external-link"></i></a></td></tr>
			<tr><td><strong>Purchase Analysis</strong></td><td>Supplier → POs → GRN</td><td>Purchase orders, Bills</td><td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'purchases', $date_from_str, $date_to_str, 'procurement')); ?>"><i class="fa fa-external-link"></i></a></td></tr>
		</tbody>
	</table>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-mouse-pointer"></i> Demo: Trial Balance drill-down</h4>
	<p class="text-muted">Click any blue amount below to see the journal entries behind it:</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="dd_demo">
		<thead><tr><th>Account</th><th>Debit</th><th>Credit</th><th>Net</th></tr></thead>
		<tbody></tbody>
	</table>
	<div id="dd_detail" style="display:none;margin-top:12px;padding:12px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;">
		<h5 id="dd_detail_title" style="margin:0 0 8px;"></h5>
		<table class="table table-condensed table-bordered" style="font-size:12px;" id="dd_detail_table">
			<thead><tr><th>Date</th><th>Journal #</th><th>Description</th><th>Debit</th><th>Credit</th><th>Source</th></tr></thead>
			<tbody id="dd_detail_body"></tbody>
		</table>
		<button class="btn btn-xs btn-default" onclick="document.getElementById('dd_detail').style.display='none'"><i class="fa fa-times"></i> Close</button>
	</div>
</div>

<script>
(function(){
	var accounts=[
		{name:'1100 Cash & Bank',dr:245000,cr:198000,journals:[
			{date:'2026-06-20',jn:'JV-0045',desc:'Customer receipt — Al Fardan',dr:12000,cr:0,src:'RV-2026-0033'},
			{date:'2026-06-18',jn:'JV-0042',desc:'Supplier payment — Gold House',dr:0,cr:8500,src:'PV-2026-0021'},
			{date:'2026-06-15',jn:'JV-0038',desc:'Bank charges',dr:0,cr:250,src:'JV-2026-0038'}
		]},
		{name:'1200 Accounts Receivable',dr:180000,cr:145000,journals:[
			{date:'2026-06-20',jn:'JV-0044',desc:'Invoice posted — INV-0045',dr:8900,cr:0,src:'INV-2026-0045'},
			{date:'2026-06-19',jn:'JV-0043',desc:'Credit note — CN-0012',dr:0,cr:2100,src:'CN-2026-0012'}
		]},
		{name:'4100 Revenue',dr:0,cr:320000,journals:[
			{date:'2026-06-20',jn:'JV-0044',desc:'Sales — 18K Ring',dr:0,cr:8900,src:'INV-2026-0045'},
			{date:'2026-06-18',jn:'JV-0041',desc:'Sales — Diamond Set',dr:0,cr:15200,src:'INV-2026-0044'}
		]},
		{name:'5100 Cost of Goods Sold',dr:210000,cr:0,journals:[
			{date:'2026-06-20',jn:'JV-0044',desc:'COGS — 18K Ring',dr:5800,cr:0,src:'INV-2026-0045'}
		]},
		{name:'2100 Accounts Payable',dr:95000,cr:130000,journals:[
			{date:'2026-06-19',jn:'JV-0043',desc:'Supplier bill — Metal Corp',dr:0,cr:22000,src:'BILL-2026-0018'}
		]}
	];
	var tb=document.querySelector('#dd_demo tbody');
	accounts.forEach(function(a){
		var tr=document.createElement('tr');
		tr.innerHTML='<td>'+a.name+'</td><td><a href="#" class="dd-link" style="color:#2563eb;text-decoration:underline;cursor:pointer;">'+a.dr.toLocaleString()+'</a></td><td><a href="#" class="dd-link" style="color:#2563eb;text-decoration:underline;cursor:pointer;">'+a.cr.toLocaleString()+'</a></td><td>'+(a.dr-a.cr).toLocaleString()+'</td>';
		tr.querySelectorAll('.dd-link').forEach(function(link){
			link.addEventListener('click',function(e){
				e.preventDefault();
				var det=document.getElementById('dd_detail');
				det.style.display='block';
				document.getElementById('dd_detail_title').textContent='Journals for: '+a.name;
				var db=document.getElementById('dd_detail_body');
				db.innerHTML='';
				a.journals.forEach(function(j){
					var r=document.createElement('tr');
					r.innerHTML='<td>'+j.date+'</td><td><code>'+j.jn+'</code></td><td>'+j.desc+'</td><td>'+(j.dr?j.dr.toLocaleString():'')+'</td><td>'+(j.cr?j.cr.toLocaleString():'')+'</td><td><a href="#" style="color:#2563eb;">'+j.src+'</a></td>';
					db.appendChild(r);
				});
			});
		});
		tb.appendChild(tr);
	});
})();
</script>
<?php
erp_section_card('Drill-Down Reports', ob_get_clean(), array('icon' => 'fa-search-plus'));
