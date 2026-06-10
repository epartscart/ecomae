<?php
/**
 * Auto Price AI — hourly scheduled crawl for all live commerce tenants.
 * Cron: 0 * * * * curl -s "https://www.ecomae.com/epc-apai-hourly-crawl.php?token=epartscart-deploy-2026"
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

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
if (is_file(__DIR__ . '/content/shop/price_engine/epc_apai_background_jobs.php')) {
	require_once __DIR__ . '/content/shop/price_engine/epc_apai_background_jobs.php';
}
require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$dryRun = !empty($_GET['dry_run']) && (string) $_GET['dry_run'] === '1';
$onlySiteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
$started = time();
$tenantsProcessed = 0;
$tenantsSkipped = 0;
$sourcesCrawled = 0;
$pricesUpdated = 0;
$suggestionsAdded = 0;
$ownSourcesPurged = 0;
$tenantResults = array();

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'Platform registry unavailable')));
}
epc_portal_db_ensure($platformPdo);

$liveTenants = array();
foreach (epc_portal_list_tenants($platformPdo) as $row) {
	$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($row['site_key'] ?? ''))));
	if ($sk !== '' && $sk !== 'platform' && (string) ($row['status'] ?? '') === 'live' && empty($row['erp_only_shared'])) {
		$liveTenants[] = $sk;
	}
}
$tenantTotal = count($liveTenants);
$tenantIndexMap = array();
foreach ($liveTenants as $i => $sk) {
	$tenantIndexMap[$sk] = $i;
}

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
	$autoCfg = epc_apai_auto_crawl_config($pdo, $siteKey);
	if (!$autoCfg['enabled']) {
		$tenantsSkipped++;
		$tenantResults[] = array('site_key' => $siteKey, 'ok' => true, 'skipped' => 'auto_crawl_disabled');
		continue;
	}

	$purged = epc_apai_purge_own_domain_sources($pdo, $siteKey);
	$ownSourcesPurged += $purged;

	$tenantIndex = (int) ($tenantIndexMap[$siteKey] ?? 0);
	if ($onlySiteKey === '' && !$dryRun && function_exists('epc_apai_hourly_crawl_should_run_tenant')
		&& !epc_apai_hourly_crawl_should_run_tenant($siteKey, $tenantIndex, $tenantTotal)) {
		$tenantsSkipped++;
		$tenantResults[] = array('site_key' => $siteKey, 'ok' => true, 'skipped' => 'stagger_slot');
		continue;
	}

	if ($dryRun) {
		$enabledSources = epc_disc_sources_for_search($pdo, $siteKey, 0, '', true);
		$tenantResults[] = array(
			'site_key' => $siteKey,
			'ok' => true,
			'dry_run' => true,
			'sources' => count($enabledSources),
			'own_purged' => $purged,
			'next_crawl_at' => epc_apai_next_scheduled_crawl_at($pdo, $siteKey),
			'stagger_slot' => $tenantIndex,
		);
		$tenantsProcessed++;
		continue;
	}

	if (function_exists('epc_disc_auto_seed_if_empty')) {
		epc_disc_auto_seed_if_empty($pdo, $siteKey);
	}
	if (function_exists('epc_apai_discover_platform_sources')) {
		epc_apai_discover_platform_sources($pdo, $siteKey);
	}
	if (function_exists('epc_apai_bg_process_pending')) {
		epc_apai_bg_process_pending($pdo, $siteKey, 2);
	}
	epc_disc_crawl_job_process_one($pdo, $siteKey);
	$res = epc_disc_crawl_sources($pdo, $siteKey, array('scheduled' => true, 'mode' => 'quick'));
	$tenantsProcessed++;
	$sourcesCrawled += (int) ($res['sources_crawled'] ?? 0);
	$pricesUpdated += (int) ($res['updated'] ?? 0);
	$suggestionsAdded += (int) ($res['added'] ?? 0);
	$tenantResults[] = array(
		'site_key' => $siteKey,
		'ok' => !empty($res['ok']),
		'updated' => (int) ($res['updated'] ?? 0),
		'added' => (int) ($res['added'] ?? 0),
		'sources_crawled' => (int) ($res['sources_crawled'] ?? 0),
		'last_scheduled_crawl_at' => (int) ($res['last_scheduled_crawl_at'] ?? 0),
		'own_purged' => $purged,
		'message' => (string) ($res['message'] ?? ''),
	);
}

echo json_encode(array(
	'ok' => true,
	'dry_run' => $dryRun,
	'started_at' => $started,
	'finished_at' => time(),
	'elapsed_sec' => time() - $started,
	'tenants_processed' => $tenantsProcessed,
	'tenants_skipped' => $tenantsSkipped,
	'sources_crawled' => $sourcesCrawled,
	'prices_updated' => $pricesUpdated,
	'suggestions_added' => $suggestionsAdded,
	'own_sources_purged' => $ownSourcesPurged,
	'tenants' => $tenantResults,
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
