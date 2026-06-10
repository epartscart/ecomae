<?php
/**
 * ERP AJAX proxy — nginx-safe /content/ path for client-erp + platform-erp shells.
 * Forwards to cp/content/shop/finance/erp/ajax_erp_endpoint.php (tenant bootstrap via Referer).
 */
declare(strict_types=1);

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
if ($docRoot === '') {
	header('Content-Type: application/json; charset=utf-8');
	http_response_code(500);
	echo json_encode(array('status' => false, 'message' => 'DOCUMENT_ROOT missing'));
	exit;
}

require_once $docRoot . '/config.php';
$cfg = new DP_Config();
$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}

$handler = $docRoot . '/' . $backend . '/content/shop/finance/erp/ajax_erp_endpoint.php';
if (!is_file($handler)) {
	header('Content-Type: application/json; charset=utf-8');
	http_response_code(404);
	echo json_encode(array('status' => false, 'message' => 'ERP AJAX handler missing'));
	exit;
}

require $handler;
