<?php
/**
 * Customer Groups / Types — classify customers for segmented reporting, pricing tiers, and credit policies.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-users"></i> Customer Groups',
	'Classify customers by group/type for reporting, pricing tiers, credit policies, and marketing segmentation.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Customer Groups'),
	),
	array(array('label' => 'New group', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-th-large"></i> Customer group master</h4>
	<p class="text-muted">Define groups to segment customers — reports, statements, aging, and promotions can all be filtered by group.</p>
	<table class="table table-bordered table-condensed" id="cg_table" style="font-size:13px;">
		<thead><tr><th>Code</th><th>Group name</th><th>Type</th><th>Credit limit</th><th>Payment terms</th><th>Discount %</th><th>Customers</th><th>Actions</th></tr></thead>
		<tbody id="cg_tbody"></tbody>
	</table>
	<button type="button" class="btn btn-default btn-sm" id="cg_add"><i class="fa fa-plus"></i> Add group</button>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-bar-chart"></i> Group-level reporting</h4>
	<p class="text-muted">Generate reports filtered by customer group — revenue by group, aging by group, top products by group.</p>
	<div class="row">
		<div class="col-md-4">
			<div class="panel panel-default"><div class="panel-body text-center">
				<i class="fa fa-pie-chart fa-2x text-primary"></i>
				<h5>Revenue by group</h5>
				<p class="text-muted small">Monthly/yearly breakdown per group</p>
			</div></div>
		</div>
		<div class="col-md-4">
			<div class="panel panel-default"><div class="panel-body text-center">
				<i class="fa fa-clock-o fa-2x text-warning"></i>
				<h5>Aging by group</h5>
				<p class="text-muted small">Outstanding receivables grouped</p>
			</div></div>
		</div>
		<div class="col-md-4">
			<div class="panel panel-default"><div class="panel-body text-center">
				<i class="fa fa-trophy fa-2x text-success"></i>
				<h5>Top products by group</h5>
				<p class="text-muted small">Best sellers per segment</p>
			</div></div>
		</div>
	</div>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Group settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Default group for new customers</label>
			<select class="form-control input-sm"><option>General</option><option>VIP</option><option>Wholesale</option></select>
		</div>
		<div class="pm-field"><label>Auto-upgrade rule</label>
			<select class="form-control input-sm"><option value="0">Manual only</option><option value="1">Auto-upgrade on revenue threshold</option></select>
		</div>
		<div class="pm-field"><label>Revenue threshold for upgrade</label>
			<input type="number" class="form-control input-sm" value="50000">
		</div>
	</div>
</div>

<script>
(function(){
	var groups = [
		{code:'GEN',name:'General',type:'Retail',credit:'5,000',terms:'Net 30',disc:'0',count:124},
		{code:'VIP',name:'VIP Customers',type:'Premium',credit:'50,000',terms:'Net 60',disc:'5',count:18},
		{code:'WHL',name:'Wholesale',type:'B2B',credit:'100,000',terms:'Net 45',disc:'12',count:7},
		{code:'GOV',name:'Government',type:'Institutional',credit:'250,000',terms:'Net 90',disc:'8',count:3},
		{code:'TRD',name:'Trade / Dealers',type:'B2B',credit:'75,000',terms:'Net 30',disc:'15',count:12},
		{code:'TMP',name:'Temporary / One-time',type:'Walk-in',credit:'0',terms:'COD',disc:'0',count:56},
	];
	var tbody=document.getElementById('cg_tbody');
	groups.forEach(function(g){
		var tr=document.createElement('tr');
		tr.innerHTML='<td><code>'+g.code+'</code></td><td><strong>'+g.name+'</strong></td><td><span class="label label-info">'+g.type+'</span></td><td>'+g.credit+'</td><td>'+g.terms+'</td><td>'+g.disc+'%</td><td><span class="badge">'+g.count+'</span></td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a> <a class="btn btn-xs btn-danger"><i class="fa fa-trash"></i></a></td>';
		tbody.appendChild(tr);
	});
})();
</script>
<?php
erp_section_card('Customer Groups', ob_get_clean(), array('icon' => 'fa-users'));
