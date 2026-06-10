<?php
/**
 * ERP staff departments, dummy users, workflows — setup.
 * Run: https://www.epartscart.com/epc-erp-staff-setup.php?token=epartscart-deploy-2026&sample=1
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

$sample = null;
if (!empty($_GET['sample']) && $_GET['sample'] !== '0') {
	$sample = epc_erp_staff_seed_demo($pdo, $cfg);
}

if (!empty($_GET['payroll_reseed']) && $_GET['payroll_reseed'] !== '0') {
	require_once __DIR__ . '/content/shop/finance/epc_erp_payroll.php';
	epc_erp_payroll_apply_demo_days($pdo);
	$pdo->exec('DELETE FROM `epc_erp_payroll_lines`');
	$pdo->exec('DELETE FROM `epc_erp_payroll_runs`');
	$payrollReseed = epc_erp_payroll_seed_demo($pdo);
} else {
	$payrollReseed = null;
}

$base = rtrim($cfg->domain_path, '/');
$be = $cfg->backend_dir;

echo json_encode(array(
	'status' => true,
	'message' => 'ERP staff departments & workflows configured',
	'sample' => $sample,
	'payroll_reseed' => $payrollReseed,
	'report' => epc_erp_staff_demo_report($pdo),
	'urls' => array(
		'erp_cp' => $base . '/' . $be . '/shop/finance/erp',
		'erp_guide' => $base . '/' . $be . '/shop/finance/erp/guide',
		'erp_portal' => $base . '/en/shop/erp',
		'staff_demo_json' => $base . '/epc-erp-staff-demo.php?token=epartscart-deploy-2026',
		'staff_setup' => $base . '/epc-erp-staff-setup.php?token=epartscart-deploy-2026&sample=1',
	),
	'hint' => 'Dummy staff login at /en/shop/erp with erp.sales@epartscart.local etc. Password: EpcStaff2026!',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
