<?php
/**
 * On-Premises Health Reporting Intake API
 * POST /api/v1/on-premises/health.php
 * Body (JSON): { license_key, status, uptime, disk_free_gb, memory_usage_mb,
 *                php_version, db_size_mb, last_backup }
 *
 * Authenticated by the license_key itself (must already be an active,
 * non-revoked license) — no separate BOS token is required.
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_api_clients.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_onprem_licenses.php';

if (($_SERVER['REQUEST_METHOD'] ?? 'GET') !== 'POST') {
	http_response_code(405);
	echo json_encode(array('ok' => false, 'error' => 'method_not_allowed'));
	exit;
}

$raw = file_get_contents('php://input');
$input = json_decode((string) $raw, true);
if (!is_array($input) || empty($input['license_key'])) {
	http_response_code(400);
	echo json_encode(array('ok' => false, 'error' => 'invalid_payload'));
	exit;
}

$pdo = epc_api_clients_platform_pdo();
if (!$pdo instanceof PDO) {
	http_response_code(503);
	echo json_encode(array('ok' => false, 'error' => 'platform_db_unavailable'));
	exit;
}

$licRow = epc_onprem_license_fetch($pdo, (string) $input['license_key']);
if (!$licRow || $licRow['status'] === 'revoked') {
	http_response_code(403);
	echo json_encode(array('ok' => false, 'error' => 'unknown_or_revoked_license'));
	exit;
}

epc_onprem_health_log($pdo, $input);
echo json_encode(array('ok' => true));
