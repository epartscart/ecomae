<?php
/**
 * AI agent CP — runtime config JS (footer load, outside .row pane).
 * Must never 500: create $db_link before DP_User::getAdminSession().
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

	$db_link = null;
	try {
		$dbHost = trim((string) ($DP_Config->host ?? ''));
		if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
			$dbHost = '127.0.0.1';
		}
		$db_link = new PDO(
			'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
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
	if (empty($user_session['csrf_guard_key']) && !empty($_COOKIE['csrf_guard_key'])) {
		$user_session['csrf_guard_key'] = (string) $_COOKIE['csrf_guard_key'];
	}

	$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}

	$config = array(
		'ajaxUrl' => '/' . $backend . '/content/shop/parts_agent/ajax_epc_parts_agent_cp.php',
		'csrfKey' => (string) ($user_session['csrf_guard_key'] ?? ''),
	);

	echo 'window.EPC_AGENT_CP=Object.assign({},window.EPC_AGENT_CP||{},'
		. json_encode($config, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ');';
} catch (Throwable $e) {
	echo 'window.EPC_AGENT_CP=Object.assign({ajaxUrl:"/cp/content/shop/parts_agent/ajax_epc_parts_agent_cp.php",csrfKey:""},window.EPC_AGENT_CP||{});';
}
