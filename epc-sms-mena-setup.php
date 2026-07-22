<?php
/**
 * Idempotent setup: Etisalat / du / Unifonic (GCC·MENA) / Pakistan SMS operators.
 * Default sender number: +971567607011 (editable later in CP → SMS Operators).
 *
 * Web: https://www.epartscart.com/epc-sms-mena-setup.php?token=epartscart-deploy-2026
 * CLI: php epc-sms-mena-setup.php
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '1');

$isCli = (PHP_SAPI === 'cli');
if (!$isCli) {
	if ((string) ($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
		http_response_code(403);
		header('Content-Type: application/json; charset=utf-8');
		exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
	}
	header('Content-Type: application/json; charset=utf-8');
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/sms/epc_sms_helpers.php';

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$defaultSender = epc_sms_default_sender_number();

/**
 * CP expects parameters as a list of {name, type, caption}.
 * parameters_values is a map of name => value.
 *
 * @return list<array{handler:string,name:string,description:string,parameters:list<array{name:string,type:string,caption:string}>,values:array<string,string>}>
 */
function epc_sms_mena_operators_def(string $defaultSender): array
{
	return array(
		array(
			'handler' => 'epc_unifonic',
			'name' => 'Unifonic (GCC / MENA)',
			'description' => 'GCC and Middle East SMS via Unifonic REST. Set AppSid from the Unifonic console. Sender number defaults to +971567607011 — change anytime under parameters.',
			'parameters' => array(
				array('name' => 'appsid', 'type' => 'text', 'caption' => 'Unifonic AppSid (API key)'),
				array('name' => 'sender_number', 'type' => 'text', 'caption' => 'Sender number / Sender ID (changeable)'),
				array('name' => 'api_url', 'type' => 'text', 'caption' => 'API URL (optional override)'),
			),
			'values' => array(
				'appsid' => '',
				'sender_number' => $defaultSender,
				'api_url' => 'https://el.cloud.unifonic.com/rest/SMS/messages',
			),
		),
		array(
			'handler' => 'epc_etisalat',
			'name' => 'Etisalat (e&) UAE',
			'description' => 'UAE Etisalat / e& partner SMS gateway. Enter API URL and credentials from your Etisalat Business Messaging contract. From-number defaults to +971567607011. Register alphanumeric Sender IDs with Etisalat/TDRA when required.',
			'parameters' => array(
				array('name' => 'api_url', 'type' => 'text', 'caption' => 'Etisalat partner API URL'),
				array('name' => 'api_key', 'type' => 'text', 'caption' => 'API key / username'),
				array('name' => 'api_secret', 'type' => 'password', 'caption' => 'API secret / password (optional)'),
				array('name' => 'auth_header', 'type' => 'text', 'caption' => 'Auth header (optional Bearer/Basic)'),
				array('name' => 'sender_number', 'type' => 'text', 'caption' => 'Sender number / Sender ID (changeable)'),
			),
			'values' => array(
				'api_url' => '',
				'api_key' => '',
				'api_secret' => '',
				'auth_header' => '',
				'sender_number' => $defaultSender,
			),
		),
		array(
			'handler' => 'epc_du',
			'name' => 'du UAE',
			'description' => 'UAE du Messaging / Business SMS gateway. Enter API URL and credentials from your du contract. Sender number defaults to +971567607011 and can be changed later without code changes.',
			'parameters' => array(
				array('name' => 'api_url', 'type' => 'text', 'caption' => 'du partner API URL'),
				array('name' => 'api_key', 'type' => 'text', 'caption' => 'API key / username'),
				array('name' => 'api_secret', 'type' => 'password', 'caption' => 'API secret / password (optional)'),
				array('name' => 'auth_header', 'type' => 'text', 'caption' => 'Auth header (optional Bearer/Basic)'),
				array('name' => 'sender_number', 'type' => 'text', 'caption' => 'Sender number / Sender ID (changeable)'),
			),
			'values' => array(
				'api_url' => '',
				'api_key' => '',
				'api_secret' => '',
				'auth_header' => '',
				'sender_number' => $defaultSender,
			),
		),
		array(
			'handler' => 'epc_pakistan',
			'name' => 'Pakistan / Jazz SMS',
			'description' => 'Pakistan SMS via Jazz or a compatible HTTP gateway (+92). Keep UAE +971567607011 as sender when the provider allows international origination, or switch to a local PK mask later in the same field.',
			'parameters' => array(
				array('name' => 'api_url', 'type' => 'text', 'caption' => 'Provider API URL'),
				array('name' => 'api_key', 'type' => 'text', 'caption' => 'API key (optional if username/password)'),
				array('name' => 'username', 'type' => 'text', 'caption' => 'Username (optional)'),
				array('name' => 'password', 'type' => 'password', 'caption' => 'Password (optional)'),
				array('name' => 'sender_number', 'type' => 'text', 'caption' => 'Sender number / mask (changeable)'),
			),
			'values' => array(
				'api_url' => '',
				'api_key' => '',
				'username' => '',
				'password' => '',
				'sender_number' => $defaultSender,
			),
		),
	);
}

$ops = epc_sms_mena_operators_def($defaultSender);
$out = array();

foreach ($ops as $op) {
	$handler = $op['handler'];
	$paramsJson = json_encode($op['parameters'], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$valuesJson = json_encode($op['values'], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$name = $op['name'];
	$desc = $op['description'];

	$st = $pdo->prepare('SELECT `id`, `parameters_values` FROM `sms_api` WHERE `handler` = ? LIMIT 1');
	$st->execute(array($handler));
	$row = $st->fetch(PDO::FETCH_ASSOC);

	if ($row) {
		$id = (int) $row['id'];
		$existing = json_decode((string) $row['parameters_values'], true);
		if (!is_array($existing)) {
			$existing = array();
		}
		foreach ($op['values'] as $k => $v) {
			if (!array_key_exists($k, $existing) || $existing[$k] === null || $existing[$k] === '') {
				if ($v !== '' || !array_key_exists($k, $existing)) {
					$existing[$k] = $v;
				}
			}
		}
		if (empty($existing['sender_number'])) {
			$existing['sender_number'] = $defaultSender;
		}
		$mergedValues = json_encode($existing, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		$pdo->prepare(
			'UPDATE `sms_api` SET `name` = ?, `parameters` = ?, `parameters_values` = ?, `description` = ?, `control_available` = 1 WHERE `id` = ?'
		)->execute(array($name, $paramsJson, $mergedValues, $desc, $id));
		$out[] = array('action' => 'updated', 'handler' => $handler, 'id' => $id);
	} else {
		$pdo->prepare(
			'INSERT INTO `sms_api` (`name`, `parameters`, `parameters_values`, `description`, `active`, `handler`, `control_available`)
			 VALUES (?, ?, ?, ?, 0, ?, 1)'
		)->execute(array($name, $paramsJson, $valuesJson, $desc, $handler));
		$out[] = array('action' => 'inserted', 'handler' => $handler, 'id' => (int) $pdo->lastInsertId());
	}
}

$result = array(
	'status' => true,
	'default_sender' => $defaultSender,
	'operators' => $out,
	'note' => 'Activate one operator in CP → SMS Operators, fill API credentials, keep or change sender_number.',
);

if ($isCli) {
	echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . "\n";
	exit(0);
}

echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
