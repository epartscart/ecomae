<?php
/**
 * ERP — multi-warehouse inventory, weighted average cost, expiry & custom fields.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_schema.php';
require_once __DIR__ . '/epc_erp_helpers.php';

function epc_erp_inventory_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_warehouses` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`storage_id` int(11) NOT NULL DEFAULT 0,
		`code` varchar(32) NOT NULL DEFAULT '',
		`name` varchar(255) NOT NULL,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_code` (`code`),
		KEY `x_storage` (`storage_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_field_defs` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`field_key` varchar(32) NOT NULL,
		`label` varchar(120) NOT NULL,
		`field_type` enum('text','number','date','select') NOT NULL DEFAULT 'text',
		`options_json` text,
		`sort_order` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_key` (`field_key`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_items` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`sku` varchar(64) NOT NULL,
		`name` varchar(255) NOT NULL,
		`product_id` int(11) NOT NULL DEFAULT 0,
		`item_type` enum('standard','perishable','serialized') NOT NULL DEFAULT 'standard',
		`track_expiry` tinyint(1) NOT NULL DEFAULT 0,
		`unit` varchar(16) NOT NULL DEFAULT 'pcs',
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_sku` (`sku`),
		KEY `x_product` (`product_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_item_fields` (
		`item_id` int(11) NOT NULL,
		`field_key` varchar(32) NOT NULL,
		`value` varchar(512) NOT NULL DEFAULT '',
		PRIMARY KEY (`item_id`,`field_key`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_stock` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`warehouse_id` int(11) NOT NULL,
		`item_id` int(11) NOT NULL,
		`qty_on_hand` decimal(14,3) NOT NULL DEFAULT 0.000,
		`avg_unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`expiry_date` date DEFAULT NULL,
		`batch_no` varchar(64) DEFAULT NULL,
		`variant_label` varchar(120) DEFAULT NULL,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_wh_item_batch` (`warehouse_id`,`item_id`,`batch_no`,`variant_label`),
		KEY `x_item` (`item_id`),
		KEY `x_expiry` (`expiry_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_movements` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`movement_type` enum('opening','purchase_in','sale_out','transfer_in','transfer_out','adjustment','return_in','return_out') NOT NULL,
		`warehouse_id` int(11) NOT NULL,
		`item_id` int(11) NOT NULL,
		`qty` decimal(14,3) NOT NULL DEFAULT 0.000,
		`unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`total_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
		`transfer_warehouse_id` int(11) NOT NULL DEFAULT 0,
		`purchase_id` int(11) NOT NULL DEFAULT 0,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`batch_no` varchar(64) DEFAULT NULL,
		`expiry_date` date DEFAULT NULL,
		`reference` varchar(128) DEFAULT NULL,
		`note` text,
		`movement_date` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`opening_batch_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_wh_item` (`warehouse_id`,`item_id`,`movement_date`),
		KEY `x_type` (`movement_type`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_closing` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`period_end` date NOT NULL,
		`warehouse_id` int(11) NOT NULL DEFAULT 0,
		`item_id` int(11) NOT NULL,
		`qty_closing` decimal(14,3) NOT NULL DEFAULT 0.000,
		`avg_unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`value_closing` decimal(14,2) NOT NULL DEFAULT 0.00,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_period` (`period_end`,`warehouse_id`,`item_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_purchase_inv_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`purchase_id` int(11) NOT NULL,
		`warehouse_id` int(11) NOT NULL,
		`item_id` int(11) NOT NULL,
		`qty` decimal(14,3) NOT NULL DEFAULT 0.000,
		`unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`batch_no` varchar(64) DEFAULT NULL,
		`expiry_date` date DEFAULT NULL,
		`movement_id` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_purchase` (`purchase_id`),
		KEY `x_item` (`item_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	if (function_exists('epc_erp_schema_add_column_if_missing')) {
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'inv_receipt_posted', 'tinyint(1) NOT NULL DEFAULT 0');
	}

	epc_erp_inventory_seed_field_defs($db);
}

function epc_erp_inventory_seed_field_defs(PDO $db)
{
	$cnt = (int) $db->query('SELECT COUNT(*) FROM `epc_erp_inv_field_defs`')->fetchColumn();
	if ($cnt > 0) {
		return;
	}
	$defs = array(
		array('custom_1', 'Custom field 1', 'text', 10),
		array('custom_2', 'Custom field 2', 'text', 20),
		array('custom_3', 'Custom field 3', 'number', 30),
		array('custom_4', 'Custom field 4', 'date', 40),
		array('custom_5', 'Custom field 5', 'text', 50),
	);
	$ins = $db->prepare('INSERT INTO `epc_erp_inv_field_defs` (`field_key`,`label`,`field_type`,`sort_order`) VALUES (?,?,?,?)');
	foreach ($defs as $d) {
		$ins->execute(array($d[0], $d[1], $d[2], $d[3]));
	}
}

function epc_erp_inventory_sync_warehouses(PDO $db)
{
	if (!$db->query("SHOW TABLES LIKE 'shop_storages'")->fetch()) {
		return 0;
	}
	$n = 0;
	$st = $db->query('SELECT `id`, `name` FROM `shop_storages` WHERE `hidden` = 0 OR `hidden` IS NULL');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$code = 'WH' . (int) $row['id'];
		$chk = $db->prepare('SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `storage_id` = ? OR `code` = ? LIMIT 1');
		$chk->execute(array((int) $row['id'], $code));
		if ($chk->fetch()) {
			continue;
		}
		$db->prepare(
			'INSERT INTO `epc_erp_inv_warehouses` (`storage_id`,`code`,`name`,`time_created`) VALUES (?,?,?,?)'
		)->execute(array((int) $row['id'], $code, (string) $row['name'], time()));
		$n++;
	}
	return $n;
}

function epc_erp_inventory_list_warehouses(PDO $db)
{
	return $db->query('SELECT * FROM `epc_erp_inv_warehouses` WHERE `active` = 1 ORDER BY `name`')->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_inventory_create_warehouse(PDO $db, array $data)
{
	$code = strtoupper(trim((string) ($data['code'] ?? '')));
	$name = trim((string) ($data['name'] ?? ''));
	if ($code === '' || $name === '') {
		throw new Exception('Warehouse code and name required');
	}
	$db->prepare(
		'INSERT INTO `epc_erp_inv_warehouses` (`storage_id`,`code`,`name`,`time_created`) VALUES (0,?,?,?)'
	)->execute(array($code, $name, time()));
	return (int) $db->lastInsertId();
}

function epc_erp_inventory_list_items(PDO $db, $limit = 500)
{
	$limit = max(1, min(2000, (int) $limit));
	return $db->query('SELECT * FROM `epc_erp_inv_items` WHERE `active` = 1 ORDER BY `sku` LIMIT ' . $limit)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_inventory_get_stock_row(PDO $db, $warehouseId, $itemId, $batchNo = '', $variant = '')
{
	$st = $db->prepare(
		'SELECT * FROM `epc_erp_inv_stock` WHERE `warehouse_id` = ? AND `item_id` = ? AND IFNULL(`batch_no`,\'\') = ? AND IFNULL(`variant_label`,\'\') = ? LIMIT 1'
	);
	$st->execute(array((int) $warehouseId, (int) $itemId, (string) $batchNo, (string) $variant));
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_erp_inventory_apply_weighted_average($oldQty, $oldCost, $inQty, $inCost)
{
	$oldQty = (float) $oldQty;
	$inQty = (float) $inQty;
	if ($inQty <= 0) {
		return (float) $oldCost;
	}
	if ($oldQty <= 0) {
		return (float) $inCost;
	}
	$totalValue = ($oldQty * $oldCost) + ($inQty * $inCost);
	$newQty = $oldQty + $inQty;
	return $newQty > 0 ? round($totalValue / $newQty, 4) : (float) $inCost;
}

function epc_erp_inventory_upsert_stock(PDO $db, $warehouseId, $itemId, $qtyDelta, $unitCost, $batchNo = '', $variant = '', $expiry = null)
{
	$row = epc_erp_inventory_get_stock_row($db, $warehouseId, $itemId, $batchNo, $variant);
	$now = time();
	if (!$row) {
		$avg = $qtyDelta > 0 ? (float) $unitCost : 0;
		$db->prepare(
			'INSERT INTO `epc_erp_inv_stock` (`warehouse_id`,`item_id`,`qty_on_hand`,`avg_unit_cost`,`batch_no`,`variant_label`,`expiry_date`,`time_updated`)
			VALUES (?,?,?,?,?,?,?,?)'
		)->execute(array(
			(int) $warehouseId, (int) $itemId, max(0, (float) $qtyDelta), $avg,
			$batchNo !== '' ? $batchNo : null,
			$variant !== '' ? $variant : null,
			$expiry ?: null,
			$now,
		));
		return;
	}
	$oldQty = (float) $row['qty_on_hand'];
	$newQty = $oldQty + (float) $qtyDelta;
	if ($newQty < 0) {
		throw new Exception('Insufficient stock (would go negative)');
	}
	$newAvg = (float) $row['avg_unit_cost'];
	if ($qtyDelta > 0) {
		$newAvg = epc_erp_inventory_apply_weighted_average($oldQty, $newAvg, $qtyDelta, $unitCost);
	}
	$db->prepare(
		'UPDATE `epc_erp_inv_stock` SET `qty_on_hand` = ?, `avg_unit_cost` = ?, `expiry_date` = COALESCE(?, `expiry_date`), `time_updated` = ? WHERE `id` = ?'
	)->execute(array($newQty, $newAvg, $expiry ?: null, $now, (int) $row['id']));
}

function epc_erp_inventory_record_movement(PDO $db, array $data)
{
	$type = (string) ($data['movement_type'] ?? 'adjustment');
	$wh = (int) ($data['warehouse_id'] ?? 0);
	$itemId = (int) ($data['item_id'] ?? 0);
	$qty = (float) ($data['qty'] ?? 0);
	$unitCost = (float) ($data['unit_cost'] ?? 0);
	if ($wh <= 0 || $itemId <= 0 || $qty == 0.0) {
		throw new Exception('Warehouse, item and quantity required');
	}
	$batch = trim((string) ($data['batch_no'] ?? ''));
	$variant = trim((string) ($data['variant_label'] ?? ''));
	$expiry = !empty($data['expiry_date']) ? (string) $data['expiry_date'] : null;

	$inTypes = array('opening', 'purchase_in', 'transfer_in', 'return_in', 'adjustment');
	$qtyAbs = abs($qty);

	if (($type === 'adjustment' && $qty > 0) || (in_array($type, $inTypes, true) && $qty > 0 && $type !== 'adjustment')) {
		epc_erp_inventory_upsert_stock($db, $wh, $itemId, $qtyAbs, $unitCost, $batch, $variant, $expiry);
	} elseif ($type === 'adjustment' && $qty < 0) {
		$row = epc_erp_inventory_get_stock_row($db, $wh, $itemId, $batch, $variant);
		if (!$row || (float) $row['qty_on_hand'] < $qtyAbs) {
			throw new Exception('Insufficient quantity on hand');
		}
		$costOut = (float) $row['avg_unit_cost'];
		epc_erp_inventory_upsert_stock($db, $wh, $itemId, -$qtyAbs, $costOut, $batch, $variant, $expiry);
		$unitCost = $costOut;
	} else {
		$row = epc_erp_inventory_get_stock_row($db, $wh, $itemId, $batch, $variant);
		if (!$row || (float) $row['qty_on_hand'] < $qtyAbs) {
			throw new Exception('Insufficient quantity on hand');
		}
		$costOut = (float) $row['avg_unit_cost'];
		epc_erp_inventory_upsert_stock($db, $wh, $itemId, -$qtyAbs, $costOut, $batch, $variant, $expiry);
		$unitCost = $costOut;
	}

	$total = round($qtyAbs * $unitCost, 2);
	$mdate = !empty($data['movement_date']) ? strtotime((string) $data['movement_date'] . ' 12:00:00') : time();
	$db->prepare(
		'INSERT INTO `epc_erp_inv_movements`
		(`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`transfer_warehouse_id`,`purchase_id`,`order_id`,
		`batch_no`,`expiry_date`,`reference`,`note`,`movement_date`,`admin_id`,`opening_batch_id`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$type, $wh, $itemId, $qtyAbs, $unitCost, $total,
		(int) ($data['transfer_warehouse_id'] ?? 0),
		(int) ($data['purchase_id'] ?? 0),
		(int) ($data['order_id'] ?? 0),
		$batch !== '' ? $batch : null,
		$expiry,
		trim((string) ($data['reference'] ?? '')),
		trim((string) ($data['note'] ?? '')),
		$mdate ?: time(),
		epc_erp_admin_id(),
		(int) ($data['opening_batch_id'] ?? 0),
	));
	return (int) $db->lastInsertId();
}

function epc_erp_inventory_create_item(PDO $db, array $data)
{
	$sku = trim((string) ($data['sku'] ?? ''));
	$name = trim((string) ($data['name'] ?? ''));
	if ($sku === '' || $name === '') {
		throw new Exception('SKU and name required');
	}
	$itemType = in_array((string) ($data['item_type'] ?? ''), array('perishable', 'serialized'), true)
		? (string) $data['item_type'] : 'standard';
	$trackExpiry = !empty($data['track_expiry']) || $itemType === 'perishable' ? 1 : 0;
	$db->prepare(
		'INSERT INTO `epc_erp_inv_items` (`sku`,`name`,`product_id`,`item_type`,`track_expiry`,`unit`,`time_created`) VALUES (?,?,?,?,?,?,?)'
	)->execute(array(
		$sku, $name, (int) ($data['product_id'] ?? 0), $itemType, $trackExpiry,
		substr((string) ($data['unit'] ?? 'pcs'), 0, 16),
		time(),
	));
	$id = (int) $db->lastInsertId();
	foreach ((array) ($data['custom_fields'] ?? array()) as $key => $val) {
		$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $key));
		if ($key === '') {
			continue;
		}
		$db->prepare('INSERT INTO `epc_erp_inv_item_fields` (`item_id`,`field_key`,`value`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')
			->execute(array($id, $key, substr((string) $val, 0, 512)));
	}
	return $id;
}

/** Resolve ERP inventory item id by SKU; optionally create from order/catalog data. */
function epc_erp_inventory_resolve_item_id(PDO $db, string $sku, array $opts = array()): int
{
	$sku = trim($sku);
	if ($sku === '') {
		return 0;
	}
	$item = epc_erp_inventory_get_item_by_sku($db, $sku);
	if ($item) {
		return (int) $item['id'];
	}
	if (empty($opts['create_if_missing'])) {
		return 0;
	}
	$name = trim((string) ($opts['name'] ?? ''));
	if ($name === '') {
		$name = $sku;
	}
	return epc_erp_inventory_create_item($db, array(
		'sku' => $sku,
		'name' => $name,
		'product_id' => (int) ($opts['product_id'] ?? 0),
	));
}

