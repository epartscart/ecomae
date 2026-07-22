<?php
/**
 * NOWPayments IPN + demo notification.
 */
$EPC_PAY_HANDLER = 'nowpayments';

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
	http_response_code(500);
	exit(json_encode(array('result' => false)));
}
$db_link->query('SET NAMES utf8;');

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
$multilang_params = multilang_init();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_crypto_payments.php';

$raw = file_get_contents('php://input');
$json = json_decode((string)$raw, true);
$isIpn = is_array($json) && (isset($json['payment_status']) || isset($json['payment_id']));

if (!$isIpn) {
	// Demo / form POST fallback (shared stub)
	require __DIR__ . '/../epc_demo/notification.php';
	exit;
}

$operation_id = (int)($json['order_id'] ?? 0);
$sum = (float)($json['price_amount'] ?? 0);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/get_pay_system_parameters.php';
if (!is_array($paysystem_parameters)) {
	$paysystem_parameters = array();
}
$headers = function_exists('getallheaders') ? getallheaders() : array();
$verified = epc_crypto_verify_ipn($paysystem_parameters, $raw, is_array($headers) ? $headers : array());
$status = strtolower((string)($json['payment_status'] ?? ''));
$paidStatuses = array('finished', 'confirmed', 'sending');
if (!$verified || !in_array($status, $paidStatuses, true) || $operation_id <= 0) {
	http_response_code(400);
	header('Content-Type: application/json');
	echo json_encode(array('result' => false, 'message' => 'IPN rejected'));
	exit;
}

$check = $db_link->prepare('SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
$check->execute(array($operation_id));
if ((int)$check->fetchColumn() !== 1) {
	header('Content-Type: application/json');
	echo json_encode(array('result' => true, 'message' => 'already processed'));
	exit;
}

$db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;')->execute(array($operation_id));
$amount = $sum;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/pay_notify.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/pay_for_order.php';

header('Content-Type: application/json');
echo json_encode(array('result' => true));
exit;
