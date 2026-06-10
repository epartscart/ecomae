<?php
/**
 * Auto Price AI — Discover tab AJAX (search, fetch prices, bulk approve).
 */
define('_ASTEXE_', 1);

if (ob_get_level()) {
	ob_end_clean();
}

$actionEarly = (string) ($_POST['action'] ?? $_GET['action'] ?? '');
$epcApaiHtmlAction = ($actionEarly === 'load_tab_html');
if (!$epcApaiHtmlAction) {
	header('Content-Type: application/json; charset=utf-8');
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;
$epcTenantDbFile = $_SERVER['DOCUMENT_ROOT'] . '/config.tenant-db.php';
if (is_file($epcTenantDbFile)) {
	$epc_tenant_db = null;
	require $epcTenantDbFile;
	if (isset($epc_tenant_db) && is_array($epc_tenant_db)) {
		foreach (array('db', 'user', 'password', 'host') as $epcTk) {
			if (!empty($epc_tenant_db[$epcTk]) && property_exists($DP_Config, $epcTk)) {
				$DP_Config->$epcTk = $epc_tenant_db[$epcTk];
			}
		}
	}
}
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($DP_Config);
	}
}

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'Database unavailable')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_discovery_adapters.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_country_sources.php';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_background_jobs.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_background_jobs.php';
}
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_marketplace_channels.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_marketplace_channels.php';
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_industry_taxonomy.php';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_categories.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_categories.php';
}

if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Admin login required')));
}

global $db_link;
$platformPdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$platformPdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'message' => 'Database unavailable')));
}

epc_ape_ensure_schema($platformPdo);

$isSuperCp = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
if ($isSuperCp) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_super_cp_platform.php';
	if (!epc_scp_guard_super_admin()) {
		http_response_code(403);
		exit(json_encode(array('ok' => false, 'message' => 'Super admin required')));
	}
}

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $_GET['site_key'] ?? ''))));
if ($siteKey === '') {
	$host = function_exists('epc_portal_host') ? strtolower(epc_portal_host()) : '';
	if (strpos($host, 'electronicae') !== false) {
		$siteKey = 'electronicae';
	} elseif (strpos($host, 'epartscart') !== false) {
		$siteKey = 'epartscart';
	} elseif (strpos($host, 'stylenlook') !== false) {
		$siteKey = 'stylenlook';
	} elseif (strpos($host, 'thejewellerytrend') !== false) {
		$siteKey = 'thejewellerytrend';
	} elseif (strpos($host, 'taxofinca') !== false) {
		$siteKey = 'taxofinca';
	} elseif (function_exists('epc_portal_site_key_from_hostname') && $host !== '') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
		$siteKey = epc_portal_site_key_from_hostname($host);
	}
	if ($siteKey === '') {
		$siteKey = 'platform';
	}
}

$pdo = $platformPdo;
if ($isSuperCp && $siteKey !== '' && $siteKey !== 'platform') {
	$tenantPdo = epc_ape_tenant_pdo($platformPdo, $siteKey);
	if ($tenantPdo instanceof PDO) {
		epc_ape_ensure_schema($tenantPdo);
		$pdo = $tenantPdo;
	}
}

$action = (string) ($_POST['action'] ?? $_GET['action'] ?? '');

if ($action === 'discover_search') {
	$keyword = trim((string) ($_POST['keyword'] ?? ''));
	$taxSlug = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim((string) ($_POST['taxonomy_slug'] ?? ''))));
	$taxonomyId = max(0, (int) ($_POST['taxonomy_id'] ?? 0));
	if ($taxSlug === '' && $taxonomyId > 0) {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
		$st = $pdo->prepare('SELECT `slug` FROM `epc_product_taxonomy_nodes` WHERE `id` = ? AND `industry_key` = ? LIMIT 1');
		$st->execute(array($taxonomyId, $industryKey));
		$taxSlug = (string) ($st->fetchColumn() ?: '');
	}
	if ($keyword === '' && $taxSlug === '') {
		exit(json_encode(array('ok' => false, 'message' => 'Enter a keyword or select a product line')));
	}
	$searchMode = in_array((string) ($_POST['search_mode'] ?? 'full'), array('fast', 'full'), true)
		? (string) ($_POST['search_mode'] ?? 'full')
		: 'full';
	$res = epc_disc_run_for_taxonomy($pdo, $siteKey, $taxSlug, $keyword, array('search_mode' => $searchMode));
	if (function_exists('epc_disc_cross_source_match')) {
		epc_disc_cross_source_match($pdo, $siteKey);
	}
	$viewParam = (string) ($_POST['view'] ?? $_POST['visibility'] ?? '');
	if ($viewParam === '' || $viewParam === 'new') {
		$viewParam = function_exists('epc_disc_default_discover_view')
			? epc_disc_default_discover_view($pdo, $siteKey)
			: 'all_suggestions';
	}
	$discFilters = array(
		'taxonomy_id' => $taxonomyId,
		'view' => $viewParam,
		'sort' => (string) ($_POST['sort'] ?? 'newest'),
	);
	$discFilters = epc_disc_default_discover_filters($pdo, $siteKey, array_merge($discFilters, array(
		'market_confirmed' => $_POST['market_confirmed'] ?? null,
		'warehouse_only' => $_POST['warehouse_only'] ?? null,
		'show_all' => $_POST['show_all'] ?? null,
	)));
	$queue = epc_disc_queue_list_for_discover($pdo, $siteKey, $discFilters);
	exit(json_encode(array(
		'ok' => !empty($res['ok']),
		'added' => (int) ($res['added'] ?? 0),
		'message' => (string) ($res['message'] ?? ''),
		'search_message' => (string) ($res['search_message'] ?? ''),
		'sources_used' => (int) ($res['sources_used'] ?? 0),
		'source_domains' => (array) ($res['source_domains'] ?? array()),
		'suggested_count' => count($queue),
		'country_code' => epc_apai_tenant_country($siteKey, $pdo),
	)));
}

