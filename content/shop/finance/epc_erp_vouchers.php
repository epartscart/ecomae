<?php
/**
 * ERP voucher numbering + ERP-only sales/purchase workflow (PO/SO → invoice, RV/PV/GV/TV).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';
require_once __DIR__ . '/epc_erp_phase8.php';

/** @return array<string, string> voucher type => prefix */
function epc_erp_voucher_prefix_map(): array
{
	return array(
		'PO' => 'PO-',
		'SO' => 'SO-',
		'PI' => 'PI-',
		'SI' => 'SI-',
		'RV' => 'RV-',
		'PV' => 'PV-',
		'GV' => 'GV-',
		'TV' => 'TV-',
	);
}

/** @return array<string, string> voucher type => human label (for setup screen). */
function epc_erp_voucher_type_labels(): array
{
	return array(
		'SO' => 'Sales order',
		'SI' => 'Sales invoice (tax invoice)',
		'PO' => 'Purchase order',
		'PI' => 'Purchase invoice',
		'RV' => 'Receipt voucher',
		'PV' => 'Payment voucher',
		'GV' => 'General journal',
		'TV' => 'Stock transfer',
	);
}

/**
 * Effective (tenant-configurable) prefix for a voucher type. Falls back to the
 * built-in default when no per-tenant override is saved in Accounting setup.
 */
function epc_erp_voucher_prefix_for(PDO $db, string $type): string
{
	$map = epc_erp_voucher_prefix_map();
	$default = $map[$type] ?? ($type . '-');
	require_once __DIR__ . '/epc_erp_extended.php';
	if (function_exists('epc_erp_platform_setting_get')) {
		$ov = trim((string) epc_erp_platform_setting_get($db, 'voucher_prefix_' . $type, ''));
		if ($ov !== '') {
			return $ov;
		}
	}
	return $default;
}

/** Effective zero-pad width for a voucher type (tenant-configurable, 1..10). */
function epc_erp_voucher_pad_for(PDO $db, string $type): int
{
	require_once __DIR__ . '/epc_erp_extended.php';
	if (function_exists('epc_erp_platform_setting_get')) {
		$p = (int) epc_erp_platform_setting_get($db, 'voucher_pad_' . $type, '5');
		if ($p >= 1 && $p <= 10) {
			return $p;
		}
	}
	return 5;
}

function epc_erp_is_erp_only_context(): bool
{
	if (function_exists('epc_portal_is_erp_only_tenant') && epc_portal_is_erp_only_tenant()) {
		return true;
	}
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
		return true;
	}
	return false;
}

/** True when tenant uses storefront order → SO → PO fulfilment (not ERP-only standalone). */
function epc_erp_has_commerce_integration(?array $settings = null): bool
{
	if ($settings !== null) {
		if (function_exists('epc_portal_resolve_access_mode')
			&& epc_portal_resolve_access_mode($settings) === 'erp_only') {
			return false;
		}
		return true;
	}
	if (epc_erp_is_erp_only_context()) {
		return false;
	}
	if (function_exists('epc_portal_tenant_has_storefront')) {
		return epc_portal_tenant_has_storefront();
	}
	if (function_exists('epc_portal_storefront_enabled')) {
		return epc_portal_storefront_enabled();
	}
	return true;
}

/** ERP tabs that depend on shop_orders / CP orders workspace. */
function epc_erp_commerce_tab_keys(): array
{
	return array('fulfilment', 'revenue', 'delivery_notes', 'procurement_link');
}

function epc_erp_filter_commerce_tabs(array $tabs, ?array $settings = null): array
{
	if (epc_erp_has_commerce_integration($settings)) {
		return $tabs;
	}
	return array_values(array_diff($tabs, epc_erp_commerce_tab_keys()));
}

