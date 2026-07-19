<?php
/**
 * Multi-vendor CP page — runtime config (footer load).
 * Always emits ajaxUrl/sampleUrl so upload works even when session lookup
 * fails on this separate script request (common on multi-tenant CP).
 */
declare(strict_types=1);

@ini_set('display_errors', '0');
@ini_set('html_errors', '0');
error_reporting(E_ALL & ~E_DEPRECATED & ~E_USER_DEPRECATED & ~E_NOTICE & ~E_STRICT);

header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

try {
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}

	$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	require_once $docRoot . '/config.php';

	$epcMvConnect = static function ($cfg) {
		$dbHost = trim((string) ($cfg->host ?? ''));
		if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
			$dbHost = '127.0.0.1';
		}
		return new PDO(
			'mysql:host=' . $dbHost . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	};

	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;

	if (is_file($docRoot . '/content/general_pages/epc_portal.php')) {
		require_once $docRoot . '/content/general_pages/epc_portal.php';
		if (function_exists('epc_portal_apply_config')) {
			ob_start();
			try {
				epc_portal_apply_config($DP_Config);
			} catch (Throwable $e) {
			}
			ob_end_clean();
			$GLOBALS['DP_Config'] = $DP_Config;
		}
	}

	try {
		$db_link = $epcMvConnect($DP_Config);
		$GLOBALS['db_link'] = $db_link;
	} catch (Throwable $e) {
		$db_link = null;
	}

	require_once $docRoot . '/content/users/dp_user.php';
	$user_session = array();
	if ($db_link instanceof PDO) {
		try {
			$sess = DP_User::getAdminSession();
			if (is_array($sess) && !empty($sess)) {
				$user_session = $sess;
			}
		} catch (Throwable $e) {
			$user_session = array();
		}
	}

	// Fallback CSRF from cookie when session row lookup failed but admin cookies exist.
	if (empty($user_session['csrf_guard_key']) && !empty($_COOKIE['csrf_guard_key'])) {
		$user_session['csrf_guard_key'] = (string) $_COOKIE['csrf_guard_key'];
	}

	$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}

	$config = array(
		'ajaxUrl' => '/' . $backend . '/content/shop/prices_upload/ajax_epc_multivendor_ingest.php',
		'sampleUrl' => '/' . $backend . '/content/shop/prices_upload/epc_multivendor_sample_file.php',
		'csrfKey' => (string) ($user_session['csrf_guard_key'] ?? ''),
		'backend' => $backend,
		'pricesUrl' => '/' . $backend . '/shop/prices',
		'storagesUrl' => '/' . $backend . '/shop/logistics/storages',
	);

	// Merge so a late footer load cannot wipe page bootstrap / boot JSON.
	echo 'window.EPC_MULTIVENDOR_CP=Object.assign({},window.EPC_MULTIVENDOR_CP||{},'
		. json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ');';
} catch (Throwable $e) {
	echo 'window.EPC_MULTIVENDOR_CP=Object.assign({ajaxUrl:"/cp/content/shop/prices_upload/ajax_epc_multivendor_ingest.php",sampleUrl:"/cp/content/shop/prices_upload/epc_multivendor_sample_file.php",backend:"cp",pricesUrl:"/cp/shop/prices",storagesUrl:"/cp/shop/logistics/storages"},window.EPC_MULTIVENDOR_CP||{});';
}
