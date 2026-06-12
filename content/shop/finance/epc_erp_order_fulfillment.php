<?php
/**
 * Commerce order → ERP sales order + per-supplier purchase orders.
 * Cost on purchase invoice (PO received); revenue on sales invoice (order complete).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';
require_once __DIR__ . '/epc_erp_vouchers.php';
require_once __DIR__ . '/epc_erp_extended.php';

function epc_erp_order_fulfillment_ensure_schema(PDO $db): void
{
	epc_erp_vouchers_ensure_schema($db);
	epc_erp_extended_ensure_schema($db);

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_orders', 'shop_order_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_orders', 'fulfillment_status', "varchar(16) NOT NULL DEFAULT 'open'");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'shop_order_item_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'supplier_id', 'int(11) NOT NULL DEFAULT 0');

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_po_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`po_id` int(11) NOT NULL,
		`shop_order_item_id` int(11) NOT NULL DEFAULT 0,
		`supplier_id` int(11) NOT NULL DEFAULT 0,
		`storage_id` int(11) NOT NULL DEFAULT 0,
		`line_no` int(11) NOT NULL DEFAULT 1,
		`description` varchar(255) NOT NULL DEFAULT '',
		`qty` decimal(14,3) NOT NULL DEFAULT 0.000,
		`unit_cost_ex_vat` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`line_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`qty_received` decimal(14,3) NOT NULL DEFAULT 0.000,
		`qty_cancelled` decimal(14,3) NOT NULL DEFAULT 0.000,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_po` (`po_id`),
		KEY `x_order_item` (`shop_order_item_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='PO lines linked to shop order items';");

	try {
		$db->exec('ALTER TABLE `epc_erp_sales_orders` ADD KEY `x_shop_order` (`shop_order_id`)');
	} catch (Exception $e) {
	}
}

/**
 * @return array{supplier_id:int,supplier_name:string,storage_id:int}
 */
