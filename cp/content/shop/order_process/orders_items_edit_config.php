<?php
/**
 * Order item edit — runtime config JS (footer load, outside CP .row pane).
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
	echo 'window.EPC_OI_EDIT={};';
	exit;
}

$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$itemId = isset($_GET['id']) ? (int) $_GET['id'] : 0;
$orderId = 0;
if ($itemId > 0) {
	try {
		$pdo = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$st = $pdo->prepare('SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ? LIMIT 1');
		$st->execute(array($itemId));
		$orderId = (int) $st->fetchColumn();
	} catch (Throwable $e) {
		$orderId = 0;
	}
}

$config = array(
	'backend' => $backend,
	'itemId' => $itemId,
	'orderId' => $orderId,
	'urls' => array(
		'order' => '/' . $backend . '/shop/orders/order?order_id=' . $orderId,
		'items' => '/' . $backend . '/shop/orders/items',
	),
);

echo 'window.EPC_OI_EDIT=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
