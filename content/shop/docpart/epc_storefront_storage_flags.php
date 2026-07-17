<?php
/**
 * Temporary storefront ON/OFF for warehouses and price lists (tenant CP).
 * Data persists in shop_storages / shop_docpart_prices + audit log.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

/**
 * Ensure storefront toggle columns/tables exist.
 * @param bool $allowAlter When false (CP page view), only probe with SELECT —
 *   never ALTER (locks → Cloudflare 524 on large tenants). Setup/AJAX may pass true.
 */
function epc_ssf_ensure_schema(PDO $db, bool $allowAlter = false): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;

	try {
		$db->query('SELECT `storefront_temp_disabled` FROM `shop_storages` LIMIT 1');
	} catch (Throwable $e) {
		if ($allowAlter) {
			try {
				$db->exec(
					'ALTER TABLE `shop_storages` ADD COLUMN `storefront_temp_disabled` TINYINT(1) NOT NULL DEFAULT 0 AFTER `hidden`;'
				);
			} catch (Throwable $e2) {
				// Column may already exist.
			}
		}
	}

	try {
		$db->query('SELECT `storefront_temp_disabled` FROM `shop_docpart_prices` LIMIT 1');
	} catch (Throwable $e) {
		if ($allowAlter) {
			try {
				$db->exec(
					'ALTER TABLE `shop_docpart_prices` ADD COLUMN `storefront_temp_disabled` TINYINT(1) NOT NULL DEFAULT 0;'
				);
			} catch (Throwable $e2) {
				// Column may already exist.
			}
		}
	}

	try {
		$db->exec("CREATE TABLE IF NOT EXISTS `epc_storefront_storage_toggle_audit` (
			`id` INT(11) NOT NULL AUTO_INCREMENT,
			`entity_type` VARCHAR(16) NOT NULL DEFAULT '',
			`entity_id` INT(11) NOT NULL DEFAULT 0,
			`entity_name` VARCHAR(255) NOT NULL DEFAULT '',
			`storefront_disabled` TINYINT(1) NOT NULL DEFAULT 0,
			`user_id` INT(11) NOT NULL DEFAULT 0,
			`user_label` VARCHAR(128) NOT NULL DEFAULT '',
			`created_at` DATETIME NOT NULL,
			PRIMARY KEY (`id`),
			KEY `entity` (`entity_type`, `entity_id`),
			KEY `created_at` (`created_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
	} catch (Throwable $e) {
		// CREATE may wait on metadata locks — never fail the CP page for this.
	}
}

/** SQL fragment: active storages only (append inside WHERE). */
function epc_ssf_storage_active_sql(string $alias = ''): string
{
	$col = ($alias !== '') ? $alias . '.`storefront_temp_disabled`' : '`storefront_temp_disabled`';
	return ' IFNULL(' . $col . ', 0) = 0 ';
}

/** SQL fragment: shop_docpart_prices_data rows on storefront-enabled price lists only. */
function epc_ssf_price_data_active_sql(string $alias = ''): string
{
	$priceCol = ($alias !== '') ? $alias . '.`price_id`' : '`price_id`';
	return $priceCol . ' IN (SELECT `id` FROM `shop_docpart_prices` WHERE ' . epc_ssf_storage_active_sql() . ')';
}

/**
 * @return array<int, true> uppercase warehouse labels hidden from storefront (short_name / name).
 */
function epc_ssf_disabled_warehouse_labels(PDO $db): array
{
	epc_ssf_ensure_schema($db);
	$out = array();
	$q = $db->query(
		'SELECT `name`, `short_name` FROM `shop_storages` WHERE IFNULL(`storefront_temp_disabled`, 0) = 1;'
	);
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		foreach (array('short_name', 'name') as $key) {
			$label = trim((string) ($row[$key] ?? ''));
			if ($label !== '') {
				$out[mb_strtoupper($label, 'UTF-8')] = true;
			}
		}
	}
	return $out;
}

/**
 * @param array<int, int> $priceIds
 * @return array<int, int>
 */
function epc_ssf_filter_enabled_price_ids(PDO $db, array $priceIds): array
{
	$disabled = epc_ssf_disabled_price_ids($db);
	if ($disabled === array()) {
		return array_values(array_unique(array_filter(array_map('intval', $priceIds))));
	}
	$out = array();
	foreach ($priceIds as $priceId) {
		$priceId = (int) $priceId;
		if ($priceId > 0 && !isset($disabled[$priceId])) {
			$out[] = $priceId;
		}
	}
	return array_values(array_unique($out));
}

/**
 * Drop stock/price lines from temporarily disabled warehouses or price lists.
 *
 * @param list<array<string, mixed>> $lines
 * @return list<array<string, mixed>>
 */
function epc_ssf_filter_agent_stock_lines(PDO $db, array $lines): array
{
	if ($lines === array()) {
		return $lines;
	}
	$disabledPrices = epc_ssf_disabled_price_ids($db);
	$disabledLabels = epc_ssf_disabled_warehouse_labels($db);
	if ($disabledPrices === array() && $disabledLabels === array()) {
		return $lines;
	}
	$out = array();
	foreach ($lines as $line) {
		if (!is_array($line)) {
			continue;
		}
		if (!empty($line['price_id']) && isset($disabledPrices[(int) $line['price_id']])) {
			continue;
		}
		$warehouse = trim((string) ($line['warehouse'] ?? $line['storage'] ?? ''));
		if ($warehouse !== '' && isset($disabledLabels[mb_strtoupper($warehouse, 'UTF-8')])) {
			continue;
		}
		$out[] = $line;
	}
	return $out;
}

function epc_ssf_is_storage_disabled(PDO $db, int $storageId): bool
{
	if ($storageId <= 0) {
		return false;
	}
	epc_ssf_ensure_schema($db);
	$stmt = $db->prepare('SELECT IFNULL(`storefront_temp_disabled`, 0) FROM `shop_storages` WHERE `id` = ? LIMIT 1;');
	$stmt->execute(array($storageId));
	$row = $stmt->fetchColumn();
	return $row !== false && (int) $row === 1;
}

function epc_ssf_is_price_disabled(PDO $db, int $priceId): bool
{
	if ($priceId <= 0) {
		return false;
	}
	epc_ssf_ensure_schema($db);
	$stmt = $db->prepare('SELECT IFNULL(`storefront_temp_disabled`, 0) FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
	$stmt->execute(array($priceId));
	$row = $stmt->fetchColumn();
	return $row !== false && (int) $row === 1;
}

function epc_ssf_storage_disabled_by_price(PDO $db, int $priceId): bool
{
	if ($priceId <= 0) {
		return false;
	}
	if (epc_ssf_is_price_disabled($db, $priceId)) {
		return true;
	}
	epc_ssf_ensure_schema($db);
	$stmt = $db->prepare(
		"SELECT `id` FROM `shop_storages`
		 WHERE `connection_options` LIKE CONCAT('%\"price_id\":', ?, '%')
		   AND IFNULL(`storefront_temp_disabled`, 0) = 1
		 LIMIT 1;"
	);
	$stmt->execute(array($priceId));
	return (bool) $stmt->fetchColumn();
}

/**
 * @return array<int, true> storage ids disabled for storefront
 */
function epc_ssf_disabled_storage_ids(PDO $db): array
{
	epc_ssf_ensure_schema($db);
	$out = array();
	$q = $db->query('SELECT `id` FROM `shop_storages` WHERE IFNULL(`storefront_temp_disabled`, 0) = 1;');
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$out[(int) $row['id']] = true;
	}
	return $out;
}

/**
 * @return array<int, true> price list ids disabled for storefront
 */
function epc_ssf_disabled_price_ids(PDO $db): array
{
	epc_ssf_ensure_schema($db);
	$out = array();
	$q = $db->query('SELECT `id` FROM `shop_docpart_prices` WHERE IFNULL(`storefront_temp_disabled`, 0) = 1;');
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$out[(int) $row['id']] = true;
	}
	return $out;
}

/**
 * Unified CP rows: warehouses + price lists.
 *
 * @return list<array<string, mixed>>
 */
function epc_ssf_cp_list_rows(PDO $db): array
{
	epc_ssf_ensure_schema($db);
	$rows = array();
	$priceIdsWithStorage = array();

	$storageSql =
		'SELECT s.`id`, s.`name`, s.`short_name`, s.`interface_type`, s.`connection_options`,
		        s.`storefront_temp_disabled`, s.`hidden`,
		        t.`handler_folder`, t.`name` AS `interface_name`
		 FROM `shop_storages` s
		 LEFT JOIN `shop_storages_interfaces_types` t ON t.`id` = s.`interface_type`
		 ORDER BY s.`id`;';

	$sq = $db->query($storageSql);
	while ($s = $sq->fetch(PDO::FETCH_ASSOC)) {
		$co = json_decode((string) ($s['connection_options'] ?? ''), true);
		$priceId = (is_array($co) && !empty($co['price_id'])) ? (int) $co['price_id'] : 0;
		if ($priceId > 0) {
			$priceIdsWithStorage[$priceId] = true;
		}
		$handler = (string) ($s['handler_folder'] ?? '');
		$typeLabel = ($handler === 'prices') ? 'Price list warehouse' : 'Warehouse';
		if ($handler === 'treelax_catalogue') {
			$typeLabel = 'Catalogue warehouse';
		} elseif ($handler !== '' && $handler !== 'prices') {
			$typeLabel = 'External supplier';
		}
		$rows[] = array(
			'entity_type' => 'storage',
			'entity_id' => (int) $s['id'],
			'name' => (string) ($s['name'] ?? ''),
			'short_name' => (string) ($s['short_name'] ?? ''),
			'type_label' => $typeLabel,
			'price_id' => $priceId,
			'storefront_disabled' => (int) ($s['storefront_temp_disabled'] ?? 0) === 1,
			'hidden' => (int) ($s['hidden'] ?? 0) === 1,
		);
	}

	$pq = $db->query(
		'SELECT p.`id`, p.`name`, IFNULL(p.`storefront_temp_disabled`, 0) AS `storefront_temp_disabled`,
		        (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `row_count`
		 FROM `shop_docpart_prices` p ORDER BY p.`id`;'
	);
	while ($p = $pq->fetch(PDO::FETCH_ASSOC)) {
		$pid = (int) ($p['id'] ?? 0);
		if (isset($priceIdsWithStorage[$pid])) {
			continue;
		}
		$rows[] = array(
			'entity_type' => 'price_list',
			'entity_id' => $pid,
			'name' => (string) ($p['name'] ?? ''),
			'short_name' => '',
			'type_label' => 'Price list (unlinked)',
			'price_id' => $pid,
			'storefront_disabled' => (int) ($p['storefront_temp_disabled'] ?? 0) === 1,
			'hidden' => false,
			'row_count' => (int) ($p['row_count'] ?? 0),
		);
	}

	return $rows;
}

function epc_ssf_sync_price_from_storage(PDO $db, int $storageId, int $disabled): void
{
	$stmt = $db->prepare('SELECT `connection_options` FROM `shop_storages` WHERE `id` = ? LIMIT 1;');
	$stmt->execute(array($storageId));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return;
	}
	$co = json_decode((string) ($row['connection_options'] ?? ''), true);
	if (!is_array($co) || empty($co['price_id'])) {
		return;
	}
	$priceId = (int) $co['price_id'];
	if ($priceId <= 0) {
		return;
	}
	$db->prepare('UPDATE `shop_docpart_prices` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1;')
		->execute(array($disabled, $priceId));
}

function epc_ssf_sync_storages_from_price(PDO $db, int $priceId, int $disabled): void
{
	$stmt = $db->prepare(
		"SELECT `id` FROM `shop_storages`
		 WHERE `connection_options` LIKE CONCAT('%\"price_id\":', ?, '%');"
	);
	$stmt->execute(array($priceId));
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$db->prepare('UPDATE `shop_storages` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1;')
			->execute(array($disabled, (int) $row['id']));
	}
}

function epc_ssf_write_audit(PDO $db, string $entityType, int $entityId, string $name, int $disabled, int $userId, string $userLabel): void
{
	epc_ssf_ensure_schema($db, true);
	$db->prepare(
		'INSERT INTO `epc_storefront_storage_toggle_audit`
		 (`entity_type`, `entity_id`, `entity_name`, `storefront_disabled`, `user_id`, `user_label`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, NOW());'
	)->execute(array($entityType, $entityId, $name, $disabled, $userId, $userLabel));
}

/**
 * @return array{ok: bool, message?: string, storefront_disabled?: int}
 */
function epc_ssf_set_toggle(PDO $db, string $entityType, int $entityId, int $disabled, int $userId = 0, string $userLabel = ''): array
{
	$entityType = ($entityType === 'price_list') ? 'price_list' : 'storage';
	$disabled = $disabled ? 1 : 0;
	if ($entityId <= 0) {
		return array('ok' => false, 'message' => 'Invalid entity id');
	}

	epc_ssf_ensure_schema($db, true);
	$name = '';

	if ($entityType === 'storage') {
		$stmt = $db->prepare('SELECT `name` FROM `shop_storages` WHERE `id` = ? LIMIT 1;');
		$stmt->execute(array($entityId));
		$name = (string) ($stmt->fetchColumn() ?: '');
		if ($name === '') {
			return array('ok' => false, 'message' => 'Storage not found');
		}
		$db->prepare('UPDATE `shop_storages` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1;')
			->execute(array($disabled, $entityId));
		epc_ssf_sync_price_from_storage($db, $entityId, $disabled);
	} else {
		$stmt = $db->prepare('SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
		$stmt->execute(array($entityId));
		$name = (string) ($stmt->fetchColumn() ?: '');
		if ($name === '') {
			return array('ok' => false, 'message' => 'Price list not found');
		}
		$db->prepare('UPDATE `shop_docpart_prices` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1;')
			->execute(array($disabled, $entityId));
		epc_ssf_sync_storages_from_price($db, $entityId, $disabled);
	}

	epc_ssf_write_audit($db, $entityType, $entityId, $name, $disabled, $userId, $userLabel);

	return array('ok' => true, 'storefront_disabled' => $disabled, 'entity_name' => $name);
}

/**
 * Remove storefront-disabled storages from part-search office_storage_bunches.
 *
 * @param list<array<string, mixed>> $bunches
 * @return list<array<string, mixed>>
 */
function epc_ssf_filter_office_storage_bunches(PDO $db, array $bunches): array
{
	$disabled = epc_ssf_disabled_storage_ids($db);
	if ($disabled === array()) {
		return $bunches;
	}
	$out = array();
	foreach ($bunches as $bunch) {
		if (!is_array($bunch)) {
			continue;
		}
		if ((int) ($bunch['protocol_version'] ?? 0) === 3 && !empty($bunch['office_storage_bunches']) && is_array($bunch['office_storage_bunches'])) {
			$nested = array();
			foreach ($bunch['office_storage_bunches'] as $nb) {
				if (!is_array($nb)) {
					continue;
				}
				$sid = (int) ($nb['storage_id'] ?? 0);
				if ($sid > 0 && isset($disabled[$sid])) {
					continue;
				}
				$nested[] = $nb;
			}
			if ($nested === array()) {
				continue;
			}
			$bunch['office_storage_bunches'] = $nested;
			$out[] = $bunch;
			continue;
		}
		if (($bunch['protocol_version'] ?? '') === 'server') {
			$out[] = $bunch;
			continue;
		}
		$sid = (int) ($bunch['storage_id'] ?? 0);
		if ($sid > 0 && isset($disabled[$sid])) {
			continue;
		}
		$out[] = $bunch;
	}
	return $out;
}
