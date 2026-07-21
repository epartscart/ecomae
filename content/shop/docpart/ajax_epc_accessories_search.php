<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

@set_time_limit(60);

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_accessories_db.php';

$DP_Config = new DP_Config();

try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
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
	'make' => isset($_REQUEST['make']) ? (string) $_REQUEST['make'] : '',
	'model' => isset($_REQUEST['model']) ? (string) $_REQUEST['model'] : '',
	'city' => isset($_REQUEST['city']) ? (string) $_REQUEST['city'] : '',
	'condition' => isset($_REQUEST['condition']) ? (string) $_REQUEST['condition'] : '',
	'price_min' => isset($_REQUEST['price_min']) ? (string) $_REQUEST['price_min'] : '',
	'price_max' => isset($_REQUEST['price_max']) ? (string) $_REQUEST['price_max'] : '',
	'id' => isset($_REQUEST['id']) ? (string) $_REQUEST['id'] : (isset($_REQUEST['listing_id']) ? (string) $_REQUEST['listing_id'] : ''),
	'sort' => isset($_REQUEST['sort']) ? (string) $_REQUEST['sort'] : 'updated-desc',
	'page' => isset($_REQUEST['page']) ? (string) $_REQUEST['page'] : '1',
	'per_page' => isset($_REQUEST['per_page']) ? (string) $_REQUEST['per_page'] : '24',
);

try {
	$result = epc_acc_marketplace_search($db, $filters);
	$result['status'] = true;
	$result['currency_default'] = 'AED';
	echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	error_log('[epc_accessories] ' . $e->getMessage());
	$payload = array('status' => false, 'message' => 'Catalog error');
	if (!empty($_GET['epc_debug']) && $_GET['epc_debug'] === '1') {
		$payload['debug'] = $e->getMessage();
	}
	echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
}
