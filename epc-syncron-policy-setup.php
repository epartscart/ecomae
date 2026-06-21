<?php
/**
 * Syncron-style inventory policy engine — schema + default policy seed.
 * Run: https://www.epartscart.com/epc-syncron-policy-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_syncron_policy.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_syncron_policy_ensure_schema($pdo);

$existing = epc_syncron_policy_list($pdo);
$seeded = array();

if (empty($existing)) {
	$seeded[] = epc_syncron_policy_save($pdo, array(
		'scope' => 'global',
		'scope_ref' => '',
		'policy_name' => 'Default automotive parts policy',
		'safety_stock_qty' => 10,
		'reorder_point' => 25,
		'reorder_qty' => 50,
		'max_stock_qty' => 200,
		'service_level_pct' => 95,
		'lead_time_days' => 7,
		'review_period_days' => 30,
		'demand_method' => 'moving_avg',
		'demand_window_days' => 90,
		'demand_alpha' => 0.3,
	));
	$seeded[] = epc_syncron_policy_save($pdo, array(
		'scope' => 'category',
		'scope_ref' => 'fast_moving',
		'policy_name' => 'Fast-moving parts (oil filters, brake pads)',
		'safety_stock_qty' => 20,
		'reorder_point' => 40,
		'reorder_qty' => 100,
		'max_stock_qty' => 500,
		'service_level_pct' => 98,
		'lead_time_days' => 3,
		'review_period_days' => 14,
		'demand_method' => 'exponential',
		'demand_window_days' => 60,
		'demand_alpha' => 0.4,
	));
	$seeded[] = epc_syncron_policy_save($pdo, array(
		'scope' => 'category',
		'scope_ref' => 'slow_moving',
		'policy_name' => 'Slow-moving / specialty parts',
		'safety_stock_qty' => 2,
		'reorder_point' => 5,
		'reorder_qty' => 10,
		'max_stock_qty' => 30,
		'service_level_pct' => 90,
		'lead_time_days' => 14,
		'review_period_days' => 60,
		'demand_method' => 'moving_avg',
		'demand_window_days' => 180,
		'demand_alpha' => 0.2,
	));
}

$policies = epc_syncron_policy_list($pdo);

echo json_encode(array(
	'status' => true,
	'message' => 'Syncron policy engine ready',
	'tables' => array('epc_erp_inv_policies', 'epc_erp_inv_demand_forecast', 'epc_erp_inv_service_levels'),
	'seeded_ids' => $seeded,
	'policies' => $policies,
	'erp_tab' => '/' . $cfg->backend_dir . '/shop/finance/erp?area=syncron&tab=syncron_policies&epc_erp_shell=1',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
