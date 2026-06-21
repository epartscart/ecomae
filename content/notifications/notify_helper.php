<?php
//Скрипт с определениями вспомогательных функций для уведомлений

// -------------------------------------------------------------------------------------------------------
// Функция отправки уведомления
function send_notify($notify_name, $notify_vars, $persons, $to_wait=true, $files=array())
{
	global $DP_Config;
	global $multilang_params;
	global $db_link;

	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/send_notify_dispatch.php';

	if (!isset($db_link) || !$db_link) {
		try {
			$db_link = new PDO(
				'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
				$DP_Config->user,
				$DP_Config->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			$db_link->query('SET NAMES utf8;');
		} catch (Throwable $e) {
			$db_link = null;
		}
	}

	if ($db_link) {
		$inline = docpart_dispatch_notification(
			$db_link,
			$DP_Config,
			$notify_name,
			$notify_vars,
			$persons,
			$files,
			$multilang_params
		);
		if (is_array($inline) && !empty($inline['status'])) {
			if (function_exists('epc_notify_store_answer')) {
				epc_notify_store_answer($inline);
			}
			return $inline;
		}
	}

	$postdata = http_build_query(
		array(
			'check' => $DP_Config->secret_succession,
			'name' => $notify_name,
			'vars' => json_encode($notify_vars),
			'persons' => json_encode($persons),
			'multilang_params' => json_encode($multilang_params),
			'files' => json_encode($files),
		)
	);

	$host = '';
	if (!empty($_SERVER['HTTP_HOST'])) {
		$host = (string)$_SERVER['HTTP_HOST'];
	} elseif (is_object($DP_Config) && !empty($DP_Config->domain_path)) {
		$host = (string)parse_url($DP_Config->domain_path, PHP_URL_HOST);
	}

	$urls = array();
	$urls[] = rtrim((string)$DP_Config->domain_path, '/') . '/content/notifications/send_notify.php';
	if ($host !== '') {
		$urls[] = 'http://127.0.0.1/content/notifications/send_notify.php';
	}

	$timeout = $to_wait ? 45 : 1;
	$connectTimeout = $to_wait ? 10 : 1;
	$curl_result = false;
	$last_error = '';

	foreach ($urls as $url) {
		$curl = curl_init();
		curl_setopt($curl, CURLOPT_URL, $url);
		curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
		curl_setopt($curl, CURLOPT_POST, true);
		curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
		curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
		curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
		curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, $connectTimeout);
		curl_setopt($curl, CURLOPT_TIMEOUT, $timeout);
		curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
		if ($host !== '' && strpos($url, '127.0.0.1') !== false) {
			curl_setopt($curl, CURLOPT_HTTPHEADER, array('Host: ' . $host));
		}
		$curl_result = curl_exec($curl);
		if ($curl_result === false) {
			$last_error = curl_error($curl);
		}
		curl_close($curl);
		if ($curl_result !== false && $curl_result !== '') {
			break;
		}
	}

	if ($curl_result === false || $curl_result === '') {
		$fail = array(
			'status' => false,
			'message' => $last_error !== '' ? $last_error : 'Empty response from send_notify.php',
		);
		if (function_exists('epc_notify_store_answer')) {
			epc_notify_store_answer($fail);
		}
		return $fail;
	}

	$decoded = json_decode($curl_result, true);
	if (function_exists('epc_notify_store_answer') && is_array($decoded)) {
		epc_notify_store_answer($decoded);
	}
	return $decoded;
}
