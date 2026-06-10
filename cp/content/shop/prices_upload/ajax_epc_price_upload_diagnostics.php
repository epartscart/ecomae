<?php
header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		[PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
	);
} catch (PDOException $e) {
	exit(json_encode(['status' => false, 'message' => 'No DB connect']));
}
$db_link->query('SET NAMES utf8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_price_upload_diagnostics.php';

if (!DP_User::isAdmin()) {
	exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

$action = (string)($_GET['action'] ?? $_POST['action'] ?? 'snapshot');
$config = [
	'backend_dir' => $DP_Config->backend_dir,
	'domain_path' => $DP_Config->domain_path,
	'tech_key' => $DP_Config->tech_key,
	'tmp_dir_prices_upload' => $DP_Config->tmp_dir_prices_upload,
];

if ($action === 'health') {
	echo json_encode([
		'status' => true,
		'snapshot' => epc_price_upload_diagnostics_snapshot($db_link, $config),
		'health' => epc_price_upload_run_health_checks($config),
	], JSON_UNESCAPED_UNICODE);
	exit;
}

echo json_encode([
	'status' => true,
	'snapshot' => epc_price_upload_diagnostics_snapshot($db_link, $config),
], JSON_UNESCAPED_UNICODE);
