<?php
/**
 * CP order detail probe — DB row check + order_card.php include smoke for specific order IDs.
 * GET ?token=…&order_id=18  (repeat order_id or comma list: order_id=16,18)
 * GET ?site_key=epartscart  (default epartscart; use all for every Model C tenant)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
);

$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
if ($onlySite === 'all') {
	$onlySite = '';
}

$orderIds = array();
foreach (preg_split('/[\s,]+/', (string) ($_GET['order_id'] ?? '18'), -1, PREG_SPLIT_NO_EMPTY) ?: array() as $rawId) {
	$id = (int) $rawId;
	if ($id > 0) {
		$orderIds[$id] = $id;
	}
}
if (!$orderIds) {
	$orderIds[18] = 18;
}

function epc_ocd_probe_order(PDO $pdo, int $orderId): array
{
	$out = array(
		'order_id' => $orderId,
		'order_exists' => false,
		'items_count' => 0,
		'items' => array(),
	);
	$st = $pdo->prepare('SELECT `id`, `office_id`, `status`, `user_id`, `time` FROM `shop_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($orderId));
	$order = $st->fetch(PDO::FETCH_ASSOC);
	if (!$order) {
		return $out;
	}
	$out['order_exists'] = true;
	$out['order'] = array(
		'office_id' => (int) ($order['office_id'] ?? 0),
		'status' => (int) ($order['status'] ?? 0),
		'user_id' => (int) ($order['user_id'] ?? 0),
		'time' => (int) ($order['time'] ?? 0),
	);

	$iq = $pdo->prepare(
		'SELECT `id`, `product_type`, `t2_storage_id`, `t2_name`, `t2_article`, `t2_manufacturer`,
			LEFT(`t2_json_params`, 120) AS `t2_json_params_preview`, `status`, `count_need`, `price`
		 FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id` ASC'
	);
	$iq->execute(array($orderId));
	while ($row = $iq->fetch(PDO::FETCH_ASSOC)) {
		$storageId = (int) ($row['t2_storage_id'] ?? 0);
		$storageExists = false;
		if ($storageId > 0) {
			$sq = $pdo->prepare('SELECT COUNT(*) FROM `shop_storages` WHERE `id` = ?');
			$sq->execute(array($storageId));
			$storageExists = ((int) $sq->fetchColumn()) > 0;
		}
		$metaPreview = (string) ($row['t2_json_params_preview'] ?? '');
		$metaOk = true;
		if ($metaPreview !== '') {
			$decoded = json_decode($metaPreview, true);
			$metaOk = is_array($decoded) || json_decode(stripslashes($metaPreview), true) !== null;
		}
		$out['items'][] = array(
			'id' => (int) ($row['id'] ?? 0),
			'product_type' => (int) ($row['product_type'] ?? 0),
			't2_storage_id' => $storageId,
			'storage_exists' => $storageExists,
			't2_name_len' => strlen((string) ($row['t2_name'] ?? '')),
			'has_quotes_in_name' => strpos((string) ($row['t2_name'] ?? ''), '"') !== false,
			'has_apai_meta' => stripos($metaPreview, 'apai_supplier') !== false,
			't2_json_meta_ok' => $metaOk,
			'status' => (int) ($row['status'] ?? 0),
		);
	}
	$out['items_count'] = count($out['items']);
	return $out;
}

function epc_ocd_render_order_card(string $host, int $orderId): array
{
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);

	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);
	$backend = (string) $cfg->backend_dir;
	$cardPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/order_card.php';

	$out = array(
		'order_id' => $orderId,
		'order_card_path' => $cardPath,
		'order_card_exists' => is_file($cardPath),
		'order_card_cp_js' => is_file($_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/order_card_cp.js'),
		'apai_fulfillment' => is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_fulfillment.php'),
	);

	if (!$out['order_card_exists']) {
		$out['include_ok'] = false;
		$out['error'] = 'order_card.php missing';
		$out['ok'] = false;
		return $out;
	}

	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		$out['include_ok'] = false;
		$out['error'] = 'DB: ' . $e->getMessage();
		$out['ok'] = false;
		return $out;
	}

	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id) { return (string) $id; }
	}
	if (!function_exists('translate_str_by_key')) {
		function translate_str_by_key($key) { return (string) $key; }
	}

	$GLOBALS['DP_Config'] = $cfg;
	$GLOBALS['DP_Template'] = (object) array('name' => 'bootstrap_admin');
	$GLOBALS['db_link'] = $pdo;
	$db_link = $pdo;
	$DP_Config = $cfg;
	$user_session = array('csrf_guard_key' => 'verify');
	$multilang_params = array('lang' => 'en');

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$GLOBALS['epc_orders_smoke_all_offices'] = true;
	$ordersBackground = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/order_process/orders_background.php';
	if (is_file($ordersBackground)) {
		include $ordersBackground;
	}

	$_GET['order_id'] = $orderId;
	@ini_set('display_errors', '0');
	@ini_set('memory_limit', '512M');
	@set_time_limit(120);

	ob_start();
	try {
		include $cardPath;
		$html = (string) ob_get_clean();
		$out['include_ok'] = true;
		$out['html_length'] = strlen($html);
		$out['has_order_items_table'] = stripos($html, 'id="order_items_table"') !== false;
		$out['has_oc_boot'] = stripos($html, 'id="epc-oc-boot"') !== false;
		$out['has_inline_for_js'] = stripos($html, 'orders_items_ids_to_orders_items_objects[') !== false
			&& stripos($html, 'product_name":"') !== false;
		$out['has_apai_badge'] = stripos($html, 'Fulfill from:') !== false;
		$out['order_not_found_msg'] = stripos($html, 'Order not found or access denied') !== false;
		$out['php_fatal_in_html'] = (bool) preg_match('/\b(Fatal error|Parse error|Uncaught)\b/i', $html);
		$out['ok'] = $out['include_ok']
			&& $out['has_order_items_table']
			&& $out['has_oc_boot']
			&& !$out['order_not_found_msg']
			&& !$out['php_fatal_in_html']
			&& !$out['has_inline_for_js'];
		$out['cp_order_url'] = 'https://' . $host . '/' . $backend . '/shop/orders/order?order_id=' . $orderId;
	} catch (Throwable $e) {
		if (ob_get_level() > 0) {
			ob_end_clean();
		}
		$out['include_ok'] = false;
		$out['error'] = $e->getMessage();
		$out['ok'] = false;
	}
	unset($_GET['order_id']);
	return $out;
}

$results = array();
foreach ($tenants as $siteKey => $host) {
	if ($onlySite !== '' && $onlySite !== $siteKey) {
		continue;
	}
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);
	$cfg = new DP_Config();
	epc_portal_apply_config($cfg);

	$tenantOut = array(
		'site_key' => $siteKey,
		'host' => $host,
		'docroot' => $_SERVER['DOCUMENT_ROOT'] ?? '',
		'db_ok' => false,
		'orders' => array(),
		'error' => null,
	);
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$tenantOut['db_ok'] = true;
		foreach ($orderIds as $orderId) {
			$probe = epc_ocd_probe_order($pdo, $orderId);
			$probe['render'] = epc_ocd_render_order_card($host, $orderId);
			$tenantOut['orders'][(string) $orderId] = $probe;
		}
	} catch (Throwable $e) {
		$tenantOut['error'] = $e->getMessage();
	}
	$tenantOut['status'] = $tenantOut['db_ok'] && !empty($tenantOut['orders'])
		&& array_reduce($tenantOut['orders'], static function ($carry, $row) {
			return $carry && !empty($row['render']['ok']);
		}, true) ? 'ok' : 'check';
	$results[$siteKey] = $tenantOut;
}

echo json_encode(
	array(
		'probe' => 'epc-cp-order-detail-verify',
		'ts' => gmdate('c'),
		'order_ids' => array_values($orderIds),
		'tenants' => $results,
	),
	JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES
);
