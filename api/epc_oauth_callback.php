<?php
/**
 * Central OAuth callback for all providers.
 *
 * Register this single clean URL with every provider:
 *   https://www.ecomae.com/api/epc_oauth_callback.php
 *
 * The provider id travels inside the signed `state`, so no query string is
 * required (Apple-friendly). A `?provider=` hint is accepted but state wins.
 *
 * Apple posts back with response_mode=form_post, so GET and POST are both
 * accepted. Errors render a friendly HTML page — never a 500.
 */
define('_ASTEXE_', 1);
require_once __DIR__ . '/../content/general_pages/epc_oauth_providers.php';

header('Cache-Control: no-store');

/**
 * Render a minimal branded error page (so users are not dropped on a raw 500).
 */
function epc_oauth_callback_fail(int $code, string $message): void
{
	http_response_code($code);
	header('Content-Type: text/html; charset=utf-8');
	$safe = htmlspecialchars($message, ENT_QUOTES, 'UTF-8');
	echo '<!doctype html><html lang="en"><head><meta charset="utf-8">'
		. '<meta name="viewport" content="width=device-width, initial-scale=1">'
		. '<title>Sign-in</title>'
		. '<style>body{font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;background:#f5f6f8;'
		. 'display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;color:#222}'
		. '.card{background:#fff;border-radius:14px;box-shadow:0 18px 50px rgba(0,0,0,.12);'
		. 'padding:32px 28px;max-width:380px;width:calc(100% - 32px);text-align:center}'
		. 'h1{font-size:18px;margin:0 0 10px}p{font-size:14px;color:#555;line-height:1.5;margin:0 0 18px}'
		. 'a{display:inline-block;padding:10px 18px;background:#2563eb;color:#fff;text-decoration:none;'
		. 'border-radius:8px;font-size:14px;font-weight:600}</style></head><body>'
		. '<div class="card"><h1>We couldn&rsquo;t sign you in</h1>'
		. '<p>' . $safe . '</p>'
		. '<a href="javascript:history.length>1?history.back():(location.href=\'/\')">Go back</a></div></body></html>';
	exit;
}

$req = $_SERVER['REQUEST_METHOD'] === 'POST' ? $_POST : $_GET;

// Provider rejection / user cancelled.
if (!empty($req['error'])) {
	$desc = (string) ($req['error_description'] ?? $req['error']);
	epc_oauth_callback_fail(400, 'The sign-in was cancelled or failed: ' . $desc);
}

$code = (string) ($req['code'] ?? '');
$state = (string) ($req['state'] ?? '');
if ($code === '' || $state === '') {
	epc_oauth_callback_fail(400, 'Missing sign-in parameters.');
}

$stateData = epc_oauth_state_unpack($state);
if ($stateData === null) {
	epc_oauth_callback_fail(400, 'This sign-in link has expired. Please try again.');
}

$provider = (string) $stateData['pv'];
if (!epc_oauth_is_configured($provider)) {
	epc_oauth_callback_fail(503, ucfirst($provider) . ' sign-in is not configured.');
}

$exchange = epc_oauth_exchange_code($provider, $code);
if (empty($exchange['ok'])) {
	epc_oauth_callback_fail(400, (string) ($exchange['message'] ?? 'Token exchange failed.'));
}

$result = epc_oauth_complete_login($stateData, $exchange);
if (empty($result['ok']) || empty($result['redirect'])) {
	epc_oauth_callback_fail(403, (string) ($result['message'] ?? 'Sign-in failed.'));
}

header('Location: ' . (string) $result['redirect'], true, 302);
exit;
