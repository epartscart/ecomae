<?php
/**
 * CP: approve / suspend frontend vendor accounts.
 * Optional — registration auto-approves; use this to review or suspend.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/vendor/epc_vendor_access.php';

$user_session = DP_User::getAdminSession();
$backend = isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp';
$baseUrl = '/' . $backend . '/users/vendor_approvals';

epc_vendor_ensure_schema($db_link);

if (!empty($_POST['action'])) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
	$action = (string) $_POST['action'];
	$id = (int) ($_POST['id'] ?? 0);
	$adminId = (int) DP_User::getAdminId();
	if ($id > 0) {
		if ($action === 'approve') {
			epc_vendor_approve_account($db_link, $id, $adminId);
		} elseif ($action === 'suspend') {
			$db_link->prepare('UPDATE `epc_vendor_accounts` SET `status` = \'suspended\', `updated_at` = ? WHERE `id` = ?')
				->execute(array(time(), $id));
		} elseif ($action === 'reject') {
			$db_link->prepare('UPDATE `epc_vendor_accounts` SET `status` = \'rejected\', `updated_at` = ? WHERE `id` = ?')
				->execute(array(time(), $id));
		}
	}
	echo '<script>location = ' . json_encode($baseUrl . '?success_message=' . rawurlencode('Updated')) . ';</script>';
	exit;
}

require_once 'content/control/actions_alert.php';

$status = isset($_GET['status']) ? trim((string) $_GET['status']) : '';
$sql = 'SELECT a.*, u.email FROM `epc_vendor_accounts` a LEFT JOIN `users` u ON u.user_id = a.user_id WHERE 1=1';
$bind = array();
if ($status !== '') {
	$sql .= ' AND a.`status` = ?';
	$bind[] = $status;
}
$sql .= ' ORDER BY a.`id` DESC LIMIT 200';
$st = $db_link->prepare($sql);
$st->execute($bind);
$rows = $st->fetchAll(PDO::FETCH_ASSOC);
?>
<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Vendor approvals
			<span class="pull-right"><a href="/en/vendor" target="_blank">Open frontend portal</a></span>
		</div>
		<div class="panel-body">
			<p>Frontend vendors register at <code>/en/vendor/register</code> and upload prices at <code>/en/vendor/upload</code> (no CP login). New accounts are auto-approved; use this page to suspend or re-approve.</p>
			<form method="get" class="form-inline" style="margin-bottom:12px;">
				<label>Status</label>
				<select name="status" class="form-control" onchange="this.form.submit()">
					<option value="">All</option>
					<?php foreach (array('approved', 'pending', 'suspended', 'rejected') as $s) { ?>
						<option value="<?php echo $s; ?>" <?php echo $status === $s ? 'selected' : ''; ?>><?php echo $s; ?></option>
					<?php } ?>
				</select>
			</form>
			<table class="table table-striped table-condensed">
				<thead>
					<tr>
						<th>ID</th>
						<th>Vendor</th>
						<th>Legal / TRN</th>
						<th>Code</th>
						<th>Email</th>
						<th>City / Emirate</th>
						<th>Warehouse</th>
						<th>Status</th>
						<th>Created</th>
						<th>Actions</th>
					</tr>
				</thead>
				<tbody>
				<?php if (!$rows) { ?>
					<tr><td colspan="10">No vendor accounts yet.</td></tr>
				<?php } ?>
				<?php foreach ($rows as $r) { ?>
					<tr>
						<td><?php echo (int) $r['id']; ?></td>
						<td><?php echo htmlspecialchars((string) $r['vendor_full'], ENT_QUOTES, 'UTF-8'); ?></td>
						<td>
							<?php echo htmlspecialchars((string) ($r['legal_name'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>
							<?php if (!empty($r['trn'])) { ?>
								<br><code><?php echo htmlspecialchars((string) $r['trn'], ENT_QUOTES, 'UTF-8'); ?></code>
							<?php } ?>
							<?php if (!empty($r['legal_reg_no'])) { ?>
								<br><small><?php echo htmlspecialchars((string) (($r['legal_reg_type'] ?? 'TL') . ' ' . $r['legal_reg_no']), ENT_QUOTES, 'UTF-8'); ?></small>
							<?php } ?>
						</td>
						<td><strong><?php echo htmlspecialchars((string) $r['vendor_short'], ENT_QUOTES, 'UTF-8'); ?></strong></td>
						<td><?php echo htmlspecialchars((string) ($r['email'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
						<td><?php
							$loc = trim((string) ($r['city'] ?? ''));
							$em = trim((string) ($r['emirate'] ?? ''));
							echo htmlspecialchars($loc . ($em !== '' ? ', ' . $em : ''), ENT_QUOTES, 'UTF-8');
						?></td>
						<td><?php echo (int) $r['storage_id']; ?></td>
						<td><?php echo htmlspecialchars((string) $r['status'], ENT_QUOTES, 'UTF-8'); ?></td>
						<td><?php echo !empty($r['created_at']) ? date('Y-m-d H:i', (int) $r['created_at']) : ''; ?></td>
						<td>
							<a class="btn btn-xs btn-default" href="<?php echo htmlspecialchars($baseUrl . '?status=' . rawurlencode($status) . '&user_id=' . (int) $r['user_id'], ENT_QUOTES, 'UTF-8'); ?>">KYC</a>
							<?php if ($r['status'] !== 'approved') { ?>
							<form method="post" style="display:inline;">
								<input type="hidden" name="action" value="approve" />
								<input type="hidden" name="id" value="<?php echo (int) $r['id']; ?>" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
								<button class="btn btn-xs btn-success" type="submit">Approve</button>
							</form>
							<?php } ?>
							<?php if ($r['status'] === 'approved') { ?>
							<form method="post" style="display:inline;">
								<input type="hidden" name="action" value="suspend" />
								<input type="hidden" name="id" value="<?php echo (int) $r['id']; ?>" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
								<button class="btn btn-xs btn-warning" type="submit">Suspend</button>
							</form>
							<?php } ?>
						</td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
			<?php
			$reviewUid = (int) ($_GET['user_id'] ?? 0);
			if ($reviewUid > 0) {
				$rfCompliance = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_reg_fields_compliance.php';
				if (is_readable($rfCompliance)) {
					require_once $rfCompliance;
					echo '<div style="margin-top:16px;padding-top:12px;border-top:1px solid #e5e7eb;">';
					echo '<p><strong>Vendor user #' . $reviewUid . '</strong> — compliance checklist from registration fields</p>';
					echo '<p><a class="btn btn-xs btn-default" href="/' . htmlspecialchars($backend, ENT_QUOTES, 'UTF-8') . '/users/polya-registracii">Configure registration fields</a></p>';
					echo epc_rf_render_approval_checklist($db_link, $reviewUid, 'vendor');
					echo '</div>';
				}
			}
			?>
		</div>
	</div>
</div>
