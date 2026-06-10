<?php
/**
 * Provider / operator console — CP page wrapper (Super CP).
 * Admin-session gated, then includes the console body from the backend dir.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
    $user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
    $login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
    echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a> to view the provider console.</div>';
    return;
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
    . '/content/shop/finance/erp/erp_provider_console.php';

if (!is_file($include)) {
    echo '<div class="alert alert-danger">Provider console file not found.</div>';
    return;
}

include $include;
?>
