<?php
/**
 * Marketing Broadcast AJAX — nginx-safe /content/ proxy.
 */
declare(strict_types=1);

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
if ($docRoot === '') {
	header('Content-Type: application/json; charset=utf-8');
	http_response_code(500);
	echo json_encode(array('ok' => false, 'message' => 'DOCUMENT_ROOT missing'));
	exit;
}

require_once $docRoot . '/config.php';
$cfg = new DP_Config();
$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}

$handler = $docRoot . '/' . $backend . '/content/control/portal/ajax_marketing_broadcast.php';
if (!is_file($handler)) {
	header('Content-Type: application/json; charset=utf-8');
	http_response_code(404);
	echo json_encode(array('ok' => false, 'message' => 'AJAX handler missing'));
	exit;
}

define('_ASTEXE_', 1);
require $handler;