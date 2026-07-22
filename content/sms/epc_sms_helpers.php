<?php
/**
 * Shared SMS helpers for MENA / GCC / Pakistan operators.
 * Sender number is always taken from operator parameters (changeable in CP).
 * Loaded by standalone send_sms.php handlers (no _ASTEXE_ bootstrap).
 */

/** Changeable default From number for UAE / GCC communication SMS. */
const EPC_SMS_DEFAULT_SENDER = '+971567607011';

function epc_sms_default_sender_number(): string
{
	return EPC_SMS_DEFAULT_SENDER;
}

/**
 * @param array<string,mixed>|null $parametersValues
 */
function epc_sms_sender_number(?array $parametersValues): string
{
	$candidates = array(
		$parametersValues['sender_number'] ?? null,
		$parametersValues['from'] ?? null,
		$parametersValues['sender'] ?? null,
		$parametersValues['sender_id'] ?? null,
	);
	foreach ($candidates as $v) {
		$v = trim((string) $v);
		if ($v !== '') {
			return $v;
		}
	}
	return epc_sms_default_sender_number();
}

/**
 * Normalize destination MSISDN to digits (international, no + / 00).
 * UAE 05x → 9715x; Pakistan 03x → 923x; keep other intl numbers.
 */
function epc_sms_normalize_msisdn(string $phone, string $defaultCountry = 'AE'): string
{
	$phone = trim($phone);
	$phone = preg_replace('/[^\d+]/', '', $phone) ?? '';
	$phone = preg_replace('/^\+/', '', $phone) ?? '';
	$phone = preg_replace('/^00/', '', $phone) ?? '';

	if ($phone === '') {
		return '';
	}

	// Local UAE mobile 05xxxxxxxx
	if (preg_match('/^05\d{8}$/', $phone)) {
		return '971' . substr($phone, 1);
	}
	// Local UAE without leading 0: 5xxxxxxxx
	if (preg_match('/^5\d{8}$/', $phone) && $defaultCountry === 'AE') {
		return '971' . $phone;
	}
	// Pakistan local 03xxxxxxxxx
	if (preg_match('/^03\d{9}$/', $phone)) {
		return '92' . substr($phone, 1);
	}
	// Pakistan without 0: 3xxxxxxxxx
	if (preg_match('/^3\d{9}$/', $phone) && $defaultCountry === 'PK') {
		return '92' . $phone;
	}
	// Already intl UAE / PK / GCC
	if (preg_match('/^(971|92|966|968|973|974|965)\d+$/', $phone)) {
		return $phone;
	}

	return $phone;
}

/**
 * @return array{status:bool,message:string}
 */
function epc_sms_json_answer(bool $ok, string $message = ''): array
{
	return array('status' => $ok, 'message' => $message);
}

function epc_sms_exit_json(bool $ok, string $message = ''): void
{
	header('Content-Type: application/json; charset=utf-8');
	echo json_encode(epc_sms_json_answer($ok, $message), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

/**
 * Load operator row + decoded parameters_values for a handler.
 *
 * @return array{0:?array,1:array}
 */
function epc_sms_load_operator(PDO $db, string $handler): array
{
	$st = $db->prepare('SELECT * FROM `sms_api` WHERE `handler` = ? LIMIT 1');
	$st->execute(array($handler));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array(null, array());
	}
	$vals = json_decode((string) ($row['parameters_values'] ?? ''), true);
	if (!is_array($vals)) {
		$vals = array();
	}
	// Prefer POST override from send_notify when provided
	if (!empty($_POST['parameters_values'])) {
		$posted = json_decode((string) $_POST['parameters_values'], true);
		if (is_array($posted)) {
			$vals = array_merge($vals, $posted);
		}
	}
	return array($row, $vals);
}

function epc_sms_require_post_auth($DP_Config): void
{
	// Same gate as legacy handlers / send_notify.php
	$check = (string) ($_POST['check'] ?? '');
	$secret = (string) ($DP_Config->secret_succession ?? '');
	if ($secret === '' || $check === '' || $check !== $secret) {
		epc_sms_exit_json(false, 'Forbidden');
	}
}

/**
 * Generic HTTP JSON POST used by Etisalat / Du / Pakistan partner gateways.
 *
 * @param array<string,mixed> $payload
 * @param array<string,string> $headers
 * @return array{ok:bool,http:int,body:string,json:?array}
 */
function epc_sms_http_json(string $url, array $payload, array $headers = array(), int $timeout = 25): array
{
	$ch = curl_init($url);
	$hdrs = array('Content-Type: application/json', 'Accept: application/json');
	foreach ($headers as $k => $v) {
		$hdrs[] = $k . ': ' . $v;
	}
	curl_setopt_array($ch, array(
		CURLOPT_POST => true,
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_HTTPHEADER => $hdrs,
		CURLOPT_POSTFIELDS => json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		CURLOPT_CONNECTTIMEOUT => 10,
		CURLOPT_TIMEOUT => $timeout,
		CURLOPT_SSL_VERIFYPEER => true,
		CURLOPT_SSL_VERIFYHOST => 2,
	));
	$body = (string) curl_exec($ch);
	$http = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$err = curl_error($ch);
	curl_close($ch);
	if ($body === '' && $err !== '') {
		return array('ok' => false, 'http' => $http, 'body' => $err, 'json' => null);
	}
	$json = json_decode($body, true);
	$ok = ($http >= 200 && $http < 300);
	return array('ok' => $ok, 'http' => $http, 'body' => $body, 'json' => is_array($json) ? $json : null);
}
