<?php
/**
 * Footer config for CP file manager (eval-safe; no inline scripts in page pane).
 */
declare(strict_types=1);

@ini_set('display_errors', '0');
@ini_set('html_errors', '0');
error_reporting(E_ALL & ~E_DEPRECATED & ~E_USER_DEPRECATED & ~E_NOTICE & ~E_STRICT);

header('Content-Type: application/javascript; charset=utf-8');
header('X-Content-Type-Options: nosniff');
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

	$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}

	$lang = 'en';
	if (!empty($_COOKIE['lang_cp'])) {
		$lang = preg_replace('/[^a-z_]/', '', strtolower((string) $_COOKIE['lang_cp'])) ?: 'en';
	}

	$langFileMap = array(
		'ru' => 'ru',
		'ar' => 'ar',
		'de' => 'de',
		'es' => 'es',
		'fr' => 'fr',
		'zh' => 'zh_CN',
		'zh_cn' => 'zh_CN',
		'pt' => 'pt_BR',
		'pt_br' => 'pt_BR',
	);
	$langKey = strtolower($lang);
	$elfLang = isset($langFileMap[$langKey]) ? $langFileMap[$langKey] : 'en';

	$cfg = array(
		'connectorUrl' => '/' . $backend . '/lib/elfinder/php/connector.php',
		'csrf' => (string) ($user_session['csrf_guard_key'] ?? ''),
		'lang' => $elfLang,
		'langUrl' => $elfLang === 'en'
			? ''
			: ('/' . $backend . '/lib/elfinder/js/i18n/elfinder.' . $elfLang . '.js'),
		'height' => 560,
	);

	echo 'window.EPC_FILEMANAGER=' . json_encode($cfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
} catch (Throwable $e) {
	echo 'window.EPC_FILEMANAGER=' . json_encode(array(
		'connectorUrl' => '/cp/lib/elfinder/php/connector.php',
		'csrf' => '',
		'lang' => 'en',
		'langUrl' => '',
		'height' => 560,
		'error' => 'config_failed',
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
}
