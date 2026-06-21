<?php
/**
 * D365 F&O — new module coverage schema setup.
 *
 * Creates tables for the modules added on top of the Greeting agent build:
 * Bank Reconciliation, Intercompany Accounting, Syncron-style Inventory Policies,
 * and PIM Custom Fields.
 *
 * Run: https://www.epartscart.com/epc-erp-d365-modules-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

require_once __DIR__ . '/content/shop/finance/epc_erp_bank_reconciliation.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_intercompany.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_syncron_policy.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_pim_custom_fields.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$modules = array(
	'bank_reconciliation' => 'epc_erp_bank_recon_ensure_schema',
	'intercompany'        => 'epc_erp_intercompany_ensure_schema',
	'syncron_policy'      => 'epc_syncron_policy_ensure_schema',
	'pim_custom_fields'   => 'epc_pim_ensure_schema',
);

$results = array();
foreach ($modules as $name => $fn) {
	try {
		$fn($pdo);
		$results[$name] = 'ok';
	} catch (Throwable $e) {
		$results[$name] = 'error: ' . $e->getMessage();
	}
}

$tableCheck = array(
	'epc_erp_bank_recon_sessions', 'epc_erp_bank_recon_statement_lines', 'epc_erp_bank_recon_rules',
	'epc_erp_legal_entities', 'epc_erp_ic_transactions', 'epc_erp_ic_balances',
	'epc_erp_ic_settlements', 'epc_erp_ic_elimination_entries',
	'epc_erp_inv_policies', 'epc_erp_inv_demand_forecast', 'epc_erp_inv_service_levels',
	'epc_pim_fields', 'epc_pim_field_options', 'epc_pim_item_values',
);

$tables = array();
foreach ($tableCheck as $t) {
	try {
		$cnt = (int) $pdo->query('SELECT COUNT(*) FROM `' . $t . '`')->fetchColumn();
		$tables[$t] = array('exists' => true, 'rows' => $cnt);
	} catch (Throwable $e) {
		$tables[$t] = array('exists' => false, 'error' => $e->getMessage());
	}
}

$be = $cfg->backend_dir;
echo json_encode(array(
	'status' => true,
	'message' => 'New modules schema created — bank recon, intercompany, syncron, PIM custom fields',
	'modules' => $results,
	'tables' => $tables,
	'erp_tabs' => array(
		'bank_recon'   => '/' . $be . '/shop/finance/erp?area=bank_recon&tab=recon_sessions&epc_erp_shell=1',
		'intercompany' => '/' . $be . '/shop/finance/erp?area=intercompany&tab=entities&epc_erp_shell=1',
		'syncron'      => '/' . $be . '/shop/finance/erp?area=syncron&tab=policies&epc_erp_shell=1',
		'pim_fields'   => '/' . $be . '/shop/finance/erp?area=pim&tab=custom_fields&epc_erp_shell=1',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
