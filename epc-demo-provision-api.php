<?php
/**
 * POST provision demo sandbox tenant.
 * https://www.ecomae.com/epc-demo-provision-api.php?token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
	http_response_code(405);
	exit(json_encode(array('ok' => false, 'message' => 'POST required')));
}

$token = (string) ($_POST['token'] ?? $_GET['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

try {
	$pdo = epc_portal_demo_platform_pdo();
	if (!$pdo instanceof PDO) {
		epc_portal_demo_json_out(array('ok' => false, 'message' => 'Platform database unavailable'), 500);
		exit;
	}

	$params = array(
		'contact_name' => $_POST['contact_name'] ?? $_POST['name'] ?? '',
		'contact_email' => $_POST['contact_email'] ?? $_POST['email'] ?? '',
		'contact_phone' => $_POST['contact_phone'] ?? $_POST['phone'] ?? '',
		'company' => $_POST['company'] ?? '',
		'industry_code' => $_POST['industry_code'] ?? $_POST['industry'] ?? '',
		'notes' => $_POST['notes'] ?? '',
		'terms' => !empty($_POST['terms']) || !empty($_POST['accept_terms']),
	);
	$clpPass = trim((string) ($_POST['clp_pass'] ?? $_GET['clp_pass'] ?? ''));
	if ($clpPass !== '') {
		$GLOBALS['epc_demo_clp_pass'] = $clpPass;
	}

	$result = epc_portal_demo_provision($pdo, $params);
	$code = !empty($result['ok']) ? 200 : 400;
	unset($result['temp_password']);
	if (isset($result['sync']) && is_array($result['sync'])) {
		$result['sync'] = array(
			'ok' => !empty($result['sync']['ok']),
			'message' => (string) ($result['sync']['message'] ?? ''),
		);
	}
	epc_portal_demo_json_out($result, $code);
} catch (Throwable $e) {
	error_log('epc-demo-provision-api: ' . $e->getMessage());
	epc_portal_demo_json_out(array(
		'ok' => false,
		'message' => 'Provision error: ' . $e->getMessage(),
	), 500);
}
