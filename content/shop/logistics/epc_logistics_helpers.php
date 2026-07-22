<?php
/**
 * Logistics helpers — delivery, carriers, shipments (storefront + CP orders).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/../channels/epc_channel_schema.php';
require_once __DIR__ . '/../channels/epc_channel_helpers.php';

function epc_logistics_h($v)
{
	return epc_channel_h($v);
}

function epc_logistics_money($v)
{
	return epc_channel_money($v);
}

function epc_logistics_seed_defaults(PDO $db)
{
	epc_channel_ensure_schema($db);
	$now = time();
	$insC = $db->prepare(
		'INSERT INTO `epc_carrier_accounts` (`code`, `name`, `active`, `demo_mode`, `config_json`, `time_created`)
		 VALUES (?, ?, 1, 1, ?, ?)
		 ON DUPLICATE KEY UPDATE `name` = VALUES(`name`)'
	);
	$catalog = epc_channel_carriers_catalog();
	foreach ($catalog as $code => $meta) {
		$name = isset($meta['name']) ? (string)$meta['name'] : strtoupper((string)$code);
		$config = array(
			'origin' => 'Dubai, UAE',
			'region' => isset($meta['region']) ? (string)$meta['region'] : 'Global',
			'track_url' => isset($meta['track_url']) ? (string)$meta['track_url'] : '',
		);
		$insC->execute(array($code, $name, json_encode($config, JSON_UNESCAPED_UNICODE), $now));
	}
}

function epc_logistics_seed_sample_data(PDO $db)
{
	epc_logistics_seed_defaults($db);
	$now = time();
	$shop_order_id = (int)$db->query('SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 ORDER BY `id` DESC LIMIT 1')->fetchColumn();
	if ($shop_order_id > 0) {
		$exists = (int)$db->query('SELECT COUNT(*) FROM `epc_carrier_shipments` WHERE `order_id` = ' . $shop_order_id)->fetchColumn();
		if ($exists === 0) {
			$db->prepare(
				'INSERT INTO `epc_carrier_shipments`
				(`order_id`, `carrier_code`, `service_code`, `tracking_number`, `label_url`, `status`, `weight_kg`, `cost`, `currency`, `recipient_json`, `shipped_at`, `time_created`)
				VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
			)->execute(array(
				$shop_order_id,
				'dhl',
				'EXPRESS',
				'JD014600012345678901',
				'',
				'shipped',
				1.5,
				57.75,
				'AED',
				json_encode(array('name' => 'Sample Customer', 'city' => 'Dubai', 'country' => 'AE')),
				$now - 7200,
				$now - 7200,
			));
		}
	}
	epc_channel_log($db, 'seed', 'Sample carrier shipment loaded for logistics hub', 'logistics');
}

function epc_logistics_dashboard(PDO $db)
{
	epc_channel_ensure_schema($db);
	$shipments = (int)$db->query('SELECT COUNT(*) FROM `epc_carrier_shipments`')->fetchColumn();
	$shipped = (int)$db->query("SELECT COUNT(*) FROM `epc_carrier_shipments` WHERE `status` = 'shipped'")->fetchColumn();
	$pending = (int)$db->query("SELECT COUNT(*) FROM `epc_carrier_shipments` WHERE `status` NOT IN ('shipped','delivered')")->fetchColumn();
	$carriers = (int)$db->query('SELECT COUNT(*) FROM `epc_carrier_accounts` WHERE `active` = 1')->fetchColumn();
	$carriers_total = (int)$db->query('SELECT COUNT(*) FROM `epc_carrier_accounts`')->fetchColumn();
	$shop_orders = (int)$db->query('SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1')->fetchColumn();
	$regions = array();
	foreach (epc_channel_carriers_catalog() as $meta) {
		$r = isset($meta['region']) ? (string)$meta['region'] : 'Global';
		$regions[$r] = true;
	}
	return array(
		'carriers' => $carriers,
		'carriers_total' => $carriers_total,
		'catalog_count' => count(epc_channel_carriers_catalog()),
		'regions' => count($regions),
		'shipments' => $shipments,
		'shipments_shipped' => $shipped,
		'shipments_pending' => $pending,
		'shop_orders' => $shop_orders,
	);
}

function epc_logistics_demo_report(PDO $db)
{
	epc_channel_ensure_schema($db);
	return array(
		'dashboard' => epc_logistics_dashboard($db),
		'carriers' => epc_channel_list_carriers($db),
		'shipments' => $db->query(
			'SELECT s.*, o.`time` AS order_time
			FROM `epc_carrier_shipments` s
			LEFT JOIN `shop_orders` o ON o.`id` = s.`order_id`
			ORDER BY s.`id` DESC LIMIT 20'
		)->fetchAll(PDO::FETCH_ASSOC),
		'sync_log' => $db->query("SELECT * FROM `epc_channel_sync_log` WHERE `kind` IN ('shipment','seed','carrier') OR `channel_code` = 'logistics' ORDER BY `id` DESC LIMIT 15")->fetchAll(PDO::FETCH_ASSOC),
	);
}

function epc_logistics_configure_urls()
{
	global $DP_Config;
	$backend = '/' . $DP_Config->backend_dir;
	return array(
		'logisticsUrl' => $backend . '/shop/logistics',
		'carriersUrl' => $backend . '/shop/logistics/carriers',
		'guideUrl' => $backend . '/shop/logistics/guide',
		'obtainModesUrl' => $backend . '/shop/logistics/sposoby-polucheniya',
		'ordersUrl' => $backend . '/shop/orders/orders',
		'stockUrl' => $backend . '/shop/logistics/stock',
		'demoJsonUrl' => '/epc-logistics-demo.php?token=epartscart-deploy-2026',
		'setupUrl' => '/epc-logistics-setup.php?token=epartscart-deploy-2026',
	);
}

function epc_logistics_guide_snapshot(PDO $db)
{
	epc_channel_ensure_schema($db);
	$report = epc_logistics_demo_report($db);
	$obtainId = 0;
	$obtainHandler = '';
	try {
		$row = $db->query("SELECT `id`, `handler` FROM `shop_obtaining_modes` WHERE `handler` = 'epc_carriers' LIMIT 1")->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			$obtainId = (int)$row['id'];
			$obtainHandler = (string)$row['handler'];
		}
	} catch (Throwable $e) {
		// optional
	}
	return array_merge($report, array(
		'generated_at' => date('Y-m-d H:i:s'),
		'obtaining_mode_id' => $obtainId,
		'obtaining_handler' => $obtainHandler,
	));
}
