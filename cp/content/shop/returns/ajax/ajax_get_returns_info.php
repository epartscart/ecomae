<?php
/**
 * Header badge: count of open return requests (not closed / not complete).
 */
header('Content-Type: application/json; charset=utf-8');
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;
try {
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (PDOException $e) {
	exit(json_encode(array('status' => 0, 'message' => 0)));
}
$db_link->query('SET NAMES utf8');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	exit(json_encode(array('status' => 0, 'message' => 0)));
}

$closedId = 0;
try {
	require_once dirname(__DIR__) . '/epc_returns_process.php';
	$closedId = epc_returns_closed_status_id($db_link);
} catch (Throwable $e) {
	$closedId = 0;
}

$sql = 'SELECT COUNT(*) FROM `shop_orders_returns` WHERE (`return_complete` IS NULL OR `return_complete` = 0)';
$args = array();
if ($closedId > 0) {
	$sql .= ' AND `status_id` <> ?';
	$args[] = $closedId;
}
$q = $db_link->prepare($sql);
$q->execute($args);
$count = (int) $q->fetchColumn();

exit(json_encode(array('status' => 1, 'message' => $count)));
