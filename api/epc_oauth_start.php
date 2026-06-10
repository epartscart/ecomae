<?php
/**
 * OAuth start endpoint — builds the provider authorize URL and redirects.
 *
 * GET /api/epc_oauth_start.php?provider=google|microsoft|facebook|github|apple
 *     &context=cp|storefront&tenant_key=...&return_url=/en/&terms=1
 *
 * Returns a 302 redirect to the provider, or a plain-text error (never 500)
 * when the provider is unknown / not configured.
 */
define('_ASTEXE_', 1);
require_once __DIR__ . '/../content/general_pages/epc_oauth_providers.php';

header('Cache-Control: no-store');

$provider = strtolower(trim((string) ($_GET['provider'] ?? '')));
if (!epc_oauth_is_known_provider($provider)) {
	http_response_code(400);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'Unknown sign-in provider.';
	exit;
}
if (!epc_oauth_is_configured($provider)) {
	http_response_code(503);
	header('Content-Type: text/plain; charset=utf-8');
	echo ucfirst($provider) . ' sign-in is not configured yet.';
	exit;
}

$mode = epc_auth_normalize_mode((string) ($_GET['context'] ?? 'storefront'));
$hints = array('tenant_key' => (string) ($_GET['tenant_key'] ?? $_GET['site_key'] ?? ''));
$ctx = epc_auth_resolve_for_mode($mode, $hints);
if (empty($ctx['ok'])) {
	http_response_code(400);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'Sign-in context error: ' . htmlspecialchars((string) ($ctx['message'] ?? 'unknown'), ENT_QUOTES, 'UTF-8');
	exit;
}
$ctx['auth_mode'] = $mode;
if (!empty($_GET['return_url'])) {
	$ctx['return_url'] = (string) $_GET['return_url'];
}
$ctx['terms_accepted'] = !empty($_GET['terms']);

$url = epc_oauth_build_auth_url($provider, $ctx);
if ($url === '') {
	http_response_code(503);
	header('Content-Type: text/plain; charset=utf-8');
	echo ucfirst($provider) . ' sign-in is not configured yet.';
	exit;
}

header('Location: ' . $url, true, 302);
exit;
