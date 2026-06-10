<?php
/**
 * Shared auth for deploy / setup scripts. Prefer env EPC_DEPLOY_TOKEN on production.
 * Optional IP allowlist: EPC_DEPLOY_ALLOWED_IPS=1.2.3.4,5.6.7.8
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
	foreach (array('HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'REMOTE_ADDR') as $key) {
		if (empty($_SERVER[$key])) {
			continue;
		}
		$raw = (string)$_SERVER[$key];
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

function epc_deploy_require_token(bool $postOnly = false): void
{
	$expected = epc_deploy_token();
	$given = $postOnly
		? (string)($_POST['token'] ?? '')
		: (string)($_POST['token'] ?? $_GET['token'] ?? '');

	if ($given === '' || !hash_equals($expected, $given)) {
		http_response_code(403);
		if (!headers_sent()) {
			header('Content-Type: application/json; charset=utf-8');
		}
		exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
	}

	$allowed = epc_deploy_allowed_ips();
	if ($allowed !== array()) {
		$ip = epc_deploy_client_ip();
		if (!in_array($ip, $allowed, true)) {
			http_response_code(403);
			if (!headers_sent()) {
				header('Content-Type: application/json; charset=utf-8');
			}
			exit(json_encode(array('status' => false, 'message' => 'IP not allowed', 'ip' => $ip)));
		}
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
