<?php
/**
 * Tax Toolkit — register schema, kits, CP route, and migrate customers on every live tenant DB.
 *
 * Dry-run:  https://www.ecomae.com/epc-tax-toolkit-setup-all.php?token=epartscart-deploy-2026
 * Apply:    …&apply=1&migrate=1
 * One DB:   …&apply=1&migrate=1&db=docpart
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
require_once __DIR__ . '/content/shop/finance/epc_tax_toolkit_cp_install.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$migrate = !empty($_GET['migrate']) && (string) $_GET['migrate'] === '1';
$onlyDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? '')));

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = trim((string) $cfg->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$targets = array();
$targets[] = array(
	'label' => 'current_config',
	'host' => function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? ''),
	'cred' => array('db' => $cfg->db, 'user' => $cfg->user, 'pass' => $cfg->password),
);

$platformPdo = epc_portal_platform_pdo();
if ($platformPdo instanceof PDO && function_exists('epc_portal_list_tenants')) {
	epc_portal_db_ensure($platformPdo);
	foreach (epc_portal_list_tenants($platformPdo) as $row) {
		if ((string) ($row['status'] ?? '') !== 'live') {
			continue;
		}
		$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['db_name'] ?? '')));
		if ($dbName === '') {
			continue;
		}
		$targets[] = array(
			'label' => (string) ($row['site_key'] ?? $dbName),
			'host' => (string) ($row['hostname'] ?? ''),
			'cred' => array(
				'db' => $dbName,
				'user' => (string) ($row['db_user'] ?? ''),
				'pass' => (string) ($row['db_password'] ?? ''),
			),
		);
	}
}

$seenDb = array();
$unique = array();
foreach ($targets as $t) {
	$db = (string) ($t['cred']['db'] ?? '');
	if ($db === '' || isset($seenDb[$db])) {
		continue;
	}
	if ($onlyDb !== '' && $db !== $onlyDb) {
		continue;
	}
	$seenDb[$db] = true;
	$unique[] = $t;
}

echo '=== EPC Tax Toolkit — all tenants ===' . "\n";
echo 'mode=' . ($apply ? 'apply' : 'dry-run') . ' migrate=' . ($migrate ? 'yes' : 'no') . "\n";
echo 'unique_dbs=' . count($unique) . "\n\n";

$summary = array();

foreach ($unique as $t) {
	$label = (string) $t['label'];
	$db = (string) $t['cred']['db'];
	$host = (string) $t['host'];
	echo "=== {$label} (db={$db}) ===\n";
	if ($host !== '') {
		echo "  host={$host}\n";
		echo "  setup_url=https://{$host}/epc-tax-toolkit-setup.php?token=" . epc_deploy_token() . "&apply=1&migrate=1\n";
	}
	$pdo = epc_tax_toolkit_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "  ERROR: cannot connect\n\n";
		$summary[] = array('label' => $label, 'host' => $host, 'db' => $db, 'ok' => false, 'error' => 'connect failed');
		continue;
	}
	if (!$apply) {
		$counts = epc_tax_toolkit_profile_counts($pdo);
		echo '  installed_kits=' . (int) ($counts['installed'] ?? 0) . ' profiles=' . (int) ($counts['profiles'] ?? 0) . "\n";
		echo "  (dry-run)\n\n";
		continue;
	}
	try {
		$result = epc_tax_toolkit_cp_install($pdo, $backend, true, $migrate);
		$m = $result['migration'];
		echo '  OK kits_seeded=' . $result['seeded'] . ' kits_installed=' . $result['installed'] . "\n";
		echo '  content_id=' . $result['content_id'] . ' menu_item_id=' . $result['menu_item_id'] . "\n";
		if ($migrate) {
			$tenant = $result['tenant'] ?? $m;
			echo '  migration tenant=' . ($tenant['kit_code'] ?? '?') . ' country=' . ($tenant['country_code'] ?? '?') . "\n";
			echo '  tenant_profiles=' . $result['profiles'] . "\n";
		}
		$summary[] = array(
			'label' => $label,
			'host' => $host,
			'db' => $db,
			'ok' => true,
			'kit' => (string) (($result['tenant']['kit_code'] ?? '') ?: ''),
			'country' => (string) (($result['tenant']['country_code'] ?? '') ?: ''),
			'catalog' => (int) ($result['seeded'] ?? 0),
			'profiles' => (int) $result['profiles'],
		);
	} catch (Throwable $e) {
		echo '  FAIL: ' . $e->getMessage() . "\n";
		$summary[] = array('label' => $label, 'host' => $host, 'db' => $db, 'ok' => false, 'error' => $e->getMessage());
	}
	echo "\n";
}

echo "--- summary ---\n";
foreach ($summary as $row) {
	if (!empty($row['ok'])) {
		echo $row['host'] . ' [' . $row['db'] . '] kit=' . ($row['kit'] ?? '') . ' country=' . ($row['country'] ?? '') . ' catalog=' . ($row['catalog'] ?? 0) . "\n";
	} else {
		echo ($row['host'] ?: $row['label']) . ' FAIL: ' . ($row['error'] ?? 'unknown') . "\n";
	}
}
echo "\nDone.\n";
