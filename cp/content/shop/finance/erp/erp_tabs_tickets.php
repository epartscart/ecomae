<?php
/**
 * Ticket / Support System — helpdesk for clients to raise issues, track resolution, escalate.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-ticket"></i> Support Tickets',
	'Client helpdesk — raise tickets, track issues, assign to staff, escalate, and measure resolution times.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Support Tickets'),
	),
	array(array('label' => 'New ticket', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<div class="row" style="margin-bottom:16px;">
		<div class="col-md-2"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;">47</h3><p class="text-muted small">Total open</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-danger"><div class="panel-body text-center"><h3 style="margin:0;color:#dc2626;">5</h3><p class="text-muted small">Critical</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-warning"><div class="panel-body text-center"><h3 style="margin:0;color:#d97706;">12</h3><p class="text-muted small">High priority</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-info"><div class="panel-body text-center"><h3 style="margin:0;color:#2563eb;">18</h3><p class="text-muted small">In progress</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-success"><div class="panel-body text-center"><h3 style="margin:0;color:#16a34a;">156</h3><p class="text-muted small">Resolved (30d)</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;">4.2h</h3><p class="text-muted small">Avg resolution</p></div></div></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-list"></i> Ticket queue</h4>
	<div class="pm-fields" style="margin-bottom:8px;">
		<div class="pm-field"><label>Status</label><select id="tk_status" class="form-control input-sm"><option value="">All</option><option value="open">Open</option><option value="progress">In progress</option><option value="resolved">Resolved</option><option value="closed">Closed</option></select></div>
		<div class="pm-field"><label>Priority</label><select id="tk_priority" class="form-control input-sm"><option value="">All</option><option value="critical">Critical</option><option value="high">High</option><option value="medium">Medium</option><option value="low">Low</option></select></div>
		<div class="pm-field"><label>Assigned to</label><select class="form-control input-sm"><option>All staff</option><option>Unassigned</option></select></div>
	</div>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="tk_table">
		<thead><tr><th>#</th><th>Subject</th><th>Client</th><th>Priority</th><th>Status</th><th>Assigned</th><th>Created</th><th>SLA due</th></tr></thead>
		<tbody id="tk_tbody"></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Ticket settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Auto-assign rule</label><select class="form-control input-sm"><option>Round-robin</option><option>Least loaded</option><option>Manual only</option></select></div>
		<div class="pm-field"><label>Escalation after (hours)</label><input type="number" class="form-control input-sm" value="8"></div>
		<div class="pm-field"><label>Client portal access</label><select class="form-control input-sm"><option value="1">Enabled — clients can submit via CP</option><option value="0">Disabled</option></select></div>
		<div class="pm-field"><label>Email notifications</label><select class="form-control input-sm"><option value="1">On every update</option><option value="2">On status change only</option><option value="0">Disabled</option></select></div>
	</div>
</div>
<script>
(function(){
	var tickets=[
		{id:'TK-0234',subj:'Cannot generate VAT return',client:'Indus Jewellers',pri:'critical',status:'open',assign:'Finance Team',created:'2026-06-21 08:15',sla:'2026-06-21 12:15'},
		{id:'TK-0233',subj:'Print designer template not saving',client:'Gold House',pri:'high',status:'progress',assign:'Dev Team',created:'2026-06-20 14:30',sla:'2026-06-21 14:30'},
		{id:'TK-0232',subj:'Add new user role for warehouse',client:'Desert Gems',pri:'medium',status:'progress',assign:'Admin',created:'2026-06-20 10:00',sla:'2026-06-22 10:00'},
		{id:'TK-0231',subj:'Invoice PDF shows wrong logo',client:'Style N Look',pri:'high',status:'open',assign:'Unassigned',created:'2026-06-20 09:45',sla:'2026-06-21 09:45'},
		{id:'TK-0230',subj:'Request: Add custom field to PO',client:'TaxoFinca',pri:'low',status:'open',assign:'Dev Team',created:'2026-06-19 16:00',sla:'2026-06-23 16:00'},
	];
	var tb=document.getElementById('tk_tbody');
	function render(data){
		tb.innerHTML='';
		data.forEach(function(t){
			var pcls={critical:'danger',high:'warning',medium:'info',low:'default'}[t.pri]||'default';
			var scls={open:'primary',progress:'info',resolved:'success',closed:'default'}[t.status]||'default';
			tb.innerHTML+='<tr><td><code>'+t.id+'</code></td><td><strong>'+t.subj+'</strong></td><td>'+t.client+'</td><td><span class="label label-'+pcls+'">'+t.pri+'</span></td><td><span class="label label-'+scls+'">'+t.status+'</span></td><td>'+t.assign+'</td><td>'+t.created+'</td><td>'+t.sla+'</td></tr>';
		});
	}
	render(tickets);
})();
</script>
<?php
erp_section_card('Support Tickets', ob_get_clean(), array('icon' => 'fa-ticket'));
