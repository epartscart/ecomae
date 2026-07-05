<?php
/**
 * Serve storefront static assets when nginx falls through to index.php (ecomae.com platform host).
 */
declare(strict_types=1);

function epc_static_serve_mime(string $path): string
{
	$ext = strtolower(pathinfo($path, PATHINFO_EXTENSION));
	$map = array(
		'css' => 'text/css; charset=utf-8',
		'js' => 'application/javascript; charset=utf-8',
		'png' => 'image/png',
		'jpg' => 'image/jpeg',
		'jpeg' => 'image/jpeg',
		'gif' => 'image/gif',
		'svg' => 'image/svg+xml',
		'webp' => 'image/webp',
		'woff' => 'font/woff',
		'woff2' => 'font/woff2',
		'ttf' => 'font/ttf',
		'ico' => 'image/x-icon',
		'mp4' => 'video/mp4',
		'webm' => 'video/webm',
		'map' => 'application/json',
		'webmanifest' => 'application/manifest+json',
	);
	return $map[$ext] ?? 'application/octet-stream';
}

function epc_static_serve_maybe_exit(): void
{
	if (PHP_SAPI === 'cli') {
		return;
	}
	$method = (string) ($_SERVER['REQUEST_METHOD'] ?? 'GET');
	if ($method !== 'GET' && $method !== 'HEAD') {
		return;
	}
	$path = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
	if (!is_string($path) || $path === '' || strpos($path, '..') !== false) {
		return;
	}
	if (!preg_match('#^/(templates|content|modules|lib|icons|favicon\.svg|favicon\.ico|manifest\.webmanifest|sw\.js)(/|$)#', $path)) {
		return;
	}
	$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
	if ($docRoot === '') {
		return;
	}
	$file = $docRoot . $path;
	if (!is_file($file) || !is_readable($file)) {
		return;
	}
	$size = filesize($file);
	header('Content-Type: ' . epc_static_serve_mime($file));
	header('Content-Length: ' . (string) $size);
	header('Cache-Control: public, max-age=604800');
	header('X-EPC-Static-Serve: 1');
	if ($method === 'HEAD') {
		exit;
	}
	readfile($file);
	exit;
}