if ($action === 'list_discovery_sources') {
	$taxonomyId = max(0, (int) ($_POST['taxonomy_id'] ?? $_GET['taxonomy_id'] ?? 0));
	$taxSlug = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim((string) ($_POST['taxonomy_slug'] ?? $_GET['taxonomy_slug'] ?? ''))));
	$forSearch = !empty($_POST['for_search']) || !empty($_GET['for_search']);
	$rows = $forSearch
		? epc_disc_sources_for_search($pdo, $siteKey, $taxonomyId, $taxSlug, false)
		: epc_disc_sources_for_tenant($pdo, $siteKey, false);
	$formatted = array();
	foreach ($rows as $row) {
		$formatted[] = epc_disc_source_format_row($row);
	}
	exit(json_encode(array(
		'ok' => true,
		'sources' => $formatted,
		'count' => count($formatted),
		'country_code' => epc_apai_tenant_country($siteKey, $pdo),
	)));
}

if ($action === 'add_discovery_source') {
	$domain = trim((string) ($_POST['domain'] ?? ''));
	if ($domain === '') {
		exit(json_encode(array('ok' => false, 'message' => 'Domain or URL is required')));
	}
	try {
		$id = epc_disc_source_save($pdo, $siteKey, array(
			'domain' => $domain,
			'label' => trim((string) ($_POST['label'] ?? '')),
			'source_type' => 'custom_website',
			'created_by_tenant' => 1,
			'enabled' => !isset($_POST['enabled']) || !empty($_POST['enabled']),
			'priority' => (int) ($_POST['priority'] ?? 100),
			'taxonomy_node_id' => max(0, (int) ($_POST['taxonomy_node_id'] ?? 0)),
			'product_line_slug' => (string) ($_POST['product_line_slug'] ?? ''),
			'requires_login' => !empty($_POST['requires_login']),
			'auth_type' => (string) ($_POST['auth_type'] ?? 'none'),
			'auth_username' => trim((string) ($_POST['auth_username'] ?? '')),
			'auth_password' => (string) ($_POST['auth_password'] ?? ''),
			'login_url' => trim((string) ($_POST['login_url'] ?? '')),
			'login_form_selector' => trim((string) ($_POST['login_form_selector'] ?? '')),
		), max(0, (int) ($_POST['id'] ?? 0)));
		$row = epc_disc_source_get($pdo, $id, $siteKey);
		exit(json_encode(array(
			'ok' => true,
			'id' => $id,
			'message' => 'Custom source saved',
			'source' => $row ? epc_disc_source_format_row($row) : null,
		)));
	} catch (Throwable $e) {
		exit(json_encode(array('ok' => false, 'message' => $e->getMessage())));
	}
}

if ($action === 'delete_discovery_source') {
	$id = max(0, (int) ($_POST['id'] ?? $_GET['id'] ?? 0));
	if ($id <= 0) {
		exit(json_encode(array('ok' => false, 'message' => 'Source id required')));
	}
	if (!epc_disc_source_delete($pdo, $id, $siteKey)) {
		exit(json_encode(array('ok' => false, 'message' => 'Only custom tenant sources can be deleted')));
	}
	exit(json_encode(array('ok' => true, 'message' => 'Custom source removed', 'id' => $id)));
}

if ($action === 'toggle_discovery_source') {
	$id = max(0, (int) ($_POST['id'] ?? $_GET['id'] ?? 0));
	if ($id <= 0) {
		exit(json_encode(array('ok' => false, 'message' => 'Source id required')));
	}
	$enabled = null;
	if (isset($_POST['enabled']) || isset($_GET['enabled'])) {
		$enabled = !empty($_POST['enabled']) || !empty($_GET['enabled']);
	}
	if (!epc_disc_source_toggle($pdo, $id, $siteKey, $enabled)) {
		exit(json_encode(array('ok' => false, 'message' => 'Source not found')));
	}
	$row = epc_disc_source_get($pdo, $id, $siteKey);
	exit(json_encode(array(
		'ok' => true,
		'message' => 'Source updated',
		'source' => $row ? epc_disc_source_format_row($row) : null,
	)));
}

if ($action === 'test_source_login') {
	try {
		$result = epc_disc_source_test_login($pdo, $siteKey, array(
			'id' => max(0, (int) ($_POST['id'] ?? 0)),
			'domain' => trim((string) ($_POST['domain'] ?? '')),
			'requires_login' => !empty($_POST['requires_login']),
			'auth_type' => (string) ($_POST['auth_type'] ?? 'none'),
			'auth_username' => trim((string) ($_POST['auth_username'] ?? '')),
			'auth_password' => (string) ($_POST['auth_password'] ?? ''),
			'login_url' => trim((string) ($_POST['login_url'] ?? '')),
			'login_form_selector' => trim((string) ($_POST['login_form_selector'] ?? '')),
		));
		exit(json_encode(array_merge(array('ok' => !empty($result['ok'])), $result)));
	} catch (Throwable $e) {
		exit(json_encode(array('ok' => false, 'message' => 'Login failed: ' . $e->getMessage())));
	}
}

