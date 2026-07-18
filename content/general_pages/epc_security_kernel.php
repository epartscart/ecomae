<?php
/**
 * Platform security kernel — shared hardening for BOS / CP / ERP / storefront / deploy.
 *
 * Goals:
 * - No unauthenticated fleet/operator mutations
 * - No secret leakage in JSON/HTML errors
 * - Tight cookies, CSRF, security headers
 * - Deploy scripts fail closed under lockdown for destructive/secret surfaces
 */
defined('_ASTEXE_') or define('_ASTEXE_', 1);

/** Send baseline security headers (safe to call multiple times). */
function epc_sec_send_headers(string $frame = 'SAMEORIGIN'): void
{
	if (headers_sent()) {
		return;
	}
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	header('X-Content-Type-Options: nosniff');
	header('X-Frame-Options: ' . $frame);
	header('Referrer-Policy: strict-origin-when-cross-origin');
	header('Permissions-Policy: geolocation=(), microphone=(), camera=(), payment=()');
	header('Cross-Origin-Opener-Policy: same-origin');
	header('X-XSS-Protection: 0');
	$uri = (string) ($_SERVER['REQUEST_URI'] ?? '');
	if (preg_match('#^/(bos|cp|erp)(/|$)#i', $uri)) {
		header('X-Robots-Tag: noindex, nofollow, noarchive');
		header('Cache-Control: no-store, no-cache, must-revalidate, private');
	}
}

/** True when production lockdown flag is present. */
function epc_sec_lockdown_enabled(): bool
{
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? dirname(__DIR__, 2)), '/');
	return is_file($root . '/.epc-security-lockdown');
}

/** Script basename classification for deploy/public PHP tools. */
function epc_sec_script_risk_class(string $scriptBasename): string
{
	$base = strtolower(basename($scriptBasename));
	$secret = array(
		'ecomae-find-db-pass.php',
		'ecomae-scan-db-pass.php',
		'ecomae-setup-super-admin.php',
		'epc-cp-trace.php',
		'epc-portal-setup.php',
		'chunk-receiver.php',
		'extract-zip.php',
	);
	if (in_array($base, $secret, true)) {
		return 'secret';
	}
	if (preg_match('/^(epc-|ecomae-).+\.php$/', $base)) {
		return 'ops';
	}
	return 'app';
}

/**
 * Hard gate for high-risk public PHP tools (call after epc_deploy_require_token or alone).
 * Secret-class scripts are denied when lockdown is on, unless EPC_DEPLOY_ALLOWED_IPS matches.
 */
function epc_sec_require_ops_access(string $risk = 'ops'): void
{
	require_once dirname(__DIR__, 2) . '/epc_deploy_auth.php';
	if ($risk === 'secret' && epc_sec_lockdown_enabled()) {
		$allowed = epc_deploy_allowed_ips();
		$ip = epc_deploy_client_ip();
		if ($allowed === array() || !in_array($ip, $allowed, true)) {
			http_response_code(403);
			header('Content-Type: application/json; charset=utf-8');
			exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
		}
	}
	epc_deploy_require_token();
}

/** Safe public error payload — never echo exception/DB details. */
function epc_sec_safe_error(string $publicMessage = 'Request failed', int $http = 400): array
{
	if ($http >= 400 && !headers_sent()) {
		http_response_code($http);
	}
	return array('ok' => false, 'error' => $publicMessage);
}

/** CSRF token for BOS / operator shells. */
function epc_sec_csrf_token(string $scope = 'bos'): string
{
	if (session_status() !== PHP_SESSION_ACTIVE) {
		@session_start();
	}
	$key = 'epc_csrf_' . preg_replace('/[^a-z0-9_]/', '', strtolower($scope));
	if (empty($_SESSION[$key]) || !is_string($_SESSION[$key])) {
		$_SESSION[$key] = bin2hex(random_bytes(32));
	}
	return $_SESSION[$key];
}

function epc_sec_csrf_validate(string $scope = 'bos', ?string $token = null): bool
{
	if (session_status() !== PHP_SESSION_ACTIVE) {
		@session_start();
	}
	$key = 'epc_csrf_' . preg_replace('/[^a-z0-9_]/', '', strtolower($scope));
	$expected = isset($_SESSION[$key]) ? (string) $_SESSION[$key] : '';
	if ($expected === '') {
		return false;
	}
	if ($token === null) {
		$token = (string) ($_POST['epc_csrf'] ?? $_SERVER['HTTP_X_EPC_CSRF'] ?? '');
	}
	return $token !== '' && hash_equals($expected, $token);
}

function epc_sec_require_csrf(string $scope = 'bos'): void
{
	$method = strtoupper((string) ($_SERVER['REQUEST_METHOD'] ?? 'GET'));
	if (!in_array($method, array('POST', 'PUT', 'PATCH', 'DELETE'), true)) {
		return;
	}
	if (!epc_sec_csrf_validate($scope)) {
		http_response_code(403);
		header('Content-Type: application/json; charset=utf-8');
		exit(json_encode(array('ok' => false, 'error' => 'CSRF validation failed')));
	}
}

