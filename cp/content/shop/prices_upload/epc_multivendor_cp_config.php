<?php
/**
 * JS config for multi-vendor price upload CP page.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
$csrf = (string) ($user_session['csrf_guard_key'] ?? '');
$backend = (string) $DP_Config->backend_dir;

header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store');

$cfg = array(
	'ajaxUrl' => '/' . $backend . '/content/shop/prices_upload/ajax_epc_multivendor_ingest.php',
	'csrfKey' => $csrf,
	'backend' => $backend,
	'pricesUrl' => '/' . $backend . '/shop/prices',
	'storagesUrl' => '/' . $backend . '/shop/logistics/storages',
);

echo 'window.EPC_MULTIVENDOR_CP = ' . json_encode($cfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