if ($action === 'fetch_prices') {
	$rawIds = $_POST['queue_ids'] ?? $_POST['queue_id'] ?? array();
	if (!is_array($rawIds)) {
		$rawIds = array($rawIds);
	}
	$queueIds = array();
	foreach ($rawIds as $id) {
		$id = (int) $id;
		if ($id > 0) {
			$queueIds[] = $id;
		}
	}
	$deep = !empty($_POST['deep']) || !empty($_GET['deep']);
	$res = epc_disc_fetch_queue_prices(
		$pdo,
		$siteKey,
		$queueIds,
		max(0, (int) ($_POST['taxonomy_id'] ?? 0)),
		array('deep' => $deep)
	);
	exit(json_encode(array(
		'ok' => !empty($res['ok']),
		'updated' => (int) ($res['updated'] ?? 0),
		'message' => (string) ($res['message'] ?? ''),
		'items' => (array) ($res['items'] ?? array()),
		'sources_used' => (int) ($res['sources_used'] ?? 0),
		'source_domains' => (array) ($res['source_domains'] ?? array()),
	)));
}

if ($action === 'start_job') {
	@set_time_limit(25);
	$jobType = preg_replace('/[^a-z_]/', '', strtolower(trim((string) ($_POST['type'] ?? $_GET['type'] ?? ''))));
	$typeAlias = array(
		'crawl' => 'crawl_quick',
		'crawl_quick' => 'crawl_quick',
		'crawl_full' => 'crawl_full',
		'warehouse_market_match' => 'warehouse_market_match',
		'warehouse_match' => 'warehouse_market_match',
		'discover_seed' => 'discover_seed',
		'compare_refresh' => 'compare_refresh',
	);
	$jobType = $typeAlias[$jobType] ?? $jobType;
	if (!function_exists('epc_apai_bg_start')) {
		exit(json_encode(array('ok' => false, 'message' => 'Background jobs unavailable')));
	}
	$jobOpts = array(
		'taxonomy_id' => max(0, (int) ($_POST['taxonomy_id'] ?? $_GET['taxonomy_id'] ?? 0)),
	);
	try {
		$jobId = epc_apai_bg_start($pdo, $siteKey, $jobType, $jobOpts);
	} catch (Throwable $e) {
		exit(json_encode(array('ok' => false, 'message' => $e->getMessage())));
	}
	if (in_array($jobType, array('warehouse_market_match', 'discover_seed', 'compare_refresh'), true)) {
		epc_apai_bg_tick($pdo, $jobId, $siteKey);
	} elseif (function_exists('epc_apai_bg_trigger_worker')) {
		epc_apai_bg_trigger_worker($siteKey, $jobId);
	}
	exit(json_encode(array(
		'ok' => true,
		'job_id' => $jobId,
		'job_type' => $jobType,
		'status' => 'pending',
		'message' => 'Job queued',
	)));
}

if ($action === 'job_status') {
	@set_time_limit(25);
	if (!function_exists('epc_apai_bg_get')) {
		exit(json_encode(array('ok' => false, 'message' => 'Background jobs unavailable')));
	}
	$jobId = max(0, (int) ($_POST['job_id'] ?? $_GET['job_id'] ?? 0));
	$runTick = !empty($_POST['run_tick']) || !empty($_GET['run_tick']);
	$job = $jobId > 0 ? epc_apai_bg_get($pdo, $jobId, $siteKey) : epc_apai_bg_active($pdo, $siteKey);
	if (!$job) {
		exit(json_encode(array('ok' => true, 'status' => 'idle', 'message' => 'No job in progress')));
	}
	$jobType = (string) ($job['job_type'] ?? '');
	$lightTick = in_array($jobType, array('warehouse_market_match', 'discover_seed', 'compare_refresh'), true);
	if ($runTick && $lightTick && in_array((string) ($job['status'] ?? ''), array('pending', 'running'), true)) {
		$job = epc_apai_bg_tick($pdo, (int) ($job['id'] ?? 0), $siteKey) ?: $job;
	}
	exit(json_encode(epc_apai_bg_status_payload($job)));
}

if ($action === 'crawl_sources') {
	$mode = in_array((string) ($_POST['mode'] ?? 'quick'), array('quick', 'full'), true)
		? (string) ($_POST['mode'] ?? 'quick')
		: 'quick';
	$useBg = function_exists('epc_apai_bg_start');
	$background = !empty($_POST['background']) || !empty($_GET['background']) || $useBg;
	$crawlOpts = array(
		'taxonomy_id' => max(0, (int) ($_POST['taxonomy_id'] ?? 0)),
		'mode' => $mode,
	);
		if ($background && $useBg) {
		$jobType = ($mode === 'full') ? 'crawl_full' : 'crawl_quick';
		$jobId = epc_apai_bg_start($pdo, $siteKey, $jobType, $crawlOpts);
		if (function_exists('epc_apai_bg_trigger_worker')) {
			epc_apai_bg_trigger_worker($siteKey, $jobId);
		}
		exit(json_encode(array(
			'ok' => true,
			'background' => true,
			'job_id' => $jobId,
			'job_type' => $jobType,
			'mode' => $mode,
			'message' => ($mode === 'full' ? 'Full' : 'Quick') . ' crawl queued',
		)));
	}
	$res = epc_disc_crawl_sources($pdo, $siteKey, $crawlOpts);
	exit(json_encode(array(
		'ok' => !empty($res['ok']),
		'updated' => (int) ($res['updated'] ?? 0),
		'added' => (int) ($res['added'] ?? 0),
		'sources_crawled' => (int) ($res['sources_crawled'] ?? 0),
		'sources_total' => (int) ($res['sources_total'] ?? 0),
		'sources_fetched' => (int) ($res['sources_fetched'] ?? 0),
		'elapsed_sec' => (float) ($res['elapsed_sec'] ?? 0),
		'mode' => (string) ($res['mode'] ?? $mode),
		'message' => (string) ($res['message'] ?? ''),
		'last_crawl_at' => (int) ($res['last_crawl_at'] ?? 0),
		'source_domains' => (array) ($res['source_domains'] ?? array()),
		'cross_source_match' => (array) ($res['cross_source_match'] ?? array()),
		'warehouse_market_match' => (array) ($res['warehouse_market_match'] ?? array()),
		'fallbacks' => (array) ($res['fallbacks'] ?? array()),
		'fallback_action' => (string) ($res['fallback_action'] ?? ''),
	)));
}

