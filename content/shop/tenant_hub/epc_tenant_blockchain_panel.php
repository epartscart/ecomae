<?php
/**
 * Super CP Tenant Hub — Blockchain proofs fleet panel.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';

$hubUrl = isset($hubUrl) ? (string)$hubUrl : '';
$tenants = isset($tenants) && is_array($tenants) ? $tenants : array();

$filterTenant = isset($_GET['bc_tenant']) ? strtolower(preg_replace('/[^a-z0-9_]/', '', (string)$_GET['bc_tenant']) ?: '') : '';
$filterType = isset($_GET['bc_type']) ? strtolower(trim((string)$_GET['bc_type'])) : '';
$filterStatus = isset($_GET['bc_status']) ? strtolower(trim((string)$_GET['bc_status'])) : '';
$allowedTypes = array('invoice', 'credit_note', 'grn', 'rma');
if ($filterType !== '' && !in_array($filterType, $allowedTypes, true)) {
	$filterType = '';
}
if ($filterStatus !== '' && !in_array($filterStatus, array('pending', 'anchored'), true)) {
	$filterStatus = '';
}

$stats = epc_bc_bos_fleet_stats();
$rows = epc_bc_bos_list_proofs_fleet(array(
	'tenant_key' => $filterTenant,
	'record_type' => $filterType,
	'status' => $filterStatus,
	'limit' => 150,
));
?>
<div class="panel panel-default epc-th-panel">
	<div class="panel-heading">
		<i class="fa fa-link"></i> Blockchain BOS proofs — fleet
		<span class="text-muted" style="font-weight:normal;margin-left:8px">Platform-wide cryptographic integrity proofs</span>
	</div>
	<div class="panel-body">
		<p class="text-muted" style="margin-top:0">
			Proofs are recorded when tenants create validated invoices, credit notes, GRNs and RMAs.
			Public verify: <a href="/epc-blockchain-verify.php" target="_blank" rel="noopener"><code>/epc-blockchain-verify.php</code></a>
		</p>

		<div class="row" style="margin-bottom:16px">
			<div class="col-sm-3"><div class="well well-sm" style="margin:0"><div class="text-muted small">Total proofs</div><strong style="font-size:22px"><?php echo (int)$stats['total']; ?></strong></div></div>
			<div class="col-sm-3"><div class="well well-sm" style="margin:0"><div class="text-muted small">Anchored</div><strong style="font-size:22px;color:#16a34a"><?php echo (int)$stats['anchored']; ?></strong></div></div>
			<div class="col-sm-3"><div class="well well-sm" style="margin:0"><div class="text-muted small">Pending</div><strong style="font-size:22px;color:#d97706"><?php echo (int)$stats['pending']; ?></strong></div></div>
			<div class="col-sm-3"><div class="well well-sm" style="margin:0"><div class="text-muted small">Tenants with proofs</div><strong style="font-size:22px"><?php echo (int)$stats['tenants']; ?></strong></div></div>
		</div>

		<form method="get" class="form-inline" style="margin-bottom:14px">
			<input type="hidden" name="tab" value="blockchain">
			<label>Tenant</label>
			<select name="bc_tenant" class="form-control input-sm">
				<option value="">All tenants</option>
				<?php foreach ($tenants as $t):
					$sk = (string)($t['site_key'] ?? '');
					if ($sk === '') {
						continue;
					}
					$sel = $filterTenant === $sk ? ' selected' : '';
					$label = $sk;
					if (!empty($t['trade_name'])) {
						$label .= ' — ' . (string)$t['trade_name'];
					}
					?>
				<option value="<?php echo epc_th_h($sk); ?>"<?php echo $sel; ?>><?php echo epc_th_h($label); ?></option>
				<?php endforeach; ?>
			</select>
			<label style="margin-left:8px">Type</label>
			<select name="bc_type" class="form-control input-sm">
				<option value="">All</option>
				<?php foreach ($allowedTypes as $ty): ?>
				<option value="<?php echo epc_th_h($ty); ?>"<?php echo $filterType === $ty ? ' selected' : ''; ?>><?php echo epc_th_h($ty); ?></option>
				<?php endforeach; ?>
			</select>
			<label style="margin-left:8px">Status</label>
			<select name="bc_status" class="form-control input-sm">
				<option value="">All</option>
				<option value="pending"<?php echo $filterStatus === 'pending' ? ' selected' : ''; ?>>pending</option>
				<option value="anchored"<?php echo $filterStatus === 'anchored' ? ' selected' : ''; ?>>anchored</option>
			</select>
			<button type="submit" class="btn btn-primary btn-sm" style="margin-left:8px"><i class="fa fa-filter"></i> Filter</button>
			<?php if ($filterTenant !== '' || $filterType !== '' || $filterStatus !== ''): ?>
			<a class="btn btn-default btn-sm" href="<?php echo epc_th_h($hubUrl); ?>?tab=blockchain">Clear</a>
			<?php endif; ?>
		</form>

		<?php if (empty($rows)): ?>
			<div class="alert alert-info" style="margin-bottom:0">No proofs match this filter yet. Create a validated invoice on a tenant with <code>blockchain_mode=anchor</code>.</div>
		<?php else: ?>
		<div class="epc-th-table-wrap" style="overflow-x:auto">
			<table class="table table-striped table-bordered table-condensed epc-th-table" style="font-size:12px;margin:0">
				<thead>
					<tr>
						<th>When</th>
						<th>Tenant</th>
						<th>Type</th>
						<th>Record</th>
						<th>Status</th>
						<th>Proof ID</th>
						<th>Verify</th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($rows as $r):
					$status = (string)($r['status'] ?? 'pending');
					$tone = $status === 'anchored' ? 'success' : 'warning';
					$uid = (string)($r['proof_uid'] ?? '');
					$verify = $uid !== '' ? epc_bc_bos_verify_url($uid) : '';
					?>
					<tr>
						<td><?php echo epc_th_h((string)($r['created_at'] ?? '')); ?></td>
						<td><code><?php echo epc_th_h((string)($r['tenant_key'] ?? '')); ?></code></td>
						<td><code><?php echo epc_th_h((string)($r['record_type'] ?? '')); ?></code></td>
						<td><?php echo epc_th_h((string)($r['record_id'] ?? '')); ?></td>
						<td><span class="label label-<?php echo $tone; ?>"><?php echo epc_th_h($status); ?></span></td>
						<td><small><code><?php echo epc_th_h($uid); ?></code></small></td>
						<td>
							<?php if ($verify !== ''): ?>
							<a class="btn btn-default btn-xs" href="<?php echo epc_th_h($verify); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Verify</a>
							<?php else: ?>
							<span class="text-muted">—</span>
							<?php endif; ?>
						</td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>
		<p class="text-muted small" style="margin:10px 0 0">Showing <?php echo count($rows); ?> most recent. Anchoring runs via <code>blockchain_anchor_batch</code> cron.</p>
		<?php endif; ?>
	</div>
</div>
