<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_dashboard_profiles.php';

$staffDash = epc_erp_staff_dashboard($db_link);
$staffList = epc_erp_staff_list($db_link);
$deptCfg = epc_erp_departments_config();
$dashProfiles = epc_erp_dashboard_profiles_config();
$userDept = epc_erp_staff_primary_department($db_link);
$canEditDash = function_exists('epc_erp_staff_user_is_full_admin') && epc_erp_staff_user_is_full_admin($db_link);
$dashMsg = '';

if ($canEditDash && ($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && isset($_POST['epc_dash_profile_save'])) {
	$csrfOk = true;
	if (function_exists('epc_erp_csrf_ok')) {
		$csrfOk = epc_erp_csrf_ok($_POST['csrf_guard_key'] ?? '');
	} elseif (!empty($csrf) && isset($_POST['csrf_guard_key'])) {
		$csrfOk = hash_equals((string) $csrf, (string) $_POST['csrf_guard_key']);
	}
	if ($csrfOk) {
		$uid = (int) ($_POST['staff_user_id'] ?? 0);
		$prof = (string) ($_POST['dashboard_profile'] ?? '');
		if ($uid > 0 && epc_erp_dashboard_profile_set($db_link, $uid, $prof)) {
			$dashMsg = 'Dashboard centre updated.';
			$staffList = epc_erp_staff_list($db_link);
		} else {
			$dashMsg = 'Could not update dashboard centre.';
		}
	} else {
		$dashMsg = 'Security check failed — reload and try again.';
	}
}

$myDash = epc_erp_dashboard_profile_meta($db_link);
?>

<div class="epc-erp-section">
	<h4><i class="fa fa-users"></i> Staff &amp; departments</h4>
	<p class="text-muted">Each department has its own ERP tabs and workflow queue. Dashboard centres (CEO, CFO, Sales, …) are controlled by staff profile — sales cannot see profit.</p>

	<div class="epc-erp-kpi" style="margin-bottom:16px;">
		<div class="kpi"><div class="lbl">Active staff</div><div class="val"><?php echo (int)$staffDash['staff_count']; ?></div></div>
		<div class="kpi"><div class="lbl">Open workflow tasks</div><div class="val"><?php echo (int)$staffDash['tasks_open']; ?></div></div>
		<div class="kpi"><div class="lbl">Active campaigns</div><div class="val"><?php echo (int)$staffDash['active_campaigns']; ?></div></div>
		<div class="kpi"><div class="lbl">Departments</div><div class="val"><?php echo count($staffDash['departments']); ?></div></div>
	</div>

	<?php if ($userDept !== ''): ?>
	<div class="alert alert-success">
		You are signed in as <strong><?php echo epc_erp_h(epc_erp_staff_department_name($userDept)); ?></strong> staff.
		Your home dashboard is <strong><?php echo epc_erp_h($myDash['label'] ?? 'Finance centre'); ?></strong>
		<?php if (!epc_erp_dashboard_can($myDash, 'profit')): ?>
			— profit &amp; margin are hidden for this profile.
		<?php endif; ?>
		Your allowed ERP tabs are filtered to your department (admins see all).
	</div>
	<?php endif; ?>

	<?php if ($dashMsg !== ''): ?>
	<div class="alert alert-info"><?php echo epc_erp_h($dashMsg); ?></div>
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

	<h5>Staff directory &amp; dashboard centres</h5>
	<p class="text-muted" style="margin-top:0;">Dashboard centre is resolved from: explicit override → job title (CEO/CFO) → department default. Example: Sales Executive never sees profit.</p>
	<table class="table table-striped table-condensed">
		<thead>
			<tr>
				<th>Name</th>
				<th>Department</th>
				<th>Job title</th>
				<th>Dashboard centre</th>
				<th>Login e-mail</th>
				<?php if ($canEditDash): ?><th>Set centre</th><?php endif; ?>
			</tr>
		</thead>
		<tbody>
		<?php foreach ($staffList as $s):
			$resolved = epc_erp_dashboard_resolve_profile($db_link, (int) $s['user_id']);
			$resolvedLabel = isset($dashProfiles[$resolved]['label']) ? $dashProfiles[$resolved]['label'] : $resolved;
			$explicit = (string) ($s['dashboard_profile'] ?? '');
			?>
			<tr>
				<td><?php echo epc_erp_h($s['display_name']); ?></td>
				<td><?php echo epc_erp_h(epc_erp_staff_department_name($s['department_code'])); ?></td>
				<td><?php echo epc_erp_h($s['job_title']); ?></td>
				<td>
					<strong><?php echo epc_erp_h($resolvedLabel); ?></strong>
					<?php if ($explicit !== ''): ?>
						<br><small class="text-muted">override: <?php echo epc_erp_h($explicit); ?></small>
					<?php endif; ?>
				</td>
				<td><code><?php echo epc_erp_h($s['email'] ?: $s['user_email']); ?></code></td>
				<?php if ($canEditDash): ?>
				<td style="min-width:190px;">
					<form method="post" class="form-inline" style="margin:0;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h(isset($csrf) ? $csrf : ''); ?>">
						<input type="hidden" name="epc_dash_profile_save" value="1">
						<input type="hidden" name="staff_user_id" value="<?php echo (int) $s['user_id']; ?>">
						<select name="dashboard_profile" class="form-control input-sm" style="max-width:140px;display:inline-block;">
							<option value="">Auto (dept/title)</option>
							<?php foreach ($dashProfiles as $pkey => $pmeta): ?>
								<option value="<?php echo epc_erp_h($pkey); ?>"<?php echo ($explicit === $pkey) ? ' selected' : ''; ?>><?php echo epc_erp_h($pmeta['label']); ?></option>
							<?php endforeach; ?>
						</select>
						<button type="submit" class="btn btn-xs btn-primary">Save</button>
					</form>
				</td>
				<?php endif; ?>
			</tr>
		<?php endforeach; ?>
		<?php if (empty($staffList)): ?>
			<tr><td colspan="<?php echo $canEditDash ? 6 : 5; ?>" class="text-muted">No staff — run <code>epc-erp-staff-setup.php?token=...&amp;sample=1</code></td></tr>
		<?php endif; ?>
		</tbody>
	</table>
	<p class="text-muted">Default password for dummy accounts: <code>EpcStaff2026!</code> — change after first login. Demo CEO/CFO: <code>erp.ceo@…</code> / <code>erp.cfo@…</code>.</p>
</div>
