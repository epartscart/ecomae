<?php
defined('_ASTEXE_') or die('No access');
/**
 * Project accounting depth — D365 F&O-style project budgets vs actuals, WIP and
 * revenue recognition (PoC / completed-contract / straight-line).
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_project_accounting.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_projects.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_prja_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$summary = epc_prja_summary($db_link, $companyId);
$projects = epc_prj_list($db_link, 200);
$selPid = (int) ($_GET['pid'] ?? (isset($projects[0]) ? (int) $projects[0]['id'] : 0));

erp_page_header(
	'<i class="fa fa-pie-chart"></i> Project accounting',
	'D365 F&amp;O-style project budgets vs actuals, work-in-progress (WIP) and revenue recognition (percentage-of-completion / completed-contract / straight-line).',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Project accounting'),
	)
);

erp_stat_cards(array(
	array('label' => 'Projects budgeted', 'value' => (string) $summary['projects_with_budget']),
	array('label' => 'Cost budget', 'value' => epc_erp_money($summary['cost_budget'], 0)),
	array('label' => 'Revenue budget', 'value' => epc_erp_money($summary['revenue_budget'], 0)),
	array('label' => 'Cost actual', 'value' => epc_erp_money($summary['cost_actual'], 0)),
	array('label' => 'WIP (latest)', 'value' => epc_erp_money($summary['wip'], 0)),
));

$tabBase = epc_erp_tab_url($erpUrl, 'project_accounting', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<form method="get" class="form-inline" style="margin-bottom:10px;">
	<?php foreach ($_GET as $k => $v) { if ($k === 'pid') { continue; } echo '<input type="hidden" name="' . epc_erp_h($k) . '" value="' . epc_erp_h((string) $v) . '">'; } ?>
	<label>Project</label>
	<select name="pid" class="form-control input-sm" onchange="this.form.submit()">
		<?php foreach ($projects as $p): ?>
			<option value="<?php echo (int) $p['id']; ?>" <?php echo (int) $p['id'] === $selPid ? 'selected' : ''; ?>><?php echo epc_erp_h(($p['code'] ? $p['code'] . ' · ' : '') . $p['name']); ?></option>
		<?php endforeach; ?>
	</select>
</form>

<?php if ($selPid <= 0): ?>
	<p class="text-muted">No projects yet. Create a project under <strong>Collaboration ▸ Projects</strong> first.</p>
<?php else:
	$budgets = epc_prja_budgets($db_link, $selPid);
	$pnl = epc_prja_pnl($db_link, $selPid);
	$txns = epc_prja_txns($db_link, $selPid);
	$recs = epc_prja_recognitions($db_link, $selPid); ?>

	<div class="row">
		<div class="col-md-6">
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Budget vs actual</strong></div>
				<table class="table table-condensed" style="margin-bottom:0;">
					<tbody>
						<tr><td>Cost budget</td><td class="text-right"><?php echo epc_erp_money($pnl['cost_budget'], 2); ?></td><td>Cost actual</td><td class="text-right"><?php echo epc_erp_money($pnl['cost_actual'], 2); ?></td></tr>
						<tr><td>Revenue budget</td><td class="text-right"><?php echo epc_erp_money($pnl['revenue_budget'], 2); ?></td><td>Revenue actual</td><td class="text-right"><?php echo epc_erp_money($pnl['revenue_actual'], 2); ?></td></tr>
						<tr><td>Budget margin</td><td class="text-right"><strong><?php echo epc_erp_money($pnl['budget_margin'], 2); ?></strong></td><td>Actual margin</td><td class="text-right"><strong><?php echo epc_erp_money($pnl['actual_margin'], 2); ?></strong></td></tr>
						<tr><td>Cost variance</td><td class="text-right"><span class="label label-<?php echo $pnl['cost_variance'] < 0 ? 'danger' : 'success'; ?>"><?php echo epc_erp_money($pnl['cost_variance'], 2); ?></span></td>
						<td>% complete</td><td class="text-right"><?php echo number_format((float) $pnl['pct_complete'] * 100, 1); ?>%
						<?php if ($pnl['over_budget']): ?><span class="label label-danger">over budget</span><?php endif; ?></td></tr>
						<tr><td>Billed</td><td class="text-right"><?php echo epc_erp_money($pnl['billed'], 2); ?></td><td></td><td></td></tr>
					</tbody>
				</table>
			</div>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Budget lines</strong></div>
				<table class="table table-condensed" style="margin-bottom:0;">
					<thead><tr><th>Category</th><th class="text-right">Cost budget</th><th class="text-right">Revenue budget</th></tr></thead>
					<tbody>
					<?php if (empty($budgets)): ?><tr><td colspan="3" class="text-muted">No budget lines.</td></tr>
					<?php else: foreach ($budgets as $b): ?>
						<tr><td><?php echo epc_erp_h($b['category']); ?></td><td class="text-right"><?php echo epc_erp_money($b['cost_budget'], 2); ?></td><td class="text-right"><?php echo epc_erp_money($b['revenue_budget'], 2); ?></td></tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
			</div>
		</div>

		<div class="col-md-6">
			<div class="well well-sm">
				<h5><i class="fa fa-plus-circle"></i> Add budget line</h5>
				<form id="epc_prja_budget" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="project_id" value="<?php echo (int) $selPid; ?>">
					<input type="text" name="category" class="form-control input-sm" placeholder="Category" required>
					<input type="number" step="0.01" name="cost_budget" class="form-control input-sm" placeholder="Cost" style="width:110px;">
					<input type="number" step="0.01" name="revenue_budget" class="form-control input-sm" placeholder="Revenue" style="width:110px;">
					<button class="btn btn-primary btn-sm">Add</button>
				</form>
			</div>
			<div class="well well-sm">
				<h5><i class="fa fa-exchange"></i> Post transaction</h5>
				<form id="epc_prja_txn" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="project_id" value="<?php echo (int) $selPid; ?>">
					<select name="txn_type" class="form-control input-sm"><option value="cost">Cost</option><option value="revenue">Revenue</option><option value="billing">Billing</option></select>
					<input type="text" name="description" class="form-control input-sm" placeholder="Description" style="width:150px;">
					<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount" style="width:110px;" required>
					<button class="btn btn-primary btn-sm">Post</button>
				</form>
			</div>
			<div class="well well-sm">
				<h5><i class="fa fa-calculator"></i> Recognize revenue</h5>
				<form id="epc_prja_recognize" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="project_id" value="<?php echo (int) $selPid; ?>">
					<select name="method" class="form-control input-sm"><option value="poc">Percentage-of-completion</option><option value="completed">Completed contract</option><option value="straight_line">Straight-line</option></select>
					<input type="number" step="0.01" min="0" max="1" name="fraction" class="form-control input-sm" placeholder="elapsed fr." style="width:100px;" title="Elapsed fraction for straight-line (0..1)">
					<button class="btn btn-success btn-sm">Run</button>
				</form>
			</div>

			<div class="panel panel-default">
				<div class="panel-heading"><strong>Recognition history</strong></div>
				<table class="table table-condensed" style="margin-bottom:0;">
					<thead><tr><th>Method</th><th class="text-right">% compl</th><th class="text-right">Rev</th><th class="text-right">Cost</th><th class="text-right">WIP</th><th>When</th></tr></thead>
					<tbody>
					<?php if (empty($recs)): ?><tr><td colspan="6" class="text-muted">No recognition runs.</td></tr>
					<?php else: foreach ($recs as $r): ?>
						<tr><td><?php echo epc_erp_h($r['method']); ?></td><td class="text-right"><?php echo number_format((float) $r['pct_complete'] * 100, 1); ?>%</td>
						<td class="text-right"><?php echo epc_erp_money($r['recognized_revenue'], 2); ?></td><td class="text-right"><?php echo epc_erp_money($r['recognized_cost'], 2); ?></td>
						<td class="text-right"><span class="label label-<?php echo (float) $r['wip'] < 0 ? 'warning' : 'info'; ?>"><?php echo epc_erp_money($r['wip'], 2); ?></span></td>
						<td><small><?php echo date('d M H:i', (int) $r['time_created']); ?></small></td></tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>

	<div class="panel panel-default">
		<div class="panel-heading"><strong>Transactions</strong></div>
		<table class="table table-condensed" style="margin-bottom:0;">
			<thead><tr><th>Date</th><th>Type</th><th>Category</th><th>Description</th><th class="text-right">Amount</th></tr></thead>
			<tbody>
			<?php if (empty($txns)): ?><tr><td colspan="5" class="text-muted">No transactions.</td></tr>
			<?php else: foreach ($txns as $t):
				$tc = $t['txn_type'] === 'cost' ? 'danger' : ($t['txn_type'] === 'revenue' ? 'success' : 'info'); ?>
				<tr><td><small><?php echo date('d M Y', (int) $t['txn_date']); ?></small></td>
				<td><span class="label label-<?php echo $tc; ?>"><?php echo epc_erp_h($t['txn_type']); ?></span></td>
				<td><?php echo epc_erp_h($t['category']); ?></td><td><?php echo epc_erp_h($t['description']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($t['amount'], 2); ?></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_prja_budget', 'prja_budget_save');
	bind('epc_prja_txn', 'prja_txn_add');
	bind('epc_prja_recognize', 'prja_recognize');
})();
</script>
