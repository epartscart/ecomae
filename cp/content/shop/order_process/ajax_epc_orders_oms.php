<?php
/**
 * One-window OMS AJAX: update item fields, item status, customer messages.
 * POST/GET: action, order_id, csrf_guard_key, …
 */
header('Content-Type: application/json; charset=utf-8');
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (PDOException $e) {
	echo json_encode(array('status' => false, 'message' => 'DB unavailable'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	echo json_encode(array('status' => false, 'message' => 'Forbidden'));
	exit;
}

$csrf_check_admin = 1;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

$action = trim((string) ($_REQUEST['action'] ?? ''));
$orderId = (int) ($_REQUEST['order_id'] ?? 0);
$adminId = (int) DP_User::getAdminId();

function epc_oms_ok($extra = array())
{
	echo json_encode(array_merge(array('status' => true), $extra), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}
function epc_oms_fail($msg)
{
	echo json_encode(array('status' => false, 'message' => $msg), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

if ($orderId <= 0) {
	epc_oms_fail('Invalid order');
}

$chk = $db_link->prepare('SELECT `id`, `paid`, `user_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1');
$chk->execute(array($orderId));
$order = $chk->fetch(PDO::FETCH_ASSOC);
if (!$order) {
	epc_oms_fail('Order not found');
}

if ($action === 'update_item') {
	$itemId = (int) ($_REQUEST['item_id'] ?? 0);
	if ($itemId <= 0) {
		epc_oms_fail('Invalid item');
	}
	$st = $db_link->prepare('SELECT * FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1');
	$st->execute(array($itemId, $orderId));
	$item = $st->fetch(PDO::FETCH_ASSOC);
	if (!$item) {
		epc_oms_fail('Item not found');
	}
	if ((int) $order['paid'] !== 0) {
		epc_oms_fail('Cannot edit items on a paid order');
	}

	$price = isset($_REQUEST['price']) ? (float) $_REQUEST['price'] : (float) $item['price'];
	$qty = isset($_REQUEST['count_need']) ? (int) $_REQUEST['count_need'] : (int) $item['count_need'];
	$purchase = isset($_REQUEST['t2_price_purchase']) ? (float) $_REQUEST['t2_price_purchase'] : (float) $item['t2_price_purchase'];
	$storageId = isset($_REQUEST['t2_storage_id']) ? (int) $_REQUEST['t2_storage_id'] : (int) $item['t2_storage_id'];
	$name = isset($_REQUEST['t2_name']) ? trim((string) $_REQUEST['t2_name']) : (string) $item['t2_name'];
	$name = str_replace(array("\"", "\\", "'", "\n", "\r", "\t"), '', $name);
	if ($qty < 1) {
		epc_oms_fail('Quantity must be at least 1');
	}
	if ($price <= 0) {
		epc_oms_fail('Price must be greater than 0');
	}

	$db_link->prepare(
		'UPDATE `shop_orders_items` SET `price` = ?, `count_need` = ?, `t2_price_purchase` = ?, `t2_storage_id` = ?, `t2_name` = ? WHERE `id` = ? AND `order_id` = ?'
	)->execute(array($price, $qty, $purchase, $storageId, $name, $itemId, $orderId));

	$log = 'OMS updated item <b>id ' . $itemId . '</b>: price=' . number_format($price, 2, '.', '')
		. ', qty=' . $qty . ', purchase=' . number_format($purchase, 2, '.', '')
		. ', storage_id=' . $storageId;
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array($orderId, time(), $adminId, 1, $log));

	epc_oms_ok(array('item_id' => $itemId));
}

if ($action === 'set_item_status') {
	$itemId = (int) ($_REQUEST['item_id'] ?? 0);
	$status = (int) ($_REQUEST['status'] ?? 0);
	if ($itemId <= 0 || $status <= 0) {
		epc_oms_fail('Invalid item status');
	}
	$st = $db_link->prepare('SELECT `id` FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1');
	$st->execute(array($itemId, $orderId));
	if (!$st->fetchColumn()) {
		epc_oms_fail('Item not found');
	}
	// Delegate to protocol script via include-compatible call isn't clean — update + log here,
	// then rely on existing notifications only when using protocol. Keep simple status set.
	$db_link->prepare('UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ? AND `order_id` = ?')
		->execute(array($status, $itemId, $orderId));
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array(
		$orderId, time(), $adminId, 1,
		'OMS set item <b>id ' . $itemId . '</b> status to ' . $status,
	));
	epc_oms_ok();
}

if ($action === 'send_message') {
	$text = trim((string) ($_REQUEST['text'] ?? ''));
	$itemId = (int) ($_REQUEST['item_id'] ?? 0);
	if ($text === '') {
		epc_oms_fail('Message text is required');
	}
	if ($itemId > 0) {
		$st = $db_link->prepare('SELECT `id`, `t2_article`, `t2_manufacturer`, `t2_name`, `price` FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1');
		$st->execute(array($itemId, $orderId));
		$it = $st->fetch(PDO::FETCH_ASSOC);
		if (!$it) {
			epc_oms_fail('Item not found');
		}
		$prefix = '[Item #' . (int) $it['id'] . ' ' . trim((string) $it['t2_manufacturer'] . ' ' . $it['t2_article']) . '] ';
		$text = $prefix . $text;
	}
	$ok = $db_link->prepare(
		'INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`, `read`) VALUES (?, 0, ?, ?, 0, 0)'
	)->execute(array($orderId, htmlentities($text), time()));
	if (!$ok) {
		epc_oms_fail('Could not send message');
	}
	// Notify customer via existing helper when available.
	try {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/notify_helper.php';
		$templates_query = $db_link->prepare('SELECT `data_value` FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1');
		$templates_query->execute();
		$tpl = $templates_query->fetch(PDO::FETCH_ASSOC);
		$tplData = $tpl ? json_decode((string) $tpl['data_value'], true) : array();
		$bg = !empty($tplData['main_color']) ? $tplData['main_color'] : '#799658';
		$userId = (int) $order['user_id'];
		$linkPath = $userId > 0
			? 'shop/orders/order?order_id=' . $orderId
			: 'shop/orders/zakaz-bez-registracii?order_id=' . $orderId;
		$order_link = '<div style="margin-top:10px;"><a style="background:' . htmlspecialchars($bg, ENT_QUOTES, 'UTF-8') . ';color:#fff;text-decoration:none;padding:7px 13px;border-radius:5px;display:inline-block" target="_blank" href="' . htmlspecialchars($DP_Config->domain_path . $linkPath, ENT_QUOTES, 'UTF-8') . '">Open order</a></div>';
		$notify_vars = array(
			'order_id' => $orderId,
			'order_link' => $order_link,
		);
		$persons = array();
		if ($userId > 0) {
			$persons[] = array('type' => 'user_id', 'user_id' => $userId);
		} else {
			$oq = $db_link->prepare('SELECT `email_not_auth`, `phone_not_auth` FROM `shop_orders` WHERE `id` = ?');
			$oq->execute(array($orderId));
			$or = $oq->fetch(PDO::FETCH_ASSOC);
			if (!empty($or['email_not_auth'])) {
				$persons[] = array('type' => 'email', 'email' => $or['email_not_auth']);
			}
		}
		if ($persons && function_exists('send_notify')) {
			send_notify('order_message_to_customer', $notify_vars, $persons, false);
		}
	} catch (Throwable $e) {
		// message already saved
	}
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array(
		$orderId, time(), $adminId, 1,
		'OMS message to customer' . ($itemId > 0 ? ' (item #' . $itemId . ')' : ''),
	));
	epc_oms_ok();
}

if ($action === 'list_messages') {
	$msgs = array();
	$q = $db_link->prepare('SELECT `id`, `text`, `time`, `is_customer`, `read` FROM `shop_orders_messages` WHERE `order_id` = ? AND `return_id` = 0 ORDER BY `id` ASC');
	$q->execute(array($orderId));
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$msgs[] = array(
			'id' => (int) $row['id'],
			'text' => html_entity_decode((string) $row['text'], ENT_QUOTES, 'UTF-8'),
			'time' => (int) $row['time'],
			'is_customer' => (int) $row['is_customer'],
			'read' => (int) $row['read'],
		);
	}
	epc_oms_ok(array('messages' => $msgs));
}

epc_oms_fail('Unknown action');
