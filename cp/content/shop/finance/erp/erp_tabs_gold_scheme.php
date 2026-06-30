<?php
/**
 * Gold Scheme Module — savings schemes in value or grams, maturity periods, bonus incentives.
 * Worldwide practice: Dubai Gold Souk, India jewellers, UK savings clubs.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-diamond"></i> Gold Scheme',
	'Customer gold savings plans — by value or gram weight, with maturity bonuses (free month, free making charges). Worldwide practice.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Gold Scheme'),
	),
	array(array('label' => 'New scheme', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-calendar-check-o"></i> Scheme templates</h4>
	<p class="text-muted">Define savings plan templates — customers enrol and pay monthly instalments (value or gram equivalent). On maturity, they get bonus benefits.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="gs_templates">
		<thead><tr><th>Scheme</th><th>Type</th><th>Duration</th><th>Monthly</th><th>Maturity bonus</th><th>Active enrolments</th><th></th></tr></thead>
		<tbody></tbody>
	</table>
	<button class="btn btn-default btn-sm" id="gs_add_tpl"><i class="fa fa-plus"></i> Add scheme template</button>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-user-plus"></i> Customer enrolments</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="gs_enrol">
		<thead><tr><th>Customer</th><th>Scheme</th><th>Start</th><th>Maturity</th><th>Paid</th><th>Remaining</th><th>Status</th><th></th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Scheme settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Default scheme type</label><select class="form-control input-sm"><option>Value (currency)</option><option>Weight (grams)</option></select></div>
		<div class="pm-field"><label>Grace period (days)</label><input type="number" class="form-control input-sm" value="7"></div>
		<div class="pm-field"><label>Penalty on missed instalment</label><select class="form-control input-sm"><option value="0">None</option><option value="1">Forfeit bonus</option><option value="2">Late fee</option></select></div>
		<div class="pm-field"><label>Auto-reminder (days before due)</label><input type="number" class="form-control input-sm" value="3"></div>
		<div class="pm-field"><label>Partial redemption allowed</label><select class="form-control input-sm"><option value="1">Yes</option><option value="0">No — full maturity only</option></select></div>
	</div>
</div>
<script>
(function(){
	var tpls=[
		{name:'Gold Saver 6M',type:'Value',dur:'6 months',monthly:'500 AED/month',bonus:'1 month free (500 AED credit)',count:34},
		{name:'Gold Saver 12M',type:'Value',dur:'12 months',monthly:'1,000 AED/month',bonus:'1 month free + free making charges',count:18},
		{name:'Gram Accumulator',type:'Grams',dur:'12 months',monthly:'2g/month',bonus:'2g bonus (free month equivalent)',count:12},
		{name:'Bridal Plan',type:'Value',dur:'9 months',monthly:'2,000 AED/month',bonus:'Free making charges on bridal set',count:8},
	];
	var tb=document.querySelector('#gs_templates tbody');
	tpls.forEach(function(t){
		tb.innerHTML+='<tr><td><strong>'+t.name+'</strong></td><td><span class="label label-info">'+t.type+'</span></td><td>'+t.dur+'</td><td>'+t.monthly+'</td><td>'+t.bonus+'</td><td><span class="badge">'+t.count+'</span></td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a></td></tr>';
	});
	var enrols=[
		{cust:'Fatima Al Maktoum',scheme:'Gold Saver 12M',start:'2026-01-15',mat:'2027-01-15',paid:'6/12',rem:'6,000 AED',status:'Active'},
		{cust:'Sara Khan',scheme:'Gram Accumulator',start:'2025-12-01',mat:'2026-12-01',paid:'7/12',rem:'10g',status:'Active'},
		{cust:'Ahmed Hassan',scheme:'Bridal Plan',start:'2026-03-01',mat:'2026-12-01',paid:'4/9',rem:'10,000 AED',status:'Active'},
		{cust:'Maryam Ali',scheme:'Gold Saver 6M',start:'2026-01-01',mat:'2026-07-01',paid:'6/6',rem:'0',status:'Matured'},
	];
	var tb2=document.querySelector('#gs_enrol tbody');
	enrols.forEach(function(e){
		var cls=e.status==='Active'?'primary':(e.status==='Matured'?'success':'default');
		tb2.innerHTML+='<tr><td>'+e.cust+'</td><td>'+e.scheme+'</td><td>'+e.start+'</td><td>'+e.mat+'</td><td>'+e.paid+'</td><td>'+e.rem+'</td><td><span class="label label-'+cls+'">'+e.status+'</span></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>';
	});
})();
</script>
<?php
erp_section_card('Gold Scheme', ob_get_clean(), array('icon' => 'fa-diamond'));
