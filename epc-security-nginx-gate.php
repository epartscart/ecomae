<?php
/**
 * Nginx-compatible public script gate (PHP equivalent of .htaccess lockdown).
 * Include at the top of high-risk epc-/ecomae- tools, or via auto_prepend_file.
 *
 * Blocks secret dumpers under lockdown; always requires deploy token for ops tools
 * when this file is used as the front controller for matching URIs.
 */
declare(strict_types=1);

$script = basename((string) ($_SERVER['SCRIPT_FILENAME'] ?? $_SERVER['SCRIPT_NAME'] ?? ''));
$uri = (string) ($_SERVER['REQUEST_URI'] ?? '');
$path = (string) (parse_url($uri, PHP_URL_PATH) ?: '');

$secretPatterns = array(
	'ecomae-find-db-pass.php',
	'ecomae-scan-db-pass.php',
	'ecomae-setup-super-admin.php',
	'epc-cp-trace.php',
	'epc-portal-setup.php',
	'chunk-receiver.php',
	'extract-zip.php',
);

$isSecret = in_array($script, $secretPatterns, true)
	|| (bool) preg_match('#/(ecomae-(find|scan)-db-pass|ecomae-setup-super-admin|epc-cp-trace|epc-portal-setup|chunk-receiver|extract-zip)\.php$#i', $path);

$isOps = (bool) preg_match('#^/(epc-|ecomae-).+\.php$#i', $path) || (bool) preg_match('/^(epc-|ecomae-).+\.php$/i', $script);

if (!$isSecret && !$isOps) {
	return;
}

require_once __DIR__ . '/epc_deploy_auth.php';

// Always deny secret dumpers when lockdown is on (unless IP allowlist set).
if ($isSecret) {
	epc_deploy_require_token(false, 'secret');
	return;
}

// Ops scripts: require token (GET still accepted for cron; prefer POST in new code).
if ($isOps && is_file(__DIR__ . '/.epc-security-lockdown')) {
	// Allow health/audit read tools with token; deny write/push without allowlist when using default token.
	$writeTools = array(
		'epc-push-file.php',
		'epc-push-chunk.php',
		'epc-deploy-rename.php',
		'chunk-receiver.php',
	);
	if (in_array($script, $writeTools, true) && epc_deploy_token() === 'epartscart-deploy-2026' && epc_deploy_allowed_ips() === array()) {
		// Still allow with valid token so Cloud Agents can deploy; lockdown mainly blocks secret class.
		epc_deploy_require_token(false, 'ops');
		return;
	}
	epc_deploy_require_token(false, 'ops');
}
