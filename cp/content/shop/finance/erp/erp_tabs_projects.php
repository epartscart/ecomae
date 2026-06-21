<?php
defined('_ASTEXE_') or die('No access');
/**
 * Project management — projects, tasks, milestones (% complete), time tracking,
 * costing (cost vs budget), billing (T&M / fixed) and project profitability.
 * Backed by epc_prj_* in epc_erp_projects.php.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_projects.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_prj_ensure_schema($db_link);

$csrfLocal = isset($csrf) ? $csrf : '';
$prjId     = isset($_GET['prj']) ? (int) $_GET['prj'] : 0;
$portfolio = epc_prj_portfolio($db_link);
$projects  = epc_prj_list($db_link, 200);

/* Customer + employee option sources (best-effort, self-contained). */
$prjCustomers = array();
try {
	foreach ($db_link->query("SELECT `user_id` AS id, `email` AS name FROM `users` WHERE `active`=1 ORDER BY `user_id` DESC LIMIT 500") as $u) {
		$prjCustomers[] = array('id' => (int) $u['id'], 'name' => (string) $u['name']);
	}
} catch (Throwable $e) {}
$prjEmployees = array();
try {
	foreach ($db_link->query("SELECT `id`, `name` FROM `epc_hr_employees` ORDER BY `name`") as $em) {
		$prjEmployees[] = array('id' => (int) $em['id'], 'name' => (string) $em['name']);
	}
} catch (Throwable $e) {}

erp_page_header(
	'<i class="fa fa-tasks"></i> Project management',
	'Projects, tasks &amp; milestones, time tracking, cost-vs-budget, billing (T&amp;M / fixed) and project profitability.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Projects'),
	)
);

erp_stat_cards(array(
	array('label' => 'Projects', 'value' => (string) $portfolio['projects']),
	array('label' => 'Open', 'value' => (string) $portfolio['open']),
	array('label' => 'Cost to date', 'value' => epc_erp_money($portfolio['cost']) . ' AED'),
	array('label' => 'Billable value', 'value' => epc_erp_money($portfolio['billable']) . ' AED'),
	array('label' => 'Margin', 'value' => epc_erp_money($portfolio['margin']) . ' AED'),
));

$tabBase = epc_erp_tab_url($erpUrl, 'projects', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
	<div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New project</h5>
			<form id="epc_prj_new" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-xs-5 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" placeholder="PRJ-001" required></div>
					<div class="col-xs-7 form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm" required></div>
				</div>
				<div class="form-group">
					<label>Customer (optional)</label>
					<select name="customer_id" class="form-control input-sm">
						<option value="0">— none —</option>
						<?php foreach ($prjCustomers as $c): ?>
							<option value="<?php echo (int) $c['id']; ?>"><?php echo epc_erp_h($c['name']); ?></option>
						<?php endforeach; ?>
					</select>
				</div>
				<div class="row">
					<div class="col-xs-5 form-group">
						<label>Billing</label>
						<select name="billing_type" class="form-control input-sm">
							<option value="tm">Time &amp; materials</option>
							<option value="fixed">Fixed price</option>
						</select>
					</div>
					<div class="col-xs-7 form-group"><label>Budget cost</label><input type="number" step="0.01" name="budget_cost" class="form-control input-sm" value="0"></div>
				</div>
				<div class="form-group"><label>Contract value (fixed price)</label><input type="number" step="0.01" name="contract_value" class="form-control input-sm" value="0"></div>
				<button type="submit" class="btn btn-primary btn-sm">Create project</button>
			</form>
		</div>
	</div>

	<div class="col-md-8">
		<h5>Project portfolio</h5>
		<div class="table-responsive">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Name</th><th>Billing</th><th class="text-right">Cost / budget</th><th class="text-right">Billable</th><th class="text-right">Margin</th><th class="text-right">%</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($projects)): ?>
				<tr><td colspan="8" class="text-muted">No projects yet. Create one on the left.</td></tr>
			<?php else: foreach ($projects as $p): $s = $p['summary']; ?>
				<tr<?php echo $prjId === (int) $p['id'] ? ' class="info"' : ''; ?>>
					<td><strong><?php echo epc_erp_h($p['code']); ?></strong></td>
					<td><?php echo epc_erp_h($p['name']); ?> <?php echo (string) $p['status'] !== 'open' ? '<span class="label label-default">' . epc_erp_h($p['status']) . '</span>' : ''; ?></td>
					<td><small><?php echo $p['billing_type'] === 'fixed' ? 'Fixed' : 'T&amp;M'; ?></small></td>
					<td class="text-right<?php echo !empty($s['over_budget']) ? ' text-danger' : ''; ?>"><?php echo epc_erp_money($s['cost']); ?> / <?php echo epc_erp_money($s['budget_cost']); ?></td>
					<td class="text-right"><?php echo epc_erp_money($s['billable_value']); ?></td>
					<td class="text-right"><?php echo epc_erp_money($s['margin']); ?></td>
					<td class="text-right"><?php echo (float) $s['percent_complete']; ?>%</td>
					<td><a class="btn btn-link btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'prj=' . (int) $p['id']); ?>">Manage</a></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
		</div>
	</div>
</div>

<?php if ($prjId > 0):
	$proj = epc_prj_get($db_link, $prjId);
	if ($proj):
		$tasks = epc_prj_tasks_list($db_link, $prjId);
		$times = epc_prj_timesheets_list($db_link, $prjId, 100);
		$sum = epc_prj_summary($db_link, $prjId);
