<?php
/**
 * Marketplace + carrier integration helpers (demo + live-ready hooks).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_channel_schema.php';

function epc_channel_h($v)
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}

function epc_channel_money($amount)
{
	return number_format((float)$amount, 2, '.', ',');
}

/**
 * Worldwide carrier partners for storefront checkout + CP logistics hub.
 * demo_base = AED demo tariff floor used by epc_channel_demo_rate().
 *
 * @return array<string,array<string,mixed>>
 */
function epc_channel_carriers_catalog()
{
	return array(
		'dhl' => array(
			'name' => 'DHL Express', 'region' => 'Global', 'accent' => '#d40511', 'icon' => 'fa-plane',
			'blurb' => 'Express worldwide via Dubai & European hubs',
			'track_url' => 'https://www.dhl.com/ae-en/home/tracking.html?tracking-id=%s',
			'services' => array('EXPRESS' => 'Express Worldwide', 'ECONOMY' => 'Economy Select'),
			'demo_base' => 45,
		),
		'fedex' => array(
			'name' => 'FedEx', 'region' => 'Global', 'accent' => '#4d148c', 'icon' => 'fa-globe',
			'blurb' => 'International Priority & Economy from UAE',
			'track_url' => 'https://www.fedex.com/fedextrack/?trknbr=%s',
			'services' => array('PRIORITY' => 'International Priority', 'ECONOMY' => 'International Economy'),
			'demo_base' => 42,
		),
		'ups' => array(
			'name' => 'UPS', 'region' => 'Global', 'accent' => '#351c15', 'icon' => 'fa-truck',
			'blurb' => 'Worldwide Saver & freight for B2B parts',
			'track_url' => 'https://www.ups.com/track?tracknum=%s',
			'services' => array('STANDARD' => 'Worldwide Saver', 'EXPRESS' => 'Worldwide Express'),
			'demo_base' => 38,
		),
		'tnt' => array(
			'name' => 'TNT Express', 'region' => 'Global', 'accent' => '#ff6600', 'icon' => 'fa-rocket',
			'blurb' => 'Europe & global express (FedEx network)',
			'track_url' => 'https://www.tnt.com/express/en_ae/site/shipping-tools/tracking.html?searchType=con&cons=%s',
			'services' => array('EXPRESS' => 'Global Express', 'ECONOMY' => 'Economy Express'),
			'demo_base' => 40,
		),
		'aramex' => array(
			'name' => 'Aramex', 'region' => 'MENA', 'accent' => '#e30613', 'icon' => 'fa-map-marker',
			'blurb' => 'MENA parcel express & domestic UAE',
			'track_url' => 'https://www.aramex.com/track/results?ShipmentNumber=%s',
			'services' => array('PPX' => 'Parcel Express', 'DOM' => 'Domestic'),
			'demo_base' => 28,
		),
		'smsa' => array(
			'name' => 'SMSA Express', 'region' => 'MENA', 'accent' => '#00a651', 'icon' => 'fa-truck',
			'blurb' => 'Saudi & GCC last-mile express',
			'track_url' => 'https://www.smsaexpress.com/trackingdetails?tracknumbers=%s',
			'services' => array('EXPRESS' => 'Express', 'DOM' => 'Domestic'),
			'demo_base' => 26,
		),
		'naqel' => array(
			'name' => 'Naqel Express', 'region' => 'MENA', 'accent' => '#1b4f9c', 'icon' => 'fa-truck',
			'blurb' => 'KSA / GCC e-commerce & B2B delivery',
			'track_url' => 'https://www.naqelexpress.com/en/track?trackingNumber=%s',
			'services' => array('EXPRESS' => 'Express', 'STANDARD' => 'Standard'),
			'demo_base' => 25,
		),
		'emirates_post' => array(
			'name' => 'Emirates Post', 'region' => 'MENA', 'accent' => '#c8102e', 'icon' => 'fa-envelope',
			'blurb' => 'UAE national post & international parcels',
			'track_url' => 'https://www.emiratespost.ae/English/track-and-trace?TrackingNumber=%s',
			'services' => array('EMS' => 'EMS Express', 'PARCEL' => 'International Parcel'),
			'demo_base' => 22,
		),
		'imile' => array(
			'name' => 'iMile', 'region' => 'MENA', 'accent' => '#ff6a00', 'icon' => 'fa-archive',
			'blurb' => 'Middle East & Asia cross-border parcels',
			'track_url' => 'https://www.imile.com/track?trackingNo=%s',
			'services' => array('EXPRESS' => 'Express', 'STANDARD' => 'Standard'),
			'demo_base' => 24,
		),
		'dpd' => array(
			'name' => 'DPD', 'region' => 'Europe', 'accent' => '#dc0032', 'icon' => 'fa-cube',
			'blurb' => 'European road & Predict delivery',
			'track_url' => 'https://www.dpd.com/tracking?parcelNumber=%s',
			'services' => array('CLASSIC' => 'Classic', 'EXPRESS' => 'Express'),
			'demo_base' => 32,
		),
		'gls' => array(
			'name' => 'GLS', 'region' => 'Europe', 'accent' => '#00205b', 'icon' => 'fa-road',
			'blurb' => 'EuroBusinessParcel across EU',
			'track_url' => 'https://gls-group.com/track/%s',
			'services' => array('BUSINESS' => 'BusinessParcel', 'EXPRESS' => 'ExpressParcel'),
			'demo_base' => 31,
		),
		'postnl' => array(
			'name' => 'PostNL', 'region' => 'Europe', 'accent' => '#ed8b00', 'icon' => 'fa-envelope-o',
			'blurb' => 'Netherlands & EU parcels',
			'track_url' => 'https://jouw.postnl.nl/track-and-trace/%s',
			'services' => array('STANDARD' => 'Standard', 'EU' => 'EU Parcel'),
			'demo_base' => 30,
		),
		'royal_mail' => array(
			'name' => 'Royal Mail', 'region' => 'Europe', 'accent' => '#ff0000', 'icon' => 'fa-flag',
			'blurb' => 'UK Tracked & International Signed',
			'track_url' => 'https://www.royalmail.com/track-your-item#/tracking-results/%s',
			'services' => array('TRACKED' => 'Tracked 48', 'INTL' => 'International Tracked'),
			'demo_base' => 33,
		),
		'chronopost' => array(
			'name' => 'Chronopost', 'region' => 'Europe', 'accent' => '#003da5', 'icon' => 'fa-clock-o',
			'blurb' => 'France express & Europe Chrono',
			'track_url' => 'https://www.chronopost.fr/tracking-no-cms/suivi-page?listeNumerosLT=%s',
			'services' => array('CHRONO' => 'Chrono 13', 'EUROPE' => 'Chrono Europe'),
			'demo_base' => 34,
		),
		'usps' => array(
			'name' => 'USPS', 'region' => 'Americas', 'accent' => '#004b87', 'icon' => 'fa-flag-checkered',
			'blurb' => 'US Priority Mail International',
			'track_url' => 'https://tools.usps.com/go/TrackConfirmAction?tLabels=%s',
			'services' => array('PRIORITY' => 'Priority Mail Intl', 'EXPRESS' => 'Priority Express Intl'),
			'demo_base' => 36,
		),
		'canada_post' => array(
			'name' => 'Canada Post', 'region' => 'Americas', 'accent' => '#e31837', 'icon' => 'fa-leaf',
			'blurb' => 'Xpresspost International',
			'track_url' => 'https://www.canadapost-postescanada.ca/track-reperage/en#/details/%s',
			'services' => array('XPRESS' => 'Xpresspost', 'INTL' => 'International Parcel'),
			'demo_base' => 35,
		),
		'sf_express' => array(
			'name' => 'SF Express', 'region' => 'Asia', 'accent' => '#000000', 'icon' => 'fa-bolt',
			'blurb' => 'China & Asia Pacific express',
			'track_url' => 'https://www.sf-express.com/en/dynamic_function/waybill/#search/bill-number/%s',
			'services' => array('STANDARD' => 'Standard Express', 'INTL' => 'International'),
			'demo_base' => 37,
		),
		'jt_express' => array(
			'name' => 'J&T Express', 'region' => 'Asia', 'accent' => '#e60012', 'icon' => 'fa-motorcycle',
			'blurb' => 'SEA & Middle East e-commerce parcels',
			'track_url' => 'https://www.jtexpress.ae/trajectoryQuery?waybillNo=%s',
			'services' => array('EXPRESS' => 'Express', 'ECONOMY' => 'Economy'),
			'demo_base' => 23,
		),
		'yamato' => array(
			'name' => 'Yamato Transport', 'region' => 'Asia', 'accent' => '#ffd400', 'icon' => 'fa-bicycle',
			'blurb' => 'Japan TA-Q-BIN & international',
			'track_url' => 'https://toi.kuronekoyamato.co.jp/cgi-bin/tneko?number=%s',
			'services' => array('TAQBIN' => 'TA-Q-BIN', 'INTL' => 'International'),
			'demo_base' => 39,
		),
		'bluedart' => array(
			'name' => 'Blue Dart', 'region' => 'Asia', 'accent' => '#0033a0', 'icon' => 'fa-paper-plane',
			'blurb' => 'India domestic & export express',
			'track_url' => 'https://www.bluedart.com/tracking?trackfor=0&trackNo=%s',
			'services' => array('EXPRESS' => 'Domestic Express', 'INTL' => 'International'),
			'demo_base' => 29,
		),
	);
}

