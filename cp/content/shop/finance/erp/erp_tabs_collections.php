<?php
defined('_ASTEXE_') or die('No access');
/**
 * Collections & credit management — D365 F&O-style collections workspace:
 * cases, activities, promise-to-pay, dunning runs and credit-hold log.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_collections.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_coll_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['cv']) ? (string) $_GET['cv'] : 'cases';
$summary = epc_coll_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-gavel"></i> Collections',
	'D365 F&amp;O-style collections workspace — cases, activities, promise-to-pay, dunning runs and credit holds, on top of the credit/ageing engine.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Collections'),
	)
);

erp_stat_cards(array(
	array('label' => 'Open cases', 'value' => (string) $summary['open_cases']),
	array('label' => 'Promise to pay', 'value' => (string) $summary['promise_cases']),
	array('label' => 'Escalated', 'value' => (string) $summary['escalated_cases']),
	array('label' => 'On credit hold', 'value' => (string) $summary['on_hold']),
	array('label' => 'Dunning runs', 'value' => (string) $summary['dunning_runs']),
	array('label' => 'Open balance', 'value' => epc_erp_money($summary['total_balance'], 0)),
));

$tabBase = epc_erp_tab_url($erpUrl, 'collections', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('cases' => 'Cases workspace', 'dunning' => 'Dunning runs', 'holds' => 'Credit holds');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'cv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'cases'):
	$detailId = (int) ($_GET['case_id'] ?? 0);
	if ($detailId > 0):
		$case = epc_coll_case_get($db_link, $detailId);
		$acts = epc_coll_activities($db_link, $detailId); ?>
		<p><a href="<?php echo epc_erp_h($tabBase . $sep . 'cv=cases'); ?>">&laquo; Back to cases</a></p>
		<?php if ($case): ?>
		<div class="row"><div class="col-md-5">
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Case #<?php echo (int) $case['id']; ?></strong> · customer <?php echo (int) $case['customer_id']; ?></div>
				<div class="panel-body">
					<p>Status: <span class="label label-default"><?php echo epc_erp_h($case['status']); ?></span></p>
					<p>Balance: <strong><?php echo epc_erp_money($case['balance'], 2); ?></strong></p>
					<p>Assigned: <?php echo epc_erp_h($case['assigned_to']); ?></p>
					<?php if ((int) $case['promise_date'] > 0): ?><p>Promise: <?php echo epc_erp_money($case['promise_amount'], 2); ?> by <?php echo date('d M Y', (int) $case['promise_date']); ?></p><?php endif; ?>
					<hr>
					<form id="epc_coll_promise" class="form">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="id" value="<?php echo (int) $case['id']; ?>">
						<div class="row"><div class="col-xs-6 form-group"><label>Promise amount</label><input type="number" step="0.01" name="amount" class="form-control input-sm"></div>
						<div class="col-xs-6 form-group"><label>Promise date</label><input type="date" name="promise_date" class="form-control input-sm"></div></div>
						<button class="btn btn-success btn-sm">Record promise to pay</button>
					</form>
				</div>
			</div>
		</div><div class="col-md-7">
			<div class="well well-sm">
				<form id="epc_coll_activity" class="form-inline">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="case_id" value="<?php echo (int) $case['id']; ?>">
					<select name="type" class="form-control input-sm"><option value="call">Call</option><option value="email">Email</option><option value="letter">Letter</option><option value="note">Note</option></select>
					<input type="text" name="outcome" class="form-control input-sm" placeholder="Outcome" style="min-width:220px;">
					<button class="btn btn-primary btn-sm">Log activity</button>
				</form>
			</div>
			<table class="table table-condensed table-bordered">
				<thead><tr><th>When</th><th>Type</th><th>Outcome</th><th class="text-right">Amount</th></tr></thead>
				<tbody>
				<?php if (empty($acts)): ?><tr><td colspan="4" class="text-muted">No activities yet.</td></tr>
				<?php else: foreach ($acts as $a): ?>
					<tr><td><small><?php echo date('d M H:i', (int) $a['time_created']); ?></small></td><td><?php echo epc_erp_h($a['type']); ?></td><td><?php echo epc_erp_h($a['outcome']); ?></td><td class="text-right"><?php echo (float) $a['amount'] > 0 ? epc_erp_money($a['amount'], 2) : '—'; ?></td></tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div></div>
		<?php endif; ?>
	<?php else:
		$cases = epc_coll_cases($db_link, $companyId); ?>
		<div class="row"><div class="col-md-4">
			<div class="well well-sm">
				<h5><i class="fa fa-plus-circle"></i> New collection case</h5>
				<form id="epc_coll_case" class="form">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<div class="form-group"><label>Customer ID</label><input type="number" name="customer_id" class="form-control input-sm" required></div>
					<div class="form-group"><label>Open balance</label><input type="number" step="0.01" name="balance" class="form-control input-sm"></div>
					<div class="form-group"><label>Assigned to</label><input type="text" name="assigned_to" class="form-control input-sm"></div>
					<div class="form-group"><label>Notes</label><textarea name="notes" class="form-control input-sm" rows="2"></textarea></div>
					<button type="submit" class="btn btn-primary btn-sm">Open case</button>
				</form>
			</div>
		</div><div class="col-md-8">
			<table class="table table-bordered table-condensed">
				<thead><tr><th>#</th><th>Customer</th><th class="text-right">Balance</th><th>Status</th><th>Assigned</th><th></th></tr></thead>
				<tbody>
				<?php if (empty($cases)): ?>
					<tr><td colspan="6" class="text-muted">No cases. Open one for any overdue customer.</td></tr>
				<?php else: foreach ($cases as $c):
					$st = (string) $c['status'];
					$cls = $st === 'resolved' ? 'success' : ($st === 'escalated' ? 'danger' : ($st === 'promise_to_pay' ? 'info' : 'warning')); ?>
					<tr>
						<td><?php echo (int) $c['id']; ?></td>
						<td><strong><?php echo (int) $c['customer_id']; ?></strong></td>
						<td class="text-right"><?php echo epc_erp_money($c['balance'], 2); ?></td>
						<td>
							<select class="input-sm epc-coll-status" data-id="<?php echo (int) $c['id']; ?>">
								<?php foreach (epc_coll_case_statuses() as $s): ?><option value="<?php echo $s; ?>" <?php echo $s === $st ? 'selected' : ''; ?>><?php echo $s; ?></option><?php endforeach; ?>
							</select>
							<span class="label label-<?php echo $cls; ?>"><?php echo epc_erp_h($st); ?></span>
						</td>
						<td><?php echo epc_erp_h($c['assigned_to']); ?></td>
						<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'cv=cases&case_id=' . (int) $c['id']); ?>">Open</a></td>
					</tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div></div>
	<?php endif; ?>

<?php elseif ($view === 'dunning'):
	$log = epc_coll_dunning_log($db_link, $companyId); ?>
	<div class="row"><div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-bell"></i> Run dunning</h5>
			<form id="epc_coll_dunning" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<p class="text-muted" style="font-size:11px;">One customer per line: <code>customerId|d1_30|d31_60|d61_90|d90_plus</code> overdue amounts. Level 1 (1–60d) · 2 (61–90) · 3 (90+). Current accounts are skipped.</p>
				<div class="form-group"><textarea name="customers" class="form-control input-sm" rows="6" placeholder="502|500|0|0|0&#10;503|0|0|0|2000"></textarea></div>
				<button type="submit" class="btn btn-primary btn-sm">Run dunning</button>
			</form>
		</div>
	</div><div class="col-md-8">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Run</th><th>Customer</th><th>Level</th><th class="text-right">Overdue</th><th>Message</th><th>When</th></tr></thead>
			<tbody>
			<?php if (empty($log)): ?>
				<tr><td colspan="6" class="text-muted">No dunning runs yet.</td></tr>
			<?php else: foreach ($log as $d):
				$lvl = (int) $d['level'];
				$lc = $lvl >= 3 ? 'danger' : ($lvl === 2 ? 'warning' : 'info'); ?>
				<tr><td>#<?php echo (int) $d['run_id']; ?></td><td><strong><?php echo (int) $d['customer_id']; ?></strong></td>
				<td><span class="label label-<?php echo $lc; ?>">L<?php echo $lvl; ?></span></td>
				<td class="text-right"><?php echo epc_erp_money($d['amount'], 2); ?></td>
				<td><small><?php echo epc_erp_h($d['message']); ?></small></td>
				<td><small><?php echo date('d M H:i', (int) $d['time_created']); ?></small></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div></div>

<?php else:
	$holds = epc_coll_holds($db_link, $companyId); ?>
	<div class="row"><div class="col-md-4">
		<div class="well well-sm">
			<h5><i class="fa fa-ban"></i> Place / release credit hold</h5>
			<form id="epc_coll_hold" class="form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="form-group"><label>Customer ID</label><input type="number" name="customer_id" class="form-control input-sm" required></div>
				<div class="form-group"><label>Action</label><select name="place" class="form-control input-sm"><option value="1">Place hold</option><option value="0">Release hold</option></select></div>
				<div class="form-group"><label>Reason</label><input type="text" name="reason" class="form-control input-sm"></div>
				<button type="submit" class="btn btn-primary btn-sm">Apply</button>
			</form>
			<p class="text-muted" style="font-size:11px;">Also flips the customer credit profile's on-hold flag used by order credit checks.</p>
		</div>
	</div><div class="col-md-8">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Customer</th><th>Action</th><th>Reason</th><th>By</th><th>When</th></tr></thead>
			<tbody>
			<?php if (empty($holds)): ?>
				<tr><td colspan="5" class="text-muted">No credit-hold actions logged.</td></tr>
			<?php else: foreach ($holds as $h): ?>
				<tr><td><strong><?php echo (int) $h['customer_id']; ?></strong></td>
				<td><span class="label label-<?php echo $h['action'] === 'place' ? 'danger' : 'success'; ?>"><?php echo epc_erp_h($h['action']); ?></span></td>
				<td><?php echo epc_erp_h($h['reason']); ?></td><td><?php echo epc_erp_h($h['actor']); ?></td>
				<td><small><?php echo date('d M H:i', (int) $h['time_created']); ?></small></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div></div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_coll_case', 'coll_case_save');
	bind('epc_coll_activity', 'coll_activity_log');
	bind('epc_coll_promise', 'coll_case_promise');
	bind('epc_coll_dunning', 'coll_dunning_run');
	bind('epc_coll_hold', 'coll_hold_set');
	document.querySelectorAll('.epc-coll-status').forEach(function(s){ s.addEventListener('change', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',s.getAttribute('data-id')); fd.append('status',s.value); post('coll_case_status', fd).then(msg); }); });
})();
</script>
