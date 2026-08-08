<?php
/**
 * Public office/storage bunches for progressive warehouse poll (PHP part_search twin).
 * Used by ASP.NET GET /storefront/search-bunches when tenant SQL is empty or mis-bound.
 * Read-only JSON — same shape as part_search_page.php office_storage_bunches.
 */
header('Content-Type: application/json;charset=utf-8;');
header('Cache-Control: no-store');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';

$DP_Config = new DP_Config;
$article_input = isset($_GET['article']) ? trim((string) $_GET['article']) : '';
if ($article_input === '' && isset($_POST['article'])) {
	$article_input = trim((string) $_POST['article']);
}
$brand_input = isset($_GET['brand']) ? trim((string) $_GET['brand']) : '';
if ($brand_input === '' && isset($_GET['brend'])) {
	$brand_input = trim((string) $_GET['brend']);
}

try {
	$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->query('SET NAMES utf8;');
} catch (Exception $e) {
	echo json_encode(array(
		'status' => false,
		'article' => $article_input,
		'brand' => $brand_input,
		'count' => 0,
		'bunches' => array(),
		'source' => 'php-chpu',
		'message' => 'Database unavailable',
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

$epc_customer_offices_path = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/order_process/get_customer_offices.php';
require_once $epc_customer_offices_path;
if (!isset($customer_offices) || !is_array($customer_offices)) {
	include $epc_customer_offices_path;
}
if (!isset($customer_offices) || !is_array($customer_offices)) {
	$customer_offices = array();
}

$office_storage_bunches = array();
$office_storage_bunches_prices = array();

for ($i = 0; $i < count($customer_offices); $i++) {
	$storages_query = $db_link->prepare(
		'SELECT DISTINCT(storage_id) AS storage_id,
			(SELECT `handler_folder` FROM `shop_storages_interfaces_types`
			 WHERE `id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = `shop_offices_storages_map`.`storage_id`)) AS `handler_folder`
		 FROM shop_offices_storages_map
		 WHERE office_id = ?
		   AND storage_id IN (SELECT id FROM shop_storages WHERE interface_type > 1 AND `hidden` = 0)'
	);
	$storages_query->execute(array($customer_offices[$i]));
	while ($storage = $storages_query->fetch(PDO::FETCH_ASSOC)) {
		$protocol_version = 1;
		$handler = isset($storage['handler_folder']) ? (string) $storage['handler_folder'] : '';
		if ($handler !== '' && is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/suppliers_handlers/' . $handler . '/get_manufacturers.php')) {
			$protocol_version = 2;
		}
		if ($handler !== 'prices') {
			$treelax_catalogue = ($handler === 'treelax_catalogue');
			$office_storage_bunches[] = array(
				'office_id' => (int) $customer_offices[$i],
				'storage_id' => (int) $storage['storage_id'],
				'sent' => 0,
				'protocol_version' => $protocol_version,
				'manufacturers_sent' => 0,
				'treelax_catalogue' => $treelax_catalogue,
			);
		} else {
			$office_storage_bunches_prices[] = array(
				'office_id' => (int) $customer_offices[$i],
				'storage_id' => (int) $storage['storage_id'],
				'sent' => 0,
				'protocol_version' => $protocol_version,
				'manufacturers_sent' => 0,
			);
		}
	}
	if (count($office_storage_bunches_prices) > 0) {
		array_unshift($office_storage_bunches, array(
			'office_id' => 0,
			'storage_id' => 0,
			'sent' => 0,
			'protocol_version' => 3,
			'manufacturers_sent' => 0,
			'office_storage_bunches' => $office_storage_bunches_prices,
		));
		$office_storage_bunches_prices = array();
	}
}

$epc_default_office_id = !empty($customer_offices) ? (int) $customer_offices[0] : 1;

if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php';
	if (function_exists('epc_ssf_filter_office_storage_bunches')) {
		$office_storage_bunches = epc_ssf_filter_office_storage_bunches($db_link, $office_storage_bunches);
	}
}

$epc_has_price_bunch = false;
foreach ($office_storage_bunches as $epc_bunch_check) {
	if (isset($epc_bunch_check['protocol_version']) && (int) $epc_bunch_check['protocol_version'] === 3) {
		$epc_has_price_bunch = true;
		break;
	}
}
if (!$epc_has_price_bunch) {
	$epc_fallback_price_bunches = array();
	try {
		$epc_fallback_storages_query = $db_link->prepare(
			'SELECT `id` FROM `shop_storages`
			 WHERE `interface_type` IN (SELECT `id` FROM `shop_storages_interfaces_types` WHERE `handler_folder` = ?)
			   AND `hidden` = 0
			 ORDER BY `id`'
		);
		$epc_fallback_storages_query->execute(array('prices'));
		while ($epc_fallback_storage = $epc_fallback_storages_query->fetch(PDO::FETCH_ASSOC)) {
			$epc_fallback_price_bunches[] = array(
				'office_id' => $epc_default_office_id,
				'storage_id' => (int) $epc_fallback_storage['id'],
				'sent' => 0,
				'protocol_version' => 1,
				'manufacturers_sent' => 0,
			);
		}
	} catch (Exception $e) {
		$epc_fallback_price_bunches = array();
	}
	if (!empty($epc_fallback_price_bunches)) {
		array_unshift($office_storage_bunches, array(
			'office_id' => 0,
			'storage_id' => 0,
			'sent' => 0,
			'protocol_version' => 3,
			'manufacturers_sent' => 0,
			'office_storage_bunches' => $epc_fallback_price_bunches,
		));
	}
}

// Expand protocol-3 nested lists with every active price-list storage (PHP part_search parity).
for ($epc_bunch_index = 0; $epc_bunch_index < count($office_storage_bunches); $epc_bunch_index++) {
	if ((int) $office_storage_bunches[$epc_bunch_index]['protocol_version'] !== 3
		|| empty($office_storage_bunches[$epc_bunch_index]['office_storage_bunches'])) {
		continue;
	}
	$epc_existing_storage_ids = array();
	foreach ($office_storage_bunches[$epc_bunch_index]['office_storage_bunches'] as $epc_price_bunch) {
		$epc_existing_storage_ids[(int) $epc_price_bunch['storage_id']] = true;
	}
	try {
		$epc_all_prices = $db_link->prepare(
			'SELECT `id` FROM `shop_storages`
			 WHERE `interface_type` IN (SELECT `id` FROM `shop_storages_interfaces_types` WHERE `handler_folder` = ?)
			   AND `hidden` = 0
			 ORDER BY `id`'
		);
		$epc_all_prices->execute(array('prices'));
		while ($epc_price_storage = $epc_all_prices->fetch(PDO::FETCH_ASSOC)) {
			$sid = (int) $epc_price_storage['id'];
			if (isset($epc_existing_storage_ids[$sid])) {
				continue;
			}
			$office_storage_bunches[$epc_bunch_index]['office_storage_bunches'][] = array(
				'office_id' => $epc_default_office_id,
				'storage_id' => $sid,
				'sent' => 0,
				'protocol_version' => 1,
				'manufacturers_sent' => 0,
			);
		}
	} catch (Exception $e) {
		// keep existing nested bunches
	}
}

echo json_encode(array(
	'status' => true,
	'article' => $article_input,
	'brand' => $brand_input,
	'count' => count($office_storage_bunches),
	'bunches' => $office_storage_bunches,
	'source' => 'php-chpu',
	'message' => '',
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
