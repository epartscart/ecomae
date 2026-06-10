<?php
/**
 * Auto Price AI — sync industry taxonomy → shop_catalogue_categories (all tenants).
 * Dry-run: https://www.ecomae.com/epc-apai-sync-categories-all.php?token=epartscart-deploy-2026
 * Apply:    …&apply=1
 * Per-DB:   …&apply=1&db=electronicae
 * Fix P108: …&apply=1&fix_product=108
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
require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php';
if (is_file(__DIR__ . '/content/shop/price_engine/epc_electronics_taxonomy.php')) {
	require_once __DIR__ . '/content/shop/price_engine/epc_electronics_taxonomy.php';
}

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$onlyDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? '')));
$fixProduct = max(0, (int) ($_GET['fix_product'] ?? 0));
$seedTax = !isset($_GET['seed']) || (string) $_GET['seed'] !== '0';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = trim((string) $cfg->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$targets = array();
$hostNow = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? '');
$currentSiteKey = 'platform';
if (is_file(__DIR__ . '/content/shop/price_engine/epc_auto_price_storefront.php')) {
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_storefront.php';
	$sk = epc_apai_resolve_storefront_site_key();
	if ($sk !== '') {
		$currentSiteKey = $sk;
	}
}
$targets[] = array(
	'label' => 'current_config',
	'host' => $hostNow,
	'site_key' => $currentSiteKey,
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
		$sk = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['site_key'] ?? $dbName)));
		$dup = false;
		foreach ($targets as $t) {
			if ((string) ($t['cred']['db'] ?? '') === $dbName) {
				$dup = true;
				break;
			}
		}
		if ($dup) {
			continue;
		}
		$targets[] = array(
			'label' => $sk,
			'host' => (string) ($row['hostname'] ?? ''),
			'site_key' => $sk,
			'cred' => array(
				'db' => $dbName,
				'user' => (string) ($row['db_user'] ?? ''),
				'pass' => (string) ($row['db_password'] ?? ''),
			),
		);
	}
}

echo "=== EPC Auto Price AI — category sync (all tenants) ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . ' seed_tax=' . ($seedTax ? 'yes' : 'no') . "\n";
echo 'targets=' . count($targets) . "\n\n";

$ok = 0;
$fail = 0;
$report = array();

foreach ($targets as $t) {
	$db = (string) ($t['cred']['db'] ?? '');
	if ($onlyDb !== '' && $db !== $onlyDb) {
		continue;
	}
	$siteKey = (string) ($t['site_key'] ?? $t['label']);
	$host = (string) ($t['host'] ?? '');
	echo "--- {$t['label']} ({$host}) db={$db} site_key={$siteKey} ---\n";

	$pdo = epc_auto_price_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "FAIL: connect\n\n";
		$fail++;
		continue;
	}

	try {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
		$taxNodes = epc_apai_tax_count($pdo, $industryKey);

		if ($apply && $seedTax && $taxNodes < 5) {
			$seed = epc_apai_seed_all_taxonomies($pdo);
			$taxNodes = (int) ($seed['nodes'] ?? $taxNodes);
			echo "seeded_taxonomy_nodes={$taxNodes} ";
		}

		if ($siteKey === 'epartscart') {
			$nativeCount = (int) $pdo->query('SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `alias` NOT LIKE \'apai-%\'')->fetchColumn();
			$apaiCount = (int) $pdo->query('SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `alias` LIKE \'apai-%\'')->fetchColumn();
			echo "SKIP_SYNC (reference tenant) native_categories={$nativeCount} apai_categories={$apaiCount}\n";
			$report[$siteKey] = array('skipped' => true, 'native' => $nativeCount, 'apai' => $apaiCount);
			$ok++;
			echo "\n";
			continue;
		}

		if (!$apply) {
			$mapped = epc_apai_category_count($pdo, $siteKey, $industryKey);
			echo "DRY industry={$industryKey} taxonomy_nodes={$taxNodes} mapped_categories={$mapped}\n";
			$report[$siteKey] = array('dry' => true, 'industry' => $industryKey, 'taxonomy' => $taxNodes, 'mapped' => $mapped);
			$ok++;
			echo "\n";
			continue;
		}

		$sync = epc_apai_sync_categories($pdo, $siteKey, $industryKey);
		$mapped = (int) ($sync['category_map_count'] ?? epc_apai_category_count($pdo, $siteKey, $industryKey));
		$rootId = (int) ($sync['root_category_id'] ?? 0);
		echo "industry={$industryKey} synced={$sync['synced']} root_category_id={$rootId} mapped={$mapped} ";

		$fixIds = array();
		if ($fixProduct > 0) {
			$chk = $pdo->prepare('SELECT `id` FROM `shop_catalogue_products` WHERE `id` = ? LIMIT 1');
			$chk->execute(array($fixProduct));
			if ((int) $chk->fetchColumn() > 0) {
				$fixIds[] = $fixProduct;
			}
		}
		$fixup = epc_apai_fixup_imported_products($pdo, $siteKey, $fixIds);
		echo "products_fixed={$fixup['fixed']} ";

		if (!empty($fixup['products'])) {
			foreach ($fixup['products'] as $pid => $pinfo) {
				echo "\n  product#{$pid} -> category#{$pinfo['category_id']} ({$pinfo['category_url']})";
			}
		}

		$cpUrl = $host !== '' ? 'https://' . preg_replace('/^https?:\/\//', '', $host) . '/' . $backend . '/shop/catalogue/products' : '';
		if ($rootId > 0 && $cpUrl !== '') {
			$cpUrl .= '?category_id=' . $rootId;
		}
		echo "\ncp_catalogue={$cpUrl}\n";

		$report[$siteKey] = array(
			'industry' => $industryKey,
			'synced' => (int) ($sync['synced'] ?? 0),
			'mapped' => $mapped,
			'root_category_id' => $rootId,
			'products_fixed' => (int) ($fixup['fixed'] ?? 0),
			'cp_url' => $cpUrl,
		);
		$ok++;
	} catch (Throwable $e) {
		echo 'FAIL: ' . $e->getMessage() . "\n";
		$fail++;
	}
	echo "\n";
}

echo "Summary: ok={$ok} fail={$fail}\n";
echo "Report JSON:\n" . json_encode($report, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT) . "\n";
echo "Verify electronicae: https://www.electronicae.com/{$backend}/shop/catalogue/products\n";
echo "Verify epartscart (unchanged): https://www.epartscart.com/{$backend}/shop/catalogue/products\n";
echo "Done.\n";
