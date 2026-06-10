<?php
/**
 * Document Control — runtime config JS (footer load, outside CP .row pane).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
if (empty($user_session) || !is_array($user_session)) {
	echo 'window.EPC_DOCUMENT_CONTROL={};';
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';

$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$ajaxUrl = function_exists('epc_document_control_cp_ajax_url')
	? epc_document_control_cp_ajax_url()
	: ('/' . $backend . '/content/shop/document_control/ajax_document_control_endpoint.php');

$config = array(
	'ajaxUrl' => $ajaxUrl,
	'csrf' => (string) ($user_session['csrf_guard_key'] ?? ''),
);

echo 'window.EPC_DOCUMENT_CONTROL=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
