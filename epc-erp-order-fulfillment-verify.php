<?php
/**
 * Read-only probe: commerce order → ERP SO + per-supplier PO linkage.
 * GET ?token=…&order_id=18  (comma-separated list OK)
 * GET ?site_key=epartscart   (default epartscart)
 * GET ?bootstrap=1           (optional: run bootstrap for missing links)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_order_fulfillment.php';

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
$host = $tenants[$siteKey] ?? $tenants['epartscart'];
$_SERVER['HTTP_HOST'] = $host;
$_SERVER['SERVER_NAME'] = $host;
unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);
$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$orderIds = array();
foreach (preg_split('/[\s,]+/', (string) ($_GET['order_id'] ?? ''), -1, PREG_SPLIT_NO_EMPTY) ?: array() as $raw) {
	$id = (int) $raw;
	if ($id > 0) {
		$orderIds[$id] = $id;
	}
}

$bootstrap = !empty($_GET['bootstrap']);
$out = array(
	'ok' => true,
	'probe' => 'epc-erp-order-fulfillment-verify',
	'ts' => gmdate('c'),
	'site_key' => $siteKey,
	'host' => $host,
	'orders' => array(),
	'scenario' => 'Multi-supplier order → open SO + draft POs per supplier; cost on PI; revenue on SI when complete.',
);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_erp_order_fulfillment_ensure_schema($pdo);

	if (!$orderIds) {
		$st = $pdo->query(
			'SELECT o.`id` FROM `shop_orders` o
			 INNER JOIN `shop_orders_items` i ON i.`order_id` = o.`id`
			 WHERE o.`successfully_created` = 1
			 GROUP BY o.`id`
			 HAVING COUNT(DISTINCT i.`t2_storage_id`) > 1
			 ORDER BY o.`id` DESC LIMIT 3'
		);
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$orderIds[(int) $row['id']] = (int) $row['id'];
		}
		if (!$orderIds) {
			$orderIds[18] = 18;
		}
	}

	foreach ($orderIds as $orderId) {
		$probe = array('order_id' => $orderId);
		$oq = $pdo->prepare(
			'SELECT o.`id`, o.`user_id`, o.`status`, o.`time`,
			 COUNT(i.`id`) AS item_count,
			 COUNT(DISTINCT i.`t2_storage_id`) AS supplier_storage_count
			 FROM `shop_orders` o
			 LEFT JOIN `shop_orders_items` i ON i.`order_id` = o.`id`
			 WHERE o.`id` = ? AND o.`successfully_created` = 1
			 GROUP BY o.`id` LIMIT 1'
		);
		$oq->execute(array($orderId));
		$order = $oq->fetch(PDO::FETCH_ASSOC);
		if (!$order) {
			$probe['error'] = 'Order not found';
			$out['orders'][] = $probe;
			continue;
		}
		$probe['shop_order'] = array(
			'user_id' => (int) $order['user_id'],
			'status' => (int) $order['status'],
			'item_count' => (int) $order['item_count'],
			'supplier_storage_count' => (int) $order['supplier_storage_count'],
			'multi_supplier' => (int) $order['supplier_storage_count'] > 1,
		);

		$items = array();
		$iq = $pdo->prepare(
			'SELECT `id`, `t2_storage_id`, `t2_name`, `t2_article`, LEFT(`t2_json_params`, 80) AS meta_preview, `count_need`, `t2_price_purchase`, `price`
			 FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`'
		);
		$iq->execute(array($orderId));
		while ($row = $iq->fetch(PDO::FETCH_ASSOC)) {
			$sup = epc_erp_order_fulfillment_resolve_line_supplier($pdo, $row);
			$items[] = array(
				'item_id' => (int) $row['id'],
				'storage_id' => (int) $row['t2_storage_id'],
				'supplier_id' => (int) $sup['supplier_id'],
				'supplier_name' => $sup['supplier_name'],
				'article' => $row['t2_article'],
				'qty' => (float) $row['count_need'],
			);
		}
		$probe['lines'] = $items;

		if ($bootstrap && !epc_erp_order_fulfillment_find_sales_order($pdo, $orderId)) {
			try {
				$probe['bootstrap'] = epc_erp_order_fulfillment_bootstrap($pdo, $orderId);
			} catch (Throwable $e) {
				$probe['bootstrap_error'] = $e->getMessage();
			}
		}

		$probe['fulfillment'] = epc_erp_order_fulfillment_status($pdo, $orderId);
		$probe['checks'] = array(
			'has_sales_order' => !empty($probe['fulfillment']['sales_order']),
			'po_count' => count($probe['fulfillment']['purchase_orders'] ?? array()),
			'po_linked_to_order' => count(array_filter($probe['fulfillment']['purchase_orders'] ?? array(), function ($p) use ($orderId) {
				return (int) ($p['order_id'] ?? 0) === $orderId;
			})) > 0,
			'cost_before_revenue_ok' => (
				empty($probe['fulfillment']['accounting']['revenue_posted'])
				|| !empty($probe['fulfillment']['accounting']['cost_posted'])
			),
		);
		$out['orders'][] = $probe;
	}
} catch (Throwable $e) {
	$out['ok'] = false;
	$out['error'] = $e->getMessage();
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
