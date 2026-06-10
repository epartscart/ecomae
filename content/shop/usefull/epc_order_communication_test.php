<?php
/**
 * Helpers for epc-order-communication-test.php and CP guide (PHP 5.6+).
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

function epc_comm_test_definitions()
{
	return array(
		array(
			'key' => 'new_order_to_manager',
			'channel' => 'E-mail',
			'trigger' => 'Checkout — new order',
			'recipient' => 'Admin + office managers + CRM',
			'cp_path' => 'Control panel → Notifications',
		),
		array(
			'key' => 'new_order_to_user',
			'channel' => 'E-mail',
			'trigger' => 'Checkout — new order',
			'recipient' => 'Customer (registered or guest e-mail)',
			'cp_path' => 'Control panel → Notifications',
		),
		array(
			'key' => 'lpo_to_supplier',
			'channel' => 'E-mail',
			'trigger' => 'Checkout — new order',
			'recipient' => 'Supplier inbox per warehouse (LPO # = order ID)',
			'cp_path' => 'Logistics → Warehouses → Supplier order email (LPO)',
		),
		array(
			'key' => 'order_message_to_manager',
			'channel' => 'E-mail',
			'trigger' => 'Customer sends message on order page',
			'recipient' => 'Admin + office managers',
			'cp_path' => 'Order card → Messages',
		),
		array(
			'key' => 'order_message_to_customer',
			'channel' => 'E-mail',
			'trigger' => 'Manager replies on order page',
			'recipient' => 'Customer',
			'cp_path' => 'Order card → Messages',
		),
		array(
			'key' => 'order_status_to_manager',
			'channel' => 'E-mail',
			'trigger' => 'Order status changed (if enabled on status)',
			'recipient' => 'Managers',
			'cp_path' => 'Orders → Statuses → order status flags',
		),
		array(
			'key' => 'order_status_to_customer',
			'channel' => 'E-mail',
			'trigger' => 'Order status changed (if enabled on status)',
			'recipient' => 'Customer',
			'cp_path' => 'Orders → Statuses → order status flags',
		),
		array(
			'key' => 'order_item_status_to_manager',
			'channel' => 'E-mail',
			'trigger' => 'Line item status changed (if enabled)',
			'recipient' => 'Managers',
			'cp_path' => 'Orders → Statuses → line item status flags',
		),
		array(
			'key' => 'order_item_status_to_customer',
			'channel' => 'E-mail',
			'trigger' => 'Line item status changed (if enabled)',
			'recipient' => 'Customer',
			'cp_path' => 'Orders → Statuses → line item status flags',
		),
		array(
			'key' => 'order_pay_to_manager',
			'channel' => 'E-mail',
			'trigger' => 'Payment recorded on order',
			'recipient' => 'Managers',
			'cp_path' => 'Order card → Payment',
		),
		array(
			'key' => 'order_pay_to_customer',
			'channel' => 'E-mail',
			'trigger' => 'Payment recorded on order',
			'recipient' => 'Customer',
			'cp_path' => 'Order card → Payment',
		),
		array(
			'key' => 'reg_notify_admin',
			'channel' => 'E-mail',
			'trigger' => 'New customer registration',
			'recipient' => 'Admin / staff',
			'cp_path' => 'Storefront registration',
		),
		array(
			'key' => 'vin_zapros',
			'channel' => 'E-mail',
			'trigger' => 'VIN request form submitted',
			'recipient' => 'Admin',
			'cp_path' => 'VIN request page',
		),
	);
}

function epc_comm_test_last_json_path()
{
	return $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_communication_test_last.json';
}

function epc_comm_test_load_last()
{
	$path = epc_comm_test_last_json_path();
	if (!is_file($path)) {
		return null;
	}
	$data = json_decode((string)file_get_contents($path), true);
	return is_array($data) ? $data : null;
}

function epc_comm_test_save_last($report)
{
	$dir = dirname(epc_comm_test_last_json_path());
	if (!is_dir($dir)) {
		mkdir($dir, 0755, true);
	}
	file_put_contents(
		epc_comm_test_last_json_path(),
		json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT)
	);
}

function epc_comm_notify_row(PDO $db, $name)
{
	$q = $db->prepare('SELECT `id`, `name`, `caption`, `email_on`, `sms_on`, `send_for_not_confirmed` FROM `notifications_settings` WHERE `name` = ? LIMIT 1');
	$q->execute(array($name));
	$row = $q->fetch(PDO::FETCH_ASSOC);
	return $row ? $row : null;
}

function epc_comm_answer_summary($answer, $matchEmail = '')
{
	if (!function_exists('epc_notify_email_status')) {
		return array('ok' => null, 'detail' => 'epc_notify_email_status unavailable');
	}
	$ok = epc_notify_email_status($answer, $matchEmail);
	$detail = '';
	if (is_array($answer) && !empty($answer['persons'])) {
		foreach ($answer['persons'] as $p) {
			if (empty($p['contacts']['email']['tried_to_send'])) {
				continue;
			}
			$em = isset($p['contacts']['email']['value']) ? (string)$p['contacts']['email']['value'] : ('user#' . (isset($p['user_id']) ? $p['user_id'] : ''));
			$detail .= $em . '=' . (!empty($p['contacts']['email']['status']) ? 'sent' : 'FAILED') . '; ';
		}
	}
	if ($detail === '' && is_array($answer)) {
		$detail = isset($answer['message']) ? (string)$answer['message'] : json_encode($answer);
	}
	return array('ok' => $ok, 'detail' => trim($detail));
}

function epc_comm_record_test(&$report, $name, $answer, $matchEmail = '', $extra = array())
{
	$summary = epc_comm_answer_summary($answer, $matchEmail);
	$sent = $summary['ok'];
	if ($sent === null && !empty($extra['log'])) {
		$log = (string)$extra['log'];
		if (preg_match('/: sent\s*$/i', $log)) {
			$sent = true;
		} elseif (preg_match('/: FAILED\s*$/i', $log)) {
			$sent = false;
		}
	}
	$report['tests'][] = array_merge(array(
		'name' => $name,
		'sent' => $sent,
		'detail' => $summary['detail'],
	), $extra);
}

function epc_comm_test_ensure_customer(PDO $db, $DP_Config, $email, $phone)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';

	$email = strtolower(trim($email));
	$q = $db->prepare('SELECT `user_id`, `email`, `email_confirmed`, `unlocked` FROM `users` WHERE LOWER(`email`) = ? LIMIT 1');
	$q->execute(array($email));
	$row = $q->fetch(PDO::FETCH_ASSOC);

	$passwordPlain = 'EpcCommTest2026!';
	$passwordHash = md5($passwordPlain . $DP_Config->secret_succession);
	$created = false;

	if (!$row) {
		$db->prepare(
			'INSERT INTO `users` (`reg_variant`, `email`, `email_confirmed`, `phone`, `phone_confirmed`, `password`, `unlocked`, `time_registered`, `admin_created`)
			 VALUES (1, ?, 1, ?, 1, ?, 1, ?, 1)'
		)->execute(array($email, $phone, $passwordHash, time()));
		$userId = (int)$db->lastInsertId();
		$created = true;
	} else {
		$userId = (int)$row['user_id'];
		$db->prepare('UPDATE `users` SET `email_confirmed` = 1, `unlocked` = 1, `phone` = ?, `phone_confirmed` = 1 WHERE `user_id` = ?')
			->execute(array($phone, $userId));
	}

	epc_trade_profile_set($db, $userId, 'epc_customer_type', 'retail');
	epc_trade_profile_set($db, $userId, 'epc_trade_approval_status', 'approved');
	epc_trade_profile_set($db, $userId, 'epc_dealing_currency', 'AED');
	epc_trade_profile_set($db, $userId, 'name', 'EPC Comm Test Customer');
	epc_trade_profile_set($db, $userId, 'surname', 'Automated');

	$groups = $db->query('SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
	if (!$groups) {
		$groups = $db->query('SELECT `id` FROM `groups` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
	}
	if ($groups) {
		$db->prepare('DELETE FROM `users_groups_bind` WHERE `user_id` = ?')->execute(array($userId));
		$db->prepare('INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)')->execute(array($userId, (int)$groups));
	}

	return array(
		'user_id' => $userId,
		'email' => $email,
		'phone' => $phone,
		'password' => $passwordPlain,
		'created' => $created,
		'trade_status' => epc_trade_approval_status($db, $userId),
	);
}

function epc_comm_test_create_order(PDO $db, $userId, $officeId, $storageIds)
{
	$statusQ = $db->query('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_created` = 1 ORDER BY `order` ASC LIMIT 1');
	$orderStatus = (int)$statusQ->fetchColumn();
	if ($orderStatus <= 0) {
		throw new Exception('No for_created order status');
	}
	$itemStatusQ = $db->query('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_created` = 1 ORDER BY `order` ASC LIMIT 1');
	$itemStatus = (int)$itemStatusQ->fetchColumn();
	if ($itemStatus <= 0) {
		throw new Exception('No for_created item status');
	}

	$db->prepare(
		'INSERT INTO `shop_orders`
		(`user_id`, `session_id`, `time`, `successfully_created`, `status`, `paid`, `how_get`, `how_get_json`, `phone_not_auth`, `email_not_auth`, `office_id`)
		VALUES (?, ?, ?, 1, ?, 0, 1, ?, ?, ?, ?)'
	)->execute(array($userId, 0, time(), $orderStatus, '{}', '', '', $officeId));
	$orderId = (int)$db->lastInsertId();

	$storageNames = array();
	$sn = $db->query('SELECT `id`, `name` FROM `shop_storages`');
	while ($s = $sn->fetch(PDO::FETCH_ASSOC)) {
		$storageNames[(int)$s['id']] = (string)$s['name'];
	}

	$lines = array();
	$n = 0;
	foreach ($storageIds as $storageId) {
		$storageId = (int)$storageId;
		if ($storageId <= 0) {
			continue;
		}
		$n++;
		$brand = 'TESTBRAND';
		$article = 'EPCCOMM' . $n;
		$whLabel = isset($storageNames[$storageId]) ? $storageNames[$storageId] : ('WH' . $storageId);
		$name = 'EPC communication test part ' . $n . ' (' . $whLabel . ')';
		$db->prepare(
			'INSERT INTO `shop_orders_items`
			(`order_id`, `product_type`, `price`, `count_need`, `product_id`, `status`,
			 `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`, `t2_time_to_exe`, `t2_time_to_exe_guaranteed`,
			 `t2_storage`, `t2_min_order`, `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`, `t2_product_json`, `sao_state`, `sao_robot`, `t2_json_params`)
			VALUES (?, 2, ?, 1, 0, ?, ?, ?, ?, ?, 10, 1, 1, ?, 1, 100, 0, ?, ?, ?, \'\', 0, 0, \'\')'
		)->execute(array(
			$orderId,
			99.50 + $n,
			$itemStatus,
			$brand,
			$article,
			$article,
			$name,
			$whLabel,
			50.00 + $n,
			$officeId,
			$storageId,
		));
		$itemId = (int)$db->lastInsertId();
		$db->prepare(
			'INSERT INTO `shop_orders_items_details`
			(`order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`)
			VALUES (?, ?, ?, ?, 0, 1, 0, 0, ?)'
		)->execute(array($orderId, $itemId, $officeId, $storageId, 50.00 + $n));
		$lines[] = array(
			'item_id' => $itemId,
			'storage_id' => $storageId,
			'storage_name' => isset($storageNames[$storageId]) ? $storageNames[$storageId] : '',
			'article' => $article,
		);
	}

	$db->prepare('INSERT INTO `shop_orders_logs` (`order_id`, `time`, `user_id`, `is_manager`, `text`, `is_robot`) VALUES (?, ?, 0, 0, ?, 1)')
		->execute(array($orderId, time(), 'EPC communication test order created (safe to delete after review).'));

	return array('order_id' => $orderId, 'lines' => $lines, 'office_id' => $officeId);
}
