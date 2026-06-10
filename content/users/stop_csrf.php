<?php
//Универсальный скрипт защиты от CSRF. Подключается перед обработкой форм.

//Вызываем, если не прошли проверку
if( !function_exists('error_exit') )
{
	function error_exit( $error_message )
	{
		$answer = array();
		$answer['error'] = $error_message;
		$answer['message'] = $error_message;
		$answer['status'] = false;
		exit( json_encode($answer) );
	}
}
//Вспомогательная функция проверки "Начинается ли нужная строка с определенной подстроки"
if ( !function_exists('str_starts_with') ) 
{
	function str_starts_with($str, $start) 
	{
		return (@substr_compare($str, $start, 0, strlen($start))==0);
	}
}

if ( !function_exists('epc_csrf_normalize_host') )
{
	function epc_csrf_normalize_host($host)
	{
		$host = strtolower(trim((string) $host));
		if (strpos($host, ':') !== false) {
			$host = explode(':', $host, 2)[0];
		}
		if (strpos($host, 'www.') === 0) {
			$host = substr($host, 4);
		}
		return $host;
	}
}

if ( !function_exists('epc_csrf_cp_path_prefixes') )
{
	function epc_csrf_cp_path_prefixes($DP_Config)
	{
		$backend = trim((string) $DP_Config->backend_dir, '/');
		if ($backend === '') {
			$backend = 'cp';
		}
		$prefixes = array('/' . $backend . '/');
		$domainPath = rtrim((string) $DP_Config->domain_path, '/') . '/';
		$prefixes[] = $domainPath . $backend . '/';
		$parsed = parse_url($domainPath);
		if (!empty($parsed['host'])) {
			$scheme = !empty($parsed['scheme']) ? $parsed['scheme'] : 'https';
			$host = (string) $parsed['host'];
			$altHost = (strpos($host, 'www.') === 0) ? substr($host, 4) : 'www.' . $host;
			$prefixes[] = $scheme . '://' . $altHost . '/' . $backend . '/';
		}
		return array_values(array_unique($prefixes));
	}
}

if ( !function_exists('epc_csrf_referer_is_cp') )
{
	function epc_csrf_referer_is_cp($referer, $DP_Config)
	{
		$referer = (string) $referer;
		if ($referer === '') {
			return false;
		}
		foreach (epc_csrf_cp_path_prefixes($DP_Config) as $prefix) {
			if (str_starts_with($referer, $prefix)) {
				return true;
			}
		}
		$refPath = parse_url($referer, PHP_URL_PATH);
		if (!is_string($refPath) || $refPath === '') {
			return false;
		}
		$backend = trim((string) $DP_Config->backend_dir, '/');
		if ($backend === '') {
			$backend = 'cp';
		}
		return stripos($refPath, '/' . $backend . '/') === 0;
	}
}

if ( !function_exists('epc_csrf_request_is_cp') )
{
	function epc_csrf_request_is_cp($DP_Config)
	{
		$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
		$path = parse_url($uri, PHP_URL_PATH);
		if (!is_string($path) || $path === '') {
			return false;
		}
		$backend = trim((string) $DP_Config->backend_dir, '/');
		if ($backend === '') {
			$backend = 'cp';
		}
		return stripos($path, '/' . $backend . '/') === 0;
	}
}

if ( !function_exists('epc_csrf_request_is_storefront_user_auth') )
{
	function epc_csrf_request_is_storefront_user_auth($DP_Config)
	{
		$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
		$path = parse_url($uri, PHP_URL_PATH);
		if (!is_string($path) || $path === '') {
			$path = '/';
		}
		$path = '/' . trim(str_replace('\\', '/', $path), '/');
		if ($path === '//') {
			$path = '/';
		}
		if (preg_match('#^/(?:en|ru|ar)?/?erp(?:/|$|/ajax)#i', $path)) {
			return true;
		}
		if (preg_match('#^/(?:en|ru|ar)?/?shop/erp(?:/|$)#i', $path)) {
			return true;
		}
		if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST'
			&& (!empty($_POST['authentication']) || (isset($_POST['logout']) && (string) $_POST['logout'] === 'true'))
			&& !epc_csrf_request_is_cp($DP_Config)) {
			return true;
		}
		return false;
	}
}

