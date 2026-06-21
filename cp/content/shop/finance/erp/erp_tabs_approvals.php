<?php
/**
 * ERP tab — BOS Workflow pillar: approval engine (rules, queue, audit log).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';

epc_bos_wf_seed($db_link);

$apanel = isset($_GET['ap_panel']) ? (string) $_GET['ap_panel'] : 'queue';
$summary = epc_bos_wf_summary($db_link);
$entityTypes = epc_bos_wf_entity_types();
$csrfLocal = isset($csrf) ? $csrf : '';

$apUrl = function ($panel) use ($erpUrl, $date_from_str, $date_to_str) {
	return epc_erp_h(epc_erp_tab_url($erpUrl, 'approvals', $date_from_str, $date_to_str, 'overview') . '&ap_panel=' . $panel);
};
?>

<div class="epc-erp-section">
	<div class="alert alert-info" style="margin-bottom:14px;">
		<strong><i class="fa fa-check-square-o"></i> Workflow &amp; approvals pillar</strong> — a reusable approval engine. Define threshold rules per document type;
		high-value transactions are routed for sign-off with a full audit trail. Config-driven, per-tenant.
	</div>

	<div class="row" style="margin-bottom:16px;">
		<div class="col-sm-3"><div style="border-left:4px solid #e67e22;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:24px;font-weight:700;"><?php echo (int) $summary['pending']; ?></div><div class="text-muted">Pending</div></div></div>
		<div class="col-sm-3"><div style="border-left:4px solid #27ae60;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:24px;font-weight:700;"><?php echo (int) $summary['approved']; ?></div><div class="text-muted">Approved</div></div></div>
		<div class="col-sm-3"><div style="border-left:4px solid #c0392b;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:24px;font-weight:700;"><?php echo (int) $summary['rejected']; ?></div><div class="text-muted">Rejected</div></div></div>
		<div class="col-sm-3"><div style="border-left:4px solid #2980b9;padding:10px 14px;background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08);"><div style="font-size:24px;font-weight:700;"><?php echo (int) $summary['rules']; ?></div><div class="text-muted">Active rules</div></div></div>
	</div>

	<ul class="nav nav-pills" style="margin-bottom:16px;">
		<li class="<?php echo $apanel === 'queue' ? 'active' : ''; ?>"><a href="<?php echo $apUrl('queue'); ?>"><i class="fa fa-inbox"></i> Approval queue</a></li>
		<li class="<?php echo $apanel === 'rules' ? 'active' : ''; ?>"><a href="<?php echo $apUrl('rules'); ?>"><i class="fa fa-sliders"></i> Rules</a></li>
		<li class="<?php echo $apanel === 'history' ? 'active' : ''; ?>"><a href="<?php echo $apUrl('history'); ?>"><i class="fa fa-history"></i> History &amp; audit</a></li>
		<li class="<?php echo $apanel === 'test' ? 'active' : ''; ?>"><a href="<?php echo $apUrl('test'); ?>"><i class="fa fa-flask"></i> Test rule</a></li>
	</ul>

<?php if ($apanel === 'queue'):
	$pending = epc_bos_wf_requests($db_link, 'pending', 100); ?>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Document</th><th>Type</th><th>Amount</th><th>Step</th><th>Raised</th><th>Decision</th></tr></thead>
		<tbody>
		<?php foreach ($pending as $r): $steps = epc_bos_wf_decode_steps($r['steps_json']); $cur = (int) $r['current_step']; $step = $steps[$cur] ?? array('label' => 'Approval'); ?>
			<tr>
				<td><strong><?php echo epc_erp_h($r['title']); ?></strong><br><small class="text-muted"><?php echo epc_erp_h($r['entity_ref']); ?></small></td>
				<td><?php echo epc_erp_h($entityTypes[$r['entity_type']] ?? $r['entity_type']); ?></td>
				<td><?php echo number_format((float) $r['amount'], 2); ?></td>
				<td><span class="label label-warning">Step <?php echo $cur + 1; ?>/<?php echo count($steps); ?>: <?php echo epc_erp_h($step['label']); ?></span></td>
				<td><small><?php echo epc_erp_h(date('d M Y', (int) $r['created_at'])); ?></small></td>
				<td style="white-space:nowrap;">
					<form data-bos-action="bos_wf_decide" style="display:inline-block;margin:0;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="request_id" value="<?php echo (int) $r['id']; ?>">
						<input type="hidden" name="decision" value="approve">
						<input type="text" name="comment" class="form-control input-sm" placeholder="Comment" style="width:120px;display:inline-block;">
						<button class="btn btn-xs btn-success" type="submit">Approve</button>
					</form>
					<form data-bos-action="bos_wf_decide" style="display:inline-block;margin:0;" onsubmit="return confirm('Reject this request?');">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="request_id" value="<?php echo (int) $r['id']; ?>">
						<input type="hidden" name="decision" value="reject">
						<button class="btn btn-xs btn-danger" type="submit">Reject</button>
					</form>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($pending)): ?><tr><td colspan="6" class="text-muted">No pending approvals.</td></tr><?php endif; ?>
		</tbody>
	</table>

<?php elseif ($apanel === 'rules'):
	$rules = epc_bos_wf_rules($db_link); ?>
	<div style="background:#f7f9fb;padding:14px;border-radius:6px;margin-bottom:16px;">
		<h4 style="margin-top:0;" id="epc_wf_form_title">Add approval rule</h4>
		<p class="text-muted" style="margin-bottom:10px;">Rules apply to accounting vouchers, sales and purchase documents. Set a threshold and an approver chain (add as many steps as you need). Edit or disable any rule anytime — changes take effect immediately.</p>
		<form data-bos-action="bos_wf_save_rule" id="epc_wf_rule_form">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="rule_id" id="epc_wf_rule_id" value="">
			<div class="row">
				<div class="col-sm-4 form-group"><label>Rule name</label><input type="text" name="name" id="epc_wf_name" class="form-control input-sm" placeholder="e.g. High-value purchase" required></div>
				<div class="col-sm-3 form-group"><label>Applies to</label>
					<select name="entity_type" id="epc_wf_entity" class="form-control input-sm">
						<?php foreach ($entityTypes as $k => $v): ?><option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($v); ?></option><?php endforeach; ?>
					</select>
				</div>
				<div class="col-sm-2 form-group"><label>Condition</label>
					<select name="operator" id="epc_wf_operator" class="form-control input-sm">
						<option value=">=">amount ≥</option>
						<option value=">">amount &gt;</option>
						<option value="<=">amount ≤</option>
						<option value="any">any amount</option>
					</select>
				</div>
				<div class="col-sm-2 form-group"><label>Threshold</label><input type="number" step="0.01" name="threshold_amount" id="epc_wf_threshold" class="form-control input-sm" placeholder="10000" value="10000"></div>
				<div class="col-sm-1 form-group"><label>Priority</label><input type="number" name="priority" id="epc_wf_priority" class="form-control input-sm" value="100" title="Lower number = checked first"></div>
			</div>
			<label>Approver chain (in order)</label>
			<div id="epc_wf_steps">
				<div class="form-group" style="display:flex;gap:8px;margin-bottom:6px;">
					<input type="text" name="step_role[]" class="form-control input-sm" placeholder="Approver role (e.g. Manager)" value="Manager" style="max-width:240px;">
					<input type="text" name="step_label[]" class="form-control input-sm" placeholder="Step label (optional)" style="max-width:240px;">
					<button type="button" class="btn btn-xs btn-default epc-wf-rm-step" title="Remove step">&times;</button>
				</div>
			</div>
			<button type="button" class="btn btn-xs btn-default" id="epc_wf_add_step"><i class="fa fa-plus"></i> Add approval step</button>
			<div style="margin-top:12px;">
				<button class="btn btn-sm btn-primary" type="submit" id="epc_wf_submit">Add rule</button>
				<button class="btn btn-sm btn-default" type="button" id="epc_wf_cancel" style="display:none;">Cancel edit</button>
			</div>
		</form>
	</div>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Rule</th><th>Document type</th><th>Condition</th><th>Approval steps</th><th>Priority</th><th></th></tr></thead>
		<tbody>
		<?php foreach ($rules as $rule): $steps = epc_bos_wf_decode_steps($rule['steps_json']); $stepNames = array_map(function ($s) { return $s['role'] ?? 'Approver'; }, $steps);
			$ruleData = array('id' => (int) $rule['id'], 'name' => (string) $rule['name'], 'entity_type' => (string) $rule['entity_type'], 'operator' => (string) $rule['operator'], 'threshold_amount' => (float) $rule['threshold_amount'], 'priority' => (int) $rule['priority'], 'steps' => $steps); ?>
			<tr class="<?php echo (int) $rule['active'] ? '' : 'text-muted'; ?>">
				<td><strong><?php echo epc_erp_h($rule['name']); ?></strong><?php if (!(int) $rule['active']): ?> <span class="label label-default">disabled</span><?php endif; ?></td>
				<td><?php echo epc_erp_h($entityTypes[$rule['entity_type']] ?? $rule['entity_type']); ?></td>
				<td><?php echo $rule['operator'] === 'any' ? 'Any amount' : ('amount ' . epc_erp_h($rule['operator']) . ' ' . number_format((float) $rule['threshold_amount'], 2)); ?></td>
				<td><?php echo epc_erp_h(implode(' → ', $stepNames)); ?></td>
				<td><?php echo (int) $rule['priority']; ?></td>
				<td style="white-space:nowrap;">
					<button type="button" class="btn btn-xs btn-primary epc-wf-edit" data-rule='<?php echo epc_erp_h(json_encode($ruleData)); ?>'>Edit</button>
					<?php if ((int) $rule['active']): ?>
					<form data-bos-action="bos_wf_disable_rule" style="display:inline;" onsubmit="return confirm('Disable rule?');">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="id" value="<?php echo (int) $rule['id']; ?>">
						<button class="btn btn-xs btn-default" type="submit">Disable</button>
					</form>
					<?php else: ?>
					<form data-bos-action="bos_wf_save_rule" style="display:inline;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="rule_id" value="<?php echo (int) $rule['id']; ?>">
						<input type="hidden" name="name" value="<?php echo epc_erp_h($rule['name']); ?>">
						<input type="hidden" name="entity_type" value="<?php echo epc_erp_h($rule['entity_type']); ?>">
						<input type="hidden" name="operator" value="<?php echo epc_erp_h($rule['operator']); ?>">
						<input type="hidden" name="threshold_amount" value="<?php echo (float) $rule['threshold_amount']; ?>">
						<input type="hidden" name="priority" value="<?php echo (int) $rule['priority']; ?>">
						<?php foreach ($steps as $s): ?><input type="hidden" name="step_role[]" value="<?php echo epc_erp_h($s['role'] ?? ''); ?>"><input type="hidden" name="step_label[]" value="<?php echo epc_erp_h($s['label'] ?? ''); ?>"><?php endforeach; ?>
						<button class="btn btn-xs btn-success" type="submit">Enable</button>
					</form>
					<?php endif; ?>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($rules)): ?><tr><td colspan="6" class="text-muted">No rules defined.</td></tr><?php endif; ?>
		</tbody>
	</table>
	<script>
	(function(){
		var stepsWrap = document.getElementById('epc_wf_steps');
		function stepRow(role, label){
			var d = document.createElement('div');
			d.className = 'form-group';
			d.style.cssText = 'display:flex;gap:8px;margin-bottom:6px;';
			d.innerHTML = '<input type="text" name="step_role[]" class="form-control input-sm" placeholder="Approver role" style="max-width:240px;">'
				+ '<input type="text" name="step_label[]" class="form-control input-sm" placeholder="Step label (optional)" style="max-width:240px;">'
				+ '<button type="button" class="btn btn-xs btn-default epc-wf-rm-step" title="Remove step">&times;</button>';
			d.querySelectorAll('input')[0].value = role || '';
			d.querySelectorAll('input')[1].value = label || '';
			return d;
		}
		document.getElementById('epc_wf_add_step').addEventListener('click', function(){ stepsWrap.appendChild(stepRow('', '')); });
		stepsWrap.addEventListener('click', function(e){
			if (e.target.classList.contains('epc-wf-rm-step')) {
				if (stepsWrap.children.length > 1) { e.target.closest('.form-group').remove(); }
			}
		});
		function resetForm(){
			document.getElementById('epc_wf_rule_id').value = '';
			document.getElementById('epc_wf_form_title').textContent = 'Add approval rule';
			document.getElementById('epc_wf_submit').textContent = 'Add rule';
			document.getElementById('epc_wf_cancel').style.display = 'none';
		}
		document.getElementById('epc_wf_cancel').addEventListener('click', function(){
			document.getElementById('epc_wf_rule_form').reset();
			stepsWrap.innerHTML = ''; stepsWrap.appendChild(stepRow('Manager', ''));
			resetForm();
		});
		document.querySelectorAll('.epc-wf-edit').forEach(function(btn){
			btn.addEventListener('click', function(){
				var r = JSON.parse(btn.getAttribute('data-rule'));
				document.getElementById('epc_wf_rule_id').value = r.id;
				document.getElementById('epc_wf_name').value = r.name;
				document.getElementById('epc_wf_entity').value = r.entity_type;
				document.getElementById('epc_wf_operator').value = r.operator;
				document.getElementById('epc_wf_threshold').value = r.threshold_amount;
				document.getElementById('epc_wf_priority').value = r.priority;
				stepsWrap.innerHTML = '';
				(r.steps && r.steps.length ? r.steps : [{role:'Manager',label:''}]).forEach(function(s){ stepsWrap.appendChild(stepRow(s.role, s.label)); });
				document.getElementById('epc_wf_form_title').textContent = 'Edit approval rule — ' + r.name;
				document.getElementById('epc_wf_submit').textContent = 'Save changes';
				document.getElementById('epc_wf_cancel').style.display = '';
				document.getElementById('epc_wf_rule_form').scrollIntoView({behavior:'smooth', block:'center'});
			});
		});
	})();
	</script>

<?php elseif ($apanel === 'test'): ?>
	<p class="text-muted">Raise a test approval request to verify a rule matches and routes correctly. This creates a real request in the queue.</p>
	<form class="form-inline epc-erp-form-inline" data-bos-action="bos_wf_raise_test">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<select name="entity_type" class="form-control input-sm">
			<?php foreach ($entityTypes as $k => $v): ?><option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($v); ?></option><?php endforeach; ?>
		</select>
		<input type="text" name="entity_ref" class="form-control input-sm" placeholder="Reference (e.g. PO-TEST-1)" value="TEST-<?php echo date('His'); ?>">
		<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount" value="15000">
		<button class="btn btn-sm btn-warning" type="submit">Raise test request</button>
	</form>

<?php else:
	$all = epc_bos_wf_requests($db_link, '', 80); ?>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Document</th><th>Type</th><th>Amount</th><th>Status</th><th>Decided</th><th>Audit trail</th></tr></thead>
		<tbody>
		<?php foreach ($all as $r): $log = epc_bos_wf_request_log($db_link, (int) $r['id']); $sc = $r['status'] === 'approved' ? 'success' : ($r['status'] === 'rejected' ? 'danger' : ($r['status'] === 'pending' ? 'warning' : 'default')); ?>
			<tr>
				<td><strong><?php echo epc_erp_h($r['title']); ?></strong><br><small class="text-muted"><?php echo epc_erp_h($r['entity_ref']); ?></small></td>
				<td><?php echo epc_erp_h($entityTypes[$r['entity_type']] ?? $r['entity_type']); ?></td>
				<td><?php echo number_format((float) $r['amount'], 2); ?></td>
				<td><span class="label label-<?php echo $sc; ?>"><?php echo epc_erp_h($r['status']); ?></span></td>
				<td><small><?php echo (int) $r['decided_at'] ? epc_erp_h(date('d M Y', (int) $r['decided_at'])) : '—'; ?></small></td>
				<td>
					<?php foreach ($log as $l): ?>
						<small><i class="fa fa-angle-right"></i> <strong><?php echo epc_erp_h($l['action']); ?></strong>
						<?php echo $l['actor_name'] ? ' by ' . epc_erp_h($l['actor_name']) : ''; ?>
						<?php echo $l['comment'] ? ' — ' . epc_erp_h($l['comment']) : ''; ?>
						<span class="text-muted">(<?php echo epc_erp_h(date('d M H:i', (int) $l['time'])); ?>)</span></small><br>
					<?php endforeach; ?>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($all)): ?><tr><td colspan="6" class="text-muted">No approval requests yet.</td></tr><?php endif; ?>
		</tbody>
	</table>
<?php endif; ?>
</div>
