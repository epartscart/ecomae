<?php
/**
 * Central Google OAuth callback (www.ecomae.com).
 */
define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_auth_social.php';

if (!empty($_GET['error'])) {
	http_response_code(400);
	echo 'Google sign-in was cancelled or failed.';
	exit;
}

$code = (string) ($_GET['code'] ?? '');
$state = (string) ($_GET['state'] ?? '');
if ($code === '' || $state === '') {
	http_response_code(400);
	echo 'Missing OAuth parameters.';
	exit;
}

$stateData = epc_auth_oauth_state_unpack($state);
if ($stateData === null) {
	http_response_code(400);
	echo 'Invalid or expired OAuth state.';
	exit;
}

$exchange = epc_auth_google_exchange_code($code);
if (empty($exchange['ok'])) {
	http_response_code(400);
	echo htmlspecialchars((string) ($exchange['message'] ?? 'Token exchange failed'), ENT_QUOTES, 'UTF-8');
	exit;
}

$result = epc_auth_google_complete_login($stateData, $exchange['profile'] ?? array());
if (empty($result['ok']) || empty($result['redirect'])) {
	http_response_code(403);
	echo htmlspecialchars((string) ($result['message'] ?? 'Sign-in failed'), ENT_QUOTES, 'UTF-8');
	exit;
}

header('Location: ' . (string) $result['redirect'], true, 302);
exit;