if ($action === 'crawl_status') {
	$jobId = max(0, (int) ($_POST['job_id'] ?? $_GET['job_id'] ?? 0));
	if (function_exists('epc_apai_bg_get')) {
		$job = $jobId > 0 ? epc_apai_bg_get($pdo, $jobId, $siteKey) : epc_apai_bg_active($pdo, $siteKey);
		if ($job) {
			$job = epc_apai_bg_tick($pdo, (int) ($job['id'] ?? 0), $siteKey) ?: $job;
			exit(json_encode(epc_apai_bg_status_payload($job)));
		}
	}
	$job = $jobId > 0 ? epc_disc_crawl_job_get($pdo, $jobId, $siteKey) : epc_disc_crawl_job_active($pdo, $siteKey);
	if (!$job) {
		exit(json_encode(array('ok' => true, 'status' => 'idle', 'message' => 'No crawl in progress')));
	}
	$result = json_decode((string) ($job['result_json'] ?? ''), true);
	if (!is_array($result)) {
		$result = array();
	}
	if ((string) ($job['status'] ?? '') === 'pending') {
		$processed = epc_disc_crawl_job_process_one($pdo, $siteKey);
		if ($processed) {
			exit(json_encode(array_merge(array('ok' => true, 'status' => 'done'), $processed)));
		}
	}
	exit(json_encode(array(
		'ok' => true,
		'status' => (string) ($job['status'] ?? 'pending'),
		'job_id' => (int) ($job['id'] ?? 0),
		'mode' => (string) ($job['mode'] ?? 'full'),
		'message' => (string) ($result['message'] ?? 'Crawl in progress…'),
		'result' => $result,
	)));
}

if ($action === 'skip_source') {
	$sourceId = max(0, (int) ($_POST['source_id'] ?? $_GET['source_id'] ?? 0));
	$hours = max(1, min(168, (int) ($_POST['hours'] ?? 24)));
	if ($sourceId <= 0) {
		exit(json_encode(array('ok' => false, 'message' => 'source_id required')));
	}
	$ok = epc_disc_source_set_skip($pdo, $sourceId, $siteKey, $hours);
	exit(json_encode(array(
		'ok' => $ok,
		'message' => $ok ? "Source skipped for {$hours}h" : 'Source not found',
		'source_id' => $sourceId,
	)));
}

if ($action === 'discover_counts') {
	$discFilters = epc_disc_default_discover_filters($pdo, $siteKey, array(
		'taxonomy_id' => max(0, (int) ($_POST['taxonomy_id'] ?? $_GET['taxonomy_id'] ?? 0)),
		'view' => (string) ($_POST['view'] ?? $_GET['view'] ?? ''),
		'sort' => (string) ($_POST['sort'] ?? $_GET['sort'] ?? 'newest'),
	));
	exit(json_encode(array(
		'ok' => true,
		'counts' => epc_disc_discover_counts($pdo, $siteKey, $discFilters),
	)));
}

if ($action === 'list_discover_queue') {
	$page = max(1, (int) ($_POST['page'] ?? $_GET['page'] ?? 1));
	$perPage = max(1, min(40, (int) ($_POST['per_page'] ?? $_GET['per_page'] ?? 20)));
	$discFilters = epc_disc_default_discover_filters($pdo, $siteKey, array(
		'taxonomy_id' => max(0, (int) ($_POST['taxonomy_id'] ?? $_GET['taxonomy_id'] ?? 0)),
		'view' => (string) ($_POST['view'] ?? $_GET['view'] ?? 'all_suggestions'),
		'sort' => (string) ($_POST['sort'] ?? $_GET['sort'] ?? 'newest'),
		'limit' => $perPage * $page,
	));
	$all = epc_disc_queue_list_for_discover($pdo, $siteKey, $discFilters);
	$offset = ($page - 1) * $perPage;
	$slice = array_slice($all, $offset, $perPage);
	exit(json_encode(array(
		'ok' => true,
		'page' => $page,
		'per_page' => $perPage,
		'total' => count($all),
		'items' => $slice,
	)));
}

if ($action === 'warehouse_market_match') {
	if (!function_exists('epc_apai_bg_start')) {
		exit(json_encode(array('ok' => false, 'message' => 'Background jobs unavailable')));
	}
	$jobId = epc_apai_bg_start($pdo, $siteKey, 'warehouse_market_match', array());
	exit(json_encode(array(
		'ok' => true,
		'background' => true,
		'job_id' => $jobId,
		'job_type' => 'warehouse_market_match',
		'message' => 'Warehouse match queued',
	)));
}

