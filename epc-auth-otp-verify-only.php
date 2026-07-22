<?php
/**
 * Verify OTP without creating a login session — used for email verification during registration.
 * POST JSON: { "email", "code", "tenant_key", "context" }
 * Returns: { "ok": true|false, "message": "..." }
 * On success also writes $_SESSION['epc_otp_verified_email'] = $email.
 */
declare(strict_types=1);

define('_ASTEXE_', 1);

// Prevent stray PHP notices / mailer debug from breaking JSON clients.
ob_start();

try {
	require_once __DIR__ . '/content/general_pages/epc_auth_email_otp.php';

	$input = epc_auth_read_json_body();
	$email = strtolower(trim((string) ($input['email'] ?? '')));
	$code = preg_replace('/\D/', '', trim((string) ($input['code'] ?? '')));
	$tenantKey = preg_replace(
		'/[^a-z0-9_]/',
		'',
		strtolower((string) ($input['tenant_key'] ?? $input['site_key'] ?? ''))
	);

	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
	$platformPdo = epc_portal_platform_pdo();
	if (!$platformPdo instanceof PDO) {
		$cfg = epc_auth_bootstrap_config();
		$platformPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	}

	$result = epc_auth_otp_verify_email_only(
		$platformPdo,
		$email,
		$code,
		array('tenant_key' => $tenantKey, 'site_key' => $tenantKey)
	);

	while (ob_get_level() > 0) {
		ob_end_clean();
	}
	epc_auth_json_response($result, !empty($result['ok']) ? 200 : 400);
} catch (Throwable $e) {
	while (ob_get_level() > 0) {
		ob_end_clean();
	}
	error_log('epc-auth-otp-verify-only: ' . $e->getMessage());
	if (!headers_sent()) {
		http_response_code(500);
		header('Content-Type: application/json; charset=utf-8');
		header('Cache-Control: no-store');
	}
	echo json_encode(array(
		'ok' => false,
		'message' => 'Verification temporarily unavailable — please retry',
	));
	exit;
}
