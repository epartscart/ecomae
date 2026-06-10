<?php
/**
 * UAE e-Invoicing schema setup.
 * Run: https://www.epartscart.com/epc-einvoice-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_einvoice_schema.php';
require_once __DIR__ . '/content/shop/finance/epc_einvoice.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_einvoice_ensure_schema($pdo);
$readiness = epc_einvoice_readiness_checklist($pdo);

echo json_encode(array(
	'status' => true,
	'message' => 'UAE e-Invoicing module ready',
	'guidelines' => 'V1.0 · 23 Feb 2026',
	'specification' => epc_einvoice_constants()['specification_id'],
	'readiness_percent' => $readiness['percent'],
	'erp_tab' => '/' . $cfg->backend_dir . '/shop/finance/erp?tab=einvoice',
	'tables' => array('epc_einvoice_settings', 'epc_einvoice_documents', 'epc_einvoice_lines', 'epc_einvoice_events', 'epc_einvoice_buyer_profiles'),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
