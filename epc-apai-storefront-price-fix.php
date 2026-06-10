<?php
/**
 * Fix APAI imported products missing storefront price (wrong storage / office map).
 *
 * HTTP: ?token=…&site_key=epartscart|electronicae&product_id=131&limit=50
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_apai_fulfillment.php';

$cfg = new DP_Config();
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
if ($siteKey === '') {
	if (function_exists('epc_apai_resolve_storefront_site_key')) {
		require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_storefront.php';
		$siteKey = epc_apai_resolve_storefront_site_key();
	}
}
if ($siteKey === '') {
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	foreach (array('epartscart' => 'epartscart', 'electronicae' => 'electronicae', 'stylenlook' => 'stylenlook', 'taxofinca' => 'taxofinca', 'thejewellerytrend' => 'thejewellerytrend') as $needle => $key) {
		if (strpos($host, $needle) !== false) {
			$siteKey = $key;
			break;
		}
	}
}
$productId = max(0, (int) ($_GET['product_id'] ?? 0));
$limit = max(1, min(200, (int) ($_GET['limit'] ?? 50)));

$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	(string) $cfg->user,
	(string) $cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
epc_ape_ensure_schema($pdo);

$out = array(
	'probe' => 'epc-apai-storefront-price-fix',
	'site_key' => $siteKey,
	'storefront_storage_id' => function_exists('epc_ape_resolve_storefront_storage_id')
		? epc_ape_resolve_storefront_storage_id($pdo)
		: 0,
	'products' => array(),
);
if (function_exists('epc_ape_ensure_catalogue_storage')) {
	try {
		$out['catalogue_storage_ensure'] = epc_ape_ensure_catalogue_storage($pdo);
	} catch (Throwable $e) {
		$out['catalogue_storage_ensure_error'] = $e->getMessage();
		$out['catalogue_storage_ensure'] = 0;
	}
	$out['storefront_storage_id'] = function_exists('epc_ape_resolve_storefront_storage_id')
		? epc_ape_resolve_storefront_storage_id($pdo)
		: 0;
	if (function_exists('epc_ape_get_last_storage_error')) {
		$err = epc_ape_get_last_storage_error();
		if ($err !== '') {
			$out['catalogue_storage_ensure_error'] = $err;
		}
	}
}

if ($productId > 0) {
	$sell = 0.0;
	$cost = 0.0;
	$q = $pdo->prepare(
		'SELECT `meta_json`, `suggested_price`, `cost_estimate` FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `product_id` = ? AND `status` = \'imported\' LIMIT 1'
	);
	$q->execute(array($siteKey, $productId));
	$row = $q->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}
		$sell = (float) ($meta['apai_sell_price'] ?? $meta['estimated_marketplace_price'] ?? $row['suggested_price'] ?? 0);
		$cost = (float) ($meta['apai_cost'] ?? $meta['import_warehouse_cost'] ?? $row['cost_estimate'] ?? 0);
	}
	if ($sell <= 0) {
		$sStmt = $pdo->prepare('SELECT MAX(`price`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price` > 0');
		$sStmt->execute(array($productId));
		$sell = (float) $sStmt->fetchColumn();
	}
	if ($cost <= 0) {
		$cStmt = $pdo->prepare('SELECT MAX(`price_purchase`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price_purchase` > 0');
		$cStmt->execute(array($productId));
		$cost = (float) $cStmt->fetchColumn();
	}
	if ($sell > 0) {
		epc_ape_set_catalogue_storage_price($pdo, $productId, $sell, $cost);
	}
	$out['products'][] = array(
		'product_id' => $productId,
		'sell' => $sell,
		'cost' => $cost,
		'storefront_visible' => epc_apai_product_storefront_offer_visible($pdo, $productId),
		'storefront_url' => epc_ape_catalogue_product_url($pdo, $productId),
	);
} elseif (function_exists('epc_apai_backfill_storefront_prices')) {
	$out['backfill'] = epc_apai_backfill_storefront_prices($pdo, $siteKey, $limit);
	$importStmt = $pdo->prepare(
		'SELECT `product_id` FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` = \'imported\' AND `product_id` > 0
		 ORDER BY `id` DESC LIMIT ' . $limit
	);
	$importStmt->execute(array($siteKey));
	foreach ($importStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $imp) {
		$pid = (int) ($imp['product_id'] ?? 0);
		if ($pid <= 0) {
			continue;
		}
		$out['products'][] = array(
			'product_id' => $pid,
			'storefront_visible' => epc_apai_product_storefront_offer_visible($pdo, $pid),
			'storefront_url' => epc_ape_catalogue_product_url($pdo, $pid),
		);
	}
}

$out['all_visible'] = true;
foreach ($out['products'] as $p) {
	if (empty($p['storefront_visible'])) {
		$out['all_visible'] = false;
		break;
	}
}

if (!empty($_GET['debug'])) {
	$diagPid = $productId > 0 ? $productId : (int) ($out['products'][0]['product_id'] ?? 0);
	if ($diagPid > 0) {
		$sd = $pdo->prepare('SELECT sd.*, s.`interface_type`, s.`name` FROM `shop_storages_data` sd LEFT JOIN `shop_storages` s ON s.`id` = sd.`storage_id` WHERE sd.`product_id` = ?');
		$sd->execute(array($diagPid));
		$out['debug_storage_rows'] = $sd->fetchAll(PDO::FETCH_ASSOC) ?: array();
		$map = $pdo->query(
			'SELECT m.*, s.`interface_type`, s.`name`
			 FROM `shop_offices_storages_map` m
			 INNER JOIN `shop_storages` s ON s.`id` = m.`storage_id`
			 ORDER BY m.`office_id`, m.`storage_id` LIMIT 20'
		);
		$out['debug_office_storage_map'] = $map ? ($map->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
		$allSt = $pdo->query('SELECT `id`, `name`, `interface_type`, `hidden`, `currency` FROM `shop_storages` ORDER BY `id`');
		$out['debug_all_storages'] = $allSt ? ($allSt->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
	}
}

echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
