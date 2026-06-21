<?php
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once __DIR__ . '/epc_demand_intelligence.php';
epc_demand_require_customer_login(true);

$DP_Config = new DP_Config();
$brand = isset($_REQUEST['brand']) ? trim((string)$_REQUEST['brand']) : 'TOYOTA';
$article = isset($_REQUEST['article']) ? trim((string)$_REQUEST['article']) : '1310154101';
$seed = isset($_REQUEST['seed']) && $_REQUEST['seed'] === '1';

try {
	$db = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable'), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

if ($seed) {
	epc_demand_seed_demo($db, $brand, $article);
}

$selected_country = isset($_REQUEST['country']) ? trim((string)$_REQUEST['country']) : '';
if ($selected_country !== '') {
	$selected_country = epc_demand_assert_country_allowed($db, $selected_country, true);
} else {
	$access = epc_demand_access_context($db);
	$selected_country = (string)$access['default_country'];
}

$card = epc_demand_build_card($db, $DP_Config, $brand, $article, null, $selected_country);
if (empty($card['demand_countries']) && docpart_normalize_article_for_price($article) === '1310154101' && strtoupper($brand) === 'TOYOTA') {
	epc_demand_seed_demo($db, $brand, $article);
	$card = epc_demand_build_card($db, $DP_Config, $brand, $article, null, $selected_country);
}

header('Cache-Control: private, max-age=60');
echo json_encode($card, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
