<?php
declare(strict_types=1);
$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$backend = 'cp';
$configFile = $docRoot . '/config.php';
if ($docRoot !== '' && is_file($configFile)) {
	require_once $configFile;
	$cfg = new DP_Config();
	$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
	if ($backend === '') $backend = 'cp';
}
$path = $docRoot . '/' . $backend . '/content/shop/payments/epc_payments_cp.js';
if (!is_file($path)) {
	http_response_code(404);
	header('Content-Type: text/plain; charset=utf-8');
	echo 'epc_payments_cp.js missing';
	exit;
}
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: public, max-age=86400');
readfile($path);
