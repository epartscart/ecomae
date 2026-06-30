<?php
/**
 * SLA Agreement Management — define, track and enforce service level agreements with clients.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-handshake-o"></i> SLA Agreements',
	'Create and manage Service Level Agreements — response times, uptime guarantees, penalty clauses, and compliance tracking.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'SLA Agreements'),
	),
	array(array('label' => 'New SLA', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-file-text"></i> Active SLA agreements</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="sla_table">
		<thead><tr><th>SLA #</th><th>Client</th><th>Service</th><th>Response time</th><th>Uptime %</th><th>Start</th><th>End</th><th>Status</th><th>Compliance</th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-pencil-square-o"></i> SLA template builder</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Template name</label><input type="text" class="form-control input-sm" placeholder="e.g. Gold Support Package"></div>
		<div class="pm-field"><label>Response time (hours)</label><input type="number" class="form-control input-sm" value="4"></div>
		<div class="pm-field"><label>Resolution time (hours)</label><input type="number" class="form-control input-sm" value="24"></div>
		<div class="pm-field"><label>Uptime guarantee %</label><input type="number" class="form-control input-sm" value="99.5" step="0.1"></div>
		<div class="pm-field"><label>Penalty clause</label>
			<select class="form-control input-sm"><option>Credit note per breach</option><option>Discount on next invoice</option><option>Contract extension</option><option>None</option></select>
		</div>
		<div class="pm-field"><label>Penalty amount per breach</label><input type="number" class="form-control input-sm" value="500"></div>
	</div>
	<button class="btn btn-primary btn-sm" style="margin-top:8px;"><i class="fa fa-save"></i> Save template</button>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-bar-chart"></i> SLA compliance dashboard</h4>
	<div class="row">
		<div class="col-md-3"><div class="panel panel-success"><div class="panel-body text-center"><h3 style="margin:0;color:#16a34a;">98.7%</h3><p class="text-muted small">Avg uptime (30d)</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-info"><div class="panel-body text-center"><h3 style="margin:0;color:#2563eb;">2.4h</h3><p class="text-muted small">Avg response time</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-warning"><div class="panel-body text-center"><h3 style="margin:0;color:#d97706;">3</h3><p class="text-muted small">Breaches this month</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-danger"><div class="panel-body text-center"><h3 style="margin:0;color:#dc2626;">1,500</h3><p class="text-muted small">Penalty credits issued</p></div></div></div>
	</div>
</div>
<script>
(function(){
	var slas=[
		{id:'SLA-001',client:'Al Fardan Group',service:'ERP Support',resp:'4h',up:'99.5',start:'2026-01-01',end:'2026-12-31',status:'Active',comp:'98.2%'},
		{id:'SLA-002',client:'Gold House Trading',service:'Platform Hosting',resp:'1h',up:'99.9',start:'2026-03-15',end:'2027-03-14',status:'Active',comp:'99.8%'},
		{id:'SLA-003',client:'Desert Gems LLC',service:'Full Support',resp:'2h',up:'99.0',start:'2025-06-01',end:'2026-05-31',status:'Expiring',comp:'97.5%'},
	];
	var tb=document.querySelector('#sla_table tbody');
	slas.forEach(function(s){
		var cls=s.status==='Active'?'success':(s.status==='Expiring'?'warning':'default');
		tb.innerHTML+='<tr><td><code>'+s.id+'</code></td><td>'+s.client+'</td><td>'+s.service+'</td><td>'+s.resp+'</td><td>'+s.up+'%</td><td>'+s.start+'</td><td>'+s.end+'</td><td><span class="label label-'+cls+'">'+s.status+'</span></td><td><strong>'+s.comp+'</strong></td></tr>';
	});
})();
</script>
<?php
erp_section_card('SLA Agreements', ob_get_clean(), array('icon' => 'fa-handshake-o'));
