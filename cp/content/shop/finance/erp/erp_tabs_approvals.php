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
	<form class="epc-erp-form-inline" data-bos-action="bos_wf_save_rule" style="margin-bottom:14px;background:#f7f9fb;padding:12px;border-radius:6px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<div class="form-group"><input type="text" name="name" class="form-control input-sm" placeholder="Rule name" required></div>
		<div class="form-group">
			<select name="entity_type" class="form-control input-sm">
				<?php foreach ($entityTypes as $k => $v): ?><option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($v); ?></option><?php endforeach; ?>
			</select>
		</div>
		<div class="form-group">
			<select name="operator" class="form-control input-sm">
				<option value=">=">amount ≥</option>
				<option value=">">amount &gt;</option>
				<option value="<=">amount ≤</option>
				<option value="any">any amount</option>
			</select>
		</div>
		<div class="form-group"><input type="number" step="0.01" name="threshold_amount" class="form-control input-sm" placeholder="Threshold" value="10000"></div>
		<div class="form-group"><input type="text" name="step_role[]" class="form-control input-sm" placeholder="Approver role 1" value="Manager"></div>
		<div class="form-group"><input type="text" name="step_role[]" class="form-control input-sm" placeholder="Approver role 2 (optional)"></div>
		<button class="btn btn-sm btn-primary" type="submit">Add rule</button>
	</form>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Rule</th><th>Document type</th><th>Condition</th><th>Approval steps</th><th></th></tr></thead>
		<tbody>
		<?php foreach ($rules as $rule): $steps = epc_bos_wf_decode_steps($rule['steps_json']); $stepNames = array_map(function ($s) { return $s['role'] ?? 'Approver'; }, $steps); ?>
			<tr class="<?php echo (int) $rule['active'] ? '' : 'text-muted'; ?>">
				<td><strong><?php echo epc_erp_h($rule['name']); ?></strong></td>
				<td><?php echo epc_erp_h($entityTypes[$rule['entity_type']] ?? $rule['entity_type']); ?></td>
				<td><?php echo $rule['operator'] === 'any' ? 'Any amount' : ('amount ' . epc_erp_h($rule['operator']) . ' ' . number_format((float) $rule['threshold_amount'], 2)); ?></td>
				<td><?php echo epc_erp_h(implode(' → ', $stepNames)); ?></td>
				<td>
					<?php if ((int) $rule['active']): ?>
					<form data-bos-action="bos_wf_disable_rule" style="display:inline;" onsubmit="return confirm('Disable rule?');">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
						<input type="hidden" name="id" value="<?php echo (int) $rule['id']; ?>">
						<button class="btn btn-xs btn-default" type="submit">Disable</button>
					</form>
					<?php else: ?><span class="label label-default">disabled</span><?php endif; ?>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($rules)): ?><tr><td colspan="5" class="text-muted">No rules defined.</td></tr><?php endif; ?>
		</tbody>
	</table>

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
