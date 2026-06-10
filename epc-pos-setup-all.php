<?php
/**
 * POS — register routes + menu on platform DB and every live tenant DB.
 *
 * Dry-run:  https://www.ecomae.com/epc-pos-setup-all.php?token=epartscart-deploy-2026
 * Apply:    …&apply=1
 * One DB:   …&apply=1&db=docpart
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
require_once __DIR__ . '/content/shop/pos/epc_pos_cp_install.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$onlyDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? '')));

$cfg = new DP_Config();
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

if (function_exists('epc_portal_platform_pdo')) {
	$platformPdo = epc_portal_platform_pdo();
} else {
	$platformPdo = null;
}

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
			'cred' => array(
				'db' => $cred['db'],
				'user' => $cred['user'],
				'pass' => $cred['pass'],
			),
			'registry_db' => $cred['registry_db'],
			'cred_source' => $cred['source'],
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

echo "=== EPC POS — all tenants ===\n";
echo 'mode=' . ($apply ? 'apply' : 'dry-run') . "\n";
echo 'targets=' . count($unique) . "\n\n";

foreach ($unique as $t) {
	$label = (string) $t['label'];
	$db = (string) $t['cred']['db'];
	$host = (string) $t['host'];
	$registryDb = (string) ($t['registry_db'] ?? $db);
	$credSource = (string) ($t['cred_source'] ?? 'registry');
	echo "=== {$label} (setup_db={$db}";
	if ($registryDb !== '' && $registryDb !== $db) {
		echo ", registry_db={$registryDb}";
	}
	echo ", cred={$credSource}) ===\n";
	if ($host !== '') {
		echo "  pos_url=https://{$host}/{$backend}/shop/pos/terminal\n";
	}
	$pdo = epc_pos_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "  ERROR: cannot connect\n\n";
		continue;
	}
	if (!$apply) {
		try {
			epc_pos_ensure_schema($pdo);
			$cnt = (int) $pdo->query('SELECT COUNT(*) FROM `epc_pos_sales`')->fetchColumn();
			echo "  pos_sales={$cnt} (dry-run)\n\n";
		} catch (Throwable $e) {
			echo "  probe: " . $e->getMessage() . "\n\n";
		}
		continue;
	}
	try {
		$result = epc_pos_cp_install($pdo, $backend);
		echo '  OK content_id=' . $result['content_id'] . ' walkin_user=' . $result['walkin_user_id'] . "\n";
		echo '  menu_item=' . ($result['menu']['items']['pos_terminal'] ?? '?') . "\n\n";
	} catch (Throwable $e) {
		echo '  FAIL: ' . $e->getMessage() . "\n\n";
	}
}

echo "Done.\n";
