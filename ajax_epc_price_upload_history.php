<?php
/**
 * Legacy root stub — do not use.
 * Canonical endpoint:
 *   /cp/content/shop/prices_upload/ajax_epc_price_upload_history.php
 */
header('Content-Type: application/json; charset=utf-8');
http_response_code(410);
exit(json_encode([
	'status' => false,
	'message' => 'Moved. Use /cp/content/shop/prices_upload/ajax_epc_price_upload_history.php',
]));
