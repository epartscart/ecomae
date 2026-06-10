<?php
/**
 * Smoke test: price lists manager (shop/prices) PHP render.
 * GET: token=epartscart-deploy-2026, key=tech_key
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';

$DP_Config = new DP_Config();
epc_portal_apply_config($DP_Config);

if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Invalid key')));
}

$backend = $DP_Config->backend_dir;
$managerPhp = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/prices_upload/prices_manager.php';

$result = array(
	'status' => true,
	'php_version' => PHP_VERSION,
	'db' => $DP_Config->db,
	'manager_exists' => is_file($managerPhp),
	'include_ok' => false,
	'html_length' => 0,
	'error' => '',
	'has_price_lists' => false,
	'price_lists_count' => 0,
);

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$db_link->query('SET NAMES utf8');
} catch (Throwable $e) {
	$result['status'] = false;
	$result['error'] = 'DB: ' . $e->getMessage();
	exit(json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

try {
	$result['price_lists_count'] = (int)$db_link->query('SELECT COUNT(*) FROM `shop_docpart_prices`')->fetchColumn();
} catch (Throwable $e) {
	$result['price_lists_count'] = -1;
}

$GLOBALS['DP_Config'] = $DP_Config;
ob_start();
try {
	include $managerPhp;
	$html = ob_get_clean();
	$result['include_ok'] = true;
	$result['html_length'] = strlen($html);
	$result['has_price_lists'] = (stripos($html, 'price list') !== false || stripos($html, 'Price lists') !== false);
} catch (Throwable $e) {
	ob_end_clean();
	$result['status'] = false;
	$result['error'] = $e->getMessage();
}

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
