<?php
/**
 * CRM Integration — connect with Salesforce, HubSpot, Zoho CRM.
 * Sync contacts, deals, activities between ERP and external CRM.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-cloud"></i> CRM Integration',
	'Connect with Salesforce, HubSpot, or Zoho CRM — sync contacts, deals, quotes, and activities bidirectionally.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'CRM Integration'),
	),
	array(array('label' => 'Add CRM', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plug'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-plug"></i> CRM platforms</h4>
	<div class="row">
		<div class="col-md-4">
			<div class="panel panel-default" style="border-top:3px solid #00a1e0;">
				<div class="panel-body text-center">
					<h4 style="color:#00a1e0;"><i class="fa fa-cloud"></i> Salesforce</h4>
					<p class="text-muted small">OAuth 2.0, REST API, real-time webhooks</p>
					<button class="btn btn-sm btn-default"><i class="fa fa-plug"></i> Connect</button>
				</div>
			</div>
		</div>
		<div class="col-md-4">
			<div class="panel panel-default" style="border-top:3px solid #ff7a59;">
				<div class="panel-body text-center">
					<h4 style="color:#ff7a59;"><i class="fa fa-hubspot"></i> HubSpot</h4>
					<p class="text-muted small">Contact sync, deal pipeline, activity log</p>
					<button class="btn btn-sm btn-default"><i class="fa fa-plug"></i> Connect</button>
				</div>
			</div>
		</div>
		<div class="col-md-4">
			<div class="panel panel-default" style="border-top:3px solid #e42527;">
				<div class="panel-body text-center">
					<h4 style="color:#e42527;"><i class="fa fa-address-book"></i> Zoho CRM</h4>
					<p class="text-muted small">Module mapping, workflow triggers</p>
					<button class="btn btn-sm btn-default"><i class="fa fa-plug"></i> Connect</button>
				</div>
			</div>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-exchange"></i> Field mapping</h4>
	<p class="text-muted">Map CRM fields to ERP customer/supplier fields. Unmapped fields are ignored during sync.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>CRM field</th><th>→</th><th>ERP field</th><th>Direction</th><th></th></tr></thead>
		<tbody>
			<tr><td>Account Name</td><td>→</td><td>Customer Name</td><td>Bidirectional</td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a></td></tr>
			<tr><td>Email</td><td>→</td><td>Contact Email</td><td>Bidirectional</td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a></td></tr>
			<tr><td>Phone</td><td>→</td><td>Phone</td><td>Bidirectional</td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a></td></tr>
			<tr><td>Deal Amount</td><td>→</td><td>Quotation Total</td><td>CRM → ERP</td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a></td></tr>
			<tr><td>Deal Stage</td><td>→</td><td>Opportunity Status</td><td>CRM → ERP</td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a></td></tr>
			<tr><td>Invoice Status</td><td>←</td><td>Invoice Paid/Outstanding</td><td>ERP → CRM</td><td><a class="btn btn-xs btn-default"><i class="fa fa-pencil"></i></a></td></tr>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Sync configuration</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>CRM platform</label>
			<select class="form-control input-sm"><option>Salesforce</option><option>HubSpot</option><option>Zoho CRM</option><option>Microsoft Dynamics 365</option><option>Pipedrive</option></select>
		</div>
		<div class="pm-field"><label>Sync frequency</label>
			<select class="form-control input-sm"><option>Real-time (webhook)</option><option>Every 5 minutes</option><option>Hourly</option><option>Daily</option></select>
		</div>
		<div class="pm-field"><label>Conflict resolution</label>
			<select class="form-control input-sm"><option>CRM wins</option><option>ERP wins</option><option>Most recent wins</option><option>Manual review</option></select>
		</div>
		<div class="pm-field"><label>Auto-create customers</label>
			<select class="form-control input-sm"><option value="1">Yes — create ERP customer from CRM contact</option><option value="0">No — match existing only</option></select>
		</div>
	</div>
</div>
<?php
erp_section_card('CRM Integration', ob_get_clean(), array('icon' => 'fa-cloud'));
