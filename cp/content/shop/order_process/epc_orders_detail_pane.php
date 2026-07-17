<?php
/**
 * Order detail + quick-edit pane for dual-pane orders workspace.
 * Expects: $order_id, $db_link, $DP_Config, $orders_statuses, $orders_items_statuses,
 *          $offices_list, $orders_items_statuses_not_count, $shop_orders_paid_type (optional).
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_orders_ws_h')) {
	$epc_orders_ws_helpers = $_SERVER['DOCUMENT_ROOT'] . '/'
		. ($GLOBALS['DP_Config']->backend_dir ?? 'cp')
		. '/content/shop/order_process/epc_orders_workspace_helpers.php';
	if (is_file($epc_orders_ws_helpers)) {
		require_once $epc_orders_ws_helpers;
	}
}

$order_id = (int) ($order_id ?? 0);
if ($order_id <= 0) {
	echo '<div class="epc-scp-orders-detail__empty"><i class="fa fa-hand-pointer-o"></i><p>Select an order from the list</p><span class="text-muted small">Ctrl+click opens the full editor</span></div>';
	return;
}

$WHERE_statuses_not_count = '';
for ($i = 0; $i < count($orders_items_statuses_not_count); $i++) {
	$WHERE_statuses_not_count .= ' AND `status` != ' . (int) $orders_items_statuses_not_count[$i];
}

$order_query = $db_link->prepare(
	"SELECT *, (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`,
	CAST((SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` $WHERE_statuses_not_count) AS DECIMAL(20,2)) AS `price_sum`
	FROM `shop_orders` WHERE `id` = ? LIMIT 1"
);
$order_query->execute(array($order_id));
$order = $order_query->fetch(PDO::FETCH_ASSOC);

if (!$order || !isset($offices_list[$order['office_id']])) {
	echo '<div class="epc-scp-orders-detail__empty"><i class="fa fa-exclamation-triangle"></i><p>Order not found or access denied</p></div>';
	return;
}

if (empty($shop_orders_paid_type)) {
	$shop_orders_paid_type = array();
	$q = $db_link->prepare('SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`');
	$q->execute();
	while ($rov = $q->fetch()) {
		$shop_orders_paid_type[$rov['id']] = $rov['name'];
	}
}

$db_link->prepare('UPDATE `shop_orders_viewed` SET `viewed_flag` = 1 WHERE `order_id` = ?')->execute(array($order_id));
$db_link->prepare('UPDATE `shop_orders_messages` SET `read` = 1 WHERE `order_id` = ? AND `is_customer` = 1')->execute(array($order_id));

$status = (int) $order['status'];
$paid = (int) $order['paid'];
$paid_type = (int) ($order['paid_type'] ?? 0);
$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}
$fullUrl = '/' . $backend . '/shop/orders/order?order_id=' . $order_id;
$itemsUrl = '/' . $backend . '/shop/orders/items';
$waUrl = '/' . $backend . '/shop/orders/whatsapp-guide';

$items_query = $db_link->prepare('SELECT * FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`');
$items_query->execute(array($order_id));
$items = $items_query->fetchAll(PDO::FETCH_ASSOC);

$logs = array();
try {
	$lq = $db_link->prepare('SELECT `time`, `text`, `is_manager` FROM `shop_orders_logs` WHERE `order_id` = ? ORDER BY `id` DESC LIMIT 6');
	$lq->execute(array($order_id));
	$logs = $lq->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$logs = array();
}

$customer_label = '';
if ((int) $order['user_id'] > 0) {
	$u = $db_link->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$u->execute(array($order['user_id']));
	$ur = $u->fetch(PDO::FETCH_ASSOC);
	$customer_label = 'ID ' . (int) $order['user_id'];
	if (!empty($ur['email'])) {
		$customer_label .= ' · ' . $ur['email'];
	}
	if (!empty($ur['phone'])) {
		$customer_label .= ' · ' . $ur['phone'];
	}
} else {
	$customer_label = translate_str_by_id(3549) . ' (ID 0)';
	if (!empty($order['phone_not_auth'])) {
		$customer_label .= ' · ' . $order['phone_not_auth'];
	}
	if (!empty($order['email_not_auth'])) {
		$customer_label .= ' · ' . $order['email_not_auth'];
	}
}
?>
<div class="epc-od" data-order-id="<?php echo (int) $order_id; ?>">
	<div class="epc-od__head">
		<div class="epc-od__title-row">
			<h3 class="epc-od__title">Order #<?php echo (int) $order_id; ?></h3>
			<span id="epc_od_status_badge"><?php echo epc_orders_ws_status_badge($status, $orders_statuses, $db_link); ?></span>
		</div>
		<div class="epc-od__meta">
			<span><i class="fa fa-clock-o"></i> <?php echo epc_orders_ws_h(date('d.m.Y H:i', (int) $order['time'])); ?></span>
			<span><i class="fa fa-building-o"></i> <?php echo epc_orders_ws_h(translate_str_by_id($offices_list[$order['office_id']])); ?></span>
			<span><i class="fa fa-truck"></i> <?php echo epc_orders_ws_h(translate_str_by_id($order['obtain_caption'])); ?></span>
		</div>
		<div class="epc-od__chips">
			<?php echo epc_orders_ws_paid_badge($paid); ?>
			<?php if (!empty($shop_orders_paid_type[$paid_type])) { ?>
			<span class="epc-scp-badge epc-scp-badge--normal"><?php echo epc_orders_ws_h(translate_str_by_id($shop_orders_paid_type[$paid_type])); ?></span>
			<?php } ?>
			<span class="epc-scp-badge epc-scp-badge--normal"><?php echo count($items); ?> lines</span>
		</div>
		<div class="epc-od__customer"><?php echo epc_orders_ws_h($customer_label); ?></div>
		<div class="epc-od__total">
			<strong><?php echo epc_orders_ws_h(number_format((float) $order['price_sum'], 2, '.', ' ')); ?></strong>
			<span class="text-muted"><?php echo epc_orders_ws_h(translate_str_by_id(3251)); ?></span>
		</div>
	</div>

	<div id="epc_od_toast" class="epc-od__toast" role="status"></div>

	<div class="epc-od__edit">
		<div class="epc-od__edit-title">Quick manage</div>
		<div class="epc-od__edit-row">
			<label for="epc_od_status">Order status</label>
			<select id="epc_od_status" class="form-control">
				<?php foreach ($orders_statuses as $sid => $sdata) { ?>
				<option value="<?php echo (int) $sid; ?>"<?php echo ((int) $sid === $status) ? ' selected' : ''; ?>><?php echo epc_orders_ws_h(translate_str_by_id($sdata['name'])); ?></option>
				<?php } ?>
			</select>
			<button type="button" class="btn btn-primary btn-sm" onclick="epcApplyOrderStatus(<?php echo (int) $order_id; ?>);"><i class="fa fa-check"></i> Update status</button>
		</div>
		<div class="epc-od__edit-row">
			<label for="epc_od_comment">Add note to order log</label>
			<textarea id="epc_od_comment" placeholder="Internal note for this order…"></textarea>
			<button type="button" class="btn btn-default btn-sm" onclick="epcAddOrderComment(<?php echo (int) $order_id; ?>);"><i class="fa fa-comment"></i> Save note</button>
		</div>
	</div>

	<div class="epc-od__actions">
		<a class="btn btn-primary btn-sm" href="<?php echo epc_orders_ws_h($fullUrl); ?>"><i class="fa fa-pencil"></i> Edit full order</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_orders_ws_h($fullUrl); ?>#order_items"><i class="fa fa-list"></i> Edit lines / pay</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_orders_ws_h($itemsUrl); ?>"><i class="fa fa-cubes"></i> All order items</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_orders_ws_h($waUrl); ?>"><i class="fa fa-whatsapp"></i> WhatsApp</a>
	</div>

	<div id="epc-order-fulfillment-panel-<?php echo (int) $order_id; ?>" class="epc-scp-orders-detail__erp-fulfillment"></div>

	<div class="epc-od__items">
		<h4 class="epc-od__items-title"><?php echo epc_orders_ws_h(translate_str_by_id(4569)); ?></h4>
		<?php if (count($items) === 0) { ?>
		<p class="text-muted">No line items</p>
		<?php } else { ?>
		<div class="table-responsive">
			<table class="table table-condensed table-striped epc-scp-data-table epc-od__table">
				<thead>
					<tr>
						<th><?php echo epc_orders_ws_h(translate_str_by_id(2070)); ?></th>
						<th><?php echo epc_orders_ws_h(translate_str_by_id(2071)); ?></th>
						<th><?php echo epc_orders_ws_h(translate_str_by_id(2102)); ?></th>
						<th>Qty</th>
						<th><?php echo epc_orders_ws_h(translate_str_by_id(2081)); ?></th>
						<th></th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($items as $item) {
					$itemStatus = (int) $item['status'];
					$itemEdit = '/' . $backend . '/shop/orders/items/edit?id=' . (int) $item['id'];
					$canEditItem = ($paid === 0);
					?>
					<tr>
						<td><?php echo epc_orders_ws_h($item['t2_manufacturer']); ?></td>
						<td><code><?php echo epc_orders_ws_h($item['t2_article']); ?></code></td>
						<td class="epc-od__name" title="<?php echo epc_orders_ws_h($item['t2_name']); ?>"><?php echo epc_orders_ws_h($item['t2_name']); ?></td>
						<td><?php echo (int) $item['count_need']; ?></td>
						<td><span class="epc-scp-badge epc-scp-badge--normal"><?php echo epc_orders_ws_h(translate_str_by_id($orders_items_statuses[$itemStatus]['name'] ?? '')); ?></span></td>
						<td>
							<?php if ($canEditItem) { ?>
							<a class="btn btn-xs btn-default" href="<?php echo epc_orders_ws_h($itemEdit); ?>" title="Edit line"><i class="fa fa-pencil"></i></a>
							<?php } else { ?>
							<a class="btn btn-xs btn-default" href="<?php echo epc_orders_ws_h($fullUrl); ?>" title="Open full order"><i class="fa fa-external-link"></i></a>
							<?php } ?>
						</td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
		</div>
		<?php } ?>
	</div>

	<?php if (count($logs) > 0) { ?>
	<div class="epc-od__logs">
		<h5>Recent log</h5>
		<?php foreach ($logs as $log) { ?>
		<div class="epc-od__log">
			<time><?php echo epc_orders_ws_h(date('d.m.Y H:i', (int) $log['time'])); ?><?php echo !empty($log['is_manager']) ? ' · staff' : ''; ?></time>
			<?php echo epc_orders_ws_h($log['text']); ?>
		</div>
		<?php } ?>
	</div>
	<?php } ?>
</div>
