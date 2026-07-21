<?php
/**
 * Add a quote line for a part with no live stock row (requested brand / cross reference).
 */
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER['DOCUMENT_ROOT'] . '/config.php');
$DP_Config = new DP_Config;
try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
	exit(json_encode(array('status' => false, 'message' => 'DB')));
}
$db_link->query('SET NAMES utf8;');
require_once($_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php');
multilang_init();
require_once($_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php');
require_once($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php');

$user_id = DP_User::getUserId();
if ($user_id <= 0) {
	// Quotes are registered customers only — never create guest drafts.
	exit(json_encode(epc_storefront_guest_commerce_denied_payload()));
}

$manufacturer = isset($_POST['manufacturer']) ? trim($_POST['manufacturer']) : '';
$article = isset($_POST['article']) ? trim($_POST['article']) : '';
$article_show = isset($_POST['article_show']) ? trim($_POST['article_show']) : '';
$name = isset($_POST['name']) ? trim($_POST['name']) : '';
$count_need = isset($_POST['count_need']) ? max(1, (int) $_POST['count_need']) : 1;

if ($manufacturer === '' || $article === '') {
	exit(json_encode(array('status' => false, 'message' => 'Brand and part number are required.')));
}

$article_norm = mb_strtoupper(preg_replace('/[^a-zA-Z0-9А-Яа-яёЁ]+/ui', '', $article), 'UTF-8');
$manufacturer = htmlentities(mb_strtoupper($manufacturer, 'UTF-8'), ENT_QUOTES, 'UTF-8');
if ($article_show === '') {
	$article_show = $article_norm;
}
$article_show = htmlentities($article_show, ENT_QUOTES, 'UTF-8');
if ($name === '') {
	$name = 'Quote request — ' . html_entity_decode($manufacturer, ENT_QUOTES, 'UTF-8') . ' ' . html_entity_decode($article_show, ENT_QUOTES, 'UTF-8');
}
$name = htmlentities($name, ENT_QUOTES, 'UTF-8');

$product_object = array(
	'product_type' => 2,
	'manufacturer' => $manufacturer,
	'article' => $article_norm,
	'article_show' => $article_show,
	'name' => $name,
	'exist' => 0,
	'price' => 0,
	'time_to_exe' => 0,
	'time_to_exe_guaranteed' => 0,
	'storage' => '',
	'min_order' => 1,
	'probability' => 0,
	'office_id' => 0,
	'storage_id' => 0,
	'price_purchase' => 0,
	'markup' => 0,
	'json_params' => '',
	'check_hash' => 'manual',
	'epc_manual_quote' => 1,
	'count_need' => $count_need,
);

$q = $db_link->prepare('SELECT `id` FROM `shop_quote_requests` WHERE `user_id` = ? AND `status` = \'draft\' ORDER BY `id` DESC LIMIT 1');
$q->execute(array($user_id));
$row = $q->fetch();
$now = time();
if ($row) {
	$quote_id = (int) $row['id'];
} else {
	$db_link->prepare('INSERT INTO `shop_quote_requests` (`user_id`, `session_id`, `status`, `time_created`, `time_updated`) VALUES (?, 0, \'draft\', ?, ?)')->execute(array($user_id, $now, $now));
	$quote_id = (int) $db_link->lastInsertId();
}

$ins = $db_link->prepare('INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`) VALUES (?, 2, ?, ?)');
$ins->execute(array($quote_id, json_encode($product_object), $count_need));
$db_link->prepare('UPDATE `shop_quote_requests` SET `time_updated` = ? WHERE `id` = ?')->execute(array(time(), $quote_id));

exit(json_encode(array('status' => true, 'quote_id' => $quote_id)));
