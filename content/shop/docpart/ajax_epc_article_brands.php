<?php
header('Content-Type: application/json;charset=utf-8;');

require_once($_SERVER['DOCUMENT_ROOT'].'/config.php');
require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_epc_article_brands.php');

$DP_Config = new DP_Config;
$article_input = isset($_GET['article']) ? trim((string)$_GET['article']) : '';
if ($article_input === '' && isset($_POST['article'])) {
	$article_input = trim((string)$_POST['article']);
}

try {
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'Database unavailable', 'manufacturers' => array()), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

$payload = epc_collect_article_catalog_brands($db_link, $DP_Config, $article_input);
echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
exit;
