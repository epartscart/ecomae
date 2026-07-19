<?php
/**
 * One-window Order Management console (detail pane).
 * Expects: $order_id, $db_link, $DP_Config, $orders_statuses, $orders_items_statuses,
 *          $offices_list, $orders_items_statuses_not_count, $shop_orders_paid_type (optional),
 *          $storages_list (optional).
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
	echo '<div class="epc-scp-orders-detail__empty"><i class="fa fa-hand-pointer-o"></i><p>Select an order to manage</p><span class="text-muted small">One-window OMS: items, status timeline, supplier &amp; customer messages</span></div>';
	return;
}

$WHERE_statuses_not_count = '';
for ($i = 0; $i < count($orders_items_statuses_not_count); $i++) {
	$WHERE_statuses_not_count .= ' AND `status` != ' . (int) $orders_items_statuses_not_count[$i];
}

$order_query = $db_link->prepare(
	"SELECT *, (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`,
	CAST((SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` $WHERE_statuses_not_count) AS DECIMAL(20,2)) AS `price_sum`,
	CAST((SELECT SUM(`t2_price_purchase`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` $WHERE_statuses_not_count) AS DECIMAL(20,2)) AS `purchase_sum`
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
if (!isset($storages_list) || !is_array($storages_list)) {
	$storages_list = array();
	try {
		$sq = $db_link->query('SELECT `id`,`name` FROM `shop_storages`');
		while ($s = $sq->fetch(PDO::FETCH_ASSOC)) {
			$storages_list[(int) $s['id']] = $s['name'];
		}
	} catch (Throwable $e) {
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
$canEditItems = ($paid === 0);

$items_query = $db_link->prepare('SELECT * FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`');
$items_query->execute(array($order_id));
$items = $items_query->fetchAll(PDO::FETCH_ASSOC);

$logs = array();
try {
	$lq = $db_link->prepare('SELECT `time`, `text`, `is_manager`, `is_robot` FROM `shop_orders_logs` WHERE `order_id` = ? ORDER BY `id` DESC LIMIT 40');
	$lq->execute(array($order_id));
	$logs = $lq->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$logs = array();
}

$messages = array();
try {
	$mq = $db_link->prepare('SELECT `id`, `text`, `time`, `is_customer` FROM `shop_orders_messages` WHERE `order_id` = ? AND `return_id` = 0 ORDER BY `id` ASC LIMIT 80');
	$mq->execute(array($order_id));
	$messages = $mq->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$messages = array();
}

$customer_label = '';
$customer_name = '';
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
	try {
		$pn = $db_link->prepare("SELECT MAX(CASE WHEN `data_key` IN ('name','first_name') THEN `value` END) AS n, MAX(CASE WHEN `data_key` IN ('surname','last_name') THEN `value` END) AS s FROM `users_profiles` WHERE `user_id` = ?");
		$pn->execute(array($order['user_id']));
		$pr = $pn->fetch(PDO::FETCH_ASSOC);
		$customer_name = trim((string) ($pr['n'] ?? '') . ' ' . (string) ($pr['s'] ?? ''));
	} catch (Throwable $e) {
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

$priceSum = (float) $order['price_sum'];
$purchaseSum = (float) ($order['purchase_sum'] ?? 0);
$benefit = $priceSum - $purchaseSum;
?>
<div class="epc-od epc-od--oms" data-order-id="<?php echo (int) $order_id; ?>" data-can-edit="<?php echo $canEditItems ? '1' : '0'; ?>">
	<div class="epc-od__head">
		<div class="epc-od__title-row">
			<h3 class="epc-od__title">Order #<?php echo (int) $order_id; ?> <span class="epc-od__oms-tag">OMS</span></h3>
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
			<span class="epc-scp-badge epc-scp-badge--normal"><?php echo count($items); ?> items</span>
		</div>
		<div class="epc-od__customer">
			<?php if ($customer_name !== '') { ?><strong><?php echo epc_orders_ws_h($customer_name); ?></strong> · <?php } ?>
			<?php echo epc_orders_ws_h($customer_label); ?>
		</div>
		<div class="epc-od__totals">
			<div><span>Amount</span><strong><?php echo epc_orders_ws_h(number_format($priceSum, 2, '.', ' ')); ?></strong></div>
			<div><span>Purchase</span><strong><?php echo epc_orders_ws_h(number_format($purchaseSum, 2, '.', ' ')); ?></strong></div>
			<div><span>Benefit</span><strong class="<?php echo $benefit >= 0 ? 'is-ok' : 'is-bad'; ?>"><?php echo epc_orders_ws_h(number_format($benefit, 2, '.', ' ')); ?></strong></div>
		</div>
	</div>

	<div id="epc_od_toast" class="epc-od__toast" role="status"></div>

	<nav class="epc-od__tabs" role="tablist">
		<button type="button" class="is-active" data-epc-od-tab="manage">Manage</button>
		<button type="button" data-epc-od-tab="items">Items (<?php echo count($items); ?>)</button>
		<button type="button" data-epc-od-tab="timeline">Status / time</button>
		<button type="button" data-epc-od-tab="messages">Messages (<?php echo count($messages); ?>)</button>
	</nav>

	<section class="epc-od__panel is-active" data-epc-od-panel="manage">
		<div class="epc-od__edit">
			<div class="epc-od__edit-title">Update order</div>
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
				<label for="epc_od_comment">Internal note (status log)</label>
				<textarea id="epc_od_comment" placeholder="Internal note for staff…"></textarea>
				<button type="button" class="btn btn-default btn-sm" onclick="epcAddOrderComment(<?php echo (int) $order_id; ?>);"><i class="fa fa-sticky-note-o"></i> Save note</button>
			</div>
		</div>
		<div class="epc-od__actions">
			<a class="btn btn-default btn-sm" href="<?php echo epc_orders_ws_h($fullUrl); ?>"><i class="fa fa-external-link"></i> Classic full card</a>
			<a class="btn btn-default btn-sm" href="<?php echo epc_orders_ws_h($fullUrl); ?>#order_items"><i class="fa fa-print"></i> Print / pay</a>
		</div>
		<div id="epc-order-fulfillment-panel-<?php echo (int) $order_id; ?>" class="epc-scp-orders-detail__erp-fulfillment"></div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="items">
		<div class="epc-od__items">
			<div class="epc-od__items-head">
				<h4 class="epc-od__items-title">Product details &amp; suppliers</h4>
				<?php if (!$canEditItems) { ?>
				<span class="text-muted small">Paid order — item prices locked</span>
				<?php } ?>
			</div>
			<?php if (!$items) { ?>
			<p class="text-muted">No line items</p>
			<?php } else { ?>
			<?php foreach ($items as $item) {
				$itemId = (int) $item['id'];
				$itemStatus = (int) $item['status'];
				$storageId = (int) ($item['t2_storage_id'] ?? 0);
				$storageName = $storages_list[$storageId] ?? ($item['t2_storage'] ?? '—');
				?>
			<article class="epc-od__item-card" data-item-id="<?php echo $itemId; ?>">
				<header>
					<div>
						<strong><?php echo epc_orders_ws_h($item['t2_manufacturer']); ?></strong>
						<code><?php echo epc_orders_ws_h($item['t2_article']); ?></code>
					</div>
					<span class="epc-scp-badge epc-scp-badge--normal"><?php echo epc_orders_ws_h(translate_str_by_id($orders_items_statuses[$itemStatus]['name'] ?? '')); ?></span>
				</header>
				<p class="epc-od__item-name"><?php echo epc_orders_ws_h($item['t2_name']); ?></p>
				<div class="epc-od__item-grid">
					<label>Sell price
						<input type="number" step="0.01" min="0" class="form-control input-sm" data-field="price" value="<?php echo epc_orders_ws_h(number_format((float) $item['price'], 2, '.', '')); ?>" <?php echo $canEditItems ? '' : 'disabled'; ?> />
					</label>
					<label>Purchase
						<input type="number" step="0.01" min="0" class="form-control input-sm" data-field="t2_price_purchase" value="<?php echo epc_orders_ws_h(number_format((float) $item['t2_price_purchase'], 2, '.', '')); ?>" <?php echo $canEditItems ? '' : 'disabled'; ?> />
					</label>
					<label>Qty
						<input type="number" step="1" min="1" class="form-control input-sm" data-field="count_need" value="<?php echo (int) $item['count_need']; ?>" <?php echo $canEditItems ? '' : 'disabled'; ?> />
					</label>
					<label>Item status
						<select class="form-control input-sm" data-field="item_status">
							<?php foreach ($orders_items_statuses as $isid => $isdata) { ?>
							<option value="<?php echo (int) $isid; ?>"<?php echo ((int) $isid === $itemStatus) ? ' selected' : ''; ?>><?php echo epc_orders_ws_h(translate_str_by_id($isdata['name'])); ?></option>
							<?php } ?>
						</select>
					</label>
					<label class="epc-od__item-supplier">Supplier / warehouse
						<select class="form-control input-sm" data-field="t2_storage_id" <?php echo $canEditItems ? '' : 'disabled'; ?>>
							<option value="0">—</option>
							<?php foreach ($storages_list as $sid => $sname) { ?>
							<option value="<?php echo (int) $sid; ?>"<?php echo ((int) $sid === $storageId) ? ' selected' : ''; ?>><?php echo epc_orders_ws_h(translate_str_by_id($sname)); ?></option>
							<?php } ?>
						</select>
						<small class="text-muted">Current: <?php echo epc_orders_ws_h(is_string($storageName) ? translate_str_by_id($storageName) : (string) $storageName); ?></small>
					</label>
					<label class="epc-od__item-name-edit">Name
						<input type="text" class="form-control input-sm" data-field="t2_name" value="<?php echo epc_orders_ws_h($item['t2_name']); ?>" <?php echo $canEditItems ? '' : 'disabled'; ?> />
					</label>
				</div>
				<footer class="epc-od__item-actions">
					<?php if ($canEditItems) { ?>
					<button type="button" class="btn btn-primary btn-xs" onclick="epcOmsSaveItem(<?php echo (int) $order_id; ?>, <?php echo $itemId; ?>);"><i class="fa fa-save"></i> Save item</button>
					<?php } ?>
					<button type="button" class="btn btn-default btn-xs" onclick="epcOmsSetItemStatus(<?php echo (int) $order_id; ?>, <?php echo $itemId; ?>);"><i class="fa fa-flag"></i> Update item status</button>
					<button type="button" class="btn btn-warning btn-xs" onclick="epcOmsMessageItem(<?php echo (int) $order_id; ?>, <?php echo $itemId; ?>, <?php echo htmlspecialchars(json_encode((string) $item['t2_article']), ENT_QUOTES, 'UTF-8'); ?>, <?php echo htmlspecialchars(json_encode(number_format((float) $item['price'], 2, '.', '')), ENT_QUOTES, 'UTF-8'); ?>);"><i class="fa fa-envelope"></i> Message customer about item</button>
				</footer>
			</article>
			<?php } ?>
			<?php } ?>
		</div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="timeline">
		<div class="epc-od__logs epc-od__logs--full">
			<h5><i class="fa fa-history"></i> Status &amp; activity by time</h5>
			<?php if (!$logs) { ?>
			<p class="text-muted">No log entries yet</p>
			<?php } else { ?>
			<ol class="epc-od__timeline">
				<?php foreach ($logs as $log) {
					$who = !empty($log['is_robot']) ? 'robot' : (!empty($log['is_manager']) ? 'staff' : 'system');
					?>
				<li>
					<time><?php echo epc_orders_ws_h(date('d.m.Y H:i:s', (int) $log['time'])); ?></time>
					<span class="epc-od__timeline-who"><?php echo epc_orders_ws_h($who); ?></span>
					<div><?php echo $log['text']; ?></div>
				</li>
				<?php } ?>
			</ol>
			<?php } ?>
		</div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="messages">
		<div class="epc-od__chat">
			<div class="epc-od__edit-title">Message to customer</div>
			<p class="text-muted small">Order-wide message, or use “Message customer about item” on a line for price-change / item-specific notes.</p>
			<div id="epc_od_chat_thread" class="epc-od__chat-thread">
				<?php if (!$messages) { ?>
				<div class="text-muted">No messages yet</div>
				<?php } else { foreach ($messages as $m) {
					$cls = !empty($m['is_customer']) ? 'is-customer' : 'is-staff';
					$text = html_entity_decode((string) $m['text'], ENT_QUOTES, 'UTF-8');
					?>
				<div class="epc-od__chat-bubble <?php echo $cls; ?>">
					<time><?php echo epc_orders_ws_h(date('d.m.Y H:i', (int) $m['time'])); ?> · <?php echo !empty($m['is_customer']) ? 'Customer' : 'Staff'; ?></time>
					<div><?php echo nl2br(epc_orders_ws_h($text)); ?></div>
				</div>
				<?php } } ?>
			</div>
			<input type="hidden" id="epc_od_msg_item_id" value="0" />
			<div id="epc_od_msg_item_hint" class="epc-od__msg-hint" style="display:none;"></div>
			<textarea id="epc_od_msg_text" class="form-control" rows="3" placeholder="Write a message to the customer…"></textarea>
			<div class="epc-od__chat-actions">
				<button type="button" class="btn btn-primary btn-sm" onclick="epcOmsSendMessage(<?php echo (int) $order_id; ?>);"><i class="fa fa-paper-plane"></i> Send to customer</button>
				<button type="button" class="btn btn-default btn-sm" onclick="epcOmsClearItemMsg();">Clear item context</button>
			</div>
		</div>
	</section>
</div>
<script>
(function(){
	var root=document.querySelector('.epc-od--oms[data-order-id="<?php echo (int) $order_id; ?>"]');
	if(!root||root.getAttribute('data-tabs-bound')==='1') return;
	root.setAttribute('data-tabs-bound','1');
	var tabs=root.querySelectorAll('[data-epc-od-tab]');
	var panels=root.querySelectorAll('[data-epc-od-panel]');
	tabs.forEach(function(btn){
		btn.addEventListener('click', function(){
			var id=btn.getAttribute('data-epc-od-tab');
			tabs.forEach(function(b){ b.classList.toggle('is-active', b===btn); });
			panels.forEach(function(p){ p.classList.toggle('is-active', p.getAttribute('data-epc-od-panel')===id); });
		});
	});
})();
</script>
