<?php
/**
 * JSON demo report — logistics & carriers only.
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/logistics/epc_logistics_helpers.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

echo json_encode(array(
	'status' => true,
	'title' => 'eParts Cart — Logistics demo',
	'module' => 'logistics',
	'cp_url' => rtrim($cfg->domain_path, '/') . '/' . $cfg->backend_dir . '/shop/logistics/carriers',
	'guide_url' => rtrim($cfg->domain_path, '/') . '/' . $cfg->backend_dir . '/shop/logistics/guide',
	'report' => epc_logistics_demo_report($pdo),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
