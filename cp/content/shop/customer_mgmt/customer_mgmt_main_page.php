<?php
/**
 * CP route shop/customer_mgmt/customer_mgmt — eval-safe wrapper.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a> to open Customer management.</div>';
	return;
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
	. '/content/shop/customer_mgmt/customer_mgmt_main.php';

if (!is_file($include)) {
	echo '<div class="alert alert-danger"><strong>Customer management module not found.</strong> Run customer-mgmt-setup.php on the server.</div>';
	return;
}

include $include;

?>
