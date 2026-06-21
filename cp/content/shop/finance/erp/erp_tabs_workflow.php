<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';

$filterDept = isset($_GET['dept']) ? (string)$_GET['dept'] : '';
$userDept = epc_erp_staff_primary_department($db_link);
if ($filterDept === '' && $userDept !== '' && !epc_erp_staff_user_is_full_admin($db_link)) {
	$filterDept = $userDept;
}
$tasks = epc_erp_workflow_list($db_link, $filterDept, '', 80);
$deptCfg = epc_erp_departments_config();
$csrfLocal = isset($csrf) ? $csrf : '';
?>

<div class="epc-erp-section">
	<h4><i class="fa fa-random"></i> Department workflow board</h4>
	<p class="text-muted">Cross-department order flow: <strong>Sales</strong> → <strong>Purchase</strong> → <strong>Logistics</strong> → <strong>Finance</strong> → <strong>Accounts</strong>. Marketing &amp; HR support parallel tracks.</p>

	<form method="get" class="form-inline" style="margin-bottom:12px;">
		<input type="hidden" name="tab" value="workflow">
		<input type="hidden" name="from" value="<?php echo epc_erp_h($date_from_str); ?>">
		<input type="hidden" name="to" value="<?php echo epc_erp_h($date_to_str); ?>">
		<label>Department</label>
		<select name="dept" class="form-control input-sm" onchange="this.form.submit()">
			<option value="">All departments</option>
			<?php foreach ($deptCfg as $code => $row): ?>
				<option value="<?php echo epc_erp_h($code); ?>" <?php echo $filterDept === $code ? 'selected' : ''; ?>><?php echo epc_erp_h($row['name']); ?></option>
			<?php endforeach; ?>
		</select>
	</form>

	<form id="epc_erp_form_workflow_create" class="form-inline epc-erp-form-inline" style="margin-bottom:12px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<select name="department_code" class="form-control input-sm">
			<?php foreach ($deptCfg as $code => $row): ?>
				<option value="<?php echo epc_erp_h($code); ?>" <?php echo $filterDept === $code ? 'selected' : ''; ?>><?php echo epc_erp_h($row['name']); ?></option>
			<?php endforeach; ?>
		</select>
		<input type="text" name="title" class="form-control input-sm" placeholder="Task title" required>
		<input type="text" name="workflow_step" class="form-control input-sm" placeholder="Step">
		<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID" value="0">
		<select name="priority" class="form-control input-sm">
			<option value="normal">Normal</option>
			<option value="high">High</option>
			<option value="low">Low</option>
		</select>
		<button type="submit" class="btn btn-sm btn-primary">Add task</button>
	</form>

	<table class="table table-bordered table-condensed">
		<thead>
			<tr>
				<th>Dept</th><th>Step</th><th>Task</th><th>Order</th><th>Assignee</th><th>Priority</th><th>Status</th><th>Due</th><th></th>
			</tr>
		</thead>
		<tbody>
		<?php foreach ($tasks as $t): ?>
			<tr>
				<td><?php echo epc_erp_h(epc_erp_staff_department_name($t['department_code'])); ?></td>
				<td><small><?php echo epc_erp_h($t['workflow_step']); ?></small></td>
				<td>
					<strong><?php echo epc_erp_h($t['title']); ?></strong>
					<?php if ($t['description']): ?><br><small class="text-muted"><?php echo epc_erp_h($t['description']); ?></small><?php endif; ?>
				</td>
				<td><?php echo (int)$t['order_id'] > 0 ? '#' . (int)$t['order_id'] : '—'; ?></td>
				<td><?php echo epc_erp_h($t['assignee_name'] ?: '—'); ?></td>
				<td><?php echo epc_erp_h($t['priority']); ?></td>
				<td><span class="label label-<?php echo $t['status'] === 'done' ? 'success' : ($t['status'] === 'in_progress' ? 'info' : 'default'); ?>"><?php echo epc_erp_h($t['status']); ?></span></td>
				<td><?php echo (int)$t['due_at'] ? epc_erp_h(date('Y-m-d', (int)$t['due_at'])) : '—'; ?></td>
				<td>
					<?php if ($t['status'] !== 'done' && $t['status'] !== 'cancelled'): ?>
					<button type="button" class="btn btn-xs btn-success epc-wf-done" data-id="<?php echo (int)$t['id']; ?>">Done</button>
					<button type="button" class="btn btn-xs btn-default epc-wf-progress" data-id="<?php echo (int)$t['id']; ?>">Start</button>
					<?php endif; ?>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($tasks)): ?>
			<tr><td colspan="9" class="text-muted">No workflow tasks — run staff setup with sample data.</td></tr>
		<?php endif; ?>
		</tbody>
	</table>
</div>

<script>
(function(){
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, extra) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf);
		if (extra) { for (var k in extra) fd.append(k, extra[k]); }
		return fetch(window.location.pathname, { method: 'POST', body: fd, credentials: 'same-origin' }).then(function(r){ return r.json(); });
	}
	function msg(ok, text) {
		var el = document.getElementById('epc_erp_msg');
		if (!el) return;
		el.className = 'alert alert-' + (ok ? 'success' : 'danger');
		el.textContent = text;
		el.style.display = 'block';
	}
	document.querySelectorAll('.epc-wf-done').forEach(function(btn){
		btn.addEventListener('click', function(){
			post('workflow_status', { task_id: btn.getAttribute('data-id'), status: 'done' }).then(function(j){
				msg(!!j.status, j.message || ''); if (j.status) setTimeout(function(){ location.reload(); }, 600);
			});
		});
	});
	document.querySelectorAll('.epc-wf-progress').forEach(function(btn){
		btn.addEventListener('click', function(){
			post('workflow_status', { task_id: btn.getAttribute('data-id'), status: 'in_progress' }).then(function(j){
				msg(!!j.status, j.message || ''); if (j.status) setTimeout(function(){ location.reload(); }, 600);
			});
		});
	});
})();
</script>
