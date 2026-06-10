<?php
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo 'window.EPC_PG={};';
	exit;
}

$backend = trim((string) $DP_Config->backend_dir, '/');
$token = 'epartscart-deploy-2026';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
	if (function_exists('epc_deploy_token')) {
		$token = epc_deploy_token();
	}
}

echo 'window.EPC_PG = ' . json_encode(array(
	'ajaxUrl' => '/' . $backend . '/content/control/portal/ajax_platform_governance.php',
	'healthApi' => 'https://www.ecomae.com/epc-platform-governance-health-api.php?token=' . rawurlencode($token),
), JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP) . ';';
