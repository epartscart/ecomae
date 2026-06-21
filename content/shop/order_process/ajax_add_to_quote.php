<?php
/**
 * Add Docpart product (type 2) lines to customer quote draft (logged-in users only).
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
require_once($_SERVER['DOCUMENT_ROOT'].'/lang/dp_lang.php');
multilang_init();
require_once($_SERVER['DOCUMENT_ROOT'].'/content/users/dp_user.php');

$user_id = DP_User::getUserId();
if ($user_id <= 0) {
	exit(json_encode(array('status' => false, 'code' => 'auth', 'message' => 'Please sign in to use quotes.')));
}

$product_objects = json_decode($_POST['product_objects'], true);
if ($product_objects === null || !is_array($product_objects)) {
	exit(json_encode(array('status' => false, 'code' => 'data', 'message' => 'Invalid payload')));
}

$type2_lines = array();
foreach ($product_objects as $product_object) {
	if ((int) $product_object['product_type'] !== 2) {
		continue;
	}
	$t2_manufacturer = $product_object['manufacturer'];
	$t2_article = $product_object['article'];
	$t2_article_show = $product_object['article_show'];
	$t2_name = $product_object['name'];
	$t2_exist = $product_object['exist'];
	$t2_time_to_exe = $product_object['time_to_exe'];
	$t2_time_to_exe_guaranteed = $product_object['time_to_exe_guaranteed'];
	$t2_storage = $product_object['storage'].'';
	$t2_min_order = $product_object['min_order'];
	$t2_probability = $product_object['probability'];
	$price = $product_object['price'];
	$t2_price_purchase = $product_object['price_purchase'];
	$t2_markup = $product_object['markup'];
	$t2_office_id = $product_object['office_id'];
	$t2_storage_id = $product_object['storage_id'];
	$t2_json_params = isset($product_object['json_params']) ? ($product_object['json_params'].'') : '';

	$check_hash = md5($t2_manufacturer.$t2_article.$t2_article_show.$t2_name.$t2_exist.$price.$t2_time_to_exe.$t2_time_to_exe_guaranteed.$t2_storage.$t2_min_order.$t2_probability.$t2_office_id.$t2_storage_id.$t2_price_purchase.$t2_markup.$t2_json_params.'2'.$DP_Config->tech_key);
	if ($check_hash != $product_object['check_hash']) {
		exit(json_encode(array('status' => false, 'code' => '35', 'message' => 'Data validation failed. Refresh the page and try again.')));
	}
	$type2_lines[] = $product_object;
}

if (count($type2_lines) < 1) {
	exit(json_encode(array('status' => false, 'message' => 'Only supplier price-search lines can be added to a quote.')));
}

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

foreach ($type2_lines as $product_object) {
	$count_need = 1;
	if (!empty($product_object['count_need'])) {
		$count_need = max(1, (int) $product_object['count_need']);
	}
	$ins = $db_link->prepare('INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`) VALUES (?, 2, ?, ?)');
	$ins->execute(array($quote_id, json_encode($product_object), $count_need));
	$db_link->prepare('UPDATE `shop_quote_requests` SET `time_updated` = ? WHERE `id` = ?')->execute(array(time(), $quote_id));
}

exit(json_encode(array('status' => true, 'quote_id' => $quote_id)));
