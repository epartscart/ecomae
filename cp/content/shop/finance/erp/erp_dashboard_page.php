<?php
/**
 * Animated ERP dashboard — CP page wrapper.
 * Mirrors erp_advanced_guide_page.php: checks the admin session, then includes
 * the dashboard body from the backend directory.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
    $user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
    $login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
    echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a> to view the dashboard.</div>';
    return;
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
    . '/content/shop/finance/erp/erp_dashboard.php';

if (!is_file($include)) {
    echo '<div class="alert alert-danger">ERP dashboard file not found.</div>';
    return;
}

include $include;
?>
