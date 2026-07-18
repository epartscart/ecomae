<?php
/**
 * Multi-vendor CP page — runtime config (footer load).
 * Must boot like commerce/history configs: define _ASTEXE_ so a direct
 * /cp/.../epc_multivendor_cp_config.php request returns JS, not "No access".
 */
declare(strict_types=1);

header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

try {
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;

	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
		if (function_exists('epc_portal_apply_config')) {
			epc_portal_apply_config($DP_Config);
		}
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
	if (empty($user_session) || !is_array($user_session)) {
		echo 'window.EPC_MULTIVENDOR_CP={};';
		exit;
	}

	$backend = trim((string) $DP_Config->backend_dir, '/');
	if ($backend === '') {
		$backend = 'cp';
	}

	$config = array(
		'ajaxUrl' => '/' . $backend . '/content/shop/prices_upload/ajax_epc_multivendor_ingest.php',
		'csrfKey' => (string) ($user_session['csrf_guard_key'] ?? ''),
		'backend' => $backend,
		'pricesUrl' => '/' . $backend . '/shop/prices',
		'storagesUrl' => '/' . $backend . '/shop/logistics/storages',
	);

	echo 'window.EPC_MULTIVENDOR_CP=' . json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
} catch (Throwable $e) {
	echo 'window.EPC_MULTIVENDOR_CP={};';
}
