<?php
/**
 * Advanced ERP guide — CP page wrapper.
 * Mirrors erp_guide_page.php: checks the admin session, then includes the
 * guide body from the backend directory.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
    $user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
    $login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
    echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a> to view this guide.</div>';
    return;
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
    . '/content/shop/finance/erp/erp_advanced_guide.php';

if (!is_file($include)) {
    echo '<div class="alert alert-danger">Advanced ERP guide file not found.</div>';
    return;
}

include $include;

// Close PHP before any template HTML when embedded in CP template eval.
?>
