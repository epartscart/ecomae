<?php
/**
 * CP Returns list — each row shows the linked order clearly.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_returns_process.php';
$automation = epc_returns_ensure_automation($db_link);

$backend = htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8');
$filter_status = isset($_GET['status_id']) ? (int) $_GET['status_id'] : 0;
$filter_order = isset($_GET['order_id']) ? (int) $_GET['order_id'] : 0;
$filter_user = isset($_GET['user_id']) ? (int) $_GET['user_id'] : 0;
$filter_unread = isset($_GET['read']) && (string) $_GET['read'] === '0';
$return_id_jump = isset($_GET['return_id']) ? (int) $_GET['return_id'] : 0;

if ($return_id_jump > 0) {
	?>
	<script>location = "/<?php echo $backend; ?>/shop/returns-manager?page=detail&return_id=<?php echo $return_id_jump; ?>";</script>
	<?php
	exit;
}

$sql = "SELECT r.*,
	s.`caption` AS `status_caption`,
	s.`color` AS `status_color`,
	(SELECT COUNT(*) FROM `shop_orders_messages` m WHERE m.`return_id` = r.`id` AND m.`read` = 0 AND m.`is_customer` = 1) AS `unread_msgs`,
	(SELECT GROUP_CONCAT(DISTINCT oi.`order_id` ORDER BY oi.`order_id` SEPARATOR ',')
		FROM `shop_orders_returns_items` ri
		INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
		WHERE ri.`return_id` = r.`id`) AS `order_ids`,
	(SELECT COUNT(*) FROM `shop_orders_returns_items` ri2 WHERE ri2.`return_id` = r.`id`) AS `lines_count`,
	u.`email` AS `customer_email`,
	u.`phone` AS `customer_phone`
FROM `shop_orders_returns` r
LEFT JOIN `shop_orders_returns_statuses` s ON s.`id` = r.`status_id`
LEFT JOIN `users` u ON u.`user_id` = r.`user_id`
WHERE 1=1";
$args = array();

if ($filter_status > 0) {
	$sql .= ' AND r.`status_id` = ?';
	$args[] = $filter_status;
}
if ($filter_user > 0) {
	$sql .= ' AND r.`user_id` = ?';
	$args[] = $filter_user;
}
if ($filter_order > 0) {
	$sql .= ' AND r.`id` IN (
		SELECT ri.`return_id` FROM `shop_orders_returns_items` ri
		INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
		WHERE oi.`order_id` = ?
	)';
	$args[] = $filter_order;
}
if ($filter_unread) {
	$sql .= ' AND r.`id` IN (SELECT DISTINCT `return_id` FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 1 AND `return_id` > 0)';
}
$sql .= ' ORDER BY r.`id` DESC LIMIT 300';

$list = $db_link->prepare($sql);
$list->execute($args);
$rows = $list->fetchAll(PDO::FETCH_ASSOC);

$statuses = $db_link->query('SELECT * FROM `shop_orders_returns_statuses` ORDER BY `id` ASC')->fetchAll(PDO::FETCH_ASSOC);
?>
<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Returns / refund requests</div>
		<div class="panel-body">
			<?php if (!empty($automation['report'])) { ?>
				<div class="alert alert-info" style="margin-bottom:12px;">
					<strong>Return process ready.</strong>
					<?php echo htmlspecialchars(implode(' · ', $automation['report']), ENT_QUOTES, 'UTF-8'); ?>
				</div>
			<?php } ?>
			<p class="text-muted" style="margin-bottom:14px;">
				Each return is tied to one or more <strong>orders</strong> through the returned line items. Open a return to approve or deny lines against that order.
			</p>
			<form method="get" class="form-inline" style="margin-bottom:14px;">
				<input type="hidden" name="page" value="list" />
				<label>Status</label>
				<select name="status_id" class="form-control" onchange="this.form.submit()">
					<option value="0">All</option>
					<?php foreach ($statuses as $st) { ?>
						<option value="<?php echo (int) $st['id']; ?>" <?php echo $filter_status === (int) $st['id'] ? 'selected' : ''; ?>>
							<?php echo htmlspecialchars(epc_returns_label($st['caption']), ENT_QUOTES, 'UTF-8'); ?>
						</option>
					<?php } ?>
				</select>
				<label style="margin-left:10px;">Order ID</label>
				<input type="number" name="order_id" class="form-control" value="<?php echo $filter_order > 0 ? $filter_order : ''; ?>" placeholder="Order #" style="width:110px;" />
				<label style="margin-left:10px;">Customer ID</label>
				<input type="number" name="user_id" class="form-control" value="<?php echo $filter_user > 0 ? $filter_user : ''; ?>" placeholder="User #" style="width:110px;" />
				<button type="submit" class="btn btn-primary" style="margin-left:8px;"><i class="fa fa-filter"></i> Filter</button>
				<a class="btn btn-default" href="/<?php echo $backend; ?>/shop/returns-manager">Reset</a>
			</form>

			<div class="table-responsive">
				<table class="table table-striped table-condensed">
					<thead>
						<tr>
							<th>Return</th>
							<th>Against order</th>
							<th>Customer</th>
							<th>Status</th>
							<th>Lines</th>
							<th>Sum</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
					<?php if (!$rows) { ?>
						<tr><td colspan="7" class="text-muted">No return requests yet. Customers create returns from issued order lines on the storefront.</td></tr>
					<?php } ?>
					<?php foreach ($rows as $r) {
						$orderIds = array_filter(array_map('intval', explode(',', (string) $r['order_ids'])));
						$statusLabel = epc_returns_label((string) $r['status_caption']);
						$cust = trim(($r['customer_email'] ?: '') . (($r['customer_phone'] && $r['customer_email']) ? ' · ' : '') . ($r['customer_phone'] ?: ''));
						if ($cust === '') {
							$cust = 'Customer #'.(int) $r['user_id'];
						}
						?>
						<tr style="background:<?php echo htmlspecialchars((string) $r['status_color'], ENT_QUOTES, 'UTF-8'); ?>;">
							<td>
								<strong>#<?php echo (int) $r['id']; ?></strong>
								<?php if ((int) $r['unread_msgs'] > 0) { ?>
									<span class="label label-danger" title="Unread customer messages"><?php echo (int) $r['unread_msgs']; ?></span>
								<?php } ?>
							</td>
							<td>
								<?php if (!$orderIds) { ?>
									<span class="text-danger">No order linked</span>
								<?php } else {
									foreach ($orderIds as $oid) { ?>
										<a href="/<?php echo $backend; ?>/shop/orders/order?order_id=<?php echo $oid; ?>" target="_blank" style="font-weight:700; margin-right:8px;">Order #<?php echo $oid; ?></a>
									<?php }
								} ?>
							</td>
							<td>
								<a href="/<?php echo $backend; ?>/users/usermanager/user?user_id=<?php echo (int) $r['user_id']; ?>" target="_blank">
									<?php echo htmlspecialchars($cust, ENT_QUOTES, 'UTF-8'); ?>
								</a>
								<div class="text-muted" style="font-size:12px;">ID <?php echo (int) $r['user_id']; ?></div>
							</td>
							<td><?php echo htmlspecialchars($statusLabel, ENT_QUOTES, 'UTF-8'); ?></td>
							<td><?php echo (int) $r['lines_count']; ?></td>
							<td><?php echo number_format((float) $r['sum'], 2, '.', ' '); ?></td>
							<td>
								<a class="btn btn-xs btn-primary" href="/<?php echo $backend; ?>/shop/returns-manager?page=detail&return_id=<?php echo (int) $r['id']; ?>">Open</a>
							</td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>
</div>
