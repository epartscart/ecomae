<?php
/**
 * ERP inventory, fixed assets, opening balances — schema migration.
 * Run: https://www.epartscart.com/epc-erp-inventory-assets-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_inventory.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_fixed_assets.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_opening.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_erp_full_ensure_schema($pdo);
$synced = epc_erp_inventory_sync_warehouses($pdo);

$whCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_erp_inv_warehouses`')->fetchColumn();
$itemCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_erp_inv_items`')->fetchColumn();
$faCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_erp_fa_assets`')->fetchColumn();

$backend = '/' . $cfg->backend_dir;

echo json_encode(array(
	'status' => true,
	'message' => 'ERP inventory & fixed assets schema ready',
	'warehouses_synced' => $synced,
	'warehouses_total' => $whCount,
	'inventory_items' => $itemCount,
	'fixed_assets' => $faCount,
	'erp_tabs' => array(
		'inventory' => $backend . '/shop/finance/erp?tab=inventory',
		'fixed_assets' => $backend . '/shop/finance/erp?tab=fixed_assets',
		'opening_balances' => $backend . '/shop/finance/erp?tab=opening_balances',
	),
	'platform' => 'https://www.ecomae.com/platform#erp-inventory',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
