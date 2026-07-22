<?php
/**
 * Etisalat (e&) UAE — partner / enterprise messaging gateway.
 *
 * Fill api_url + api_key from your Etisalat Business / aggregator contract.
 * sender_number defaults to +971567607011 and can be changed in CP → SMS operators.
 *
 * Expected partner API: POST JSON { from, to, text, api_key } → { success|status, message }
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

list($row, $params) = epc_sms_load_operator($db_link, 'epc_etisalat');
if (!$row) {
	epc_sms_exit_json(false, 'Etisalat operator not configured');
}

$apiUrl = trim((string) ($params['api_url'] ?? ''));
$apiKey = trim((string) ($params['api_key'] ?? ''));
$apiSecret = trim((string) ($params['api_secret'] ?? ''));
$sender = epc_sms_sender_number($params);
$body = (string) ($_POST['body'] ?? '');
$to = epc_sms_normalize_msisdn((string) ($_POST['main_field'] ?? ''), 'AE');

if ($apiUrl === '') {
	epc_sms_exit_json(false, 'Etisalat API URL is required (from your e& Business / SMS partner contract)');
}
if ($apiKey === '') {
	epc_sms_exit_json(false, 'Etisalat API key is required');
}
if ($to === '' || $body === '') {
	epc_sms_exit_json(false, 'Recipient and message body are required');
}

$payload = array(
	'api_key' => $apiKey,
	'from' => $sender,
	'sender' => $sender,
	'to' => $to,
	'recipient' => $to,
	'text' => $body,
	'body' => $body,
	'message' => $body,
);
if ($apiSecret !== '') {
	$payload['api_secret'] = $apiSecret;
}

$headers = array();
$authHeader = trim((string) ($params['auth_header'] ?? ''));
if ($authHeader !== '') {
	$headers['Authorization'] = $authHeader;
} elseif ($apiSecret !== '') {
	$headers['Authorization'] = 'Bearer ' . $apiKey;
}

$res = epc_sms_http_json($apiUrl, $payload, $headers);
$json = $res['json'];
$success = $res['ok'];
if (is_array($json)) {
	if (array_key_exists('success', $json)) {
		$success = (bool) $json['success'];
	} elseif (array_key_exists('status', $json)) {
		$success = ($json['status'] === true || $json['status'] === 'success' || $json['status'] === 'ok' || (int) $json['status'] === 1);
	}
}
if ($success) {
	epc_sms_exit_json(true, '');
}

$msg = 'Etisalat send failed (HTTP ' . $res['http'] . ')';
if (is_array($json) && !empty($json['message'])) {
	$msg = (string) $json['message'];
} elseif ($res['body'] !== '') {
	$msg .= ': ' . substr($res['body'], 0, 240);
}
epc_sms_exit_json(false, $msg);