/**
 * @return array<string,float>
 */
function epc_channel_carrier_demo_bases()
{
	$bases = array();
	foreach (epc_channel_carriers_catalog() as $code => $c) {
		$bases[$code] = isset($c['demo_base']) ? (float)$c['demo_base'] : 35.0;
	}
	return $bases;
}

function epc_channel_demo_rate($carrier_code, $weight_kg, $dest_country = 'AE')
{
	$weight_kg = max(0.1, (float)$weight_kg);
	$bases = epc_channel_carrier_demo_bases();
	$b = isset($bases[$carrier_code]) ? $bases[$carrier_code] : 35.0;
	$intl = ($dest_country !== '' && strtoupper($dest_country) !== 'AE') ? 1.35 : 1.0;
	return round(($b + ($weight_kg * 8.5)) * $intl, 2);
}

function epc_channel_log(PDO $db, $kind, $message, $channel_code = '', $payload = null)
{
	$db->prepare(
		'INSERT INTO `epc_channel_sync_log` (`kind`, `channel_code`, `message`, `payload_json`, `time_created`) VALUES (?, ?, ?, ?, ?)'
	)->execute(array(
		(string)$kind,
		(string)$channel_code,
		mb_substr((string)$message, 0, 512),
		$payload !== null ? json_encode($payload, JSON_UNESCAPED_UNICODE) : null,
		time(),
	));
}

