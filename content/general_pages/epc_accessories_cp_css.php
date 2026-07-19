<?php
/**
 * Serve accessories CP CSS when nginx 404s /cp/templates paths.
 */
declare(strict_types=1);

$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$path = $root . '/cp/content/shop/accessories/epc_accessories_cp.css';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_accessories_cp.css missing';
	exit;
}

$ver = function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260719accCss1';
$mtime = (int) filemtime($path);
$etag = '"' . md5($mtime . '|' . filesize($path) . '|' . $ver) . '"';

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=86400');
header('ETag: ' . $etag);

if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
	http_response_code(304);
	exit;
}

readfile($path);
