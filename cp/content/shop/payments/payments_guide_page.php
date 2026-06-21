<?php
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir . '/content/shop/payments/payments_guide.php';
if (!is_file($include)) {
	echo '<div class="alert alert-danger">Payments guide not found.</div>';
	return;
}
include $include;

// Close PHP before template HTML when embedded in CP template eval (dp_core.php).
?>
