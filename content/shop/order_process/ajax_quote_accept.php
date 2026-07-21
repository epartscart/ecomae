<?php
/**
 * Customer accepts a quoted request: lines go to cart at quoted_price, quote -> accepted.
 */
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER['DOCUMENT_ROOT'].'/config.php');
$DP_Config = new DP_Config;
try {
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
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
$session_id = 0;

$quote_id = isset($_POST['quote_id']) ? (int) $_POST['quote_id'] : 0;
if ($quote_id <= 0) {
	exit(json_encode(array('status' => false, 'message' => 'Invalid quote')));
}

try {
	$db_link->beginTransaction();

	$q = $db_link->prepare('SELECT * FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1 FOR UPDATE');
	$q->execute(array($quote_id, $user_id));
	$quote = $q->fetch(PDO::FETCH_ASSOC);
	if (!$quote || $quote['status'] !== 'quoted') {
		$db_link->rollBack();
		exit(json_encode(array('status' => false, 'message' => 'Quote is not available for acceptance')));
	}

	$items_q = $db_link->prepare('SELECT * FROM `shop_quote_items` WHERE `quote_id` = ? ORDER BY `id` ASC');
	$items_q->execute(array($quote_id));
	$items = $items_q->fetchAll(PDO::FETCH_ASSOC);
	if (count($items) < 1) {
		$db_link->rollBack();
		exit(json_encode(array('status' => false, 'message' => 'No lines in quote')));
	}

	foreach ($items as $it) {
		$use_alt = !empty($it['offer_alternative'])
			&& (int) $it['offer_alternative'] === 1
			&& !empty($it['alt_manufacturer'])
			&& !empty($it['alt_article']);

		$effective_price = $use_alt
			? (isset($it['alt_quoted_price']) ? (float) $it['alt_quoted_price'] : 0.0)
			: (isset($it['quoted_price']) ? (float) $it['quoted_price'] : 0.0);

		if ($effective_price <= 0) {
			$db_link->rollBack();
			exit(json_encode(array('status' => false, 'message' => 'Quote is incomplete — wait for staff pricing on all lines')));
		}
	}

	foreach ($items as $it) {
		$product_object = json_decode($it['product_object_json'], true);
		if (!is_array($product_object) || (int) $product_object['product_type'] !== 2) {
			throw new Exception('bad_line');
		}

		$use_alt = !empty($it['offer_alternative'])
			&& (int) $it['offer_alternative'] === 1
			&& !empty($it['alt_manufacturer'])
			&& !empty($it['alt_article']);

		$requested_mfr = isset($product_object['manufacturer']) ? (string) $product_object['manufacturer'] : '';
		$requested_art = isset($product_object['article_show']) ? (string) $product_object['article_show'] : (isset($product_object['article']) ? (string) $product_object['article'] : '');

		if ($use_alt) {
			$alt_mfr = mb_strtoupper(trim((string) $it['alt_manufacturer']), 'UTF-8');
			$alt_art_show = trim((string) (!empty($it['alt_article_show']) ? $it['alt_article_show'] : $it['alt_article']));
			$alt_art = mb_strtoupper(preg_replace('/[^a-zA-Z0-9А-Яа-яёЁ]+/ui', '', $alt_art_show), 'UTF-8');
			$alt_name = trim((string) ($it['alt_name'] ?? ''));
			if ($alt_name === '') {
				$alt_name = $alt_mfr.' '.$alt_art_show.' (alternative)';
			}
			$product_object['manufacturer'] = $alt_mfr;
			$product_object['article'] = $alt_art;
			$product_object['article_show'] = $alt_art_show;
			$product_object['name'] = $alt_name;
			$product_object['epc_quote_alternative'] = 1;
			$product_object['epc_requested_manufacturer'] = $requested_mfr;
			$product_object['epc_requested_article'] = $requested_art;
			$price = (float) $it['alt_quoted_price'];
			$count_need = max(1, (int) ($it['alt_count_need'] ?: 1));
		} else {
			$price = (float) $it['quoted_price'];
			$count_need = max(1, (int) $it['count_need']);
		}

		$product_object['price'] = $price;

		if ($it['quoted_time_to_exe'] !== null && $it['quoted_time_to_exe'] !== '') {
			$texe = (int) $it['quoted_time_to_exe'];
			$product_object['time_to_exe'] = $texe;
			$product_object['time_to_exe_guaranteed'] = $texe;
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
		$t2_price_purchase = $product_object['price_purchase'];
		$t2_markup = $product_object['markup'];
		$t2_office_id = $product_object['office_id'];
		$t2_storage_id = $product_object['storage_id'];
		$t2_json_params = isset($product_object['json_params']) ? ($product_object['json_params'].'') : '';

		$check_hash = md5($t2_manufacturer.$t2_article.$t2_article_show.$t2_name.$t2_exist.$price.$t2_time_to_exe.$t2_time_to_exe_guaranteed.$t2_storage.$t2_min_order.$t2_probability.$t2_office_id.$t2_storage_id.$t2_price_purchase.$t2_markup.$t2_json_params.'2'.$DP_Config->tech_key);
		$product_object['check_hash'] = $check_hash;

		$count_need = max(1, (int) $count_need);
		$product_object['count_need'] = $count_need;

		$by_flag = false;
		$t2_json_params_array = json_decode($t2_json_params, true);
		if (!empty($t2_json_params_array) && ((int) $t2_json_params_array['used'] === 1)) {
			$by_flag = true;
		}

		if ($by_flag === false) {
			$check_already_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_carts` WHERE 
					`product_type`=2 AND 
					`user_id`=? AND 
					`session_id`=? AND 
					`t2_manufacturer` = ? AND 
					`t2_article` = ? AND 
					`t2_exist` = ? AND 
					`t2_time_to_exe` = ? AND 
					`t2_time_to_exe_guaranteed` = ? AND 
					`t2_probability` = ? AND 
					`t2_office_id` = ? AND 
					`t2_storage_id` = ? AND 
					CAST(`price` AS DECIMAL(12,4)) = CAST(? AS DECIMAL(12,4));');
			$check_already_query->execute(array($user_id, $session_id, $t2_manufacturer, $t2_article, $t2_exist, $t2_time_to_exe, $t2_time_to_exe_guaranteed, $t2_probability, $t2_office_id, $t2_storage_id, $price));
			if ((int) $check_already_query->fetchColumn() > 0) {
				$db_link->rollBack();
				exit(json_encode(array('status' => false, 'code' => 'already', 'message' => 'One or more items are already in your cart at this price — adjust the cart and try again')));
			}
		}

		$time = time();
		$t2_json_params_db = $t2_json_params;

		$SQL_INSERT = "INSERT INTO `shop_carts` (
            `product_type`,
            `price`,
            `count_need`,
            `time`,
            `user_id`,
			`session_id`,
            `t2_manufacturer`,
            `t2_article`,
            `t2_article_show`,
            `t2_name`,
            `t2_exist`,
            `t2_time_to_exe`,
            `t2_time_to_exe_guaranteed`,
            `t2_storage`,
            `t2_min_order`,
            `t2_probability`,
            `t2_markup`,
            `t2_price_purchase`,
            `t2_office_id`,
            `t2_storage_id`,
            `t2_product_json`,
			`t2_json_params`
                ) VALUES (";
		$binding_values = array(2, $price, $count_need, $time, $user_id, $session_id, $t2_manufacturer, $t2_article, $t2_article_show, $t2_name, $t2_exist, $t2_time_to_exe, $t2_time_to_exe_guaranteed, (string) $t2_storage, $t2_min_order, $t2_probability, $t2_markup, $t2_price_purchase, $t2_office_id, $t2_storage_id, json_encode($product_object), $t2_json_params_db);
		$SQL_INSERT = $SQL_INSERT.str_repeat('?,', count($binding_values) - 1).'?)';

		if (!$db_link->prepare($SQL_INSERT)->execute($binding_values)) {
			throw new Exception('insert');
		}
	}

	$db_link->prepare('UPDATE `shop_quote_requests` SET `status` = \'accepted\', `time_updated` = ? WHERE `id` = ?')->execute(array(time(), $quote_id));
	$db_link->commit();
	exit(json_encode(array('status' => true)));
} catch (PDOException $e) {
	if ($db_link->inTransaction()) {
		$db_link->rollBack();
	}
	exit(json_encode(array('status' => false, 'message' => 'Could not complete acceptance')));
} catch (Exception $e) {
	if ($db_link->inTransaction()) {
		$db_link->rollBack();
	}
	exit(json_encode(array('status' => false, 'message' => 'Could not complete acceptance')));
}
