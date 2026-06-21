<?php
/**
 * Auto Price AI shell JS — served from /content/ (nginx-safe proxy).
 */
declare(strict_types=1);

$path = dirname(__DIR__, 2) . '/cp/content/control/portal/epc_auto_price_shell.js';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_auto_price_shell.js missing';
	exit;
}

$ver = '20260606apai2';
$mtime = (int) filemtime($path);
$etag = '"' . md5($mtime . '|' . filesize($path) . '|' . $ver) . '"';

header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: public, max-age=86400');
header('ETag: ' . $etag);

if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
	http_response_code(304);
	exit;
}

readfile($path);
