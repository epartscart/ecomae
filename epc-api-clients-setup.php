<?php
/**
 * Create epc_api_clients table + sample Catalog / Price PRO keys.
 * Run once: https://www.ecomae.com/epc-api-clients-setup.php?token=epartscart-deploy-2026
 * Keys printed here ONLY — never embed in marketing HTML.
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_api_v1.php';
require_once __DIR__ . '/content/general_pages/epc_api_clients.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_api_clients_platform_pdo();
if (!$pdo instanceof PDO) {
	exit("Platform DB connect failed\n");
}

epc_api_clients_ensure_table($pdo);

function epc_acs_upsert(PDO $pdo, string $product, string $label, string $email, int $dailyLimit, array $actions): string
{
	$plain = epc_api_clients_make_key($product);
	$hash = hash('sha256', $plain);
	$prefix = substr($plain, 0, 24);
	$now = time();
	$actionsJson = $actions ? json_encode($actions, JSON_UNESCAPED_UNICODE) : '[]';

	$existing = $pdo->prepare('SELECT `id` FROM `epc_api_clients` WHERE `label` = ? LIMIT 1');
	$existing->execute(array($label));
	$id = (int) $existing->fetchColumn();

	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `epc_api_clients` SET `client_key_hash` = ?, `client_key_prefix` = ?, `product` = ?, `contact_email` = ?,
			 `active` = 1, `daily_limit` = ?, `allowed_actions_json` = ?, `time_updated` = ? WHERE `id` = ?'
		)->execute(array($hash, $prefix, $product, $email, $dailyLimit, $actionsJson, $now, $id));
	} else {
		$pdo->prepare(
			'INSERT INTO `epc_api_clients` (`client_key_hash`, `client_key_prefix`, `product`, `label`, `contact_email`,
			 `active`, `daily_limit`, `calls_today`, `calls_reset_date`, `allowed_actions_json`, `time_created`, `time_updated`)
			 VALUES (?, ?, ?, ?, ?, 1, ?, 0, CURDATE(), ?, ?, ?)'
		)->execute(array($hash, $prefix, $product, $label, $email, $dailyLimit, $actionsJson, $now, $now));
	}

	return $plain;
}

$catalogActions = array('manufacturers', 'models', 'modifications', 'categories', 'articles', 'vin', 'status');

$keys = array(
	'catalog_demo' => epc_acs_upsert($pdo, 'catalog', 'Sandbox Catalog API (demo)', 'api-demo@ecomae.com', 2000, $catalogActions),
	'price_demo' => epc_acs_upsert($pdo, 'price_pro', 'Sandbox Price PRO (demo)', 'api-demo@ecomae.com', 500, array()),
	'both_partner' => epc_acs_upsert($pdo, 'both', 'Partner bundle sample', 'partners@ecomae.com', 5000, array()),
);

echo "db: " . $cfg->db . "\n";
echo "table: epc_api_clients\n\n";
echo "=== API CLIENT KEYS (store securely — NOT for marketing) ===\n\n";
foreach ($keys as $name => $plain) {
	echo $name . ":\n  " . $plain . "\n\n";
}
echo "Catalog test:\n";
echo "  curl -s -H \"X-API-Key: YOUR_CATALOG_KEY\" \"https://www.ecomae.com/api/v1/catalog.php?action=manufacturers&section=passenger\"\n";
echo "Price PRO test:\n";
echo "  curl -s -H \"X-API-Key: YOUR_PRICEPRO_KEY\" \"https://www.ecomae.com/api/v1/price/lookup.php?brand=BOSCH&article=0986424590\"\n";
echo "Without key (expect 401):\n";
echo "  curl -s \"https://www.ecomae.com/api/v1/catalog.php?action=status\"\n";
echo "Done.\n";
