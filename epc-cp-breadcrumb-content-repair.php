<?php
/**
 * Repair missing intermediate CP content folders (breadcrumb "404 Not found" on shop/foo/bar routes).
 * Run: https://www.epartscart.com/epc-cp-breadcrumb-content-repair.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_cp_breadcrumb.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$folders = epc_cp_breadcrumb_repair_intermediate_folders($pdo);

echo json_encode(array(
	'status' => true,
	'message' => 'CP breadcrumb folder nodes ensured',
	'folders' => $folders,
	'db' => $cfg->db,
	'host' => $_SERVER['HTTP_HOST'] ?? '',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
