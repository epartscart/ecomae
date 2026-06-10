<?php
/**
 * AI agent CP — runtime config JS (footer load, outside .row pane).
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
	echo 'window.EPC_AGENT_CP={};';
	exit;
}

$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$csrf = (string) ($user_session['csrf_guard_key'] ?? '');
$ajaxUrl = '/' . $backend . '/content/shop/parts_agent/ajax_epc_parts_agent_cp.php';

$config = array(
	'ajaxUrl' => $ajaxUrl,
	'csrfKey' => $csrf,
);

echo 'window.EPC_AGENT_CP=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
