<?php
/**
 * AJAX — warehouse spare parts search (brand + article only).
 * POST/GET brand, article
 */
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store, no-cache, must-revalidate');

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/epc_spare_parts_warehouse.php';

$DP_Config = new DP_Config();

$brand = isset($_REQUEST['brand']) ? trim((string) $_REQUEST['brand']) : '';
$article = isset($_REQUEST['article']) ? trim((string) $_REQUEST['article']) : '';
if ($brand === '' && isset($_REQUEST['manufacturer'])) {
	$brand = trim((string) $_REQUEST['manufacturer']);
}

try {
	$pdo = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)
	);
} catch (Throwable $e) {
	http_response_code(503);
	echo json_encode(array('ok' => false, 'message' => 'Database unavailable.'), JSON_UNESCAPED_UNICODE);
	exit;
}

$result = epc_spare_parts_warehouse_search($brand, $article, $pdo, $DP_Config);
echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
