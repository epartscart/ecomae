<?php
/**
 * JSON report — marketing & growth module.
 * https://www.epartscart.com/epc-marketing-demo.php?token=...
 */
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/marketing/epc_marketing_helpers.php';

try {
	$cfg = new DP_Config();
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$report = epc_marketing_demo_report($pdo);
	$report['urls'] = array(
		'cp_hub' => rtrim($cfg->domain_path, '/') . '/' . $cfg->backend_dir . '/shop/marketing/marketing',
	);
	echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('status' => false, 'error' => $e->getMessage()));
}
