<?php
/**
 * Discover tab visibility + crawl helpers — deploy-token verification.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
if ($siteKey === '') {
	$siteKey = 'epartscart';
}

try {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';
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
		echo json_encode(array('ok' => false, 'error' => 'tenant_not_found', 'site_key' => $siteKey));
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

	$hasFiltersFn = function_exists('epc_disc_default_discover_filters');
	$hasListFn = function_exists('epc_disc_queue_list_for_discover');
	$hasCountsFn = function_exists('epc_disc_discover_counts');
	$hasCrawlFn = function_exists('epc_disc_crawl_sources');
	$hasDeepFn = function_exists('epc_disc_deep_fetch_url');
	$hasFindFn = function_exists('epc_disc_queue_find_existing');
	$hasBaKeyFn = function_exists('epc_apai_brand_article_key');
	$hasBaDupFn = function_exists('epc_disc_queue_dup_key');
	$industry = epc_apai_resolve_industry($pdo, $siteKey);
	$baSample = $hasBaKeyFn ? epc_apai_brand_article_key('Toyota', '1310154101') : '';
	$baDupSample = '';
	if ($hasBaDupFn && $industry === 'auto_parts') {
		$baDupSample = epc_disc_queue_dup_key(array(
			'site_key' => $siteKey,
			'specs_json' => json_encode(array('brand' => 'Toyota', 'article_number' => '1310154101', 'brand_article_key' => 'toyota:1310154101')),
			'source_domain' => 'partsouq.com',
			'title' => 'Ignition coil',
		), 0, 'auto_parts');
	}

	$newView = $hasListFn ? epc_disc_queue_list_for_discover($pdo, $siteKey, array('view' => 'all_suggestions', 'sort' => 'newest', 'limit' => 60)) : array();
	$priceView = $hasListFn ? epc_disc_queue_list_for_discover($pdo, $siteKey, array('view' => 'price_changes', 'sort' => 'price_change', 'limit' => 60)) : array();
	$mcView = $hasListFn ? epc_disc_queue_list_for_discover($pdo, $siteKey, array('view' => 'market_confirmed', 'sort' => 'newest', 'limit' => 60)) : array();
	$allView = $hasListFn ? epc_disc_queue_list_for_discover($pdo, $siteKey, array('view' => 'all_suggestions', 'sort' => 'newest', 'limit' => 60)) : array();
	$defaultFilters = $hasFiltersFn
		? epc_disc_default_discover_filters($pdo, $siteKey, array())
		: array('view' => 'all_suggestions');
	$defaultViewList = $hasListFn
		? epc_disc_queue_list_for_discover($pdo, $siteKey, $defaultFilters)
		: array();
	$counts = $hasCountsFn ? epc_disc_discover_counts($pdo, $siteKey) : array('new' => 0, 'price_changes' => 0);

	$stmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'imported\'');
	$stmt->execute(array($siteKey));
	$rawImported = (int) $stmt->fetchColumn();

	$rawSuggestedStmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\'');
	$rawSuggestedStmt->execute(array($siteKey));
	$rawSuggestedBefore = (int) $rawSuggestedStmt->fetchColumn();

	$rawRejectedStmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'rejected\'');
	$rawRejectedStmt->execute(array($siteKey));
	$rawRejected = (int) $rawRejectedStmt->fetchColumn();

	$autoSeedResult = null;
	if (!empty($_GET['run_seed']) && (string) $_GET['run_seed'] === '1' && function_exists('epc_disc_auto_seed_if_empty')) {
		$autoSeedResult = epc_disc_auto_seed_if_empty($pdo, $siteKey, array('force' => !empty($_GET['force_seed'])));
		$newView = $hasListFn ? epc_disc_queue_list_for_discover($pdo, $siteKey, array('view' => 'all_suggestions', 'sort' => 'newest', 'limit' => 60)) : array();
		$mcView = $hasListFn ? epc_disc_queue_list_for_discover($pdo, $siteKey, array('view' => 'market_confirmed', 'sort' => 'newest', 'limit' => 60)) : array();
		$defaultFilters = $hasFiltersFn ? epc_disc_default_discover_filters($pdo, $siteKey, array()) : array('view' => 'all_suggestions');
		$defaultViewList = $hasListFn ? epc_disc_queue_list_for_discover($pdo, $siteKey, $defaultFilters) : array();
		$counts = $hasCountsFn ? epc_disc_discover_counts($pdo, $siteKey) : array('new' => 0, 'price_changes' => 0);
	}
	$rawSuggestedStmt->execute(array($siteKey));
	$rawSuggested = (int) $rawSuggestedStmt->fetchColumn();

	$tenantCfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$lastCrawl = epc_disc_get_last_crawl_at($pdo, $siteKey);

	echo json_encode(array(
		'ok' => $hasFiltersFn && $hasListFn && $hasCountsFn && $hasCrawlFn && $hasDeepFn && $hasFindFn && $hasBaKeyFn,
		'site_key' => $siteKey,
		'host' => (string) ($row['host'] ?? ''),
		'profile' => (string) ($tenantCfg['profile'] ?? ''),
		'industry' => $industry,
		'functions' => array(
			'epc_disc_default_discover_filters' => $hasFiltersFn,
			'epc_disc_default_discover_view' => function_exists('epc_disc_default_discover_view'),
			'epc_disc_queue_list_for_discover' => $hasListFn,
			'epc_disc_discover_counts' => $hasCountsFn,
			'epc_disc_crawl_sources' => $hasCrawlFn,
		),
		'views' => array(
			'all_suggestions' => count($allView),
			'market_confirmed' => count($mcView),
			'price_changes' => count($priceView),
			'default_view_resolved' => (string) ($defaultFilters['view'] ?? ''),
			'default_view_fallback_from' => (string) ($defaultFilters['fallback_from'] ?? ''),
			'default_view_list' => count($defaultViewList),
			'counts' => $counts,
		),
		'queue' => array(
			'raw_suggested_before' => $rawSuggestedBefore,
			'raw_suggested' => $rawSuggested,
			'raw_imported' => $rawImported,
			'raw_rejected' => $rawRejected,
		),
		'auto_seed' => $autoSeedResult,
		'last_crawl_at' => $lastCrawl,
		'sample_all' => array_slice(array_map(function ($r) {
			return array(
				'id' => (int) ($r['id'] ?? 0),
				'title' => substr((string) ($r['title'] ?? ''), 0, 60),
				'market_confirmed' => !empty($r['market_confirmed']),
				'source_match_count' => (int) ($r['source_match_count'] ?? 0),
			);
		}, $allView), 0, 3),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage(), 'site_key' => $siteKey));
}
