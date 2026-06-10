<?php
/**
 * Combined setup: Procurement + Customer management.
 */
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

header('Content-Type: application/json; charset=utf-8');

ob_start();
include __DIR__ . '/epc-procurement-setup.php';
$proc = json_decode(ob_get_clean(), true);

ob_start();
include __DIR__ . '/epc-customer-mgmt-cp-setup.php';
$cm = json_decode(ob_get_clean(), true);

echo json_encode(array(
	'status' => !empty($proc['status']) && !empty($cm['status']),
	'procurement' => $proc,
	'customer_mgmt' => $cm,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
