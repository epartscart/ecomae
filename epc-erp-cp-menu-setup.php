<?php
/**
 * Top-level ERP Suite CP menu (shell mode) — Super CP and tenant sites.
 *
 * Run: https://www.ecomae.com/epc-erp-cp-menu-setup.php?token=epartscart-deploy-2026
 *      https://www.epartscart.com/epc-erp-cp-menu-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$menu = epc_cp_mainstream_menu_apply($pdo);

echo "ERP Suite menu updated.\n";
echo "control_items_id (hub): " . (int) $menu['items']['erp_hub'] . "\n";
echo "group_id (ERP): " . (int) $menu['erp_group'] . "\n";
echo "URL: /" . $cfg->backend_dir . "/shop/finance/erp?epc_erp_shell=1\n";
echo "Launcher: /" . $cfg->backend_dir . "/shop/finance/erp/erp_launcher.php\n";
