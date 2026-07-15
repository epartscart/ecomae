<?php
/**
 * AML Compliance — Anti-Money Laundering compliance module with reporting.
 * KYC checks, suspicious transaction monitoring, CTR filing, risk scoring.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_aml_compliance.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
epc_erp_pm_inline_assets();

epc_aml_ensure_schema($db_link);
$amlCompanyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

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
	<p class="text-muted small">
		<!-- NOTE: epc_aml_check_transaction() is a stateless on-demand calculator — it does not persist alert rows
		     to any table. The epc_aml_transactions table exists for logging but no list/query function exists
		     in the backend. The rows below are illustrative examples only. Wire the "Transaction check" form
		     below to run a real check against live rules. -->
		Illustrative example alerts shown below. Use the transaction checker to run a real-time AML check against your active rules.
	</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="aml_alerts">
		<thead><tr><th>Date</th><th>Alert type</th><th>Customer</th><th>Detail</th><th>Risk</th><th>Action</th><th></th></tr></thead>
		<tbody>
			<!-- Illustrative rows — no real alert log table is queryable from this backend file -->
			<tr><td>2026-06-20</td><td><span class="label label-warning">Cash threshold</span></td><td>Walk-in customer</td><td><small>Cash purchase 52,000 AED (near threshold 55,000)</small></td><td><span class="label label-warning">Medium</span></td><td><button class="btn btn-xs btn-warning">Review</button></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-18</td><td><span class="label label-danger">Structuring</span></td><td>Sara Imports LLC</td><td><small>3 payments: 18K + 17K + 19K = 54K in 24h</small></td><td><span class="label label-danger">High</span></td><td><button class="btn btn-xs btn-danger">Escalate</button></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-15</td><td><span class="label label-danger">PEP match</span></td><td>Mohammad H.</td><td><small>Name matches sanctions watchlist (partial)</small></td><td><span class="label label-danger">High</span></td><td><button class="btn btn-xs btn-danger">Verify identity</button></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-10</td><td><span class="label label-warning">Unusual pattern</span></td><td>Gold Traders Int.</td><td><small>5x normal purchase volume this week</small></td><td><span class="label label-warning">Medium</span></td><td><button class="btn btn-xs btn-warning">Monitor</button></td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-search"></i> Real-time transaction check</h4>
	<p class="text-muted">Run an on-demand AML check against your active rules for any customer/amount combination.</p>
	<form id="aml_check_form" style="margin-bottom:12px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="aml_check">
		<div class="pm-fields">
			<div class="pm-field"><label>Customer ID</label><input type="number" name="customer_id" class="form-control input-sm" placeholder="Customer ID" required></div>
			<div class="pm-field"><label>Amount</label><input type="number" step="any" name="amount" class="form-control input-sm" placeholder="0.00" required></div>
			<div class="pm-field"><label>Currency</label>
				<select name="currency" class="form-control input-sm">
					<option value="AED">AED</option>
					<option value="USD">USD</option>
					<option value="EUR">EUR</option>
					<option value="GBP">GBP</option>
				</select>
			</div>
			<div class="pm-field"><label>&nbsp;</label><button type="submit" class="btn btn-warning btn-sm"><i class="fa fa-search"></i> Run AML check</button></div>
		</div>
	</form>
	<div id="aml_check_result" style="display:none;"></div>
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
	var endpoint = <?php echo json_encode($erpAjaxUrl); ?>;
	var checkForm = document.getElementById('aml_check_form');
	var resultBox = document.getElementById('aml_check_result');
	if (checkForm) {
		checkForm.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(checkForm);
			resultBox.style.display = 'none';
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (res) {
					var ok = res && res.ok;
					var data = res && res.data ? res.data : {};
					var flagged = data.flagged;
					var score = data.risk_score !== undefined ? data.risk_score : '—';
					var flags = data.flags && data.flags.length ? data.flags.join('; ') : 'None';
					var cls = flagged ? 'danger' : 'success';
					var label = flagged ? 'FLAGGED' : 'CLEAR';
					resultBox.innerHTML = '<div class="alert alert-' + cls + '"><strong>' + label + '</strong> — Risk score: ' + score + '/100. Flags: ' + flags + '</div>';
					resultBox.style.display = 'block';
				})
				.catch(function () {
					resultBox.innerHTML = '<div class="alert alert-danger">Error running AML check.</div>';
					resultBox.style.display = 'block';
				});
		});
	}
})();
</script>
<?php
erp_section_card('AML Compliance', ob_get_clean(), array('icon' => 'fa-shield'));
