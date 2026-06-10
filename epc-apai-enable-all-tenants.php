<?php
/**
 * Ensure auto_price_ai feature flag enabled for all live tenants.
 * GET ?token=…&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_integrations_helpers.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	exit("Platform DB unavailable\n");
}

epc_portal_db_ensure($platformPdo);
epc_integrations_ensure_schema($platformPdo);

echo "=== Enable auto_price_ai for all live tenants ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

$flags = array('auto_price_ai' => true, 'parts_agent' => true);
$ok = 0;

foreach (epc_portal_list_tenants($platformPdo) as $row) {
	if ((string) ($row['status'] ?? '') !== 'live') {
		continue;
	}
	$siteKey = (string) ($row['site_key'] ?? '');
	$before = epc_integrations_feature_enabled('auto_price_ai', $siteKey, $platformPdo);
	echo "{$siteKey}: before=" . ($before ? 'enabled' : 'disabled');
	if ($apply) {
		$res = epc_integrations_save_feature_flags($platformPdo, $siteKey, $flags);
		$after = epc_integrations_feature_enabled('auto_price_ai', $siteKey, $platformPdo);
		echo " saved={$res['saved']} after=" . ($after ? 'enabled' : 'disabled');
	}
	echo "\n";
	$ok++;
}

if ($apply && function_exists('epc_perf_cache_bust')) {
	epc_perf_cache_bust('epc_int_features');
	echo "\nFeature cache busted.\n";
}

echo "\nTenants updated: {$ok}\nDone.\n";
