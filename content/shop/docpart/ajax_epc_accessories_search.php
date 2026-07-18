<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

@set_time_limit(90);

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_accessories_catalog.php';

$DP_Config = new DP_Config();

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password
	);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

$filters = array(
	'q' => isset($_REQUEST['q']) ? (string) $_REQUEST['q'] : '',
	'category' => isset($_REQUEST['category']) ? (string) $_REQUEST['category'] : '',
	'subcategory' => isset($_REQUEST['subcategory']) ? (string) $_REQUEST['subcategory'] : '',
	'brand' => isset($_REQUEST['brand']) ? (string) $_REQUEST['brand'] : '',
	'region' => isset($_REQUEST['region']) ? (string) $_REQUEST['region'] : '',
	'warehouse' => isset($_REQUEST['warehouse']) ? (string) $_REQUEST['warehouse'] : '',
	'price_min' => isset($_REQUEST['price_min']) ? (string) $_REQUEST['price_min'] : '',
	'price_max' => isset($_REQUEST['price_max']) ? (string) $_REQUEST['price_max'] : '',
	'sort' => isset($_REQUEST['sort']) ? (string) $_REQUEST['sort'] : 'price-desc',
	'page' => isset($_REQUEST['page']) ? (string) $_REQUEST['page'] : '1',
	'per_page' => isset($_REQUEST['per_page']) ? (string) $_REQUEST['per_page'] : '24',
	'in_stock' => isset($_REQUEST['in_stock']) ? (string) $_REQUEST['in_stock'] : '1',
	'refresh' => isset($_REQUEST['refresh']) && $_REQUEST['refresh'] === '1',
);

try {
	$result = epc_acc_search($db, $filters);
	$pricesVisible = true;
	$helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
	if (is_file($helpers)) {
		require_once $helpers;
		if (function_exists('epc_storefront_prices_visible_for_user')) {
			if (session_status() !== PHP_SESSION_ACTIVE) {
				@session_start();
			}
			$pricesVisible = epc_storefront_prices_visible_for_user(null);
		}
	}
	$result['status'] = true;
	$result['prices_visible'] = $pricesVisible;
	$result['currency'] = !empty($DP_Config->currency) ? (string) $DP_Config->currency : 'AED';
	echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	error_log('[epc_accessories] ' . $e->getMessage());
	echo json_encode(array('status' => false, 'message' => 'Catalog error'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
}