function epc_channel_list_marketplaces(PDO $db)
{
	epc_channel_ensure_schema($db);
	return $db->query('SELECT * FROM `epc_marketplace_channels` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
}

function epc_channel_list_carriers(PDO $db)
{
	epc_channel_ensure_schema($db);
	return $db->query('SELECT * FROM `epc_carrier_accounts` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
}

function epc_channel_dashboard(PDO $db)
{
	epc_channel_ensure_schema($db);
	$mp_orders = (int)$db->query('SELECT COUNT(*) FROM `epc_marketplace_orders`')->fetchColumn();
	$mp_pending = (int)$db->query("SELECT COUNT(*) FROM `epc_marketplace_orders` WHERE `status` IN ('pending','awaiting_shipment')")->fetchColumn();
	$sku_count = (int)$db->query('SELECT COUNT(*) FROM `epc_marketplace_sku_map` WHERE `active` = 1')->fetchColumn();
	return array(
		'marketplace_orders' => $mp_orders,
		'marketplace_pending' => $mp_pending,
		'sku_mapped' => $sku_count,
	);
}

function epc_channel_seed_defaults(PDO $db)
{
	epc_channel_ensure_schema($db);
	$now = time();
	$channels = array(
		array('amazon', 'Amazon (SP-API)', 'A2EUQ1WTGCTBG2', '{"region":"eu-west-1","marketplace":"Amazon.ae"}'),
		array('ebay', 'eBay (Sell API)', 'EBAY-US', '{"site_id":0,"marketplace":"eBay Motors"}'),
	);
	$ins = $db->prepare(
		'INSERT INTO `epc_marketplace_channels` (`code`, `name`, `marketplace_id`, `active`, `demo_mode`, `config_json`, `time_created`)
		 VALUES (?, ?, ?, 1, 1, ?, ?)
		 ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `marketplace_id` = VALUES(`marketplace_id`)'
	);
	foreach ($channels as $c) {
		$ins->execute(array($c[0], $c[1], $c[2], $c[3], $now));
	}
}

function epc_channel_seed_sample_data(PDO $db)
{
	epc_channel_seed_defaults($db);
	$now = time();

	$ch = $db->query('SELECT `id`, `code` FROM `epc_marketplace_channels`')->fetchAll(PDO::FETCH_KEY_PAIR);
	$amazon_id = 0;
	$ebay_id = 0;
	foreach ($ch as $id => $code) {
		if ($code === 'amazon') {
			$amazon_id = (int)$id;
		}
		if ($code === 'ebay') {
			$ebay_id = (int)$id;
		}
	}

	$skus = array(
		array($amazon_id, 'BOSCH', '0986424590', 'AMZ-0986424590', 'B0123456789', 'Bosch Oil Filter', 42.00, 25),
		array($amazon_id, 'MANN', 'HU7008Z', 'AMZ-HU7008Z', 'B0987654321', 'Mann Oil Filter', 38.50, 18),
		array($ebay_id, 'NGK', 'BKR6E', 'EBY-BKR6E', null, 'NGK Spark Plug BKR6E', 12.00, 120),
		array($ebay_id, 'VALEO', '828038', 'EBY-828038', null, 'Valeo Clutch Kit', 285.00, 4),
	);
	$insSku = $db->prepare(
		'INSERT INTO `epc_marketplace_sku_map`
		(`channel_id`, `manufacturer`, `article`, `external_sku`, `external_asin`, `title`, `price`, `stock_qty`, `active`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?)
		ON DUPLICATE KEY UPDATE `title` = VALUES(`title`), `price` = VALUES(`price`), `stock_qty` = VALUES(`stock_qty`), `time_updated` = VALUES(`time_updated`)'
	);
	foreach ($skus as $s) {
		if ($s[0] <= 0) {
			continue;
		}
		$insSku->execute(array($s[0], $s[1], $s[2], $s[3], $s[4], $s[5], $s[6], $s[7], $now));
	}

	$orders = array(
		array($amazon_id, 'AMZ-402-8819201', 'awaiting_shipment', 'Ahmed Al Mansoori', 'ahmed.sample@example.com', 'Dubai', 'AE', 156.75,
			array(array('sku' => 'AMZ-0986424590', 'qty' => 2, 'price' => 42.00), array('sku' => 'AMZ-HU7008Z', 'qty' => 2, 'price' => 36.38))),
		array($ebay_id, 'EBY-12-99887766', 'pending', 'John Smith', 'john.sample@example.com', 'London', 'GB', 48.00,
			array(array('sku' => 'EBY-BKR6E', 'qty' => 4, 'price' => 12.00))),
	);
	$insOrd = $db->prepare(
		'INSERT INTO `epc_marketplace_orders`
		(`channel_id`, `external_order_id`, `status`, `customer_name`, `customer_email`, `ship_city`, `ship_country`, `currency`, `total_amount`, `items_json`, `time_created`)
		VALUES (?, ?, ?, ?, ?, ?, ?, \'AED\', ?, ?, ?)
		ON DUPLICATE KEY UPDATE `status` = VALUES(`status`), `total_amount` = VALUES(`total_amount`)'
	);
	foreach ($orders as $o) {
		if ($o[0] <= 0) {
			continue;
		}
		$insOrd->execute(array($o[0], $o[1], $o[2], $o[3], $o[4], $o[5], $o[6], $o[7], json_encode($o[8]), $now - 3600));
	}

	epc_channel_log($db, 'seed', 'Sample marketplace SKUs and orders loaded', 'system');
}

function epc_channel_sync_inventory_demo(PDO $db, $channel_code)
{
	epc_channel_ensure_schema($db);
	$st = $db->prepare('SELECT `id` FROM `epc_marketplace_channels` WHERE `code` = ? LIMIT 1');
	$st->execute(array($channel_code));
	$cid = (int)$st->fetchColumn();
	if ($cid <= 0) {
		throw new Exception('Channel not found: ' . $channel_code);
	}
	$rows = $db->prepare('SELECT * FROM `epc_marketplace_sku_map` WHERE `channel_id` = ? AND `active` = 1');
	$rows->execute(array($cid));
	$updated = 0;
	$payload = array();
	while ($row = $rows->fetch(PDO::FETCH_ASSOC)) {
		$updated++;
		$payload[] = array(
			'sku' => $row['external_sku'],
			'qty' => (int)$row['stock_qty'],
			'price' => (float)$row['price'],
		);
	}
	$db->prepare('UPDATE `epc_marketplace_channels` SET `last_sync_at` = ? WHERE `id` = ?')->execute(array(time(), $cid));
	epc_channel_log($db, 'inventory_sync', 'Demo inventory push: ' . $updated . ' SKUs', $channel_code, array('items' => $payload));
	return array('channel' => $channel_code, 'skus_pushed' => $updated, 'items' => $payload);
}

function epc_channel_import_order_demo(PDO $db, $marketplace_order_id)
{
	epc_channel_ensure_schema($db);
	$st = $db->prepare('SELECT mo.*, mc.`code` AS channel_code FROM `epc_marketplace_orders` mo INNER JOIN `epc_marketplace_channels` mc ON mc.`id` = mo.`channel_id` WHERE mo.`id` = ? LIMIT 1');
	$st->execute(array((int)$marketplace_order_id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Marketplace order not found');
	}
	if (!empty($row['shop_order_id'])) {
		return array('status' => true, 'message' => 'Already linked', 'shop_order_id' => (int)$row['shop_order_id']);
	}
	$db->prepare('UPDATE `epc_marketplace_orders` SET `status` = ?, `imported_at` = ?, `shop_order_id` = NULL WHERE `id` = ?')
		->execute(array('imported', time(), (int)$row['id']));
	epc_channel_log($db, 'order_import', 'Demo import of ' . $row['external_order_id'], $row['channel_code'], array('marketplace_order_id' => (int)$row['id']));
	return array(
		'status' => true,
		'message' => 'Demo import complete — link to shop_orders when live API credentials are configured',
		'marketplace_order_id' => (int)$row['id'],
		'external_order_id' => $row['external_order_id'],
	);
}

function epc_channel_create_shipment_demo(PDO $db, $order_id, $carrier_code, $service_code, $weight_kg = 1.0)
{
	epc_channel_ensure_schema($db);
	$order_id = (int)$order_id;
	$carrier_code = preg_replace('/[^a-z0-9_]/', '', strtolower((string)$carrier_code));
	$catalog = epc_channel_carriers_catalog();
	if (!isset($catalog[$carrier_code])) {
		throw new Exception('Unknown carrier');
	}
	$chk = $db->prepare('SELECT `id` FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1');
	$chk->execute(array($order_id));
	if (!(int)$chk->fetchColumn()) {
		throw new Exception('Order not found');
	}
	$cost = epc_channel_demo_rate($carrier_code, $weight_kg);
	$tracking = strtoupper(substr($carrier_code, 0, 3)) . date('ymd') . str_pad((string)$order_id, 6, '0', STR_PAD_LEFT) . random_int(100, 999);
	$track_tpl = $catalog[$carrier_code]['track_url'];
	$label_url = sprintf($track_tpl, $tracking);
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_carrier_shipments`
		(`order_id`, `carrier_code`, `service_code`, `tracking_number`, `label_url`, `status`, `weight_kg`, `cost`, `currency`, `shipped_at`, `time_created`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array($order_id, $carrier_code, $service_code, $tracking, $label_url, 'shipped', (float)$weight_kg, $cost, 'AED', $now, $now));
	$shipment_id = (int)$db->lastInsertId();
	epc_channel_log($db, 'shipment', 'Demo label created ' . $tracking, $carrier_code, array('order_id' => $order_id, 'shipment_id' => $shipment_id));
	return array(
		'shipment_id' => $shipment_id,
		'carrier' => $catalog[$carrier_code]['name'],
		'tracking_number' => $tracking,
		'tracking_url' => $label_url,
		'cost' => $cost,
		'currency' => 'AED',
	);
}

function epc_channel_demo_report(PDO $db)
{
	epc_channel_ensure_schema($db);
	return array(
		'dashboard' => epc_channel_dashboard($db),
		'channels' => epc_channel_list_marketplaces($db),
		'sku_map' => $db->query('SELECT m.*, c.`code` AS channel_code FROM `epc_marketplace_sku_map` m INNER JOIN `epc_marketplace_channels` c ON c.`id` = m.`channel_id` ORDER BY m.`id` LIMIT 20')->fetchAll(PDO::FETCH_ASSOC),
		'marketplace_orders' => $db->query(
			'SELECT mo.*, c.`code` AS channel_code, c.`name` AS channel_name
			FROM `epc_marketplace_orders` mo
			INNER JOIN `epc_marketplace_channels` c ON c.`id` = mo.`channel_id`
			ORDER BY mo.`id` DESC LIMIT 20'
		)->fetchAll(PDO::FETCH_ASSOC),
		'sync_log' => $db->query("SELECT * FROM `epc_channel_sync_log` WHERE `kind` IN ('inventory_sync','order_import','seed') OR `channel_code` IN ('amazon','ebay','system') ORDER BY `id` DESC LIMIT 15")->fetchAll(PDO::FETCH_ASSOC),
	);
}

function epc_channel_configure_urls()
{
	global $DP_Config;
	$backend = '/' . $DP_Config->backend_dir;
	return array(
		'channelsUrl' => $backend . '/shop/channels/channels',
		'guideUrl' => $backend . '/shop/channels/guide',
		'logisticsGuideUrl' => $backend . '/shop/logistics/guide',
		'logisticsUrl' => $backend . '/shop/logistics',
		'ordersUrl' => $backend . '/shop/orders/orders',
		'demoJsonUrl' => '/epc-channels-demo.php?token=epartscart-deploy-2026',
		'setupUrl' => '/epc-channels-setup.php?token=epartscart-deploy-2026',
	);
}

function epc_channel_guide_snapshot(PDO $db)
{
	epc_channel_ensure_schema($db);
	$report = epc_channel_demo_report($db);
	return array_merge($report, array(
		'generated_at' => date('Y-m-d H:i:s'),
	));
}
