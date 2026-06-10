<?php
/**
 * Serve POS tenant settings JS (Super CP overview page).
 */
declare(strict_types=1);

$path = __DIR__ . '/epc_pos_tenant_manage.js';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_pos_tenant_manage.js missing';
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
