<?php
/**
 * Virtual Warehouse / Exhibition Stock — track stock held at exhibitions, displays,
 * consignment locations, or virtual warehouses separate from main inventory.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-building-o"></i> Virtual Warehouse',
	'Manage exhibition stock, display inventory, consignment goods, and virtual locations. Track items separate from main warehouse.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Virtual Warehouse'),
	),
	array(array('label' => 'New location', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-map-marker"></i> Virtual locations</h4>
	<p class="text-muted">Define virtual warehouses for stock taken out for exhibitions, displays, consignments, or inter-branch transit.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="vw_table">
		<thead><tr><th>Location code</th><th>Name</th><th>Type</th><th>Items</th><th>Value</th><th>Responsible</th><th>Status</th><th></th></tr></thead>
		<tbody></tbody>
	</table>
	<button class="btn btn-default btn-sm" id="vw_add"><i class="fa fa-plus"></i> Add virtual location</button>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-exchange"></i> Stock transfer</h4>
	<p class="text-muted">Move stock between main warehouse and virtual locations. All transfers are tracked with full audit trail.</p>
	<div class="pm-fields">
		<div class="pm-field"><label>From</label>
			<select class="form-control input-sm"><option>Main warehouse</option><option>VW-EXHIB-01 (Dubai Gold Show)</option><option>VW-DISP-01 (Showroom display)</option></select>
		</div>
		<div class="pm-field"><label>To</label>
			<select class="form-control input-sm"><option>VW-EXHIB-01 (Dubai Gold Show)</option><option>VW-DISP-01 (Showroom display)</option><option>Main warehouse</option></select>
		</div>
		<div class="pm-field"><label>Items</label><input type="text" class="form-control input-sm" placeholder="Select items..."></div>
		<div class="pm-field"><label>Reason</label>
			<select class="form-control input-sm"><option>Exhibition</option><option>Display</option><option>Consignment</option><option>Inter-branch</option><option>Return to main</option></select>
		</div>
		<div class="pm-field"><label>&nbsp;</label>
			<button class="btn btn-primary btn-sm"><i class="fa fa-exchange"></i> Transfer</button>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-history"></i> Transfer history</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Date</th><th>From</th><th>To</th><th>Items</th><th>Value</th><th>Reason</th><th>By</th></tr></thead>
		<tbody>
			<tr><td>2026-06-20</td><td>Main</td><td>VW-EXHIB-01</td><td>25 items</td><td>180,000 AED</td><td>Exhibition</td><td>Warehouse Mgr</td></tr>
			<tr><td>2026-06-18</td><td>Main</td><td>VW-DISP-01</td><td>12 items</td><td>95,000 AED</td><td>Display</td><td>Showroom Lead</td></tr>
			<tr><td>2026-06-15</td><td>VW-EXHIB-01</td><td>Main</td><td>8 items</td><td>62,000 AED</td><td>Return to main</td><td>Warehouse Mgr</td></tr>
		</tbody>
	</table>
</div>
<script>
(function(){
	var locs=[
		{code:'VW-EXHIB-01',name:'Dubai Gold Show 2026',type:'Exhibition',items:25,value:'180,000 AED',resp:'Events Team',status:'Active'},
		{code:'VW-DISP-01',name:'Showroom display — Mall of Emirates',type:'Display',items:12,value:'95,000 AED',resp:'Retail Manager',status:'Active'},
		{code:'VW-CONS-01',name:'Consignment — Al Fardan',type:'Consignment',items:8,value:'120,000 AED',resp:'Sales Manager',status:'Active'},
		{code:'VW-TRANS-01',name:'In-transit — Abu Dhabi branch',type:'Transit',items:5,value:'45,000 AED',resp:'Logistics',status:'In transit'},
	];
	var tb=document.querySelector('#vw_table tbody');
	locs.forEach(function(l){
		var cls=l.status==='Active'?'success':(l.status==='In transit'?'info':'default');
		tb.innerHTML+='<tr><td><code>'+l.code+'</code></td><td><strong>'+l.name+'</strong></td><td><span class="label label-info">'+l.type+'</span></td><td>'+l.items+'</td><td>'+l.value+'</td><td>'+l.resp+'</td><td><span class="label label-'+cls+'">'+l.status+'</span></td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a></td></tr>';
	});
})();
</script>
<?php
erp_section_card('Virtual Warehouse', ob_get_clean(), array('icon' => 'fa-building-o'));
