<?php
/**
 * Cart security hash for Docpart product_type = 2 (must match ajax_add_to_basket.php and ajax_getProductsOfBunch.php).
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

function docpart_type2_cart_check_hash(array $product_object, $price, $tech_key)
{
	$t2_manufacturer = isset($product_object['manufacturer']) ? (string) $product_object['manufacturer'] : '';
	$t2_article = isset($product_object['article']) ? (string) $product_object['article'] : '';
	$t2_article_show = isset($product_object['article_show']) ? (string) $product_object['article_show'] : '';
	$t2_name = isset($product_object['name']) ? (string) $product_object['name'] : '';
	$t2_exist = isset($product_object['exist']) ? (string) $product_object['exist'] : '';
	$t2_time_to_exe = isset($product_object['time_to_exe']) ? (string) $product_object['time_to_exe'] : '';
	$t2_time_to_exe_guaranteed = isset($product_object['time_to_exe_guaranteed']) ? (string) $product_object['time_to_exe_guaranteed'] : '';
	$t2_storage = isset($product_object['storage']) ? (string) $product_object['storage'] : '';
	$t2_min_order = isset($product_object['min_order']) ? (string) $product_object['min_order'] : '';
	$t2_probability = isset($product_object['probability']) ? (string) $product_object['probability'] : '';
	$t2_office_id = isset($product_object['office_id']) ? (string) $product_object['office_id'] : '';
	$t2_storage_id = isset($product_object['storage_id']) ? (string) $product_object['storage_id'] : '';
	$t2_price_purchase = isset($product_object['price_purchase']) ? (string) $product_object['price_purchase'] : '';
	$t2_markup = isset($product_object['markup']) ? (string) $product_object['markup'] : '';
	$t2_json_params = '';
	if (isset($product_object['json_params']) && $product_object['json_params'] !== null && $product_object['json_params'] !== '') {
		$t2_json_params = is_string($product_object['json_params'])
			? $product_object['json_params']
			: json_encode($product_object['json_params'], JSON_UNESCAPED_UNICODE);
	}
	$price_str = (string) $price;

	return md5(
		$t2_manufacturer
		. $t2_article
		. $t2_article_show
		. $t2_name
		. $t2_exist
		. $price_str
		. $t2_time_to_exe
		. $t2_time_to_exe_guaranteed
		. $t2_storage
		. $t2_min_order
		. $t2_probability
		. $t2_office_id
		. $t2_storage_id
		. $t2_price_purchase
		. $t2_markup
		. $t2_json_params
		. '2'
		. $tech_key
	);
}