/** Simple file-based rate limit (per IP + bucket). */
function epc_sec_rate_limit(string $bucket, int $maxAttempts = 20, int $windowSec = 300): bool
{
	$ip = function_exists('epc_deploy_client_ip') ? epc_deploy_client_ip() : (string) ($_SERVER['REMOTE_ADDR'] ?? '0');
	$dir = sys_get_temp_dir() . '/epc_rl';
	if (!is_dir($dir)) {
		@mkdir($dir, 0700, true);
	}
	$file = $dir . '/' . hash('sha256', $bucket . '|' . $ip) . '.json';
	$now = time();
	$data = array('t' => $now, 'n' => 0);
	if (is_file($file)) {
		$raw = @file_get_contents($file);
		$decoded = is_string($raw) ? json_decode($raw, true) : null;
		if (is_array($decoded) && isset($decoded['t'], $decoded['n'])) {
			if (($now - (int) $decoded['t']) <= $windowSec) {
				$data = array('t' => (int) $decoded['t'], 'n' => (int) $decoded['n']);
			}
		}
	}
	$data['n']++;
	@file_put_contents($file, json_encode($data), LOCK_EX);
	return $data['n'] <= $maxAttempts;
}

function epc_sec_require_rate_limit(string $bucket, int $maxAttempts = 20, int $windowSec = 300): void
{
	if (!epc_sec_rate_limit($bucket, $maxAttempts, $windowSec)) {
		http_response_code(429);
		header('Content-Type: application/json; charset=utf-8');
		exit(json_encode(array('ok' => false, 'error' => 'Too many attempts — try again later')));
	}
}

/**
 * Does this user_id belong to a backend/admin group in the given PDO DB?
 */
function epc_sec_user_has_backend_group(PDO $pdo, int $userId): bool
{
	if ($userId <= 0) {
		return false;
	}
	try {
		$ids = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1')->fetchAll(PDO::FETCH_COLUMN);
		if (!$ids) {
			return false;
		}
		$all = array();
		foreach ($ids as $gid) {
			$gid = (int) $gid;
			$all[] = $gid;
			$st = $pdo->prepare('SELECT `id` FROM `groups` WHERE `parent` = ?');
			$st->execute(array($gid));
			foreach ($st->fetchAll(PDO::FETCH_COLUMN) as $child) {
				$all[] = (int) $child;
			}
		}
		$all = array_values(array_unique(array_filter($all)));
		if ($all === array()) {
			return false;
		}
		$in = implode(',', array_fill(0, count($all), '?'));
		$st = $pdo->prepare("SELECT COUNT(*) FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN ($in)");
		$st->execute(array_merge(array($userId), $all));
		return ((int) $st->fetchColumn()) > 0;
	} catch (Throwable $e) {
		return false;
	}
}

/** Platform operator email allowlist (comma-separated env or settings). */
function epc_sec_provider_email_allowlist(): array
{
	$raw = getenv('EPC_BOS_PROVIDER_EMAILS');
	if ($raw === false || trim((string) $raw) === '') {
		// Safe defaults for this platform — storefront customers never match.
		$raw = 'ecomae.admin,admin@ecomae.com,hello@ecomae.com';
	}
	$list = array();
	foreach (explode(',', (string) $raw) as $part) {
		$part = strtolower(trim($part));
		if ($part !== '') {
			$list[] = $part;
		}
	}
	return $list;
}

function epc_sec_email_is_provider_allowlisted(string $email): bool
{
	$email = strtolower(trim($email));
	if ($email === '') {
		return false;
	}
	foreach (epc_sec_provider_email_allowlist() as $entry) {
		if ($entry === $email) {
			return true;
		}
		// Allow bare local-part match for CMS logins like ecomae.admin (no @).
		if (strpos($entry, '@') === false && ($email === $entry || strpos($email, $entry . '@') === 0)) {
			return true;
		}
	}
	return false;
}

/**
 * Decide BOS role after password OK.
 * @return array{role:string,tenant_key:string,allowed:bool,reason:string}
 */
function epc_sec_bos_resolve_role(PDO $pdo, array $userRow, string $email): array
{
	$userId = (int) ($userRow['id'] ?? $userRow['ID'] ?? $userRow['user_id'] ?? 0);
	$siteKey = trim((string) ($userRow['site_key'] ?? ''));
	$table = (string) ($userRow['_table'] ?? 'users');

	if ($siteKey !== '') {
		return array('role' => 'tenant', 'tenant_key' => $siteKey, 'allowed' => true, 'reason' => 'tenant_site_key');
	}

	// Dedicated admin tables on platform DB → provider.
	if (in_array($table, array('admin', 'epc_cp_users'), true)) {
		return array('role' => 'provider', 'tenant_key' => '', 'allowed' => true, 'reason' => 'admin_table');
	}

	$backend = epc_sec_user_has_backend_group($pdo, $userId);
	$allowlisted = epc_sec_email_is_provider_allowlisted($email);

	if ($backend || $allowlisted) {
		return array('role' => 'provider', 'tenant_key' => '', 'allowed' => true, 'reason' => $backend ? 'backend_group' : 'allowlist');
	}

	return array(
		'role' => 'guest',
		'tenant_key' => '',
		'allowed' => false,
		'reason' => 'not_operator',
	);
}