if ($action === 'match_catalogue_market') {
	$res = function_exists('epc_disc_match_catalogue_to_market')
		? epc_disc_match_catalogue_to_market($pdo, $siteKey)
		: array('ok' => false, 'items' => array(), 'count' => 0, 'message' => 'Not available');
	exit(json_encode(array_merge(array('ok' => !empty($res['ok'])), $res)));
}

if ($action === 'bulk_approve') {
	$rawIds = $_POST['queue_ids'] ?? array();
	if (!is_array($rawIds)) {
		$rawIds = array($rawIds);
	}
	$queueIds = array();
	foreach ($rawIds as $id) {
		$id = (int) $id;
		if ($id > 0) {
			$queueIds[] = $id;
		}
	}
	if (!$queueIds) {
		exit(json_encode(array('ok' => false, 'message' => 'No items selected')));
	}
	$overridesRaw = $_POST['category_overrides'] ?? '';
	$overrides = array();
	if (is_string($overridesRaw) && $overridesRaw !== '') {
		$decoded = json_decode($overridesRaw, true);
		if (is_array($decoded)) {
			$overrides = $decoded;
		}
	} elseif (is_array($overridesRaw)) {
		$overrides = $overridesRaw;
	}
	try {
		$res = epc_disc_bulk_approve($pdo, $siteKey, $queueIds, array('category_overrides' => $overrides));
	} catch (Throwable $e) {
		http_response_code(500);
		exit(json_encode(array(
			'ok' => false,
			'message' => 'Bulk import failed: ' . $e->getMessage(),
		)));
	}
	exit(json_encode(array(
		'ok' => !empty($res['ok']),
		'imported' => (int) ($res['imported'] ?? 0),
		'failed' => (int) ($res['failed'] ?? 0),
		'message' => (string) ($res['message'] ?? ''),
		'results' => (array) ($res['results'] ?? array()),
	)));
}

if ($action === 'advise_category') {
	$queueId = max(0, (int) ($_POST['queue_id'] ?? $_GET['queue_id'] ?? 0));
	if ($queueId <= 0) {
		exit(json_encode(array('ok' => false, 'message' => 'queue_id required')));
	}
	$stmt = $pdo->prepare('SELECT * FROM `epc_product_discovery_queue` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
	$stmt->execute(array($queueId, $siteKey));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		exit(json_encode(array('ok' => false, 'message' => 'Queue item not found')));
	}
	$advisory = epc_apai_advise_category($pdo, $siteKey, $row);
	exit(json_encode(array_merge(array('ok' => true, 'queue_id' => $queueId), $advisory)));
}

if ($action === 'list_categories') {
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['industry_key'] ?? $_GET['industry_key'] ?? ''))));
	$cats = epc_apai_list_industry_categories($pdo, $siteKey, $industryKey);
	exit(json_encode(array(
		'ok' => true,
		'categories' => $cats,
		'industry_key' => $industryKey !== '' ? $industryKey : epc_apai_resolve_industry($pdo, $siteKey),
	)));
}

if ($action === 'list_my_imports') {
	$filter = (string) ($_POST['filter'] ?? $_GET['filter'] ?? 'new');
	$data = epc_disc_queue_list_for_imports($pdo, $siteKey, array(
		'filter' => $filter,
		'limit' => max(1, min(200, (int) ($_POST['limit'] ?? $_GET['limit'] ?? 60))),
	));
	exit(json_encode(array_merge(array('ok' => true), $data)));
}

if ($action === 'dismiss_duplicate') {
	$keepId = max(0, (int) ($_POST['keep_id'] ?? 0));
	$dismissRaw = $_POST['dismiss_ids'] ?? array();
	if (!is_array($dismissRaw)) {
		$dismissRaw = array($dismissRaw);
	}
	$dismissIds = array();
	foreach ($dismissRaw as $did) {
		$did = (int) $did;
		if ($did > 0) {
			$dismissIds[] = $did;
		}
	}
	$approveKeep = !isset($_POST['approve_keep']) || !empty($_POST['approve_keep']);
	try {
		$res = epc_disc_queue_dismiss_duplicates($pdo, $siteKey, $keepId, $dismissIds, $approveKeep);
		$counts = epc_disc_imports_counts($pdo, $siteKey);
		exit(json_encode(array_merge(array('ok' => !empty($res['ok']), 'counts' => $counts), $res)));
	} catch (Throwable $e) {
		exit(json_encode(array('ok' => false, 'message' => $e->getMessage())));
	}
}

