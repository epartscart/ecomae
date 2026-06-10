<?php
/**
 * ERP staff / workflow demo JSON report.
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_staff.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_erp_full_ensure_schema($pdo);

$base = rtrim($cfg->domain_path, '/');
echo json_encode(array(
	'status' => true,
	'title' => 'eParts Cart — ERP staff & workflow demo',
	'generated_at' => date('c'),
	'report' => epc_erp_staff_demo_report($pdo),
	'department_tabs' => array_map(function ($row) {
		return array('name' => $row['name'], 'tabs' => $row['tabs'], 'workflows' => $row['workflows']);
	}, epc_erp_departments_config()),
	'urls' => array(
		'erp' => $base . '/' . $cfg->backend_dir . '/shop/finance/erp',
		'erp_tab_staff' => $base . '/' . $cfg->backend_dir . '/shop/finance/erp?tab=staff',
		'erp_tab_workflow' => $base . '/' . $cfg->backend_dir . '/shop/finance/erp?tab=workflow',
		'portal' => $base . '/en/shop/erp',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
