<?php
/**
 * End-to-end order communication test: test customer, test order, all e-mail channels.
 * GET: token, key, [run=1], [email=786yawer@gmail.com], [dry_run=1]
 */
header('Content-Type: application/json; charset=utf-8');

$token = isset($_GET['token']) ? $_GET['token'] : '';
if ($token !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/users/dp_user.php';
require_once __DIR__ . '/content/notifications/notify_helper.php';
require_once __DIR__ . '/content/shop/usefull/epc_admin_notifications.php';
require_once __DIR__ . '/content/shop/usefull/epc_supplier_notifications.php';
require_once __DIR__ . '/content/shop/usefull/epc_order_communication_test.php';

$DP_Config = new DP_Config;
$key = isset($_GET['key']) ? (string)$_GET['key'] : '';
if ($key !== $DP_Config->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
	);
} catch (Exception $e) {
	exit(json_encode(array('ok' => false, 'error' => $e->getMessage())));
}

$db_link = $db;

if (!isset($_SERVER['HTTP_REFERER']) || $_SERVER['HTTP_REFERER'] === '') {
	$_SERVER['HTTP_REFERER'] = rtrim($DP_Config->domain_path, '/') . '/en/';
}

require_once __DIR__ . '/lang/dp_lang.php';
$multilang_params = multilang_init();

$dryRun = !empty($_GET['dry_run']);
$run = !empty($_GET['run']);
$testEmail = strtolower(trim(isset($_GET['email']) ? (string)$_GET['email'] : '786yawer@gmail.com'));
$testPhone = trim(isset($_GET['phone']) ? (string)$_GET['phone'] : '+971500000001');

$report = array(
	'ok' => true,
	'dry_run' => $dryRun,
	'run' => $run,
	'generated_at' => date('Y-m-d H:i:s'),
	'test_customer' => null,
	'test_order' => null,
	'smtp' => array(
		'mode' => (int)$DP_Config->smtp_mode,
		'host' => (string)$DP_Config->smtp_host,
		'from' => (string)$DP_Config->from_email,
		'admin_inbox' => epc_admin_notify_email(),
	),
	'notification_catalog' => array(),
	'tests' => array(),
	'order_logs' => array(),
	'cp_links' => array(
		'guide' => '/' . $DP_Config->backend_dir . '/shop/orders/guide',
		'orders' => '/' . $DP_Config->backend_dir . '/shop/orders/orders',
		'notifications' => '/' . $DP_Config->backend_dir . '/control/notifications',
	),
);

foreach (epc_comm_test_definitions() as $def) {
	$row = epc_comm_notify_row($db, $def['key']);
	$report['notification_catalog'][] = array_merge($def, array(
		'registered' => $row !== null,
		'email_on' => $row ? (int)$row['email_on'] : null,
		'sms_on' => $row ? (int)$row['sms_on'] : null,
	));
}

$storages = $db->query('SELECT `id`, `name` FROM `shop_storages` ORDER BY `id` LIMIT 5')->fetchAll();
$storageIds = array();
foreach ($storages as $s) {
	$storageIds[] = (int)$s['id'];
}
if (count($storageIds) < 2) {
	$storageIds = array(6, 7, 8);
}

$officeId = (int)$db->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
if ($officeId <= 0) {
	$officeId = 1;
}

