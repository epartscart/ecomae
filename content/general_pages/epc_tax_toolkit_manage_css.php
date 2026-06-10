<?php
/**
 * Serve Tax Toolkit manage CSS (nginx may 404 new files under /cp/content/…).
 */
declare(strict_types=1);

$path = dirname(__DIR__, 2) . '/cp/content/control/portal/epc_tax_toolkit_manage.css';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_tax_toolkit_manage.css missing';
	exit;
}

$mtime = (int) filemtime($path);
$etag = '"' . md5($mtime . '|' . filesize($path)) . '"';

header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=604800, immutable');
header('ETag: ' . $etag);

if (isset($_SERVER['HTTP_IF_NONE_MATCH']) && trim((string) $_SERVER['HTTP_IF_NONE_MATCH']) === $etag) {
	http_response_code(304);
	exit;
}

readfile($path);
