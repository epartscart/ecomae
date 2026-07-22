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

function epc_channel_carriers_catalog()
{
	return array(
		'dhl' => array('name' => 'DHL Express', 'track_url' => 'https://www.dhl.com/ae-en/home/tracking.html?tracking-id=%s', 'services' => array('EXPRESS' => 'Express Worldwide', 'ECONOMY' => 'Economy Select')),
		'fedex' => array('name' => 'FedEx', 'track_url' => 'https://www.fedex.com/fedextrack/?trknbr=%s', 'services' => array('PRIORITY' => 'International Priority', 'ECONOMY' => 'International Economy')),
		'aramex' => array('name' => 'Aramex', 'track_url' => 'https://www.aramex.com/track/results?ShipmentNumber=%s', 'services' => array('PPX' => 'Parcel Express', 'DOM' => 'Domestic')),
		'ups' => array('name' => 'UPS', 'track_url' => 'https://www.ups.com/track?tracknum=%s', 'services' => array('STANDARD' => 'Worldwide Saver')),
	);
}

function epc_channel_demo_rate($carrier_code, $weight_kg, $dest_country = 'AE')
{
	$weight_kg = max(0.1, (float)$weight_kg);
	$base = array('dhl' => 45, 'fedex' => 42, 'aramex' => 28, 'ups' => 38);
	$b = isset($base[$carrier_code]) ? $base[$carrier_code] : 35;
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

/**
 * Worldwide sell-channel partners (Amazon / eBay / Noon / regional).
 * Plug-and-play: seed into epc_marketplace_channels; enable/disable from CP hub.
 *
 * @return array<string,array<string,mixed>>
 */
function epc_channel_marketplaces_catalog()
{
	return array(
		// Legacy primary keys kept for sample SKUs / sync demos
		'amazon' => array(
			'name' => 'Amazon.ae', 'family' => 'Amazon', 'region' => 'MENA', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon UAE — SP-API listings & FBA/FBM',
			'marketplace_id' => 'A2EUQ1WTGCTBG2', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.ae', 'domain' => 'amazon.ae'),
		),
		'ebay' => array(
			'name' => 'eBay US / Motors', 'family' => 'eBay', 'region' => 'Americas', 'accent' => '#e53238', 'icon' => 'fa-shopping-bag',
			'blurb' => 'eBay Sell API — US + Motors inventory',
			'marketplace_id' => 'EBAY-US', 'api' => 'Sell API',
			'config' => array('site_id' => 0, 'marketplace' => 'eBay Motors', 'domain' => 'ebay.com'),
		),
		'amazon_com' => array(
			'name' => 'Amazon.com', 'family' => 'Amazon', 'region' => 'Americas', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon US — SP-API North America',
			'marketplace_id' => 'ATVPDKIKX0DER', 'api' => 'SP-API',
			'config' => array('region' => 'us-east-1', 'marketplace' => 'Amazon.com', 'domain' => 'amazon.com'),
		),
		'amazon_ca' => array(
			'name' => 'Amazon.ca', 'family' => 'Amazon', 'region' => 'Americas', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Canada',
			'marketplace_id' => 'A2EUQ1WTGCTBG2', 'api' => 'SP-API',
			'config' => array('region' => 'us-east-1', 'marketplace' => 'Amazon.ca', 'domain' => 'amazon.ca'),
		),
		'amazon_mx' => array(
			'name' => 'Amazon.com.mx', 'family' => 'Amazon', 'region' => 'Americas', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Mexico',
			'marketplace_id' => 'A1AM78C64UM0Y8', 'api' => 'SP-API',
			'config' => array('region' => 'us-east-1', 'marketplace' => 'Amazon.com.mx', 'domain' => 'amazon.com.mx'),
		),
		'amazon_br' => array(
			'name' => 'Amazon.com.br', 'family' => 'Amazon', 'region' => 'Americas', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Brazil',
			'marketplace_id' => 'A2Q3Y263D00KWC', 'api' => 'SP-API',
			'config' => array('region' => 'us-east-1', 'marketplace' => 'Amazon.com.br', 'domain' => 'amazon.com.br'),
		),
		'amazon_uk' => array(
			'name' => 'Amazon.co.uk', 'family' => 'Amazon', 'region' => 'Europe', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon United Kingdom',
			'marketplace_id' => 'A1F83G8C2ARO7P', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.co.uk', 'domain' => 'amazon.co.uk'),
		),
		'amazon_de' => array(
			'name' => 'Amazon.de', 'family' => 'Amazon', 'region' => 'Europe', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Germany',
			'marketplace_id' => 'A1PA6795UKMFR9', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.de', 'domain' => 'amazon.de'),
		),
		'amazon_fr' => array(
			'name' => 'Amazon.fr', 'family' => 'Amazon', 'region' => 'Europe', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon France',
			'marketplace_id' => 'A13V1IB3VIYZZH', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.fr', 'domain' => 'amazon.fr'),
		),
		'amazon_it' => array(
			'name' => 'Amazon.it', 'family' => 'Amazon', 'region' => 'Europe', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Italy',
			'marketplace_id' => 'APJ6JRA9NG5V4', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.it', 'domain' => 'amazon.it'),
		),
		'amazon_es' => array(
			'name' => 'Amazon.es', 'family' => 'Amazon', 'region' => 'Europe', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Spain',
			'marketplace_id' => 'A1RKKUPIHCS9HS', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.es', 'domain' => 'amazon.es'),
		),
		'amazon_nl' => array(
			'name' => 'Amazon.nl', 'family' => 'Amazon', 'region' => 'Europe', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Netherlands',
			'marketplace_id' => 'A1805IZSGTT6HS', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.nl', 'domain' => 'amazon.nl'),
		),
		'amazon_sa' => array(
			'name' => 'Amazon.sa', 'family' => 'Amazon', 'region' => 'MENA', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Saudi Arabia',
			'marketplace_id' => 'A17E79C6D8DWNP', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.sa', 'domain' => 'amazon.sa'),
		),
		'amazon_eg' => array(
			'name' => 'Amazon.eg', 'family' => 'Amazon', 'region' => 'MENA', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Egypt',
			'marketplace_id' => 'ARBP9OOSHTCHU', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.eg', 'domain' => 'amazon.eg'),
		),
		'amazon_in' => array(
			'name' => 'Amazon.in', 'family' => 'Amazon', 'region' => 'Asia', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon India',
			'marketplace_id' => 'A21TJRUUN4KGV', 'api' => 'SP-API',
			'config' => array('region' => 'eu-west-1', 'marketplace' => 'Amazon.in', 'domain' => 'amazon.in'),
		),
		'amazon_au' => array(
			'name' => 'Amazon.com.au', 'family' => 'Amazon', 'region' => 'Asia', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Australia',
			'marketplace_id' => 'A39IBJ37TRP1C6', 'api' => 'SP-API',
			'config' => array('region' => 'us-west-2', 'marketplace' => 'Amazon.com.au', 'domain' => 'amazon.com.au'),
		),
		'amazon_jp' => array(
			'name' => 'Amazon.co.jp', 'family' => 'Amazon', 'region' => 'Asia', 'accent' => '#ff9900', 'icon' => 'fa-amazon',
			'blurb' => 'Amazon Japan',
			'marketplace_id' => 'A1VC38T7YXB528', 'api' => 'SP-API',
			'config' => array('region' => 'us-west-2', 'marketplace' => 'Amazon.co.jp', 'domain' => 'amazon.co.jp'),
		),
		'ebay_uk' => array(
			'name' => 'eBay UK', 'family' => 'eBay', 'region' => 'Europe', 'accent' => '#e53238', 'icon' => 'fa-shopping-bag',
			'blurb' => 'eBay.co.uk Sell API',
			'marketplace_id' => 'EBAY-GB', 'api' => 'Sell API',
			'config' => array('site_id' => 3, 'marketplace' => 'eBay UK', 'domain' => 'ebay.co.uk'),
		),
		'ebay_de' => array(
			'name' => 'eBay Germany', 'family' => 'eBay', 'region' => 'Europe', 'accent' => '#e53238', 'icon' => 'fa-shopping-bag',
			'blurb' => 'eBay.de Sell API',
			'marketplace_id' => 'EBAY-DE', 'api' => 'Sell API',
			'config' => array('site_id' => 77, 'marketplace' => 'eBay DE', 'domain' => 'ebay.de'),
		),
		'ebay_au' => array(
			'name' => 'eBay Australia', 'family' => 'eBay', 'region' => 'Asia', 'accent' => '#e53238', 'icon' => 'fa-shopping-bag',
			'blurb' => 'eBay.com.au Sell API',
			'marketplace_id' => 'EBAY-AU', 'api' => 'Sell API',
			'config' => array('site_id' => 15, 'marketplace' => 'eBay AU', 'domain' => 'ebay.com.au'),
		),
		'ebay_ca' => array(
			'name' => 'eBay Canada', 'family' => 'eBay', 'region' => 'Americas', 'accent' => '#e53238', 'icon' => 'fa-shopping-bag',
			'blurb' => 'eBay.ca Sell API',
			'marketplace_id' => 'EBAY-CA', 'api' => 'Sell API',
			'config' => array('site_id' => 2, 'marketplace' => 'eBay CA', 'domain' => 'ebay.ca'),
		),
		'ebay_fr' => array(
			'name' => 'eBay France', 'family' => 'eBay', 'region' => 'Europe', 'accent' => '#e53238', 'icon' => 'fa-shopping-bag',
			'blurb' => 'eBay.fr Sell API',
			'marketplace_id' => 'EBAY-FR', 'api' => 'Sell API',
			'config' => array('site_id' => 71, 'marketplace' => 'eBay FR', 'domain' => 'ebay.fr'),
		),
		'noon' => array(
			'name' => 'noon UAE', 'family' => 'noon', 'region' => 'MENA', 'accent' => '#feee00', 'icon' => 'fa-sun-o',
			'blurb' => 'noon.com UAE — catalogue & fulfilment',
			'marketplace_id' => 'NOON-AE', 'api' => 'noon Partner',
			'config' => array('marketplace' => 'noon UAE', 'domain' => 'noon.com', 'country' => 'AE'),
		),
		'noon_sa' => array(
			'name' => 'noon KSA', 'family' => 'noon', 'region' => 'MENA', 'accent' => '#feee00', 'icon' => 'fa-sun-o',
			'blurb' => 'noon.com Saudi Arabia',
			'marketplace_id' => 'NOON-SA', 'api' => 'noon Partner',
			'config' => array('marketplace' => 'noon KSA', 'domain' => 'noon.com', 'country' => 'SA'),
		),
		'noon_eg' => array(
			'name' => 'noon Egypt', 'family' => 'noon', 'region' => 'MENA', 'accent' => '#feee00', 'icon' => 'fa-sun-o',
			'blurb' => 'noon.com Egypt',
			'marketplace_id' => 'NOON-EG', 'api' => 'noon Partner',
			'config' => array('marketplace' => 'noon Egypt', 'domain' => 'noon.com', 'country' => 'EG'),
		),
		'dubizzle' => array(
			'name' => 'dubizzle', 'family' => 'Classifieds', 'region' => 'MENA', 'accent' => '#e31c23', 'icon' => 'fa-tags',
			'blurb' => 'UAE classifieds & auto parts listings',
			'marketplace_id' => 'DUBIZZLE-AE', 'api' => 'Partner API',
			'config' => array('marketplace' => 'dubizzle', 'domain' => 'dubizzle.com', 'country' => 'AE'),
		),
		'salla' => array(
			'name' => 'Salla', 'family' => 'Commerce', 'region' => 'MENA', 'accent' => '#004d40', 'icon' => 'fa-store',
			'blurb' => 'Saudi commerce platform for brands',
			'marketplace_id' => 'SALLA-SA', 'api' => 'Salla API',
			'config' => array('marketplace' => 'Salla', 'domain' => 'salla.sa', 'country' => 'SA'),
		),
		'jumia' => array(
			'name' => 'Jumia', 'family' => 'Commerce', 'region' => 'Africa', 'accent' => '#f68b1e', 'icon' => 'fa-globe',
			'blurb' => 'Pan-African marketplace',
			'marketplace_id' => 'JUMIA', 'api' => 'Seller Center',
			'config' => array('marketplace' => 'Jumia', 'domain' => 'jumia.com'),
		),
		'daraz_pk' => array(
			'name' => 'Daraz Pakistan', 'family' => 'Commerce', 'region' => 'Asia', 'accent' => '#f85606', 'icon' => 'fa-shopping-cart',
			'blurb' => 'Daraz / Alibaba Pakistan',
			'marketplace_id' => 'DARAZ-PK', 'api' => 'Daraz Open',
			'config' => array('marketplace' => 'Daraz PK', 'domain' => 'daraz.pk', 'country' => 'PK'),
		),
		'flipkart' => array(
			'name' => 'Flipkart', 'family' => 'Commerce', 'region' => 'Asia', 'accent' => '#2874f0', 'icon' => 'fa-shopping-cart',
			'blurb' => 'India Flipkart Seller Hub',
			'marketplace_id' => 'FLIPKART-IN', 'api' => 'Seller API',
			'config' => array('marketplace' => 'Flipkart', 'domain' => 'flipkart.com', 'country' => 'IN'),
		),
		'allegro' => array(
			'name' => 'Allegro', 'family' => 'Commerce', 'region' => 'Europe', 'accent' => '#ff5a00', 'icon' => 'fa-shopping-bag',
			'blurb' => 'Poland Allegro REST API',
			'marketplace_id' => 'ALLEGRO-PL', 'api' => 'Allegro REST',
			'config' => array('marketplace' => 'Allegro', 'domain' => 'allegro.pl', 'country' => 'PL'),
		),
		'mercadolibre' => array(
			'name' => 'Mercado Libre', 'family' => 'Commerce', 'region' => 'Americas', 'accent' => '#fff159', 'icon' => 'fa-handshake-o',
			'blurb' => 'LATAM Mercado Libre',
			'marketplace_id' => 'MELI', 'api' => 'MELI API',
			'config' => array('marketplace' => 'Mercado Libre', 'domain' => 'mercadolibre.com'),
		),
		'walmart' => array(
			'name' => 'Walmart Marketplace', 'family' => 'Commerce', 'region' => 'Americas', 'accent' => '#0071ce', 'icon' => 'fa-cube',
			'blurb' => 'Walmart US Marketplace API',
			'marketplace_id' => 'WALMART-US', 'api' => 'Marketplace API',
			'config' => array('marketplace' => 'Walmart', 'domain' => 'walmart.com', 'country' => 'US'),
		),
		'etsy' => array(
			'name' => 'Etsy', 'family' => 'Commerce', 'region' => 'Global', 'accent' => '#f56400', 'icon' => 'fa-heart',
			'blurb' => 'Etsy Open API v3 listings',
			'marketplace_id' => 'ETSY', 'api' => 'Open API v3',
			'config' => array('marketplace' => 'Etsy', 'domain' => 'etsy.com'),
		),
		'shopify' => array(
			'name' => 'Shopify Channel', 'family' => 'Commerce', 'region' => 'Global', 'accent' => '#95bf47', 'icon' => 'fa-shopping-bag',
			'blurb' => 'Push catalogue to any Shopify storefront',
			'marketplace_id' => 'SHOPIFY', 'api' => 'Admin API',
			'config' => array('marketplace' => 'Shopify', 'domain' => 'myshopify.com'),
		),
	);
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
	$active = (int)$db->query('SELECT COUNT(*) FROM `epc_marketplace_channels` WHERE `active` = 1')->fetchColumn();
	$total = (int)$db->query('SELECT COUNT(*) FROM `epc_marketplace_channels`')->fetchColumn();
	$regions = array();
	foreach (epc_channel_marketplaces_catalog() as $meta) {
		$r = isset($meta['region']) ? (string)$meta['region'] : 'Global';
		$regions[$r] = true;
	}
	return array(
		'marketplace_orders' => $mp_orders,
		'marketplace_pending' => $mp_pending,
		'sku_mapped' => $sku_count,
		'channels_active' => $active,
		'channels_total' => $total,
		'catalog_count' => count(epc_channel_marketplaces_catalog()),
		'regions' => count($regions),
	);
}

function epc_channel_seed_defaults(PDO $db)
{
	epc_channel_ensure_schema($db);
	$now = time();
	$ins = $db->prepare(
		'INSERT INTO `epc_marketplace_channels` (`code`, `name`, `marketplace_id`, `active`, `demo_mode`, `config_json`, `time_created`)
		 VALUES (?, ?, ?, 1, 1, ?, ?)
		 ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `marketplace_id` = VALUES(`marketplace_id`)'
	);
	foreach (epc_channel_marketplaces_catalog() as $code => $meta) {
		$config = isset($meta['config']) && is_array($meta['config']) ? $meta['config'] : array();
		$config['region_label'] = isset($meta['region']) ? (string)$meta['region'] : 'Global';
		$config['family'] = isset($meta['family']) ? (string)$meta['family'] : '';
		$config['api'] = isset($meta['api']) ? (string)$meta['api'] : '';
		$ins->execute(array(
			$code,
			(string)($meta['name'] ?? strtoupper($code)),
			(string)($meta['marketplace_id'] ?? ''),
			json_encode($config, JSON_UNESCAPED_UNICODE),
			$now,
		));
	}
}

function epc_channel_seed_sample_data(PDO $db)
{
	epc_channel_seed_defaults($db);
	$now = time();

	$ch = $db->query('SELECT `id`, `code` FROM `epc_marketplace_channels`')->fetchAll(PDO::FETCH_KEY_PAIR);
	$amazon_id = 0;
	$ebay_id = 0;
	$noon_id = 0;
	foreach ($ch as $id => $code) {
		if ($code === 'amazon') {
			$amazon_id = (int)$id;
		}
		if ($code === 'ebay') {
			$ebay_id = (int)$id;
		}
		if ($code === 'noon') {
			$noon_id = (int)$id;
		}
	}

	$skus = array(
		array($amazon_id, 'BOSCH', '0986424590', 'AMZ-0986424590', 'B0123456789', 'Bosch Oil Filter', 42.00, 25),
		array($amazon_id, 'MANN', 'HU7008Z', 'AMZ-HU7008Z', 'B0987654321', 'Mann Oil Filter', 38.50, 18),
		array($ebay_id, 'NGK', 'BKR6E', 'EBY-BKR6E', null, 'NGK Spark Plug BKR6E', 12.00, 120),
		array($ebay_id, 'VALEO', '828038', 'EBY-828038', null, 'Valeo Clutch Kit', 285.00, 4),
		array($noon_id, 'BOSCH', '0986424590', 'NOON-0986424590', null, 'Bosch Oil Filter (noon)', 44.00, 20),
		array($noon_id, 'NGK', 'BKR6E', 'NOON-BKR6E', null, 'NGK Spark Plug BKR6E (noon)', 13.50, 80),
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
		array($noon_id, 'NOON-AE-778812', 'awaiting_shipment', 'Sara Hassan', 'sara.sample@example.com', 'Abu Dhabi', 'AE', 57.50,
			array(array('sku' => 'NOON-0986424590', 'qty' => 1, 'price' => 44.00), array('sku' => 'NOON-BKR6E', 'qty' => 1, 'price' => 13.50))),
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
		'sync_log' => $db->query("SELECT * FROM `epc_channel_sync_log` WHERE `kind` IN ('inventory_sync','order_import','seed','channel') OR `channel_code` = 'system' ORDER BY `id` DESC LIMIT 15")->fetchAll(PDO::FETCH_ASSOC),
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