if ($dryRun && !$run) {
	$channelKeys = array();
	foreach (epc_comm_test_definitions() as $def) {
		$channelKeys[] = $def['key'];
	}
	$report['plan'] = array(
		'create_customer' => array('email' => $testEmail, 'phone' => $testPhone, 'password' => 'EpcCommTest2026!'),
		'create_order' => array('storages' => $storageIds, 'office_id' => $officeId),
		'channels_to_fire' => $channelKeys,
	);
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

if (!$run) {
	$report['hint'] = 'Add &run=1 to create test customer + order and send all communication tests.';
	$last = epc_comm_test_load_last();
	if ($last) {
		$report['last_run'] = $last;
	}
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

@ini_set('max_execution_time', '300');
@ini_set('display_errors', '0');

try {
	$customer = epc_comm_test_ensure_customer($db, $DP_Config, $testEmail, $testPhone);
	$report['test_customer'] = array(
		'user_id' => $customer['user_id'],
		'email' => $customer['email'],
		'phone' => $customer['phone'],
		'password' => $customer['password'],
		'login_url' => rtrim($DP_Config->domain_path, '/') . '/en/users/login',
		'trade_status' => $customer['trade_status'],
		'created' => $customer['created'],
		'note' => 'Use these credentials to log in as test customer on storefront.',
	);

	$orderInfo = epc_comm_test_create_order($db, (int)$customer['user_id'], $officeId, array_slice($storageIds, 0, 3));
	$orderId = (int)$orderInfo['order_id'];
	$report['test_order'] = array(
		'order_id' => $orderId,
		'cp_url' => rtrim($DP_Config->domain_path, '/') . '/' . $DP_Config->backend_dir . '/shop/orders/order?order_id=' . $orderId,
		'storefront_url' => rtrim($DP_Config->domain_path, '/') . '/en/shop/orders/order?order_id=' . $orderId,
		'lines' => $orderInfo['lines'],
		'office_id' => $officeId,
	);

	$db_link = $db;
	global $db_link;

	epc_checkout_send_order_notifications($db, $orderId, (int)$customer['user_id'], $officeId, '', '');

	$logs = $db->prepare('SELECT `text`, `time` FROM `shop_orders_logs` WHERE `order_id` = ? ORDER BY `id` DESC LIMIT 20');
	$logs->execute(array($orderId));
	$report['order_logs'] = $logs->fetchAll();

	foreach ($report['order_logs'] as $log) {
		$text = (string)$log['text'];
		if (stripos($text, 'admin') !== false && stripos($text, 'Order email') !== false) {
			epc_comm_record_test($report, 'new_order_to_manager', epc_notify_last_answer(), epc_admin_notify_email(), array('source' => 'checkout_bundle', 'log' => $text));
		}
		if (stripos($text, 'customer') !== false && stripos($text, 'Order email') !== false) {
			epc_comm_record_test($report, 'new_order_to_user', epc_notify_last_answer(), $testEmail, array('source' => 'checkout_bundle', 'log' => $text));
		}
		if (stripos($text, 'Supplier LPO') !== false) {
			epc_comm_record_test($report, 'lpo_to_supplier', null, '', array('source' => 'checkout_bundle', 'log' => $text));
		}
	}

	$order_id = $orderId;
	$order_text = '';
	include __DIR__ . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_manager.php';
	$managerVars = array('order_id' => $orderId, 'order_text' => $order_text);
	$staffPersons = epc_staff_notify_persons((int)$customer['user_id'], $officeId);
	if (empty($staffPersons)) {
		$staffPersons = epc_admin_notify_persons_direct();
	}
	$mgrAnswer = send_notify('new_order_to_manager', $managerVars, $staffPersons, true);
	epc_comm_record_test($report, 'new_order_to_manager (direct)', $mgrAnswer, epc_admin_notify_email());

	$order_text = '';
	include __DIR__ . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_user.php';
	$custAnswer = send_notify('new_order_to_user', array('order_id' => $orderId, 'order_text' => $order_text), array(array('type' => 'user_id', 'user_id' => (int)$customer['user_id'])), true);
	epc_comm_record_test($report, 'new_order_to_user (direct)', $custAnswer, $testEmail);

	epc_send_supplier_lpo_notifications($db, $orderId);
	$lpoLogs = $db->prepare('SELECT `text` FROM `shop_orders_logs` WHERE `order_id` = ? AND `text` LIKE ? ORDER BY `id` DESC LIMIT 10');
	$lpoLogs->execute(array($orderId, '%Supplier LPO%'));
	foreach ($lpoLogs->fetchAll(PDO::FETCH_COLUMN) as $lpoLine) {
		epc_comm_record_test($report, 'lpo_to_supplier', null, '', array('log' => $lpoLine));
	}

	$msgText = 'EPC test message from customer at ' . date('Y-m-d H:i:s');
	$db->prepare('INSERT INTO `shop_orders_messages` (`order_id`, `text`, `time`, `is_customer`, `read`) VALUES (?, ?, ?, 1, 0)')
		->execute(array($orderId, $msgText, time()));
	$msgVars = array('order_id' => $orderId, 'order_text' => nl2br(htmlspecialchars($msgText, ENT_QUOTES, 'UTF-8')));
	$msgMgr = send_notify('order_message_to_manager', $msgVars, $staffPersons, true);
	epc_comm_record_test($report, 'order_message_to_manager', $msgMgr, epc_admin_notify_email());

	$replyText = 'EPC test reply from manager at ' . date('Y-m-d H:i:s');
	$db->prepare('INSERT INTO `shop_orders_messages` (`order_id`, `text`, `time`, `is_customer`, `read`) VALUES (?, ?, ?, 0, 0)')
		->execute(array($orderId, $replyText, time()));
	$msgCust = send_notify('order_message_to_customer', array('order_id' => $orderId, 'order_text' => nl2br(htmlspecialchars($replyText, ENT_QUOTES, 'UTF-8'))), array(array('type' => 'user_id', 'user_id' => (int)$customer['user_id'])), true);
	epc_comm_record_test($report, 'order_message_to_customer', $msgCust, $testEmail);

	$paidStatus = $db->query('SELECT `id`, `name`, `to_manager_email`, `to_customer_email` FROM `shop_orders_statuses_ref` WHERE `to_manager_email` = 1 OR `to_customer_email` = 1 ORDER BY `order` ASC LIMIT 1')->fetch();
	if ($paidStatus) {
		$stName = (string)$paidStatus['name'];
		$statusVars = array('order_id' => $orderId, 'status_name' => $stName, 'order_text' => '<p>EPC test order status change</p>');
		if ((int)$paidStatus['to_manager_email'] === 1 && epc_comm_notify_row($db, 'order_status_to_manager')) {
			$a = send_notify('order_status_to_manager', $statusVars, $staffPersons, true);
			epc_comm_record_test($report, 'order_status_to_manager', $a, epc_admin_notify_email(), array('status_id' => (int)$paidStatus['id']));
		}
		if ((int)$paidStatus['to_customer_email'] === 1 && epc_comm_notify_row($db, 'order_status_to_customer')) {
			$a = send_notify('order_status_to_customer', $statusVars, array(array('type' => 'user_id', 'user_id' => (int)$customer['user_id'])), true);
			epc_comm_record_test($report, 'order_status_to_customer', $a, $testEmail, array('status_id' => (int)$paidStatus['id']));
		}
	}

	$itemRow = $db->prepare('SELECT `id` FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id` ASC LIMIT 1');
	$itemRow->execute(array($orderId));
	$itemId = (int)$itemRow->fetchColumn();
	$itemStatus = $db->query('SELECT `id`, `name`, `to_manager_email`, `to_customer_email` FROM `shop_orders_items_statuses_ref` WHERE `to_manager_email` = 1 OR `to_customer_email` = 1 ORDER BY `order` ASC LIMIT 1')->fetch();
	if ($itemId > 0 && $itemStatus) {
		$itemVars = array(
			'order_id' => $orderId,
			'order_item_id' => $itemId,
			'status_name' => (string)$itemStatus['name'],
			'order_text' => '<p>EPC test line item status change</p>',
		);
		if ((int)$itemStatus['to_manager_email'] === 1 && epc_comm_notify_row($db, 'order_item_status_to_manager')) {
			$a = send_notify('order_item_status_to_manager', $itemVars, $staffPersons, true);
			epc_comm_record_test($report, 'order_item_status_to_manager', $a, epc_admin_notify_email());
		}
		if ((int)$itemStatus['to_customer_email'] === 1 && epc_comm_notify_row($db, 'order_item_status_to_customer')) {
			$a = send_notify('order_item_status_to_customer', $itemVars, array(array('type' => 'user_id', 'user_id' => (int)$customer['user_id'])), true);
			epc_comm_record_test($report, 'order_item_status_to_customer', $a, $testEmail);
		}
	}

	$payVars = array('order_id' => $orderId, 'order_text' => '<p>EPC test payment notification</p>', 'pay_sum' => '99.50');
	if (epc_comm_notify_row($db, 'order_pay_to_manager')) {
		$a = send_notify('order_pay_to_manager', $payVars, $staffPersons, true);
		epc_comm_record_test($report, 'order_pay_to_manager', $a, epc_admin_notify_email());
	}
	if (epc_comm_notify_row($db, 'order_pay_to_customer')) {
		$a = send_notify('order_pay_to_customer', $payVars, array(array('type' => 'user_id', 'user_id' => (int)$customer['user_id'])), true);
		epc_comm_record_test($report, 'order_pay_to_customer', $a, $testEmail);
	}

	$sent = 0;
	$failed = 0;
	$unknown = 0;
	foreach ($report['tests'] as $t) {
		if ($t['sent'] === true) {
			$sent++;
		} elseif ($t['sent'] === false) {
			$failed++;
		} else {
			$unknown++;
		}
	}
	$report['summary'] = array(
		'total_checks' => count($report['tests']),
		'sent_ok' => $sent,
		'sent_failed' => $failed,
		'unknown_or_log_only' => $unknown,
		'check_inboxes' => array_values(array_unique(array($testEmail, epc_admin_notify_email()))),
	);

	epc_comm_test_save_last($report);
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Exception $e) {
	http_response_code(500);
	echo json_encode(array(
		'ok' => false,
		'error' => $e->getMessage(),
		'file' => $e->getFile(),
		'line' => $e->getLine(),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
}
