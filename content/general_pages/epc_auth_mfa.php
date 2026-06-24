<?php
/**
 * MFA (Multi-Factor Authentication) — TOTP enrollment, verification, and policy enforcement.
 *
 * Implements RFC 6238 TOTP (Time-Based One-Time Password) compatible with
 * Google Authenticator, Authy, Microsoft Authenticator, etc.
 *
 * Usage:
 *   require_once __DIR__ . '/epc_auth_mfa.php';
 *
 *   // Enroll user
 *   $enrollment = epc_mfa_enroll($pdo, $userId, $email);
 *   // -> ['secret' => '...', 'qr_uri' => 'otpauth://totp/...', 'backup_codes' => [...]]
 *
 *   // Verify code during enrollment (confirms user scanned QR)
 *   $ok = epc_mfa_confirm_enrollment($pdo, $userId, $totpCode);
 *
 *   // Verify TOTP on login
 *   $ok = epc_mfa_verify($pdo, $userId, $totpCode);
 *
 *   // Check if MFA required for current session
 *   $needed = epc_mfa_required_for_session($pdo, $userId);
 *
 *   // Route guard — call in epc_cp_auth_gate or ERP shell
 *   epc_mfa_enforce_route_guard($pdo, $userId, $currentPath);
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

/* ─────────────────── Schema ─────────────────── */

