<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: private, max-age=300');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once __DIR__ . '/epc_demand_intelligence.php';
epc_demand_bootstrap_db_link();
epc_demand_require_customer_login(true);

$DP_Config = new DP_Config();

try {
	$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

epc_demand_ensure_schema($db);
$access = epc_demand_access_context($db);
echo json_encode(array(
	'status' => true,
	'access' => $access,
	'countries' => epc_demand_country_overview($db),
	'registry' => array_values(epc_demand_country_registry()),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