function epc_erp_inventory_stock_report(PDO $db, $warehouseId = 0)
{
	$sql = 'SELECT s.*, i.sku, i.name, i.item_type, w.name AS warehouse_name
		FROM `epc_erp_inv_stock` s
		INNER JOIN `epc_erp_inv_items` i ON i.id = s.item_id
		INNER JOIN `epc_erp_inv_warehouses` w ON w.id = s.warehouse_id
		WHERE i.active = 1';
	$params = array();
	if ($warehouseId > 0) {
		$sql .= ' AND s.warehouse_id = ?';
		$params[] = (int) $warehouseId;
	}
	$sql .= ' ORDER BY w.name, i.sku, s.batch_no';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_inventory_low_stock_lines(PDO $db, $warehouseId = 0)
{
	require_once __DIR__ . '/epc_erp_phase8.php';
	epc_erp_phase8_ensure_schema($db);
	$sql = 'SELECT s.*, i.sku, i.name, i.reorder_level, w.name AS warehouse_name
		FROM `epc_erp_inv_stock` s
		INNER JOIN `epc_erp_inv_items` i ON i.id = s.item_id AND i.active = 1
		INNER JOIN `epc_erp_inv_warehouses` w ON w.id = s.warehouse_id
		WHERE i.reorder_level > 0 AND s.qty_on_hand <= i.reorder_level';
	$params = array();
	if ($warehouseId > 0) {
		$sql .= ' AND s.warehouse_id = ?';
		$params[] = (int)$warehouseId;
	}
	$sql .= ' ORDER BY (s.qty_on_hand / NULLIF(i.reorder_level, 0)) ASC, i.sku LIMIT 200';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_inventory_set_reorder_level(PDO $db, $itemId, $level)
{
	require_once __DIR__ . '/epc_erp_phase8.php';
	epc_erp_phase8_ensure_schema($db);
	$db->prepare('UPDATE `epc_erp_inv_items` SET `reorder_level` = ? WHERE `id` = ? AND `active` = 1')
		->execute(array(max(0, (float)$level), (int)$itemId));
}

function epc_erp_inventory_run_closing(PDO $db, $periodEnd, $warehouseId = 0)
{
	$periodEnd = date('Y-m-d', strtotime((string) $periodEnd));
	$rows = epc_erp_inventory_stock_report($db, $warehouseId);
	$n = 0;
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_inv_closing` (`period_end`,`warehouse_id`,`item_id`,`qty_closing`,`avg_unit_cost`,`value_closing`,`time_created`)
		VALUES (?,?,?,?,?,?,?)
		ON DUPLICATE KEY UPDATE `qty_closing` = VALUES(`qty_closing`), `avg_unit_cost` = VALUES(`avg_unit_cost`), `value_closing` = VALUES(`value_closing`)'
	);
	foreach ($rows as $r) {
		$val = round((float) $r['qty_on_hand'] * (float) $r['avg_unit_cost'], 2);
		$ins->execute(array(
			$periodEnd, (int) $r['warehouse_id'], (int) $r['item_id'],
			(float) $r['qty_on_hand'], (float) $r['avg_unit_cost'], $val, time(),
		));
		$n++;
	}
	return $n;
}

function epc_erp_inventory_valuation_total(PDO $db, $warehouseId = 0)
{
	$sql = 'SELECT SUM(`qty_on_hand` * `avg_unit_cost`) FROM `epc_erp_inv_stock` s INNER JOIN `epc_erp_inv_items` i ON i.id = s.item_id WHERE i.active = 1';
	$params = array();
	if ($warehouseId > 0) {
		$sql .= ' AND s.warehouse_id = ?';
		$params[] = (int) $warehouseId;
	}
	$st = $db->prepare($sql);
	$st->execute($params);
	return round((float) $st->fetchColumn(), 2);
}

function epc_erp_inventory_warehouse_by_storage(PDO $db, $storageId)
{
	$storageId = (int) $storageId;
	if ($storageId <= 0) {
		return 0;
	}
	$st = $db->prepare('SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `storage_id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($storageId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ? (int) $row['id'] : 0;
}

function epc_erp_inventory_warehouse_resolve(PDO $db, $warehouseId, $warehouseCode = '')
{
	$warehouseId = (int) $warehouseId;
	if ($warehouseId > 0) {
		return $warehouseId;
	}
	$warehouseCode = strtoupper(trim((string) $warehouseCode));
	if ($warehouseCode === '') {
		return 0;
	}
	$st = $db->prepare('SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `code` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($warehouseCode));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ? (int) $row['id'] : 0;
}

function epc_erp_inventory_get_item_by_sku(PDO $db, $sku)
{
	$sku = trim((string) $sku);
	if ($sku === '') {
		return null;
	}
	$st = $db->prepare('SELECT * FROM `epc_erp_inv_items` WHERE `sku` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($sku));
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

/**
 * Paired warehouse transfer at weighted-average cost from source.
 */
function epc_erp_inventory_transfer(PDO $db, array $data)
{
	$from = (int) ($data['from_warehouse_id'] ?? 0);
	$to = (int) ($data['to_warehouse_id'] ?? 0);
	$itemId = (int) ($data['item_id'] ?? 0);
	$qty = (float) ($data['qty'] ?? 0);
	if ($from <= 0 || $to <= 0 || $from === $to) {
		throw new Exception('Source and destination warehouses required (must differ)');
	}
	if ($itemId <= 0 || $qty <= 0) {
		throw new Exception('Item and positive quantity required');
	}
	$batch = trim((string) ($data['batch_no'] ?? ''));
	$variant = trim((string) ($data['variant_label'] ?? ''));
	$ref = trim((string) ($data['reference'] ?? ''));
	if ($ref === '') {
		$ref = 'TRF-' . date('Ymd-His');
	}
	$row = epc_erp_inventory_get_stock_row($db, $from, $itemId, $batch, $variant);
	if (!$row || (float) $row['qty_on_hand'] < $qty) {
		throw new Exception('Insufficient stock at source warehouse');
	}
	$unitCost = (float) $row['avg_unit_cost'];
	$note = trim((string) ($data['note'] ?? ''));
	$outId = epc_erp_inventory_record_movement($db, array(
		'movement_type' => 'transfer_out',
		'warehouse_id' => $from,
		'item_id' => $itemId,
		'qty' => $qty,
		'unit_cost' => $unitCost,
		'transfer_warehouse_id' => $to,
		'batch_no' => $batch,
		'variant_label' => $variant,
		'reference' => $ref,
		'note' => $note,
	));
	$inId = epc_erp_inventory_record_movement($db, array(
		'movement_type' => 'transfer_in',
		'warehouse_id' => $to,
		'item_id' => $itemId,
		'qty' => $qty,
		'unit_cost' => $unitCost,
		'transfer_warehouse_id' => $from,
		'batch_no' => $batch,
		'variant_label' => $variant,
		'reference' => $ref,
		'note' => $note,
	));
	return array('transfer_out_id' => $outId, 'transfer_in_id' => $inId, 'unit_cost' => $unitCost, 'reference' => $ref);
}

/**
 * Parse inventory receipt lines from purchase POST or nested arrays.
 */
function epc_erp_inventory_parse_receipt_lines(array $data)
{
	$lines = array();
	if (!empty($data['inventory_lines']) && is_string($data['inventory_lines'])) {
		$decoded = json_decode($data['inventory_lines'], true);
		if (is_array($decoded)) {
			$data['inventory_lines'] = $decoded;
		}
	}
	if (!empty($data['inventory_lines']) && is_array($data['inventory_lines'])) {
		foreach ($data['inventory_lines'] as $line) {
			if (!is_array($line)) {
				continue;
			}
			$lines[] = $line;
		}
	}
	if (!empty($data['inventory_csv']) && is_string($data['inventory_csv'])) {
		$parsed = epc_erp_inventory_parse_csv_text($data['inventory_csv'], (int) ($data['warehouse_id'] ?? 0));
		foreach ($parsed['rows'] as $row) {
			$lines[] = $row;
		}
	}
	return $lines;
}

/**
 * Bulk stock movements from CSV (sku, qty, unit_cost, batch, expiry, movement_type).
 */
function epc_erp_inventory_import_csv(PDO $db, $csvText, $defaultWarehouseId = 0, $defaultMovementType = 'purchase_in')
{
	$parsed = epc_erp_inventory_parse_csv_text($csvText, $defaultWarehouseId, $defaultMovementType);
	$posted = 0;
	$errors = array();
	foreach ($parsed['rows'] as $i => $row) {
		$lineNo = $i + 2;
		try {
			$wh = epc_erp_inventory_warehouse_resolve($db, (int) ($row['warehouse_id'] ?? 0), (string) ($row['warehouse_code'] ?? ''));
			if ($wh <= 0) {
				throw new Exception('Warehouse required');
			}
			$itemId = (int) ($row['item_id'] ?? 0);
			if ($itemId <= 0) {
				$item = epc_erp_inventory_get_item_by_sku($db, (string) ($row['sku'] ?? ''));
				if (!$item) {
					throw new Exception('Unknown SKU: ' . ($row['sku'] ?? ''));
				}
				$itemId = (int) $item['id'];
			}
			$qty = (float) ($row['qty'] ?? 0);
			if ($qty == 0.0) {
				throw new Exception('Qty is zero');
			}
			$type = (string) ($row['movement_type'] ?? $defaultMovementType);
			if ($type === 'transfer') {
				$toWh = epc_erp_inventory_warehouse_resolve($db, 0, (string) ($row['to_warehouse_code'] ?? ''));
				if ($toWh <= 0) {
					$toWh = (int) ($row['to_warehouse_id'] ?? 0);
				}
				if ($toWh <= 0) {
					throw new Exception('transfer requires to_warehouse_code or to_warehouse_id');
				}
				epc_erp_inventory_transfer($db, array(
					'from_warehouse_id' => $wh,
					'to_warehouse_id' => $toWh,
					'item_id' => $itemId,
					'qty' => abs($qty),
					'batch_no' => (string) ($row['batch_no'] ?? ''),
					'reference' => (string) ($row['reference'] ?? 'CSV-TRF'),
				));
			} else {
				$qtySigned = $qty;
				if (in_array($type, array('opening', 'purchase_in', 'transfer_in', 'return_in'), true) && $qtySigned < 0) {
					// CSV convenience: negative in-bound quantity means stock correction out.
					$type = 'adjustment';
				} elseif (in_array($type, array('sale_out', 'transfer_out', 'return_out'), true) && $qtySigned < 0) {
					$qtySigned = abs($qtySigned);
				}
				epc_erp_inventory_record_movement($db, array(
					'movement_type' => $type,
					'warehouse_id' => $wh,
					'item_id' => $itemId,
					'qty' => $qtySigned,
					'unit_cost' => (float) ($row['unit_cost'] ?? 0),
					'batch_no' => (string) ($row['batch_no'] ?? ''),
					'expiry_date' => (string) ($row['expiry_date'] ?? ''),
					'reference' => (string) ($row['reference'] ?? 'CSV'),
				));
			}
			$posted++;
		} catch (Exception $e) {
			$errors[] = 'Line ' . $lineNo . ': ' . $e->getMessage();
		}
	}
	return array('posted' => $posted, 'errors' => $errors, 'skipped' => count($errors));
}

function epc_erp_inventory_parse_csv_text($csvText, $defaultWarehouseId = 0, $defaultMovementType = 'purchase_in')
{
	$csvText = str_replace(array("\r\n", "\r"), "\n", (string) $csvText);
	$csvText = trim($csvText);
	$rows = array();
	if ($csvText === '') {
		return array('rows' => $rows);
	}
	$lines = explode("\n", $csvText);
	$header = null;
	$map = array();
	foreach ($lines as $line) {
		$line = trim($line);
		if ($line === '' || strpos($line, '#') === 0) {
			continue;
		}
		$cols = str_getcsv($line);
		if ($header === null) {
			$header = array();
			foreach ($cols as $c) {
				$key = strtolower(preg_replace('/[^a-z0-9_]/', '_', trim((string) $c)));
				$header[] = $key;
			}
			continue;
		}
		$row = array('warehouse_id' => (int) $defaultWarehouseId, 'movement_type' => $defaultMovementType);
		foreach ($header as $idx => $key) {
			if ($key === '') {
				continue;
			}
			$row[$key] = isset($cols[$idx]) ? trim((string) $cols[$idx]) : '';
		}
		if (!empty($row['sku']) || !empty($row['item_id'])) {
			$rows[] = $row;
		}
	}
	return array('rows' => $rows);
}

function epc_erp_inventory_csv_template()
{
	return "sku,qty,unit_cost,batch_no,expiry_date,movement_type,warehouse_code,to_warehouse_code,reference\n"
		. "SKU-001,10,25.50,BATCH-A,2026-12-31,purchase_in,WH1,,\n"
		. "SKU-002,5,0,,,transfer,WH1,WH2,TRF-001\n"
		. "SKU-001,-2,0,,,adjustment,WH1,,ADJ-NEG\n"
		. "SKU-001,-3,0,,,purchase_in,WH1,,NEG-AS-ADJ\n";
}

/**
 * Post purchase_in movements when a supplier invoice is saved (idempotent per purchase).
 */
function epc_erp_inventory_receive_purchase(PDO $db, $purchaseId, array $data = array())
{
	$purchaseId = (int) $purchaseId;
	if ($purchaseId <= 0) {
		return array('posted' => 0, 'skipped' => 'invalid purchase');
	}
	epc_erp_inventory_ensure_schema($db);
	$pst = $db->prepare('SELECT * FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$pst->execute(array($purchaseId));
	$purchase = $pst->fetch(PDO::FETCH_ASSOC);
	if (!$purchase) {
		throw new Exception('Purchase not found');
	}
	if (!empty($purchase['inv_receipt_posted'])) {
		return array('posted' => 0, 'skipped' => 'already posted');
	}
	$lines = epc_erp_inventory_parse_receipt_lines($data);
	if ($lines === array()) {
		return array('posted' => 0, 'skipped' => 'no inventory lines');
	}
	$wh = (int) ($data['warehouse_id'] ?? 0);
	if ($wh <= 0) {
		$wh = epc_erp_inventory_warehouse_by_storage($db, (int) ($purchase['storage_id'] ?? 0));
	}
	if ($wh <= 0 && !empty($data['warehouse_code'])) {
		$wh = epc_erp_inventory_warehouse_resolve($db, 0, (string) $data['warehouse_code']);
	}
	if ($wh <= 0) {
		throw new Exception('Warehouse required for inventory receipt (select warehouse or link purchase storage to ERP warehouse)');
	}
	$invNo = trim((string) ($purchase['invoice_number'] ?? ''));
	$ref = $invNo !== '' ? ('PINV-' . $invNo) : ('PINV-' . $purchaseId);
	$n = 0;
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_purchase_inv_lines`
		(`purchase_id`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`batch_no`,`expiry_date`,`movement_id`)
		VALUES (?,?,?,?,?,?,?,?)'
	);
	foreach ($lines as $line) {
		$itemId = (int) ($line['item_id'] ?? 0);
		if ($itemId <= 0) {
			$itemId = epc_erp_inventory_resolve_item_id($db, (string) ($line['sku'] ?? ''), array(
				'create_if_missing' => !empty($data['create_items_if_missing']),
				'name' => (string) ($line['name'] ?? ''),
			));
			if ($itemId <= 0) {
				throw new Exception('Unknown SKU: ' . ($line['sku'] ?? ''));
			}
		}
		$qty = (float) ($line['qty'] ?? 0);
		if ($qty <= 0) {
			continue;
		}
		$unitCost = (float) ($line['unit_cost'] ?? 0);
		if ($unitCost <= 0 && (float) $purchase['amount_ex_vat'] > 0) {
			$unitCost = round((float) $purchase['amount_ex_vat'] / $qty, 4);
		}
		$lineWh = epc_erp_inventory_warehouse_resolve($db, (int) ($line['warehouse_id'] ?? 0), (string) ($line['warehouse_code'] ?? ''));
		$useWh = $lineWh > 0 ? $lineWh : $wh;
		$mid = epc_erp_inventory_record_movement($db, array(
			'movement_type' => 'purchase_in',
			'warehouse_id' => $useWh,
			'item_id' => $itemId,
			'qty' => $qty,
			'unit_cost' => $unitCost,
			'batch_no' => (string) ($line['batch_no'] ?? ''),
			'expiry_date' => (string) ($line['expiry_date'] ?? ''),
			'purchase_id' => $purchaseId,
			'reference' => (string) ($line['reference'] ?? $ref),
			'movement_date' => !empty($purchase['purchase_date']) ? date('Y-m-d', (int) $purchase['purchase_date']) : date('Y-m-d'),
		));
		$ins->execute(array(
			$purchaseId, $useWh, $itemId, $qty, $unitCost,
			($line['batch_no'] ?? '') !== '' ? $line['batch_no'] : null,
			!empty($line['expiry_date']) ? $line['expiry_date'] : null,
			$mid,
		));
		$n++;
	}
	if ($n > 0) {
		$db->prepare('UPDATE `epc_erp_purchases` SET `inv_receipt_posted` = 1 WHERE `id` = ?')->execute(array($purchaseId));
	}
	return array('posted' => $n, 'warehouse_id' => $wh, 'reference' => $ref);
}
