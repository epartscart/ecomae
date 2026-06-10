<?php
/**
 * GET demo status by prospect email.
 * https://www.ecomae.com/epc-demo-status.php?email=...&token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');

$token = (string) ($_GET['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$email = trim((string) ($_GET['email'] ?? ''));
if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
	http_response_code(400);
	exit(json_encode(array('ok' => false, 'message' => 'Valid email required')));
}

$pdo = epc_portal_demo_platform_pdo();
if (!$pdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'Platform database unavailable')));
}

$row = epc_portal_demo_get_by_email($pdo, $email);
if ($row === null) {
	echo json_encode(array('ok' => true, 'found' => false, 'message' => 'No active demo for this email'));
	exit;
}

echo json_encode(array(
	'ok' => true,
	'found' => true,
	'site_key' => $row['site_key'],
	'industry_code' => $row['industry_code'],
	'trade_name' => $row['trade_name'],
	'expires_at' => (int) ($row['demo_expires_at'] ?? 0),
	'expires_date' => date('c', (int) ($row['demo_expires_at'] ?? 0)),
	'days_left' => $row['days_left'] ?? 0,
	'urls' => $row['urls'] ?? epc_portal_demo_urls((string) $row['site_key']),
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
