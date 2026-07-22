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

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';
$ver = epc_cp_page_asset_version() . 'lc2';
$backend = (string)$GLOBALS['DP_Config']->backend_dir;
epc_cp_register_page_assets(
	array(
		'/content/shop/finance/epc_erp_ui.css?v=' . rawurlencode($ver),
		'/content/general_pages/epc_logistics_carriers.css?v=' . rawurlencode($ver),
	),
	array(
		'/' . $backend . '/content/shop/logistics/logistics_carriers.js?v=' . rawurlencode($ver),
	)
);

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/logistics/logistics_carriers.php';
if (!is_file($include)) {
	echo '<div class="alert alert-danger">Logistics carriers module not found.</div>';
	return;
}
include $include;

// Close PHP before template HTML when embedded in CP template eval (dp_core.php).
?>
