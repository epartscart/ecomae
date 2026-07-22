<?php
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a>.</div>';
	return;
}

// CSS/JS for this URL are declared in epc_cp_page_assets.php (shop/logistics/carriers).
// The module also emits a CSS <link> fallback so the hub never renders unstyled.

$backend = (string)$GLOBALS['DP_Config']->backend_dir;
$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/logistics/logistics_carriers.php';
if (!is_file($include)) {
	// Some CP boots set DOCUMENT_ROOT to the backend folder.
	$alt = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/logistics/logistics_carriers.php';
	if (is_file($alt)) {
		$include = $alt;
	}
}
if (!is_file($include)) {
	echo '<div class="alert alert-danger">Logistics carriers module not found.</div>';
	return;
}
include $include;

// Close PHP before template HTML when embedded in CP template eval (dp_core.php).
?>
