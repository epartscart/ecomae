<?php
/**
 * Verify warehouse price list vs market matching for auto_parts tenants.
 * GET /epc-warehouse-market-verify.php?token=…&site_key=epartscart&run=1
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
require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
$runMatch = !empty($_GET['run']) && (string) $_GET['run'] === '1';
$runWhOnly = !empty($_GET['wh_only']) && (string) $_GET['wh_only'] === '1';
$linkStorages = !empty($_GET['link']) && (string) $_GET['link'] === '1';

$host = strtolower(preg_replace('/:\d+$/', '', trim((string) ($_SERVER['HTTP_HOST'] ?? ''))));
$isSuperCpHost = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$tenantHostMap = array(
	'epartscart' => array('www.epartscart.com', 'epartscart.com'),
	'electronicae' => array('www.electronicae.com', 'electronicae.com'),
	'taxofinca' => array('www.taxofinca.com', 'taxofinca.com'),
	'stylenlook' => array('www.stylenlook.com', 'stylenlook.com'),
	'thejewellerytrend' => array('www.thejewellerytrend.com', 'thejewellerytrend.com'),
);
$onTenantDocroot = false;
foreach ($tenantHostMap as $sk => $hosts) {
	foreach ($hosts as $h) {
		if ($host === $h || str_ends_with($host, '.' . $h)) {
			if ($siteKey === '' || $siteKey === $sk) {
				$siteKey = $sk;
			}
			$onTenantDocroot = true;
			break 2;
		}
	}
}

$pdo = null;
$resolvedDb = (string) ($cfg->db ?? '');
if ($onTenantDocroot && !$isSuperCpHost) {
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		http_response_code(500);
		exit(json_encode(array('ok' => false, 'error' => 'tenant_db_connect_failed', 'message' => $e->getMessage())));
	}
} else {
	$platformPdo = epc_portal_platform_pdo();
	if (!$platformPdo instanceof PDO) {
		http_response_code(500);
		exit(json_encode(array('ok' => false, 'error' => 'Platform registry unavailable')));
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
		exit(json_encode(array('ok' => false, 'error' => 'tenant_not_found', 'site_key' => $siteKey)));
	}

	$pdo = epc_auto_price_setup_connect(array(
		'db' => (string) ($row['db_name'] ?? ''),
		'user' => (string) ($row['db_user'] ?? ''),
		'pass' => (string) ($row['db_password'] ?? ''),
	), $cfg);
	$resolvedDb = (string) ($row['db_name'] ?? '');
}
if (!$pdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'db_connect_failed')));
}

epc_ape_ensure_schema($pdo);
epc_disc_ensure_schema($pdo);

$linkResult = null;
if ($linkStorages && function_exists('epc_apai_link_warehouse_price_lists')) {
	$linkResult = epc_apai_link_warehouse_price_lists($pdo, $siteKey);
}

$storagesOut = array();
try {
	$sq = $pdo->query(
		'SELECT s.`id`, s.`name`, s.`short_name`, s.`interface_type`, s.`connection_options`,
		        p.`id` AS `resolved_price_id`, p.`name` AS `resolved_price_name`
		 FROM `shop_storages` s
		 LEFT JOIN `shop_docpart_prices` p ON p.`id` = CAST(JSON_UNQUOTE(JSON_EXTRACT(s.`connection_options`, "$.price_id")) AS UNSIGNED)
		 WHERE s.`hidden` = 0
		 ORDER BY s.`id`'
	);
	while ($srow = $sq->fetch(PDO::FETCH_ASSOC)) {
		$co = json_decode((string) ($srow['connection_options'] ?? ''), true);
		$storagesOut[] = array(
			'id' => (int) ($srow['id'] ?? 0),
			'name' => (string) ($srow['name'] ?? ''),
			'short_name' => (string) ($srow['short_name'] ?? ''),
			'interface_type' => (int) ($srow['interface_type'] ?? 0),
			'connection_price_id' => is_array($co) && !empty($co['price_id']) ? (int) $co['price_id'] : 0,
			'resolved_price_id' => (int) ($srow['resolved_price_id'] ?? 0),
			'resolved_price_name' => (string) ($srow['resolved_price_name'] ?? ''),
		);
	}
} catch (Throwable $e) {
	$storagesOut = array('error' => $e->getMessage());
}

$priceListsOut = array();
try {
	$priceListsOut = $pdo->query(
		'SELECT p.`id`, p.`name`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `rows`
		 FROM `shop_docpart_prices` p ORDER BY p.`id`'
	)->fetchAll(PDO::FETCH_ASSOC) ?: array();
} catch (Throwable $e) {
	$priceListsOut = array(array('error' => $e->getMessage()));
}

$sampleParts = array('C110', 'C110J', 'DT068');
$sampleRows = array();
foreach ($sampleParts as $part) {
	try {
		$stmt = $pdo->prepare(
			'SELECT d.`price_id`, d.`manufacturer`, d.`article`, d.`article_show`, d.`name`, d.`exist`, d.`price`, p.`name` AS `price_list_name`
			 FROM `shop_docpart_prices_data` d
			 LEFT JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
			 WHERE UPPER(REPLACE(REPLACE(REPLACE(COALESCE(NULLIF(TRIM(d.`article_show`), \'\'), TRIM(d.`article`)), \' \', \'\'), \'-\', \'\'), \'.\', \'\')) LIKE ?
			   AND IFNULL(d.`price`, 0) > 0
			 ORDER BY IFNULL(d.`exist`, 0) DESC, d.`price` ASC
			 LIMIT 5'
		);
		$norm = strtoupper(preg_replace('/[\s\-\.]+/', '', $part));
		$stmt->execute(array('%' . $norm . '%'));
		$sampleRows[$part] = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	} catch (Throwable $e) {
		$sampleRows[$part] = array(array('error' => $e->getMessage()));
	}
}

$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
$tenantCfg = epc_ape_tenant_config_get($pdo, $siteKey);
$warehouseMaps = function_exists('epc_disc_resolve_warehouse_price_lists')
	? epc_disc_resolve_warehouse_price_lists($pdo, $siteKey)
	: (function_exists('epc_disc_warehouse_price_lists_for_tenant')
		? epc_disc_warehouse_price_lists_for_tenant($pdo, $siteKey)
		: array());
$whMatrix = epc_ape_warehouse_matrix($pdo);
$whMarketCounts = function_exists('epc_disc_warehouse_market_counts')
	? epc_disc_warehouse_market_counts($pdo, $siteKey)
	: array();

$matchResult = null;
$warehouseMatchResult = null;
$crossResult = null;
if (($runMatch || $runWhOnly) && $industryKey === 'auto_parts') {
	if ($runMatch && !$runWhOnly) {
		$crossResult = epc_disc_cross_source_match($pdo, $siteKey);
		$matchResult = function_exists('epc_disc_match_catalogue_to_market')
			? epc_disc_match_catalogue_to_market($pdo, $siteKey)
			: null;
	}
	if (function_exists('epc_disc_match_warehouse_to_market')) {
		$warehouseMatchResult = epc_disc_match_warehouse_to_market($pdo, $siteKey);
		$whMarketCounts = epc_disc_warehouse_market_counts($pdo, $siteKey);
	}
}

$pricesDataCount = 0;
try {
	$pricesDataCount = (int) $pdo->query('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE IFNULL(`price`, 0) > 0')->fetchColumn();
} catch (Throwable $e) {
}
$warehouseListTotal = function_exists('epc_disc_warehouse_list_count')
	? epc_disc_warehouse_list_count($pdo, array())
	: 0;
$warehouseListSample = function_exists('epc_disc_warehouse_list')
	? epc_disc_warehouse_list($pdo, array('page' => 1, 'per_page' => 5))
	: array();
$warehousePriceListOptions = function_exists('epc_disc_warehouse_price_list_options')
	? epc_disc_warehouse_price_list_options($pdo, $siteKey)
	: array();

echo json_encode(array(
	'ok' => true,
	'site_key' => $siteKey,
	'db' => $resolvedDb,
	'industry_key' => $industryKey,
	'profile' => (string) ($tenantCfg['profile'] ?? ''),
	'functions' => array(
		'epc_disc_resolve_warehouse_price_lists' => function_exists('epc_disc_resolve_warehouse_price_lists'),
		'epc_disc_warehouse_price_lists_for_tenant' => function_exists('epc_disc_warehouse_price_lists_for_tenant'),
		'epc_apai_link_warehouse_price_lists' => function_exists('epc_apai_link_warehouse_price_lists'),
		'epc_disc_cross_source_match' => function_exists('epc_disc_cross_source_match'),
		'epc_disc_match_warehouse_to_market' => function_exists('epc_disc_match_warehouse_to_market'),
		'epc_disc_match_catalogue_to_market' => function_exists('epc_disc_match_catalogue_to_market'),
		'epc_disc_warehouse_list' => function_exists('epc_disc_warehouse_list'),
		'epc_disc_warehouse_compare_job_enqueue' => function_exists('epc_disc_warehouse_compare_job_enqueue'),
	),
	'storages' => $storagesOut,
	'price_lists' => $priceListsOut,
	'prices_data_rows' => $pricesDataCount,
	'warehouse_list_total' => $warehouseListTotal,
	'warehouse_list_sample' => $warehouseListSample,
	'warehouse_price_list_options' => $warehousePriceListOptions,
	'warehouse_maps' => $warehouseMaps,
	'warehouse_matrix' => $whMatrix,
	'warehouse_matrix_count' => count($whMatrix),
	'warehouse_market_counts' => $whMarketCounts,
	'sample_parts' => $sampleRows,
	'link_storages' => $linkResult,
	'run_match' => $runMatch,
	'cross_source_match' => $crossResult,
	'catalogue_market_match' => $matchResult,
	'warehouse_market_match' => $warehouseMatchResult,
	'ui' => array(
		'discover_tab' => '/cp/control/portal/epc_auto_price_engine?site_key=' . $siteKey . '&tab=discover',
		'compare_tab' => '/cp/control/portal/epc_auto_price_engine?site_key=' . $siteKey . '&tab=compare',
	),
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
