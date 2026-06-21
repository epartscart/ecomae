<?php
/**
 * Serve POS Terminal JS (nginx may not expose new files under /cp/content/...).
 */
declare(strict_types=1);

$path = __DIR__ . '/epc_pos_terminal.js';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_pos_terminal.js missing';
	exit;
}

$mtime = (int) filemtime($path);
$etag = '"' . md5($mtime . '|' . filesize($path)) . '"';

header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: public, max-age=86400');
header('ETag: ' . $etag);

if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
	http_response_code(304);
	exit;
}

readfile($path);