function epc_erp_vouchers_ensure_schema(PDO $db): void
{
	epc_erp_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_extended.php';
	epc_erp_extended_ensure_schema($db);

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_voucher_sequences` (
		`voucher_type` varchar(8) NOT NULL,
		`year` int(11) NOT NULL,
		`last_seq` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`voucher_type`, `year`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP voucher number sequences';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_sales_orders` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`so_no` varchar(32) NOT NULL,
		`customer_user_id` int(11) NOT NULL DEFAULT 0,
		`contact_id` int(11) NOT NULL DEFAULT 0,
		`title` varchar(255) NOT NULL DEFAULT '',
		`amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`status` enum('draft','confirmed','invoiced','cancelled') NOT NULL DEFAULT 'draft',
		`sales_invoice_id` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_so_no` (`so_no`),
		KEY `x_customer` (`customer_user_id`),
		KEY `x_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP manual sales orders (no storefront)';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_sales_order_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`sales_order_id` int(11) NOT NULL,
		`line_no` int(11) NOT NULL DEFAULT 1,
		`description` varchar(255) NOT NULL,
		`qty` decimal(14,3) NOT NULL DEFAULT 1.000,
		`unit_price_ex_vat` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`line_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		PRIMARY KEY (`id`),
		KEY `x_so` (`sales_order_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP sales order lines';");

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchase_orders', 'voucher_no', 'varchar(32) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'voucher_no', 'varchar(32) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'po_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'voucher_no', 'varchar(32) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'sales_order_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'sales_invoice_id', 'int(11) NOT NULL DEFAULT 0');

	$db->exec('UPDATE `epc_erp_purchase_orders` SET `voucher_no` = `po_no` WHERE (`voucher_no` IS NULL OR `voucher_no` = \'\') AND `po_no` != \'\'');
}

/**
 * Next voucher number: PREFIX-YYYY-NNNNN (atomic per type/year).
 */
function epc_erp_next_voucher_no(PDO $db, string $type): string
{
	epc_erp_vouchers_ensure_schema($db);
	$type = strtoupper(preg_replace('/[^A-Z]/', '', $type));
	$map = epc_erp_voucher_prefix_map();
	if (!isset($map[$type])) {
		throw new Exception('Unknown voucher type: ' . $type);
	}
	$prefix = epc_erp_voucher_prefix_for($db, $type);
	$pad = epc_erp_voucher_pad_for($db, $type);
	$year = (int) date('Y');
	$started = $db->beginTransaction();
	try {
		$sel = $db->prepare(
			'SELECT `last_seq` FROM `epc_erp_voucher_sequences` WHERE `voucher_type` = ? AND `year` = ? FOR UPDATE'
		);
		$sel->execute(array($type, $year));
		$seq = (int) $sel->fetchColumn();
		if ($seq <= 0) {
			$db->prepare(
				'INSERT INTO `epc_erp_voucher_sequences` (`voucher_type`, `year`, `last_seq`) VALUES (?,?,1)
				 ON DUPLICATE KEY UPDATE `last_seq` = `last_seq` + 1'
			)->execute(array($type, $year));
			$seq = 1;
		} else {
			$seq++;
			$db->prepare(
				'UPDATE `epc_erp_voucher_sequences` SET `last_seq` = ? WHERE `voucher_type` = ? AND `year` = ?'
			)->execute(array($seq, $type, $year));
		}
		if ($started) {
			$db->commit();
		}
	} catch (Exception $e) {
		if ($started && $db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
	return $prefix . $year . '-' . str_pad((string) $seq, $pad, '0', STR_PAD_LEFT);
}

function epc_erp_manual_sales_orders_list(PDO $db, int $date_from = 0, int $date_to = 0, array $filters = array(), int $limit = 150): array
{
	epc_erp_vouchers_ensure_schema($db);
	$sql = 'SELECT so.*, u.`email` AS customer_email, d.`invoice_number` AS invoice_no
		FROM `epc_erp_sales_orders` so
		LEFT JOIN `users` u ON u.`user_id` = so.`customer_user_id`
		LEFT JOIN `epc_einvoice_documents` d ON d.`id` = so.`sales_invoice_id`
		WHERE 1=1';
	$params = array();
	if ($date_from > 0) {
		$sql .= ' AND so.`time_created` >= ?';
		$params[] = $date_from;
	}
	if ($date_to > 0) {
		$sql .= ' AND so.`time_created` <= ?';
		$params[] = $date_to;
	}
	if (!empty($filters['status'])) {
		$sql .= ' AND so.`status` = ?';
		$params[] = $filters['status'];
	}
	if (!empty($filters['q'])) {
		$sql .= ' AND (so.`so_no` LIKE ? OR u.`email` LIKE ? OR so.`title` LIKE ?)';
		$like = '%' . $filters['q'] . '%';
		$params = array_merge($params, array($like, $like, $like));
	}
	$sql .= ' ORDER BY so.`time_updated` DESC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_sales_order_parse_lines(array $data): array
{
	$lines = array();
	if (!empty($data['lines_json'])) {
		$decoded = json_decode((string) $data['lines_json'], true);
		if (is_array($decoded)) {
			$lines = $decoded;
		}
	}
	if (empty($lines) && !empty($data['line_desc']) && is_array($data['line_desc'])) {
		$n = count($data['line_desc']);
		for ($i = 0; $i < $n; $i++) {
			$desc = trim((string) ($data['line_desc'][$i] ?? ''));
			if ($desc === '') {
				continue;
			}
			$qty = max(0.0001, (float) ($data['line_qty'][$i] ?? 1));
			$unit = round((float) ($data['line_unit'][$i] ?? 0), 4);
			$net = round($qty * $unit, 2);
			$lines[] = array(
				'description' => $desc,
				'qty' => $qty,
				'unit_price_ex_vat' => $unit,
				'line_ex_vat' => $net,
			);
		}
	}
	return $lines;
}

function epc_erp_sales_order_save(PDO $db, array $data): int
{
	epc_erp_vouchers_ensure_schema($db);
	$id = (int) ($data['id'] ?? 0);
	$customerId = (int) ($data['customer_user_id'] ?? $data['user_id'] ?? 0);
	$title = trim((string) ($data['title'] ?? ''));
	$lines = epc_erp_sales_order_parse_lines($data);
	$amountEx = round((float) ($data['amount_ex_vat'] ?? 0), 2);
	if (!empty($lines)) {
		$amountEx = 0.0;
		foreach ($lines as $ln) {
			$amountEx += round((float) ($ln['line_ex_vat'] ?? 0), 2);
		}
		$amountEx = round($amountEx, 2);
	}
	if ($customerId <= 0 || $title === '' || $amountEx <= 0) {
		throw new Exception('Customer, title, and amount (or lines) are required');
	}
	$uq = $db->prepare('SELECT `user_id` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$uq->execute(array($customerId));
	if (!$uq->fetch()) {
		throw new Exception('Customer not found');
	}
	require_once __DIR__ . '/epc_tax_toolkit.php';
	$taxCalc = epc_tax_toolkit_calc_amounts($db, $amountEx, $customerId, (int) ($data['contact_id'] ?? 0));
	$vat = $taxCalc['vat_amount'];
	$total = $taxCalc['total_amount'];
	$now = time();
	$status = in_array($data['status'] ?? '', array('draft', 'confirmed', 'invoiced', 'cancelled'), true)
		? $data['status'] : 'draft';
	if ($id > 0) {
		$chk = $db->prepare('SELECT `status` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1');
		$chk->execute(array($id));
		$cur = (string) $chk->fetchColumn();
		if ($cur === 'invoiced') {
			throw new Exception('Invoiced sales orders cannot be edited');
		}
		$db->prepare(
			'UPDATE `epc_erp_sales_orders` SET `customer_user_id`=?, `title`=?, `amount_ex_vat`=?, `vat_amount`=?, `total_amount`=?, `status`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array($customerId, $title, $amountEx, $vat, $total, $status, trim((string) ($data['notes'] ?? '')), $now, $id));
		$db->prepare('DELETE FROM `epc_erp_sales_order_lines` WHERE `sales_order_id` = ?')->execute(array($id));
		$soId = $id;
	} else {
		$soNo = epc_erp_next_voucher_no($db, 'SO');
		$db->prepare(
			'INSERT INTO `epc_erp_sales_orders` (`so_no`, `customer_user_id`, `contact_id`, `title`, `amount_ex_vat`, `vat_amount`, `total_amount`, `status`, `notes`, `admin_id`, `time_created`, `time_updated`)
			 VALUES (?,?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array(
			$soNo,
			$customerId,
			(int) ($data['contact_id'] ?? 0),
			$title,
			$amountEx,
			$vat,
			$total,
			'draft',
			trim((string) ($data['notes'] ?? '')),
			epc_erp_admin_id(),
			$now,
			$now,
		));
		$soId = (int) $db->lastInsertId();
	}
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_sales_order_lines` (`sales_order_id`, `line_no`, `description`, `qty`, `unit_price_ex_vat`, `line_ex_vat`)
		 VALUES (?,?,?,?,?,?)'
	);
	$lineNo = 1;
	foreach ($lines as $ln) {
		$ins->execute(array(
			$soId,
			$lineNo++,
			mb_substr(trim((string) ($ln['description'] ?? $ln['item_name'] ?? 'Line')), 0, 255),
			max(0.0001, (float) ($ln['qty'] ?? 1)),
			round((float) ($ln['unit_price_ex_vat'] ?? $ln['unit_price'] ?? 0), 4),
			round((float) ($ln['line_ex_vat'] ?? 0), 2),
		));
	}
	if (empty($lines)) {
		$ins->execute(array($soId, 1, $title, 1, $amountEx, $amountEx));
	}
	if (empty($soNo)) {
		$g = $db->prepare('SELECT `so_no` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1');
		$g->execute(array($soId));
		$soNo = (string) $g->fetchColumn();
	}
	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'sales_order_save', 'sales_order', $soId, 'Sales order saved', array('so_no' => $soNo));
	return $soId;
}

