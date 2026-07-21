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

/**
 * Resolve price_list id for a warehouse from connection_options.price_id.
 */
function epc_oms_storage_price_id(PDO $db, int $storageId): int
{
	if ($storageId <= 0) {
		return 0;
	}
	try {
		$st = $db->prepare('SELECT `connection_options` FROM `shop_storages` WHERE `id` = ? LIMIT 1');
		$st->execute(array($storageId));
		$opts = json_decode((string) $st->fetchColumn(), true);
		return is_array($opts) ? (int) ($opts['price_id'] ?? 0) : 0;
	} catch (Throwable $e) {
		return 0;
	}
}

function epc_oms_norm_article(string $article): string
{
	$match = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
	if (is_file($match)) {
		require_once $match;
		if (function_exists('docpart_normalize_article_for_price')) {
			return (string) docpart_normalize_article_for_price($article);
		}
	}
	return mb_strtoupper(preg_replace('/[^a-zA-Z0-9А-Яа-яёЁ]+/ui', '', $article), 'UTF-8');
}

/**
 * Lookup warehouse base price + customer sell for brand/article on a storage.
 *
 * @return array<string,mixed>|null
 */
function epc_oms_lookup_warehouse_offer(PDO $db, int $storageId, string $brand, string $article, int $customerUserId): ?array
{
	$priceId = epc_oms_storage_price_id($db, $storageId);
	if ($priceId <= 0 || trim($article) === '') {
		return null;
	}
	$artNorm = epc_oms_norm_article($article);
	$brandNorm = mb_strtoupper(trim($brand), 'UTF-8');
	$row = null;
	try {
		// Prefer exact brand + article on this warehouse price list.
		$st = $db->prepare(
			'SELECT `manufacturer`, `article`, `article_show`, `name`, `price`, `exist`
			 FROM `shop_docpart_prices_data`
			 WHERE `price_id` = ?
			   AND (
					REPLACE(REPLACE(REPLACE(UPPER(`article`), \'-\', \'\'), \' \', \'\'), \'.\', \'\') = ?
				 OR REPLACE(REPLACE(REPLACE(UPPER(`article_show`), \'-\', \'\'), \' \', \'\'), \'.\', \'\') = ?
			   )
			 ORDER BY
				CASE WHEN UPPER(TRIM(`manufacturer`)) = ? THEN 0 ELSE 1 END,
				`price` ASC
			 LIMIT 1'
		);
		$st->execute(array($priceId, $artNorm, $artNorm, $brandNorm));
		$row = $st->fetch(PDO::FETCH_ASSOC) ?: null;
	} catch (Throwable $e) {
		$row = null;
	}
	if (!$row) {
		return null;
	}
	$purchase = round((float) ($row['price'] ?? 0), 2);
	if ($purchase <= 0) {
		return null;
	}
	$offerBrand = trim((string) ($row['manufacturer'] ?? $brand));
	$offerArticle = trim((string) ($row['article_show'] ?? $row['article'] ?? $article));
	$offerName = trim((string) ($row['name'] ?? ''));
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';
	$groupId = function_exists('epc_pricing_resolve_customer_group_id')
		? epc_pricing_resolve_customer_group_id($db, $customerUserId, 0)
		: 0;
	if ($groupId <= 0) {
		// Fallback: retail profile, then guest.
		try {
			$groupId = (int) $db->query("SELECT `group_id` FROM `epc_price_profiles` WHERE `code` = 'retail' LIMIT 1")->fetchColumn();
		} catch (Throwable $e) {
			$groupId = 0;
		}
		if ($groupId <= 0) {
			try {
				$groupId = (int) $db->query('SELECT `id` FROM `groups` WHERE `for_guests` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
			} catch (Throwable $e) {
				$groupId = 0;
			}
		}
	}
	$sell = $purchase;
	$markupPct = 0;
	if (function_exists('epc_pricing_apply_sell_from_purchase') && $groupId > 0) {
		$priced = epc_pricing_apply_sell_from_purchase($db, $groupId, $offerBrand, $purchase, $offerArticle, (int) $storageId);
		if (!empty($priced['visible'])) {
			$sell = round((float) $priced['price'], 2);
			$markupPct = (int) ($priced['markup_percent'] ?? 0);
		}
	}
	$storageCaption = '';
	try {
		$stCap = $db->prepare('SELECT COALESCE(NULLIF(TRIM(`short_name`), \'\'), `name`) FROM `shop_storages` WHERE `id` = ? LIMIT 1');
		$stCap->execute(array($storageId));
		$storageCaption = (string) $stCap->fetchColumn();
	} catch (Throwable $e) {
	}
	return array(
		'storage_id' => $storageId,
		'storage' => $storageCaption,
		'price_id' => $priceId,
		'manufacturer' => $offerBrand,
		'article' => epc_oms_norm_article($offerArticle) !== '' ? epc_oms_norm_article($offerArticle) : $offerArticle,
		'article_show' => $offerArticle,
		'name' => $offerName,
		'purchase' => $purchase,
		'price' => $sell,
		'markup_percent' => $markupPct,
		'exist' => (int) ($row['exist'] ?? 0),
		'group_id' => $groupId,
	);
}

function epc_oms_storage_caption(PDO $db, int $storageId): string
{
	if ($storageId <= 0) {
		return '';
	}
	try {
		$stCap = $db->prepare('SELECT COALESCE(NULLIF(TRIM(`short_name`), \'\'), `name`) FROM `shop_storages` WHERE `id` = ? LIMIT 1');
		$stCap->execute(array($storageId));
		return (string) $stCap->fetchColumn();
	} catch (Throwable $e) {
		return '';
	}
}

if ($action === 'lookup_warehouse_price') {
	$storageId = (int) ($_REQUEST['t2_storage_id'] ?? $_REQUEST['storage_id'] ?? 0);
	$brand = trim((string) ($_REQUEST['t2_manufacturer'] ?? $_REQUEST['brand'] ?? ''));
	$article = trim((string) ($_REQUEST['t2_article'] ?? $_REQUEST['article'] ?? ''));
	if ($storageId <= 0) {
		epc_oms_fail('Choose a warehouse');
	}
	if ($article === '') {
		epc_oms_fail('Article is required for warehouse price lookup');
	}
	$offer = epc_oms_lookup_warehouse_offer($db_link, $storageId, $brand, $article, (int) ($order['user_id'] ?? 0));
	if ($offer === null) {
		epc_oms_fail('No price found for this brand/article on the selected warehouse');
	}
	epc_oms_ok(array('offer' => $offer));
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
	// Paid orders stay editable for warehouse / alternative fulfillment amendments.

	$price = isset($_REQUEST['price']) ? (float) $_REQUEST['price'] : (float) $item['price'];
	$qty = isset($_REQUEST['count_need']) ? (int) $_REQUEST['count_need'] : (int) $item['count_need'];
	$purchase = isset($_REQUEST['t2_price_purchase']) ? (float) $_REQUEST['t2_price_purchase'] : (float) $item['t2_price_purchase'];
	$storageId = isset($_REQUEST['t2_storage_id']) ? (int) $_REQUEST['t2_storage_id'] : (int) $item['t2_storage_id'];
	$name = isset($_REQUEST['t2_name']) ? trim((string) $_REQUEST['t2_name']) : (string) $item['t2_name'];
	$brand = isset($_REQUEST['t2_manufacturer']) ? trim((string) $_REQUEST['t2_manufacturer']) : (string) $item['t2_manufacturer'];
	$article = isset($_REQUEST['t2_article']) ? trim((string) $_REQUEST['t2_article']) : (string) $item['t2_article'];
	$articleShow = isset($_REQUEST['t2_article_show']) ? trim((string) $_REQUEST['t2_article_show']) : (string) ($item['t2_article_show'] ?? $article);
	$sanitize = static function ($v) {
		return str_replace(array("\"", "\\", "'", "\n", "\r", "\t"), '', (string) $v);
	};
	$name = $sanitize($name);
	$brand = $sanitize($brand);
	$article = $sanitize($article);
	$articleShow = $sanitize($articleShow !== '' ? $articleShow : $article);
	if ($qty < 1) {
		epc_oms_fail('Quantity must be at least 1');
	}
	if ($price <= 0) {
		epc_oms_fail('Price must be greater than 0');
	}
	if ($brand === '' || $article === '') {
		epc_oms_fail('Brand and article number are required');
	}

	// Optional: reprice from chosen warehouse when requested.
	$reprice = !empty($_REQUEST['reprice_from_warehouse']) || !empty($_REQUEST['apply_warehouse_price']);
	if ($reprice && $storageId > 0) {
		$offer = epc_oms_lookup_warehouse_offer($db_link, $storageId, $brand, $article, (int) ($order['user_id'] ?? 0));
		if ($offer === null) {
			epc_oms_fail('No price found for this brand/article on the selected warehouse');
		}
		$purchase = (float) $offer['purchase'];
		$price = (float) $offer['price'];
		if ($name === '' && !empty($offer['name'])) {
			$name = (string) $offer['name'];
		}
		if (!empty($offer['article_show'])) {
			$articleShow = (string) $offer['article_show'];
		}
		if (!empty($offer['manufacturer'])) {
			$brand = (string) $offer['manufacturer'];
		}
	}

	$storageCaption = epc_oms_storage_caption($db_link, $storageId);

	// Preserve original request when staff applies an alternative.
	$jsonParams = (string) ($item['t2_json_params'] ?? '');
	$meta = array();
	if ($jsonParams !== '') {
		$decoded = json_decode($jsonParams, true);
		if (is_array($decoded)) {
			$meta = $decoded;
		}
	}
	$origBrand = (string) ($item['t2_manufacturer'] ?? '');
	$origArticle = (string) ($item['t2_article'] ?? '');
	$isAlt = (!empty($_REQUEST['offer_alternative']) || ($brand !== $origBrand || $article !== $origArticle));
	if ($isAlt) {
		if (empty($meta['requested_manufacturer'])) {
			$meta['requested_manufacturer'] = $origBrand;
		}
		if (empty($meta['requested_article'])) {
			$meta['requested_article'] = $origArticle;
		}
		$meta['offer_alternative'] = 1;
		$meta['alt_manufacturer'] = $brand;
		$meta['alt_article'] = $article;
		$meta['alt_storage_id'] = $storageId;
	}
	$jsonOut = $meta !== array() ? json_encode($meta, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) : $jsonParams;

	$db_link->prepare(
		'UPDATE `shop_orders_items` SET `price` = ?, `count_need` = ?, `t2_price_purchase` = ?, `t2_storage_id` = ?, `t2_storage` = ?, `t2_name` = ?, `t2_manufacturer` = ?, `t2_article` = ?, `t2_article_show` = ?, `t2_json_params` = ? WHERE `id` = ? AND `order_id` = ?'
	)->execute(array($price, $qty, $purchase, $storageId, $storageCaption, $name, $brand, $article, $articleShow, $jsonOut, $itemId, $orderId));

	try {
		$db_link->prepare(
			'UPDATE `shop_orders_items_details` SET `storage_id` = ? WHERE `order_item_id` = ? AND `order_id` = ?'
		)->execute(array($storageId, $itemId, $orderId));
	} catch (Throwable $e) {
	}

	$log = 'OMS updated item <b>id ' . $itemId . '</b>: ' . $brand . ' / ' . $article
		. ($isAlt ? ' (alternative)' : '')
		. ', price=' . number_format($price, 2, '.', '')
		. ', qty=' . $qty . ', purchase=' . number_format($purchase, 2, '.', '')
		. ', storage_id=' . $storageId;
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array($orderId, time(), $adminId, 1, $log));

	epc_oms_ok(array(
		'item_id' => $itemId,
		'price' => $price,
		'purchase' => $purchase,
		't2_storage' => $storageCaption,
		'alternative' => $isAlt ? 1 : 0,
	));
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

if ($action === 'set_courier') {
	if ((int) $order['paid'] !== 0) {
		epc_oms_fail('Cannot change courier on a paid order');
	}
	$fee = isset($_REQUEST['delivery_price']) ? (float) $_REQUEST['delivery_price'] : 0.0;
	if ($fee < 0) {
		epc_oms_fail('Courier fee cannot be negative');
	}
	$country = strtoupper(substr(trim((string) ($_REQUEST['country'] ?? '')), 0, 2));
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_order_courier_vat.php';
	$extra = array();
	if (strlen($country) === 2) {
		$extra['country'] = $country;
	}
	$how = epc_order_set_courier_charge($db_link, $orderId, $fee, $extra);
	$full = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `id` = ? LIMIT 1');
	$full->execute(array($orderId));
	$orderRow = $full->fetch(PDO::FETCH_ASSOC) ?: array('id' => $orderId, 'user_id' => $order['user_id'], 'how_get_json' => json_encode($how));
	$calc = epc_order_courier_vat_amounts($db_link, $orderRow, (int) $order['user_id']);
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array(
		$orderId,
		time(),
		$adminId,
		1,
		'OMS set courier fee (customer pays) ex-VAT=' . number_format($fee, 2, '.', '')
			. ' AED, ship=' . (string) ($calc['destination_country'] ?? '')
			. ', VAT=' . number_format((float) $calc['vat_amount'], 2, '.', ''),
	));
	epc_oms_ok(array(
		'delivery_price' => (float) $how['delivery_price'],
		'courier' => $calc,
	));
}

if ($action === 'erp_document_map') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_order_courier_vat.php';
	try {
		$map = epc_order_erp_document_map($db_link, $orderId);
	} catch (Throwable $e) {
		epc_oms_fail($e->getMessage());
	}
	epc_oms_ok(array('map' => $map));
}

if ($action === 'refresh_item_cost') {
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
	// Prefer warehouse price-list cost + customer sell when storage/brand/article are set.
	$storageId = (int) ($item['t2_storage_id'] ?? 0);
	$brand = (string) ($item['t2_manufacturer'] ?? '');
	$article = (string) ($item['t2_article_show'] ?? $item['t2_article'] ?? '');
	$offer = ($storageId > 0 && $article !== '')
		? epc_oms_lookup_warehouse_offer($db_link, $storageId, $brand, $article, (int) ($order['user_id'] ?? 0))
		: null;
	if ($offer !== null) {
		$db_link->prepare(
			'UPDATE `shop_orders_items` SET `t2_price_purchase` = ?, `price` = ? WHERE `id` = ? AND `order_id` = ?'
		)->execute(array((float) $offer['purchase'], (float) $offer['price'], $itemId, $orderId));
		$db_link->prepare(
			'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
		)->execute(array(
			$orderId, time(), $adminId, 1,
			'OMS refreshed warehouse price for item <b>id ' . $itemId . '</b>: purchase='
				. number_format((float) $offer['purchase'], 2, '.', '')
				. ' sell=' . number_format((float) $offer['price'], 2, '.', '')
				. ' (storage ' . $storageId . ')',
		));
		epc_oms_ok(array(
			'item_id' => $itemId,
			'purchase' => (float) $offer['purchase'],
			'price' => (float) $offer['price'],
			'source' => 'warehouse_price_list',
		));
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_order_supplier_fulfillment.php';
	$eff = epc_order_item_effective_purchase($db_link, $item);
	$unit = (float) $eff['unit'];
	if ($unit <= 0) {
		epc_oms_fail('No purchase cost found for this line');
	}
	$db_link->prepare(
		'UPDATE `shop_orders_items` SET `t2_price_purchase` = ? WHERE `id` = ? AND `order_id` = ?'
	)->execute(array($unit, $itemId, $orderId));
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array(
		$orderId, time(), $adminId, 1,
		'OMS refreshed purchase cost for item <b>id ' . $itemId . '</b>: '
			. number_format($unit, 2, '.', '') . ' AED (source ' . (string) $eff['source'] . ')',
	));
	epc_oms_ok(array('item_id' => $itemId, 'purchase' => $unit, 'source' => $eff['source']));
}

if ($action === 'supplier_fulfillment_status') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_order_supplier_fulfillment.php';
	try {
		$r = epc_order_supplier_fulfillment_bootstrap($db_link, $orderId, $adminId);
	} catch (Throwable $e) {
		epc_oms_fail($e->getMessage());
	}
	epc_oms_ok(array('fulfillment' => $r));
}

if ($action === 'supplier_fulfillment_set_stage') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_order_supplier_fulfillment.php';
	$key = trim((string) ($_REQUEST['supplier_key'] ?? ''));
	$stage = trim((string) ($_REQUEST['stage'] ?? ''));
	if ($key === '' || $stage === '') {
		epc_oms_fail('supplier_key and stage required');
	}
	try {
		$r = epc_order_supplier_fulfillment_set_stage($db_link, $orderId, $key, $stage, $adminId);
	} catch (Throwable $e) {
		epc_oms_fail($e->getMessage());
	}
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array(
		$orderId, time(), $adminId, 1,
		'OMS supplier fulfillment <b>' . htmlspecialchars($key, ENT_QUOTES, 'UTF-8') . '</b> → ' . htmlspecialchars($stage, ENT_QUOTES, 'UTF-8'),
	));
	epc_oms_ok(array('fulfillment' => $r));
}

if ($action === 'supplier_fulfillment_advance') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_order_supplier_fulfillment.php';
	$key = trim((string) ($_REQUEST['supplier_key'] ?? ''));
	if ($key === '') {
		epc_oms_fail('supplier_key required');
	}
	try {
		$r = epc_order_supplier_fulfillment_advance($db_link, $orderId, $key, $adminId);
	} catch (Throwable $e) {
		epc_oms_fail($e->getMessage());
	}
	$stage = '';
	foreach ($r['suppliers'] ?? array() as $s) {
		if (($s['supplier_key'] ?? '') === $key) {
			$stage = (string) ($s['stage'] ?? '');
			break;
		}
	}
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array(
		$orderId, time(), $adminId, 1,
		'OMS advanced supplier fulfillment <b>' . htmlspecialchars($key, ENT_QUOTES, 'UTF-8') . '</b> → ' . htmlspecialchars($stage, ENT_QUOTES, 'UTF-8'),
	));
	epc_oms_ok(array('fulfillment' => $r));
}


