<?php
/**
 * UAE tax compliance tab — runtime config (footer load, outside CP eval pane).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($DP_Config);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
if (empty($user_session) || !is_array($user_session)) {
	echo 'window.EPC_UAE_TAX_COMPLIANCE={};';
	exit;
}

$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$ajaxUrl = '/' . $backend . '/content/shop/finance/erp/ajax_erp_endpoint.php';

$config = array(
	'ajaxUrl' => $ajaxUrl,
	'csrf' => (string) ($user_session['csrf_guard_key'] ?? ''),
);

echo 'window.EPC_UAE_TAX_COMPLIANCE=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
