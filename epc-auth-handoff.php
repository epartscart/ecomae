<?php
/**
 * Cross-host session handoff after OAuth on central ecomae.com.
 */
define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_auth_common.php';

$p = (string) ($_GET['p'] ?? '');
$s = (string) ($_GET['s'] ?? '');
$data = epc_auth_handoff_verify($p, $s);
if ($data === null) {
	http_response_code(403);
	echo 'Invalid or expired sign-in link. Please try again.';
	exit;
}

$userId = (int) ($data['uid'] ?? 0);
$sessionToken = (string) ($data['sess'] ?? '');
$mode = epc_auth_normalize_mode((string) ($data['mode'] ?? 'cp'));
$path = (string) ($data['path'] ?? '/');
if ($path === '' || $path[0] !== '/') {
	$path = '/' . ltrim($path, '/');
}

if ($userId <= 0 || $sessionToken === '') {
	http_response_code(403);
	echo 'Incomplete handoff payload.';
	exit;
}

if ($mode === 'storefront') {
	epc_auth_set_storefront_session_cookies($userId, $sessionToken);
} else {
	epc_auth_set_cp_session_cookies($userId, $sessionToken);
}

$redirect = $path;
if (!empty($data['host'])) {
	$host = (string) $data['host'];
	$redirect = 'https://' . $host . $path;
}

header('Location: ' . $redirect, true, 302);
exit;