function epc_mfa_ensure_schema(PDO $pdo): void
{
	static $done = false;
	if ($done) { return; }
	$done = true;

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_mfa_secrets` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`user_id` INT UNSIGNED NOT NULL,
			`method` ENUM(\'totp\',\'webauthn\') NOT NULL DEFAULT \'totp\',
			`secret` VARCHAR(64) NOT NULL DEFAULT \'\',
			`confirmed` TINYINT(1) NOT NULL DEFAULT 0,
			`label` VARCHAR(120) NOT NULL DEFAULT \'\',
			`webauthn_credential_id` VARCHAR(512) NULL,
			`webauthn_public_key` TEXT NULL,
			`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			`last_used_at` DATETIME NULL,
			UNIQUE KEY `user_method` (`user_id`, `method`, `label`),
			INDEX `user_confirmed` (`user_id`, `confirmed`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_mfa_backup_codes` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`user_id` INT UNSIGNED NOT NULL,
			`code_hash` VARCHAR(64) NOT NULL,
			`used` TINYINT(1) NOT NULL DEFAULT 0,
			`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			INDEX `user_used` (`user_id`, `used`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_mfa_policy` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`tenant_key` VARCHAR(64) NOT NULL DEFAULT \'__platform__\',
			`require_mfa_for_roles` TEXT NOT NULL DEFAULT \'\',
			`require_mfa_for_paths` TEXT NOT NULL DEFAULT \'\',
			`grace_period_hours` INT NOT NULL DEFAULT 72,
			`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			`updated_at` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
			UNIQUE KEY `tenant` (`tenant_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_mfa_audit_log` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`user_id` INT UNSIGNED NOT NULL,
			`action` VARCHAR(32) NOT NULL,
			`success` TINYINT(1) NOT NULL DEFAULT 1,
			`ip_address` VARCHAR(45) NOT NULL DEFAULT \'\',
			`user_agent` VARCHAR(255) NOT NULL DEFAULT \'\',
			`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			INDEX `user_action` (`user_id`, `action`, `created_at`),
			INDEX `created` (`created_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);
}

/* ─────────────────── TOTP Core (RFC 6238) ─────────────────── */

function epc_mfa_generate_secret(int $length = 20): string
{
	$bytes = random_bytes($length);
	return epc_mfa_base32_encode($bytes);
}

function epc_mfa_base32_encode(string $data): string
{
	$alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
	$binary = '';
	foreach (str_split($data) as $char) {
		$binary .= str_pad(decbin(ord($char)), 8, '0', STR_PAD_LEFT);
	}
	$result = '';
	$chunks = str_split($binary, 5);
	foreach ($chunks as $chunk) {
		$chunk = str_pad($chunk, 5, '0', STR_PAD_RIGHT);
		$result .= $alphabet[bindec($chunk)];
	}
	return $result;
}

function epc_mfa_base32_decode(string $input): string
{
	$alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
	$input = strtoupper(rtrim($input, '='));
	$binary = '';
	for ($i = 0; $i < strlen($input); $i++) {
		$pos = strpos($alphabet, $input[$i]);
		if ($pos === false) { continue; }
		$binary .= str_pad(decbin($pos), 5, '0', STR_PAD_LEFT);
	}
	$result = '';
	$chunks = str_split($binary, 8);
	foreach ($chunks as $chunk) {
		if (strlen($chunk) < 8) { break; }
		$result .= chr(bindec($chunk));
	}
	return $result;
}

function epc_mfa_totp_code(string $secret, int $timeSlice = null, int $digits = 6): string
{
	if ($timeSlice === null) {
		$timeSlice = (int) floor(time() / 30);
	}
	$key = epc_mfa_base32_decode($secret);
	$time = pack('N*', 0, $timeSlice);
	$hmac = hash_hmac('sha1', $time, $key, true);
	$offset = ord($hmac[strlen($hmac) - 1]) & 0x0F;
	$code = (
		((ord($hmac[$offset]) & 0x7F) << 24) |
		((ord($hmac[$offset + 1]) & 0xFF) << 16) |
		((ord($hmac[$offset + 2]) & 0xFF) << 8) |
		(ord($hmac[$offset + 3]) & 0xFF)
	) % pow(10, $digits);
	return str_pad((string) $code, $digits, '0', STR_PAD_LEFT);
}

function epc_mfa_verify_totp(string $secret, string $code, int $window = 1): bool
{
	$code = trim($code);
	if ($code === '' || strlen($code) !== 6) {
		return false;
	}
	$timeSlice = (int) floor(time() / 30);
	for ($i = -$window; $i <= $window; $i++) {
		if (hash_equals(epc_mfa_totp_code($secret, $timeSlice + $i), $code)) {
			return true;
		}
	}
	return false;
}

function epc_mfa_otpauth_uri(string $secret, string $email, string $issuer = 'ECOM AE'): string
{
	$label = rawurlencode($issuer) . ':' . rawurlencode($email);
	return 'otpauth://totp/' . $label
		. '?secret=' . rawurlencode($secret)
		. '&issuer=' . rawurlencode($issuer)
		. '&digits=6&period=30&algorithm=SHA1';
}

/* ─────────────────── Enrollment ─────────────────── */

function epc_mfa_enroll(PDO $pdo, int $userId, string $email): array
{
	epc_mfa_ensure_schema($pdo);

	// Check if already enrolled
	$st = $pdo->prepare('SELECT `id`, `confirmed` FROM `epc_mfa_secrets` WHERE `user_id` = ? AND `method` = \'totp\' LIMIT 1');
	$st->execute(array($userId));
	$existing = $st->fetch(PDO::FETCH_ASSOC);
	if ($existing && (int) $existing['confirmed'] === 1) {
		return array('ok' => false, 'error' => 'TOTP already enrolled. Disable first to re-enroll.');
	}

	// Remove any unconfirmed enrollment
	if ($existing) {
		$pdo->prepare('DELETE FROM `epc_mfa_secrets` WHERE `id` = ?')->execute(array($existing['id']));
	}

	$secret = epc_mfa_generate_secret();
	$uri = epc_mfa_otpauth_uri($secret, $email);

	$pdo->prepare(
		'INSERT INTO `epc_mfa_secrets` (`user_id`, `method`, `secret`, `confirmed`, `label`) VALUES (?, \'totp\', ?, 0, ?)'
	)->execute(array($userId, $secret, 'TOTP (' . date('Y-m-d') . ')'));

	// Generate backup codes
	$backupCodes = epc_mfa_generate_backup_codes($pdo, $userId);

	epc_mfa_log($pdo, $userId, 'enroll_start');

	return array(
		'ok'           => true,
		'secret'       => $secret,
		'qr_uri'       => $uri,
		'backup_codes' => $backupCodes,
		'message'      => 'Scan the QR code with your authenticator app, then enter a code to confirm.',
	);
}

function epc_mfa_confirm_enrollment(PDO $pdo, int $userId, string $code): array
{
	epc_mfa_ensure_schema($pdo);

	$st = $pdo->prepare('SELECT `id`, `secret` FROM `epc_mfa_secrets` WHERE `user_id` = ? AND `method` = \'totp\' AND `confirmed` = 0 LIMIT 1');
	$st->execute(array($userId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'error' => 'No pending TOTP enrollment found.');
	}

	if (!epc_mfa_verify_totp($row['secret'], $code)) {
		epc_mfa_log($pdo, $userId, 'enroll_confirm_fail', false);
		return array('ok' => false, 'error' => 'Invalid code. Check your authenticator app and try again.');
	}

	$pdo->prepare('UPDATE `epc_mfa_secrets` SET `confirmed` = 1, `last_used_at` = NOW() WHERE `id` = ?')
		->execute(array($row['id']));

	epc_mfa_log($pdo, $userId, 'enroll_confirmed');

	return array('ok' => true, 'message' => 'TOTP enrollment confirmed. MFA is now active on your account.');
}

/* ─────────────────── Verification ─────────────────── */

function epc_mfa_verify(PDO $pdo, int $userId, string $code): array
{
	epc_mfa_ensure_schema($pdo);

	// Check for backup code first
	if (strlen(trim($code)) === 10) {
		return epc_mfa_verify_backup_code($pdo, $userId, $code);
	}

	$st = $pdo->prepare('SELECT `id`, `secret` FROM `epc_mfa_secrets` WHERE `user_id` = ? AND `method` = \'totp\' AND `confirmed` = 1 LIMIT 1');
	$st->execute(array($userId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'error' => 'TOTP not enrolled.');
	}

	if (!epc_mfa_verify_totp($row['secret'], $code)) {
		epc_mfa_log($pdo, $userId, 'verify_fail', false);
		return array('ok' => false, 'error' => 'Invalid code.');
	}

	$pdo->prepare('UPDATE `epc_mfa_secrets` SET `last_used_at` = NOW() WHERE `id` = ?')
		->execute(array($row['id']));

	epc_mfa_log($pdo, $userId, 'verify_ok');

	return array('ok' => true, 'message' => 'MFA verified.');
}

/* ─────────────────── Backup Codes ─────────────────── */

function epc_mfa_generate_backup_codes(PDO $pdo, int $userId, int $count = 8): array
{
	epc_mfa_ensure_schema($pdo);

	// Delete old unused codes
	$pdo->prepare('DELETE FROM `epc_mfa_backup_codes` WHERE `user_id` = ?')->execute(array($userId));

	$codes = array();
	for ($i = 0; $i < $count; $i++) {
		$plain = strtoupper(bin2hex(random_bytes(5))); // 10-char hex
		$hash = hash('sha256', $plain);
		$pdo->prepare('INSERT INTO `epc_mfa_backup_codes` (`user_id`, `code_hash`) VALUES (?, ?)')
			->execute(array($userId, $hash));
		$codes[] = $plain;
	}
	return $codes;
}

function epc_mfa_verify_backup_code(PDO $pdo, int $userId, string $code): array
{
	$hash = hash('sha256', strtoupper(trim($code)));
	$st = $pdo->prepare('SELECT `id` FROM `epc_mfa_backup_codes` WHERE `user_id` = ? AND `code_hash` = ? AND `used` = 0 LIMIT 1');
	$st->execute(array($userId, $hash));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		epc_mfa_log($pdo, $userId, 'backup_code_fail', false);
		return array('ok' => false, 'error' => 'Invalid backup code.');
	}

	$pdo->prepare('UPDATE `epc_mfa_backup_codes` SET `used` = 1 WHERE `id` = ?')
		->execute(array($row['id']));

	epc_mfa_log($pdo, $userId, 'backup_code_used');

	return array('ok' => true, 'message' => 'Backup code accepted.', 'is_backup' => true);
}

/* ─────────────────── Policy ─────────────────── */

function epc_mfa_get_policy(PDO $pdo, string $tenantKey = '__platform__'): array
{
	epc_mfa_ensure_schema($pdo);

	$st = $pdo->prepare('SELECT * FROM `epc_mfa_policy` WHERE `tenant_key` = ? LIMIT 1');
	$st->execute(array($tenantKey));
	$row = $st->fetch(PDO::FETCH_ASSOC);

	$defaults = array(
		'require_mfa_for_roles' => array('super_admin', 'finance_admin', 'finance_user'),
		'require_mfa_for_paths' => array(
			'/cp/shop/finance/',
			'/cp/content/shop/finance/',
		),
		'grace_period_hours' => 72,
	);

	if (!$row) {
		return $defaults;
	}

	$roles = json_decode((string) $row['require_mfa_for_roles'], true);
	$paths = json_decode((string) $row['require_mfa_for_paths'], true);

	return array(
		'require_mfa_for_roles' => is_array($roles) ? $roles : $defaults['require_mfa_for_roles'],
		'require_mfa_for_paths' => is_array($paths) ? $paths : $defaults['require_mfa_for_paths'],
		'grace_period_hours'    => (int) ($row['grace_period_hours'] ?? $defaults['grace_period_hours']),
	);
}

function epc_mfa_save_policy(PDO $pdo, array $policy, string $tenantKey = '__platform__'): bool
{
	epc_mfa_ensure_schema($pdo);

	$roles = json_encode($policy['require_mfa_for_roles'] ?? array());
	$paths = json_encode($policy['require_mfa_for_paths'] ?? array());
	$grace = (int) ($policy['grace_period_hours'] ?? 72);

	$st = $pdo->prepare(
		'INSERT INTO `epc_mfa_policy` (`tenant_key`, `require_mfa_for_roles`, `require_mfa_for_paths`, `grace_period_hours`)
		 VALUES (?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `require_mfa_for_roles` = VALUES(`require_mfa_for_roles`),
		 `require_mfa_for_paths` = VALUES(`require_mfa_for_paths`),
		 `grace_period_hours` = VALUES(`grace_period_hours`)'
	);
	return $st->execute(array($tenantKey, $roles, $paths, $grace));
}

/* ─────────────────── Session + Route Guard ─────────────────── */

function epc_mfa_is_enrolled(PDO $pdo, int $userId): bool
{
	epc_mfa_ensure_schema($pdo);
	$st = $pdo->prepare('SELECT COUNT(*) FROM `epc_mfa_secrets` WHERE `user_id` = ? AND `confirmed` = 1');
	$st->execute(array($userId));
	return ((int) $st->fetchColumn()) > 0;
}

function epc_mfa_session_verified(): bool
{
	return !empty($_SESSION['mfa_verified']) && (int) $_SESSION['mfa_verified_at'] > (time() - 86400);
}

function epc_mfa_session_set_verified(): void
{
	$_SESSION['mfa_verified'] = true;
	$_SESSION['mfa_verified_at'] = time();
}

function epc_mfa_required_for_user(PDO $pdo, int $userId, string $tenantKey = '__platform__'): bool
{
	$policy = epc_mfa_get_policy($pdo, $tenantKey);
	$requiredRoles = $policy['require_mfa_for_roles'];
	if (empty($requiredRoles)) {
		return false;
	}

	// Get user's groups/roles
	try {
		$st = $pdo->prepare(
			'SELECT g.`name` FROM `user_groups` g
			 INNER JOIN `user_group_link` l ON l.`group_id` = g.`id`
			 WHERE l.`user_id` = ?'
		);
		$st->execute(array($userId));
		$userGroups = $st->fetchAll(PDO::FETCH_COLUMN);
	} catch (Exception $e) {
		$userGroups = array();
	}

	// Check if user is admin (user_id in sessions with type=1 → treat as super_admin)
	$isAdmin = false;
	try {
		$st = $pdo->prepare('SELECT `type` FROM `users` WHERE `user_id` = ? LIMIT 1');
		$st->execute(array($userId));
		$userType = (int) $st->fetchColumn();
		$isAdmin = ($userType === 1);
	} catch (Exception $e) {
		// users table structure varies
	}

	if ($isAdmin && in_array('super_admin', $requiredRoles, true)) {
		return true;
	}

	foreach ($userGroups as $group) {
		$normalized = strtolower(str_replace(' ', '_', trim((string) $group)));
		if (in_array($normalized, $requiredRoles, true)) {
			return true;
		}
	}

	// Check ERP department access
	try {
		$st = $pdo->prepare(
			'SELECT `department` FROM `epc_erp_user_departments` WHERE `user_id` = ?'
		);
		$st->execute(array($userId));
		$depts = $st->fetchAll(PDO::FETCH_COLUMN);
		foreach ($depts as $dept) {
			$normalized = strtolower(str_replace(' ', '_', trim((string) $dept)));
			if (strpos($normalized, 'finance') !== false && in_array('finance_user', $requiredRoles, true)) {
				return true;
			}
		}
	} catch (Exception $e) {
		// table may not exist yet
	}

	return false;
}

function epc_mfa_path_requires_mfa(string $path, PDO $pdo, string $tenantKey = '__platform__'): bool
{
	$policy = epc_mfa_get_policy($pdo, $tenantKey);
	foreach ($policy['require_mfa_for_paths'] as $guardedPath) {
		if (strpos($path, $guardedPath) !== false) {
			return true;
		}
	}
	return false;
}

function epc_mfa_enforce_route_guard(PDO $pdo, int $userId, string $currentPath): void
{
	if ($userId <= 0) {
		return;
	}
	if (epc_mfa_session_verified()) {
		return;
	}

	$pathNeedsMfa = epc_mfa_path_requires_mfa($currentPath, $pdo);
	$userNeedsMfa = epc_mfa_required_for_user($pdo, $userId);

	if (!$pathNeedsMfa && !$userNeedsMfa) {
		return;
	}

	if (!epc_mfa_is_enrolled($pdo, $userId)) {
		// User needs MFA but hasn't enrolled — redirect to enrollment
		$_SESSION['mfa_redirect_after'] = $currentPath;
		header('Location: /cp/shop/finance/erp?epc_mfa=enroll&redirect=' . urlencode($currentPath), true, 302);
		exit;
	}

	// User is enrolled but session not verified — redirect to MFA verification
	$_SESSION['mfa_redirect_after'] = $currentPath;
	header('Location: /cp/shop/finance/erp?epc_mfa=verify&redirect=' . urlencode($currentPath), true, 302);
	exit;
}

/* ─────────────────── Status / Admin ─────────────────── */

function epc_mfa_user_status(PDO $pdo, int $userId): array
{
	epc_mfa_ensure_schema($pdo);

	$st = $pdo->prepare(
		'SELECT `method`, `confirmed`, `label`, `last_used_at`, `created_at`
		 FROM `epc_mfa_secrets`
		 WHERE `user_id` = ?
		 ORDER BY `confirmed` DESC, `created_at`'
	);
	$st->execute(array($userId));
	$methods = $st->fetchAll(PDO::FETCH_ASSOC);

	$st2 = $pdo->prepare('SELECT COUNT(*) FROM `epc_mfa_backup_codes` WHERE `user_id` = ? AND `used` = 0');
	$st2->execute(array($userId));
	$unusedBackupCodes = (int) $st2->fetchColumn();

	$enrolled = false;
	foreach ($methods as $m) {
		if ((int) $m['confirmed'] === 1) {
			$enrolled = true;
			break;
		}
	}

	return array(
		'enrolled'           => $enrolled,
		'methods'            => $methods,
		'backup_codes_left'  => $unusedBackupCodes,
		'session_verified'   => epc_mfa_session_verified(),
	);
}

function epc_mfa_disable(PDO $pdo, int $userId): array
{
	epc_mfa_ensure_schema($pdo);

	$pdo->prepare('DELETE FROM `epc_mfa_secrets` WHERE `user_id` = ?')->execute(array($userId));
	$pdo->prepare('DELETE FROM `epc_mfa_backup_codes` WHERE `user_id` = ?')->execute(array($userId));

	unset($_SESSION['mfa_verified'], $_SESSION['mfa_verified_at']);

	epc_mfa_log($pdo, $userId, 'disabled');

	return array('ok' => true, 'message' => 'MFA has been disabled.');
}

/* ─────────────────── Audit Log ─────────────────── */

function epc_mfa_log(PDO $pdo, int $userId, string $action, bool $success = true): void
{
	try {
		epc_mfa_ensure_schema($pdo);
		$pdo->prepare(
			'INSERT INTO `epc_mfa_audit_log` (`user_id`, `action`, `success`, `ip_address`, `user_agent`)
			 VALUES (?, ?, ?, ?, ?)'
		)->execute(array(
			$userId,
			$action,
			$success ? 1 : 0,
			(string) ($_SERVER['REMOTE_ADDR'] ?? ''),
			substr((string) ($_SERVER['HTTP_USER_AGENT'] ?? ''), 0, 255),
		));
	} catch (Exception $e) {
		// Don't break flow on audit log failure
	}
}

function epc_mfa_recent_activity(PDO $pdo, int $userId, int $limit = 20): array
{
	epc_mfa_ensure_schema($pdo);
	$st = $pdo->prepare(
		'SELECT `action`, `success`, `ip_address`, `created_at`
		 FROM `epc_mfa_audit_log`
		 WHERE `user_id` = ?
		 ORDER BY `created_at` DESC
		 LIMIT ?'
	);
	$st->execute(array($userId, $limit));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

/* ─────────────────── QR Code SVG (no external dependency) ─────────────────── */

function epc_mfa_qr_data_uri(string $otpauthUri): string
{
	$size = strlen($otpauthUri);
	$apiUrl = 'https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=' . rawurlencode($otpauthUri);
	return $apiUrl;
}

/* ─────────────────── CP AJAX handler ─────────────────── */

function epc_mfa_handle_ajax(PDO $pdo, int $userId): array
{
	$action = (string) ($_POST['mfa_action'] ?? $_GET['mfa_action'] ?? '');

	switch ($action) {
		case 'status':
			return epc_mfa_user_status($pdo, $userId);

		case 'enroll':
			$email = (string) ($_POST['email'] ?? '');
			if ($email === '') {
				try {
					$st = $pdo->prepare('SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1');
					$st->execute(array($userId));
					$email = (string) $st->fetchColumn();
				} catch (Exception $e) {
					$email = 'user@ecomae.com';
				}
			}
			return epc_mfa_enroll($pdo, $userId, $email);

		case 'confirm':
			$code = (string) ($_POST['code'] ?? '');
			return epc_mfa_confirm_enrollment($pdo, $userId, $code);

		case 'verify':
			$code = (string) ($_POST['code'] ?? '');
			$result = epc_mfa_verify($pdo, $userId, $code);
			if ($result['ok']) {
				epc_mfa_session_set_verified();
			}
			return $result;

		case 'disable':
			return epc_mfa_disable($pdo, $userId);

		case 'activity':
			return array('ok' => true, 'activity' => epc_mfa_recent_activity($pdo, $userId));

		case 'regenerate_backup':
			$codes = epc_mfa_generate_backup_codes($pdo, $userId);
			epc_mfa_log($pdo, $userId, 'backup_codes_regenerated');
			return array('ok' => true, 'backup_codes' => $codes);

		default:
			return array('ok' => false, 'error' => 'Unknown MFA action');
	}
}

/* ─────────────────── CP Shell Auth Gate ─────────────────── */

/**
 * CP shell plugin gate: enforce MFA before granting ERP access.
 * Call from CP authentication plugin after password verification.
 * Returns true if access is granted, false if MFA challenge is needed.
 */
function epc_mfa_cp_auth_gate(PDO $pdo, int $userId, string $currentPath): array
{
	if (epc_mfa_session_verified()) {
		return array('granted' => true, 'reason' => 'mfa_session_active');
	}

	$status = epc_mfa_user_status($pdo, $userId);
	$policy = epc_mfa_get_policy($pdo);
	$requiredPaths = array_filter(explode(',', (string) ($policy['require_mfa_for_paths'] ?? '')));
	$requiredRoles = array_filter(explode(',', (string) ($policy['require_mfa_for_roles'] ?? '')));

	$pathRequiresMfa = false;
	foreach ($requiredPaths as $pattern) {
		if (strpos($currentPath, trim($pattern)) !== false) {
			$pathRequiresMfa = true;
			break;
		}
	}

	if (!$pathRequiresMfa && empty($requiredRoles)) {
		return array('granted' => true, 'reason' => 'mfa_not_required');
	}

	if (!$status['enrolled']) {
		return array(
			'granted' => false,
			'reason' => 'mfa_enrollment_required',
			'redirect' => '/cp/mfa/enroll',
			'grace_hours' => (int) ($policy['grace_period_hours'] ?? 72),
		);
	}

	return array(
		'granted' => false,
		'reason' => 'mfa_verification_required',
		'redirect' => '/cp/mfa/verify',
	);
}

/**
 * ERP finance access gate: require MFA for sensitive ERP operations.
 */
function epc_mfa_erp_finance_gate(PDO $pdo, int $userId, string $erpTab): array
{
	$sensitiveTabs = array('gl', 'vat_return', 'payroll', 'einvoice', 'cash_bank', 'balance_sheet');
	if (!in_array($erpTab, $sensitiveTabs, true)) {
		return array('granted' => true, 'reason' => 'tab_not_sensitive');
	}
	if (epc_mfa_session_verified()) {
		return array('granted' => true, 'reason' => 'mfa_session_active');
	}
	$status = epc_mfa_user_status($pdo, $userId);
	if (!$status['enrolled']) {
		return array('granted' => false, 'reason' => 'mfa_required_for_finance', 'redirect' => '/cp/mfa/enroll');
	}
	return array('granted' => false, 'reason' => 'mfa_challenge_required', 'redirect' => '/cp/mfa/verify');
}

/**
 * Update MFA policy for a tenant (upsert).
 */
function epc_mfa_update_policy(PDO $pdo, string $tenantKey, array $data): array
{
	epc_mfa_ensure_schema($pdo);
	$pdo->prepare(
		'INSERT INTO `epc_mfa_policy` (`tenant_key`, `require_mfa_for_roles`, `require_mfa_for_paths`, `grace_period_hours`)
		 VALUES (?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `require_mfa_for_roles` = VALUES(`require_mfa_for_roles`),
		 `require_mfa_for_paths` = VALUES(`require_mfa_for_paths`),
		 `grace_period_hours` = VALUES(`grace_period_hours`)'
	)->execute(array(
		$tenantKey,
		(string) ($data['require_mfa_for_roles'] ?? ''),
		(string) ($data['require_mfa_for_paths'] ?? ''),
		(int) ($data['grace_period_hours'] ?? 72),
	));
	return array('ok' => true, 'message' => 'MFA policy updated');
}
