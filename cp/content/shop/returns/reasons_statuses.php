<?php
/**
 * CP: manage return reasons and statuses + show automation flags.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_returns_process.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
$automation = epc_returns_ensure_automation($db_link);
$backend = htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8');

if (!empty($_POST['action'])) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
	$action = (string) $_POST['action'];
	if ($action === 'add_reason') {
		$caption = trim((string) ($_POST['caption'] ?? ''));
		if ($caption !== '') {
			$key = epc_returns_ensure_lang_string($db_link, 'epc_ret_rs_'.md5(strtolower($caption)), $caption);
			$db_link->prepare('INSERT INTO `shop_orders_returns_reasons` (`caption`) VALUES (?)')->execute(array($key));
		}
	}
	if ($action === 'add_status') {
		$caption = trim((string) ($_POST['caption'] ?? ''));
		$color = trim((string) ($_POST['color'] ?? '#eeeeee'));
		if ($caption !== '') {
			$key = epc_returns_ensure_lang_string($db_link, 'epc_ret_st_'.md5(strtolower($caption)), $caption);
			$db_link->prepare('INSERT INTO `shop_orders_returns_statuses` (`color`,`caption`) VALUES (?,?)')->execute(array($color, $key));
		}
	}
	?>
	<script>location="/<?php echo $backend; ?>/shop/returns-manager?page=reasons_statuses&action=select&success_message=<?php echo urlencode('Saved'); ?>");</script>
	<?php
	exit;
}

$reasons = $db_link->query('SELECT * FROM `shop_orders_returns_reasons` ORDER BY `id` ASC')->fetchAll(PDO::FETCH_ASSOC);
$statuses = $db_link->query('SELECT * FROM `shop_orders_returns_statuses` ORDER BY `id` ASC')->fetchAll(PDO::FETCH_ASSOC);
$itemFlags = $db_link->query('SELECT `id`,`name`,`color`,`check_for_return`,`for_return`,`complete_return`,`reject_return`,`issue_flag`,`for_finish` FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC, `id` ASC')->fetchAll(PDO::FETCH_ASSOC);
$csrf = htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
?>
<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Return process &amp; automation</div>
		<div class="panel-body">
			<p class="text-muted">
				Workflow: customer requests return on <strong>Issued</strong> order lines → item moves to <strong>Request for refund</strong> → staff approves/denies against the order → item moves to Return approved / Return rejected → return request closed.
			</p>
			<table class="table table-condensed table-bordered" style="max-width:900px;">
				<thead>
					<tr>
						<th>Item status</th>
						<th>Can request return</th>
						<th>In return</th>
						<th>Approved</th>
						<th>Rejected</th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($itemFlags as $st) {
					$show = ((int)$st['check_for_return'] || (int)$st['for_return'] || (int)$st['complete_return'] || (int)$st['reject_return'] || (int)$st['issue_flag'] || (int)$st['for_finish']);
					if (!$show) { continue; }
					?>
					<tr>
						<td><?php echo htmlspecialchars(epc_returns_label((string)$st['name']), ENT_QUOTES, 'UTF-8'); ?> <span class="text-muted">#<?php echo (int)$st['id']; ?></span></td>
						<td><?php echo (int)$st['check_for_return'] ? '✓' : '—'; ?></td>
						<td><?php echo (int)$st['for_return'] ? '✓' : '—'; ?></td>
						<td><?php echo (int)$st['complete_return'] ? '✓' : '—'; ?></td>
						<td><?php echo (int)$st['reject_return'] ? '✓' : '—'; ?></td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
			<?php if (!empty($automation['report'])) { ?>
				<div class="alert alert-success"><?php echo htmlspecialchars(implode(' · ', $automation['report']), ENT_QUOTES, 'UTF-8'); ?></div>
			<?php } else { ?>
				<div class="alert alert-info">Automation flags are already set.</div>
			<?php } ?>
		</div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Reasons</div>
		<div class="panel-body">
			<ul class="list-group">
				<?php foreach ($reasons as $r) { ?>
					<li class="list-group-item">#<?php echo (int)$r['id']; ?> — <?php echo htmlspecialchars(epc_returns_label($r['caption']), ENT_QUOTES, 'UTF-8'); ?></li>
				<?php } ?>
			</ul>
			<form method="post">
				<input type="hidden" name="action" value="add_reason" />
				<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
				<div class="form-group">
					<label>New reason</label>
					<input type="text" name="caption" class="form-control" required />
				</div>
				<button type="submit" class="btn btn-primary">Add reason</button>
			</form>
		</div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Return request statuses</div>
		<div class="panel-body">
			<ul class="list-group">
				<?php foreach ($statuses as $s) { ?>
					<li class="list-group-item">
						<span style="display:inline-block;width:14px;height:14px;background:<?php echo htmlspecialchars($s['color'], ENT_QUOTES, 'UTF-8'); ?>; margin-right:6px;"></span>
						#<?php echo (int)$s['id']; ?> — <?php echo htmlspecialchars(epc_returns_label($s['caption']), ENT_QUOTES, 'UTF-8'); ?>
					</li>
				<?php } ?>
			</ul>
			<form method="post">
				<input type="hidden" name="action" value="add_status" />
				<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
				<div class="form-group">
					<label>New status</label>
					<input type="text" name="caption" class="form-control" required />
				</div>
				<div class="form-group">
					<label>Color</label>
					<input type="color" name="color" class="form-control" value="#f5f3cc" style="max-width:120px;" />
				</div>
				<button type="submit" class="btn btn-primary">Add status</button>
			</form>
		</div>
	</div>
</div>
<p class="col-lg-12"><a href="/<?php echo $backend; ?>/shop/returns-manager">Back to returns list</a></p>
