<?php
/**
 * Live snapshot for CP order fulfilment guide (PHP 5.6+).
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

function epc_order_guide_snapshot(PDO $db, $config)
{
	$backend = isset($config['backend_dir']) ? (string)$config['backend_dir'] : 'cp';
	$domain = rtrim((string)($config['domain_path'] ?? ''), '/');

	$notifications = array();
	try {
		$nq = $db->query(
			"SELECT `id`, `name`, `caption`, `email_on`, `sms_on`, `email_subject`
			 FROM `notifications_settings`
			 WHERE `name` IN ('new_order_to_manager','new_order_to_user','lpo_to_supplier')
			 ORDER BY FIELD(`name`, 'new_order_to_manager','new_order_to_user','lpo_to_supplier')"
		);
		while ($row = $nq->fetch(PDO::FETCH_ASSOC)) {
			$notifications[] = $row;
		}
	} catch (Exception $e) {
		$notifications = array();
	}

	$storages = array();
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_supplier_notifications.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_supplier_notifications.php';
	}
	try {
		$sq = $db->query(
			'SELECT s.`id`, s.`name`, s.`interface_type`, s.`connection_options`, p.`name` AS `price_list`, p.`sender_email`
			 FROM `shop_storages` s
			 LEFT JOIN `shop_docpart_prices` p ON p.`id` = CAST(JSON_UNQUOTE(JSON_EXTRACT(s.`connection_options`, "$.price_id")) AS UNSIGNED)
			 ORDER BY s.`id`'
		);
		while ($row = $sq->fetch(PDO::FETCH_ASSOC)) {
			$sid = (int)$row['id'];
			$resolved = function_exists('epc_storage_supplier_order_email')
				? epc_storage_supplier_order_email($db, $sid)
				: '';
			$opts = json_decode((string)($row['connection_options'] ?? ''), true);
			$storages[] = array(
				'id' => $sid,
				'name' => (string)$row['name'],
				'price_list' => (string)($row['price_list'] ?? ''),
				'order_email' => is_array($opts) ? trim((string)($opts['order_email'] ?? '')) : '',
				'price_sender_email' => trim((string)($row['sender_email'] ?? '')),
				'resolved_order_email' => $resolved,
				'lpo_ready' => $resolved !== '',
			);
		}
	} catch (Exception $e) {
		$storages = array();
	}

	$orderStats = array('total' => 0, 'today' => 0, 'last_7_days' => 0);
	try {
		$orderStats['total'] = (int)$db->query('SELECT COUNT(*) FROM `shop_orders`')->fetchColumn();
		$orderStats['today'] = (int)$db->query('SELECT COUNT(*) FROM `shop_orders` WHERE `time` >= UNIX_TIMESTAMP(CURDATE())')->fetchColumn();
		$orderStats['last_7_days'] = (int)$db->query('SELECT COUNT(*) FROM `shop_orders` WHERE `time` >= UNIX_TIMESTAMP(DATE_SUB(CURDATE(), INTERVAL 7 DAY))')->fetchColumn();
	} catch (Exception $e) {
	}

	$pendingApprovals = 0;
	try {
		$pendingApprovals = (int)$db->query(
			"SELECT COUNT(DISTINCT `user_id`) FROM `users_profiles`
			 WHERE `data_key` = 'epc_trade_approval_status' AND `data_value` = 'pending'"
		)->fetchColumn();
	} catch (Exception $e) {
	}

	$recentLpoLogs = array();
	try {
		$lq = $db->query(
			"SELECT l.`order_id`, l.`text`, l.`time`
			 FROM `shop_orders_logs` l
			 WHERE l.`text` LIKE '%Supplier LPO%'
			 ORDER BY l.`id` DESC LIMIT 8"
		);
		while ($row = $lq->fetch(PDO::FETCH_ASSOC)) {
			$recentLpoLogs[] = $row;
		}
	} catch (Exception $e) {
	}

	$orderStatuses = array();
	try {
		$os = $db->query('SELECT `id`, `name`, `for_created`, `for_paid`, `for_finish`, `to_manager_email`, `to_customer_email` FROM `shop_orders_statuses_ref` ORDER BY `order` ASC');
		while ($row = $os->fetch(PDO::FETCH_ASSOC)) {
			$orderStatuses[] = $row;
		}
	} catch (Exception $e) {
	}

	$itemStatuses = array();
	try {
		$is = $db->query('SELECT `id`, `name`, `for_created`, `for_finish`, `to_manager_email`, `to_customer_email` FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC LIMIT 20');
		while ($row = $is->fetch(PDO::FETCH_ASSOC)) {
			$itemStatuses[] = $row;
		}
	} catch (Exception $e) {
	}

	$lpoReadyCount = 0;
	foreach ($storages as $s) {
		if (!empty($s['lpo_ready'])) {
			$lpoReadyCount++;
		}
	}

	return array(
		'generated_at' => date('Y-m-d H:i:s'),
		'backend' => $backend,
		'domain' => $domain,
		'notifications' => $notifications,
		'storages' => $storages,
		'storages_lpo_ready' => $lpoReadyCount,
		'storages_total' => count($storages),
		'order_stats' => $orderStats,
		'pending_trade_approvals' => $pendingApprovals,
		'recent_lpo_logs' => $recentLpoLogs,
		'order_statuses' => $orderStatuses,
		'item_statuses' => $itemStatuses,
		'checklist' => epc_order_guide_checklist($backend),
	);
}

function epc_order_guide_checklist($backend)
{
	return array(
		array(
			'step' => 'Retail / wholesale registration',
			'where' => '/' . $backend . '/users/customer_approvals',
			'test' => 'Register test wholesale user → status Pending → approve + set dealing currency',
		),
		array(
			'step' => 'Checkout blocked until approved',
			'where' => 'Storefront checkout',
			'test' => 'Pending user can browse/cart but cannot complete checkout',
		),
		array(
			'step' => 'Place test order',
			'where' => 'Storefront → cart → checkout',
			'test' => 'Order appears in CP orders list; customer redirected to order page',
		),
		array(
			'step' => 'Manager e-mail (new_order_to_manager)',
			'where' => '/' . $backend . '/control/notifications',
			'test' => 'Admin + office managers receive order summary with CP link',
		),
		array(
			'step' => 'Customer e-mail (new_order_to_user)',
			'where' => 'Same notification settings',
			'test' => 'Customer inbox receives confirmation with line items',
		),
		array(
			'step' => 'Supplier LPO (lpo_to_supplier)',
			'where' => '/' . $backend . '/shop/logistics/storages',
			'test' => 'One e-mail per warehouse with lines; LPO # = order ID; log on order card',
		),
		array(
			'step' => 'Process in CP',
			'where' => '/' . $backend . '/shop/orders/order?order_id=',
			'test' => 'Change line statuses, record payment, message customer',
		),
	);
}
