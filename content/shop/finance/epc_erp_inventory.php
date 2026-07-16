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

	// Serial-number register — one row per serialized unit, tracked through its
	// lifecycle (in_stock -> sold/returned/scrapped) with the movement refs.
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_inv_serials` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`item_id` int(11) NOT NULL,
		`serial_no` varchar(128) NOT NULL,
		`warehouse_id` int(11) NOT NULL DEFAULT 0,
		`batch_no` varchar(64) DEFAULT NULL,
		`status` enum('in_stock','sold','returned','scrapped','in_transit') NOT NULL DEFAULT 'in_stock',
		`in_movement_id` int(11) NOT NULL DEFAULT 0,
		`out_movement_id` int(11) NOT NULL DEFAULT 0,
		`unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`note` varchar(255) DEFAULT NULL,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_item_serial` (`item_id`,`serial_no`),
		KEY `x_status` (`status`),
		KEY `x_wh` (`warehouse_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Serial-number register'");

	if (function_exists('epc_erp_schema_add_column_if_missing')) {
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'inv_receipt_posted', 'tinyint(1) NOT NULL DEFAULT 0');
		// Barcode (EAN/UPC/QR payload) on the item master for scan lookups, and
		// serial capture on movements for serialized goods.
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'barcode', "varchar(128) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_movements', 'serial_no', "varchar(128) DEFAULT NULL");
		// Per-tenant product-field classification: each product/inventory field
		// can be flagged as an inventory attribute (stock-tracked, part of the
		// item master & valuation) or a non-inventory attribute (descriptive /
		// catalogue only). Defaults are seeded from the industry pack at
		// onboarding; the client can re-classify any field afterwards.
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_field_defs', 'field_role', "enum('inventory','non_inventory') NOT NULL DEFAULT 'inventory'");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_field_defs', 'source_pack', "varchar(64) NOT NULL DEFAULT ''");

		// Item master — extended fields (released-product depth: identity, groups,
		// dimension/costing groups, units, tax, planning, physical dims, prices).
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'search_name', "varchar(255) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'product_type', "varchar(16) NOT NULL DEFAULT 'item'");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'item_group', "varchar(64) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'item_model_group', "varchar(64) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'costing_method', "varchar(24) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'storage_dim_group', "varchar(64) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'tracking_dim_group', "varchar(64) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'purchase_unit', "varchar(16) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'sales_unit', "varchar(16) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'default_warehouse_id', 'int(11) NOT NULL DEFAULT 0');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'default_vendor_id', 'int(11) NOT NULL DEFAULT 0');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'sales_tax_group', "varchar(64) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'purchase_tax_group', "varchar(64) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'buyer_group', "varchar(64) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'coverage_group', "varchar(64) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'abc_code', "varchar(8) NOT NULL DEFAULT ''");
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'net_weight', 'decimal(14,3) NOT NULL DEFAULT 0.000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'gross_weight', 'decimal(14,3) NOT NULL DEFAULT 0.000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'tare_weight', 'decimal(14,3) NOT NULL DEFAULT 0.000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'volume', 'decimal(14,3) NOT NULL DEFAULT 0.000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'gross_depth', 'decimal(14,3) NOT NULL DEFAULT 0.000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'gross_width', 'decimal(14,3) NOT NULL DEFAULT 0.000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'gross_height', 'decimal(14,3) NOT NULL DEFAULT 0.000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'standard_cost', 'decimal(14,4) NOT NULL DEFAULT 0.0000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'sales_price', 'decimal(14,4) NOT NULL DEFAULT 0.0000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'purchase_price', 'decimal(14,4) NOT NULL DEFAULT 0.0000');
		epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'notes', "varchar(1000) NOT NULL DEFAULT ''");
	}

	epc_erp_inventory_seed_field_defs($db);
}

/**
 * List every product/inventory field definition for this tenant.
 *
 * @return array<int,array<string,mixed>>
 */
