<?php
/**
 * Register CP: Documents → Document Control (current site database).
 * https://www.ecomae.com/epc-document-control-cp-setup.php?token=epartscart-deploy-2026
 * Probe only: ?probe=1
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/document_control/epc_document_control_cp_install.php';

$cfg = new DP_Config();
$backend = trim((string) $cfg->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

try {
	$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
	$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => 'DB connect failed: ' . $e->getMessage()), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

$host = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? 'localhost');
$probeOnly = isset($_GET['probe']) && (string) $_GET['probe'] === '1';

if ($probeOnly) {
	echo json_encode(array(
		'status' => true,
		'mode' => 'probe',
		'host' => $host,
		'db' => $cfg->db,
		'probe' => epc_document_control_cp_probe($pdo, $_SERVER['DOCUMENT_ROOT'], $backend),
		'url' => epc_document_control_cp_public_url($host, $backend),
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

try {
	$result = epc_document_control_cp_install($pdo, $backend);
} catch (Exception $e) {
	echo json_encode(array('status' => false, 'message' => $e->getMessage(), 'db' => $cfg->db), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

echo json_encode(array(
	'status' => true,
	'message' => 'Document Control System registered',
	'host' => $host,
	'db' => $cfg->db,
	'hub_content_id' => $result['hub_content_id'],
	'content_id' => $result['content_id'],
	'legacy_redirect_id' => $result['legacy_redirect_id'],
	'menu_group_id' => $result['menu']['documents_group'],
	'menu_item_id' => $result['menu']['document_control_item'],
	'probe' => epc_document_control_cp_probe($pdo, $_SERVER['DOCUMENT_ROOT'], $backend),
	'urls' => array(
		'cp_panel' => epc_document_control_cp_public_url($host, $backend),
		'platform_erp' => epc_document_control_cp_public_url($host, $backend),
		'legacy_url' => (strpos($host, ':') !== false ? 'https' : ((!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http')) . '://' . $host . '/' . $backend . '/shop/modul-pechati-dokumentov',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
