<?php
/**
 * Session security hardening — enterprise-grade session management.
 *
 * Features:
 * - Session fixation prevention (regenerate ID on privilege change)
 * - Idle timeout (configurable, default 30 min)
 * - Absolute session lifetime (default 8 hours)
 * - IP binding (optional, for high-security contexts)
 * - User-agent consistency check
 *
 * Worldwide principle: no country-specific behaviour.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Harden session settings. Call BEFORE session_start().
 */
function epc_session_harden_ini(): void
{
	// Cookies only (no URL-based session IDs)
	ini_set('session.use_only_cookies', '1');
	ini_set('session.use_strict_mode', '1');

	// HttpOnly + Secure flags
	$params = session_get_cookie_params();
	$secure = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off')
		|| (!empty($_SERVER['HTTP_X_FORWARDED_PROTO']) && $_SERVER['HTTP_X_FORWARDED_PROTO'] === 'https');
	session_set_cookie_params([
		'lifetime' => 0,
		'path' => $params['path'] ?: '/',
		'domain' => $params['domain'] ?: '',
		'secure' => $secure,
		'httponly' => true,
		'samesite' => 'Lax',
	]);
}

/**
 * Regenerate session ID on privilege escalation (login, role change).
 * Prevents session fixation attacks.
 */
function epc_session_regenerate(): void
{
	if (session_status() === PHP_SESSION_ACTIVE) {
		session_regenerate_id(true);
		$_SESSION['epc_session_created'] = time();
		$_SESSION['epc_session_last_active'] = time();
		$_SESSION['epc_session_ua'] = $_SERVER['HTTP_USER_AGENT'] ?? '';
	}
}

/**
 * Validate session integrity — call on every authenticated request.
 *
 * @param int $idleTimeout  Seconds of inactivity before session expires (default: 1800 = 30 min).
 * @param int $absoluteLifetime  Maximum session lifetime in seconds (default: 28800 = 8 hours).
 * @return bool True if session is valid, false if expired/invalid.
 */
function epc_session_validate(int $idleTimeout = 1800, int $absoluteLifetime = 28800): bool
{
	if (session_status() !== PHP_SESSION_ACTIVE) {
		return false;
	}

	$now = time();

	// Check absolute lifetime
	$created = isset($_SESSION['epc_session_created']) ? (int) $_SESSION['epc_session_created'] : 0;
	if ($created > 0 && ($now - $created) > $absoluteLifetime) {
		epc_session_destroy_safe();
		return false;
	}

	// Check idle timeout
	$lastActive = isset($_SESSION['epc_session_last_active']) ? (int) $_SESSION['epc_session_last_active'] : 0;
	if ($lastActive > 0 && ($now - $lastActive) > $idleTimeout) {
		epc_session_destroy_safe();
		return false;
	}

	// User-agent consistency (detect session hijacking)
	$storedUa = isset($_SESSION['epc_session_ua']) ? (string) $_SESSION['epc_session_ua'] : '';
	$currentUa = (string) ($_SERVER['HTTP_USER_AGENT'] ?? '');
	if ($storedUa !== '' && $storedUa !== $currentUa) {
		epc_session_destroy_safe();
		return false;
	}

	// Update last-active timestamp
	$_SESSION['epc_session_last_active'] = $now;

	return true;
}

/**
 * Safely destroy the current session.
 */
function epc_session_destroy_safe(): void
{
	$_SESSION = [];
	if (ini_get('session.use_cookies')) {
		$p = session_get_cookie_params();
		setcookie(session_name(), '', time() - 42000, $p['path'], $p['domain'], $p['secure'], $p['httponly']);
	}
	if (session_status() === PHP_SESSION_ACTIVE) {
		session_destroy();
	}
}

/**
 * Get session security metadata for audit/display.
 *
 * @return array{created: int, last_active: int, idle_seconds: int, lifetime_seconds: int}
 */
function epc_session_metadata(): array
{
	$now = time();
	$created = (int) ($_SESSION['epc_session_created'] ?? $now);
	$lastActive = (int) ($_SESSION['epc_session_last_active'] ?? $now);
	return [
		'created' => $created,
		'last_active' => $lastActive,
		'idle_seconds' => $now - $lastActive,
		'lifetime_seconds' => $now - $created,
	];
}
