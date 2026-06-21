<?php
/**
 * Transparent password hash upgrade — MD5 → bcrypt.
 *
 * The legacy scheme stores md5($plain . $secret_succession). This helper lets
 * the login flow verify either format and silently re-hash to bcrypt on
 * successful authentication so every active user migrates automatically.
 *
 * Worldwide principle: no country-specific logic; pure auth utility.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Check whether a stored hash is the legacy MD5 format (32-hex-char string).
 */
function epc_password_is_legacy_md5(string $storedHash): bool
{
	return strlen($storedHash) === 32 && ctype_xdigit($storedHash);
}

/**
 * Verify a plaintext password against the stored hash.
 *
 * Supports both:
 *   - Legacy: md5($plain . $secret)
 *   - Modern: password_hash() output (bcrypt / argon2)
 *
 * @return bool True when password matches.
 */
function epc_password_verify(string $plain, string $storedHash, string $secret): bool
{
	if (epc_password_is_legacy_md5($storedHash)) {
		return hash_equals($storedHash, md5($plain . $secret));
	}
	return password_verify($plain, $storedHash);
}

/**
 * Hash a plaintext password using bcrypt (cost 12).
 */
function epc_password_hash(string $plain): string
{
	return password_hash($plain, PASSWORD_BCRYPT, ['cost' => 12]);
}

/**
 * After successful verification, upgrade the stored hash to bcrypt if it is
 * still in the legacy MD5 format. Call this inside the login flow, AFTER
 * confirming the password is correct.
 *
 * @return bool True if an upgrade was performed.
 */
function epc_password_upgrade_if_needed(PDO $db, int $userId, string $plain, string $storedHash): bool
{
	if (!epc_password_is_legacy_md5($storedHash)) {
		return false;
	}
	$newHash = epc_password_hash($plain);
	// Try user_id first (most common), fall back to id
	$st = $db->prepare('UPDATE `users` SET `password` = ? WHERE `user_id` = ? LIMIT 1');
	$st->execute([$newHash, $userId]);
	if ($st->rowCount() === 0) {
		$st2 = $db->prepare('UPDATE `users` SET `password` = ? WHERE `id` = ? LIMIT 1');
		$st2->execute([$newHash, $userId]);
	}
	return true;
}

/**
 * Check whether a stored hash needs rehashing (e.g. bcrypt cost changed).
 */
function epc_password_needs_rehash(string $storedHash): bool
{
	if (epc_password_is_legacy_md5($storedHash)) {
		return true;
	}
	return password_needs_rehash($storedHash, PASSWORD_BCRYPT, ['cost' => 12]);
}
