<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: private, max-age=120');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once __DIR__ . '/epc_demand_intelligence.php';
epc_demand_require_customer_login(true);

$DP_Config = new DP_Config();
$limit = isset($_REQUEST['limit']) ? (int)$_REQUEST['limit'] : 10;
if ($limit < 1) {
	$limit = 10;
}
if ($limit > 20) {
	$limit = 20;
}
$reseed = !isset($_REQUEST['reseed']) || $_REQUEST['reseed'] !== '0';

try {
	$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

$payload = epc_demand_build_showcase($db, $DP_Config, $limit, $reseed);
echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
