<?php
/**
 * BOC / Super CP console CSS — no _ASTEXE_ (direct <link> from CP head).
 */
declare(strict_types=1);

$path = dirname(__DIR__, 2) . '/cp/templates/bootstrap_admin/css/epc_boc_console.css';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_boc_console.css missing';
	exit;
}

$ver = '20260722tenant1';
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
