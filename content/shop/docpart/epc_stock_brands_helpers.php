<?php
/**
 * Shared helpers: customer office price lists and in-stock brand aggregates.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_stock_brand_price_ids($db_link)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/order_process/get_customer_offices.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php';

	$price_ids = array();
	foreach ($customer_offices as $office_id) {
		$sq = $db_link->prepare(
			'SELECT s.`connection_options` FROM `shop_storages` AS s '
			. 'INNER JOIN `shop_offices_storages_map` AS m ON m.`storage_id` = s.`id` '
			. 'WHERE m.`office_id` = ? AND s.`interface_type` = 2 AND s.`hidden` = 0 '
			. 'AND ' . epc_ssf_storage_active_sql('s')
		);
		$sq->execute(array((int) $office_id));
		while ($srow = $sq->fetch(PDO::FETCH_ASSOC)) {
			$co = json_decode($srow['connection_options'], true);
			if (!empty($co['price_id'])) {
				$price_ids[(int) $co['price_id']] = true;
			}
		}
	}
	$price_ids = array_keys($price_ids);
	$price_ids = epc_ssf_filter_enabled_price_ids($db_link, $price_ids);
	if (count($price_ids) === 0) {
		$price_ids = array(1);
	}
	return $price_ids;
}

/**
 * All price list IDs that currently have in-stock rows (site-wide fallback).
 *
 * @return array<int, int>
 */
function epc_stock_brand_price_ids_with_stock($db_link)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php';
	$ids = array();
	try {
		$q = $db_link->query(
			'SELECT DISTINCT d.`price_id` FROM `shop_docpart_prices_data` d '
			. 'INNER JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id` '
			. 'WHERE IFNULL(d.`exist`, 0) > 0 AND IFNULL(d.`price`, 0) > 0 '
			. 'AND TRIM(IFNULL(d.`manufacturer`, \'\')) != \'\' '
			. 'AND TRIM(IFNULL(d.`article`, \'\')) != \'\' '
			. 'AND ' . epc_ssf_storage_active_sql('p')
		);
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$ids[] = (int) $row['price_id'];
		}
	} catch (Exception $e) {
	}
	$ids = array_values(array_unique(array_filter($ids)));
	return count($ids) > 0 ? epc_ssf_filter_enabled_price_ids($db_link, $ids) : array(1);
}

/**
 * @return array<int, array{name: string, parts_count: int, letter: string}>
 */
function epc_stock_brands_with_counts($db_link, array $price_ids)
{
	if (count($price_ids) === 0) {
		return array();
	}

	$placeholders = implode(',', array_fill(0, count($price_ids), '?'));
	$articleExpr = "COALESCE(NULLIF(TRIM(`article_show`), ''), TRIM(`article`))";
	$sql = 'SELECT MIN(TRIM(`manufacturer`)) AS `name`, '
		. 'COUNT(DISTINCT ' . $articleExpr . ') AS `parts_count` '
		. 'FROM `shop_docpart_prices_data` '
		. 'WHERE `price_id` IN (' . $placeholders . ') '
		. 'AND TRIM(IFNULL(`manufacturer`, \'\')) != \'\' '
		. 'AND ' . $articleExpr . ' != \'\' '
		. 'AND IFNULL(`price`, 0) > 0 '
		. 'AND IFNULL(`exist`, 0) > 0 '
		. 'GROUP BY UPPER(TRIM(`manufacturer`)) '
		. 'ORDER BY UPPER(TRIM(`manufacturer`)) ASC';

	$stmt = $db_link->prepare($sql);
	$stmt->execute($price_ids);
	$brands = array();
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$name = trim((string) $row['name']);
		if ($name === '') {
			continue;
		}
		$first = mb_strtoupper(mb_substr($name, 0, 1, 'UTF-8'), 'UTF-8');
		if (preg_match('/^[0-9]/u', $first)) {
			$letter = '0-9';
		} elseif (preg_match('/^[A-Z]$/u', $first)) {
			$letter = $first;
		} else {
			$letter = '#';
		}
		$brands[] = array(
			'name' => $name,
			'parts_count' => (int) $row['parts_count'],
			'letter' => $letter,
		);
	}
	return $brands;
}

/**
 * @return array{0: string, 1: string}
 */
function epc_stock_brand_match_params($brand)
{
	$upper = mb_strtoupper(trim((string) $brand), 'UTF-8');
	$compact = preg_replace('/[^A-Z0-9А-ЯЁ]+/u', '', $upper);
	return array($upper, $compact);
}

