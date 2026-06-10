<?php
/**
 * ECOM AE public API v1 — entry router.
 * Routes: /epc-api/v1/* (via index.php early exit or direct if rewritten).
 */
declare(strict_types=1);

define('_ASTEXE_', 1);

try {
	require_once __DIR__ . '/content/general_pages/epc_api_v1.php';
	epc_api_v1_dispatch();
} catch (Throwable $e) {
	if (!headers_sent()) {
		http_response_code(500);
		header('Content-Type: application/json; charset=utf-8');
	}
	echo json_encode(array(
		'ok' => false,
		'error' => array('code' => 'internal_error', 'message' => 'API request failed.'),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
}
