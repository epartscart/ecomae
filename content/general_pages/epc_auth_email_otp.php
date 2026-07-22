<?php
/**
 * CP email OTP — platform table on ecomae + per-tenant user resolution.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_auth_common.php';
require_once __DIR__ . '/epc_auth_smtp.php';

function epc_auth_otp_ensure_schema(PDO $platformPdo): void
{
	$platformPdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_auth_otp_requests` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`email` VARCHAR(120) NOT NULL,
			`code_hash` VARCHAR(64) NOT NULL,
			`tenant_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`context_json` TEXT NULL,
			`expires_at` INT NOT NULL,
			`ip_address` VARCHAR(45) NOT NULL DEFAULT \'\',
			`created_at` INT NOT NULL DEFAULT 0,
			INDEX `email_created` (`email`, `created_at`),
			INDEX `expires_at` (`expires_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

function epc_auth_otp_rate_limited(PDO $platformPdo, string $email): bool
{
	$email = strtolower(trim($email));
	if ($email === '') {
		return true;
	}
	epc_auth_otp_ensure_schema($platformPdo);
	$since = time() - 3600;
	$st = $platformPdo->prepare(
		'SELECT COUNT(*) FROM `epc_auth_otp_requests` WHERE `email` = ? AND `created_at` > ?'
	);
	$st->execute(array($email, $since));
	if ((int) $st->fetchColumn() >= 5) {
		return true;
	}
	$ip = (string) ($_SERVER['REMOTE_ADDR'] ?? '');
	if ($ip === '') {
		return false;
	}
	$stIp = $platformPdo->prepare(
		'SELECT COUNT(*) FROM `epc_auth_otp_requests` WHERE `ip_address` = ? AND `created_at` > ?'
	);
	$stIp->execute(array($ip, $since));
	return ((int) $stIp->fetchColumn()) >= 20;
}

function epc_auth_otp_purge_expired(PDO $platformPdo): void
{
	epc_auth_otp_ensure_schema($platformPdo);
	$platformPdo->prepare('DELETE FROM `epc_auth_otp_requests` WHERE `expires_at` < ?')
		->execute(array(time() - 86400));
}

function epc_auth_otp_send(PDO $platformPdo, string $email, array $context): array
{
	$email = strtolower(trim($email));
	if ($email === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'Valid email is required');
	}
	if (!epc_auth_require_https()) {
		return array('ok' => false, 'message' => 'HTTPS is required for sign-in');
	}
	if (epc_auth_otp_rate_limited($platformPdo, $email)) {
		return array('ok' => false, 'message' => 'Too many sign-in attempts — try again in an hour (max 5 codes per email, 20 per IP)');
	}

	epc_auth_otp_purge_expired($platformPdo);
	$code = (string) random_int(100000, 999999);
	$hash = hash('sha256', $code . '|' . epc_auth_signing_secret());
	$tenantKey = (string) ($context['tenant_key'] ?? '');
	$authMode = epc_auth_normalize_mode((string) ($context['auth_mode'] ?? 'cp'));
	$expires = time() + 600;
	$ip = (string) ($_SERVER['REMOTE_ADDR'] ?? '');
	$storeContext = $context;
	$storeContext['auth_mode'] = $authMode;

	epc_auth_otp_ensure_schema($platformPdo);
	$platformPdo->prepare(
		'INSERT INTO `epc_auth_otp_requests` (`email`, `code_hash`, `tenant_key`, `context_json`, `expires_at`, `ip_address`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		$email,
		$hash,
		$tenantKey,
		json_encode($storeContext),
		$expires,
		$ip,
		time(),
	));
	$otpId = (int) $platformPdo->lastInsertId();

	$send = epc_auth_send_otp_email($email, $code, $context);
	if (empty($send['ok'])) {
		if (epc_auth_otp_demo_fallback_allowed($tenantKey)) {
			epc_auth_otp_store_operator_code($platformPdo, $otpId, $code);
			return array(
				'ok' => true,
				'message' => 'Sign-in code ready — email could not be sent (SMTP). Super CP operators can view the code under Modern auth settings.',
				'expires_in' => 600,
				'demo_otp_logged' => true,
				'smtp_hint' => (string) ($send['message'] ?? ''),
			);
		}
		return array(
			'ok' => false,
			'message' => (string) ($send['message'] ?? 'Could not send email — check SMTP settings in Control Panel'),
			'smtp_detail' => (string) ($send['detail'] ?? ''),
		);
	}

	return array('ok' => true, 'message' => 'Sign-in code sent — check your inbox', 'expires_in' => 600);
}

function epc_auth_otp_verify(PDO $platformPdo, string $email, string $code, array $context): array
{
	$email = strtolower(trim($email));
	$code = preg_replace('/\D/', '', trim($code));
	if ($email === '' || strlen($code) !== 6) {
		return array('ok' => false, 'message' => 'Email and 6-digit code are required');
	}
	if (!epc_auth_require_https()) {
		return array('ok' => false, 'message' => 'HTTPS is required for sign-in');
	}

	epc_auth_otp_ensure_schema($platformPdo);
	$hash = hash('sha256', $code . '|' . epc_auth_signing_secret());
	$tenantKey = (string) ($context['tenant_key'] ?? '');
	$st = $platformPdo->prepare(
		'SELECT * FROM `epc_auth_otp_requests`
		 WHERE `email` = ? AND `code_hash` = ? AND `tenant_key` = ? AND `expires_at` >= ?
		 ORDER BY `id` DESC LIMIT 1'
	);
	$st->execute(array($email, $hash, $tenantKey, time()));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'message' => 'Invalid or expired code');
	}

	$platformPdo->prepare('DELETE FROM `epc_auth_otp_requests` WHERE `id` = ?')->execute(array((int) $row['id']));

	$authMode = epc_auth_normalize_mode((string) ($context['auth_mode'] ?? 'cp'));
	$hints = array('tenant_key' => (string) ($context['tenant_key'] ?? $tenantKey));
	$resolved = epc_auth_resolve_for_mode($authMode, $hints);
	if (empty($resolved['ok'])) {
		return array('ok' => false, 'message' => (string) ($resolved['message'] ?? 'Unknown tenant context'));
	}
	$resolved['auth_mode'] = $authMode;

	if ($authMode === 'storefront') {
		$userId = epc_auth_find_or_provision_storefront_customer($resolved, $email, '');
		if ($userId <= 0) {
			return array('ok' => false, 'message' => 'Could not sign in — account may be locked');
		}
	} else {
		$userId = epc_auth_find_or_provision_cp_user($resolved, $email, '');
		if ($userId <= 0) {
			return array('ok' => false, 'message' => 'No CP access for this email on this workspace');
		}
	}

	$returnUrl = (string) ($context['return_url'] ?? '');
	$finish = epc_auth_finish_login($resolved, $userId, 'email', $returnUrl);
	if (empty($finish['ok'])) {
		return array('ok' => false, 'message' => (string) ($finish['message'] ?? 'Could not create session'));
	}

	return array(
		'ok' => true,
		'message' => 'Signed in',
		'redirect' => (string) ($finish['redirect'] ?? epc_auth_post_login_redirect($resolved)),
	);
}

/**
 * Verify OTP for registration only — no login session / user provisioning.
 *
 * @param array<string,mixed> $context
 * @return array{ok:bool,message:string,verified_email?:string}
 */
