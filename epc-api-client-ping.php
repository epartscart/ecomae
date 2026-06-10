<?php
/**
 * Debug ping for external API client stack.
 * https://www.ecomae.com/epc-api-client-ping.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}
define('_ASTEXE_', 1);
echo "php=" . PHP_VERSION . "\n";
try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_api_clients.php';
	$pdo = epc_api_clients_platform_pdo();
	echo 'pdo=' . ($pdo instanceof PDO ? 'ok' : 'fail') . "\n";
	epc_api_clients_ensure_table($pdo);
	echo "table=ok\n";
	echo "key_extract=" . (epc_api_clients_extract_key() === '' ? 'empty' : 'set') . "\n";
} catch (Throwable $e) {
	echo 'error=' . $e->getMessage() . "\n";
}
echo "done\n";