if ($action === 'shell_kpi') {
	$kpiCacheDir = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/_cache/apai_kpi';
	$kpiCacheFile = $kpiCacheDir . '/' . preg_replace('/[^a-z0-9_]/', '_', $siteKey) . '.json';
	if (is_file($kpiCacheFile) && (time() - (int) filemtime($kpiCacheFile)) < 60) {
		$cachedKpi = json_decode((string) file_get_contents($kpiCacheFile), true);
		if (is_array($cachedKpi) && !empty($cachedKpi['kpi'])) {
			exit(json_encode(array('ok' => true, 'kpi' => $cachedKpi['kpi'], 'cached' => true)));
		}
	}
	$kpi = epc_disc_kpi($pdo, $siteKey);
	$industryKey = function_exists('epc_apai_resolve_industry')
		? epc_apai_resolve_industry($pdo, $siteKey)
		: (string) ($kpi['industry_key'] ?? 'general_retail');
	$catCount = function_exists('epc_apai_category_count') ? epc_apai_category_count($pdo, $siteKey, $industryKey) : 0;
	$indProfiles = function_exists('epc_apai_industry_profiles') ? epc_apai_industry_profiles() : array();
	$industryLabel = (string) (($indProfiles[$industryKey]['label'] ?? ucfirst(str_replace('_', ' ', $industryKey))));
	$kpiOut = array(
		'suggested' => (int) ($kpi['suggested'] ?? 0),
		'imported' => (int) ($kpi['imported'] ?? 0),
		'tax_count' => (int) ($kpi['taxonomy_nodes'] ?? 0),
		'category_map_count' => (int) $catCount,
		'industry_label' => $industryLabel,
	);
	if (!is_dir($kpiCacheDir)) {
		@mkdir($kpiCacheDir, 0755, true);
	}
	@file_put_contents($kpiCacheFile, json_encode(array('kpi' => $kpiOut, 'at' => time()), JSON_UNESCAPED_UNICODE), LOCK_EX);
	exit(json_encode(array(
		'ok' => true,
		'kpi' => $kpiOut,
	)));
}