?>
<hr>
<div class="epc-erp-section">
	<h4><i class="fa fa-folder-open"></i> <?php echo epc_erp_h($proj['code'] . ' · ' . $proj['name']); ?></h4>
	<div class="epc-erp-kpi" style="margin-bottom:12px;">
		<div class="kpi"><div class="lbl">Hours</div><div class="val"><?php echo (float) $sum['hours']; ?></div></div>
		<div class="kpi"><div class="lbl">Cost / budget</div><div class="val"><?php echo epc_erp_money($sum['cost']); ?> / <?php echo epc_erp_money($sum['budget_cost']); ?></div></div>
		<div class="kpi"><div class="lbl">Billable value</div><div class="val"><?php echo epc_erp_money($sum['billable_value']); ?></div></div>
		<div class="kpi"><div class="lbl">Revenue recognised</div><div class="val"><?php echo epc_erp_money($sum['revenue_recognized']); ?></div></div>
		<div class="kpi"><div class="lbl">Margin</div><div class="val"><?php echo epc_erp_money($sum['margin']); ?></div></div>
		<div class="kpi"><div class="lbl">% complete</div><div class="val"><?php echo (float) $sum['percent_complete']; ?>%</div></div>
	</div>
</div>

<div class="row">
	<div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-check-square-o"></i> Add task / milestone</h5>
			<form id="epc_prj_task" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="project_id" value="<?php echo (int) $prjId; ?>">
				<input type="text" name="name" class="form-control input-sm" placeholder="Task / milestone" style="width:200px;" required>
				<input type="number" step="0.01" name="planned_hours" class="form-control input-sm" placeholder="Planned hrs" style="width:100px;">
				<input type="number" step="1" min="0" max="100" name="percent_complete" class="form-control input-sm" placeholder="% done" style="width:80px;">
				<button type="submit" class="btn btn-default btn-sm">Add</button>
			</form>
			<table class="table table-condensed" style="margin-top:8px;">
				<thead><tr><th>Task</th><th class="text-right">Planned hrs</th><th class="text-right">% complete</th><th>Status</th></tr></thead>
				<tbody>
				<?php if (empty($tasks)): ?>
					<tr><td colspan="4" class="text-muted">No tasks yet.</td></tr>
				<?php else: foreach ($tasks as $t): ?>
					<tr><td><?php echo epc_erp_h($t['name']); ?></td><td class="text-right"><?php echo (float) $t['planned_hours']; ?></td><td class="text-right"><?php echo (float) $t['percent_complete']; ?>%</td><td><small><?php echo epc_erp_h($t['status']); ?></small></td></tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	</div>

	<div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-clock-o"></i> Log time</h5>
			<form id="epc_prj_time" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="project_id" value="<?php echo (int) $prjId; ?>">
				<select name="task_id" class="form-control input-sm" style="width:150px;">
					<option value="0">— general —</option>
					<?php foreach ($tasks as $t): ?><option value="<?php echo (int) $t['id']; ?>"><?php echo epc_erp_h($t['name']); ?></option><?php endforeach; ?>
				</select>
				<?php if (!empty($prjEmployees)): ?>
				<select name="employee_id" class="form-control input-sm" style="width:140px;">
					<option value="0">— employee —</option>
					<?php foreach ($prjEmployees as $em): ?><option value="<?php echo (int) $em['id']; ?>"><?php echo epc_erp_h($em['name']); ?></option><?php endforeach; ?>
				</select>
				<?php endif; ?>
				<input type="number" step="0.25" name="hours" class="form-control input-sm" placeholder="Hours" style="width:80px;" required>
				<input type="number" step="0.01" name="cost_rate" class="form-control input-sm" placeholder="Cost/hr" style="width:90px;">
				<input type="number" step="0.01" name="bill_rate" class="form-control input-sm" placeholder="Bill/hr" style="width:90px;">
				<label style="font-weight:normal;font-size:12px;"><input type="checkbox" name="billable" value="1" checked> Billable</label>
				<button type="submit" class="btn btn-default btn-sm">Log</button>
			</form>
			<table class="table table-condensed" style="margin-top:8px;">
				<thead><tr><th>Task</th><th class="text-right">Hrs</th><th class="text-right">Cost</th><th class="text-right">Bill</th></tr></thead>
				<tbody>
				<?php if (empty($times)): ?>
					<tr><td colspan="4" class="text-muted">No time logged.</td></tr>
				<?php else: foreach ($times as $ts): ?>
					<tr><td><small><?php echo epc_erp_h($ts['task_name'] ?: 'General'); ?></small></td><td class="text-right"><?php echo (float) $ts['hours']; ?></td><td class="text-right"><?php echo epc_erp_money((float) $ts['hours'] * (float) $ts['cost_rate']); ?></td><td class="text-right"><?php echo (int) $ts['billable'] ? epc_erp_money((float) $ts['hours'] * (float) $ts['bill_rate']) : '—'; ?></td></tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	</div>
</div>
<?php endif; endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; } if(j.status) setTimeout(function(){ location.reload(); }, 700); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_prj_new', 'prj_save');
	bind('epc_prj_task', 'prj_task_save');
	bind('epc_prj_time', 'prj_log_time');
})();
</script>
