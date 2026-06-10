<?php
/**
 * Auto Price AI — daily platform pack + marketplace sync for all live tenants.
 * Scans industry/country taxonomy packs for domains not yet in epc_discovery_sources
 * and auto-adds them enabled; also merges new sell marketplaces (eBay, Amazon, local).
 *
 * Cron: 0 3 * * * curl -s "https://www.ecomae.com/epc-apai-daily-source-expand.php?token=epartscart-deploy-2026"
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
@set_time_limit(300);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';
if (is_file(__DIR__ . '/content/shop/price_engine/epc_apai_marketplace_channels.php')) {
	require_once __DIR__ . '/content/shop/price_engine/epc_apai_marketplace_channels.php';
}

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$onlySiteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
$dryRun = !empty($_GET['dry_run']) && (string) $_GET['dry_run'] === '1';
$weekly = !empty($_GET['weekly']) && (string) $_GET['weekly'] === '1';
$started = time();
$tenantsProcessed = 0;
$tenantsSkipped = 0;
$totalSourcesAdded = 0;
$totalMarketplacesAdded = 0;
$tenantResults = array();

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'Platform registry unavailable')));
}
epc_portal_db_ensure($platformPdo);

foreach (epc_portal_list_tenants($platformPdo) as $row) {
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($row['site_key'] ?? ''))));
	if ($siteKey === '' || $siteKey === 'platform') {
		$tenantsSkipped++;
		continue;
	}
	if ($onlySiteKey !== '' && $siteKey !== $onlySiteKey) {
		continue;
	}
	if ((string) ($row['status'] ?? '') !== 'live') {
		$tenantsSkipped++;
		continue;
	}
	if (!empty($row['erp_only_shared'])) {
		$tenantsSkipped++;
		continue;
	}
	$hostname = strtolower(trim((string) ($row['hostname'] ?? '')));
	if ($hostname !== '' && function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname($hostname)) {
		$tenantsSkipped++;
		continue;
	}
	$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($row['db_name'] ?? ''))));
	if ($dbName === '') {
		$tenantsSkipped++;
		continue;
	}

	$pdo = epc_auto_price_setup_connect(array(
		'db' => $dbName,
		'user' => (string) ($row['db_user'] ?? ''),
		'pass' => (string) ($row['db_password'] ?? ''),
	), $cfg);
	if (!$pdo instanceof PDO) {
		$tenantResults[] = array('site_key' => $siteKey, 'ok' => false, 'error' => 'db_connect_failed');
		$tenantsSkipped++;
		continue;
	}

	epc_ape_ensure_schema($pdo);
	epc_disc_ensure_schema($pdo);
	epc_apai_purge_own_domain_sources($pdo, $siteKey);

	$countBefore = 0;
	$st = $pdo->prepare('SELECT COUNT(*) FROM `epc_discovery_sources` WHERE `site_key` = ? AND `enabled` = 1');
	$st->execute(array($siteKey));
	$countBefore = (int) $st->fetchColumn();

	$industry = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : '';
	$country = epc_apai_tenant_country($siteKey, $pdo);
	$packCount = count(epc_apai_country_sources_for_tenant($pdo, $siteKey, $industry));

	if ($dryRun) {
		$tenantsProcessed++;
		$tenantResults[] = array(
			'site_key' => $siteKey,
			'ok' => true,
			'dry_run' => true,
			'country' => $country,
			'industry' => $industry,
			'pack_domains' => $packCount,
			'sources_before' => $countBefore,
		);
		continue;
	}

	$sourcesAdded = 0;
	$marketAdded = 0;
	if (function_exists('epc_apai_discover_platform_sources')) {
		$disc = epc_apai_discover_platform_sources($pdo, $siteKey);
		foreach ((array) ($disc['results'] ?? array()) as $r) {
			if ((string) ($r['site_key'] ?? '') === $siteKey) {
				$sourcesAdded = (int) ($r['sources_added'] ?? 0);
				$marketAdded = (int) ($r['marketplaces_added'] ?? 0);
				break;
			}
		}
	}

	$countAfter = 0;
	$st->execute(array($siteKey));
	$countAfter = (int) $st->fetchColumn();

	$sellDomains = array();
	if (function_exists('epc_apai_marketplace_channels_for_tenant')) {
		$channels = epc_apai_marketplace_channels_for_tenant($pdo, $siteKey);
		$sellDomains = (array) ($channels['sell_domains'] ?? array());
	}

	$tenantsProcessed++;
	$totalSourcesAdded += $sourcesAdded;
	$totalMarketplacesAdded += $marketAdded;
	$tenantResults[] = array(
		'site_key' => $siteKey,
		'ok' => true,
		'country' => $country,
		'industry' => $industry,
		'pack_domains' => $packCount,
		'sources_before' => $countBefore,
		'sources_after' => $countAfter,
		'sources_added' => $sourcesAdded,
		'marketplaces_added' => $marketAdded,
		'sell_marketplaces' => $sellDomains,
	);
}

echo json_encode(array(
	'ok' => true,
	'job' => $weekly ? 'weekly_platform_sync' : 'daily_source_expand',
	'dry_run' => $dryRun,
	'started_at' => $started,
	'finished_at' => time(),
	'elapsed_sec' => time() - $started,
	'tenants_processed' => $tenantsProcessed,
	'tenants_skipped' => $tenantsSkipped,
	'total_sources_added' => $totalSourcesAdded,
	'total_marketplaces_added' => $totalMarketplacesAdded,
	'tenants' => $tenantResults,
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
