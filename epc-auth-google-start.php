<?php
/**
 * GET: tenant_key, context=cp|storefront, return_url (optional)
 */
define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_auth_social.php';

$mode = epc_auth_normalize_mode((string) ($_GET['context'] ?? 'cp'));
$hints = array(
	'tenant_key' => (string) ($_GET['tenant_key'] ?? $_GET['site_key'] ?? ''),
);
$ctx = epc_auth_resolve_for_mode($mode, $hints);
if (empty($ctx['ok'])) {
	http_response_code(400);
	echo 'Auth context error: ' . htmlspecialchars((string) ($ctx['message'] ?? 'unknown'), ENT_QUOTES, 'UTF-8');
	exit;
}
$ctx['auth_mode'] = $mode;
if (!empty($_GET['return_url'])) {
	$ctx['return_url'] = (string) $_GET['return_url'];
}

$url = epc_auth_google_start_url($ctx);
if ($url === '') {
	http_response_code(503);
	echo 'Google sign-in is not configured.';
	exit;
}
header('Location: ' . $url, true, 302);
exit;
