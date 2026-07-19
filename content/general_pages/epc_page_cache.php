<?php
/**
 * Full-page output cache for anonymous storefront requests.
 *
 * Wraps ob_start() early in the request lifecycle and stores the rendered
 * HTML for anonymous visitors.  Subsequent requests serve the cached page
 * directly, bypassing the full PHP/MySQL rendering pipeline.
 *
 * Cache keys include: hostname, URI path, language cookie, and guest status.
 * Logged-in users always get a fresh (non-cached) page.
 */
defined('_ASTEXE_') or die('No access');

function epc_page_cache_dir(): string
{
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_page_cache';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	return $dir;
}

function epc_page_cache_enabled(): bool
{
	if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
		return false;
	}
	if (!empty($_POST)) {
		return false;
	}
	// Skip for logged-in users (session cookie present)
	if (!empty($_COOKIE['PHPSESSID']) || !empty($_COOKIE['admin_hash']) || !empty($_COOKIE['epc_user_id'])) {
		return false;
	}
	// Skip for CP/BOS/API paths
	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (!is_string($path)) {
		return false;
	}
	if (preg_match('#^/(cp|bos|api|epc-api|admin)(/|$)#i', $path)) {
		return false;
	}
	// Brochures are standalone HTML (early exit); never cache CMS 404 shells for them.
	if (preg_match('#^/(?:(?:en|ru|ar)/)?brochure(?:-cp|/cp)?/?$#i', $path)) {
		return false;
	}
	// Skip if nocache param
	if (isset($_GET['nocache']) || isset($_GET['epc_debug'])) {
		return false;
	}
	return true;
}

function epc_page_cache_key(): string
{
	$host = strtolower(preg_replace('/[^a-z0-9.\-]/', '', $_SERVER['HTTP_HOST'] ?? 'localhost'));
	$uri  = preg_replace('/[^a-zA-Z0-9\/\-_.]/', '_', parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH));
	$lang = preg_replace('/[^a-z]/', '', strtolower($_COOKIE['lang'] ?? 'en'));
	return 'page_' . $host . '_' . md5($uri . '|' . $lang);
}

/**
 * Try to serve a cached page. Call early in index.php, before dp_core.
 * Returns true if cache was served (caller should exit).
 */
function epc_page_cache_try_serve(): bool
{
	if (!epc_page_cache_enabled()) {
		return false;
	}
	$file = epc_page_cache_dir() . '/' . epc_page_cache_key() . '.html';
	if (!is_file($file)) {
		return false;
	}
	$meta = @file_get_contents($file . '.meta');
	if ($meta !== false) {
		$m = json_decode($meta, true);
		if (is_array($m) && isset($m['exp']) && (int) $m['exp'] < time()) {
			@unlink($file);
			@unlink($file . '.meta');
			return false;
		}
	}
	$html = @file_get_contents($file);
	if ($html === false || strlen($html) < 100) {
		return false;
	}
	// Reject stale partial renders that slipped through before the
	// completeness check was added to epc_page_cache_flush().
	if (stripos(trim($html), '<!doctype') !== 0 && stripos($html, '<html') === false) {
		@unlink($file);
		@unlink($file . '.meta');
		return false;
	}
	// Reject cached splash/error pages (nginx error_page may have served these as 200)
	if (stripos($html, 'epc-platform-status') !== false && stripos($html, 'Service update') !== false) {
		@unlink($file);
		@unlink($file . '.meta');
		return false;
	}
	header('Content-Type: text/html; charset=UTF-8');
	header('X-EPC-Cache: HIT');
	if (function_exists('ob_gzhandler') && !ini_get('zlib.output_compression') && strpos($_SERVER['HTTP_ACCEPT_ENCODING'] ?? '', 'gzip') !== false) {
		ob_start('ob_gzhandler');
	}
	echo $html;
	return true;
}

/**
 * Start output buffering so the rendered page can be cached on shutdown.
 * Call after confirming the request is cacheable but before rendering.
 *
 * Thundering-herd protection: acquires a file lock so only ONE process
 * renders a given page at a time. Others wait for the lock, then check
 * if the cache file was populated meanwhile.
 */
