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
	$price_str = number_format((float) $price, 2, '.', '');

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

/**
 * Recompute cart hashes after late mutations (manufacturer synonym, VAT-inclusive display price).
 * Must run after all price/brand changes so client hash matches ajax_add_to_basket.php.
 *
 * @param array<string,mixed> $product
 */
function docpart_refresh_product_cart_hashes(array &$product, $tech_key): void
{
	$tech_key = (string) $tech_key;
	$product_type = (int) ($product['product_type'] ?? 2);

	// Normalize money fields so JSON float 237.7 and "237.70" hash identically.
	if (isset($product['price']) && is_numeric($product['price'])) {
		$product['price'] = number_format((float) $product['price'], 2, '.', '');
	}
	if (isset($product['price_purchase']) && is_numeric($product['price_purchase'])) {
		$product['price_purchase'] = number_format((float) $product['price_purchase'], 2, '.', '');
	}
	if (isset($product['markup']) && is_numeric($product['markup'])) {
		$product['markup'] = (string) (int) $product['markup'];
	}

	if ($product_type === 1) {
		$product['check_hash'] = md5(
			($product['product_id'] ?? '')
			. ($product['office_id'] ?? '')
			. ($product['storage_id'] ?? '')
			. ($product['storage_record_id'] ?? '')
			. ($product['price'] ?? '')
			. $tech_key
		);
		return;
	}

	$product['check_hash'] = docpart_type2_cart_check_hash($product, $product['price'] ?? 0, $tech_key);

	if (empty($product['groups_price']) || !is_array($product['groups_price'])) {
		return;
	}
	if (!isset($product['groups_check_hash']) || !is_array($product['groups_check_hash'])) {
		$product['groups_check_hash'] = array();
	}
	foreach ($product['groups_price'] as $gid => $group_price) {
		$group_price_norm = is_numeric($group_price)
			? number_format((float) $group_price, 2, '.', '')
			: (string) $group_price;
		$product['groups_price'][$gid] = $group_price_norm;
		$tmp = $product;
		$tmp['price'] = $group_price_norm;
		if (isset($product['groups_markup'][$gid])) {
			$tmp['markup'] = (string) (int) $product['groups_markup'][$gid];
		}
		$product['groups_check_hash'][$gid] = docpart_type2_cart_check_hash($tmp, $group_price_norm, $tech_key);
	}
}

/**
 * @param array<int,array<string,mixed>> $products
 */
function docpart_refresh_products_cart_hashes(array &$products, $tech_key): void
{
	foreach ($products as &$product) {
		if (is_array($product)) {
			docpart_refresh_product_cart_hashes($product, $tech_key);
		}
	}
	unset($product);
}
