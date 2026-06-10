<?php
/**
 * Verify OTP without creating a session — used for email verification during registration.
 * POST JSON: { "email", "code", "tenant_key", "context" }
 * Returns: { "ok": true|false, "message": "..." }
 * On success also writes $_SESSION['epc_otp_verified_email'] = $email.
 */
define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_auth_email_otp.php';

if (session_status() === PHP_SESSION_NONE) {
	session_start();
}

$input = epc_auth_read_json_body();
$email = strtolower(trim((string) ($input['email'] ?? '')));
$code  = preg_replace('/\D/', '', trim((string) ($input['code'] ?? '')));

if ($email === '' || strlen($code) !== 6) {
	epc_auth_json_response(array('ok' => false, 'message' => 'Email and 6-digit code are required'), 400);
}
if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
	epc_auth_json_response(array('ok' => false, 'message' => 'Invalid email address'), 400);
}
if (!epc_auth_require_https()) {
	epc_auth_json_response(array('ok' => false, 'message' => 'HTTPS is required'), 400);
}

require_once __DIR__ . '/content/general_pages/epc_portal.php';
$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	$cfg = epc_auth_bootstrap_config();
	try {
		$platformPdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		epc_auth_json_response(array('ok' => false, 'message' => 'Platform database unavailable'), 503);
	}
}

epc_auth_otp_ensure_schema($platformPdo);
$hash = hash('sha256', $code . '|' . epc_auth_signing_secret());
$tenantKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($input['tenant_key'] ?? $input['site_key'] ?? '')));

$st = $platformPdo->prepare(
	'SELECT `id` FROM `epc_auth_otp_requests`
	 WHERE `email` = ? AND `code_hash` = ? AND `tenant_key` = ? AND `expires_at` >= ?
	 ORDER BY `id` DESC LIMIT 1'
);
$st->execute(array($email, $hash, $tenantKey, time()));
$row = $st->fetch(PDO::FETCH_ASSOC);

if (!$row) {
	epc_auth_json_response(array('ok' => false, 'message' => 'Invalid or expired code — please try again'), 400);
}

// Consume the OTP (single-use)
$platformPdo->prepare('DELETE FROM `epc_auth_otp_requests` WHERE `id` = ?')->execute(array((int) $row['id']));

// Mark in session so the registration handler can set email_confirmed = 1
$_SESSION['epc_otp_verified_email'] = $email;
$_SESSION['epc_otp_verified_at']    = time();

epc_auth_json_response(array('ok' => true, 'message' => 'Email verified', 'verified_email' => $email));
