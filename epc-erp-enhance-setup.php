<?php
/**
 * ERP Phase 8 schema + tab access refresh.
 * Run: https://www.ecomae.com/epc-erp-enhance-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_phase8.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_staff.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_erp_full_ensure_schema($pdo);
epc_erp_phase8_ensure_schema($pdo);
epc_erp_staff_ensure_schema($pdo);
epc_erp_staff_seed_departments($pdo);

echo "OK — ERP Phase 8 schema ready\n";
echo "- epc_erp_contacts, epc_erp_rfq, epc_erp_delivery_notes\n";
echo "- epc_erp_bank_statement_lines, epc_erp_expense_reports\n";
echo "- Department tab seeds updated (run on each tenant DB)\n";
