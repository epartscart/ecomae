<?php
/**
 * Probe: marketplace arbitrage scan for a tenant.
 * HTTP: ?token=…&site_key=electronicae&quick=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));
if ($siteKey === '') {
	$siteKey = 'electronicae';
}

try {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_apai_marketplace_channels.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);

	$platformPdo = epc_portal_platform_pdo();
	if (!$platformPdo instanceof PDO) {
		throw new RuntimeException('Platform registry unavailable');
	}
	epc_portal_db_ensure($platformPdo);

	$row = null;
	foreach (epc_portal_list_tenants($platformPdo) as $t) {
		if ((string) ($t['site_key'] ?? '') === $siteKey) {
			$row = $t;
			break;
		}
	}
	if (!$row) {
		http_response_code(404);
		echo "tenant_not_found: {$siteKey}\n";
		exit;
	}

	$pdo = epc_auto_price_setup_connect(array(
		'db' => (string) ($row['db_name'] ?? ''),
		'user' => (string) ($row['db_user'] ?? ''),
		'pass' => (string) ($row['db_password'] ?? ''),
	), $cfg);
	if (!$pdo instanceof PDO) {
		throw new RuntimeException('Tenant database unavailable for ' . $siteKey);
	}
	epc_ape_ensure_schema($pdo);

	$fnOk = function_exists('epc_disc_marketplace_arbitrage_scan')
		&& function_exists('epc_apai_marketplace_arbitrage_enabled')
		&& function_exists('epc_disc_marketplace_gaps_matrix');

	$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);
	echo "Tenant: {$siteKey}\n";
	echo 'Functions: ' . ($fnOk ? 'ok' : 'missing') . "\n";
	echo 'Arbitrage enabled: ' . (epc_apai_marketplace_arbitrage_enabled($pdo, $siteKey) ? 'yes' : 'no') . "\n";
	echo 'Sell: ' . implode(', ', (array) ($channels['sell_domains'] ?? array())) . "\n";
	echo 'Buy: ' . implode(', ', (array) ($channels['buy'] ?? array())) . "\n";
	echo 'Primary: ' . (string) ($channels['primary_label'] ?? '') . "\n";
	echo 'Default discover view: ' . epc_disc_default_discover_view($pdo, $siteKey) . "\n";

	$quick = !empty($_GET['quick']);
	$scan = epc_disc_marketplace_arbitrage_scan($pdo, $siteKey, array('check_presence' => !$quick, 'limit' => $quick ? 15 : 40));
	echo "\nScan: " . ($scan['message'] ?? '') . "\n";

	$gaps = epc_disc_marketplace_gaps_matrix($pdo, $siteKey, array('limit' => 10));
	echo 'Gaps matrix rows: ' . count($gaps) . "\n";
	foreach (array_slice($gaps, 0, 5) as $g) {
		echo '  - ' . ($g['title'] ?? '') . ' | buy ' . ($g['buy_price'] ?? 0) . ' | margin ' . ($g['margin_pct'] ?? 0) . "%\n";
	}

	$counts = epc_disc_discover_counts($pdo, $siteKey);
	echo "\nDiscover counts: marketplace_opportunities=" . (int) ($counts['marketplace_opportunities'] ?? 0)
		. ' all=' . (int) ($counts['all_suggestions'] ?? 0) . "\n";
} catch (Throwable $e) {
	http_response_code(500);
	echo 'error: ' . $e->getMessage() . "\n";
}
