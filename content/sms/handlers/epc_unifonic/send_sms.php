<?php
/**
 * Unifonic (GCC / MENA) SMS handler.
 * CP params: appsid, sender_number (or sender_id), api_url (optional).
 * Default sender: +971567607011 (change anytime in SMS operators).
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

list($row, $params) = epc_sms_load_operator($db_link, 'epc_unifonic');
if (!$row) {
	epc_sms_exit_json(false, 'Unifonic operator not configured');
}

$appsid = trim((string) ($params['appsid'] ?? $params['api_key'] ?? ''));
if ($appsid === '') {
	epc_sms_exit_json(false, 'Unifonic AppSid / API key is required');
}

$sender = epc_sms_sender_number($params);
// Unifonic SenderID is often alphanumeric; if numeric with +, strip +
$senderId = ltrim($sender, '+');
$body = (string) ($_POST['body'] ?? '');
$to = epc_sms_normalize_msisdn((string) ($_POST['main_field'] ?? ''), 'AE');
if ($to === '' || $body === '') {
	epc_sms_exit_json(false, 'Recipient and message body are required');
}

$apiUrl = trim((string) ($params['api_url'] ?? ''));
if ($apiUrl === '') {
	$apiUrl = 'https://el.cloud.unifonic.com/rest/SMS/messages';
}

$payload = array(
	'AppSid' => $appsid,
	'SenderID' => $senderId,
	'Recipient' => $to,
	'Body' => $body,
);

$res = epc_sms_http_json($apiUrl, $payload);
$json = $res['json'];
if ($res['ok'] && is_array($json) && !empty($json['success'])) {
	epc_sms_exit_json(true, '');
}

$msg = 'Unifonic send failed';
if (is_array($json)) {
	$msg = (string) ($json['message'] ?? $json['errorCode'] ?? $msg);
	if (!empty($json['errorCode'])) {
		$msg .= ' [' . $json['errorCode'] . ']';
	}
} elseif ($res['body'] !== '') {
	$msg .= ': ' . substr($res['body'], 0, 200);
}
epc_sms_exit_json(false, $msg);
