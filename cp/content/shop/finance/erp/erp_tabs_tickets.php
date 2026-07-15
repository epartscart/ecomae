<?php
/**
 * Ticket / Support System — helpdesk for clients to raise issues, track resolution, escalate.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_tickets.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
epc_erp_pm_inline_assets();

epc_tickets_ensure_schema($db_link);
$tkCompanyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$tkStatusFilter = isset($_GET['tk_status']) ? (string) $_GET['tk_status'] : '';
$tickets = epc_tickets_list($db_link, $tkCompanyId, $tkStatusFilter);
$tkStats = epc_tickets_stats($db_link, $tkCompanyId);

erp_page_header(
	'<i class="fa fa-ticket"></i> Support Tickets',
	'Client helpdesk — raise tickets, track issues, assign to staff, escalate, and measure resolution times.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Support Tickets'),
	),
	array(array('label' => 'New ticket', 'id' => 'tk_new_toggle', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<div class="row" style="margin-bottom:16px;">
		<div class="col-md-2"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;"><?php echo (int) $tkStats['open']; ?></h3><p class="text-muted small">Total open</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-danger"><div class="panel-body text-center"><h3 style="margin:0;color:#dc2626;"><?php echo (int) $tkStats['critical']; ?></h3><p class="text-muted small">Critical</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-warning"><div class="panel-body text-center"><h3 style="margin:0;color:#d97706;"><?php echo (int) $tkStats['high']; ?></h3><p class="text-muted small">High priority</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-info"><div class="panel-body text-center"><h3 style="margin:0;color:#2563eb;"><?php echo (int) $tkStats['in_progress']; ?></h3><p class="text-muted small">In progress</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-success"><div class="panel-body text-center"><h3 style="margin:0;color:#16a34a;"><?php echo (int) $tkStats['resolved_30d']; ?></h3><p class="text-muted small">Resolved (30d)</p></div></div></div>
		<div class="col-md-2"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;"><?php echo epc_erp_h((string) $tkStats['avg_resolution_hours']); ?>h</h3><p class="text-muted small">Avg resolution</p></div></div></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-plus"></i> New ticket</h4>
	<form id="tk_new_form" method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" style="display:none;margin-bottom:16px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="ticket_save">
		<div class="pm-fields">
			<div class="pm-field"><label>Subject</label><input type="text" name="subject" class="form-control input-sm" required></div>
			<div class="pm-field"><label>Client name</label><input type="text" name="client_name" class="form-control input-sm"></div>
			<div class="pm-field"><label>Category</label><select name="category" class="form-control input-sm"><option value="general">General</option><option value="billing">Billing</option><option value="technical">Technical</option><option value="feature_request">Feature request</option></select></div>
			<div class="pm-field"><label>Priority</label><select name="priority" class="form-control input-sm"><option value="low">Low</option><option value="medium" selected>Medium</option><option value="high">High</option><option value="critical">Critical</option></select></div>
			<div class="pm-field" style="flex:2;min-width:260px;"><label>Description</label><textarea name="description" class="form-control input-sm" rows="2"></textarea></div>
		</div>
		<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Create ticket</button>
	</form>
	<h4><i class="fa fa-list"></i> Ticket queue</h4>
	<div class="pm-fields" style="margin-bottom:8px;">
		<form method="GET">
			<div class="pm-field"><label>Status</label><select name="tk_status" class="form-control input-sm" onchange="this.form.submit()">
				<option value="">All</option>
				<option value="open"<?php echo $tkStatusFilter === 'open' ? ' selected' : ''; ?>>Open</option>
				<option value="in_progress"<?php echo $tkStatusFilter === 'in_progress' ? ' selected' : ''; ?>>In progress</option>
				<option value="waiting"<?php echo $tkStatusFilter === 'waiting' ? ' selected' : ''; ?>>Waiting</option>
				<option value="resolved"<?php echo $tkStatusFilter === 'resolved' ? ' selected' : ''; ?>>Resolved</option>
				<option value="closed"<?php echo $tkStatusFilter === 'closed' ? ' selected' : ''; ?>>Closed</option>
			</select></div>
		</form>
	</div>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="tk_table">
		<thead><tr><th>#</th><th>Subject</th><th>Client</th><th>Priority</th><th>Status</th><th>Assigned</th><th>Created</th></tr></thead>
		<tbody id="tk_tbody">
		<?php if (empty($tickets)): ?>
			<tr><td colspan="7" style="text-align:center;color:#999">No tickets yet</td></tr>
		<?php else: foreach ($tickets as $t): ?>
			<?php
			$pcls = array('critical' => 'danger', 'high' => 'warning', 'medium' => 'info', 'low' => 'default');
			$scls = array('open' => 'primary', 'in_progress' => 'info', 'waiting' => 'warning', 'resolved' => 'success', 'closed' => 'default');
			?>
			<tr>
				<td><code><?php echo epc_erp_h($t['ticket_no']); ?></code></td>
				<td><strong><?php echo epc_erp_h($t['subject']); ?></strong></td>
				<td><?php echo epc_erp_h($t['client_name']); ?></td>
				<td><span class="label label-<?php echo $pcls[$t['priority']] ?? 'default'; ?>"><?php echo epc_erp_h($t['priority']); ?></span></td>
				<td>
					<select class="form-control input-sm tk-status" data-id="<?php echo (int) $t['id']; ?>" style="display:inline-block;width:auto;">
						<?php foreach (array('open', 'in_progress', 'waiting', 'resolved', 'closed') as $s): ?>
							<option value="<?php echo $s; ?>"<?php echo $t['status'] === $s ? ' selected' : ''; ?>><?php echo str_replace('_', ' ', $s); ?></option>
						<?php endforeach; ?>
					</select>
				</td>
				<td><?php echo epc_erp_h($t['assigned_name'] ?: 'Unassigned'); ?></td>
				<td><?php echo $t['time_created'] ? date('Y-m-d H:i', (int) $t['time_created']) : ''; ?></td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
</div>
<script>
(function(){
	var endpoint = <?php echo json_encode($erpAjaxUrl); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	var toggleBtn = document.getElementById('tk_new_toggle');
	if (toggleBtn) {
		toggleBtn.addEventListener('click', function () {
			var f = document.getElementById('tk_new_form');
			if (f) { f.style.display = f.style.display === 'none' ? 'block' : 'none'; }
		});
	}
	document.querySelectorAll('.tk-status').forEach(function (sel) {
		sel.addEventListener('change', function () {
			var fd = new FormData();
			fd.append('action', 'ticket_update');
			fd.append('csrf_guard_key', csrf);
			fd.append('id', sel.getAttribute('data-id'));
			fd.append('status', sel.value);
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function () { setTimeout(function () { location.reload(); }, 400); });
		});
	});
})();
</script>
<?php
erp_section_card('Support Tickets', ob_get_clean(), array('icon' => 'fa-ticket'));
