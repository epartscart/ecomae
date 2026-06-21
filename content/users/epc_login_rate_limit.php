<?php
/**
 * Login rate limiting — brute-force protection.
 *
 * Tracks failed login attempts per IP + email in a DB table.
 * After $maxAttempts failures within $windowSeconds, the account/IP is
 * temporarily locked out. The lockout clears automatically after the window
 * expires.
 *
 * Enterprise standard: rate limiting is critical for SOC2, ISO 27001, and PCI.
 * Worldwide principle: no country-specific behaviour.
 */
defined('_ASTEXE_') or die('No access');

function epc_login_rate_limit_ensure_table(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	try {
		$db->exec("CREATE TABLE IF NOT EXISTS `epc_login_attempts` (
			`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
			`ip_address` VARCHAR(45) NOT NULL DEFAULT '',
			`email` VARCHAR(255) NOT NULL DEFAULT '',
			`attempted_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			`success` TINYINT(1) NOT NULL DEFAULT 0,
			INDEX `idx_ip_email` (`ip_address`, `email`, `attempted_at`),
			INDEX `idx_cleanup` (`attempted_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
	} catch (Exception $e) {
		// Table may already exist or DB lacks CREATE privilege — safe to continue.
	}
	$done = true;
}

/**
 * Record a login attempt.
 */
function epc_login_rate_limit_record(PDO $db, string $email, bool $success): void
{
	epc_login_rate_limit_ensure_table($db);
	$ip = epc_login_rate_limit_client_ip();
	try {
		$st = $db->prepare("INSERT INTO `epc_login_attempts` (`ip_address`, `email`, `attempted_at`, `success`) VALUES (?, ?, NOW(), ?)");
		$st->execute([$ip, strtolower(trim($email)), $success ? 1 : 0]);
	} catch (Exception $e) {
		// Never block login flow due to rate-limit storage failure.
	}
}

/**
 * Check if a login attempt should be blocked.
 *
 * @param int $maxAttempts  Max failed attempts before lockout (default: 10).
 * @param int $windowSeconds  Time window in seconds (default: 900 = 15 min).
 * @return array{blocked: bool, remaining: int, retry_after: int}
 */
function epc_login_rate_limit_check(PDO $db, string $email, int $maxAttempts = 10, int $windowSeconds = 900): array
{
	epc_login_rate_limit_ensure_table($db);
	$ip = epc_login_rate_limit_client_ip();
	$result = ['blocked' => false, 'remaining' => $maxAttempts, 'retry_after' => 0];

	try {
		// Check by IP (protects against distributed attacks on one IP)
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_login_attempts` WHERE `ip_address` = ? AND `success` = 0 AND `attempted_at` > DATE_SUB(NOW(), INTERVAL ? SECOND)");
		$st->execute([$ip, $windowSeconds]);
		$ipFails = (int) $st->fetchColumn();

		// Check by email (protects against credential stuffing on one account)
		$st2 = $db->prepare("SELECT COUNT(*) FROM `epc_login_attempts` WHERE `email` = ? AND `success` = 0 AND `attempted_at` > DATE_SUB(NOW(), INTERVAL ? SECOND)");
		$st2->execute([strtolower(trim($email)), $windowSeconds]);
		$emailFails = (int) $st2->fetchColumn();

		$maxFails = max($ipFails, $emailFails);
		$result['remaining'] = max(0, $maxAttempts - $maxFails);

		if ($maxFails >= $maxAttempts) {
			$result['blocked'] = true;
			// Calculate retry_after from earliest expiring attempt
			$st3 = $db->prepare("SELECT MIN(`attempted_at`) AS oldest FROM `epc_login_attempts` WHERE (`ip_address` = ? OR `email` = ?) AND `success` = 0 AND `attempted_at` > DATE_SUB(NOW(), INTERVAL ? SECOND) ORDER BY `attempted_at` ASC LIMIT 1");
			$st3->execute([$ip, strtolower(trim($email)), $windowSeconds]);
			$oldest = $st3->fetchColumn();
			if ($oldest) {
				$result['retry_after'] = max(0, $windowSeconds - (time() - strtotime($oldest)));
			}
		}
	} catch (Exception $e) {
		// If rate-limit check fails, allow the attempt (fail open for availability).
	}

	return $result;
}

/**
 * Clear failed attempts after a successful login.
 */
function epc_login_rate_limit_clear(PDO $db, string $email): void
{
	$ip = epc_login_rate_limit_client_ip();
	try {
		$st = $db->prepare("DELETE FROM `epc_login_attempts` WHERE `ip_address` = ? AND `email` = ?");
		$st->execute([$ip, strtolower(trim($email))]);
	} catch (Exception $e) {
		// Silent.
	}
}

/**
 * Periodic cleanup — remove attempts older than 24 hours.
 * Call from a cron or at login time.
 */
function epc_login_rate_limit_cleanup(PDO $db): void
{
	try {
		$db->exec("DELETE FROM `epc_login_attempts` WHERE `attempted_at` < DATE_SUB(NOW(), INTERVAL 24 HOUR)");
	} catch (Exception $e) {
		// Silent.
	}
}

function epc_login_rate_limit_client_ip(): string
{
	// Cloudflare passes the real IP in CF-Connecting-IP
	if (!empty($_SERVER['HTTP_CF_CONNECTING_IP'])) {
		return (string) $_SERVER['HTTP_CF_CONNECTING_IP'];
	}
	if (!empty($_SERVER['HTTP_X_FORWARDED_FOR'])) {
		$parts = explode(',', (string) $_SERVER['HTTP_X_FORWARDED_FOR']);
		return trim($parts[0]);
	}
	return (string) ($_SERVER['REMOTE_ADDR'] ?? '0.0.0.0');
}
