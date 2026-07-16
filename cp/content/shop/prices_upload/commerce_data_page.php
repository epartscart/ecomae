<?php
/**
 * CP route shop/prices/commerce — eval-safe wrapper.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$epc_cp_login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $epc_cp_login
		. '">log in to the control panel</a> to upload commerce data.</div>';
	return;
}

$epc_commerce_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
	. '/content/shop/prices_upload/commerce_data_upload.php';

if (!is_file($epc_commerce_include)) {
	echo '<div class="alert alert-danger"><strong>Commerce upload could not load:</strong> file not found.</div>';
	return;
}

include $epc_commerce_include;
?>
