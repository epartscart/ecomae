<?php
/**
 * Server overload guard — DISABLED for storefront requests.
 *
 * Previously this checked load average and served a 503 page, but it caused
 * more problems than it solved: false positives during normal cache-warming,
 * redirect loops in the splash page, and user confusion.
 *
 * The page cache + thundering-herd locks in brand helpers now handle load
 * management properly. This guard is kept only as an extreme safety net
 * for genuinely catastrophic load (20x CPU cores, e.g. DDoS).
 *
 * Bypasses (always allowed through):
 *  - CLI (cron, deploy scripts)
 *  - BOS requests (/bos/)
 *  - Cache purge / deploy endpoints (epc-*)
 *  - AJAX requests to CP
 *  - CP/ERP paths
 *  - Storefront requests (handled by page cache instead)
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

	// Never block BOS, deploy scripts, API, health checks, CP, ERP
	if (preg_match('#^/(bos|epc-|api/|epc_static|cp|erp)#i', $path)) {
		return false;
	}

	// DISABLED for storefront — page cache handles it. Only trigger on DDoS-level load.
	if (!is_readable('/proc/loadavg')) {
		return false;
	}
	$load_str = @file_get_contents('/proc/loadavg');
	if ($load_str === false) {
		return false;
	}
	$parts = explode(' ', $load_str);
	$load_1min = (float) ($parts[0] ?? 0);

	// Extreme threshold: 20x CPU cores. Only triggers during genuine DDoS or runaway processes.
	// Normal cold-cache renders should NOT trigger this.
	$cores = 1;
	if (is_readable('/proc/cpuinfo')) {
		$cpuinfo = @file_get_contents('/proc/cpuinfo');
		if ($cpuinfo !== false) {
			$cores = max(1, substr_count($cpuinfo, 'processor'));
		}
	}
	$threshold = $cores * 20;

	if ($load_1min < $threshold) {
		return false;
	}

	// Server is genuinely overloaded — serve a minimal auto-refresh page
	http_response_code(503);
	header('Retry-After: 15');
	header('Content-Type: text/html; charset=UTF-8');
	header('Cache-Control: no-store, no-cache');
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
<title>Loading...</title>
<meta http-equiv="refresh" content="10">
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;background:#0f172a;color:#e2e8f0;display:flex;align-items:center;justify-content:center;min-height:100vh;padding:2rem}
.card{text-align:center;max-width:420px;background:#1e293b;border-radius:16px;padding:3rem 2rem;box-shadow:0 25px 50px rgba(0,0,0,.3)}
h1{font-size:1.4rem;margin-bottom:.75rem;color:#f1f5f9}
p{color:#94a3b8;line-height:1.6;margin:0}
.bar{width:100%;height:4px;background:#334155;border-radius:2px;overflow:hidden;margin:1.5rem 0}
.bar span{display:block;width:30%;height:100%;background:linear-gradient(90deg,#3b82f6,#8b5cf6);border-radius:2px;animation:load 1.5s ease-in-out infinite}
@keyframes load{0%{transform:translateX(-100%)}100%{transform:translateX(400%)}}
.hint{font-size:.8rem;color:#64748b;margin-top:1rem}
</style>
</head>
<body>
<div class="card">
<h1>Loading your store...</h1>
<p>The page is warming up. It will reload automatically.</p>
<div class="bar"><span></span></div>
<p class="hint">This only happens briefly after a server update.</p>
</div>
</body>
</html>';
}
