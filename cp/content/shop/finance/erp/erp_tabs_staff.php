<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';

$staffDash = epc_erp_staff_dashboard($db_link);
$staffList = epc_erp_staff_list($db_link);
$deptCfg = epc_erp_departments_config();
$userDept = epc_erp_staff_primary_department($db_link);
?>

<div class="epc-erp-section">
	<h4><i class="fa fa-users"></i> Staff &amp; departments</h4>
	<p class="text-muted">Each department has its own ERP tabs and workflow queue. Dummy users are seeded for training — rename in CP Users or re-run staff setup.</p>

	<div class="epc-erp-kpi" style="margin-bottom:16px;">
		<div class="kpi"><div class="lbl">Active staff</div><div class="val"><?php echo (int)$staffDash['staff_count']; ?></div></div>
		<div class="kpi"><div class="lbl">Open workflow tasks</div><div class="val"><?php echo (int)$staffDash['tasks_open']; ?></div></div>
		<div class="kpi"><div class="lbl">Active campaigns</div><div class="val"><?php echo (int)$staffDash['active_campaigns']; ?></div></div>
		<div class="kpi"><div class="lbl">Departments</div><div class="val"><?php echo count($staffDash['departments']); ?></div></div>
	</div>

	<?php if ($userDept !== ''): ?>
	<div class="alert alert-success">
		You are signed in as <strong><?php echo epc_erp_h(epc_erp_staff_department_name($userDept)); ?></strong> staff.
		Your allowed ERP tabs are filtered to your department (admins see all).
	</div>
	<?php endif; ?>

	<h5>Department map</h5>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Department</th><th>ERP tabs</th><th>Open tasks</th><th>Standard workflow</th></tr></thead>
		<tbody>
		<?php foreach ($staffDash['departments'] as $d): ?>
			<?php
			$code = $d['code'];
			$row = isset($deptCfg[$code]) ? $deptCfg[$code] : null;
			$tabs = json_decode($d['tabs_json'], true);
			$flows = json_decode($d['workflows_json'], true);
			$tabLabel = is_array($tabs) && in_array('*', $tabs, true) ? 'All modules' : (is_array($tabs) ? implode(', ', $tabs) : '—');
			$open = isset($staffDash['tasks_by_department'][$code]) ? (int)$staffDash['tasks_by_department'][$code] : 0;
			?>
			<tr>
				<td>
					<?php if ($row): ?><i class="fa <?php echo epc_erp_h($row['icon']); ?>" style="color:<?php echo epc_erp_h($row['color']); ?>"></i><?php endif; ?>
					<strong><?php echo epc_erp_h($d['name']); ?></strong>
				</td>
				<td><small><?php echo epc_erp_h($tabLabel); ?></small></td>
				<td><?php echo $open; ?></td>
				<td><small><?php echo epc_erp_h(is_array($flows) ? implode(' → ', array_slice($flows, 0, 3)) . (count($flows) > 3 ? '…' : '') : '—'); ?></small></td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>

	<h5>Staff directory (dummy users)</h5>
	<table class="table table-striped table-condensed">
		<thead><tr><th>Name</th><th>Department</th><th>Job title</th><th>Login e-mail</th><th>User ID</th></tr></thead>
		<tbody>
		<?php foreach ($staffList as $s): ?>
			<tr>
				<td><?php echo epc_erp_h($s['display_name']); ?></td>
				<td><?php echo epc_erp_h(epc_erp_staff_department_name($s['department_code'])); ?></td>
				<td><?php echo epc_erp_h($s['job_title']); ?></td>
				<td><code><?php echo epc_erp_h($s['email'] ?: $s['user_email']); ?></code></td>
				<td><?php echo (int)$s['user_id']; ?></td>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($staffList)): ?>
			<tr><td colspan="5" class="text-muted">No staff — run <code>epc-erp-staff-setup.php?token=...&amp;sample=1</code></td></tr>
		<?php endif; ?>
		</tbody>
	</table>
	<p class="text-muted">Default password for dummy accounts: <code>EpcStaff2026!</code> — change after first login.</p>
</div>
