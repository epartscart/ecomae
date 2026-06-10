<?php
/**
 * Auto Price AI — end-to-end import → supplier → margin verification probe.
 *
 * HTTP: ?token=…&site_key=epartscart|electronicae|all&simulate=0|1&dry_run=1
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
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_fulfillment.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$platformPdo = epc_portal_platform_pdo();

$targetKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'all'))));
$simulate = !empty($_GET['simulate']);
$dryRun = !empty($_GET['dry_run']);
$backfill = !empty($_GET['backfill']);
$fixStorefront = !empty($_GET['fix_storefront']);

$out = array(
	'probe' => 'epc-apai-e2e-flow-verify',
	'timestamp' => date('c'),
	'simulate_import' => $simulate,
	'dry_run' => $dryRun,
	'backfill' => $backfill,
	'fix_storefront' => $fixStorefront,
	'tenants' => array(),
	'overall_pass' => true,
);

/**
 * @param array<string,mixed> $checks
 */
function epc_apai_e2e_check(array &$checks, string $key, bool $ok, string $detail = ''): void
{
	$checks[$key] = array('pass' => $ok, 'detail' => $detail);
}

foreach (epc_portal_list_tenants($platformPdo) as $row) {
	if ((string) ($row['status'] ?? '') !== 'live') {
		continue;
	}
	$siteKey = (string) ($row['site_key'] ?? '');
	if ($targetKey !== '' && $targetKey !== 'all' && $siteKey !== $targetKey) {
		continue;
	}
	if (!in_array($siteKey, array('epartscart', 'electronicae'), true) && $targetKey === 'all') {
		continue;
	}

	$cred = epc_portal_tenant_setup_credentials($row);
	$tenantOut = array(
		'site_key' => $siteKey,
		'hostname' => (string) ($row['hostname'] ?? ''),
		'checks' => array(),
		'pass' => true,
		'sample_import' => null,
		'simulate_result' => null,
	);

	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . (string) $cred['db'] . ';charset=utf8',
			(string) ($cred['user'] ?: $cfg->user),
			(string) ($cred['pass'] ?: $cfg->password),
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		epc_apai_e2e_check($tenantOut['checks'], 'db_connect', false, $e->getMessage());
		$tenantOut['pass'] = false;
		$out['tenants'][$siteKey] = $tenantOut;
		$out['overall_pass'] = false;
		continue;
	}

	epc_ape_ensure_schema($pdo);
	$profile = (string) (epc_ape_tenant_config_get($pdo, $siteKey)['profile'] ?? '');
	$industry = epc_apai_resolve_industry($pdo, $siteKey);
	epc_apai_e2e_check($tenantOut['checks'], 'profile_set', $profile !== '', $profile);
	epc_apai_e2e_check($tenantOut['checks'], 'industry_set', $industry !== '', $industry);

	if ($backfill && function_exists('epc_apai_backfill_imported_fulfillment')) {
		$tenantOut['backfill'] = epc_apai_backfill_imported_fulfillment($pdo, $siteKey, 20);
	}
	if ($fixStorefront && function_exists('epc_apai_backfill_storefront_prices')) {
		$tenantOut['storefront_price_backfill'] = epc_apai_backfill_storefront_prices($pdo, $siteKey, 50);
	}

	$importStmt = $pdo->prepare(
		'SELECT q.*, scp.`published_flag`, scp.`alias`
		 FROM `epc_product_discovery_queue` q
		 LEFT JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id`
		 WHERE q.`site_key` = ? AND q.`status` = \'imported\' AND q.`product_id` > 0
		 ORDER BY q.`id` DESC LIMIT 1'
	);
	$importStmt->execute(array($siteKey));
	$importRow = $importStmt->fetch(PDO::FETCH_ASSOC);

	if ($importRow) {
		$pid = (int) ($importRow['product_id'] ?? 0);
		$meta = json_decode((string) ($importRow['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}

		epc_apai_e2e_check($tenantOut['checks'], 'catalogue_product_exists', $pid > 0, 'product_id=' . $pid);
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'catalogue_published',
			(int) ($importRow['published_flag'] ?? 0) === 1,
			'alias=' . (string) ($importRow['alias'] ?? '')
		);

		$hasSupplierMeta = !empty($meta['apai_supplier_name']) || !empty($meta['apai_supplier_id']);
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'import_supplier_linked',
			$hasSupplierMeta,
			'supplier=' . (string) ($meta['apai_supplier_name'] ?? '') . ' id=' . (int) ($meta['apai_supplier_id'] ?? 0)
		);

		$cost = (float) ($meta['apai_cost'] ?? $meta['import_warehouse_cost'] ?? 0);
		$sell = (float) ($meta['apai_sell_price'] ?? 0);
		epc_apai_e2e_check($tenantOut['checks'], 'import_cost_set', $cost > 0, (string) $cost);
		epc_apai_e2e_check($tenantOut['checks'], 'import_margin_meta', isset($meta['apai_margin_pct']) || isset($meta['apai_margin']), 'margin_pct=' . (float) ($meta['apai_margin_pct'] ?? 0));

		$srcProd = $pdo->prepare(
			'SELECT sp.`warehouse_cost`, sp.`meta_json`, ps.`external_ref`, ps.`name`
			 FROM `epc_price_source_products` sp
			 INNER JOIN `epc_price_sources` ps ON ps.`id` = sp.`source_id`
			 WHERE sp.`product_id` = ? AND ps.`site_key` = ?
			 ORDER BY sp.`id` DESC LIMIT 1'
		);
		$srcProd->execute(array($pid, $siteKey));
		$srcRow = $srcProd->fetch(PDO::FETCH_ASSOC);
		$srcMeta = is_array($srcRow) ? json_decode((string) ($srcRow['meta_json'] ?? ''), true) : null;
		if (!is_array($srcMeta)) {
			$srcMeta = array();
		}
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'source_product_linked',
			is_array($srcRow),
			is_array($srcRow) ? ('domain=' . (string) ($srcMeta['supplier_domain'] ?? $srcRow['external_ref'] ?? '')) : 'none'
		);
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'source_product_margin',
			(float) ($srcMeta['warehouse_cost'] ?? $srcRow['warehouse_cost'] ?? 0) > 0,
			'warehouse_cost=' . (float) ($srcMeta['warehouse_cost'] ?? $srcRow['warehouse_cost'] ?? 0)
		);

		$storageCost = $pdo->prepare(
			'SELECT MIN(`price_purchase`) AS `cost`, MIN(`price`) AS `sell`
			 FROM `shop_storages_data` WHERE `product_id` = ? AND `price` > 0'
		);
		$storageCost->execute(array($pid));
		$sd = $storageCost->fetch(PDO::FETCH_ASSOC) ?: array();
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'storage_price_purchase',
			(float) ($sd['cost'] ?? 0) > 0 || $cost > 0,
			'price_purchase=' . (float) ($sd['cost'] ?? 0)
		);

		$storefrontVisible = function_exists('epc_apai_product_storefront_offer_visible')
			? epc_apai_product_storefront_offer_visible($pdo, $pid)
			: false;
		$storefrontStorageId = function_exists('epc_ape_resolve_storefront_storage_id')
			? epc_ape_resolve_storefront_storage_id($pdo)
			: 0;
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'storefront_offer_visible',
			$storefrontVisible,
			'storefront_storage_id=' . $storefrontStorageId . ' sell=' . $sell
		);

		$fulfillment = epc_apai_product_fulfillment_meta($pdo, $siteKey, $pid);
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'product_fulfillment_meta',
			is_array($fulfillment) && !empty($fulfillment['apai_supplier_name']),
			is_array($fulfillment) ? (string) ($fulfillment['apai_fulfillment_source'] ?? '') : 'missing'
		);

		$orderApai = $pdo->prepare(
			'SELECT `id`, `t2_json_params`, `price`, `product_id`
			 FROM `shop_orders_items`
			 WHERE `product_id` = ? AND `t2_json_params` LIKE \'%apai_supplier%\'
			 ORDER BY `id` DESC LIMIT 1'
		);
		$orderApai->execute(array($pid));
		$orderItem = $orderApai->fetch(PDO::FETCH_ASSOC);
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'order_item_apai_meta',
			is_array($orderItem),
			is_array($orderItem) ? ('order_item_id=' . (int) $orderItem['id']) : 'no order yet — add product to cart and checkout to verify'
		);

		$stampJson = is_array($fulfillment) ? epc_apai_order_item_json_params($fulfillment) : '';
		$stampOk = $stampJson !== '' && strpos($stampJson, 'apai_supplier_name') !== false;
		epc_apai_e2e_check(
			$tenantOut['checks'],
			'order_stamp_ready',
			$stampOk,
			$stampOk ? substr($stampJson, 0, 120) : 'fulfillment meta missing'
		);

		$tenantOut['sample_import'] = array(
			'queue_id' => (int) ($importRow['id'] ?? 0),
			'product_id' => $pid,
			'title' => (string) ($importRow['title'] ?? ''),
			'alias' => (string) ($importRow['alias'] ?? ''),
			'apai_supplier' => (string) ($meta['apai_supplier_name'] ?? ''),
			'apai_cost' => $cost,
			'apai_sell' => $sell,
			'apai_margin_pct' => (float) ($meta['apai_margin_pct'] ?? 0),
			'storefront_url' => epc_ape_catalogue_product_url($pdo, $pid),
		);
	} else {
		epc_apai_e2e_check($tenantOut['checks'], 'has_imported_product', false, 'no imported queue row — run Discover → Add to catalogue first');
	}

	if ($simulate && !$dryRun) {
		$suggestStmt = $pdo->prepare(
			'SELECT * FROM `epc_product_discovery_queue`
			 WHERE `site_key` = ? AND `status` = \'suggested\'
			 ORDER BY `id` DESC LIMIT 1'
		);
		$suggestStmt->execute(array($siteKey));
		$suggestRow = $suggestStmt->fetch(PDO::FETCH_ASSOC);
		if ($suggestRow) {
			$res = epc_disc_queue_approve_import($pdo, $siteKey, (int) $suggestRow['id']);
			$tenantOut['simulate_result'] = $res;
			epc_apai_e2e_check($tenantOut['checks'], 'simulate_import', !empty($res['ok']), (string) ($res['message'] ?? ''));
		} else {
			epc_apai_e2e_check($tenantOut['checks'], 'simulate_import', false, 'no suggested queue item');
		}
	} elseif ($simulate && $dryRun) {
		$cnt = (int) $pdo->prepare('SELECT COUNT(*) FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\'')
			->execute(array($siteKey)) ?: 0;
		$cntStmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\'');
		$cntStmt->execute(array($siteKey));
		$cnt = (int) $cntStmt->fetchColumn();
		$tenantOut['simulate_result'] = array('dry_run' => true, 'suggested_count' => $cnt);
		epc_apai_e2e_check($tenantOut['checks'], 'simulate_import_dry', $cnt > 0, 'suggested=' . $cnt);
	}

	foreach ($tenantOut['checks'] as $checkKey => $c) {
		if (empty($c['pass']) && $checkKey === 'order_item_apai_meta') {
			continue;
		}
		if (empty($c['pass'])) {
			$tenantOut['pass'] = false;
			$out['overall_pass'] = false;
		}
	}

	$out['tenants'][$siteKey] = $tenantOut;
}

echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
