<?php
/**
 * Order detail — runtime config JS (footer load, outside eval'd CP content pane).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
if (empty($user_session) || !is_array($user_session)) {
	echo 'window.EPC_OC={};';
	exit;
}

$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$order_id = isset($_GET['order_id']) ? (int) $_GET['order_id'] : 0;
$customer_id = 0;
if ($order_id > 0) {
	try {
		$pdo = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$st = $pdo->prepare('SELECT `user_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1');
		$st->execute(array($order_id));
		$customer_id = (int) $st->fetchColumn();
	} catch (Throwable $e) {
	}
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_translate.php';

$config = array(
	'backend' => $backend,
	'orderId' => $order_id,
	'customerId' => $customer_id,
	'managerId' => (int) DP_User::getAdminId(),
	'csrf' => (string) ($user_session['csrf_guard_key'] ?? ''),
	'domainPath' => (string) ($DP_Config->domain_path ?? '/'),
	'urls' => array(
		'orders' => '/' . $backend . '/shop/orders/orders',
		'order' => '/' . $backend . '/shop/orders/order?order_id=' . $order_id,
		'deleteOrders' => '/' . $backend . '/content/shop/order_process/ajax_delete_orders.php',
		'setViewed' => '/' . $backend . '/content/shop/order_process/ajax_set_orders_viewed.php',
		'authWithUser' => '/' . $backend . '/content/users/auth_with_user.php',
		'payRefund' => '/' . $backend . '/content/shop/order_process/ajax_order_pay_refund.php',
		'userComment' => '/' . $backend . '/content/users/ajax_set_user_comment.php',
		'addCommentLog' => '/' . $backend . '/content/shop/order_process/ajax_add_comment_to_log.php',
		'accountOps' => '/' . $backend . '/shop/finance/account_operations',
		'setOrderStatus' => '/content/shop/protocol/set_order_status.php',
		'setItemStatus' => '/content/shop/protocol/set_order_item_status.php',
		'printDoc' => '/content/shop/print_docs/service/print.php',
	),
	'msg' => array(
		'deleteConfirm' => translate_str_by_id(3510),
		'setViewedFail' => translate_str_by_id(3599),
		'authFail' => translate_str_by_id(3541),
		'statusSaved' => translate_str_by_id(2157),
		'paymentAdded' => translate_str_by_id(3529),
		'refundOk' => translate_str_by_id(3538),
		'itemsStatusOk' => translate_str_by_id(2722),
	),
);

echo 'window.EPC_OC=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
