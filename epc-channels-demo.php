<?php
/**
 * JSON demo report — marketplace + carrier sample data.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/channels/epc_channel_helpers.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_channel_ensure_schema($pdo);
$report = epc_channel_demo_report($pdo);

echo json_encode(array(
	'status' => true,
	'title' => 'eParts Cart — Channels demo (marketplace)',
	'module' => 'channels',
	'generated_at' => date('c'),
	'cp_url' => rtrim($cfg->domain_path, '/') . '/' . $cfg->backend_dir . '/shop/channels/channels',
	'logistics_url' => rtrim($cfg->domain_path, '/') . '/' . $cfg->backend_dir . '/shop/logistics/carriers',
	'features' => array(
		'marketplaces' => array('Amazon SP-API (demo)', 'eBay Sell API (demo)'),
	),
	'report' => $report,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
