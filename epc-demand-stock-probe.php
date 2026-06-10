<?php
header('Content-Type: application/json; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit('Forbidden');
}
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';
require_once __DIR__ . '/content/shop/docpart/epc_demand_intelligence.php';
$cfg = new DP_Config();
$db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db, $cfg->user, $cfg->password);
$db->query('SET NAMES utf8;');
$total = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE IFNULL(`exist`,0) > 0 AND IFNULL(`price`,0) > 0')->fetchColumn();
$engine = epc_demand_fetch_engine_stock_lines($db, 5);
$top = epc_demand_fetch_top_stock_lines($db, 5);
echo json_encode(array(
	'in_stock_rows' => $total,
	'engine_sample' => $engine,
	'top_sample' => $top,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
