<?php
/**
 * Fix / Unfix Purchase Structure — jewellery business tracks fixed and unfixed gold purchases.
 * Fixed = locked-in gold rate at purchase. Unfixed = floating rate until settlement.
 * Reports margin on both structures.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-lock"></i> Fix / Unfix Tracking',
	'Track fixed and unfixed gold purchase structures — fixed rate (locked at purchase) vs. unfixed (floating until settlement). Report margins on both.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Fix / Unfix'),
	),
	array(array('label' => 'New purchase', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-pie-chart"></i> Portfolio summary</h4>
	<div class="row">
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #16a34a;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">Fixed purchases</h5><h3 style="margin:0;color:#16a34a;">1,240g</h3><small class="text-muted">Avg rate: 289.50 AED/g</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #dc2626;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">Unfixed purchases</h5><h3 style="margin:0;color:#dc2626;">680g</h3><small class="text-muted">Current rate: 295.50 AED/g</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #2563eb;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">Unrealised gain (unfix)</h5><h3 style="margin:0;color:#2563eb;">4,080 AED</h3><small class="text-muted">+6.00 AED/g since purchase</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #7c3aed;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">Fix margin</h5><h3 style="margin:0;color:#7c3aed;">14.2%</h3><small class="text-muted">vs. 11.8% on unfix</small></div></div></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-lock"></i> Fixed purchases (rate locked)</h4>
	<p class="text-muted">Rate was locked at purchase time — no market fluctuation risk. Margin is calculated from fixed cost.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="fu_fixed">
		<thead><tr><th>PO #</th><th>Supplier</th><th>Date</th><th>Weight</th><th>Fixed rate</th><th>Total cost</th><th>Current value</th><th>Margin</th><th>Status</th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-unlock"></i> Unfixed purchases (floating rate)</h4>
	<p class="text-muted">Rate NOT locked — cost calculated at today's rate until settlement (fixing). Exposes to market risk/opportunity.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="fu_unfix">
		<thead><tr><th>PO #</th><th>Supplier</th><th>Date</th><th>Weight</th><th>Rate at purchase</th><th>Today's rate</th><th>Gain/Loss</th><th>Settlement due</th><th></th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Fix/Unfix settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Default purchase type</label>
			<select class="form-control input-sm"><option>Fixed (lock rate at PO)</option><option>Unfixed (settle later)</option><option>Ask each time</option></select>
		</div>
		<div class="pm-field"><label>Max unfix settlement days</label><input type="number" class="form-control input-sm" value="30"></div>
		<div class="pm-field"><label>Auto-fix alert threshold</label>
			<select class="form-control input-sm"><option value="0">No auto-alert</option><option value="3">3% adverse move</option><option value="5">5% adverse move</option></select>
		</div>
		<div class="pm-field"><label>Report currency</label>
			<select class="form-control input-sm"><option>Tenant currency</option><option>USD equivalent</option></select>
		</div>
	</div>
</div>
<script>
(function(){
	var fixed=[
		{po:'PO-2026-0045',sup:'Al Romaizan',date:'2026-06-15',wt:'250g',rate:'289.50',cost:'72,375 AED',curVal:'73,875 AED',margin:'14.2%',status:'In stock'},
		{po:'PO-2026-0042',sup:'Malabar Gold',date:'2026-06-10',wt:'400g',rate:'288.00',cost:'115,200 AED',curVal:'118,200 AED',margin:'15.1%',status:'In stock'},
		{po:'PO-2026-0038',sup:'Kalyan',date:'2026-06-05',wt:'320g',rate:'291.00',cost:'93,120 AED',curVal:'94,560 AED',margin:'13.8%',status:'Partial sold'},
		{po:'PO-2026-0035',sup:'Joyalukkas',date:'2026-06-01',wt:'270g',rate:'290.20',cost:'78,354 AED',curVal:'79,785 AED',margin:'14.5%',status:'In stock'},
	];
	var tb=document.querySelector('#fu_fixed tbody');
	fixed.forEach(function(f){
		var cls=f.status==='In stock'?'success':'warning';
		tb.innerHTML+='<tr><td><code>'+f.po+'</code></td><td>'+f.sup+'</td><td>'+f.date+'</td><td>'+f.wt+'</td><td>'+f.rate+' AED/g</td><td>'+f.cost+'</td><td>'+f.curVal+'</td><td><strong class="text-success">'+f.margin+'</strong></td><td><span class="label label-'+cls+'">'+f.status+'</span></td></tr>';
	});
	var unfix=[
		{po:'PO-2026-0047',sup:'Dubai Gold Ref.',date:'2026-06-18',wt:'180g',rateAt:'289.50',today:'295.50',gl:'+1,080 AED',settle:'2026-07-18'},
		{po:'PO-2026-0044',sup:'Al Romaizan',date:'2026-06-12',wt:'300g',rateAt:'287.00',today:'295.50',gl:'+2,550 AED',settle:'2026-07-12'},
		{po:'PO-2026-0040',sup:'Malabar Gold',date:'2026-06-08',wt:'200g',rateAt:'290.00',today:'295.50',gl:'+1,100 AED',settle:'2026-07-08'},
	];
	var tb2=document.querySelector('#fu_unfix tbody');
	unfix.forEach(function(u){
		var cls=u.gl.charAt(0)==='+'?'text-success':'text-danger';
		tb2.innerHTML+='<tr><td><code>'+u.po+'</code></td><td>'+u.sup+'</td><td>'+u.date+'</td><td>'+u.wt+'</td><td>'+u.rateAt+' AED/g</td><td>'+u.today+' AED/g</td><td class="'+cls+'"><strong>'+u.gl+'</strong></td><td>'+u.settle+'</td><td><button class="btn btn-xs btn-success"><i class="fa fa-lock"></i> Fix now</button></td></tr>';
	});
})();
</script>
<?php
erp_section_card('Fix / Unfix Tracking', ob_get_clean(), array('icon' => 'fa-lock'));