function epc_page_cache_start_capture(int $ttl = 300): void
{
	if (!epc_page_cache_enabled()) {
		return;
	}

	// Thundering-herd lock: only one process renders a given page
	$lockFile = epc_page_cache_dir() . '/' . epc_page_cache_key() . '.lock';
	$lockFp = @fopen($lockFile, 'c');
	if ($lockFp) {
		if (!flock($lockFp, LOCK_EX | LOCK_NB)) {
			// Another process is rendering this page — wait for it
			flock($lockFp, LOCK_EX);
			fclose($lockFp);
			// Check if cache is now populated
			if (epc_page_cache_try_serve()) {
				exit;
			}
			// Not populated — we'll render it ourselves (re-acquire lock)
			$lockFp = @fopen($lockFile, 'c');
			if ($lockFp) { flock($lockFp, LOCK_EX); }
		}
		$GLOBALS['__epc_page_cache_lock_fp'] = $lockFp;
	}

	$GLOBALS['__epc_page_cache_ttl'] = $ttl;
	$GLOBALS['__epc_page_cache_active'] = true;
	ob_start();
	register_shutdown_function('epc_page_cache_flush');
}

/** Release the thundering-herd page cache lock. */
function epc_page_cache_release_lock(): void
{
	if (!empty($GLOBALS['__epc_page_cache_lock_fp'])) {
		$fp = $GLOBALS['__epc_page_cache_lock_fp'];
		flock($fp, LOCK_UN);
		fclose($fp);
		unset($GLOBALS['__epc_page_cache_lock_fp']);
	}
}

/** Shutdown function: write the captured output to file cache. */
function epc_page_cache_flush(): void
{
	if (empty($GLOBALS['__epc_page_cache_active'])) {
		epc_page_cache_release_lock();
		return;
	}
	$GLOBALS['__epc_page_cache_active'] = false;
	$html = ob_get_contents();
	if ($html === false || strlen($html) < 200) {
		epc_page_cache_release_lock();
		return;
	}
	$code = http_response_code();
	if ($code !== 200 && $code !== false) {
		epc_page_cache_release_lock();
		return;
	}
	// Only cache complete HTML pages — never cache partial renders from
	// timeouts or fatal errors.  A valid page must open with a doctype or
	// <html tag and close with </html>.
	$trimmed = trim($html);
	$hasHtmlOpen = (stripos($trimmed, '<!doctype') === 0 || stripos($trimmed, '<html') !== false);
	$hasHtmlClose = (stripos($trimmed, '</html>') !== false);
	if (!$hasHtmlOpen || !$hasHtmlClose) {
		epc_page_cache_release_lock();
		return;
	}
	// Never cache error/splash pages that slipped through with HTTP 200
	if (stripos($html, 'Service update') !== false && stripos($html, 'epc-platform-status') !== false) {
		epc_page_cache_release_lock();
		return;
	}
	if (stripos($html, 'Temporarily Busy') !== false && stripos($html, 'Retry-After') !== false) {
		epc_page_cache_release_lock();
		return;
	}
	// Also reject "Loading your store" splash pages
	if (stripos($html, 'Loading your store') !== false && strlen($html) < 2000) {
		epc_page_cache_release_lock();
		return;
	}
	$ttl = isset($GLOBALS['__epc_page_cache_ttl']) ? (int) $GLOBALS['__epc_page_cache_ttl'] : 300;
	$file = epc_page_cache_dir() . '/' . epc_page_cache_key() . '.html';
	@file_put_contents($file, $html, LOCK_EX);
	@file_put_contents($file . '.meta', json_encode(array(
		'exp' => time() + $ttl,
		'uri' => $_SERVER['REQUEST_URI'] ?? '/',
		'host' => $_SERVER['HTTP_HOST'] ?? '',
		'time' => date('Y-m-d H:i:s'),
	)), LOCK_EX);

	epc_page_cache_release_lock();
}

/** Purge all page cache files (call after deploy or content changes). */
function epc_page_cache_purge_all(): int
{
	$dir = epc_page_cache_dir();
	if (!is_dir($dir)) {
		return 0;
	}
	$count = 0;
	foreach (glob($dir . '/*') ?: array() as $f) {
		if (@unlink($f)) {
			$count++;
		}
	}
	return $count;
}
