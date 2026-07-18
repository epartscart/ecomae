<?php
/**
 * Public demo provision (marketing wizard) — no deploy token; rate-limited by email/IP.
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');
header('Referrer-Policy: strict-origin-when-cross-origin');
set_time_limit(300);

if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
	http_response_code(405);
	exit(json_encode(array('ok' => false, 'message' => 'POST required')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_security_kernel.php';
epc_sec_require_rate_limit('demo_provision_ip', 8, 3600);
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

	$result = epc_portal_demo_provision($pdo, $params);
	$code = !empty($result['ok']) ? 200 : 400;
	unset($result['temp_password'], $result['log']);
	epc_portal_demo_json_out($result, $code);
} catch (Throwable $e) {
	error_log('epc-demo-provision-public: ' . $e->getMessage());
	epc_portal_demo_json_out(array(
		'ok' => false,
		'message' => 'Provision error — please retry or contact hello@ecomae.com',
	), 500);
}
