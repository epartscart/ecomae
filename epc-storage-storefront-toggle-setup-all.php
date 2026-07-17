<?php
/**
 * Storefront storage toggle — schema on platform + all live tenant DBs.
 * Dry-run: https://www.ecomae.com/epc-storage-storefront-toggle-setup-all.php?token=epartscart-deploy-2026
 * Apply:    …&apply=1
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
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
require_once __DIR__ . '/content/shop/docpart/epc_storefront_storage_flags.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$onlyDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? '')));

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$targets = array();
$targets[] = array(
	'label' => 'current_config',
	'host' => function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? ''),
	'site_key' => 'platform',
	'cred' => array('db' => $cfg->db, 'user' => $cfg->user, 'pass' => $cfg->password),
);

$platformPdo = epc_portal_platform_pdo();
if ($platformPdo instanceof PDO && function_exists('epc_portal_list_tenants')) {
	epc_portal_db_ensure($platformPdo);
	foreach (epc_portal_list_tenants($platformPdo) as $row) {
		if ((string) ($row['status'] ?? '') !== 'live') {
			continue;
		}
		$cred = epc_portal_tenant_setup_credentials($row);
		if ($cred['db'] === '') {
			continue;
		}
		$targets[] = array(
			'label' => (string) ($row['site_key'] ?? $cred['db']),
			'host' => (string) ($row['hostname'] ?? ''),
			'site_key' => (string) ($row['site_key'] ?? $cred['db']),
			'cred' => array('db' => $cred['db'], 'user' => $cred['user'], 'pass' => $cred['pass']),
		);
	}
}

$seen = array();
$unique = array();
foreach ($targets as $t) {
	$db = (string) ($t['cred']['db'] ?? '');
	if ($db === '' || isset($seen[$db])) {
		continue;
	}
	if ($onlyDb !== '' && $db !== $onlyDb) {
		continue;
	}
	$seen[$db] = true;
	$unique[] = $t;
}

echo "=== EPC storefront storage toggle — all tenants ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'targets=' . count($unique) . "\n\n";

$ok = 0;
$fail = 0;

foreach ($unique as $t) {
	$db = (string) ($t['cred']['db'] ?? '');
	$label = (string) ($t['label'] ?? $db);
	echo "--- {$label} db={$db} ---\n";
	if (!$apply) {
		echo "dry-run skip\n\n";
		$ok++;
		continue;
	}
	$pdo = epc_auto_price_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "FAIL connect\n\n";
		$fail++;
		continue;
	}
	try {
		epc_ssf_ensure_schema($pdo, true);
		$storages = (int) $pdo->query('SELECT COUNT(*) FROM `shop_storages`')->fetchColumn();
		$prices = (int) $pdo->query('SELECT COUNT(*) FROM `shop_docpart_prices`')->fetchColumn();
		echo "OK storages={$storages} price_lists={$prices}\n\n";
		$ok++;
	} catch (Throwable $e) {
		echo 'FAIL ' . $e->getMessage() . "\n\n";
		$fail++;
	}
}

echo "=== done ok={$ok} fail={$fail} ===\n";
