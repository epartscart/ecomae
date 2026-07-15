<?php
/**
 * SLA Agreement Management — define, track and enforce service level agreements with clients.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_sla.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
epc_erp_pm_inline_assets();

epc_sla_ensure_schema($db_link);
$slaCompanyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$slas = epc_sla_list($db_link, $slaCompanyId);

erp_page_header(
	'<i class="fa fa-handshake-o"></i> SLA Agreements',
	'Create and manage Service Level Agreements — response times, uptime guarantees, penalty clauses, and compliance tracking.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'SLA Agreements'),
	),
	array(array('label' => 'New SLA', 'id' => 'sla_add', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-file-text"></i> Active SLA agreements</h4>
	<form id="sla_new_form" method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" style="display:none;margin-bottom:16px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="sla_save">
		<div class="pm-fields">
			<div class="pm-field"><label>SLA code</label><input type="text" name="sla_code" class="form-control input-sm" required placeholder="e.g. SLA-001"></div>
			<div class="pm-field"><label>Client name</label><input type="text" name="client_name" class="form-control input-sm" required></div>
			<div class="pm-field"><label>Service type</label><input type="text" name="service_type" class="form-control input-sm" placeholder="e.g. ERP Support"></div>
			<div class="pm-field"><label>Response time (hours)</label><input type="number" step="any" name="response_hours" class="form-control input-sm" value="4"></div>
			<div class="pm-field"><label>Resolution time (hours)</label><input type="number" step="any" name="resolution_hours" class="form-control input-sm" value="24"></div>
			<div class="pm-field"><label>Uptime guarantee %</label><input type="number" step="0.1" name="uptime_pct" class="form-control input-sm" value="99.5"></div>
			<div class="pm-field"><label>Penalty type</label>
				<select name="penalty_type" class="form-control input-sm">
					<option value="credit_note">Credit note per breach</option>
					<option value="discount">Discount on next invoice</option>
					<option value="extension">Contract extension</option>
					<option value="none">None</option>
				</select>
			</div>
			<div class="pm-field"><label>Penalty amount per breach</label><input type="number" step="any" name="penalty_amount" class="form-control input-sm" value="500"></div>
			<div class="pm-field"><label>Start date</label><input type="date" name="start_date" class="form-control input-sm"></div>
			<div class="pm-field"><label>End date</label><input type="date" name="end_date" class="form-control input-sm"></div>
			<div class="pm-field"><label>Status</label>
				<select name="status" class="form-control input-sm">
					<option value="active">Active</option>
					<option value="expiring">Expiring</option>
					<option value="expired">Expired</option>
					<option value="suspended">Suspended</option>
				</select>
			</div>
			<div class="pm-field" style="flex:2;min-width:260px;"><label>Notes</label><textarea name="notes" class="form-control input-sm" rows="2"></textarea></div>
		</div>
		<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save SLA</button>
	</form>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="sla_table">
		<thead><tr><th>SLA #</th><th>Client</th><th>Service</th><th>Response</th><th>Resolution</th><th>Uptime %</th><th>Start</th><th>End</th><th>Status</th></tr></thead>
		<tbody>
		<?php if (empty($slas)): ?>
			<tr><td colspan="9" style="text-align:center;color:#999">No SLA agreements yet</td></tr>
		<?php else: foreach ($slas as $s): ?>
			<?php
			$scls = array('active' => 'success', 'expiring' => 'warning', 'expired' => 'default', 'suspended' => 'danger');
			?>
			<tr>
				<td><code><?php echo epc_erp_h($s['sla_code']); ?></code></td>
				<td><strong><?php echo epc_erp_h($s['client_name']); ?></strong></td>
				<td><?php echo epc_erp_h($s['service_type']); ?></td>
				<td><?php echo number_format((float) $s['response_hours'], 1); ?>h</td>
				<td><?php echo number_format((float) $s['resolution_hours'], 1); ?>h</td>
				<td><?php echo number_format((float) $s['uptime_pct'], 2); ?>%</td>
				<td><?php echo $s['start_date'] ? epc_erp_h($s['start_date']) : '—'; ?></td>
				<td><?php echo $s['end_date'] ? epc_erp_h($s['end_date']) : '—'; ?></td>
				<td><span class="label label-<?php echo $scls[$s['status']] ?? 'default'; ?>"><?php echo epc_erp_h(ucfirst($s['status'])); ?></span></td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-bar-chart"></i> SLA compliance dashboard</h4>
	<!-- Illustrative stat cards — no real aggregate function exists in the backend for these metrics -->
	<div class="row">
		<div class="col-md-3"><div class="panel panel-success"><div class="panel-body text-center"><h3 style="margin:0;color:#16a34a;"><?php echo count(array_filter($slas, function($r){ return $r['status'] === 'active'; })); ?></h3><p class="text-muted small">Active agreements</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-warning"><div class="panel-body text-center"><h3 style="margin:0;color:#d97706;"><?php echo count(array_filter($slas, function($r){ return $r['status'] === 'expiring'; })); ?></h3><p class="text-muted small">Expiring soon</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-danger"><div class="panel-body text-center"><h3 style="margin:0;color:#dc2626;"><?php echo count(array_filter($slas, function($r){ return $r['status'] === 'expired'; })); ?></h3><p class="text-muted small">Expired</p></div></div></div>
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;"><?php echo count($slas); ?></h3><p class="text-muted small">Total agreements</p></div></div></div>
	</div>
</div>
<script>
(function(){
	var endpoint = <?php echo json_encode($erpAjaxUrl); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	var addBtn = document.getElementById('sla_add');
	if (addBtn) {
		addBtn.addEventListener('click', function () {
			var f = document.getElementById('sla_new_form');
			if (f) { f.style.display = f.style.display === 'none' ? 'block' : 'none'; }
		});
	}
	var form = document.getElementById('sla_new_form');
	if (form) {
		form.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(form);
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function () { location.reload(); });
		});
	}
})();
</script>
<?php
erp_section_card('SLA Agreements', ob_get_clean(), array('icon' => 'fa-handshake-o'));