function epc_erp_order_fulfillment_resolve_line_supplier(PDO $db, array $item): array
{
	$storageId = (int) ($item['t2_storage_id'] ?? 0);
	$supplierId = 0;
	$supplierName = '';

	$apaiFile = __DIR__ . '/../price_engine/epc_apai_fulfillment.php';
	if (is_file($apaiFile)) {
		require_once $apaiFile;
		if (function_exists('epc_apai_decode_order_item_meta')) {
			$meta = epc_apai_decode_order_item_meta((string) ($item['t2_json_params'] ?? ''));
			$supplierId = (int) ($meta['apai_supplier_id'] ?? 0);
			$supplierName = trim((string) ($meta['apai_supplier_name'] ?? $meta['apai_fulfillment_source'] ?? ''));
		}
	}

	if ($supplierId <= 0 && $storageId > 0) {
		$st = $db->prepare('SELECT `id`, `name` FROM `epc_erp_suppliers` WHERE `active` = 1 AND `storage_id` = ? LIMIT 1');
		$st->execute(array($storageId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			$supplierId = (int) $row['id'];
			$supplierName = (string) $row['name'];
		}
	}

	if ($supplierId <= 0 && $storageId > 0) {
		$st = $db->prepare('SELECT `name` FROM `shop_storages` WHERE `id` = ? LIMIT 1');
		$st->execute(array($storageId));
		$storageName = trim((string) $st->fetchColumn());
		if ($storageName !== '') {
			$supplierName = $storageName;
			$supplierId = epc_erp_create_supplier($db, array(
				'name' => $storageName,
				'storage_id' => $storageId,
				'country_code' => 'AE',
			));
		}
	}

	if ($supplierName === '' && $supplierId > 0) {
		$st = $db->prepare('SELECT `name` FROM `epc_erp_suppliers` WHERE `id` = ? LIMIT 1');
		$st->execute(array($supplierId));
		$supplierName = trim((string) $st->fetchColumn());
	}
	if ($supplierName === '') {
		$supplierName = 'Unassigned supplier';
	}

	return array(
		'supplier_id' => $supplierId,
		'supplier_name' => $supplierName,
		'storage_id' => $storageId,
	);
}

function epc_erp_order_fulfillment_load_order(PDO $db, int $orderId): ?array
{
	$st = $db->prepare('SELECT * FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1');
	$st->execute(array($orderId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_erp_order_fulfillment_load_items(PDO $db, int $orderId): array
{
	$st = $db->prepare('SELECT * FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`');
	$st->execute(array($orderId));
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_erp_order_fulfillment_item_received_qty(PDO $db, int $orderItemId): float
{
	$st = $db->prepare(
		'SELECT IFNULL(SUM(`count_reserved` + `count_issued`), 0) FROM `shop_orders_items_details` WHERE `order_item_id` = ?'
	);
	$st->execute(array($orderItemId));
	return (float) $st->fetchColumn();
}

function epc_erp_order_fulfillment_find_sales_order(PDO $db, int $orderId): ?array
{
	epc_erp_order_fulfillment_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_sales_orders` WHERE `shop_order_id` = ? ORDER BY `id` DESC LIMIT 1');
	$st->execute(array($orderId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_erp_order_fulfillment_create_sales_order(PDO $db, int $orderId, array $order, array $items): int
{
	$existing = epc_erp_order_fulfillment_find_sales_order($db, $orderId);
	if ($existing) {
		return (int) $existing['id'];
	}

	$customerId = (int) ($order['user_id'] ?? 0);
	if ($customerId <= 0) {
		throw new Exception('Guest orders need a customer user_id before ERP sales order bootstrap');
	}

	$lines = array();
	foreach ($items as $item) {
		$qty = max(0.0001, (float) ($item['count_need'] ?? 0));
		$unit = round((float) ($item['price'] ?? 0), 4);
		$net = round($qty * $unit, 2);
		if ($net <= 0) {
			continue;
		}
		$sup = epc_erp_order_fulfillment_resolve_line_supplier($db, $item);
		$desc = trim((string) ($item['t2_name'] ?? ''));
		if ($desc === '') {
			$desc = trim((string) ($item['t2_article'] ?? 'Line'));
		}
		$lines[] = array(
			'description' => $desc,
			'qty' => $qty,
			'unit_price_ex_vat' => $unit,
			'line_ex_vat' => $net,
			'shop_order_item_id' => (int) ($item['id'] ?? 0),
			'supplier_id' => (int) $sup['supplier_id'],
		);
	}

	$amountEx = 0.0;
	foreach ($lines as $ln) {
		$amountEx += (float) $ln['line_ex_vat'];
	}
	$amountEx = round($amountEx, 2);
	if ($amountEx <= 0) {
		throw new Exception('Order has no billable lines for ERP sales order');
	}

	require_once __DIR__ . '/epc_tax_toolkit.php';
	$taxCalc = epc_tax_toolkit_calc_amounts($db, $amountEx, $customerId, 0);
	$now = time();
	$soNo = epc_erp_next_voucher_no($db, 'SO');
	$db->prepare(
		'INSERT INTO `epc_erp_sales_orders`
		(`so_no`, `shop_order_id`, `customer_user_id`, `contact_id`, `title`, `amount_ex_vat`, `vat_amount`, `total_amount`,
		 `status`, `fulfillment_status`, `notes`, `admin_id`, `time_created`, `time_updated`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$soNo,
		$orderId,
		$customerId,
		0,
		'Shop order #' . $orderId,
		$amountEx,
		$taxCalc['vat_amount'],
		$taxCalc['total_amount'],
		'confirmed',
		'open',
		'Auto-linked from commerce order #' . $orderId,
		epc_erp_admin_id(),
		$now,
		$now,
	));
	$soId = (int) $db->lastInsertId();

	$ins = $db->prepare(
		'INSERT INTO `epc_erp_sales_order_lines`
		(`sales_order_id`, `shop_order_item_id`, `supplier_id`, `line_no`, `description`, `qty`, `unit_price_ex_vat`, `line_ex_vat`)
		VALUES (?,?,?,?,?,?,?,?)'
	);
	$lineNo = 1;
	foreach ($lines as $ln) {
		$ins->execute(array(
			$soId,
			(int) ($ln['shop_order_item_id'] ?? 0),
			(int) ($ln['supplier_id'] ?? 0),
			$lineNo++,
			mb_substr((string) $ln['description'], 0, 255),
			(float) $ln['qty'],
			(float) $ln['unit_price_ex_vat'],
			(float) $ln['line_ex_vat'],
		));
	}

	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'so_from_shop_order', 'sales_order', $soId, 'Sales order from shop order', array(
		'shop_order_id' => $orderId,
		'so_no' => $soNo,
	));

	return $soId;
}

function epc_erp_order_fulfillment_create_po_for_supplier(
	PDO $db,
	int $orderId,
	int $supplierId,
	string $supplierName,
	array $lines
): int {
	$chk = $db->prepare(
		'SELECT p.`id` FROM `epc_erp_purchase_orders` p
		 INNER JOIN `epc_erp_po_lines` pl ON pl.`po_id` = p.`id`
		 WHERE p.`order_id` = ? AND p.`supplier_id` = ? AND p.`status` NOT IN (\'cancelled\', \'received\')
		 LIMIT 1'
	);
	$chk->execute(array($orderId, $supplierId));
	$existingId = (int) $chk->fetchColumn();
	if ($existingId > 0) {
		return $existingId;
	}

	$amountEx = 0.0;
	foreach ($lines as $ln) {
		$amountEx += (float) ($ln['line_ex_vat'] ?? 0);
	}
	$amountEx = round($amountEx, 2);
	if ($amountEx <= 0 || $supplierId <= 0) {
		return 0;
	}

	require_once __DIR__ . '/epc_tax_toolkit.php';
	$taxCalc = epc_tax_toolkit_purchase_amounts($db, $amountEx, $supplierId);
	$now = time();
	$poNo = epc_erp_next_voucher_no($db, 'PO');
	$title = 'PO for order #' . $orderId . ' — ' . $supplierName;
	$db->prepare(
		'INSERT INTO `epc_erp_purchase_orders`
		(`po_no`, `voucher_no`, `supplier_id`, `title`, `amount_ex_vat`, `vat_amount`, `total_amount`, `status`, `order_id`, `notes`, `admin_id`, `time_created`, `time_updated`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$poNo,
		$poNo,
		$supplierId,
		$title,
		$amountEx,
		$taxCalc['vat_amount'],
		$taxCalc['total_amount'],
		'draft',
		$orderId,
		'Customer order ref #' . $orderId,
		epc_erp_admin_id(),
		$now,
		$now,
	));
	$poId = (int) $db->lastInsertId();

	$ins = $db->prepare(
		'INSERT INTO `epc_erp_po_lines`
		(`po_id`, `shop_order_item_id`, `supplier_id`, `storage_id`, `line_no`, `description`, `qty`, `unit_cost_ex_vat`, `line_ex_vat`, `time_updated`)
		VALUES (?,?,?,?,?,?,?,?,?,?)'
	);
	$lineNo = 1;
	foreach ($lines as $ln) {
		$ins->execute(array(
			$poId,
			(int) ($ln['shop_order_item_id'] ?? 0),
			$supplierId,
			(int) ($ln['storage_id'] ?? 0),
			$lineNo++,
			mb_substr((string) ($ln['description'] ?? 'Line'), 0, 255),
			(float) ($ln['qty'] ?? 0),
			(float) ($ln['unit_cost_ex_vat'] ?? 0),
			(float) ($ln['line_ex_vat'] ?? 0),
			$now,
		));
	}

	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'po_from_shop_order', 'purchase_order', $poId, 'Draft PO from shop order', array(
		'shop_order_id' => $orderId,
		'po_no' => $poNo,
		'supplier_id' => $supplierId,
	));

	return $poId;
}

/**
 * Append lines to an existing open PO (same supplier / order).
 *
 * @param array<int, array<string, mixed>> $lines
 */
function epc_erp_order_fulfillment_append_po_lines(PDO $db, int $poId, array $lines): int
{
	if ($poId <= 0 || empty($lines)) {
		return 0;
	}
	$st = $db->prepare('SELECT * FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($poId));
	$po = $st->fetch(PDO::FETCH_ASSOC);
	if (!$po || $po['status'] === 'cancelled' || (int) ($po['purchase_id'] ?? 0) > 0) {
		throw new Exception('Cannot append lines to this purchase order');
	}
	$maxSt = $db->prepare('SELECT IFNULL(MAX(`line_no`), 0) FROM `epc_erp_po_lines` WHERE `po_id` = ?');
	$maxSt->execute(array($poId));
	$lineNo = (int) $maxSt->fetchColumn();
	$amountAdd = 0.0;
	$now = time();
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_po_lines`
		(`po_id`, `shop_order_item_id`, `supplier_id`, `storage_id`, `line_no`, `description`, `qty`, `unit_cost_ex_vat`, `line_ex_vat`, `time_updated`)
		VALUES (?,?,?,?,?,?,?,?,?,?)'
	);
	foreach ($lines as $ln) {
		$lineEx = round((float) ($ln['line_ex_vat'] ?? 0), 2);
		if ($lineEx <= 0) {
			continue;
		}
		$ins->execute(array(
			$poId,
			(int) ($ln['shop_order_item_id'] ?? 0),
			(int) ($po['supplier_id'] ?? 0),
			(int) ($ln['storage_id'] ?? 0),
			++$lineNo,
			mb_substr((string) ($ln['description'] ?? 'Line'), 0, 255),
			(float) ($ln['qty'] ?? 0),
			(float) ($ln['unit_cost_ex_vat'] ?? 0),
			$lineEx,
			$now,
		));
		$amountAdd += $lineEx;
	}
	if ($amountAdd > 0) {
		require_once __DIR__ . '/epc_tax_toolkit.php';
		$newEx = round((float) $po['amount_ex_vat'] + $amountAdd, 2);
		$taxCalc = epc_tax_toolkit_purchase_amounts($db, $newEx, (int) $po['supplier_id']);
		$db->prepare(
			'UPDATE `epc_erp_purchase_orders` SET `amount_ex_vat` = ?, `vat_amount` = ?, `total_amount` = ?, `time_updated` = ? WHERE `id` = ?'
		)->execute(array($newEx, $taxCalc['vat_amount'], $taxCalc['total_amount'], $now, $poId));
	}
	return $poId;
}

/**
 * Reassign an open PO line to another supplier/storage (supplier swap mid-fulfillment).
 *
 * @return array{order_item_id:int,old_po_id:int,new_po_id:int,new_supplier_id:int,cancelled_qty:float}
 */
function epc_erp_order_fulfillment_swap_line_supplier(PDO $db, int $orderId, int $orderItemId, int $newStorageId): array
{
	epc_erp_order_fulfillment_ensure_schema($db);
	$orderId = (int) $orderId;
	$orderItemId = (int) $orderItemId;
	$newStorageId = (int) $newStorageId;
	if ($orderId <= 0 || $orderItemId <= 0 || $newStorageId <= 0) {
		throw new Exception('Order, line, and new storage are required');
	}

	$itemSt = $db->prepare('SELECT * FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1');
	$itemSt->execute(array($orderItemId, $orderId));
	$item = $itemSt->fetch(PDO::FETCH_ASSOC);
	if (!$item) {
		throw new Exception('Order line not found');
	}

	$plSt = $db->prepare(
		'SELECT pl.*, po.`id` AS po_id, po.`status`, po.`purchase_id`, po.`supplier_id`, po.`po_no`
		 FROM `epc_erp_po_lines` pl
		 INNER JOIN `epc_erp_purchase_orders` po ON po.`id` = pl.`po_id`
		 WHERE pl.`shop_order_item_id` = ? AND po.`order_id` = ?
		   AND po.`status` != \'cancelled\' AND po.`purchase_id` = 0
		 ORDER BY pl.`id` DESC LIMIT 1'
	);
	$plSt->execute(array($orderItemId, $orderId));
	$poLine = $plSt->fetch(PDO::FETCH_ASSOC);
	if (!$poLine) {
		throw new Exception('No open PO line found for this order item');
	}

	$qty = (float) ($poLine['qty'] ?? 0);
	$received = epc_erp_order_fulfillment_item_received_qty($db, $orderItemId);
	$remaining = round(max(0.0, $qty - $received), 3);
	if ($remaining <= 0) {
		throw new Exception('Nothing left to reassign on this line');
	}

	$now = time();
	$db->prepare(
		'UPDATE `epc_erp_po_lines` SET `qty_cancelled` = ?, `time_updated` = ? WHERE `id` = ?'
	)->execute(array($remaining, $now, (int) $poLine['id']));

	$oldPoId = (int) $poLine['po_id'];
	$openSt = $db->prepare(
		'SELECT COUNT(*) FROM `epc_erp_po_lines` WHERE `po_id` = ?
		 AND (`qty` - `qty_received` - `qty_cancelled`) > 0.001'
	);
	$openSt->execute(array($oldPoId));
	if ((int) $openSt->fetchColumn() === 0) {
		epc_erp_po_set_status($db, $oldPoId, 'cancelled');
	}

	$item['t2_storage_id'] = $newStorageId;
	$sup = epc_erp_order_fulfillment_resolve_line_supplier($db, $item);
	$newSupplierId = (int) $sup['supplier_id'];
	if ($newSupplierId <= 0) {
		throw new Exception('Could not resolve supplier for storage #' . $newStorageId);
	}
	if ($newSupplierId === (int) $poLine['supplier_id']) {
		throw new Exception('Line is already assigned to this supplier');
	}

	$unitCost = round((float) ($item['t2_price_purchase'] ?? 0), 4);
	$desc = trim((string) ($item['t2_name'] ?? ''));
	if ($desc === '') {
		$desc = trim((string) ($item['t2_article'] ?? 'Line'));
	}
	$newLine = array(
		'shop_order_item_id' => $orderItemId,
		'storage_id' => $newStorageId,
		'description' => $desc,
		'qty' => $remaining,
		'unit_cost_ex_vat' => $unitCost,
		'line_ex_vat' => round($remaining * $unitCost, 2),
	);

	$chk = $db->prepare(
		'SELECT p.`id` FROM `epc_erp_purchase_orders` p
		 INNER JOIN `epc_erp_po_lines` pl ON pl.`po_id` = p.`id`
		 WHERE p.`order_id` = ? AND p.`supplier_id` = ? AND p.`status` NOT IN (\'cancelled\', \'received\') AND p.`purchase_id` = 0
		 LIMIT 1'
	);
	$chk->execute(array($orderId, $newSupplierId));
	$existingPoId = (int) $chk->fetchColumn();
	if ($existingPoId > 0) {
		epc_erp_order_fulfillment_append_po_lines($db, $existingPoId, array($newLine));
		$newPoId = $existingPoId;
	} else {
		$newPoId = epc_erp_order_fulfillment_create_po_for_supplier(
			$db,
			$orderId,
			$newSupplierId,
			(string) $sup['supplier_name'],
			array($newLine)
		);
	}

	$so = epc_erp_order_fulfillment_find_sales_order($db, $orderId);
	if ($so) {
		$db->prepare('UPDATE `epc_erp_sales_order_lines` SET `supplier_id` = ? WHERE `sales_order_id` = ? AND `shop_order_item_id` = ?')
			->execute(array($newSupplierId, (int) $so['id'], $orderItemId));
	}

	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'po_supplier_swap', 'purchase_order', $newPoId, 'Supplier swap on order line', array(
		'shop_order_id' => $orderId,
		'order_item_id' => $orderItemId,
		'old_po_id' => $oldPoId,
		'new_storage_id' => $newStorageId,
		'cancelled_qty' => $remaining,
	));

	return array(
		'order_item_id' => $orderItemId,
		'old_po_id' => $oldPoId,
		'new_po_id' => $newPoId,
		'new_supplier_id' => $newSupplierId,
		'cancelled_qty' => $remaining,
	);
}

/**
 * Bootstrap ERP sales order + draft POs (one per supplier) for a commerce order.
 *
 * @return array{shop_order_id:int,sales_order_id:int,po_ids:array<int>,created:bool}
 */
function epc_erp_order_fulfillment_bootstrap(PDO $db, int $orderId): array
{
	epc_erp_order_fulfillment_ensure_schema($db);
	$orderId = (int) $orderId;
	if ($orderId <= 0) {
		throw new Exception('Order ID required');
	}
	$order = epc_erp_order_fulfillment_load_order($db, $orderId);
	if (!$order) {
		throw new Exception('Shop order not found');
	}
	$items = epc_erp_order_fulfillment_load_items($db, $orderId);
	if (empty($items)) {
		throw new Exception('Shop order has no line items');
	}

	$soId = epc_erp_order_fulfillment_create_sales_order($db, $orderId, $order, $items);

	$bySupplier = array();
	foreach ($items as $item) {
		$sup = epc_erp_order_fulfillment_resolve_line_supplier($db, $item);
		$sid = (int) $sup['supplier_id'];
		if ($sid <= 0) {
			continue;
		}
		$qty = max(0.0001, (float) ($item['count_need'] ?? 0));
		$unitCost = round((float) ($item['t2_price_purchase'] ?? 0), 4);
		$lineEx = round($qty * $unitCost, 2);
		$desc = trim((string) ($item['t2_name'] ?? ''));
		if ($desc === '') {
			$desc = trim((string) ($item['t2_article'] ?? 'Line'));
		}
		if (!isset($bySupplier[$sid])) {
			$bySupplier[$sid] = array(
				'supplier_name' => $sup['supplier_name'],
				'lines' => array(),
			);
		}
		$bySupplier[$sid]['lines'][] = array(
			'shop_order_item_id' => (int) $item['id'],
			'storage_id' => (int) $sup['storage_id'],
			'description' => $desc,
			'qty' => $qty,
			'unit_cost_ex_vat' => $unitCost,
			'line_ex_vat' => $lineEx,
		);
	}

	$poIds = array();
	foreach ($bySupplier as $supplierId => $bundle) {
		$poId = epc_erp_order_fulfillment_create_po_for_supplier(
			$db,
			$orderId,
			(int) $supplierId,
			(string) $bundle['supplier_name'],
			$bundle['lines']
		);
		if ($poId > 0) {
			$poIds[] = $poId;
		}
	}

	epc_erp_order_fulfillment_pf_sync($db, $orderId);

	return array(
		'shop_order_id' => $orderId,
		'sales_order_id' => $soId,
		'po_ids' => $poIds,
		'created' => true,
	);
}

/** Mirror the order into the Process flow "Customer Order → Delivery" case (best-effort). */
function epc_erp_order_fulfillment_pf_sync(PDO $db, int $orderId): void
{
	try {
		$pfFile = __DIR__ . '/epc_erp_processflow.php';
		if ($orderId > 0 && is_file($pfFile)) {
			require_once $pfFile;
			if (function_exists('epc_pf_sync_order_case')) {
				epc_pf_sync_order_case($db, $orderId);
			}
		}
	} catch (Exception $e) {
		// never block fulfilment
	}
}

function epc_erp_order_fulfillment_sync_received_qty(PDO $db, int $orderId): void
{
	epc_erp_order_fulfillment_ensure_schema($db);
	$st = $db->prepare(
		'SELECT pl.* FROM `epc_erp_po_lines` pl
		 INNER JOIN `epc_erp_purchase_orders` po ON po.`id` = pl.`po_id`
		 WHERE po.`order_id` = ?'
	);
	$st->execute(array($orderId));
	$now = time();
	$upd = $db->prepare(
		'UPDATE `epc_erp_po_lines` SET `qty_received` = ?, `qty_cancelled` = ?, `time_updated` = ? WHERE `id` = ?'
	);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$itemId = (int) ($row['shop_order_item_id'] ?? 0);
		if ($itemId <= 0) {
			continue;
		}
		$received = epc_erp_order_fulfillment_item_received_qty($db, $itemId);
		$qty = (float) ($row['qty'] ?? 0);
		$cancelled = max(0.0, $qty - $received);
		$upd->execute(array($received, $cancelled, $now, (int) $row['id']));
	}
}

function epc_erp_order_fulfillment_sync_po_statuses(PDO $db, int $orderId): array
{
	epc_erp_order_fulfillment_sync_received_qty($db, $orderId);
	$st = $db->prepare('SELECT * FROM `epc_erp_purchase_orders` WHERE `order_id` = ? AND `status` NOT IN (\'cancelled\', \'received\')');
	$st->execute(array($orderId));
	$updated = array();
	$now = time();
	while ($po = $st->fetch(PDO::FETCH_ASSOC)) {
		$poId = (int) $po['id'];
		$agg = $db->prepare(
			'SELECT IFNULL(SUM(`qty`),0) AS qty_total, IFNULL(SUM(`qty_received`),0) AS qty_recv
			 FROM `epc_erp_po_lines` WHERE `po_id` = ?'
		);
		$agg->execute(array($poId));
		$a = $agg->fetch(PDO::FETCH_ASSOC) ?: array();
		$qtyTotal = (float) ($a['qty_total'] ?? 0);
		$qtyRecv = (float) ($a['qty_recv'] ?? 0);
		$openQty = 0.0;
		$openSt = $db->prepare(
			'SELECT IFNULL(SUM(GREATEST(0, `qty` - `qty_received` - `qty_cancelled`)), 0)
			 FROM `epc_erp_po_lines` WHERE `po_id` = ?'
		);
		$openSt->execute(array($poId));
		$openQty = (float) $openSt->fetchColumn();

		$newStatus = (string) $po['status'];
		if ($qtyTotal > 0 && $openQty <= 0.001 && $qtyRecv > 0) {
			$newStatus = 'received';
		} elseif ($qtyRecv > 0 && $openQty > 0.001) {
			$newStatus = 'partial';
		} elseif ($qtyRecv > 0 && $openQty <= 0.001) {
			$newStatus = 'received';
		} elseif ($newStatus === 'draft') {
			$newStatus = 'approved';
		}
		if ($newStatus !== $po['status']) {
			epc_erp_po_set_status($db, $poId, $newStatus);
			$updated[] = array('po_id' => $poId, 'status' => $newStatus);
		}
	}
	return $updated;
}

function epc_erp_order_fulfillment_sync_sales_status(PDO $db, int $orderId): string
{
	$so = epc_erp_order_fulfillment_find_sales_order($db, $orderId);
	if (!$so) {
		return 'none';
	}
	$soId = (int) $so['id'];
	if ((string) ($so['status'] ?? '') === 'invoiced') {
		return 'invoiced';
	}

	$poSt = $db->prepare(
		'SELECT COUNT(*) AS total,
			SUM(CASE WHEN `status` IN (\'received\') OR `purchase_id` > 0 THEN 1 ELSE 0 END) AS done
		 FROM `epc_erp_purchase_orders` WHERE `order_id` = ? AND `status` != \'cancelled\''
	);
	$poSt->execute(array($orderId));
	$poAgg = $poSt->fetch(PDO::FETCH_ASSOC) ?: array();
	$poTotal = (int) ($poAgg['total'] ?? 0);
	$poDone = (int) ($poAgg['done'] ?? 0);

	$orderComplete = epc_erp_order_is_complete($db, $orderId);
	$fulfillment = 'open';
	if ($poTotal > 0 && $poDone > 0 && $poDone < $poTotal) {
		$fulfillment = 'partial';
	} elseif ($poTotal > 0 && $poDone >= $poTotal) {
		$fulfillment = 'fulfilled';
	}
	if ($orderComplete) {
		$fulfillment = 'fulfilled';
	}

	$db->prepare('UPDATE `epc_erp_sales_orders` SET `fulfillment_status` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($fulfillment, time(), $soId));

	return $fulfillment;
}

function epc_erp_order_fulfillment_status(PDO $db, int $orderId): array
{
	epc_erp_order_fulfillment_ensure_schema($db);
	epc_erp_order_fulfillment_sync_po_statuses($db, $orderId);
	$fulfillment = epc_erp_order_fulfillment_sync_sales_status($db, $orderId);
	epc_erp_order_fulfillment_pf_sync($db, $orderId);

	$so = epc_erp_order_fulfillment_find_sales_order($db, $orderId);
	$poSt = $db->prepare(
		'SELECT po.*, s.`name` AS supplier_name,
			(SELECT COUNT(*) FROM `epc_erp_po_lines` WHERE `po_id` = po.`id`) AS line_count
		 FROM `epc_erp_purchase_orders` po
		 LEFT JOIN `epc_erp_suppliers` s ON s.`id` = po.`supplier_id`
		 WHERE po.`order_id` = ?
		 ORDER BY po.`id`'
	);
	$poSt->execute(array($orderId));
	$pos = $poSt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$purchaseInvoices = array();
	$piSt = $db->prepare(
		'SELECT `id`, `invoice_number`, `supplier_id`, `total_amount`, `status`, `gl_journal_id`, `po_id`
		 FROM `epc_erp_purchases` WHERE `active` = 1 AND `order_id` = ? ORDER BY `id`'
	);
	$piSt->execute(array($orderId));
	$purchaseInvoices = $piSt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$salesInvoice = null;
	if ($so && (int) ($so['sales_invoice_id'] ?? 0) > 0) {
		$invSt = $db->prepare('SELECT `id`, `invoice_number`, `validation_ok`, `order_id` FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1');
		$invSt->execute(array((int) $so['sales_invoice_id']));
		$salesInvoice = $invSt->fetch(PDO::FETCH_ASSOC) ?: null;
	} elseif ($orderId > 0) {
		$invSt = $db->prepare(
			'SELECT `id`, `invoice_number`, `validation_ok`, `order_id` FROM `epc_einvoice_documents`
			 WHERE `order_id` = ? ORDER BY `id` DESC LIMIT 1'
		);
		$invSt->execute(array($orderId));
		$salesInvoice = $invSt->fetch(PDO::FETCH_ASSOC) ?: null;
	}

	return array(
		'shop_order_id' => $orderId,
		'order_complete' => epc_erp_order_is_complete($db, $orderId),
		'fulfillment_status' => $fulfillment,
		'sales_order' => $so,
		'purchase_orders' => $pos,
		'purchase_invoices' => $purchaseInvoices,
		'sales_invoice' => $salesInvoice,
		'accounting' => array(
			'cost_posted' => count(array_filter($purchaseInvoices, function ($p) {
				return (int) ($p['gl_journal_id'] ?? 0) > 0;
			})) > 0,
			'revenue_posted' => $salesInvoice !== null,
		),
	);
}

/**
 * Post purchase invoice for a received PO (cost recognition — may run before order complete).
 */
function epc_erp_order_fulfillment_post_po_invoice(PDO $db, int $poId): array
{
	epc_erp_order_fulfillment_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($poId));
	$po = $st->fetch(PDO::FETCH_ASSOC);
	if (!$po) {
		throw new Exception('Purchase order not found');
	}
	if ((int) ($po['purchase_id'] ?? 0) > 0) {
		$purchaseId = (int) $po['purchase_id'];
		$piSt = $db->prepare('SELECT `invoice_number`, `gl_journal_id` FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1');
		$piSt->execute(array($purchaseId));
		$pi = $piSt->fetch(PDO::FETCH_ASSOC) ?: array();
		return array(
			'po_id' => $poId,
			'purchase_id' => $purchaseId,
			'voucher_no' => (string) ($pi['invoice_number'] ?? ''),
			'shop_order_id' => (int) ($po['order_id'] ?? 0),
			'already_posted' => true,
		);
	}
	if (!in_array($po['status'], array('approved', 'partial', 'received'), true)) {
		epc_erp_order_fulfillment_sync_po_statuses($db, (int) $po['order_id']);
		$st->execute(array($poId));
		$po = $st->fetch(PDO::FETCH_ASSOC);
	}
	if ($po['status'] === 'approved') {
		epc_erp_order_fulfillment_sync_received_qty($db, (int) $po['order_id']);
		$openSt = $db->prepare(
			'SELECT IFNULL(SUM(GREATEST(0, `qty` - `qty_received` - `qty_cancelled`)), 0)
			 FROM `epc_erp_po_lines` WHERE `po_id` = ?'
		);
		$openSt->execute(array($poId));
		if ((float) $openSt->fetchColumn() <= 0.001) {
			epc_erp_po_set_status($db, $poId, 'received');
			$db->prepare('UPDATE `epc_erp_purchase_orders` SET `received_at` = ?, `time_updated` = ? WHERE `id` = ?')
				->execute(array(time(), time(), $poId));
			$st->execute(array($poId));
			$po = $st->fetch(PDO::FETCH_ASSOC);
		}
	}
	if (!in_array($po['status'], array('partial', 'received'), true)) {
		throw new Exception('PO must be partial or received before purchase invoice posting');
	}

	$lineSt = $db->prepare('SELECT * FROM `epc_erp_po_lines` WHERE `po_id` = ? ORDER BY `line_no`');
	$lineSt->execute(array($poId));
	$lines = $lineSt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$amountEx = 0.0;
	foreach ($lines as $ln) {
		$recv = (float) ($ln['qty_received'] ?? 0);
		$qty = $recv > 0 ? $recv : (float) ($ln['qty'] ?? 0);
		$unit = (float) ($ln['unit_cost_ex_vat'] ?? 0);
		$amountEx += round($qty * $unit, 2);
	}
	$amountEx = round($amountEx, 2);
	if ($amountEx <= 0) {
		$amountEx = round((float) $po['amount_ex_vat'], 2);
	}

	$piNo = epc_erp_next_voucher_no($db, 'PI');
	$orderId = (int) ($po['order_id'] ?? 0);
	$purchaseId = epc_erp_create_purchase($db, array(
		'supplier_id' => (int) $po['supplier_id'],
		'order_id' => $orderId,
		'invoice_number' => $piNo,
		'amount_ex_vat' => $amountEx,
		'note' => 'From PO ' . $po['po_no'] . ' (order #' . $orderId . ')',
		'status' => 'confirmed',
		'allow_open_order' => 1,
	));

	$db->prepare('UPDATE `epc_erp_purchases` SET `voucher_no` = ?, `po_id` = ? WHERE `id` = ?')
		->execute(array($piNo, $poId, $purchaseId));
	$db->prepare(
		'UPDATE `epc_erp_purchase_orders` SET `purchase_id` = ?, `voucher_no` = ?, `status` = \'received\', `received_at` = ?, `time_updated` = ? WHERE `id` = ?'
	)->execute(array($purchaseId, (string) ($po['voucher_no'] ?: $po['po_no']), time(), time(), $poId));

	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'po_to_purchase_order_flow', 'purchase_order', $poId, 'PO posted to purchase invoice (open order OK)', array(
		'purchase_id' => $purchaseId,
		'shop_order_id' => $orderId,
	));

	if ($orderId > 0) {
		epc_erp_order_fulfillment_sync_sales_status($db, $orderId);
	}

	return array(
		'po_id' => $poId,
		'purchase_id' => $purchaseId,
		'voucher_no' => $piNo,
		'shop_order_id' => $orderId,
	);
}

/**
 * Post sales invoice when shop order is complete (revenue recognition).
 */
function epc_erp_order_fulfillment_post_sales_invoice(PDO $db, int $orderId, int $adminId = 0): array
{
	epc_erp_order_fulfillment_ensure_schema($db);
	epc_erp_assert_order_complete($db, $orderId, 'Sales invoice from order');

	$so = epc_erp_order_fulfillment_find_sales_order($db, $orderId);
	if ($so && (int) ($so['sales_invoice_id'] ?? 0) > 0) {
		return array(
			'shop_order_id' => $orderId,
			'sales_order_id' => (int) $so['id'],
			'sales_invoice_id' => (int) $so['sales_invoice_id'],
			'already_posted' => true,
		);
	}

	require_once __DIR__ . '/epc_erp_invoices.php';
	$existing = $db->prepare(
		'SELECT `id` FROM `epc_einvoice_documents` WHERE `order_id` = ? AND `active` = 1 ORDER BY `id` DESC LIMIT 1'
	);
	$existing->execute(array($orderId));
	$existingId = (int) $existing->fetchColumn();
	if ($existingId <= 0 && $so) {
		$soInv = $db->prepare('SELECT `id` FROM `epc_einvoice_documents` WHERE `sales_order_id` = ? AND `active` = 1 ORDER BY `id` DESC LIMIT 1');
		$soInv->execute(array((int) $so['id']));
		$existingId = (int) $soInv->fetchColumn();
	}
	if ($existingId > 0) {
		if ($so) {
			$db->prepare(
				'UPDATE `epc_erp_sales_orders` SET `status` = \'invoiced\', `sales_invoice_id` = ?, `fulfillment_status` = \'invoiced\', `time_updated` = ? WHERE `id` = ?'
			)->execute(array($existingId, time(), (int) $so['id']));
			$db->prepare('UPDATE `epc_einvoice_documents` SET `order_id` = ? WHERE `id` = ? AND `order_id` = 0')
				->execute(array($orderId, $existingId));
		}
		return array(
			'shop_order_id' => $orderId,
			'sales_order_id' => $so ? (int) $so['id'] : 0,
			'sales_invoice_id' => $existingId,
			'already_posted' => true,
		);
	}

	if ($so && in_array($so['status'], array('draft', 'confirmed'), true)) {
		$r = epc_erp_so_convert_to_invoice($db, (int) $so['id']);
		$db->prepare('UPDATE `epc_erp_sales_orders` SET `fulfillment_status` = \'invoiced\' WHERE `id` = ?')
			->execute(array((int) $so['id']));
		return array_merge($r, array('shop_order_id' => $orderId, 'already_posted' => false));
	}

	$invoiceId = epc_erp_invoice_from_order($db, $orderId, array(), $adminId > 0 ? $adminId : epc_erp_admin_id());
	if ($so) {
		$db->prepare(
			'UPDATE `epc_erp_sales_orders` SET `status` = \'invoiced\', `sales_invoice_id` = ?, `fulfillment_status` = \'invoiced\', `time_updated` = ? WHERE `id` = ?'
		)->execute(array($invoiceId, time(), (int) $so['id']));
	}

	return array(
		'shop_order_id' => $orderId,
		'sales_order_id' => $so ? (int) $so['id'] : 0,
		'sales_invoice_id' => $invoiceId,
		'already_posted' => false,
	);
}

/**
 * Auto-post received POs (cost) and sales invoice when order complete (revenue).
 */
function epc_erp_order_fulfillment_auto_post(PDO $db, int $orderId, int $adminId = 0): array
{
	$result = array('shop_order_id' => $orderId, 'purchase_invoices' => array(), 'sales_invoice' => null);
	epc_erp_order_fulfillment_sync_po_statuses($db, $orderId);

	$poSt = $db->prepare(
		'SELECT `id`, `status`, `purchase_id` FROM `epc_erp_purchase_orders`
		 WHERE `order_id` = ? AND `status` IN (\'partial\', \'received\') AND `purchase_id` = 0'
	);
	$poSt->execute(array($orderId));
	while ($po = $poSt->fetch(PDO::FETCH_ASSOC)) {
		try {
			$result['purchase_invoices'][] = epc_erp_order_fulfillment_post_po_invoice($db, (int) $po['id']);
		} catch (Exception $e) {
			$result['purchase_invoices'][] = array('po_id' => (int) $po['id'], 'error' => $e->getMessage());
		}
	}

	if (epc_erp_order_is_complete($db, $orderId)) {
		try {
			$result['sales_invoice'] = epc_erp_order_fulfillment_post_sales_invoice($db, $orderId, $adminId);
		} catch (Exception $e) {
			$result['sales_invoice'] = array('error' => $e->getMessage());
		}
	}

	$result['status'] = epc_erp_order_fulfillment_status($db, $orderId);
	return $result;
}
