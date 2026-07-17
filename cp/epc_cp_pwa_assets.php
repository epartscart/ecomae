<?php
/**
 * Serve CP PWA static assets early (manifest, service worker, offline shell,
 * icons) before the heavy CP bootstrap. CP routes normally funnel through
 * index.php, so these physical files under /cp/ would otherwise 404 or be
 * intercepted. Kept dependency-free and fast (readfile + exit).
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_pwa_maybe_serve_asset(): void
{
	$uri = isset($_SERVER['REQUEST_URI']) ? (string) $_SERVER['REQUEST_URI'] : '';
	$path = parse_url($uri, PHP_URL_PATH);
	if (!is_string($path) || $path === '') {
		return;
	}

	// Map public /cp/... path → physical file + content type.
	static $map = array(
		'/cp/manifest.webmanifest' => array('manifest.webmanifest', 'application/manifest+json; charset=utf-8'),
		'/cp/sw.js' => array('sw.js', 'application/javascript; charset=utf-8'),
		'/cp/offline.html' => array('offline.html', 'text/html; charset=utf-8'),
		'/cp/assets/app/icon-192.svg' => array('assets/app/icon-192.svg', 'image/svg+xml'),
		'/cp/assets/app/icon-512.svg' => array('assets/app/icon-512.svg', 'image/svg+xml'),
	);

	if (!isset($map[$path])) {
		return;
	}

	$rel = $map[$path][0];
	$ctype = $map[$path][1];
	$file = __DIR__ . '/' . $rel;
	if (!is_file($file)) {
		return;
	}

	if (!headers_sent()) {
		header('Content-Type: ' . $ctype);
		header('Cache-Control: public, max-age=3600');
		// Service workers must be served from their own scope with no-cache
		// during rollout so updates propagate.
		if ($path === '/cp/sw.js') {
			header('Service-Worker-Allowed: /cp/');
			header('Cache-Control: no-cache');
		}
	}
	readfile($file);
	exit;
}