if ($action === 'update_items') {
	$raw = $_REQUEST['items'] ?? '[]';
	if (is_string($raw)) {
		$itemsIn = json_decode($raw, true);
	} else {
		$itemsIn = $raw;
	}
	if (!is_array($itemsIn) || !$itemsIn) {
		epc_oms_fail('No items to update');
	}
	$sanitize = static function ($v) {
		return str_replace(array("\"", "\\", "'", "\n", "\r", "\t"), '', (string) $v);
	};
	$updated = 0;
	foreach ($itemsIn as $row) {
		if (!is_array($row)) {
			continue;
		}
		$itemId = (int) ($row['item_id'] ?? 0);
		if ($itemId <= 0) {
			continue;
		}
		$st = $db_link->prepare('SELECT * FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1');
		$st->execute(array($itemId, $orderId));
		$item = $st->fetch(PDO::FETCH_ASSOC);
		if (!$item) {
			continue;
		}
		$price = isset($row['price']) ? (float) $row['price'] : (float) $item['price'];
		$qty = isset($row['count_need']) ? (int) $row['count_need'] : (int) $item['count_need'];
		$purchase = isset($row['t2_price_purchase']) ? (float) $row['t2_price_purchase'] : (float) $item['t2_price_purchase'];
		$storageId = isset($row['t2_storage_id']) ? (int) $row['t2_storage_id'] : (int) $item['t2_storage_id'];
		$name = isset($row['t2_name']) ? $sanitize($row['t2_name']) : (string) $item['t2_name'];
		$brand = isset($row['t2_manufacturer']) ? $sanitize($row['t2_manufacturer']) : (string) $item['t2_manufacturer'];
		$article = isset($row['t2_article']) ? $sanitize($row['t2_article']) : (string) $item['t2_article'];
		$articleShow = isset($row['t2_article_show']) ? $sanitize($row['t2_article_show']) : (string) ($item['t2_article_show'] ?? $article);
		if ($qty < 1 || $price <= 0 || $brand === '' || $article === '') {
			continue;
		}
		if (!empty($row['reprice_from_warehouse']) && $storageId > 0) {
			$offer = epc_oms_lookup_warehouse_offer($db_link, $storageId, $brand, $article, (int) ($order['user_id'] ?? 0));
			if ($offer !== null) {
				$purchase = (float) $offer['purchase'];
				$price = (float) $offer['price'];
				if ($name === '' && !empty($offer['name'])) {
					$name = (string) $offer['name'];
				}
				if (!empty($offer['article_show'])) {
					$articleShow = (string) $offer['article_show'];
				}
			}
		}
		$storageCaption = epc_oms_storage_caption($db_link, $storageId);
		$db_link->prepare(
			'UPDATE `shop_orders_items` SET `price` = ?, `count_need` = ?, `t2_price_purchase` = ?, `t2_storage_id` = ?, `t2_storage` = ?, `t2_name` = ?, `t2_manufacturer` = ?, `t2_article` = ?, `t2_article_show` = ? WHERE `id` = ? AND `order_id` = ?'
		)->execute(array($price, $qty, $purchase, $storageId, $storageCaption, $name, $brand, $article, $articleShow !== '' ? $articleShow : $article, $itemId, $orderId));
		try {
			$db_link->prepare(
				'UPDATE `shop_orders_items_details` SET `storage_id` = ? WHERE `order_item_id` = ? AND `order_id` = ?'
			)->execute(array($storageId, $itemId, $orderId));
		} catch (Throwable $e) {
		}
		$updated++;
	}
	if ($updated <= 0) {
		epc_oms_fail('No lines updated');
	}
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array($orderId, time(), $adminId, 1, 'OMS batch-updated <b>' . $updated . '</b> line(s)'));
	epc_oms_ok(array('updated' => $updated));
}

