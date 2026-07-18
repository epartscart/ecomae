<?php
/**
 * Shared auth for deploy / setup scripts. Prefer env EPC_DEPLOY_TOKEN on production.
 * Optional IP allowlist: EPC_DEPLOY_ALLOWED_IPS=1.2.3.4,5.6.7.8
 *
 * Under .epc-security-lockdown, secret-class scripts require IP allowlist as well.
 */
declare(strict_types=1);

function epc_deploy_token(): string
{
	static $token = null;
	if ($token !== null) {
		return $token;
	}
	$env = getenv('EPC_DEPLOY_TOKEN');
	$token = ($env !== false && $env !== '') ? $env : 'epartscart-deploy-2026';
	return $token;
}

function epc_deploy_client_ip(): string
{
	// Prefer edge-provided CF IP only when REMOTE_ADDR looks like a trusted proxy loopback/private.
	$remote = isset($_SERVER['REMOTE_ADDR']) ? (string) $_SERVER['REMOTE_ADDR'] : '';
	$trustProxy = $remote !== '' && (
		filter_var($remote, FILTER_VALIDATE_IP, FILTER_FLAG_NO_PRIV_RANGE | FILTER_FLAG_NO_RES_RANGE) === false
		|| $remote === '127.0.0.1'
		|| $remote === '::1'
	);
	$keys = $trustProxy
		? array('HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'REMOTE_ADDR')
		: array('REMOTE_ADDR');
	foreach ($keys as $key) {
		if (empty($_SERVER[$key])) {
			continue;
		}
		$raw = (string) $_SERVER[$key];
		if ($key === 'HTTP_X_FORWARDED_FOR') {
			$parts = explode(',', $raw);
			$raw = trim($parts[0]);
		}
		if (filter_var($raw, FILTER_VALIDATE_IP)) {
			return $raw;
		}
	}
	return '0.0.0.0';
}

function epc_deploy_allowed_ips(): array
{
	$raw = getenv('EPC_DEPLOY_ALLOWED_IPS');
	if ($raw === false || trim($raw) === '') {
		return array();
	}
	return array_values(array_filter(array_map('trim', explode(',', $raw))));
}

function epc_deploy_lockdown_enabled(): bool
{
	$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? __DIR__), '/');
	return is_file($root . '/.epc-security-lockdown');
}

function epc_deploy_forbidden(string $message = 'Forbidden'): void
{
	http_response_code(403);
	if (!headers_sent()) {
		header('Content-Type: application/json; charset=utf-8');
		header('X-Content-Type-Options: nosniff');
		header('Cache-Control: no-store');
	}
	exit(json_encode(array('status' => false, 'message' => $message)));
}

/**
 * @param bool $postOnly Require token in POST body only (no GET leakage).
 * @param string $risk   app|ops|secret — secret blocked under lockdown without IP allowlist.
 */
function epc_deploy_require_token(bool $postOnly = false, string $risk = 'ops'): void
{
	$expected = epc_deploy_token();
	$given = $postOnly
		? (string) ($_POST['token'] ?? '')
		: (string) ($_POST['token'] ?? $_GET['token'] ?? '');

	if ($given === '' || !hash_equals($expected, $given)) {
		epc_deploy_forbidden('Forbidden');
	}

	$allowed = epc_deploy_allowed_ips();
	if ($allowed !== array()) {
		$ip = epc_deploy_client_ip();
		if (!in_array($ip, $allowed, true)) {
			// Do not echo client IP (info disclosure).
			epc_deploy_forbidden('Forbidden');
		}
	}

	if ($risk === 'secret' && epc_deploy_lockdown_enabled()) {
		if ($allowed === array()) {
			epc_deploy_forbidden('Forbidden');
		}
	}

	// When lockdown is on and default token is still in use, deny secret + write tools.
	if (epc_deploy_lockdown_enabled() && $expected === 'epartscart-deploy-2026' && $risk === 'secret') {
		epc_deploy_forbidden('Forbidden');
	}
}

function epc_redirect_safe_target(string $target): string
{
	$target = trim($target);
	if ($target === '' || $target === '/') {
		return '/';
	}
	if (preg_match('#^https?://#i', $target) || strpos($target, '//') === 0) {
		return '/';
	}
	if ($target[0] !== '/') {
		return '/';
	}
	if (preg_match('/[\r\n\x00]/', $target)) {
		return '/';
	}
	return $target;
}
