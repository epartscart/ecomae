<?php
/**
 * Super CP — Communication setup (email notifications + internal tasks).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_super_cp_platform.php';

if (!epc_scp_guard_super_admin()) {
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : (function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null);
if (!$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

$flash = '';
$flashClass = 'info';
$editTaskId = max(0, (int) ($_GET['edit_task'] ?? 0));
$editTask = null;
$taskFilter = isset($_GET['task_status']) ? (string) $_GET['task_status'] : '';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_scp_action'])) {
	$action = (string) $_POST['epc_scp_action'];
	if ($action === 'save_comm_settings') {
		epc_scp_comm_settings_save($pdo, $_POST);
		$flash = 'Notification settings saved.';
		$flashClass = 'success';
	}
	if ($action === 'save_task') {
		$createdBy = class_exists('DP_User') ? (int) DP_User::getAdminId() : 0;
		$taskPost = $_POST;
		$dueRaw = trim((string) ($taskPost['due_at'] ?? ''));
		if ($dueRaw !== '' && !ctype_digit($dueRaw)) {
			$ts = strtotime($dueRaw);
			$taskPost['due_at'] = $ts !== false ? $ts : 0;
		}
		$res = epc_scp_task_save($pdo, $taskPost, max(0, (int) ($_POST['id'] ?? 0)), $createdBy);
		if (!empty($res['ok'])) {
			$flash = 'Task saved.';
			$flashClass = 'success';
			$editTaskId = 0;
		} else {
			$flash = (string) ($res['message'] ?? 'Save failed');
			$flashClass = 'danger';
		}
	}
	if ($action === 'delete_task') {
		epc_scp_task_delete($pdo, max(0, (int) ($_POST['id'] ?? 0)));
		$flash = 'Task deleted.';
		$flashClass = 'success';
		$editTaskId = 0;
	}
}

$comm = epc_scp_comm_settings_get($pdo);
$tasks = epc_scp_tasks_list($pdo, $taskFilter);
$tenants = epc_scp_tenant_options($pdo);
$platformUsers = epc_scp_platform_users($pdo);
$categories = epc_scp_task_categories();
$statuses = epc_scp_task_statuses();
$priorities = epc_scp_task_priorities();
$backend = epc_scp_backend();

if ($editTaskId > 0) {
	foreach ($tasks as $t) {
		if ((int) $t['id'] === $editTaskId) {
			$editTask = $t;
			break;
		}
	}
}

$smtpDiag = array('source' => 'n/a', 'host' => '', 'port' => '', 'encryption' => '', 'from_name' => '', 'from_email' => '');
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_common.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_common.php';
	if (function_exists('epc_auth_smtp_diagnose')) {
		$smtpDiag = epc_auth_smtp_diagnose();
	}
}
?>
<div class="col-lg-12 epc-scp-panel epc-scp-communication">
<?php
epc_scp_render_hero(
	'Super CP',
	'Communication setup',
	'Platform email notification policy, SMTP diagnostics, and internal operator tasks with assignment.',
	array(
		array('label' => 'Modern auth', 'icon' => 'fa-sign-in', 'url' => '/' . $backend . '/control/portal/epc_cp_auth_settings'),
		array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => epc_scp_operator_guide_url(), 'primary' => true),
	)
);
?>
<?php epc_scp_render_workspace_intro('communication'); ?>

<?php if ($flash !== '') { ?>
<div class="alert alert-<?php echo epc_scp_h($flashClass); ?>"><?php echo epc_scp_h($flash); ?></div>
<?php } ?>

<div class="row">
	<div class="col-md-6">
		<div class="epc-scp-form-card">
			<h4><i class="fa fa-envelope"></i> Email notifications</h4>
			<p class="text-muted small">Controls which platform events trigger outbound email. SMTP transport is configured in <code>config.epc-smtp.php</code> (see Modern auth).</p>
			<table class="table table-condensed table-bordered epc-scp-status-table" style="margin-bottom:14px">
				<tr><th>SMTP source</th><td><?php echo epc_scp_h($smtpDiag['source'] ?? ''); ?></td></tr>
				<tr><th>Host</th><td><code><?php echo epc_scp_h(($smtpDiag['host'] ?? '') . ':' . ($smtpDiag['port'] ?? '')); ?></code> (<?php echo epc_scp_h($smtpDiag['encryption'] ?? ''); ?>)</td></tr>
				<tr><th>From</th><td><?php echo epc_scp_h($smtpDiag['from_name'] ?? ''); ?> &lt;<?php echo epc_scp_h($smtpDiag['from_email'] ?? ''); ?>&gt;</td></tr>
			</table>
			<form method="post">
				<input type="hidden" name="epc_scp_action" value="save_comm_settings" />
				<div class="form-group"><label>From name</label><input class="form-control input-sm" name="notify_from_name" value="<?php echo epc_scp_h($comm['notify_from_name'] ?? ''); ?>" /></div>
				<div class="form-group"><label>From email</label><input class="form-control input-sm" type="email" name="notify_from_email" value="<?php echo epc_scp_h($comm['notify_from_email'] ?? ''); ?>" /></div>
				<div class="form-group"><label>Reply-to</label><input class="form-control input-sm" type="email" name="notify_reply_to" value="<?php echo epc_scp_h($comm['notify_reply_to'] ?? ''); ?>" /></div>
				<div class="form-group"><label>Digest hour (UTC)</label><input class="form-control input-sm" type="number" min="0" max="23" name="digest_hour_utc" value="<?php echo epc_scp_h($comm['digest_hour_utc'] ?? '6'); ?>" /></div>
				<div class="checkbox"><label><input type="checkbox" name="notify_tenant_onboard" value="1"<?php echo !empty($comm['notify_tenant_onboard']) ? ' checked' : ''; ?>> Tenant onboard confirmation</label></div>
				<div class="checkbox"><label><input type="checkbox" name="notify_tenant_dns_live" value="1"<?php echo !empty($comm['notify_tenant_dns_live']) ? ' checked' : ''; ?>> Tenant goes live (DNS)</label></div>
				<div class="checkbox"><label><input type="checkbox" name="notify_demo_expiry" value="1"<?php echo !empty($comm['notify_demo_expiry']) ? ' checked' : ''; ?>> Demo expiry reminder</label></div>
				<div class="checkbox"><label><input type="checkbox" name="notify_task_assigned" value="1"<?php echo !empty($comm['notify_task_assigned']) ? ' checked' : ''; ?>> Task assigned to operator</label></div>
				<div class="checkbox"><label><input type="checkbox" name="notify_daily_digest" value="1"<?php echo !empty($comm['notify_daily_digest']) ? ' checked' : ''; ?>> Daily operator digest</label></div>
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save notification policy</button>
			</form>
		</div>
	</div>
	<div class="col-md-6">
		<div class="epc-scp-form-card">
			<h4><i class="fa fa-<?php echo $editTask ? 'edit' : 'plus'; ?>"></i> <?php echo $editTask ? 'Edit task' : 'New internal task'; ?></h4>
			<form method="post">
				<input type="hidden" name="epc_scp_action" value="save_task" />
				<input type="hidden" name="id" value="<?php echo (int) ($editTask['id'] ?? 0); ?>" />
				<div class="form-group"><label>Title</label><input class="form-control input-sm" name="title" required value="<?php echo epc_scp_h($editTask['title'] ?? ''); ?>" /></div>
				<div class="form-group"><label>Description</label><textarea class="form-control input-sm" name="description" rows="3"><?php echo epc_scp_h($editTask['description'] ?? ''); ?></textarea></div>
				<div class="row">
					<div class="col-sm-6"><div class="form-group"><label>Category</label>
						<select class="form-control input-sm" name="category">
							<?php foreach ($categories as $k => $label) { ?>
							<option value="<?php echo epc_scp_h($k); ?>"<?php echo ($editTask['category'] ?? 'support') === $k ? ' selected' : ''; ?>><?php echo epc_scp_h($label); ?></option>
							<?php } ?>
						</select>
					</div></div>
					<div class="col-sm-6"><div class="form-group"><label>Priority</label>
						<select class="form-control input-sm" name="priority">
							<?php foreach ($priorities as $k => $label) { ?>
							<option value="<?php echo epc_scp_h($k); ?>"<?php echo ($editTask['priority'] ?? 'normal') === $k ? ' selected' : ''; ?>><?php echo epc_scp_h($label); ?></option>
							<?php } ?>
						</select>
					</div></div>
				</div>
				<div class="row">
					<div class="col-sm-6"><div class="form-group"><label>Status</label>
						<select class="form-control input-sm" name="status">
							<?php foreach ($statuses as $k => $label) { ?>
							<option value="<?php echo epc_scp_h($k); ?>"<?php echo ($editTask['status'] ?? 'open') === $k ? ' selected' : ''; ?>><?php echo epc_scp_h($label); ?></option>
							<?php } ?>
						</select>
					</div></div>
					<div class="col-sm-6"><div class="form-group"><label>Due date</label>
						<input class="form-control input-sm" type="date" name="due_at" value="<?php echo !empty($editTask['due_at']) ? epc_scp_h(date('Y-m-d', (int) $editTask['due_at'])) : ''; ?>" />
					</div></div>
				</div>
				<div class="form-group"><label>Assign to (platform user)</label>
					<select class="form-control input-sm" name="assigned_to">
						<option value="0">— Unassigned —</option>
						<?php foreach ($platformUsers as $u) {
							$label = trim((string) ($u['fname'] ?? ''));
							if ($label === '') {
								$label = (string) ($u['email'] ?? '');
							}
							?>
						<option value="<?php echo (int) $u['user_id']; ?>"<?php echo (int) ($editTask['assigned_to'] ?? 0) === (int) $u['user_id'] ? ' selected' : ''; ?>><?php echo epc_scp_h($label); ?></option>
						<?php } ?>
					</select>
				</div>
				<div class="form-group"><label>Assign email (fallback)</label><input class="form-control input-sm" type="email" name="assigned_email" value="<?php echo epc_scp_h($editTask['assigned_email'] ?? ''); ?>" /></div>
				<div class="form-group"><label>Related tenant</label>
					<select class="form-control input-sm" name="site_key">
						<option value="">—</option>
						<?php foreach ($tenants as $t) { ?>
						<option value="<?php echo epc_scp_h($t['site_key']); ?>"<?php echo ($editTask['site_key'] ?? '') === $t['site_key'] ? ' selected' : ''; ?>><?php echo epc_scp_h($t['label']); ?></option>
						<?php } ?>
					</select>
				</div>
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save task</button>
				<?php if ($editTask) { ?><a class="btn btn-default btn-sm" href="<?php echo epc_scp_h('/' . $backend . '/control/portal/epc_super_cp_communication'); ?>">Cancel</a><?php } ?>
			</form>
		</div>
	</div>
</div>

<div class="epc-scp-table-card" style="margin-top:16px">
	<div class="epc-scp-filter-bar" style="margin-bottom:12px;border:none;padding:0;background:transparent">
		<form method="get" class="form-inline">
			<label>Tasks:</label>
			<select name="task_status" class="form-control input-sm">
				<option value="">All statuses</option>
				<?php foreach ($statuses as $k => $label) { ?>
				<option value="<?php echo epc_scp_h($k); ?>"<?php echo $taskFilter === $k ? ' selected' : ''; ?>><?php echo epc_scp_h($label); ?></option>
				<?php } ?>
			</select>
			<button type="submit" class="btn btn-default btn-sm">Filter</button>
		</form>
	</div>
	<h4><i class="fa fa-tasks"></i> Internal tasks (<?php echo count($tasks); ?>)</h4>
	<div class="table-responsive">
		<table class="table table-striped table-bordered table-condensed epc-scp-data-table">
			<thead><tr><th>Task</th><th>Category</th><th>Assignee</th><th>Status</th><th>Due</th><th></th></tr></thead>
			<tbody>
			<?php if (count($tasks) === 0) { ?>
				<tr><td colspan="6" class="epc-scp-empty-cell">
					<?php
					epc_scp_render_empty_state(
						'No internal tasks yet',
						'Track onboarding follow-ups, DNS go-live checks, or support escalations — assign to a platform user or email.',
						array(
							array('label' => 'Operator guide', 'icon' => 'fa-book', 'url' => epc_scp_operator_guide_url(), 'primary' => true),
							array('label' => 'Modern auth', 'icon' => 'fa-sign-in', 'url' => '/' . $backend . '/control/portal/epc_cp_auth_settings'),
						)
					);
					?>
				</td></tr>
			<?php } ?>
			<?php foreach ($tasks as $t) {
				$pBadge = 'epc-scp-badge--normal';
				if ($t['priority'] === 'urgent') {
					$pBadge = 'epc-scp-badge--urgent';
				} elseif ($t['priority'] === 'high') {
					$pBadge = 'epc-scp-badge--high';
				}
				?>
				<tr>
					<td>
						<strong><?php echo epc_scp_h($t['title']); ?></strong>
						<span class="epc-scp-badge <?php echo epc_scp_h($pBadge); ?>"><?php echo epc_scp_h($priorities[$t['priority']] ?? $t['priority']); ?></span>
						<?php if (!empty($t['description'])) { ?><div class="text-muted small"><?php echo epc_scp_h(substr(strip_tags($t['description']), 0, 120)); ?></div><?php } ?>
						<?php if (!empty($t['site_key'])) { ?><div><code><?php echo epc_scp_h($t['site_key']); ?></code></div><?php } ?>
					</td>
					<td><?php echo epc_scp_h($categories[$t['category']] ?? $t['category']); ?></td>
					<td><?php echo epc_scp_h($t['assigned_email'] !== '' ? $t['assigned_email'] : ('User #' . (int) $t['assigned_to'])); ?></td>
					<td><span class="label label-<?php echo $t['status'] === 'done' ? 'success' : ($t['status'] === 'cancelled' ? 'default' : 'info'); ?>"><?php echo epc_scp_h($statuses[$t['status']] ?? $t['status']); ?></span></td>
					<td><?php echo !empty($t['due_at']) ? epc_scp_h(date('Y-m-d', (int) $t['due_at'])) : '—'; ?></td>
					<td class="epc-scp-actions-cell">
						<a class="btn btn-xs btn-default" href="?edit_task=<?php echo (int) $t['id']; ?><?php echo $taskFilter !== '' ? '&task_status=' . urlencode($taskFilter) : ''; ?>"><i class="fa fa-edit"></i></a>
						<form method="post" style="display:inline" onsubmit="return confirm('Delete this task?');">
							<input type="hidden" name="epc_scp_action" value="delete_task" />
							<input type="hidden" name="id" value="<?php echo (int) $t['id']; ?>" />
							<button type="submit" class="btn btn-xs btn-danger"><i class="fa fa-trash"></i></button>
						</form>
					</td>
				</tr>
			<?php } ?>
			</tbody>
		</table>
	</div>
</div>
</div>