if ($action === 'set_items_status') {
	$status = (int) ($_REQUEST['status'] ?? 0);
	if ($status <= 0) {
		epc_oms_fail('Invalid status');
	}
	$raw = $_REQUEST['item_ids'] ?? '[]';
	$ids = is_string($raw) ? json_decode($raw, true) : $raw;
	if (!is_array($ids) || !$ids) {
		// all lines on order
		$q = $db_link->prepare('SELECT `id` FROM `shop_orders_items` WHERE `order_id` = ?');
		$q->execute(array($orderId));
		$ids = array();
		while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
			$ids[] = (int) $r['id'];
		}
	}
	$ids = array_values(array_unique(array_filter(array_map('intval', $ids))));
	if (!$ids) {
		epc_oms_fail('No items');
	}
	$ph = implode(',', array_fill(0, count($ids), '?'));
	$args = array_merge(array($status), $ids, array($orderId));
	$db_link->prepare("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` IN ($ph) AND `order_id` = ?")->execute($args);
	$db_link->prepare(
		'INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,0)'
	)->execute(array(
		$orderId, time(), $adminId, 1,
		'OMS set status ' . $status . ' on <b>' . count($ids) . '</b> line(s)',
	));
	epc_oms_ok(array('updated' => count($ids), 'status' => $status));
}

epc_oms_fail('Unknown action');
