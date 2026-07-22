<?php
/**
 * Marketing Broadcast JS loader — nginx-safe /content/ path.
 */
declare(strict_types=1);

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$backend = 'cp';
$configFile = $docRoot . '/config.php';
if ($docRoot !== '' && is_file($configFile)) {
	require_once $configFile;
	$cfg = new DP_Config();
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}
}
$path = $docRoot . '/' . $backend . '/content/control/portal/epc_marketing_broadcast.js';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_marketing_broadcast.js missing';
	exit;
}

$ver = '20260722mb2';
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
