<?php
/**
 * BOC — Warehouse & Inventory Control (route). Fleet-wide multi-warehouse
 * control: stock value, locations and replenishment risk across every tenant.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_console.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_advanced.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
    echo '<div class="alert alert-warning">Warehouse control is available on <strong>BOC</strong> (Super CP) only.</div>';
    return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
    global $DP_Config;
    echo '<div class="alert alert-warning">Please <a href="/' . epc_boc_h((string) $DP_Config->backend_dir) . '/">log in to BOC</a>.</div>';
    return;
}

global $db_link;
if (!isset($db_link) || !($db_link instanceof PDO)) {
    echo '<div class="alert alert-danger">Platform database unavailable.</div>';
    return;
}

$backend = (string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp');
$base = '/' . trim($backend, '/');
$operatorName = (class_exists('DP_User') && method_exists('DP_User', 'getName') && (string) DP_User::getName() !== '') ? (string) DP_User::getName() : 'Operator';

epc_boc_console_open(array('active' => 'warehouse_control', 'title' => 'Warehouse & Inventory', 'base' => $base, 'operator' => $operatorName, 'env' => 'Production'));
epc_boc_render_warehouse_control($db_link, $base);
epc_boc_console_close();
