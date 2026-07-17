<?php
/**
 * Migrate storefront temp-disable columns + audit table (current / one tenant DB).
 * GET https://www.ecomae.com/epc-storage-storefront-toggle-setup.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/docpart/epc_storefront_storage_flags.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$cfg = new DP_Config();

echo "=== EPC storefront storage toggle — schema ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'db=' . $cfg->db . "\n\n";

if (!$apply) {
	echo "Dry-run only. Add &apply=1 to migrate.\n";
	exit;
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	echo 'FAIL connect: ' . $e->getMessage() . "\n";
	exit(1);
}

epc_ssf_ensure_schema($pdo, true);

$storageCol = (int) $pdo->query(
	"SELECT COUNT(*) FROM information_schema.COLUMNS
	 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shop_storages' AND COLUMN_NAME = 'storefront_temp_disabled'"
)->fetchColumn();
$priceCol = (int) $pdo->query(
	"SELECT COUNT(*) FROM information_schema.COLUMNS
	 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shop_docpart_prices' AND COLUMN_NAME = 'storefront_temp_disabled'"
)->fetchColumn();
$auditTable = (int) $pdo->query(
	"SELECT COUNT(*) FROM information_schema.TABLES
	 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_storefront_storage_toggle_audit'"
)->fetchColumn();

echo "shop_storages.storefront_temp_disabled=" . ($storageCol ? 'yes' : 'no') . "\n";
echo "shop_docpart_prices.storefront_temp_disabled=" . ($priceCol ? 'yes' : 'no') . "\n";
echo "epc_storefront_storage_toggle_audit=" . ($auditTable ? 'yes' : 'no') . "\n";
echo "storages=" . (int) $pdo->query('SELECT COUNT(*) FROM `shop_storages`')->fetchColumn() . "\n";
echo "price_lists=" . (int) $pdo->query('SELECT COUNT(*) FROM `shop_docpart_prices`')->fetchColumn() . "\n";
echo "OK\n";
