<?php
/**
 * Customer submits draft quote for staff review (draft -> submitted).
 */
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER['DOCUMENT_ROOT'].'/config.php');
$DP_Config = new DP_Config;
try {
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
	exit(json_encode(array('status' => false, 'message' => 'DB')));
}
$db_link->query('SET NAMES utf8;');
require_once($_SERVER['DOCUMENT_ROOT'].'/content/users/dp_user.php');
require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/epc_storefront_prices_helpers.php');

$user_id = DP_User::getUserId();
if ($user_id <= 0) {
	exit(json_encode(epc_storefront_guest_commerce_denied_payload()));
}

$quote_id = isset($_POST['quote_id']) ? (int) $_POST['quote_id'] : 0;
$customer_note = isset($_POST['customer_note']) ? trim($_POST['customer_note']) : '';

if ($quote_id <= 0) {
	exit(json_encode(array('status' => false, 'message' => 'Invalid quote')));
}

$q = $db_link->prepare('SELECT `id`, `status` FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1');
$q->execute(array($quote_id, $user_id));
$row = $q->fetch(PDO::FETCH_ASSOC);
if (!$row || $row['status'] !== 'draft') {
	exit(json_encode(array('status' => false, 'message' => 'Quote not found or already submitted')));
}

$c = $db_link->prepare('SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ?');
$c->execute(array($quote_id));
if ((int) $c->fetchColumn() < 1) {
	exit(json_encode(array('status' => false, 'message' => 'Add at least one line before submitting')));
}

$now = time();
$upd = $db_link->prepare('UPDATE `shop_quote_requests` SET `status` = \'submitted\', `time_submitted` = ?, `time_updated` = ?, `customer_note` = ? WHERE `id` = ? AND `user_id` = ?');
$ok = $upd->execute(array($now, $now, $customer_note, $quote_id, $user_id));

exit(json_encode(array('status' => (bool) $ok)));
