<?php
/**
 * Super CP Tenant Hub — Blockchain proofs fleet panel.
 * Includes per-tenant mode controls and manual Merkle anchor drain.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';

$hubUrl = isset($hubUrl) ? (string)$hubUrl : '';
$tenants = isset($tenants) && is_array($tenants) ? $tenants : array();
$modes = epc_bc_bos_modes();
$anchorNetwork = epc_bc_bos_anchor_network();

$filterTenant = isset($_GET['bc_tenant']) ? strtolower(preg_replace('/[^a-z0-9_]/', '', (string)$_GET['bc_tenant']) ?: '') : '';
$filterType = isset($_GET['bc_type']) ? strtolower(trim((string)$_GET['bc_type'])) : '';
$filterStatus = isset($_GET['bc_status']) ? strtolower(trim((string)$_GET['bc_status'])) : '';
$filterMode = '';
if (isset($_GET['bc_mode']) && trim((string)$_GET['bc_mode']) !== '') {
	$filterMode = epc_bc_bos_normalize_mode((string)$_GET['bc_mode']);
	if (!isset($modes[$filterMode])) {
		$filterMode = '';
	}
}
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

$modeCounts = array('anchor' => 0, 'network' => 0, 'off' => 0);
$tenantModeRows = array();
foreach ($tenants as $t) {
	$sk = (string)($t['site_key'] ?? '');
	if ($sk === '') {
		continue;
	}
	$m = epc_bc_bos_normalize_mode((string)($t['blockchain_mode'] ?? 'anchor'));
	if (!isset($modeCounts[$m])) {
		$modeCounts[$m] = 0;
	}
	$modeCounts[$m]++;
	if ($filterMode !== '' && $m !== $filterMode) {
		continue;
	}
	$tenantModeRows[] = array(
		'site_key' => $sk,
		'trade_name' => (string)($t['trade_name'] ?? ''),
		'status' => (string)($t['status'] ?? ''),
		'blockchain_mode' => $m,
	);
}
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
			· Anchor network: <code><?php echo epc_th_h($anchorNetwork); ?></code>
			<span class="text-muted">(env <code>EPC_BC_ANCHOR_NETWORK</code>)</span>
		</p>

		<div class="row" style="margin-bottom:16px">
			<div class="col-sm-3"><div class="well well-sm" style="margin:0"><div class="text-muted small">Total proofs</div><strong style="font-size:22px"><?php echo (int)$stats['total']; ?></strong></div></div>
			<div class="col-sm-3"><div class="well well-sm" style="margin:0"><div class="text-muted small">Anchored</div><strong style="font-size:22px;color:#16a34a"><?php echo (int)$stats['anchored']; ?></strong></div></div>
			<div class="col-sm-3"><div class="well well-sm" style="margin:0"><div class="text-muted small">Pending</div><strong style="font-size:22px;color:#d97706"><?php echo (int)$stats['pending']; ?></strong></div></div>
			<div class="col-sm-3"><div class="well well-sm" style="margin:0"><div class="text-muted small">Tenants with proofs</div><strong style="font-size:22px"><?php echo (int)$stats['tenants']; ?></strong></div></div>
		</div>

		<div class="well well-sm" style="margin-bottom:16px">
			<div class="row">
				<div class="col-sm-8">
					<strong><i class="fa fa-cog"></i> Fleet ops</strong>
					<p class="text-muted small" style="margin:6px 0 0">
						Cron drains pending proofs via <code>blockchain_anchor_batch</code>.
						Use <em>Anchor pending now</em> to Merkle-anchor up to 100 pending proofs immediately.
						<code>network</code> mode is roadmap — today it records and anchors like <code>anchor</code>.
					</p>
					<p class="small" style="margin:8px 0 0">
						Modes in fleet:
						<span class="label label-success">anchor <?php echo (int)$modeCounts['anchor']; ?></span>
						<span class="label label-info">network <?php echo (int)$modeCounts['network']; ?></span>
						<span class="label label-default">off <?php echo (int)$modeCounts['off']; ?></span>
					</p>
				</div>
				<div class="col-sm-4 text-right" style="padding-top:8px">
					<form method="post" style="display:inline">
						<input type="hidden" name="epc_th_bc_anchor_now" value="1">
						<button type="submit" class="btn btn-warning btn-sm"<?php echo ((int)$stats['pending'] < 1) ? ' disabled' : ''; ?>>
							<i class="fa fa-anchor"></i> Anchor pending now
							<?php if ((int)$stats['pending'] > 0): ?>
							(<?php echo (int)$stats['pending']; ?>)
							<?php endif; ?>
						</button>
					</form>
				</div>
			</div>
		</div>

		<h4 style="margin-top:0"><i class="fa fa-sliders"></i> Tenant blockchain modes</h4>
		<p class="text-muted small">Change mode without opening Onboard edit. Off skips all new proofs for that tenant.</p>
		<form method="get" class="form-inline" style="margin-bottom:10px">
			<input type="hidden" name="tab" value="blockchain">
			<label>Show mode</label>
			<select name="bc_mode" class="form-control input-sm" onchange="this.form.submit()">
				<option value="">All modes</option>
				<?php foreach ($modes as $mk => $ml): ?>
				<option value="<?php echo epc_th_h($mk); ?>"<?php echo $filterMode === $mk ? ' selected' : ''; ?>><?php echo epc_th_h($ml); ?></option>
				<?php endforeach; ?>
			</select>
			<?php if ($filterTenant !== ''): ?>
			<input type="hidden" name="bc_tenant" value="<?php echo epc_th_h($filterTenant); ?>">
			<?php endif; ?>
			<?php if ($filterType !== ''): ?>
			<input type="hidden" name="bc_type" value="<?php echo epc_th_h($filterType); ?>">
			<?php endif; ?>
			<?php if ($filterStatus !== ''): ?>
			<input type="hidden" name="bc_status" value="<?php echo epc_th_h($filterStatus); ?>">
			<?php endif; ?>
		</form>
		<div class="epc-th-table-wrap" style="overflow-x:auto;margin-bottom:22px">
			<table class="table table-striped table-bordered table-condensed epc-th-table" style="font-size:12px;margin:0">
				<thead>
					<tr>
						<th>Tenant</th>
						<th>Trade name</th>
						<th>Status</th>
						<th style="min-width:220px">Blockchain mode</th>
					</tr>
				</thead>
				<tbody>
				<?php if (empty($tenantModeRows)): ?>
					<tr><td colspan="4" class="text-muted">No tenants match this mode filter.</td></tr>
				<?php else: ?>
				<?php foreach ($tenantModeRows as $tm):
					$cur = $tm['blockchain_mode'];
					?>
					<tr>
						<td><code><?php echo epc_th_h($tm['site_key']); ?></code></td>
						<td><?php echo epc_th_h($tm['trade_name'] !== '' ? $tm['trade_name'] : '—'); ?></td>
						<td><span class="label label-default"><?php echo epc_th_h($tm['status']); ?></span></td>
						<td>
							<form method="post" class="form-inline" style="margin:0">
								<input type="hidden" name="epc_th_bc_mode" value="1">
								<input type="hidden" name="site_key" value="<?php echo epc_th_h($tm['site_key']); ?>">
								<select name="blockchain_mode" class="form-control input-sm" style="max-width:200px;display:inline-block" onchange="this.form.submit()" title="Change blockchain mode">
									<?php foreach ($modes as $mk => $ml): ?>
									<option value="<?php echo epc_th_h($mk); ?>"<?php echo $cur === $mk ? ' selected' : ''; ?>><?php echo epc_th_h($ml); ?></option>
									<?php endforeach; ?>
								</select>
							</form>
						</td>
					</tr>
				<?php endforeach; ?>
				<?php endif; ?>
				</tbody>
			</table>
		</div>

		<h4><i class="fa fa-list"></i> Recent proofs</h4>
		<form method="get" class="form-inline" style="margin-bottom:14px">
			<input type="hidden" name="tab" value="blockchain">
			<?php if ($filterMode !== ''): ?>
			<input type="hidden" name="bc_mode" value="<?php echo epc_th_h($filterMode); ?>">
			<?php endif; ?>
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
			<?php if ($filterTenant !== '' || $filterType !== '' || $filterStatus !== '' || $filterMode !== ''): ?>
			<a class="btn btn-default btn-sm" href="<?php echo epc_th_h($hubUrl); ?>?tab=blockchain">Clear</a>
			<?php endif; ?>
		</form>

		<?php if (empty($rows)): ?>
			<div class="alert alert-info" style="margin-bottom:0">No proofs match this filter yet. Create a validated invoice on a tenant with <code>blockchain_mode=anchor</code> (or network).</div>
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
		<p class="text-muted small" style="margin:10px 0 0">Showing <?php echo count($rows); ?> most recent. Anchoring runs via cron or <em>Anchor pending now</em>.</p>
		<?php endif; ?>
	</div>
</div>