if ($action === 'load_tab_html') {
	@set_time_limit(30);
	header('Content-Type: application/json; charset=utf-8');
	$tabRaw = $_POST['tab'] ?? $_GET['tab'] ?? 'discover';
	$tabKey = preg_replace('/[^a-z_]/', '', strtolower(trim((string) $tabRaw)));
	$tabAliases = array(
		'discovery' => 'discover',
		'taxonomy' => 'product_lines',
		'disc_sources' => 'uae_sources',
		'market_sources' => 'uae_sources',
		'settings' => 'rules',
		'dashboard' => 'discover',
		'my_imports' => 'imports',
	);
	if (isset($tabAliases[$tabKey])) {
		$tabKey = $tabAliases[$tabKey];
	}
	if ($tabKey === '') {
		$tabKey = 'discover';
	}
	$_GET['tab'] = $tabKey;
	$_GET['site_key'] = $siteKey;
	$_GET['apai_partial'] = '1';
	if (in_array($tabKey, array('discover', 'product_lines', 'guide', 'compare'), true)) {
		$_GET['fast_partial'] = '1';
	}
	$cacheExtra = array();
	foreach (array('view', 'taxonomy_id', 'disc_sort', 'imports_filter', 'filter_taxonomy_id', 'pl_page') as $epcApaiQ) {
		if (isset($_POST[$epcApaiQ]) && (string) $_POST[$epcApaiQ] !== '') {
			$_GET[$epcApaiQ] = (string) $_POST[$epcApaiQ];
			$cacheExtra[$epcApaiQ] = (string) $_POST[$epcApaiQ];
		}
	}
	$tabCacheDir = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/_cache/apai_tabs';
	$tabCacheKey = preg_replace('/[^a-z0-9_\-]/', '_', $siteKey . '_' . $tabKey . ($tabKey === 'product_lines' ? '_v2' : '') . '_' . md5(json_encode($cacheExtra)));
	$tabCacheFile = $tabCacheDir . '/' . $tabCacheKey . '.html';
	$tabCacheTtl = ($tabKey === 'product_lines') ? 900 : 60;
	if (is_file($tabCacheFile) && (time() - (int) filemtime($tabCacheFile)) < $tabCacheTtl) {
		$cachedHtml = (string) file_get_contents($tabCacheFile);
		if (trim($cachedHtml) !== '') {
			exit(json_encode(array(
				'ok' => true,
				'html' => $cachedHtml,
				'tab' => $tabKey,
				'cached' => true,
			), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
		}
	}
	$enginePath = $_SERVER['DOCUMENT_ROOT'] . '/' . trim((string) ($DP_Config->backend_dir ?? 'cp'), '/') . '/content/control/portal/epc_auto_price_engine.php';
	ob_start();
	try {
		if (!is_file($enginePath)) {
			throw new RuntimeException('Auto Price AI engine stub missing');
		}
		include $enginePath;
		$html = (string) ob_get_clean();
		if (trim($html) === '') {
			throw new RuntimeException('Tab rendered empty (check PHP error log)');
		}
		if (preg_match('/Fatal error|Parse error|Uncaught/i', $html)) {
			throw new RuntimeException('Tab render failed — see server error log');
		}
		if (!is_dir($tabCacheDir)) {
			@mkdir($tabCacheDir, 0755, true);
		}
		@file_put_contents($tabCacheFile, $html, LOCK_EX);
		exit(json_encode(array(
			'ok' => true,
			'html' => $html,
			'tab' => $tabKey,
		), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
	} catch (Throwable $e) {
		if (ob_get_level()) {
			ob_end_clean();
		}
		http_response_code(500);
		exit(json_encode(array(
			'ok' => false,
			'error' => $e->getMessage(),
			'tab' => $tabKey,
		), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
	}
}

if ($action === 'product_line_prices') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_product_line_rankings.php';
	$taxonomyId = max(0, (int) ($_POST['taxonomy_id'] ?? $_GET['taxonomy_id'] ?? 0));
	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	$prices = epc_apai_product_line_market_prices($pdo, $siteKey, $taxonomyId, $industryKey);
	$label = '—';
	$pmin = (float) ($prices['price_min'] ?? 0);
	$pmax = (float) ($prices['price_max'] ?? 0);
	$cur = (string) ($prices['currency'] ?? 'AED');
	if ($pmin > 0 && $pmax > 0) {
		$label = number_format($pmin, 0) . '–' . number_format($pmax, 0) . ' ' . $cur;
	} elseif ($pmin > 0) {
		$label = 'from ' . number_format($pmin, 0) . ' ' . $cur;
	}
	exit(json_encode(array(
		'ok' => true,
		'taxonomy_id' => $taxonomyId,
		'label' => $label,
		'price_min' => $pmin,
		'price_max' => $pmax,
		'currency' => $cur,
	), JSON_UNESCAPED_UNICODE));
}

if ($action === 'product_lines_tax_tree') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_product_line_rankings.php';
	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	$taxTree = epc_apai_tax_list_tree($pdo, $industryKey);
	$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	$pageBase = '/' . $backend . '/control/portal/epc_auto_price_engine';
	ob_start();
	echo '<p class="text-muted" style="font-size:12px">Full industry taxonomy tree loaded.</p>';
	echo '<form method="post" style="margin-bottom:12px">';
	echo '<input type="hidden" name="epc_ape_action" value="sync_categories" />';
	echo '<input type="hidden" name="site_key" value="' . htmlspecialchars($siteKey, ENT_QUOTES, 'UTF-8') . '" />';
	echo '<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-sitemap"></i> Sync categories to catalogue</button> ';
	echo '<a class="btn btn-default btn-sm" href="/' . htmlspecialchars($backend, ENT_QUOTES, 'UTF-8') . '/shop/catalogue/products">Open CP catalogue</a>';
	echo '</form>';
	$renderTax = function ($nodes, $depth = 0) use (&$renderTax, $pageBase, $siteKey) {
		if (!$nodes) {
			return;
		}
		echo '<ul class="epc-tax-tree__list" data-depth="' . (int) $depth . '">';
		foreach ($nodes as $n) {
			echo '<li><span class="epc-tax-tree__name">' . htmlspecialchars((string) ($n['name_en'] ?? ''), ENT_QUOTES, 'UTF-8') . '</span> ';
			echo '<code class="epc-tax-tree__slug">' . htmlspecialchars((string) ($n['slug'] ?? ''), ENT_QUOTES, 'UTF-8') . '</code>';
			echo ' <form method="post" class="epc-tax-tree__action epc-pl-inline-form" style="display:inline">';
			echo '<input type="hidden" name="epc_ape_action" value="run_discovery" />';
			echo '<input type="hidden" name="site_key" value="' . htmlspecialchars($siteKey, ENT_QUOTES, 'UTF-8') . '" />';
			echo '<input type="hidden" name="taxonomy_slug" value="' . htmlspecialchars((string) ($n['slug'] ?? ''), ENT_QUOTES, 'UTF-8') . '" />';
			echo '<input type="hidden" name="return_tab" value="discover" />';
			echo '<button type="submit" class="btn btn-xs btn-primary">Discover</button></form>';
			if (!empty($n['children'])) {
				$renderTax($n['children'], $depth + 1);
			}
			echo '</li>';
		}
		echo '</ul>';
	};
	$renderTax($taxTree);
	$html = (string) ob_get_clean();
	exit(json_encode(array('ok' => true, 'html' => $html), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
}

if ($action === 'warehouse_list') {
	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	if ($industryKey !== 'auto_parts') {
		exit(json_encode(array('ok' => false, 'message' => 'Warehouse list is for auto parts tenants only')));
	}
	$page = max(1, (int) ($_POST['page'] ?? $_GET['page'] ?? 1));
	$perPage = max(1, min(50, (int) ($_POST['per_page'] ?? $_GET['per_page'] ?? 50)));
	$filters = array(
		'page' => $page,
		'per_page' => $perPage,
		'price_list_id' => max(0, (int) ($_POST['price_list_id'] ?? $_GET['price_list_id'] ?? 0)),
		'search' => trim((string) ($_POST['search'] ?? $_GET['search'] ?? '')),
	);
	$total = epc_disc_warehouse_list_count($pdo, $filters);
	$items = epc_disc_warehouse_list($pdo, $filters);
	$priceLists = epc_disc_warehouse_price_list_options($pdo, $siteKey);
	exit(json_encode(array(
		'ok' => true,
		'page' => $page,
		'per_page' => $perPage,
		'total' => $total,
		'items' => $items,
		'price_lists' => $priceLists,
		'message' => number_format($total) . ' warehouse items — select up to 10 to compare with market',
	), JSON_UNESCAPED_UNICODE));
}

if ($action === 'warehouse_compare_selected') {
	$industryKey = function_exists('epc_apai_resolve_industry') ? epc_apai_resolve_industry($pdo, $siteKey) : 'general_retail';
	if ($industryKey !== 'auto_parts') {
		exit(json_encode(array('ok' => false, 'message' => 'Warehouse compare is for auto parts tenants only')));
	}
	$rawKeys = $_POST['keys'] ?? $_POST['brand_article_keys'] ?? array();
	if (!is_array($rawKeys)) {
		$rawKeys = array($rawKeys);
	}
	$keys = array();
	foreach ($rawKeys as $k) {
		$k = strtolower(trim((string) $k));
		if ($k !== '' && strpos($k, ':') !== false) {
			$keys[$k] = $k;
		}
	}
	$keys = array_values($keys);
	if (!$keys) {
		exit(json_encode(array('ok' => false, 'message' => 'Select at least one warehouse item')));
	}
	if (count($keys) > 10) {
		exit(json_encode(array('ok' => false, 'message' => 'Select at most 10 items to compare with market')));
	}
	$rowContext = array();
	$rawCtx = $_POST['context'] ?? array();
	if (is_string($rawCtx)) {
		$decoded = json_decode($rawCtx, true);
		$rawCtx = is_array($decoded) ? $decoded : array();
	}
	if (is_array($rawCtx)) {
		foreach ($rawCtx as $ctxKey => $ctxRow) {
			if (is_array($ctxRow)) {
				$rowContext[strtolower(trim((string) $ctxKey))] = $ctxRow;
			}
		}
	}
	$active = epc_disc_warehouse_compare_job_active($pdo, $siteKey);
	if ($active) {
		exit(json_encode(array(
			'ok' => false,
			'message' => 'A warehouse compare job is already running',
			'job_id' => (int) ($active['id'] ?? 0),
		)));
	}
	$jobId = epc_disc_warehouse_compare_job_enqueue($pdo, $siteKey, $keys, $rowContext);
	if ($jobId <= 0) {
		exit(json_encode(array('ok' => false, 'message' => 'Could not start compare job')));
	}
	$step = epc_disc_warehouse_compare_job_step($pdo, $siteKey, $jobId);
	exit(json_encode(array(
		'ok' => true,
		'job_id' => $jobId,
		'status' => (string) ($step['status'] ?? 'running'),
		'done' => (int) ($step['done'] ?? 0),
		'total' => (int) ($step['total'] ?? count($keys)),
		'results' => (array) ($step['results'] ?? array()),
		'message' => (string) ($step['message'] ?? 'Compare job started'),
	), JSON_UNESCAPED_UNICODE));
}

if ($action === 'job_status') {
	$jobId = max(0, (int) ($_POST['job_id'] ?? $_GET['job_id'] ?? 0));
	$jobType = trim((string) ($_POST['job_type'] ?? $_GET['job_type'] ?? 'warehouse_compare'));
	if ($jobType === 'warehouse_compare' && function_exists('epc_disc_warehouse_compare_job_get')) {
		$job = $jobId > 0
			? epc_disc_warehouse_compare_job_get($pdo, $jobId, $siteKey)
			: epc_disc_warehouse_compare_job_active($pdo, $siteKey);
		if (!$job) {
			$recent = function_exists('epc_disc_warehouse_market_recent')
				? epc_disc_warehouse_market_recent($pdo, $siteKey, 10)
				: array();
			exit(json_encode(array(
				'ok' => true,
				'status' => 'idle',
				'message' => 'No compare job in progress',
				'recent' => $recent,
			), JSON_UNESCAPED_UNICODE));
		}
		$status = (string) ($job['status'] ?? 'pending');
		if (in_array($status, array('pending', 'running'), true)) {
			$step = epc_disc_warehouse_compare_job_step($pdo, $siteKey, (int) ($job['id'] ?? 0));
			if (is_array($step)) {
				exit(json_encode(array_merge(array('ok' => true), $step), JSON_UNESCAPED_UNICODE));
			}
		}
		$result = json_decode((string) ($job['result_json'] ?? ''), true);
		if (!is_array($result)) {
			$opts = json_decode((string) ($job['options_json'] ?? ''), true);
			$result = is_array($opts) ? array(
				'done' => (int) ($opts['done'] ?? 0),
				'total' => (int) ($opts['total'] ?? 0),
				'results' => (array) ($opts['results'] ?? array()),
			) : array();
		}
		exit(json_encode(array_merge(array(
			'ok' => true,
			'status' => $status === 'done' ? 'done' : $status,
			'job_id' => (int) ($job['id'] ?? 0),
		), $result), JSON_UNESCAPED_UNICODE));
	}
	$jobId = max(0, (int) ($_POST['job_id'] ?? $_GET['job_id'] ?? 0));
	$job = $jobId > 0 ? epc_disc_crawl_job_get($pdo, $jobId, $siteKey) : epc_disc_crawl_job_active($pdo, $siteKey);
	if (!$job) {
		exit(json_encode(array('ok' => true, 'status' => 'idle', 'message' => 'No job in progress')));
	}
	$result = json_decode((string) ($job['result_json'] ?? ''), true);
	if (!is_array($result)) {
		$result = array();
	}
	if ((string) ($job['status'] ?? '') === 'pending') {
		$processed = epc_disc_crawl_job_process_one($pdo, $siteKey);
		if ($processed) {
			exit(json_encode(array_merge(array('ok' => true, 'status' => 'done'), $processed)));
		}
	}
	exit(json_encode(array(
		'ok' => true,
		'status' => (string) ($job['status'] ?? 'pending'),
		'job_id' => (int) ($job['id'] ?? 0),
		'mode' => (string) ($job['mode'] ?? 'full'),
		'message' => (string) ($result['message'] ?? 'Job in progress…'),
		'result' => $result,
	)));
}

if ($action === 'country_info') {
	$cc = epc_apai_tenant_country($siteKey, $pdo);
	$meta = epc_apai_country_meta($cc);
	$sources = epc_apai_country_sources_for_tenant($pdo, $siteKey);
	exit(json_encode(array(
		'ok' => true,
		'country_code' => $cc,
		'country_label' => (string) ($meta['label'] ?? $cc),
		'tld' => (string) ($meta['tld'] ?? 'ae'),
		'currency' => (string) ($meta['currency'] ?? 'AED'),
		'source_count' => count($sources),
		'sources' => array_slice($sources, 0, 12),
	)));
}

http_response_code(400);
echo json_encode(array('ok' => false, 'message' => 'Unknown action: ' . $action));