function epc_erp_sales_order_set_status(PDO $db, int $soId, string $status): bool
{
	epc_erp_vouchers_ensure_schema($db);
	$allowed = array('draft', 'confirmed', 'invoiced', 'cancelled');
	if (!in_array($status, $allowed, true)) {
		throw new Exception('Invalid sales order status');
	}
	$db->prepare('UPDATE `epc_erp_sales_orders` SET `status` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($status, time(), $soId));
	return true;
}

/**
 * Delete a sales order. Only draft orders may be deleted; confirmed/invoiced
 * orders are part of the audit trail and must be cancelled, not removed.
 */
function epc_erp_sales_order_delete(PDO $db, int $soId): bool
{
	epc_erp_vouchers_ensure_schema($db);
	$st = $db->prepare('SELECT `status` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($soId));
	$status = $st->fetchColumn();
	if ($status === false) {
		throw new Exception('Sales order not found');
	}
	if ($status !== 'draft') {
		throw new Exception('Only draft sales orders can be deleted');
	}
	$started = $db->beginTransaction();
	try {
		$db->prepare('DELETE FROM `epc_erp_sales_order_lines` WHERE `sales_order_id` = ?')->execute(array($soId));
		$db->prepare('DELETE FROM `epc_erp_sales_orders` WHERE `id` = ?')->execute(array($soId));
		if ($started) {
			$db->commit();
		}
	} catch (Exception $e) {
		if ($started && $db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
	return true;
}

function epc_erp_so_convert_to_invoice(PDO $db, int $soId): array
{
	epc_erp_vouchers_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_invoices.php';
	epc_erp_invoices_ensure_schema($db);

	$st = $db->prepare('SELECT * FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($soId));
	$so = $st->fetch(PDO::FETCH_ASSOC);
	if (!$so) {
		throw new Exception('Sales order not found');
	}
	if ((int) ($so['sales_invoice_id'] ?? 0) > 0) {
		throw new Exception('Sales order already invoiced');
	}
	if (!in_array($so['status'], array('draft', 'confirmed'), true)) {
		throw new Exception('Only draft or confirmed sales orders can be invoiced');
	}

	$linesSt = $db->prepare('SELECT * FROM `epc_erp_sales_order_lines` WHERE `sales_order_id` = ? ORDER BY `line_no`');
	$linesSt->execute(array($soId));
	$rawLines = $linesSt->fetchAll(PDO::FETCH_ASSOC);
	$invLines = array();
	foreach ($rawLines as $ln) {
		$qty = max(0.0001, (float) $ln['qty']);
		$unit = round((float) $ln['unit_price_ex_vat'], 4);
		$net = round((float) $ln['line_ex_vat'], 2);
		require_once __DIR__ . '/epc_tax_toolkit.php';
		$lineTax = epc_tax_toolkit_calc_line($db, $net, (int) $so['customer_user_id'], (int) ($so['contact_id'] ?? 0));
		$invLines[] = array(
			'line_no' => count($invLines) + 1,
			'item_name' => $ln['description'],
			'item_description' => '',
			'item_type' => 'G',
			'quantity' => $qty,
			'uom_code' => 'C62',
			'unit_price' => $unit,
			'line_net' => $net,
			'tax_category' => $lineTax['tax_category'],
			'tax_rate' => $lineTax['tax_rate'],
			'tax_amount' => $lineTax['tax_amount'],
			'gross_amount' => $lineTax['gross_amount'],
			'vat_line_aed' => $lineTax['vat_line_aed'],
			'line_amount_aed' => $lineTax['line_amount_aed'],
		);
	}

	$siNo = epc_erp_next_voucher_no($db, 'SI');
	$post = array(
		'invoice_number' => $siNo,
		'user_id' => (int) $so['customer_user_id'],
		'order_id' => (int) ($so['shop_order_id'] ?? 0),
		'sales_order_id' => $soId,
		'issue_date' => date('Y-m-d'),
		'payment_due_date' => date('Y-m-d', strtotime('+30 days')),
		'line_desc' => array(),
		'line_qty' => array(),
		'line_unit' => array(),
	);
	foreach ($invLines as $ln) {
		$post['line_desc'][] = $ln['item_name'];
		$post['line_qty'][] = $ln['quantity'];
		$post['line_unit'][] = $ln['unit_price'];
	}

	$invoiceId = epc_erp_invoice_save($db, $post, epc_erp_admin_id());
	$shopOrderId = (int) ($so['shop_order_id'] ?? 0);
	$db->prepare(
		'UPDATE `epc_einvoice_documents` SET `sales_order_id` = ?, `order_id` = IF(? > 0, ?, `order_id`) WHERE `id` = ?'
	)->execute(array($soId, $shopOrderId, $shopOrderId, $invoiceId));

	$totalIncl = round((float) $so['total_amount'], 2);
	epc_erp_customer_settlement($db, array(
		'user_id' => (int) $so['customer_user_id'],
		'amount' => $totalIncl,
		'direction' => 'credit',
		'entry_kind' => 'adjustment',
		'reference' => $siNo,
		'note' => 'Sales invoice from SO ' . $so['so_no'],
		'post_gl' => true,
	));

	$db->prepare(
		'UPDATE `epc_erp_sales_orders` SET `status` = \'invoiced\', `sales_invoice_id` = ?, `time_updated` = ? WHERE `id` = ?'
	)->execute(array($invoiceId, time(), $soId));

	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'so_to_invoice', 'sales_order', $soId, 'Converted to sales invoice', array('invoice_id' => $invoiceId, 'si_no' => $siNo));

	return array(
		'sales_order_id' => $soId,
		'sales_invoice_id' => $invoiceId,
		'invoice_number' => $siNo,
	);
}

function epc_erp_po_convert_to_purchase(PDO $db, int $poId): array
{
	epc_erp_vouchers_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($poId));
	$po = $st->fetch(PDO::FETCH_ASSOC);
	if (!$po) {
		throw new Exception('Purchase order not found');
	}
	if ((int) ($po['purchase_id'] ?? 0) > 0) {
		throw new Exception('Purchase order already linked to a purchase invoice');
	}
	if (!in_array($po['status'], array('approved', 'partial', 'received'), true)) {
		throw new Exception('Approve the PO before converting to purchase invoice');
	}

	$piNo = epc_erp_next_voucher_no($db, 'PI');
	$purchaseId = epc_erp_create_purchase($db, array(
		'supplier_id' => (int) $po['supplier_id'],
		'order_id' => (int) ($po['order_id'] ?? 0),
		'invoice_number' => $piNo,
		'amount_ex_vat' => (float) $po['amount_ex_vat'],
		'note' => 'From PO ' . $po['po_no'] . ($po['notes'] ? (' — ' . $po['notes']) : ''),
		'status' => 'confirmed',
		'allow_open_order' => !empty($po['order_id']),
	));

	$db->prepare(
		'UPDATE `epc_erp_purchases` SET `voucher_no` = ?, `po_id` = ? WHERE `id` = ?'
	)->execute(array($piNo, $poId, $purchaseId));
	$db->prepare(
		'UPDATE `epc_erp_purchase_orders` SET `purchase_id` = ?, `voucher_no` = ?, `status` = \'received\', `received_at` = ?, `time_updated` = ? WHERE `id` = ?'
	)->execute(array($purchaseId, (string) ($po['voucher_no'] ?: $po['po_no']), time(), time(), $poId));

	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'po_to_purchase', 'purchase_order', $poId, 'Converted to purchase invoice', array('purchase_id' => $purchaseId, 'pi_no' => $piNo));

	return array(
		'po_id' => $poId,
		'purchase_id' => $purchaseId,
		'voucher_no' => $piNo,
	);
}

function epc_erp_receipt_voucher(PDO $db, array $data): array
{
	epc_erp_vouchers_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_advances.php';
	epc_erp_advances_ensure_schema($db);
	$userId = (int) ($data['user_id'] ?? $data['customer_user_id'] ?? 0);
	$accountId = (int) ($data['account_id'] ?? 0);
	$amount = round((float) ($data['amount'] ?? 0), 2);
	$isAdvance = !isset($data['is_advance']) || !empty($data['is_advance']);
	$salesOrderId = (int) ($data['sales_order_id'] ?? 0);
	if ($userId <= 0 || $accountId <= 0 || $amount <= 0) {
		throw new Exception('Customer, bank account, and amount required');
	}
	$voucherNo = epc_erp_next_voucher_no($db, 'RV');
	$time = !empty($data['time']) ? (int) $data['time'] : time();
	$reference = trim((string) ($data['reference'] ?? $voucherNo));
	$note = trim((string) ($data['note'] ?? ($isAdvance ? 'Customer advance receipt ' : 'Customer receipt ') . $voucherNo));

	$db->prepare(
		'INSERT INTO `epc_erp_cash_bank_entries`
		(`account_id`, `time`, `entry_type`, `direction`, `amount`, `counterparty_type`, `counterparty_id`, `reference`, `note`, `voucher_no`, `sales_order_id`, `sales_invoice_id`, `is_advance`, `admin_id`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$accountId,
		$time,
		'receipt',
		1,
		$amount,
		'customer',
		$userId,
		$reference,
		$note,
		$voucherNo,
		$salesOrderId,
		(int) ($data['sales_invoice_id'] ?? 0),
		$isAdvance ? 1 : 0,
		epc_erp_admin_id(),
	));
	$cashId = (int) $db->lastInsertId();

	$settle = epc_erp_customer_settlement($db, array(
		'user_id' => $userId,
		'amount' => $amount,
		'direction' => 'debit',
		'entry_kind' => $isAdvance ? 'settlement' : 'settlement',
		'reference' => $voucherNo,
		'note' => $note,
		'post_gl' => !empty($data['post_gl']) && !$isAdvance,
		'order_id' => (int) ($data['order_id'] ?? 0),
	));

	if ($isAdvance) {
		epc_uae_vat_record_advance_on_receipt($db, $cashId, $userId, $amount, $salesOrderId, $time);
		try {
			epc_erp_gl_post_advance_receipt($db, $cashId);
		} catch (Exception $e) {
		}
	} else {
		try {
			epc_erp_gl_post_cash_entry($db, $cashId);
		} catch (Exception $e) {
		}
	}

	return array(
		'voucher_no' => $voucherNo,
		'cash_entry_id' => $cashId,
		'ledger_id' => $settle['ledger_id'] ?? 0,
		'is_advance' => $isAdvance ? 1 : 0,
	);
}

function epc_erp_payment_voucher(PDO $db, array $data): array
{
	epc_erp_vouchers_ensure_schema($db);
	$data['reference'] = trim((string) ($data['reference'] ?? ''));
	if ($data['reference'] === '') {
		$data['reference'] = epc_erp_next_voucher_no($db, 'PV');
	} elseif (strpos($data['reference'], 'PV-') !== 0) {
		$data['reference'] = epc_erp_next_voucher_no($db, 'PV');
	}
	$cashId = epc_erp_supplier_payment($db, $data);
	$db->prepare('UPDATE `epc_erp_cash_bank_entries` SET `voucher_no` = ? WHERE `id` = ?')
		->execute(array($data['reference'], $cashId));
	return array('voucher_no' => $data['reference'], 'cash_entry_id' => $cashId);
}

function epc_erp_transfer_voucher(PDO $db, array $data): array
{
	epc_erp_vouchers_ensure_schema($db);
	$fromId = (int) ($data['from_account_id'] ?? 0);
	$toId = (int) ($data['to_account_id'] ?? 0);
	$amount = round((float) ($data['amount'] ?? 0), 2);
	if ($fromId <= 0 || $toId <= 0 || $fromId === $toId || $amount <= 0) {
		throw new Exception('Two distinct accounts and a positive amount required');
	}
	$voucherNo = epc_erp_next_voucher_no($db, 'TV');
	$time = !empty($data['time']) ? (int) $data['time'] : time();
	$note = trim((string) ($data['note'] ?? 'Transfer ' . $voucherNo));
	if (!$db->beginTransaction()) {
		throw new Exception('Transaction start failed');
	}
	try {
		$db->prepare(
			'INSERT INTO `epc_erp_cash_bank_entries`
			(`account_id`, `time`, `entry_type`, `direction`, `amount`, `counterparty_type`, `counterparty_id`, `reference`, `note`, `voucher_no`, `admin_id`)
			VALUES (?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array($fromId, $time, 'transfer_out', 0, $amount, 'internal', $toId, $voucherNo, $note, $voucherNo, epc_erp_admin_id()));
		$outId = (int) $db->lastInsertId();
		$db->prepare(
			'INSERT INTO `epc_erp_cash_bank_entries`
			(`account_id`, `time`, `entry_type`, `direction`, `amount`, `counterparty_type`, `counterparty_id`, `transfer_pair_id`, `reference`, `note`, `voucher_no`, `admin_id`)
			VALUES (?,?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array($toId, $time, 'transfer_in', 1, $amount, 'internal', $fromId, $outId, $voucherNo, $note, $voucherNo, epc_erp_admin_id()));
		$inId = (int) $db->lastInsertId();
		$db->prepare('UPDATE `epc_erp_cash_bank_entries` SET `transfer_pair_id` = ? WHERE `id` = ?')
			->execute(array($inId, $outId));
		$db->commit();
		try {
			epc_erp_gl_post_cash_entry($db, $outId);
			epc_erp_gl_post_cash_entry($db, $inId);
		} catch (Exception $e) {
		}
		return array('voucher_no' => $voucherNo, 'out_id' => $outId, 'in_id' => $inId);
	} catch (Exception $e) {
		$db->rollBack();
		throw $e;
	}
}

/**
 * Customer statement — SO, SI, RV, ledger, opening balance (ERP-only aggregate).
 *
 * @return array<int, array<string, mixed>>
 */
function epc_erp_customer_statement_aggregate(PDO $db, int $user_id, int $from = 0, int $to = 0, int $limit = 200): array
{
	epc_erp_vouchers_ensure_schema($db);
	$user_id = (int) $user_id;
	if ($user_id <= 0) {
		return array();
	}
	$rows = array();

	if ($from <= 0 && $to <= 0) {
		$to = time();
		$from = strtotime('-2 years', $to);
	}

	$st = $db->prepare(
		'SELECT `id`, `so_no` AS voucher_no, \'SO\' AS voucher_type, `time_created` AS `time`,
			`title` AS description, `total_amount` AS amount, `status`
		 FROM `epc_erp_sales_orders`
		 WHERE `customer_user_id` = ? AND `time_created` >= ? AND `time_created` <= ?
		 ORDER BY `time_created` DESC'
	);
	$st->execute(array($user_id, $from, $to));
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$rows[] = array(
			'time' => (int) $r['time'],
			'voucher_type' => 'SO',
			'voucher_no' => $r['voucher_no'],
			'description' => $r['description'] . ' (' . $r['status'] . ')',
			'debit' => in_array($r['status'], array('confirmed', 'draft'), true) ? (float) $r['amount'] : 0.0,
			'credit' => 0.0,
			'source' => 'sales_order',
			'source_id' => (int) $r['id'],
		);
	}

	$inv = $db->prepare(
		'SELECT `id`, `invoice_number` AS voucher_no, \'SI\' AS voucher_type, `issue_date` AS `time`,
			`total_incl_vat` AS amount, `status`
		 FROM `epc_einvoice_documents`
		 WHERE `user_id` = ? AND `active` = 1 AND `issue_date` >= ? AND `issue_date` <= ?
		 ORDER BY `issue_date` DESC'
	);
	$inv->execute(array($user_id, $from, $to));
	while ($r = $inv->fetch(PDO::FETCH_ASSOC)) {
		$rows[] = array(
			'time' => (int) $r['time'],
			'voucher_type' => 'SI',
			'voucher_no' => $r['voucher_no'],
			'description' => 'Sales invoice (' . $r['status'] . ')',
			'debit' => (float) $r['amount'],
			'credit' => 0.0,
			'source' => 'sales_invoice',
			'source_id' => (int) $r['id'],
		);
	}

	$cashSql = 'SELECT `id`, `voucher_no`, `time`, `amount`, `reference`, `note`, `is_advance`
		 FROM `epc_erp_cash_bank_entries`
		 WHERE `active` = 1 AND `counterparty_type` = \'customer\' AND `counterparty_id` = ?
		   AND `entry_type` = \'receipt\' AND `time` >= ? AND `time` <= ?
		 ORDER BY `time` DESC';
	try {
		$c = $db->query("SHOW COLUMNS FROM `epc_erp_cash_bank_entries` LIKE 'is_advance'");
		if (!$c->fetch()) {
			$cashSql = 'SELECT `id`, `voucher_no`, `time`, `amount`, `reference`, `note`, 0 AS `is_advance`
			 FROM `epc_erp_cash_bank_entries`
			 WHERE `active` = 1 AND `counterparty_type` = \'customer\' AND `counterparty_id` = ?
			   AND `entry_type` = \'receipt\' AND `time` >= ? AND `time` <= ?
			 ORDER BY `time` DESC';
		}
	} catch (Exception $e) {
	}
	$cash = $db->prepare($cashSql);
	$cash->execute(array($user_id, $from, $to));
	while ($r = $cash->fetch(PDO::FETCH_ASSOC)) {
		$vno = $r['voucher_no'] ?: $r['reference'];
		$isAdv = !empty($r['is_advance']);
		$rows[] = array(
			'time' => (int) $r['time'],
			'voucher_type' => $isAdv ? 'ADV' : 'RV',
			'voucher_no' => $vno,
			'description' => ($isAdv ? 'Advance receipt — ' : '') . ($r['note'] ?: 'Receipt voucher'),
			'debit' => 0.0,
			'credit' => (float) $r['amount'],
			'source' => $isAdv ? 'advance_receipt' : 'cash_entry',
			'source_id' => (int) $r['id'],
			'is_advance' => $isAdv ? 1 : 0,
		);
	}

	$legacy = $db->prepare(
		'SELECT a.*,
			(SELECT `key` FROM `shop_accounting_codes` WHERE `id` = a.`operation_code` LIMIT 1) AS operation_key
		 FROM `shop_users_accounting` a
		 WHERE a.`user_id` = ? AND a.`active` = 1 AND a.`time` >= ? AND a.`time` <= ?
		 ORDER BY a.`time` DESC'
	);
	$legacy->execute(array($user_id, $from, $to));
	while ($line = $legacy->fetch(PDO::FETCH_ASSOC)) {
		$ref = '';
		if (!empty($line['tech_value_text'])) {
			$meta = json_decode((string) $line['tech_value_text'], true);
			if (is_array($meta) && !empty($meta['reference'])) {
				$ref = (string) $meta['reference'];
			}
		}
		$isCredit = ((int) $line['income'] === 1);
		$rows[] = array(
			'time' => (int) $line['time'],
			'voucher_type' => $isCredit ? 'AR+' : 'AR-',
			'voucher_no' => $ref !== '' ? $ref : ('LEDGER-' . (int) $line['id']),
			'description' => (string) ($line['operation_key'] ?? 'Ledger'),
			'debit' => $isCredit ? (float) $line['amount'] : 0.0,
			'credit' => $isCredit ? 0.0 : (float) $line['amount'],
			'source' => 'shop_users_accounting',
			'source_id' => (int) $line['id'],
			'order_id' => (int) ($line['order_id'] ?? 0),
		);
	}

	usort($rows, function ($a, $b) {
		return ($b['time'] ?? 0) <=> ($a['time'] ?? 0);
	});
	return array_slice($rows, 0, (int) $limit);
}

function epc_erp_erp_only_dashboard_links(string $erpUrl, string $from, string $to): array
{
	return array(
		array('label' => 'Purchase orders', 'tab' => 'purchase_orders', 'area' => 'purchasing', 'icon' => 'fa-clipboard'),
		array('label' => 'Sales orders', 'tab' => 'sales_orders', 'area' => 'sales', 'icon' => 'fa-shopping-cart'),
		array('label' => 'Receipt vouchers', 'tab' => 'cash_bank', 'area' => 'finance', 'icon' => 'fa-download'),
		array('label' => 'Payment vouchers', 'tab' => 'payables', 'area' => 'purchasing', 'icon' => 'fa-upload'),
		array('label' => 'General ledger', 'tab' => 'gl', 'area' => 'finance', 'icon' => 'fa-book'),
		array('label' => 'Customer statements', 'tab' => 'receivables', 'area' => 'sales', 'icon' => 'fa-users'),
		array('label' => 'Document control', 'tab' => 'document_control', 'area' => 'finance', 'icon' => 'fa-print'),
	);
}
