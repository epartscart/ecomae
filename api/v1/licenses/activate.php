<?php
/**
 * On-Premises License Activation API
 * POST /api/v1/licenses/activate.php
 * Body (JSON): { license_key, fingerprint, hostname, ip, php_version, os }
 *
 * Validates the license against the platform registry, binds it to the
 * requesting server's fingerprint, and returns a signed activation
 * certificate plus the core-engine bundle needed to run the app locally.
 */
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_api_clients.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_onprem_licenses.php';

if (($_SERVER['REQUEST_METHOD'] ?? 'GET') !== 'POST') {
	http_response_code(405);
	echo json_encode(array('success' => false, 'error' => 'method_not_allowed', 'message' => 'Use POST.'));
	exit;
}

$raw = file_get_contents('php://input');
$input = json_decode((string) $raw, true);
if (!is_array($input)) {
	http_response_code(400);
	echo json_encode(array('success' => false, 'error' => 'invalid_json', 'message' => 'Body must be JSON.'));
	exit;
}

$pdo = epc_api_clients_platform_pdo();
if (!$pdo instanceof PDO) {
	http_response_code(503);
	echo json_encode(array('success' => false, 'error' => 'platform_db_unavailable', 'message' => 'License registry unavailable.'));
	exit;
}

$result = epc_onprem_license_activate($pdo, $input);
http_response_code($result['success'] ? 200 : 400);
echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
