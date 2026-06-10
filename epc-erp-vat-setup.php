<?php
/**
 * UAE VAT schema + settings migration.
 * Run: https://www.epartscart.com/epc-erp-vat-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_schema.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/content/shop/finance/epc_uae_vat.php';
require_once __DIR__ . '/content/shop/pricing/epc_pricing.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_erp_ensure_schema($pdo);
epc_erp_gl_ensure_schema($pdo);
epc_uae_vat_ensure_settings($pdo);

$recalc = epc_uae_vat_recalc_purchases($pdo);

$suppliers_uae = (int)$pdo->query("SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1 AND UPPER(`country_code`) IN ('AE','UAE')")->fetchColumn();
$vat_rate = epc_uae_vat_rate_percent($pdo);

echo json_encode(array(
	'status' => true,
	'message' => 'UAE VAT setup complete',
	'vat_rate_percent' => $vat_rate,
	'vat_uae_sales_only' => epc_pricing_get_setting($pdo, 'vat_uae_sales_only', '1'),
	'purchases_recalculated' => $recalc,
	'suppliers_uae' => $suppliers_uae,
	'erp_vat_tab' => '/' . $cfg->backend_dir . '/shop/finance/erp?tab=vat_return',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
