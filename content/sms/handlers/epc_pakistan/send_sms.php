<?php
/**
 * Pakistan SMS gateway (Jazz / Telenor / generic HTTP partner).
 *
 * sender_number can stay UAE (+971567607011) or a Pakistan long-code / mask —
 * change anytime under CP → SMS operators.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$db_link->query('SET NAMES utf8;');
} catch (Throwable $e) {
	header('Content-Type: application/json; charset=utf-8');
	echo json_encode(array('status' => false, 'message' => 'Database error'));
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/sms/epc_sms_helpers.php';
epc_sms_require_post_auth($DP_Config);

list($row, $params) = epc_sms_load_operator($db_link, 'epc_pakistan');
if (!$row) {
	epc_sms_exit_json(false, 'Pakistan SMS operator not configured');
}

$apiUrl = trim((string) ($params['api_url'] ?? ''));
$apiKey = trim((string) ($params['api_key'] ?? ''));
$username = trim((string) ($params['username'] ?? ''));
$password = trim((string) ($params['password'] ?? ''));
$sender = epc_sms_sender_number($params);
$body = (string) ($_POST['body'] ?? '');
$to = epc_sms_normalize_msisdn((string) ($_POST['main_field'] ?? ''), 'PK');

if ($apiUrl === '') {
	epc_sms_exit_json(false, 'Pakistan SMS API URL is required');
}
if ($apiKey === '' && ($username === '' || $password === '')) {
	epc_sms_exit_json(false, 'Provide api_key or username/password for the Pakistan gateway');
}
if ($to === '' || $body === '') {
	epc_sms_exit_json(false, 'Recipient and message body are required');
}

$payload = array(
	'api_key' => $apiKey,
	'username' => $username,
	'password' => $password,
	'from' => $sender,
	'sender' => ltrim($sender, '+'),
	'to' => $to,
	'mobilenum' => $to,
	'text' => $body,
	'message' => $body,
	'msg' => $body,
);

$headers = array();
if ($apiKey !== '') {
	$headers['Authorization'] = 'Bearer ' . $apiKey;
}

$res = epc_sms_http_json($apiUrl, $payload, $headers);
$json = $res['json'];
$success = $res['ok'];
if (is_array($json)) {
	if (array_key_exists('success', $json)) {
		$success = (bool) $json['success'];
	} elseif (array_key_exists('status', $json)) {
		$st = $json['status'];
		$success = ($st === true || $st === 'success' || $st === 'ok' || (string) $st === '0' || (int) $st === 1);
	} elseif (isset($json['code'])) {
		$success = ((int) $json['code'] === 0 || (string) $json['code'] === '00');
	}
}
if ($success) {
	epc_sms_exit_json(true, '');
}

$msg = 'Pakistan SMS send failed (HTTP ' . $res['http'] . ')';
if (is_array($json) && !empty($json['message'])) {
	$msg = (string) $json['message'];
} elseif ($res['body'] !== '') {
	$msg .= ': ' . substr($res['body'], 0, 240);
}
epc_sms_exit_json(false, $msg);
