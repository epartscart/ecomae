<?php
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo 'window.EPC_PHC={};';
	exit;
}

$token = 'epartscart-deploy-2026';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
	if (function_exists('epc_deploy_token')) {
		$token = epc_deploy_token();
	}
}

echo 'window.EPC_PHC = ' . json_encode(array(
	'apiUrl' => 'https://www.ecomae.com/epc-platform-health-checkup-api.php?token=' . rawurlencode($token),
	'storageKey' => 'epc_platform_health_checkup_last_run',
), JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP) . ';';
