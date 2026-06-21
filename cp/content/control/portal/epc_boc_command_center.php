<?php
/**
 * BOC — Operations Command Center (route). One controlled view over the whole
 * fleet: commerce tenants, ERP-only clients and demo sandboxes, with live RAG
 * health. Renders inside the BOC console shell (BOS <-> BOC information flow).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_console.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php';

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
    echo '<div class="alert alert-warning">Command Center is available on <strong>BOC</strong> (www.ecomae.com Super CP) only.</div>';
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
$opId = (class_exists('DP_User') && method_exists('DP_User', 'getUserId')) ? (int) DP_User::getUserId() : 0;
$nav = function_exists('epc_boc_nav_for_user') ? epc_boc_nav_for_user($db_link, $opId) : epc_boc_nav();

epc_boc_console_open(array('active' => 'command_center', 'title' => 'Command Center', 'base' => $base, 'operator' => $operatorName, 'env' => 'Production', 'nav' => $nav, 'scope' => 'All units · Fleet'));
epc_boc_render_command_center($db_link, $base);
epc_boc_console_close();
?>
