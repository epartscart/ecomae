<?php
defined('_ASTEXE_') or die('No access');
/**
 * Quality management — test plans, quality orders and
 * non-conformance (NCR).
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_quality.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_qm_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$view = isset($_GET['qv']) ? (string) $_GET['qv'] : 'orders';
$summary = epc_qm_summary($db_link, $companyId);

erp_page_header(
	'<i class="fa fa-check-circle"></i> Quality management',
	'Enterprise test plans, quality orders (inspection results &amp; verdict) and non-conformance (NCR) with corrective actions.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Quality management'),
	)
);

erp_stat_cards(array(
	array('label' => 'Test plans', 'value' => (string) $summary['plans']),
	array('label' => 'Quality orders', 'value' => (string) $summary['orders']),
	array('label' => 'Passed', 'value' => (string) $summary['passed']),
	array('label' => 'Failed', 'value' => (string) $summary['failed']),
	array('label' => 'Open NCRs', 'value' => (string) $summary['open_ncr']),
));

$tabBase = epc_erp_tab_url($erpUrl, 'quality', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$views = array('orders' => 'Quality orders', 'plans' => 'Test plans', 'ncr' => 'Non-conformance');
$sevLabel = array('minor' => 'default', 'major' => 'warning', 'critical' => 'danger');
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="btn-group btn-group-sm" style="margin-bottom:10px;">
	<?php foreach ($views as $k => $lbl): ?>
		<a class="btn btn-<?php echo $view === $k ? 'primary' : 'default'; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'qv=' . $k); ?>"><?php echo epc_erp_h($lbl); ?></a>
	<?php endforeach; ?>
</div>

<?php if ($view === 'plans'):
	$plans = epc_qm_plans($db_link, $companyId);
	$selPlan = (int) ($_GET['plan_id'] ?? 0); ?>
	<div class="row"><div class="col-md-5">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New test plan</h5>
			<form id="epc_qm_plan" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="text" name="code" class="form-control input-sm" placeholder="Code" style="width:110px;" required>
				<input type="text" name="name" class="form-control input-sm" placeholder="Name" style="width:180px;">
				<label><input type="checkbox" name="active" value="1" checked> active</label>
				<button class="btn btn-primary btn-sm">Save</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Code</th><th>Name</th><th class="text-right">Tests</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($plans)): ?><tr><td colspan="4" class="text-muted">No test plans.</td></tr>
			<?php else: foreach ($plans as $p): ?>
				<tr><td><strong><?php echo epc_erp_h($p['code']); ?></strong></td><td><?php echo epc_erp_h($p['name']); ?></td>
				<td class="text-right"><?php echo (int) $p['test_count']; ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'qv=plans&plan_id=' . (int) $p['id']); ?>">Tests</a></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-7">
		<?php if ($selPlan > 0):
			$tests = epc_qm_plan_tests($db_link, $selPlan); ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>Plan tests</strong></div>
				<div class="panel-body">
					<form id="epc_qm_test" class="form-inline" style="margin-bottom:8px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="plan_id" value="<?php echo (int) $selPlan; ?>">
						<input type="text" name="name" class="form-control input-sm" placeholder="Test name" required>
						<select name="test_type" class="form-control input-sm" onchange="var q=this.value==='quantitative';this.form.min_val.style.display=q?'':'none';this.form.max_val.style.display=q?'':'none';this.form.unit.style.display=q?'':'none';this.form.expected.style.display=q?'none':'';"><option value="quantitative">quantitative</option><option value="qualitative">qualitative</option></select>
						<input type="text" name="unit" class="form-control input-sm" placeholder="unit" style="width:70px;">
						<input type="number" step="0.0001" name="min_val" class="form-control input-sm" placeholder="min" style="width:80px;">
						<input type="number" step="0.0001" name="max_val" class="form-control input-sm" placeholder="max" style="width:80px;">
						<input type="text" name="expected" class="form-control input-sm" placeholder="expected" style="width:110px;display:none;">
						<button class="btn btn-primary btn-sm">Add test</button>
					</form>
					<table class="table table-condensed">
						<thead><tr><th>Test</th><th>Type</th><th>Acceptance</th></tr></thead>
						<tbody>
						<?php if (empty($tests)): ?><tr><td colspan="3" class="text-muted">No tests yet.</td></tr>
						<?php else: foreach ($tests as $t):
							$acc = $t['test_type'] === 'qualitative' ? ('= ' . epc_erp_h($t['expected'])) : (epc_erp_h(($t['min_val'] !== null ? rtrim(rtrim($t['min_val'], '0'), '.') : '−∞') . ' .. ' . ($t['max_val'] !== null ? rtrim(rtrim($t['max_val'], '0'), '.') : '+∞') . ' ' . $t['unit'])); ?>
							<tr><td><?php echo epc_erp_h($t['name']); ?></td><td><span class="label label-default"><?php echo epc_erp_h($t['test_type']); ?></span></td><td><?php echo $acc; ?></td></tr>
						<?php endforeach; endif; ?>
						</tbody>
					</table>
				</div>
			</div>
		<?php else: ?><p class="text-muted">Pick a plan to manage its tests.</p><?php endif; ?>
	</div></div>

<?php elseif ($view === 'ncr'):
	$ncrs = epc_qm_ncrs($db_link, $companyId);
	$orders = epc_qm_orders($db_link, $companyId, 100); ?>
	<div class="well well-sm">
		<h5><i class="fa fa-exclamation-triangle"></i> Raise non-conformance</h5>
		<form id="epc_qm_ncr" class="form-inline">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<select name="order_id" class="form-control input-sm"><option value="0">(standalone)</option>
				<?php foreach ($orders as $o): ?><option value="<?php echo (int) $o['id']; ?>">QO-<?php echo (int) $o['id']; ?> · <?php echo epc_erp_h($o['ref_id']); ?></option><?php endforeach; ?>
			</select>
			<input type="text" name="title" class="form-control input-sm" placeholder="Title" style="width:200px;" required>
			<select name="severity" class="form-control input-sm"><option value="minor">minor</option><option value="major">major</option><option value="critical">critical</option></select>
			<select name="disposition" class="form-control input-sm"><option value="">disposition…</option><option value="use_as_is">use as-is</option><option value="rework">rework</option><option value="scrap">scrap</option><option value="return">return</option></select>
			<button class="btn btn-primary btn-sm">Raise</button>
		</form>
	</div>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>#</th><th>Title</th><th>Severity</th><th>Disposition</th><th>Status</th><th>Action</th></tr></thead>
		<tbody>
		<?php if (empty($ncrs)): ?><tr><td colspan="6" class="text-muted">No non-conformances.</td></tr>
		<?php else: foreach ($ncrs as $n): ?>
			<tr><td><strong>NCR-<?php echo (int) $n['id']; ?></strong></td><td><?php echo epc_erp_h($n['title']); ?></td>
			<td><span class="label label-<?php echo $sevLabel[$n['severity']] ?? 'default'; ?>"><?php echo epc_erp_h($n['severity']); ?></span></td>
			<td><?php echo epc_erp_h($n['disposition'] !== '' ? str_replace('_', ' ', $n['disposition']) : '—'); ?></td>
			<td><span class="label label-<?php echo $n['status'] === 'closed' ? 'success' : 'info'; ?>"><?php echo epc_erp_h($n['status']); ?></span></td>
			<td>
				<form class="epc_qm_ncr_upd form-inline" style="display:inline;">
					<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
					<input type="hidden" name="id" value="<?php echo (int) $n['id']; ?>">
					<input type="hidden" name="disposition" value="<?php echo epc_erp_h($n['disposition']); ?>">
					<select name="status" class="input-sm">
						<?php foreach (array('open', 'investigate', 'action', 'closed') as $s): ?><option value="<?php echo $s; ?>" <?php echo $n['status'] === $s ? 'selected' : ''; ?>><?php echo $s; ?></option><?php endforeach; ?>
					</select>
					<input type="text" name="corrective_action" class="input-sm" placeholder="corrective action" value="<?php echo epc_erp_h((string) $n['corrective_action']); ?>" style="width:160px;">
					<button class="btn btn-default btn-xs">Update</button>
				</form>
			</td></tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>

<?php else:
	$orders = epc_qm_orders($db_link, $companyId, 200);
	$plans = epc_qm_plans($db_link, $companyId);
	$selOrder = (int) ($_GET['order_id'] ?? 0); ?>
	<div class="row"><div class="col-md-6">
		<div class="well well-sm">
			<h5><i class="fa fa-plus-circle"></i> New quality order</h5>
			<form id="epc_qm_order" class="form-inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<select name="plan_id" class="form-control input-sm" required><option value="">Plan…</option>
					<?php foreach ($plans as $p): ?><option value="<?php echo (int) $p['id']; ?>"><?php echo epc_erp_h($p['code']); ?></option><?php endforeach; ?>
				</select>
				<select name="ref_type" class="form-control input-sm"><option value="po">PO</option><option value="so">SO</option><option value="production">Production</option><option value="item">Item</option></select>
				<input type="text" name="ref_id" class="form-control input-sm" placeholder="Reference" style="width:120px;">
				<input type="number" name="item_id" class="form-control input-sm" placeholder="Item ID" style="width:90px;">
				<input type="number" step="0.0001" name="qty" class="form-control input-sm" placeholder="Qty" style="width:90px;">
				<button class="btn btn-primary btn-sm">Create</button>
			</form>
		</div>
		<table class="table table-bordered table-condensed">
			<thead><tr><th>#</th><th>Plan</th><th>Ref</th><th>Verdict</th><th></th></tr></thead>
			<tbody>
			<?php if (empty($orders)): ?><tr><td colspan="5" class="text-muted">No quality orders.</td></tr>
			<?php else: foreach ($orders as $o): ?>
				<tr><td><strong>QO-<?php echo (int) $o['id']; ?></strong></td><td><?php echo epc_erp_h((string) $o['plan_code']); ?></td>
				<td><small><?php echo epc_erp_h($o['ref_type'] . ' ' . $o['ref_id']); ?></small></td>
				<td><?php echo $o['verdict'] === '' ? '<span class="label label-default">open</span>' : '<span class="label label-' . ($o['verdict'] === 'pass' ? 'success' : 'danger') . '">' . epc_erp_h($o['verdict']) . '</span>'; ?></td>
				<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($tabBase . $sep . 'qv=orders&order_id=' . (int) $o['id']); ?>">Inspect</a></td></tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>
	</div><div class="col-md-6">
		<?php if ($selOrder > 0):
			$ord = null;
			foreach ($orders as $o) { if ((int) $o['id'] === $selOrder) { $ord = $o; break; } }
			$tests = $ord ? epc_qm_plan_tests($db_link, (int) $ord['plan_id']) : array();
			$existing = array();
			foreach (epc_qm_order_results($db_link, $selOrder) as $r) { $existing[(int) $r['test_id']] = $r; } ?>
			<div class="panel panel-default">
				<div class="panel-heading"><strong>QO-<?php echo (int) $selOrder; ?></strong> — record inspection results</div>
				<div class="panel-body">
					<form id="epc_qm_record">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="order_id" value="<?php echo (int) $selOrder; ?>">
						<table class="table table-condensed">
							<thead><tr><th>Test</th><th>Acceptance</th><th>Result</th><th>Eval</th></tr></thead>
							<tbody>
							<?php if (empty($tests)): ?><tr><td colspan="4" class="text-muted">Plan has no tests.</td></tr>
							<?php else: foreach ($tests as $t):
								$tid = (int) $t['id'];
								$ex = $existing[$tid] ?? null;
								$acc = $t['test_type'] === 'qualitative' ? ('= ' . epc_erp_h($t['expected'])) : epc_erp_h(($t['min_val'] !== null ? rtrim(rtrim($t['min_val'], '0'), '.') : '−∞') . '..' . ($t['max_val'] !== null ? rtrim(rtrim($t['max_val'], '0'), '.') : '+∞') . ' ' . $t['unit']); ?>
								<tr><td><?php echo epc_erp_h($t['name']); ?></td><td><small><?php echo $acc; ?></small></td>
								<td>
									<?php if ($t['test_type'] === 'qualitative'): ?>
										<input type="text" name="v[<?php echo $tid; ?>][value_text]" class="form-control input-sm" value="<?php echo $ex ? epc_erp_h((string) $ex['value_text']) : ''; ?>" placeholder="outcome">
									<?php else: ?>
										<input type="number" step="0.0001" name="v[<?php echo $tid; ?>][value_num]" class="form-control input-sm" value="<?php echo $ex && $ex['value_num'] !== null ? epc_erp_h(rtrim(rtrim((string) $ex['value_num'], '0'), '.')) : ''; ?>" placeholder="measure">
									<?php endif; ?>
								</td>
								<td><?php echo $ex ? '<span class="label label-' . ($ex['result'] === 'pass' ? 'success' : 'danger') . '">' . epc_erp_h($ex['result']) . '</span>' : '—'; ?></td></tr>
							<?php endforeach; endif; ?>
							</tbody>
						</table>
						<button class="btn btn-success btn-sm">Save results &amp; evaluate</button>
						<?php if ($ord && $ord['verdict'] !== ''): ?><span style="margin-left:10px;">Verdict: <span class="label label-<?php echo $ord['verdict'] === 'pass' ? 'success' : 'danger'; ?>"><?php echo epc_erp_h($ord['verdict']); ?></span></span><?php endif; ?>
					</form>
				</div>
			</div>
		<?php else: ?><p class="text-muted">Pick a quality order to record inspection results.</p><?php endif; ?>
	</div></div>
<?php endif; ?>

<script>
(function(){
	var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
	function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 900); }
	function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
	bind('epc_qm_plan', 'qm_plan_save');
	bind('epc_qm_test', 'qm_test_add');
	bind('epc_qm_order', 'qm_order_create');
	bind('epc_qm_record', 'qm_order_record');
	bind('epc_qm_ncr', 'qm_ncr_create');
	Array.prototype.forEach.call(document.querySelectorAll('.epc_qm_ncr_upd'), function(f){ f.addEventListener('submit', function(e){ e.preventDefault(); post('qm_ncr_update', new FormData(f)).then(msg); }); });
})();
</script>
