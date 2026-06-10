<?php
/**
 * Auto Price AI — process pending background jobs (crawl, warehouse match, etc.).
 * Cron: * * * * * curl -s "https://www.ecomae.com/epc-apai-background-jobs-cron.php?token=epartscart-deploy-2026"
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
@set_time_limit(55);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_background_jobs.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$onlySiteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
$onlyJobId = max(0, (int) ($_GET['job_id'] ?? 0));
$started = time();
$processed = 0;
$results = array();

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'Platform registry unavailable')));
}
epc_portal_db_ensure($platformPdo);

foreach (epc_portal_list_tenants($platformPdo) as $row) {
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($row['site_key'] ?? ''))));
	if ($siteKey === '' || $siteKey === 'platform') {
		continue;
	}
	if ($onlySiteKey !== '' && $siteKey !== $onlySiteKey) {
		continue;
	}
	if ((string) ($row['status'] ?? '') !== 'live' || !empty($row['erp_only_shared'])) {
		continue;
	}
	$pdo = epc_auto_price_setup_connect(array(
		'db' => (string) ($row['db_name'] ?? ''),
		'user' => (string) ($row['db_user'] ?? ''),
		'pass' => (string) ($row['db_password'] ?? ''),
	), $cfg);
	if (!$pdo instanceof PDO) {
		continue;
	}
	epc_ape_ensure_schema($pdo);
	epc_apai_bg_ensure_schema($pdo);

	if ($onlyJobId > 0) {
		$job = epc_apai_bg_get($pdo, $onlyJobId, $siteKey);
		if ($job && in_array((string) ($job['status'] ?? ''), array('pending', 'running'), true)) {
			epc_apai_bg_tick($pdo, $onlyJobId, $siteKey);
			$processed++;
		}
		$results[] = array('site_key' => $siteKey, 'job_id' => $onlyJobId, 'processed' => 1);
		continue;
	}

	$ticks = 0;
	while ($ticks < 5 && (time() - $started) < 50) {
		$before = epc_apai_bg_active($pdo, $siteKey);
		if (!$before) {
			break;
		}
		epc_apai_bg_tick($pdo, (int) ($before['id'] ?? 0), $siteKey);
		$processed++;
		$ticks++;
		$after = epc_apai_bg_get($pdo, (int) ($before['id'] ?? 0), $siteKey);
		if ($after && in_array((string) ($after['status'] ?? ''), array('done', 'failed'), true)) {
			break;
		}
	}
	$results[] = array('site_key' => $siteKey, 'ticks' => $ticks);
}

echo json_encode(array(
	'ok' => true,
	'processed' => $processed,
	'elapsed_sec' => time() - $started,
	'results' => $results,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
