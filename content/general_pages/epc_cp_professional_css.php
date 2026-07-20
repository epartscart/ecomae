<?php
/**
 * Serve CP professional CSS when nginx 404s /cp/templates/bootstrap_admin/css/*.css on ecomae.
 */
declare(strict_types=1);

$root = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$path = $root . '/cp/templates/bootstrap_admin/css/epc_cp_professional.css';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_cp_professional.css missing';
	exit;
}

$ver = '20260720topnavclick2';
$mtime = (int) filemtime($path);
$etag = '"' . md5($mtime . '|' . filesize($path) . '|' . $ver) . '"';

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=604800, immutable');
header('ETag: ' . $etag);

if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
	http_response_code(304);
	exit;
}

readfile($path);