/**
 * In-stock part rows for one manufacturer (office price lists, then site-wide fallback).
 *
 * @return array<int, array<string, mixed>>
 */
function epc_stock_brand_parts_for_manufacturer($db_link, $brand, $limit = 5000)
{
	$brand = trim((string) $brand);
	if ($brand === '') {
		return array();
	}
	if ($limit < 1 || $limit > 10000) {
		$limit = 5000;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php';

	list($brandUpper, $brandCompact) = epc_stock_brand_match_params($brand);
	$articleExpr = "COALESCE(NULLIF(TRIM(`article_show`), ''), TRIM(`article`))";
	$where = 'TRIM(IFNULL(`manufacturer`, \'\')) != \'\' AND ' . $articleExpr . ' != \'\' '
		. 'AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0 '
		. 'AND (UPPER(TRIM(`manufacturer`)) = ? OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(`manufacturer`)), \' \', \'\'), \'-\', \'\'), \'.\', \'\') = ?)';

	$load = function (array $price_ids) use ($db_link, $where, $articleExpr, $brandUpper, $brandCompact, $limit) {
		$price_ids = epc_ssf_filter_enabled_price_ids($db_link, $price_ids);
		if (count($price_ids) === 0) {
			return array();
		}
		$placeholders = implode(',', array_fill(0, count($price_ids), '?'));
		$sql = 'SELECT UPPER(TRIM(`manufacturer`)) AS `manufacturer_key`, MIN(TRIM(`manufacturer`)) AS `manufacturer`, '
			. 'MIN(`article`) AS `article`, MIN(' . $articleExpr . ') AS `article_show`, '
			. 'MIN(`name`) AS `name`, SUM(IFNULL(`exist`, 0)) AS `exist`, MIN(`price`) AS `price`, '
			. 'MIN(`time_to_exe`) AS `time_to_exe`, MIN(`storage`) AS `storage`, MIN(`min_order`) AS `min_order` '
			. 'FROM `shop_docpart_prices_data` '
			. 'WHERE `price_id` IN (' . $placeholders . ') AND ' . $where . ' '
			. 'GROUP BY UPPER(TRIM(`manufacturer`)), ' . $articleExpr . ' '
			. 'ORDER BY MIN(`price`) ASC, `article_show` ASC LIMIT ' . (int) $limit;
		$params = array_merge($price_ids, array($brandUpper, $brandCompact));
		$stmt = $db_link->prepare($sql);
		$stmt->execute($params);
		return $stmt->fetchAll(PDO::FETCH_ASSOC);
	};

	$rows = $load(epc_stock_brand_price_ids($db_link));
	if (count($rows) === 0) {
		$rows = $load(epc_stock_brand_price_ids_with_stock($db_link));
	}
	return $rows;
}

/**
 * Tag in-stock brands as genuine (UMAPI OE) or aftermarket for /parts index.
 *
 * @param PDO $db_link
 * @param array<int, array{name: string, parts_count: int, letter: string}> $brands
 * @param string $catalog_url
 * @return array<int, array{name: string, parts_count: int, letter: string, part_type: string}>
 */
function epc_stock_brands_tag_part_types($db_link, array $brands, $catalog_url = '')
{
	if (count($brands) === 0) {
		return $brands;
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_genuine_manufacturers.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_manufacturer_synonyms.php';

	global $DP_Config;
	$genuine_index = epc_genuine_build_frontend_index($db_link, $DP_Config, $catalog_url);
	$genuine_keys = isset($genuine_index['brands']) && is_array($genuine_index['brands'])
		? $genuine_index['brands']
		: array();
	$synonym_map = docpart_load_manufacturer_synonym_map($db_link);

	foreach ($brands as $idx => $brand_row) {
		$name = isset($brand_row['name']) ? trim((string) $brand_row['name']) : '';
		$is_genuine = false;
		if ($name !== '') {
			$equiv = docpart_synonym_names_for_brand($name, $synonym_map);
			foreach ($equiv as $equiv_name) {
				$key = docpart_synonym_normalize_brand($equiv_name);
				if ($key !== '' && !empty($genuine_keys[$key])) {
					$is_genuine = true;
					break;
				}
			}
		}
		$brands[$idx]['part_type'] = $is_genuine ? 'genuine' : 'aftermarket';
	}

	return $brands;
}
