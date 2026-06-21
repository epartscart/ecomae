<?php
/**
 * CP route shop/pos/terminal — eval-safe wrapper.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir
	. '/content/shop/pos/epc_pos_terminal.php';

if (empty($user_session) || !is_array($user_session)) {
	$login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a> to open POS Terminal.</div>';
} elseif (!is_file($include)) {
	echo '<div class="alert alert-danger"><strong>POS module not found.</strong> Run epc-pos-setup.php on the server.</div>';
} else {
	include $include;
}
?>
