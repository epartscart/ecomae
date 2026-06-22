<?php
/**
 * Server overload guard.
 *
 * Checks system load average and serves a lightweight static HTML page when
 * the server is critically overloaded.  This prevents cascading failures
 * where heavy storefront renders block BOS, CP, and marketing requests.
 *
 * Bypasses:
 *  - CLI (cron, deploy scripts)
 *  - BOS requests (/bos/)
 *  - Cache purge / deploy endpoints (epc-*)
 *  - AJAX requests to CP
 *  - Already-cached pages (page cache serves directly)
 */
defined('_ASTEXE_') or die('No access');

function epc_server_guard_check(): bool
{
	if (PHP_SAPI === 'cli') {
		return false;
	}

	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (!is_string($path)) {
		$path = '/';
	}

	// Never block BOS, deploy scripts, API, or health checks
	if (preg_match('#^/(bos|epc-|api/|epc_static)#i', $path)) {
		return false;
	}
	// Never block CP AJAX (login, portal calls)
	if (preg_match('#^/cp/content/control/portal/ajax#i', $path)) {
		return false;
	}
	// Never block CP login page
	if (preg_match('#^/cp(?:/|$)#i', $path)) {
		return false;
	}

	// Check 1-minute load average (Linux only)
	if (!is_readable('/proc/loadavg')) {
		return false;
	}
	$load_str = @file_get_contents('/proc/loadavg');
	if ($load_str === false) {
		return false;
	}
	$parts = explode(' ', $load_str);
	$load_1min = (float) ($parts[0] ?? 0);

	// Threshold: 4x CPU cores.  On a 2-core machine this fires at load 8+.
	$cores = 1;
	if (is_readable('/proc/cpuinfo')) {
		$cpuinfo = @file_get_contents('/proc/cpuinfo');
		if ($cpuinfo !== false) {
			$cores = max(1, substr_count($cpuinfo, 'processor'));
		}
	}
	$threshold = $cores * 4;

	// Lightweight tenants (non-epartscart) get a higher threshold — they render
	// in <3s since they don't have 133K+ parts.  Only block them if TRULY critical.
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$isHeavyTenant = (strpos($host, 'epartscart') !== false);
	if (!$isHeavyTenant) {
		$threshold = $cores * 6;
	}

	if ($load_1min < $threshold) {
		return false;
	}

	// Server is overloaded — serve a lightweight page
	http_response_code(503);
	header('Retry-After: 30');
	header('Content-Type: text/html; charset=UTF-8');
	header('Cache-Control: no-store');
	echo epc_server_guard_busy_html();
	return true;
}

function epc_server_guard_busy_html(): string
{
	return '<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Temporarily Busy</title>
<meta http-equiv="refresh" content="15">
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;background:#0f172a;color:#e2e8f0;display:flex;align-items:center;justify-content:center;min-height:100vh;padding:2rem}
.card{text-align:center;max-width:480px;background:#1e293b;border-radius:16px;padding:3rem 2rem;box-shadow:0 25px 50px rgba(0,0,0,.3)}
.icon{font-size:3rem;margin-bottom:1rem}
h1{font-size:1.5rem;margin-bottom:.75rem;color:#f1f5f9}
p{color:#94a3b8;line-height:1.6;margin-bottom:1rem}
.hint{font-size:.85rem;color:#64748b;margin-top:1.5rem}
.bar{width:100%;height:4px;background:#334155;border-radius:2px;overflow:hidden;margin-top:1rem}
.bar span{display:block;width:30%;height:100%;background:linear-gradient(90deg,#3b82f6,#8b5cf6);border-radius:2px;animation:load 1.5s ease-in-out infinite}
@keyframes load{0%{transform:translateX(-100%)}100%{transform:translateX(400%)}}
</style>
</head>
<body>
<div class="card">
<div class="icon">&#9881;</div>
<h1>Server is temporarily busy</h1>
<p>We are processing a large number of requests right now. This page will automatically refresh in 15 seconds.</p>
<div class="bar"><span></span></div>
<p class="hint">If this persists, try accessing <a href="/bos/" style="color:#60a5fa">/bos/</a> or <a href="/cp/" style="color:#60a5fa">/cp/</a> directly.</p>
</div>
</body>
</html>';
}
