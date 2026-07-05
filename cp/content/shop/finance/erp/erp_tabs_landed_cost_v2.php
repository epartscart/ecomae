<?php
/**
 * Landed Cost V2 — distribute expenses over product cost by value, weight, or quantity.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-ship"></i> Landed Cost Distribution',
	'Distribute freight, customs, insurance and handling charges over product costs by value, weight, volume, or quantity method.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Landed Cost'),
	),
	array(array('label' => 'New Cost Sheet', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-calculator"></i> Cost distribution sheets</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="lc_table">
		<thead><tr><th>Sheet #</th><th>PO Reference</th><th>Supplier</th><th>Total goods</th><th>Freight</th><th>Customs</th><th>Insurance</th><th>Other</th><th>Method</th><th>Status</th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-plus-circle"></i> Create cost sheet</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>PO / GRN reference</label><input type="text" class="form-control input-sm" placeholder="PO-2026-001"></div>
		<div class="pm-field"><label>Supplier</label><input type="text" class="form-control input-sm" placeholder="Search supplier..."></div>
		<div class="pm-field"><label>Distribution method</label>
			<select class="form-control input-sm"><option>By value (pro-rata)</option><option>By weight</option><option>By volume</option><option>By quantity</option><option>Equal split</option></select>
		</div>
	</div>
	<h5 style="margin-top:12px;">Expense lines</h5>
	<table class="table table-bordered table-condensed" style="font-size:12px;">
		<thead><tr><th>Expense type</th><th>Amount</th><th>Currency</th><th>Vendor / Reference</th></tr></thead>
		<tbody>
			<tr><td><select class="form-control input-sm"><option>Freight</option><option>Customs duty</option><option>Insurance</option><option>Handling</option><option>Inspection</option><option>Other</option></select></td><td><input type="number" class="form-control input-sm" step="0.01"></td><td><select class="form-control input-sm"><option>AED</option><option>USD</option><option>EUR</option><option>GBP</option></select></td><td><input type="text" class="form-control input-sm"></td></tr>
		</tbody>
	</table>
	<button class="btn btn-default btn-xs"><i class="fa fa-plus"></i> Add expense line</button>
	<hr>
	<button class="btn btn-primary btn-sm"><i class="fa fa-calculator"></i> Calculate &amp; distribute</button>
	<button class="btn btn-success btn-sm"><i class="fa fa-check"></i> Post to inventory cost</button>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-pie-chart"></i> Cost breakdown analysis</h4>
	<div class="row">
		<div class="col-md-3"><div class="panel panel-info"><div class="panel-body text-center"><h3 style="margin:0;color:#2563eb;">12,450</h3><p class="text-muted small">Total landed costs (MTD)</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-success"><div class="panel-body text-center"><h3 style="margin:0;color:#16a34a;">3.2%</h3><p class="text-muted small">Avg cost uplift %</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-warning"><div class="panel-body text-center"><h3 style="margin:0;color:#d97706;">7</h3><p class="text-muted small">Pending sheets</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#475569;">45</h3><p class="text-muted small">Sheets this quarter</p></div></div></div>
	</div>
</div>
<script>
(function(){
	var rows=[
		{id:'LC-001',po:'PO-2026-089',sup:'Far East Trading',goods:'45,000',freight:'2,100',customs:'1,800',ins:'450',other:'300',method:'By value',status:'Posted'},
		{id:'LC-002',po:'PO-2026-092',sup:'Guangzhou Metals',goods:'72,000',freight:'3,400',customs:'2,900',ins:'720',other:'0',method:'By weight',status:'Posted'},
		{id:'LC-003',po:'PO-2026-101',sup:'Mumbai Gems Int',goods:'28,500',freight:'1,200',customs:'1,425',ins:'285',other:'150',method:'By value',status:'Draft'},
	];
	var tb=document.querySelector('#lc_table tbody');
	rows.forEach(function(r){
		var cls=r.status==='Posted'?'success':'warning';
		tb.innerHTML+='<tr><td><code>'+r.id+'</code></td><td>'+r.po+'</td><td>'+r.sup+'</td><td>'+r.goods+'</td><td>'+r.freight+'</td><td>'+r.customs+'</td><td>'+r.ins+'</td><td>'+r.other+'</td><td>'+r.method+'</td><td><span class="label label-'+cls+'">'+r.status+'</span></td></tr>';
	});
})();
</script>
<?php
erp_section_card('Landed Cost Distribution', ob_get_clean(), array('icon' => 'fa-ship'));
