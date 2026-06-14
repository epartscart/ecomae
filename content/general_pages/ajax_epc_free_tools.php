<?php
/**
 * Free Tools AJAX — register, whoami, compute, save, list.
 * Public endpoint (no tenant auth): a free account is a lightweight email lead.
 * All compute is country-driven via epc_free_tools_compute().
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
if ($docRoot === '') {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'message' => 'DOCUMENT_ROOT missing'));
	exit;
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once $docRoot . '/content/general_pages/epc_ecomae_free_tools.php';

$raw = file_get_contents('php://input');
$body = json_decode((string) $raw, true);
if (!is_array($body)) {
	$body = $_POST;
}
$action = isset($body['action']) ? preg_replace('/[^a-z_]/', '', (string) $body['action']) : '';

function epc_free_tools_out($a)
{
	echo json_encode($a, JSON_UNESCAPED_SLASHES);
	exit;
}

switch ($action) {
	case 'register':
		epc_free_tools_out(epc_free_tools_register(
			(string) ($body['email'] ?? ''),
			(string) ($body['company'] ?? ''),
			(string) ($body['country'] ?? '')
		));
		break;

	case 'whoami':
		$acc = epc_free_tools_account_by_token((string) ($body['token'] ?? ''));
		if (!$acc) {
			epc_free_tools_out(array('ok' => false));
		}
		epc_free_tools_out(array('ok' => true, 'account' => array(
			'email' => $acc['email'], 'company' => $acc['company'], 'country' => $acc['country'],
		)));
		break;

	case 'compute':
		$acc = epc_free_tools_account_by_token((string) ($body['token'] ?? ''));
		if (!$acc) {
			epc_free_tools_out(array('ok' => false, 'message' => 'Please register first.'));
		}
		$tool = preg_replace('/[^a-z]/', '', (string) ($body['tool'] ?? ''));
		$country = (string) ($body['country'] ?? $acc['country'] ?? 'XX');
		$inputs = isset($body['inputs']) && is_array($body['inputs']) ? $body['inputs'] : array();
		epc_free_tools_out(epc_free_tools_compute($tool, $country, $inputs));
		break;

	case 'save':
		$acc = epc_free_tools_account_by_token((string) ($body['token'] ?? ''));
		if (!$acc) {
			epc_free_tools_out(array('ok' => false, 'message' => 'Please register first.'));
		}
		$payload = isset($body['payload']) && is_array($body['payload']) ? $body['payload'] : array();
		epc_free_tools_out(epc_free_tools_save(
			(int) $acc['id'],
			preg_replace('/[^a-z]/', '', (string) ($body['tool'] ?? '')),
			(string) ($body['country'] ?? ''),
			substr((string) ($body['title'] ?? 'Saved result'), 0, 180),
			$payload
		));
		break;

	case 'list':
		$acc = epc_free_tools_account_by_token((string) ($body['token'] ?? ''));
		if (!$acc) {
			epc_free_tools_out(array('ok' => false, 'message' => 'Please register first.'));
		}
		epc_free_tools_out(array('ok' => true, 'saves' => epc_free_tools_list_saves((int) $acc['id'])));
		break;

	default:
		http_response_code(400);
		epc_free_tools_out(array('ok' => false, 'message' => 'Unknown action'));
}
