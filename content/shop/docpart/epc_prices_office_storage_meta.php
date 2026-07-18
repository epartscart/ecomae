<?php
/**
 * Batched warehouse/office metadata for prices_enclosure — kills N+1 on search.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

/**
 * Build office_storage_dataInfo + price_ids from bunches in a few queries.
 *
 * @param array<int, array{office_id?:int|string, storage_id?:int|string}> $office_storage_bunches
 * @return array{0: array<int, list<array<string,mixed>>>, 1: list<int>}
 */
function epc_prices_build_office_storage_data_info(
	PDO $db_link,
	array $office_storage_bunches,
	int $user_id,
	int $group_id
): array {
	$office_storage_dataInfo = array();
	$price_ids_for_query = array();

	$pairs = array();
	$storageIds = array();
	$officeIds = array();
	foreach ($office_storage_bunches as $bunch) {
		if (!is_array($bunch)) {
			continue;
		}
		$officeId = (int) ($bunch['office_id'] ?? 0);
		$storageId = (int) ($bunch['storage_id'] ?? 0);
		if ($storageId < 1) {
			continue;
		}
		$pairs[] = array($officeId, $storageId);
		$storageIds[$storageId] = true;
		if ($officeId > 0) {
			$officeIds[$officeId] = true;
		}
	}
	if (!$pairs) {
		return array($office_storage_dataInfo, $price_ids_for_query);
	}

	$disabledStorages = array();
	$disabledPrices = array();
	if (function_exists('epc_ssf_disabled_storage_ids')) {
		$disabledStorages = epc_ssf_disabled_storage_ids($db_link);
	}
	if (function_exists('epc_ssf_disabled_price_ids')) {
		$disabledPrices = epc_ssf_disabled_price_ids($db_link);
	}

	$storageIdList = array_keys($storageIds);
	$ph = implode(',', array_fill(0, count($storageIdList), '?'));
	$storages = array();
	$st = $db_link->prepare(
		'SELECT s.`id`, s.`connection_options`, s.`name`, s.`short_name`,
			(SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = s.`currency`) AS `rate`,
			IFNULL(s.`storefront_temp_disabled`, 0) AS `storefront_temp_disabled`
		 FROM `shop_storages` s
		 WHERE s.`id` IN (' . $ph . ')'
	);
	$st->execute($storageIdList);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$storages[(int) $row['id']] = $row;
	}

	$offices = array();
	if ($officeIds) {
		$officeList = array_keys($officeIds);
		$oph = implode(',', array_fill(0, count($officeList), '?'));
		$oq = $db_link->prepare('SELECT `id`, `caption` FROM `shop_offices` WHERE `id` IN (' . $oph . ')');
		$oq->execute($officeList);
		while ($row = $oq->fetch(PDO::FETCH_ASSOC)) {
			$offices[(int) $row['id']] = (string) ($row['caption'] ?? '');
		}
	}

	// Map rows: additional_time + markups for this group in one query.
	$mapByPair = array();
	$markupsByPair = array();
	$mq = $db_link->prepare(
		'SELECT `office_id`, `storage_id`, `additional_time`, `min_point`, `max_point`, `markup`/100 AS `markup`
		 FROM `shop_offices_storages_map`
		 WHERE `group_id` = ? AND `storage_id` IN (' . $ph . ')
		 ORDER BY `office_id`, `storage_id`, `min_point`'
	);
	$mq->execute(array_merge(array($group_id), $storageIdList));
	while ($row = $mq->fetch(PDO::FETCH_ASSOC)) {
		$oid = (int) $row['office_id'];
		$sid = (int) $row['storage_id'];
		$key = $oid . ':' . $sid;
		if (!isset($mapByPair[$key])) {
			$mapByPair[$key] = (int) ($row['additional_time'] ?? 0);
			$markupsByPair[$key] = array();
		}
		$markupsByPair[$key][] = array(
			'min_point' => $row['min_point'],
			'max_point' => $row['max_point'],
			'markup' => $row['markup'],
		);
	}

	// Manager full-name override only when logged in (guest skip = big win).
	$managerCaptions = array();
	if ($user_id > 0) {
		$like = '%"' . $user_id . '"%';
		$cq = $db_link->prepare(
			'SELECT m.`office_id`, m.`storage_id`, s.`name` AS `storage_caption`
			 FROM `shop_offices` o
			 INNER JOIN `shop_offices_storages_map` m ON o.`id` = m.`office_id`
			 INNER JOIN `shop_storages` s ON s.`id` = m.`storage_id`
			 WHERE o.`users` LIKE ? AND s.`id` IN (' . $ph . ')'
		);
		$cq->execute(array_merge(array($like), $storageIdList));
		while ($row = $cq->fetch(PDO::FETCH_ASSOC)) {
			$managerCaptions[((int) $row['office_id']) . ':' . ((int) $row['storage_id'])] =
				(string) $row['storage_caption'];
		}
	}

	foreach ($pairs as $pair) {
		$officeId = $pair[0];
		$storageId = $pair[1];
		if (isset($disabledStorages[$storageId])) {
			continue;
		}
		$row = $storages[$storageId] ?? null;
		if (!$row || (int) ($row['storefront_temp_disabled'] ?? 0) === 1) {
			continue;
		}
		$connection_options = json_decode((string) ($row['connection_options'] ?? ''), true);
		if (!is_array($connection_options) || empty($connection_options['price_id'])) {
			continue;
		}
		$priceId = (int) $connection_options['price_id'];
		if ($priceId < 1 || isset($disabledPrices[$priceId])) {
			continue;
		}

		$key = $officeId . ':' . $storageId;
		$storageCaption = !empty($row['short_name']) ? (string) $row['short_name'] : (string) $row['name'];
		if (!empty($managerCaptions[$key])) {
			$storageCaption = $managerCaptions[$key];
		}
		$additionalTime = isset($mapByPair[$key]) ? (int) ($mapByPair[$key] / 24) : 0;
		$markups = $markupsByPair[$key] ?? array();

		$info = array(
			'price_id' => $priceId,
			'storage_id' => $storageId,
			'office_id' => $officeId,
			'probability' => (int) ($connection_options['probability'] ?? 0),
			'color' => $connection_options['color'] ?? '',
			'rate' => $row['rate'],
			'additional_time' => $additionalTime,
			'office_caption' => $offices[$officeId] ?? '',
			'storage_caption' => $storageCaption,
			'markups' => $markups,
		);
		$price_ids_for_query[] = $priceId;
		if (!isset($office_storage_dataInfo[$priceId])) {
			$office_storage_dataInfo[$priceId] = array();
		}
		$office_storage_dataInfo[$priceId][] = $info;
	}

	return array($office_storage_dataInfo, array_values(array_unique($price_ids_for_query)));
}
