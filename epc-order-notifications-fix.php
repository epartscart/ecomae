<?php
/**
 * Fix order notification delivery + optional resend for a specific order.
 * https://www.epartscart.com/epc-order-notifications-fix.php?token=epartscart-deploy-2026
 * https://www.epartscart.com/epc-order-notifications-fix.php?token=epartscart-deploy-2026&order_id=14
 */
header('Content-Type: text/plain; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/lang/dp_lang.php';
require_once __DIR__ . '/content/users/dp_user.php';
require_once __DIR__ . '/content/notifications/notify_helper.php';
require_once __DIR__ . '/content/shop/usefull/epc_admin_notifications.php';
require_once __DIR__ . '/content/shop/usefull/epc_supplier_notifications.php';

$cfg = new DP_Config();
$DP_Config = $cfg;
$db_link = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$multilang_params = multilang_init();

function epc_onf_line(string $label, $value): void
{
	echo $label . ': ' . (is_scalar($value) ? (string)$value : json_encode($value, JSON_UNESCAPED_UNICODE)) . "\n";
}

$fix = $db_link->prepare(
	'UPDATE `notifications_settings`
	 SET `send_for_not_confirmed` = 1, `email_on` = 1
	 WHERE `name` IN (\'new_order_to_manager\', \'new_order_to_user\', \'lpo_to_supplier\')'
);
$fix->execute();
echo "Updated notifications_settings (send_for_not_confirmed=1 for order emails).\n\n";

$rows = $db_link->query(
	"SELECT `name`, `email_on`, `send_for_not_confirmed`
	 FROM `notifications_settings`
	 WHERE `name` IN ('new_order_to_manager','new_order_to_user','lpo_to_supplier')"
)->fetchAll(PDO::FETCH_ASSOC);
foreach ($rows as $row) {
	epc_onf_line($row['name'], 'email_on=' . $row['email_on'] . ', send_for_not_confirmed=' . $row['send_for_not_confirmed']);
}

echo "\nSMTP config:\n";
epc_onf_line('smtp_mode', (int)$cfg->smtp_mode);
epc_onf_line('smtp_host', (string)$cfg->smtp_host);
epc_onf_line('from_email', (string)$cfg->from_email);
epc_onf_line('admin_notify', epc_admin_notify_email());

$orderId = (int)($_GET['order_id'] ?? 0);
if ($orderId <= 0) {
	echo "\nAdd &order_id=14 to resend staff + customer emails for that order.\n";
	exit;
}

$orderQ = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `id` = ? LIMIT 1');
$orderQ->execute(array($orderId));
$order = $orderQ->fetch(PDO::FETCH_ASSOC);
if (!$order) {
	exit("\nOrder {$orderId} not found.\n");
}

$userId = (int)($order['user_id'] ?? 0);
$officeId = (int)($order['office_id'] ?? 0);
if ($officeId <= 0) {
	$officeId = (int)($order['office_id_buyer'] ?? 0);
}

echo "\nOrder #{$orderId}:\n";
epc_onf_line('time', isset($order['time']) ? date('Y-m-d H:i:s', (int)$order['time']) : '');
epc_onf_line('user_id', $userId);
if ($userId > 0) {
	$uQ = $db_link->prepare('SELECT `email`, `email_confirmed` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$uQ->execute(array($userId));
	$uRow = $uQ->fetch(PDO::FETCH_ASSOC);
	if ($uRow) {
		epc_onf_line('customer_email', (string)$uRow['email'] . ' (confirmed=' . (int)$uRow['email_confirmed'] . ')');
	}
} else {
	epc_onf_line('guest_email', (string)($order['email_not_auth'] ?? ''));
}

echo "\nResending notifications for order #{$orderId} (user_id={$userId}, office_id={$officeId})...\n";
@ini_set('display_errors', '1');
@ini_set('max_execution_time', '180');
if (function_exists('ob_implicit_flush')) {
	ob_implicit_flush(true);
}

$order_id = $orderId;

try {
echo "Building staff email body...\n";
$order_text = '';
include __DIR__ . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_manager.php';
echo "Staff body length: " . strlen($order_text) . "\n";
$notify_vars = array('order_id' => $orderId, 'order_text' => $order_text);

$staffAnswer = null;
$persons = epc_staff_notify_persons($userId, $officeId);
if (empty($persons)) {
	$persons = epc_admin_notify_persons_direct();
}
echo "Sending staff notification...\n";
$staffAnswer = send_notify('new_order_to_manager', $notify_vars, $persons, true);
echo "\nStaff (new_order_to_manager):\n";
echo json_encode($staffAnswer, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT) . "\n";

$order_text = '';
include __DIR__ . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_user.php';
$msgQ = $db_link->prepare(
	'SELECT `text` FROM `shop_orders_messages` WHERE `order_id` = ? AND `is_customer` = 1 ORDER BY `id` ASC LIMIT 1'
);
$msgQ->execute(array($orderId));
$orderMessage = $msgQ->fetch(PDO::FETCH_ASSOC);
if (!empty($orderMessage['text'])) {
	$order_text .= '<h4>' . translate_str_by_id(4509) . '</h4>';
	$order_text .= '<div style="font-family: Calibri; font-size: 14px;">' . str_replace("\n", '<br/>', $orderMessage['text']) . '</div>';
}
$customerVars = array('order_id' => $orderId, 'order_text' => $order_text);

$customerPersons = array();
if ($userId > 0) {
	$customerPersons[] = array('type' => 'user_id', 'user_id' => $userId);
} else {
	$customerPersons[] = array(
		'type' => 'direct_contact',
		'contacts' => array(
			'email' => array('value' => htmlentities((string)($order['email_not_auth'] ?? ''))),
			'phone' => array('value' => htmlentities((string)($order['phone_not_auth'] ?? ''))),
		),
	);
}
$customerAnswer = send_notify('new_order_to_user', $customerVars, $customerPersons, true);
echo "\nCustomer (new_order_to_user):\n";
echo json_encode($customerAnswer, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT) . "\n";

echo "\nSending supplier LPO e-mails (lpo_to_supplier)...\n";
epc_send_supplier_lpo_notifications($db_link, $orderId);
$lpoLogs = $db_link->prepare('SELECT `text` FROM `shop_orders_logs` WHERE `order_id` = ? AND `text` LIKE ? ORDER BY `id` DESC LIMIT 15');
$lpoLogs->execute(array($orderId, '%Supplier LPO%'));
foreach ($lpoLogs->fetchAll(PDO::FETCH_COLUMN) as $logLine) {
	echo '  ' . $logLine . "\n";
}
} catch (Throwable $e) {
	echo "\nERROR: " . $e->getMessage() . "\n" . $e->getFile() . ':' . $e->getLine() . "\n";
}

echo "\nDone.\n";
