<?php
/**
 * AJAX: load order detail pane HTML for dual-pane orders workspace.
 */
header('Content-Type: text/html; charset=utf-8');

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

$dbHost = trim((string) ($DP_Config->host ?? ''));
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}

try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (PDOException $e) {
	http_response_code(502);
	echo '<div class="epc-scp-orders-detail__empty"><p>Database unavailable</p></div>';
	exit;
}
$GLOBALS['db_link'] = $db_link;
$db_link->query('SET NAMES utf8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo '<div class="epc-scp-orders-detail__empty"><p>Access denied</p></div>';
	exit;
}

$order_id = isset($_GET['order_id']) ? (int) $_GET['order_id'] : 0;
require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/order_process/orders_background.php';

$epc_orders_detail_pane = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir
	. '/content/shop/order_process/epc_orders_detail_pane.php';
ob_start();
if (is_file($epc_orders_detail_pane)) {
	include $epc_orders_detail_pane;
} else {
	echo '<div class="epc-scp-orders-detail__empty"><p>Detail pane file not found</p></div>';
}
echo ob_get_clean();
