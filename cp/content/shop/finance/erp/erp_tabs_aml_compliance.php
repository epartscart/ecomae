<?php
/**
 * AML Compliance — Anti-Money Laundering compliance module with reporting.
 * KYC checks, suspicious transaction monitoring, CTR filing, risk scoring.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-shield"></i> AML Compliance',
	'Anti-Money Laundering compliance — KYC verification, suspicious transaction monitoring, CTR filing, and risk assessment.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'AML Compliance'),
	),
	array(array('label' => 'New STR', 'url' => '#', 'class' => 'btn-danger', 'icon' => 'fa-exclamation-triangle'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-dashboard"></i> AML dashboard</h4>
	<div class="row">
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #16a34a;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">KYC verified</h5><h3 style="margin:0;color:#16a34a;">94%</h3><small class="text-muted">187 of 199 customers</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #dc2626;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">High-risk customers</h5><h3 style="margin:0;color:#dc2626;">8</h3><small class="text-muted">Enhanced due diligence</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #d97706;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">STRs filed (YTD)</h5><h3 style="margin:0;color:#d97706;">3</h3><small class="text-muted">Suspicious Transaction Reports</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #2563eb;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">CTRs filed (YTD)</h5><h3 style="margin:0;color:#2563eb;">12</h3><small class="text-muted">Cash Transaction Reports</small></div></div></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-exclamation-triangle"></i> Alerts &amp; monitoring</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="aml_alerts">
		<thead><tr><th>Date</th><th>Alert type</th><th>Customer</th><th>Detail</th><th>Risk</th><th>Action</th><th></th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-user-secret"></i> KYC register</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Customer</th><th>ID type</th><th>ID verified</th><th>Risk level</th><th>Last review</th><th>Next review</th><th></th></tr></thead>
		<tbody>
			<tr><td>Ahmed Al Rashid</td><td>Emirates ID</td><td><i class="fa fa-check-circle text-success"></i> Verified</td><td><span class="label label-success">Low</span></td><td>2026-03-15</td><td>2027-03-15</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>John Williams</td><td>Passport</td><td><i class="fa fa-check-circle text-success"></i> Verified</td><td><span class="label label-warning">Medium</span></td><td>2026-05-20</td><td>2026-11-20</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>Unknown Cash Buyer</td><td>—</td><td><i class="fa fa-times-circle text-danger"></i> Pending</td><td><span class="label label-danger">High</span></td><td>—</td><td>Overdue</td><td><a class="btn btn-xs btn-warning"><i class="fa fa-exclamation"></i> Review</a></td></tr>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> AML configuration</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Cash reporting threshold</label><input type="number" class="form-control input-sm" value="55000" id="aml_threshold"></div>
		<div class="pm-field"><label>Structuring detection (split payments)</label>
			<select class="form-control input-sm"><option value="1">Enabled — flag if 3+ cash payments within 24h sum to threshold</option><option value="0">Disabled</option></select>
		</div>
		<div class="pm-field"><label>KYC renewal period</label>
			<select class="form-control input-sm"><option>Annual (low risk)</option><option>6 months (medium risk)</option><option>3 months (high risk)</option></select>
		</div>
		<div class="pm-field"><label>PEP screening</label>
			<select class="form-control input-sm"><option value="1">Enabled — check against sanctions list</option><option value="0">Manual only</option></select>
		</div>
		<div class="pm-field"><label>STR filing authority</label>
			<select class="form-control input-sm"><option>UAE FIU (goAML)</option><option>UK NCA (SAR Online)</option><option>US FinCEN</option><option>Custom authority</option></select>
		</div>
	</div>
</div>
<script>
(function(){
	var alerts=[
		{date:'2026-06-20',type:'Cash threshold',cust:'Walk-in customer',detail:'Cash purchase 52,000 AED (near threshold 55,000)',risk:'Medium',action:'Review'},
		{date:'2026-06-18',type:'Structuring',cust:'Sara Imports LLC',detail:'3 payments: 18K + 17K + 19K = 54K in 24h',risk:'High',action:'Escalate'},
		{date:'2026-06-15',type:'PEP match',cust:'Mohammad H.',detail:'Name matches sanctions watchlist (partial)',risk:'High',action:'Verify identity'},
		{date:'2026-06-10',type:'Unusual pattern',cust:'Gold Traders Int.',detail:'5x normal purchase volume this week',risk:'Medium',action:'Monitor'},
	];
	var tb=document.querySelector('#aml_alerts tbody');
	alerts.forEach(function(a){
		var cls=a.risk==='High'?'danger':(a.risk==='Medium'?'warning':'info');
		tb.innerHTML+='<tr><td>'+a.date+'</td><td><span class="label label-'+cls+'">'+a.type+'</span></td><td>'+a.cust+'</td><td><small>'+a.detail+'</small></td><td><span class="label label-'+cls+'">'+a.risk+'</span></td><td><button class="btn btn-xs btn-'+cls+'">'+a.action+'</button></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>';
	});
})();
</script>
<?php
erp_section_card('AML Compliance', ob_get_clean(), array('icon' => 'fa-shield'));