function epc_auth_otp_verify_email_only(PDO $platformPdo, string $email, string $code, array $context = array()): array
{
	$email = strtolower(trim($email));
	$code = preg_replace('/\D/', '', trim($code));
	if ($email === '' || strlen($code) !== 6) {
		return array('ok' => false, 'message' => 'Email and 6-digit code are required');
	}
	if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
		return array('ok' => false, 'message' => 'Invalid email address');
	}
	if (!epc_auth_require_https()) {
		return array('ok' => false, 'message' => 'HTTPS is required');
	}

	epc_auth_otp_ensure_schema($platformPdo);
	$hash = hash('sha256', $code . '|' . epc_auth_signing_secret());
	$tenantKey = preg_replace(
		'/[^a-z0-9_]/',
		'',
		strtolower((string) ($context['tenant_key'] ?? $context['site_key'] ?? ''))
	);

	$st = $platformPdo->prepare(
		'SELECT `id` FROM `epc_auth_otp_requests`
		 WHERE `email` = ? AND `code_hash` = ? AND `tenant_key` = ? AND `expires_at` >= ?
		 ORDER BY `id` DESC LIMIT 1'
	);
	$st->execute(array($email, $hash, $tenantKey, time()));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'message' => 'Invalid or expired code — please try again');
	}

	$platformPdo->prepare('DELETE FROM `epc_auth_otp_requests` WHERE `id` = ?')->execute(array((int) $row['id']));

	if (session_status() === PHP_SESSION_NONE) {
		session_start();
	}
	$_SESSION['epc_otp_verified_email'] = $email;
	$_SESSION['epc_otp_verified_at'] = time();

	return array(
		'ok' => true,
		'message' => 'Email verified',
		'verified_email' => $email,
	);
}

/**
 * @return array{ok:bool, message:string, detail:string, transport:string}
 */
function epc_auth_send_otp_email(string $email, string $code, array $context): array
{
	$label = (string) ($context['login_label'] ?? 'Sign in');
	if (epc_auth_normalize_mode((string) ($context['auth_mode'] ?? 'cp')) === 'storefront') {
		$label = (string) ($context['login_label'] ?? 'Shop');
	}
	$subject = $label . ' — sign-in code ' . $code;
	$body = "Your sign-in code is: {$code}\n\nIt expires in 10 minutes.\n\nIf you did not request this, ignore this email.\n";
	$html = '<p>Your sign-in code for <strong>' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8')
		. '</strong> is:</p><p style="font-size:28px;letter-spacing:6px;font-weight:700;">'
		. htmlspecialchars($code, ENT_QUOTES, 'UTF-8') . '</p><p>Expires in 10 minutes.</p>';

	return epc_auth_smtp_send_html($email, $subject, $html, $body);
}