if ( !function_exists('epc_csrf_should_use_admin_session') )
{
	function epc_csrf_should_use_admin_session($DP_Config, $csrf_check_admin_preset)
	{
		if (!empty($csrf_check_admin_preset)) {
			return true;
		}
		if (function_exists('epc_portal_apply_config')) {
			epc_portal_apply_config($DP_Config);
		}
		if (function_exists('epc_csrf_request_is_storefront_user_auth')
			&& epc_csrf_request_is_storefront_user_auth($DP_Config)) {
			return false;
		}
		if (!empty($_COOKIE['admin_session']) && epc_csrf_request_is_cp($DP_Config)) {
			return true;
		}
		$referer = isset($_SERVER['HTTP_REFERER']) ? (string) $_SERVER['HTTP_REFERER'] : '';
		if ($referer !== '' && epc_csrf_referer_is_cp($referer, $DP_Config)) {
			return true;
		}
		if ($referer !== '' && !empty($_COOKIE['admin_session'])) {
			$refPath = parse_url($referer, PHP_URL_PATH);
			if (is_string($refPath) && preg_match('#^/(?:en|ru|ar)?/?erp(?:/|$)#i', $refPath)) {
				return false;
			}
			$refHost = parse_url($referer, PHP_URL_HOST);
			$cfgHost = parse_url((string) $DP_Config->domain_path, PHP_URL_HOST);
			$reqHost = isset($_SERVER['HTTP_HOST']) ? (string) $_SERVER['HTTP_HOST'] : '';
			if ($reqHost !== '' && strpos($reqHost, ':') !== false) {
				$reqHost = explode(':', $reqHost, 2)[0];
			}
			$hosts = array();
			foreach (array($refHost, $cfgHost, $reqHost) as $h) {
				if ($h !== null && $h !== '') {
					$hosts[epc_csrf_normalize_host($h)] = true;
				}
			}
			if (count($hosts) === 1 && (epc_csrf_request_is_cp($DP_Config) || epc_csrf_referer_is_cp($referer, $DP_Config))) {
				return true;
			}
		}
		return false;
	}
}



//Должно быть соединение с БД
if( !isset($db_link) )
{
	error_exit('Error! CSRF 0');
}


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );


//Должен быть передан код в форме
if( !isset( $_GET["csrf_guard_key"] ) && !isset( $_POST["csrf_guard_key"] ) )
{
	error_exit('Error! CSRF 1');
}
$csrf_guard_key = '';//Сюда записываем код из формы, полученный от пользователя
if( isset( $_GET["csrf_guard_key"] ) )
{
	$csrf_guard_key = $_GET["csrf_guard_key"];
}
else if( isset( $_POST["csrf_guard_key"] ) )
{
	$csrf_guard_key = $_POST["csrf_guard_key"];
}
else
{
	error_exit('Error! CSRF 2');
}
if( empty($csrf_guard_key) )
{
	error_exit('Error! CSRF 3');
}


//ВАЖНО! У админа и у простого пользователя сессии разные. Поэтому, нужно определить, откуда идет запрос. Если со страниц панели управления, то, проверяем сессию админа, если из клиентской части - значит сессию простого пользователя.
global $DP_Config;
$csrf_check_admin_preset = isset($csrf_check_admin) ? $csrf_check_admin : false;

// Multi-tenant: direct AJAX hits config.php before CP bootstrap — resolve tenant DB before session lookup.
if (!function_exists('epc_portal_apply_config')) {
	$epcPortal = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (is_file($epcPortal)) {
		require_once $epcPortal;
	}
}
if (function_exists('epc_portal_apply_config') && is_object($DP_Config)) {
	epc_portal_apply_config($DP_Config);
	if (isset($db_link)) {
		try {
			$db_link = new PDO(
				'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
				$DP_Config->user,
				$DP_Config->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			$db_link->query('SET NAMES utf8');
		} catch (Exception $e) {
			error_exit('Error! CSRF 0.1');
		}
	}
}

$csrf_check_admin = epc_csrf_should_use_admin_session($DP_Config, $csrf_check_admin_preset);

if( $csrf_check_admin )
{
	$user_session = DP_User::getAdminSession();
}
else
{
	$user_session = DP_User::getUserSession();
}

if( $user_session == false || !is_array($user_session) )
{
	error_exit('Error! CSRF 3.1');
}

//ПРОВЕРКА
if( $user_session["csrf_guard_key"] != $csrf_guard_key )
{
	error_exit('Error! CSRF 4');
}
?>