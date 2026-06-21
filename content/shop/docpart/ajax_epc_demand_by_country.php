<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: private, max-age=90');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once __DIR__ . '/epc_demand_intelligence.php';
epc_demand_require_customer_login(true);

$DP_Config = new DP_Config();
$country = isset($_REQUEST['country']) ? trim((string)$_REQUEST['country']) : '';
$limit = isset($_REQUEST['limit']) ? (int)$_REQUEST['limit'] : 50;
$seed = isset($_REQUEST['seed']) && $_REQUEST['seed'] === '1';

try {
	$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

if ($seed) {
	epc_demand_build_showcase($db, $DP_Config, 10, true);
}

$country = epc_demand_assert_country_allowed($db, $country, true);
$payload = epc_demand_build_country_view($db, $DP_Config, $country, $limit);
if (!empty($payload['status'])) {
	$payload['countries_overview'] = epc_demand_country_overview($db);
}
echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
