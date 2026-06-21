<?php
/**
 * Compact order detail pane for dual-pane orders workspace (right column).
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
	echo '<div class="epc-scp-orders-detail__empty"><i class="fa fa-hand-pointer-o"></i><p>Select an order from the list</p></div>';
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
$fullUrl = '/' . $DP_Config->backend_dir . '/shop/orders/order?order_id=' . $order_id;

$items_query = $db_link->prepare('SELECT * FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`');
$items_query->execute(array($order_id));
$items = $items_query->fetchAll(PDO::FETCH_ASSOC);

$customer_label = '';
if ((int) $order['user_id'] > 0) {
	$u = $db_link->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$u->execute(array($order['user_id']));
	$ur = $u->fetch(PDO::FETCH_ASSOC);
	$customer_label = 'ID ' . (int) $order['user_id'];
	if (!empty($ur['email'])) {
		$customer_label .= ' · ' . $ur['email'];
	}
} else {
	$customer_label = translate_str_by_id(3549) . ' (ID 0)';
	if (!empty($order['phone_not_auth'])) {
		$customer_label .= ' · ' . $order['phone_not_auth'];
	}
}
?>
<div class="epc-scp-orders-detail__head">
	<div class="epc-scp-orders-detail__title-row">
		<h3 class="epc-scp-orders-detail__title">Order #<?php echo (int) $order_id; ?></h3>
		<?php echo epc_orders_ws_status_badge($status, $orders_statuses, $db_link); ?>
	</div>
	<div class="epc-scp-orders-detail__meta">
		<span><i class="fa fa-clock-o"></i> <?php echo epc_orders_ws_h(date('d.m.Y H:i', (int) $order['time'])); ?></span>
		<span><i class="fa fa-building-o"></i> <?php echo epc_orders_ws_h(translate_str_by_id($offices_list[$order['office_id']])); ?></span>
	</div>
	<div class="epc-scp-orders-detail__chips">
		<?php echo epc_orders_ws_paid_badge($paid); ?>
		<?php if (!empty($shop_orders_paid_type[$paid_type])) { ?>
		<span class="epc-scp-badge epc-scp-badge--normal"><?php echo epc_orders_ws_h(translate_str_by_id($shop_orders_paid_type[$paid_type])); ?></span>
		<?php } ?>
		<span class="epc-scp-badge epc-scp-badge--high"><?php echo epc_orders_ws_h(translate_str_by_id($order['obtain_caption'])); ?></span>
	</div>
	<div class="epc-scp-orders-detail__customer text-muted small"><?php echo epc_orders_ws_h($customer_label); ?></div>
	<div class="epc-scp-orders-detail__total">
		<strong><?php echo epc_orders_ws_h(number_format((float) $order['price_sum'], 2, '.', ' ')); ?></strong>
		<span class="text-muted"><?php echo epc_orders_ws_h(translate_str_by_id(3251)); ?></span>
	</div>
	<div class="epc-scp-orders-detail__actions">
		<a class="btn btn-primary btn-sm" href="<?php echo epc_orders_ws_h($fullUrl); ?>"><i class="fa fa-external-link"></i> Full order</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_orders_ws_h('/' . $DP_Config->backend_dir . '/shop/orders/whatsapp-guide'); ?>"><i class="fa fa-whatsapp"></i> WhatsApp</a>
	</div>
	<div id="epc-order-fulfillment-panel-<?php echo (int) $order_id; ?>" class="epc-scp-orders-detail__erp-fulfillment"></div>
</div>

<div class="epc-scp-orders-detail__items">
	<h4 class="epc-scp-orders-detail__items-title"><?php echo epc_orders_ws_h(translate_str_by_id(4569)); ?></h4>
	<?php if (count($items) === 0) { ?>
	<p class="text-muted">No line items</p>
	<?php } else { ?>
	<div class="table-responsive">
		<table class="table table-condensed table-striped epc-scp-data-table epc-scp-orders-detail__table">
			<thead>
				<tr>
					<th><?php echo epc_orders_ws_h(translate_str_by_id(2070)); ?></th>
					<th><?php echo epc_orders_ws_h(translate_str_by_id(2071)); ?></th>
					<th><?php echo epc_orders_ws_h(translate_str_by_id(2102)); ?></th>
					<th>Qty</th>
					<th><?php echo epc_orders_ws_h(translate_str_by_id(2081)); ?></th>
				</tr>
			</thead>
			<tbody>
			<?php foreach ($items as $item) {
				$itemStatus = (int) $item['status'];
				?>
				<tr>
					<td><?php echo epc_orders_ws_h($item['t2_manufacturer']); ?></td>
					<td><code><?php echo epc_orders_ws_h($item['t2_article']); ?></code></td>
					<td class="epc-scp-orders-detail__name"><?php echo epc_orders_ws_h($item['t2_name']); ?></td>
					<td><?php echo (int) $item['count_need']; ?></td>
					<td><span class="epc-scp-badge epc-scp-badge--normal"><?php echo epc_orders_ws_h(translate_str_by_id($orders_items_statuses[$itemStatus]['name'] ?? '')); ?></span></td>
				</tr>
			<?php } ?>
			</tbody>
		</table>
	</div>
	<?php } ?>
</div>
