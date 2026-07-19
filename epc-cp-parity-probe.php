<?php
/**
 * Retired probe — use epc-cp-common-parity-setup.php instead.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
echo json_encode(array(
	'ok' => false,
	'retired' => true,
	'use' => 'epc-cp-common-parity-setup.php?token=…',
), JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
