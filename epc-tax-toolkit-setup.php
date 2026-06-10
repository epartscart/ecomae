<?php
/**
 * Tax Toolkit — schema, seed kits, Super CP route, customer migration (current tenant DB).
 * Run on tenant host: https://{tenant-host}/epc-tax-toolkit-setup.php?token=epartscart-deploy-2026&apply=1&migrate=1
 * Batch all DBs:     https://www.ecomae.com/epc-tax-toolkit-setup-all.php?token=…&apply=1&migrate=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$apply = !empty($_GET['apply']);
$migrate = !empty($_GET['migrate']);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/finance/epc_tax_toolkit_cp_install.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$host = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? '');
$pdo = epc_tax_toolkit_setup_connect(
	array('db' => $cfg->db, 'user' => $cfg->user, 'pass' => $cfg->password),
	$cfg
);
if (!$pdo instanceof PDO) {
	exit('DB connect failed for db=' . $cfg->db . "\n");
}

echo "=== EPC Tax Toolkit Setup ===\n";
echo 'host: ' . $host . "\n";
echo 'db: ' . $cfg->db . "\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . ' migrate=' . ($migrate ? 'yes' : 'no') . "\n\n";

try {
	$result = epc_tax_toolkit_cp_install($pdo, (string) $cfg->backend_dir, $apply, $migrate);
} catch (Throwable $e) {
	exit('Setup failed: ' . $e->getMessage() . "\n");
}

echo "Schema OK: epc_tax_toolkits, epc_tax_toolkit_installs, epc_customer_tax_profile\n";
echo "Kits seeded/updated: {$result['seeded']}\n";
foreach ($result['kit_codes'] as $code) {
	echo "  - {$code}\n";
}

if ($apply) {
	echo "\nInstalled kits: {$result['installed']} (AE-UAE-VAT = tenant default)\n";
	echo "Menu item id: {$result['menu_item_id']}\n";
	echo "Content id: {$result['content_id']}\n";
	echo "Super CP URL: /{$cfg->backend_dir}/control/portal/epc_tax_toolkit_manage\n";
}

if ($migrate) {
	$m = $result['migration'];
	echo "\nMigration: users={$m['users']} contacts={$m['contacts']} skipped={$m['skipped']}\n";
	if (!empty($m['errors'])) {
		echo 'Errors: ' . count($m['errors']) . "\n";
		foreach (array_slice($m['errors'], 0, 5) as $err) {
			echo "  - {$err}\n";
		}
	}
	echo "Profiles total: {$result['profiles']}\n";
}

echo "\nDone.\n";
