<?php
/**
 * Verify Auto Price AI hourly crawl + self-tenant source exclusion.
 * GET /epc-apai-hourly-crawl-verify.php?token=…&site_key=epartscart
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
require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = null;
$platformPdo = epc_portal_platform_pdo();
if ($platformPdo instanceof PDO && $siteKey !== '') {
	epc_portal_db_ensure($platformPdo);
	if (function_exists('epc_portal_tenant_get')) {
		require_once __DIR__ . '/content/general_pages/epc_portal_tenant_intro.php';
		$row = epc_portal_tenant_get($platformPdo, $siteKey);
		if (is_array($row)) {
			$pdo = epc_auto_price_setup_connect(array(
				'db' => (string) ($row['db_name'] ?? ''),
				'user' => (string) ($row['db_user'] ?? ''),
				'pass' => (string) ($row['db_password'] ?? ''),
			), $cfg);
		}
	}
}
if (!$pdo instanceof PDO) {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}
epc_ape_ensure_schema($pdo);
epc_disc_ensure_schema($pdo);

$ownDomains = epc_apai_tenant_own_domains($siteKey, $pdo);
$pack = epc_apai_country_sources_for_tenant($pdo, $siteKey, epc_apai_resolve_industry($pdo, $siteKey));
$purged = epc_apai_purge_own_domain_sources($pdo, $siteKey);
epc_apai_install_country_sources($pdo, $siteKey);
$listSources = epc_disc_sources_list($pdo, $siteKey);
$dbOwn = array();
foreach ($listSources as $src) {
	$d = (string) ($src['domain'] ?? '');
	if ($d !== '' && epc_apai_is_tenant_own_domain($siteKey, $d, $pdo)) {
		$dbOwn[] = $d;
	}
}

$hasHourlyFn = function_exists('epc_disc_crawl_sources');
$hasScheduledCol = false;
try {
	$col = $pdo->query("SHOW COLUMNS FROM `epc_auto_price_tenant_config` LIKE 'last_scheduled_crawl_at'")->fetch(PDO::FETCH_ASSOC);
	$hasScheduledCol = !empty($col);
} catch (Throwable $e) {
}

$autoCfg = epc_apai_auto_crawl_config($pdo, $siteKey);
$lastScheduled = epc_disc_get_last_scheduled_crawl_at($pdo, $siteKey);
$lastCrawl = epc_disc_get_last_crawl_at($pdo, $siteKey);
$nextCrawl = epc_apai_next_scheduled_crawl_at($pdo, $siteKey);

echo json_encode(array(
	'ok' => empty($dbOwn),
	'site_key' => $siteKey,
	'own_domains' => $ownDomains,
	'pack_domains' => array_column($pack, 'domain'),
	'pack_contains_own' => (bool) array_filter($pack, function ($src) use ($siteKey, $pdo) {
		return epc_apai_is_tenant_own_domain($siteKey, (string) ($src['domain'] ?? ''), $pdo);
	}),
	'db_own_domains' => $dbOwn,
	'purged_own_sources' => $purged,
	'listed_source_count' => count($listSources),
	'listed_domains' => array_column(array_map('epc_disc_source_format_row', $listSources), 'domain'),
	'auto_crawl' => $autoCfg,
	'last_crawl_at' => $lastCrawl,
	'last_scheduled_crawl_at' => $lastScheduled,
	'next_scheduled_crawl_at' => $nextCrawl,
	'minutes_until_next' => $nextCrawl > 0 ? max(0, (int) ceil(($nextCrawl - time()) / 60)) : null,
	'hourly_script' => '/epc-apai-hourly-crawl.php',
	'checks' => array(
		'epc_disc_crawl_sources' => $hasHourlyFn,
		'last_scheduled_crawl_at_column' => $hasScheduledCol,
		'no_own_domain_in_list' => empty($dbOwn),
	),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
