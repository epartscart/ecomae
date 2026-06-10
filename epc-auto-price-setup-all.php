<?php
/**
 * Auto Price Engine — install on platform + all live tenant DBs.
 * Dry-run: https://www.ecomae.com/epc-auto-price-setup-all.php?token=epartscart-deploy-2026
 * Apply:    …&apply=1&seed=1
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

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$seed = !empty($_GET['seed']) && (string) $_GET['seed'] === '1';
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
		$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['db_name'] ?? '')));
		if ($dbName === '') {
			continue;
		}
		$targets[] = array(
			'label' => (string) ($row['site_key'] ?? $dbName),
			'host' => (string) ($row['hostname'] ?? ''),
			'site_key' => (string) ($row['site_key'] ?? $dbName),
			'cred' => array(
				'db' => $dbName,
				'user' => (string) ($row['db_user'] ?? ''),
				'pass' => (string) ($row['db_password'] ?? ''),
			),
		);
	}
}

echo "=== EPC Auto Price AI — batch setup (all tenants) ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . ' seed=' . ($seed ? 'yes' : 'no') . "\n";
echo 'targets=' . count($targets) . "\n\n";

$ok = 0;
$fail = 0;

foreach ($targets as $t) {
	$db = (string) ($t['cred']['db'] ?? '');
	if ($onlyDb !== '' && $db !== $onlyDb) {
		continue;
	}
	echo "--- {$t['label']} ({$t['host']}) db={$db} ---\n";
	$pdo = epc_auto_price_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "FAIL: connect\n\n";
		$fail++;
		continue;
	}
	try {
		$res = epc_auto_price_cp_install(
			$pdo,
			$backend,
			$apply,
			$seed,
			(string) ($t['site_key'] ?? $t['label']),
			(string) ($t['host'] ?? '')
		);
		echo 'schema=ok';
		if ($apply) {
			echo " content_id={$res['content_id']} menu={$res['menu_item_id']}";
		}
		if ($seed && !empty($res['seeded'])) {
			$s = $res['seeded'];
			echo " seed_sources={$s['sources']}";
			if (!empty($s['industry_key'])) {
				echo " industry={$s['industry_key']}";
			}
			if (!empty($s['taxonomy_nodes'])) {
				echo " taxonomy={$s['taxonomy_nodes']}";
			}
			if (!empty($s['categories_synced'])) {
				echo " categories_synced={$s['categories_synced']}";
			}
			if (!empty($s['category_count'])) {
				echo " category_map={$s['category_count']}";
			}
			if (!empty($s['discovery_queue'])) {
				echo " discovery_queue={$s['discovery_queue']}";
			}
			if (!empty($s['discovery_imported_id'])) {
				echo " imported_product={$s['discovery_imported_id']}";
			}
			if (!empty($s['imported_refreshed'])) {
				echo " source_prices_refreshed={$s['imported_refreshed']}";
			}
		}
		echo "\n\n";
		$ok++;
	} catch (Throwable $e) {
		echo 'FAIL: ' . $e->getMessage() . "\n\n";
		$fail++;
	}
}

echo "Summary: ok={$ok} fail={$fail}\n";
echo "Super CP: /{$backend}/control/portal/epc_auto_price_engine?site_key=epartscart&tab=discover\n";
echo "Tenants: epartscart, electronicae, stylenlook, thejewellerytrend, taxofinca\n";
echo "Discovery cron: /epc-auto-discovery-run.php?token=…&site_key=TENANT&taxonomy=SLUG\n";
echo "Done.\n";
