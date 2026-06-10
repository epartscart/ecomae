<?php
/**
 * Auto Price Engine — schema, CP route, tenant presets.
 * Run: https://{host}/epc-auto-price-setup.php?token=epartscart-deploy-2026&apply=1&seed=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$apply = !empty($_GET['apply']);
$seed = !empty($_GET['seed']);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$host = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? '');
$pdo = epc_auto_price_setup_connect(
	array('db' => $cfg->db, 'user' => $cfg->user, 'pass' => $cfg->password),
	$cfg
);
if (!$pdo instanceof PDO) {
	exit('DB connect failed for db=' . $cfg->db . "\n");
}

$siteKey = 'platform';
if (stripos($host, 'electronicae') !== false) {
	$siteKey = 'electronicae';
} elseif (stripos($host, 'epartscart') !== false) {
	$siteKey = 'epartscart';
}

echo "=== EPC Auto Price Engine Setup ===\n";
echo 'host: ' . $host . "\n";
echo 'db: ' . $cfg->db . "\n";
echo 'site_key: ' . $siteKey . "\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . ' seed=' . ($seed ? 'yes' : 'no') . "\n\n";

try {
	$result = epc_auto_price_cp_install($pdo, (string) $cfg->backend_dir, $apply, $seed, $siteKey, $host);
} catch (Throwable $e) {
	exit('Setup failed: ' . $e->getMessage() . "\n");
}

echo "Schema OK: epc_price_sources, epc_price_source_products, epc_price_compare_runs,\n";
echo "           epc_channel_listings, epc_auto_price_rules, epc_auto_price_tenant_config\n";

if ($seed && !empty($result['seeded'])) {
	$s = $result['seeded'];
	echo "\nSeed: config=" . ($s['config'] ? 'yes' : 'no') . " sources={$s['sources']} demo_product=" . ($s['demo_product'] ? 'yes' : 'no') . "\n";
}

if ($apply) {
	echo "\nMenu item id: {$result['menu_item_id']}\n";
	echo "Content id: {$result['content_id']}\n";
	echo "Super CP URL: /{$cfg->backend_dir}/control/portal/epc_auto_price_engine\n";
	echo "Cron URL: /epc-auto-price-run.php?token=…&site_key={$siteKey}\n";
}

echo "\nDone.\n";
