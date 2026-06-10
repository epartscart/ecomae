<?php
/**
 * CP: industry filter switcher (GET redirect).
 */
define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

$DP_Config = new DP_Config();
epc_portal_apply_config($DP_Config);

if (session_status() !== PHP_SESSION_ACTIVE) {
	session_start();
}

$code = isset($_GET['industry']) ? preg_replace('/[^a-z0-9_]/', '', strtolower($_GET['industry'])) : '';
$all = epc_portal_industries();
if ($code !== '' && isset($all[$code])) {
	$_SESSION[epc_portal_cp_industry_session_key()] = $code;
}

$back = isset($_GET['back']) ? (string) $_GET['back'] : ('/' . $DP_Config->backend_dir);
if (strpos($back, '://') !== false || strpos($back, "\n") !== false) {
	$back = '/' . $DP_Config->backend_dir;
}
header('Location: ' . $back, true, 302);
exit;
