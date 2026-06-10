<?php
/**
 * CP route shop/orders/guide — eval-safe wrapper.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$epc_cp_login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $epc_cp_login
		. '">log in to the control panel</a> to view this guide.</div>';
	return;
}

$epc_guide_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
	. '/content/shop/order_process/order_fulfilment_guide.php';

if (!is_file($epc_guide_include)) {
	echo '<div class="alert alert-danger"><strong>Guide could not load:</strong> file not found.</div>';
	return;
}

include $epc_guide_include;

?>