function epc_erp_inv_field_defs_all(PDO $db)
{
	epc_erp_inventory_ensure_schema($db);
	return $db->query(
		'SELECT `id`,`field_key`,`label`,`field_type`,`options_json`,`sort_order`,`active`,`field_role`,`source_pack`
		 FROM `epc_erp_inv_field_defs` ORDER BY `field_role` DESC, `sort_order`, `id`'
	)->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/** Set a field's role (inventory | non_inventory). */
function epc_erp_inv_field_set_role(PDO $db, $fieldKey, $role)
{
	$role = ($role === 'non_inventory') ? 'non_inventory' : 'inventory';
	$db->prepare('UPDATE `epc_erp_inv_field_defs` SET `field_role` = ? WHERE `field_key` = ?')
		->execute(array($role, (string) $fieldKey));
}

/** Enable/disable a field. */
function epc_erp_inv_field_set_active(PDO $db, $fieldKey, $active)
{
	$db->prepare('UPDATE `epc_erp_inv_field_defs` SET `active` = ? WHERE `field_key` = ?')
		->execute(array($active ? 1 : 0, (string) $fieldKey));
}

/**
 * Insert or update a single field definition.
 *
 * @param array<string,mixed> $def field_key,label,field_type,options(array),field_role,sort_order
 */
function epc_erp_inv_field_upsert(PDO $db, array $def)
{
	epc_erp_inventory_ensure_schema($db);
	$key = substr(preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($def['field_key'] ?? ''))), 0, 32);
	if ($key === '') {
		return false;
	}
	$label = substr((string) ($def['label'] ?? $key), 0, 120);
	$type = in_array($def['field_type'] ?? 'text', array('text', 'number', 'date', 'select'), true) ? (string) $def['field_type'] : 'text';
	$role = (($def['field_role'] ?? 'inventory') === 'non_inventory') ? 'non_inventory' : 'inventory';
	$optionsJson = null;
	if ($type === 'select' && !empty($def['options']) && is_array($def['options'])) {
		$optionsJson = json_encode(array_values($def['options']));
	}
	$sort = isset($def['sort_order']) ? (int) $def['sort_order'] : 0;
	$pack = substr((string) ($def['source_pack'] ?? ''), 0, 64);
	// Preserve an admin-chosen role on an existing field: only set role on insert.
	$db->prepare(
		'INSERT INTO `epc_erp_inv_field_defs` (`field_key`,`label`,`field_type`,`options_json`,`sort_order`,`active`,`field_role`,`source_pack`)
		 VALUES (?,?,?,?,?,1,?,?)
		 ON DUPLICATE KEY UPDATE `label`=VALUES(`label`), `field_type`=VALUES(`field_type`),
			`options_json`=VALUES(`options_json`), `active`=1, `source_pack`=VALUES(`source_pack`)'
	)->execute(array($key, $label, $type, $optionsJson, $sort, $role, $pack));
	return true;
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
	$serial = trim((string) ($data['serial_no'] ?? ''));
	$hasSerialCol = epc_erp_inventory_has_column($db, 'epc_erp_inv_movements', 'serial_no');
	if ($hasSerialCol) {
		$db->prepare(
			'INSERT INTO `epc_erp_inv_movements`
			(`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`transfer_warehouse_id`,`purchase_id`,`order_id`,
			`batch_no`,`expiry_date`,`serial_no`,`reference`,`note`,`movement_date`,`admin_id`,`opening_batch_id`)
			VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array(
			$type, $wh, $itemId, $qtyAbs, $unitCost, $total,
			(int) ($data['transfer_warehouse_id'] ?? 0),
			(int) ($data['purchase_id'] ?? 0),
			(int) ($data['order_id'] ?? 0),
			$batch !== '' ? $batch : null,
			$expiry,
			$serial !== '' ? $serial : null,
			trim((string) ($data['reference'] ?? '')),
			trim((string) ($data['note'] ?? '')),
			$mdate ?: time(),
			epc_erp_admin_id(),
			(int) ($data['opening_batch_id'] ?? 0),
		));
	} else {
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
	}
	$movementId = (int) $db->lastInsertId();

	// Maintain the serial register for serialized goods.
	if ($serial !== '') {
		$incoming = in_array($type, array('opening', 'purchase_in', 'transfer_in', 'return_in'), true)
			|| ($type === 'adjustment' && $qty > 0);
		epc_erp_inventory_register_serial($db, array(
			'item_id' => $itemId,
			'serial_no' => $serial,
			'warehouse_id' => $wh,
			'batch_no' => $batch,
			'unit_cost' => $unitCost,
			'movement_id' => $movementId,
			'incoming' => $incoming,
		));
	}
	return $movementId;
}

/** Lightweight column-existence cache to keep movement inserts portable. */
function epc_erp_inventory_has_column(PDO $db, $table, $column)
{
	static $cache = array();
	$key = $table . '.' . $column;
	if (isset($cache[$key])) {
		return $cache[$key];
	}
	try {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM information_schema.COLUMNS
			 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?'
		);
		$st->execute(array($table, $column));
		$cache[$key] = ((int) $st->fetchColumn() > 0);
	} catch (Exception $e) {
		$cache[$key] = false;
	}
	return $cache[$key];
}

/** Upsert a serial record and flip its status on in/out movements. */
function epc_erp_inventory_register_serial(PDO $db, array $data)
{
	$itemId = (int) ($data['item_id'] ?? 0);
	$serial = trim((string) ($data['serial_no'] ?? ''));
	if ($itemId <= 0 || $serial === '') {
		return;
	}
	$incoming = !empty($data['incoming']);
	$now = time();
	if ($incoming) {
		$db->prepare(
			"INSERT INTO `epc_erp_inv_serials`
			 (`item_id`,`serial_no`,`warehouse_id`,`batch_no`,`status`,`in_movement_id`,`unit_cost`,`time_created`,`time_updated`)
			 VALUES (?,?,?,?, 'in_stock', ?, ?, ?, ?)
			 ON DUPLICATE KEY UPDATE `status`='in_stock', `warehouse_id`=VALUES(`warehouse_id`),
			   `batch_no`=VALUES(`batch_no`), `in_movement_id`=VALUES(`in_movement_id`),
			   `unit_cost`=VALUES(`unit_cost`), `time_updated`=VALUES(`time_updated`)"
		)->execute(array($itemId, $serial, (int) ($data['warehouse_id'] ?? 0),
			($data['batch_no'] ?? '') !== '' ? $data['batch_no'] : null,
			(int) ($data['movement_id'] ?? 0), (float) ($data['unit_cost'] ?? 0), $now, $now));
	} else {
		$db->prepare(
			"UPDATE `epc_erp_inv_serials`
			 SET `status`='sold', `out_movement_id`=?, `time_updated`=?
			 WHERE `item_id`=? AND `serial_no`=?"
		)->execute(array((int) ($data['movement_id'] ?? 0), $now, $itemId, $serial));
	}
}

/**
 * Stock ledger: movements for an item (and optional warehouse) with a running
 * on-hand balance, oldest first.
 *
 * @return array<int,array<string,mixed>>
 */
function epc_erp_inventory_ledger(PDO $db, $itemId = 0, $warehouseId = 0, $limit = 500)
{
	epc_erp_inventory_ensure_schema($db);
	$where = array('m.`active` = 1');
	$args = array();
	if ((int) $itemId > 0) { $where[] = 'm.`item_id` = ?'; $args[] = (int) $itemId; }
	if ((int) $warehouseId > 0) { $where[] = 'm.`warehouse_id` = ?'; $args[] = (int) $warehouseId; }
	$serialSel = epc_erp_inventory_has_column($db, 'epc_erp_inv_movements', 'serial_no') ? 'm.`serial_no`' : "'' AS serial_no";
	$sql = 'SELECT m.`id`, m.`movement_type`, m.`warehouse_id`, m.`item_id`, m.`qty`, m.`unit_cost`,
			m.`total_cost`, m.`batch_no`, ' . $serialSel . ', m.`reference`, m.`movement_date`,
			i.`sku`, i.`name` AS item_name, w.`name` AS warehouse_name
		 FROM `epc_erp_inv_movements` m
		 LEFT JOIN `epc_erp_inv_items` i ON i.`id` = m.`item_id`
		 LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = m.`warehouse_id`
		 WHERE ' . implode(' AND ', $where) . '
		 ORDER BY m.`movement_date` ASC, m.`id` ASC
		 LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($args);
	$rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$inTypes = array('opening', 'purchase_in', 'transfer_in', 'return_in');
	$balByKey = array();
	foreach ($rows as &$r) {
		$key = $r['item_id'] . ':' . $r['warehouse_id'];
		if (!isset($balByKey[$key])) { $balByKey[$key] = 0.0; }
		$signed = in_array($r['movement_type'], $inTypes, true) ? (float) $r['qty'] : -(float) $r['qty'];
		$balByKey[$key] += $signed;
		$r['signed_qty'] = $signed;
		$r['running_balance'] = round($balByKey[$key], 3);
	}
	unset($r);
	return array_reverse($rows);
}

/** Resolve an item by barcode (then SKU) for scan-based lookups. */
function epc_erp_inventory_find_by_barcode(PDO $db, $code)
{
	epc_erp_inventory_ensure_schema($db);
	$code = trim((string) $code);
	if ($code === '') {
		return null;
	}
	if (epc_erp_inventory_has_column($db, 'epc_erp_inv_items', 'barcode')) {
		$st = $db->prepare('SELECT * FROM `epc_erp_inv_items` WHERE `barcode` = ? AND `active` = 1 LIMIT 1');
		$st->execute(array($code));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) { return $row; }
	}
	$st = $db->prepare('SELECT * FROM `epc_erp_inv_items` WHERE `sku` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($code));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

/** Serial register listing (optionally filtered by item/status/search). */
function epc_erp_inventory_serials(PDO $db, $itemId = 0, $status = '', $search = '', $limit = 300)
{
	epc_erp_inventory_ensure_schema($db);
	$where = array('1=1');
	$args = array();
	if ((int) $itemId > 0) { $where[] = 's.`item_id` = ?'; $args[] = (int) $itemId; }
	if ($status !== '') { $where[] = 's.`status` = ?'; $args[] = $status; }
	if ($search !== '') { $where[] = 's.`serial_no` LIKE ?'; $args[] = '%' . $search . '%'; }
	$sql = 'SELECT s.*, i.`sku`, i.`name` AS item_name, w.`name` AS warehouse_name
		 FROM `epc_erp_inv_serials` s
		 LEFT JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id`
		 LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = s.`warehouse_id`
		 WHERE ' . implode(' AND ', $where) . '
		 ORDER BY s.`time_updated` DESC, s.`id` DESC
		 LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($args);
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
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
	$barcode = trim((string) ($data['barcode'] ?? ''));

	// Released-product (item master) extended fields — only persist the columns
	// that exist (schema is migrated lazily via ensure_schema).
	$cols = array('sku', 'name', 'product_id', 'item_type', 'track_expiry', 'unit', 'time_created');
	$vals = array(
		$sku, $name, (int) ($data['product_id'] ?? 0), $itemType, $trackExpiry,
		substr((string) ($data['unit'] ?? 'pcs'), 0, 16), time(),
	);
	$extendedStr = array(
		'barcode' => 128, 'search_name' => 255, 'product_type' => 16, 'item_group' => 64,
		'item_model_group' => 64, 'costing_method' => 24, 'storage_dim_group' => 64,
		'tracking_dim_group' => 64, 'purchase_unit' => 16, 'sales_unit' => 16,
		'sales_tax_group' => 64, 'purchase_tax_group' => 64, 'buyer_group' => 64,
		'coverage_group' => 64, 'abc_code' => 8, 'notes' => 1000,
	);
	foreach ($extendedStr as $col => $len) {
		if (isset($data[$col]) && epc_erp_inventory_has_column($db, 'epc_erp_inv_items', $col)) {
			$cols[] = $col;
			$vals[] = substr((string) $data[$col], 0, $len);
		}
	}
	$extendedInt = array('default_warehouse_id', 'default_vendor_id');
	foreach ($extendedInt as $col) {
		if (isset($data[$col]) && epc_erp_inventory_has_column($db, 'epc_erp_inv_items', $col)) {
			$cols[] = $col;
			$vals[] = (int) $data[$col];
		}
	}
	$extendedDec = array(
		'net_weight', 'gross_weight', 'tare_weight', 'volume', 'gross_depth',
		'gross_width', 'gross_height', 'standard_cost', 'sales_price', 'purchase_price',
		'reorder_level',
	);
	foreach ($extendedDec as $col) {
		if (isset($data[$col]) && $data[$col] !== '' && epc_erp_inventory_has_column($db, 'epc_erp_inv_items', $col)) {
			$cols[] = $col;
			$vals[] = (float) $data[$col];
		}
	}
	$placeholders = implode(',', array_fill(0, count($cols), '?'));
	$colList = '`' . implode('`,`', $cols) . '`';
	$db->prepare("INSERT INTO `epc_erp_inv_items` ($colList) VALUES ($placeholders)")->execute($vals);
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

/**
 * Reconstruct per-item on-hand quantity and weighted-average value AS OF a given
 * date by replaying the dated stock-movement ledger (epc_erp_inv_movements).
 * This is the ERP "as-at date" inventory snapshot — e.g. on-hand at a fiscal
 * year-end — independent of the current live snapshot. $asOf is a unix timestamp
 * (use end of the chosen day). Returns array keyed by item_id =>
 * array('qty', 'avg_cost', 'value'). Optional $warehouseId limits to one site.
 */
function epc_erp_inventory_on_hand_as_of(PDO $db, $asOf, $warehouseId = 0)
{
	epc_erp_inventory_ensure_schema($db);
	$asOf = (int) $asOf;
	$inTypes = array('opening', 'purchase_in', 'transfer_in', 'return_in');
	$outTypes = array('sale_out', 'transfer_out', 'return_out');
	$sql = "SELECT `item_id`, `movement_type`, `qty`, `unit_cost`, `movement_date`
		FROM `epc_erp_inv_movements`
		WHERE `active` = 1 AND `movement_date` <= ?";
	$params = array($asOf);
	if ((int) $warehouseId > 0) {
		$sql .= " AND `warehouse_id` = ?";
		$params[] = (int) $warehouseId;
	}
	$sql .= " ORDER BY `item_id` ASC, `movement_date` ASC, `id` ASC";
	$st = $db->prepare($sql);
	$st->execute($params);
	$acc = array();
	foreach ($st as $m) {
		$item = (int) $m['item_id'];
		if (!isset($acc[$item])) {
			$acc[$item] = array('qty' => 0.0, 'avg' => 0.0);
		}
		$qty = (float) $m['qty'];
		$cost = (float) $m['unit_cost'];
		$type = (string) $m['movement_type'];
		// Receipts (and adjustments — qty is stored as an absolute receipt) raise
		// stock and re-average the cost; issues lower stock at the running average.
		if (in_array($type, $inTypes, true) || $type === 'adjustment') {
			$newAvg = epc_erp_inventory_apply_weighted_average($acc[$item]['qty'], $acc[$item]['avg'], $qty, $cost);
			$acc[$item]['qty'] += $qty;
			$acc[$item]['avg'] = $newAvg;
		} elseif (in_array($type, $outTypes, true)) {
			$acc[$item]['qty'] -= $qty;
			if ($acc[$item]['qty'] < 0) {
				$acc[$item]['qty'] = 0.0;
			}
		}
	}
	$out = array();
	foreach ($acc as $item => $a) {
		$out[$item] = array(
			'qty' => round($a['qty'], 3),
			'avg_cost' => round($a['avg'], 4),
			'value' => round($a['qty'] * $a['avg'], 2),
		);
	}
	return $out;
}

function epc_erp_inventory_stock_report(PDO $db, $warehouseId = 0)
{
	$sql = 'SELECT s.*, i.sku, i.name, i.item_type, i.unit, w.name AS warehouse_name
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

/** Warehouse to draw a sale from: where the item holds most stock, else the first active warehouse. */
function epc_erp_inventory_pick_warehouse_for_item(PDO $db, int $itemId): int
{
	$st = $db->prepare('SELECT `warehouse_id` FROM `epc_erp_inv_stock` WHERE `item_id` = ? ORDER BY `qty_on_hand` DESC LIMIT 1');
	$st->execute(array($itemId));
	$wh = (int) $st->fetchColumn();
	if ($wh > 0) {
		return $wh;
	}
	$row = $db->query('SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `active` = 1 ORDER BY `id` LIMIT 1')->fetch(PDO::FETCH_ASSOC);
	return $row ? (int) $row['id'] : 0;
}

/**
 * Record real sale-out demand from a posted sales invoice so Order planning and
 * SCM forecast from actual billed sales (online customer/CP-portal orders and
 * manual sales alike). Each sold line writes a `sale_out` movement against the
 * matching ERP item; stock is deducted best-effort (clamped, never throws) so a
 * short-stock sale still registers as demand. Idempotent per invoice via the
 * `SALEINV-<docId>` reference, so re-posting never double-counts.
 *
 * @param array<int,array<string,mixed>> $docLines invoice lines (fallback when no order_id)
 * @return int number of demand movements recorded
 */
function epc_erp_inventory_record_sale_demand(PDO $db, int $docId, int $orderId = 0, array $docLines = array(), int $whenTs = 0): int
{
	if ($docId <= 0) {
		return 0;
	}
	epc_erp_inventory_ensure_schema($db);

	$ref = 'SALEINV-' . $docId;
	$chk = $db->prepare("SELECT COUNT(*) FROM `epc_erp_inv_movements` WHERE `movement_type` = 'sale_out' AND `reference` = ?");
	$chk->execute(array($ref));
	if ((int) $chk->fetchColumn() > 0) {
		return 0; // already recorded
	}
	if ($whenTs <= 0) {
		$whenTs = time();
	}

	// item_id => demanded qty
	$demand = array();
	if ($orderId > 0) {
		$st = $db->prepare('SELECT `product_id`, SUM(`count_need`) AS q FROM `shop_orders_items` WHERE `order_id` = ? AND `product_id` > 0 GROUP BY `product_id`');
		$st->execute(array($orderId));
		$lk = $db->prepare('SELECT `id` FROM `epc_erp_inv_items` WHERE `product_id` = ? AND `active` = 1 LIMIT 1');
		foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
			$qty = (float) $r['q'];
			if ($qty <= 0) {
				continue;
			}
			$lk->execute(array((int) $r['product_id']));
			$iid = (int) $lk->fetchColumn();
			if ($iid > 0) {
				$demand[$iid] = ($demand[$iid] ?? 0) + $qty;
			}
		}
	}
	if (empty($demand) && !empty($docLines)) {
		$byName = $db->prepare('SELECT * FROM `epc_erp_inv_items` WHERE `name` = ? AND `active` = 1 LIMIT 1');
		foreach ($docLines as $ln) {
			$qty = (float) ($ln['quantity'] ?? 0);
			if ($qty <= 0) {
				continue;
			}
			$item = null;
			$sku = trim((string) ($ln['item_sku'] ?? $ln['sku'] ?? ''));
			if ($sku !== '') {
				$item = epc_erp_inventory_get_item_by_sku($db, $sku);
			}
			if (!$item) {
				$nm = trim((string) ($ln['item_name'] ?? ''));
				if ($nm !== '') {
					$byName->execute(array($nm));
					$item = $byName->fetch(PDO::FETCH_ASSOC) ?: null;
				}
			}
			if ($item) {
				$iid = (int) $item['id'];
				$demand[$iid] = ($demand[$iid] ?? 0) + $qty;
			}
		}
	}
	if (empty($demand)) {
		return 0;
	}

	$ins = $db->prepare(
		"INSERT INTO `epc_erp_inv_movements`
		(`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`order_id`,`reference`,`note`,`movement_date`,`admin_id`,`active`)
		VALUES ('sale_out',?,?,?,?,?,?,?,?,?,?,1)"
	);
	$recorded = 0;
	foreach ($demand as $iid => $qty) {
		$wh = epc_erp_inventory_pick_warehouse_for_item($db, (int) $iid);
		if ($wh <= 0) {
			continue;
		}
		$row = epc_erp_inventory_get_stock_row($db, $wh, (int) $iid);
		$cost = $row ? (float) $row['avg_unit_cost'] : 0.0;
		if ($row) {
			$take = min((float) $row['qty_on_hand'], (float) $qty);
			if ($take > 0) {
				try {
					epc_erp_inventory_upsert_stock($db, $wh, (int) $iid, -$take, $cost);
				} catch (Exception $e) {
				}
			}
		}
		$ins->execute(array($wh, (int) $iid, (float) $qty, $cost, round((float) $qty * $cost, 2), $orderId, $ref, 'Sales invoice demand', $whenTs, epc_erp_admin_id()));
		$recorded++;
	}
	return $recorded;
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
		// Blockchain BOS: anchor GRN / goods receipt (best-effort).
		try {
			$bcFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
			if (is_file($bcFile)) {
				require_once $bcFile;
				$ref = epc_bc_bos_grn_record_id(array_merge($purchase, array('id' => $purchaseId)));
				epc_bc_bos_maybe_record_document(
					'grn',
					$ref,
					array(
						'purchase_id' => $purchaseId,
						'reference' => $ref,
						'warehouse_id' => $wh,
						'lines_posted' => $n,
						'invoice_number' => $invNo,
						'movement_type' => 'purchase_in',
					)
				);
			}
		} catch (Exception $e) {
			// best-effort
		}
	}
	return array('posted' => $n, 'warehouse_id' => $wh, 'reference' => $ref);
}
