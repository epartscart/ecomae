<?php
/**
 * Build Docpart-shaped product list from uploaded price lists (for CHPU part pages).
 */
defined('_ASTEXE_') or die('No access');

function epc_build_initial_price_bunch($db_link, $DP_Config, $article, $manufacturer, $group_id, $user_id, $customer_offices)
{
	$result = array(
		'result' => 0,
		'storage_id' => 0,
		'Products' => array(),
	);

	if ($article === '' || $manufacturer === '') {
		return $result;
	}

	require_once($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php');
	require_once($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/suppliers_handlers/prices/common_interface.php');

	$epc_default_office_id = !empty($customer_offices) ? (int)$customer_offices[0] : 1;
	$office_storage_bunches = array();
	$epc_mfr_show = mb_strtoupper(html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8'), 'UTF-8');
	$epc_mfr_names = array($epc_mfr_show);

	try {
		$synonym_query = $db_link->prepare('SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = (SELECT `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1);');
		$synonym_query->execute(array($epc_mfr_show));
		$synonym_record = $synonym_query->fetch(PDO::FETCH_ASSOC);
		if ($synonym_record && !empty($synonym_record['name'])) {
			$epc_mfr_names[] = mb_strtoupper(trim($synonym_record['name']), 'UTF-8');
		}
		$synonyms_query = $db_link->prepare('SELECT `synonym` FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = (SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1);');
		$synonyms_query->execute(array($epc_mfr_show));
		while ($synonym_row = $synonyms_query->fetch(PDO::FETCH_ASSOC)) {
			if (!empty($synonym_row['synonym'])) {
				$epc_mfr_names[] = mb_strtoupper(trim($synonym_row['synonym']), 'UTF-8');
			}
		}
	} catch (Exception $e) {
	}

	$epc_mfr_names = array_values(array_unique($epc_mfr_names));
	$manufacturers_for_prices = array();
	foreach ($epc_mfr_names as $epc_mfr_name) {
		$manufacturers_for_prices[] = array('manufacturer' => $epc_mfr_name);
	}

	try {
		$price_storages_query = $db_link->prepare(
			'SELECT `id` FROM `shop_storages`
			WHERE `interface_type` IN (SELECT `id` FROM `shop_storages_interfaces_types` WHERE `handler_folder` = ?)
			AND `hidden` = 0
			ORDER BY `id`;'
		);
		$price_storages_query->execute(array('prices'));
		while ($price_storage = $price_storages_query->fetch(PDO::FETCH_ASSOC)) {
			$storage_id = (int)$price_storage['id'];
			$office_id = $epc_default_office_id;
			foreach ($customer_offices as $customer_office_id) {
				$office_map_query = $db_link->prepare('SELECT `office_id` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? LIMIT 1;');
				$office_map_query->execute(array((int)$customer_office_id, $storage_id));
				if ($office_map_query->fetch()) {
					$office_id = (int)$customer_office_id;
					break;
				}
			}
			$office_storage_bunches[] = array(
				'office_id' => $office_id,
				'storage_id' => $storage_id,
			);
		}
	} catch (Exception $e) {
		return $result;
	}

	if (empty($office_storage_bunches)) {
		return $result;
	}

	$storage_options = array(
		'user_id' => (int)$user_id,
		'group_id' => (int)$group_id,
		'customer_group_id' => (int)$group_id,
		'office_storage_bunches' => $office_storage_bunches,
		'analogs' => array(),
	);

	$prices = new prices_enclosure($article, $manufacturers_for_prices, $storage_options, $article);
	if (empty($prices->Products)) {
		$prices = new prices_enclosure($article, array(), $storage_options, $article);
	}

	$products = array();
	foreach ($prices->Products as $product) {
		if (!is_object($product) || empty($product->valid)) {
			continue;
		}
		$products[] = $product;
	}

	$result['result'] = count($products) > 0 ? 1 : 0;
	$result['Products'] = $products;

	return $result;
}
