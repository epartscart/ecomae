<?php
/**
 * Automated Report Scheduling — daily/weekly/monthly report generation and email delivery.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-clock-o"></i> Report Scheduler',
	'Automate report generation — daily, weekly, or monthly reports delivered to specific email addresses as PDF/Excel.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Report Scheduler'),
	),
	array(array('label' => 'New schedule', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-calendar"></i> Scheduled reports</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="rs_table">
		<thead><tr><th>Report</th><th>Frequency</th><th>Recipients</th><th>Format</th><th>Last sent</th><th>Next run</th><th>Status</th><th></th></tr></thead>
		<tbody></tbody>
	</table>
	<button class="btn btn-default btn-sm" id="rs_add"><i class="fa fa-plus"></i> Add scheduled report</button>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-plus-circle"></i> Create new schedule</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Report type</label>
			<select class="form-control input-sm">
				<option>Trial Balance</option><option>Profit &amp; Loss</option><option>Balance Sheet</option>
				<option>Aging Report</option><option>VAT Return Summary</option><option>Sales Analysis</option>
				<option>Purchase Analysis</option><option>Inventory Valuation</option><option>Cash Flow</option>
				<option>Customer Statement</option><option>Stock Movement</option><option>Revenue by Product</option>
			</select>
		</div>
		<div class="pm-field"><label>Frequency</label>
			<select class="form-control input-sm"><option>Daily (end of day)</option><option>Weekly (Monday)</option><option>Weekly (Friday)</option><option>Monthly (1st)</option><option>Monthly (last day)</option><option>Quarterly</option></select>
		</div>
		<div class="pm-field"><label>Time</label><input type="time" class="form-control input-sm" value="08:00"></div>
		<div class="pm-field"><label>Recipients (email)</label><input type="text" class="form-control input-sm" placeholder="finance@company.com, ceo@company.com"></div>
		<div class="pm-field"><label>Format</label>
			<select class="form-control input-sm"><option>PDF</option><option>Excel (XLSX)</option><option>CSV</option><option>PDF + Excel</option></select>
		</div>
		<div class="pm-field"><label>Include comparison</label>
			<select class="form-control input-sm"><option value="0">No</option><option value="1">vs. previous period</option><option value="2">vs. same period last year</option></select>
		</div>
	</div>
	<button class="btn btn-primary btn-sm" style="margin-top:8px;"><i class="fa fa-save"></i> Save schedule</button>
	<button class="btn btn-default btn-sm" style="margin-top:8px;"><i class="fa fa-play"></i> Run now (test)</button>
</div>
<script>
(function(){
	var schedules=[
		{report:'Daily Sales Summary',freq:'Daily',to:'ceo@company.com',fmt:'PDF',last:'2026-06-20 08:00',next:'2026-06-21 08:00',status:'Active'},
		{report:'Weekly Aging Report',freq:'Weekly (Mon)',to:'finance@company.com',fmt:'Excel',last:'2026-06-16 08:00',next:'2026-06-23 08:00',status:'Active'},
		{report:'Monthly P&L',freq:'Monthly (1st)',to:'ceo@company.com, cfo@company.com',fmt:'PDF + Excel',last:'2026-06-01 08:00',next:'2026-07-01 08:00',status:'Active'},
		{report:'Inventory Valuation',freq:'Weekly (Fri)',to:'warehouse@company.com',fmt:'Excel',last:'2026-06-20 17:00',next:'2026-06-27 17:00',status:'Active'},
		{report:'VAT Return Prep',freq:'Monthly (last)',to:'tax@company.com',fmt:'PDF',last:'2026-05-31 08:00',next:'2026-06-30 08:00',status:'Active'},
	];
	var tb=document.querySelector('#rs_table tbody');
	schedules.forEach(function(s){
		tb.innerHTML+='<tr><td><strong>'+s.report+'</strong></td><td>'+s.freq+'</td><td><small>'+s.to+'</small></td><td><span class="label label-default">'+s.fmt+'</span></td><td>'+s.last+'</td><td>'+s.next+'</td><td><span class="label label-success">'+s.status+'</span></td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a> <a class="btn btn-xs btn-danger"><i class="fa fa-trash"></i></a></td></tr>';
	});
})();
</script>
<?php
erp_section_card('Report Scheduler', ob_get_clean(), array('icon' => 'fa-clock-o'));
