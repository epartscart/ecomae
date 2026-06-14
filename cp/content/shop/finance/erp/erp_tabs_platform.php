<?php
defined('_ASTEXE_') or die('No access');
/**
 * Platform / cross-cutting services — batch jobs (definitions + run history +
 * recurrence) and feature management (flags). Links to the existing workflow
 * engine and data-entity / OData layer.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_platform.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_plt_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['pv']) ? (string) $_GET['pv'] : 'batch';
$summary = epc_plt_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-cogs"></i> Platform services',
	'Cross-cutting platform: batch jobs, feature management, workflow and data entities (D365 F&amp;O platform layer).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Platform services'),
	)
);

erp_stat_cards(array(
	array('label' => 'Batch jobs', 'value' => (string) $summary['jobs']),
	array('label' => 'Active jobs', 'value' => (string) $summary['active_jobs']),
	array('label' => 'Job runs', 'value' => (string) $summary['runs']),
	array('label' => 'Features on', 'value' => $summary['features_on'] . ' / ' . $summary['features']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'platform', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('batch' => 'Batch jobs', 'features' => 'Feature management', 'services' => 'Platform services');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'pv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'features'):
	$features = epc_plt_features($db_link, $companyId); ?>
	<div class="well well-sm">
		<h5><i class="fa fa-flask"></i> New / update feature flag</h5>
		<form id="epc_plt_feat" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="text" name="code" class="form-control input-sm" placeholder="code" style="width:140px;" required>
			<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:180px;">
			<label><input type="checkbox" name="enabled" value="1"> enabled</label>
			<button class="btn btn-primary btn-sm">Save</button>
		</form>
	</div>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Code</th><th>Name</th><th>State</th><th></th></tr></thead>
		<tbody>
		<?php if (empty($features)): ?><tr><td colspan="4" class="text-muted">No features.</td></tr>
		<?php else: foreach ($features as $f): ?>
			<tr><td><code><?php echo epc_erp_h($f['code']); ?></code></td><td><?php echo epc_erp_h($f['name']); ?></td>
			<td><span class="label label-<?php echo $f['enabled'] ? 'success' : 'default'; ?>"><?php echo $f['enabled'] ? 'enabled' : 'disabled'; ?></span></td>
			<td>
				<form class="epc_plt_toggle form-inline" style="display:inline;">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="code" value="<?php echo epc_erp_h($f['code']); ?>">
					<input type="hidden" name="enabled" value="<?php echo $f['enabled'] ? '0' : '1'; ?>">
					<button class="btn btn-default btn-xs"><?php echo $f['enabled'] ? 'Disable' : 'Enable'; ?></button>
				</form>
			</td></tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>

<?php elseif ($view === 'services'): ?>
	<div class="row">
		<div class="col-md-4"><div class="panel panel-default"><div class="panel-body">
			<h5><i class="fa fa-random"></i> Workflow</h5>
			<p class="text-muted">Submit → approve → reject rules with multi-step approval hierarchy.</p>
			<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'workflow', $date_from_str, $date_to_str)); ?>">Open workflow</a>
		</div></div></div>
		<div class="col-md-4"><div class="panel panel-default"><div class="panel-body">
			<h5><i class="fa fa-database"></i> Data entities &amp; OData</h5>
			<p class="text-muted">Logical data entities + OData-style query explorer + business events.</p>
			<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'integration', $date_from_str, $date_to_str)); ?>">Open data &amp; integration</a>
		</div></div></div>
		<div class="col-md-4"><div class="panel panel-default"><div class="panel-body">
			<h5><i class="fa fa-shield"></i> Security roles</h5>
			<p class="text-muted">Privileges → duties → roles → user assignment + access explorer.</p>
			<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'security_roles', $date_from_str, $date_to_str)); ?>">Open security roles</a>
		</div></div></div>
	</div>
	<p class="text-muted">These cross-cutting services span every module — workflow approvals, the data-entity / OData integration layer, and role-based security.</p>

<?php else:
	$jobs = epc_plt_batch_jobs($db_link, $companyId);
	$selJob = (int) ($_GET['job_id'] ?? 0); ?>
	<div class="row"><div class="col-md-7">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New batch job</h5>
			<form id="epc_plt_job" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="text" name="code" class="form-control input-sm" placeholder="Code" style="width:110px;" required>
				<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:160px;">
				<input type="number" name="recurrence_min" class="form-control input-sm" placeholder="Every (min)" style="width:100px;" title="0 = one-time">
				<label><input type="checkbox" name="active" value="1" checked> active</label>
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Recurrence</th><th>Status</th><th>Next run</th><th class="text-right">Runs</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($jobs)): ?><tr><td colspan="6" class="text-muted">No batch jobs.</td></tr>
			<?php else: foreach ($jobs as $j):
				$stColor = array('waiting' => 'default', 'executing' => 'info', 'ended' => 'success', 'error' => 'danger', 'canceled' => 'warning'); ?>
				<tr><td><strong><?php echo epc_erp_h($j['code']); ?></strong> <small><?php echo epc_erp_h($j['name']); ?></small></td>
				<td><?php echo (int) $j['recurrence_min'] > 0 ? 'every ' . (int) $j['recurrence_min'] . ' min' : 'one-time'; ?></td>
				<td><span class="label label-<?php echo $stColor[$j['status']] ?? 'default'; ?>"><?php echo epc_erp_h($j['status']); ?></span></td>
				<td><?php echo (int) $j['next_run'] > 0 ? date('Y-m-d H:i', (int) $j['next_run']) : '—'; ?></td>
				<td class="text-right"><?php echo (int) $j['run_count']; ?></td>
				<td>
					<form class="epc_plt_run form-inline" style="display:inline;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="job_id" value="<?php echo (int) $j['id']; ?>">
						<input type="hidden" name="status" value="ended">
						<button class="btn btn-success btn-xs" title="Run now">Run</button>
					</form>
					<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'job_id=' . (int) $j['id']); ?>">History</a>
				</td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-5">
		<?php if ($selJob > 0):
			$runs = epc_plt_batch_runs($db_link, $selJob); ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Run history</strong></div>
				<table class="table table-condensed" style="margin-bottom:0;">
					<thead><tr><th>When</th><th>Status</th><th>Message</th></tr></thead>
					<tbody>
					<?php if (empty($runs)): ?><tr><td colspan="3" class="text-muted">No runs.</td></tr>
					<?php else: foreach ($runs as $r): ?>
						<tr><td><?php echo date('Y-m-d H:i', (int) $r['started']); ?></td>
						<td><span class="label label-<?php echo $r['status'] === 'ended' ? 'success' : ($r['status'] === 'error' ? 'danger' : 'default'); ?>"><?php echo epc_erp_h($r['status']); ?></span></td>
						<td><?php echo epc_erp_h($r['message']); ?></td></tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
			</div>
		<?php else: ?><p class="text-muted">Pick a job to view its run history. "Run" records an execution and rolls the next run by the recurrence.</p><?php endif; ?>
	</div></div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	function bindAll(cls, action){ Array.prototype.forEach.call(document.querySelectorAll('.'+cls), function(f){ f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }); }
	bind('epc_plt_job', 'plt_job_save');
	bind('epc_plt_feat', 'plt_feature_save');
	bindAll('epc_plt_run', 'plt_job_run');
	bindAll('epc_plt_toggle', 'plt_feature_save');
})();
</script>
