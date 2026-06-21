<?php
/**
 * CP route shop/procurement/procurement — eval-safe wrapper.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a> to open Procurement.</div>';
	return;
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
	. '/content/shop/procurement/procurement_main.php';

if (!is_file($include)) {
	echo '<div class="alert alert-danger"><strong>Procurement module not found.</strong> Run procurement-setup.php on the server.</div>';
	return;
}

include $include;

?>
