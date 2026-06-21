<?php
/**
 * Procurement — suppliers, purchases, payments, fulfillment (uses ERP ledger).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_procurement_schema.php';
require_once __DIR__ . '/../finance/epc_erp_helpers.php';
require_once __DIR__ . '/../finance/epc_uae_vat.php';

function epc_proc_h($v)
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}

function epc_proc_money($n)
{
	return number_format((float)$n, 2, '.', ',');
}

function epc_procurement_dashboard(PDO $db): array
{
	epc_procurement_ensure_schema($db);
	$suppliers = (int)$db->query('SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1')->fetchColumn();
	$withTrn = (int)$db->query('SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1 AND `trn` != ""')->fetchColumn();
	$purchases = (int)$db->query('SELECT COUNT(*) FROM `epc_erp_purchases` WHERE `active` = 1')->fetchColumn();
	$payable = epc_erp_payable_balance($db);
	$storages = (int)$db->query('SELECT COUNT(*) FROM `shop_storages`')->fetchColumn();
	$linked = (int)$db->query('SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1 AND `storage_id` IS NOT NULL AND `storage_id` > 0')->fetchColumn();
	$advances = (float)$db->query('SELECT IFNULL(SUM(`amount`),0) FROM `epc_procurement_advances` WHERE `active` = 1')->fetchColumn();
	return array(
		'suppliers' => $suppliers,
		'suppliers_with_trn' => $withTrn,
		'purchase_invoices' => $purchases,
		'payable_balance' => $payable,
		'warehouses' => $storages,
		'warehouses_linked' => $linked,
		'advances_paid' => $advances,
	);
}

function epc_procurement_list_suppliers(PDO $db): array
{
	epc_procurement_ensure_schema($db);
	$rows = epc_erp_list_suppliers($db);
	foreach ($rows as &$r) {
		$sid = (int)($r['storage_id'] ?? 0);
		$r['warehouse_name'] = '';
		if ($sid > 0) {
			$st = $db->prepare('SELECT `name` FROM `shop_storages` WHERE `id` = ? LIMIT 1');
			$st->execute(array($sid));
			$r['warehouse_name'] = (string)$st->fetchColumn();
		}
	}
	return $rows;
}

function epc_procurement_list_warehouses(PDO $db): array
{
	return $db->query(
		'SELECT s.*,
			(SELECT `id` FROM `epc_erp_suppliers` WHERE `storage_id` = s.`id` AND `active` = 1 LIMIT 1) AS supplier_id
		FROM `shop_storages` s ORDER BY s.`name`'
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_procurement_update_supplier(PDO $db, int $id, array $data): void
{
	epc_procurement_ensure_schema($db);
	if ($id <= 0) {
		throw new Exception('Invalid supplier');
	}
	$country = epc_uae_vat_normalize_country($data['country_code'] ?? 'AE');
	$vatReg = !empty($data['vat_registered']) ? 1 : 0;
	$db->prepare(
		'UPDATE `epc_erp_suppliers` SET
		`name` = ?, `contact_email` = ?, `contact_phone` = ?, `trn` = ?,
		`country_code` = ?, `vat_registered` = ?, `storage_id` = ?,
		`legal_reg_no` = ?, `legal_reg_type` = ?, `authority_name` = ?,
		`address_line1` = ?, `city` = ?, `emirate` = ?, `payment_terms` = ?, `notes` = ?
		WHERE `id` = ? AND `active` = 1'
	)->execute(array(
		trim((string)$data['name']),
		trim((string)($data['contact_email'] ?? '')),
		trim((string)($data['contact_phone'] ?? '')),
		trim((string)($data['trn'] ?? '')),
		$country,
		$vatReg,
		!empty($data['storage_id']) ? (int)$data['storage_id'] : null,
		trim((string)($data['legal_reg_no'] ?? '')),
		in_array($data['legal_reg_type'] ?? 'TL', array('TL', 'EID', 'PAS', 'CD'), true) ? $data['legal_reg_type'] : 'TL',
		trim((string)($data['authority_name'] ?? '')),
		trim((string)($data['address_line1'] ?? '')),
		trim((string)($data['city'] ?? 'Dubai')),
		trim((string)($data['emirate'] ?? 'Dubai')),
		trim((string)($data['payment_terms'] ?? '')),
		trim((string)($data['notes'] ?? '')),
		$id,
	));
}

function epc_procurement_create_supplier(PDO $db, array $data): int
{
	epc_procurement_ensure_schema($db);
	$id = epc_erp_create_supplier($db, $data);
	epc_procurement_update_supplier($db, $id, $data);
	return $id;
}

function epc_procurement_record_advance(PDO $db, array $data): int
{
	epc_procurement_ensure_schema($db);
	$supplier_id = (int)($data['supplier_id'] ?? 0);
	$amount = round((float)($data['amount'] ?? 0), 2);
	if ($supplier_id <= 0 || $amount <= 0) {
		throw new Exception('Supplier and amount required');
	}
	$cash_id = epc_erp_supplier_payment($db, array(
		'supplier_id' => $supplier_id,
		'amount' => $amount,
		'reference' => trim((string)($data['reference'] ?? 'Advance payment')),
		'note' => trim((string)($data['note'] ?? 'Procurement advance')),
		'account_id' => (int)($data['account_id'] ?? 0),
		'is_advance' => 1,
		'purchase_order_id' => (int)($data['purchase_order_id'] ?? 0),
	));
	$db->prepare(
		'INSERT INTO `epc_procurement_advances` (`supplier_id`, `time`, `amount`, `reference`, `note`, `cash_entry_id`, `admin_id`)
		VALUES (?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		$supplier_id,
		time(),
		$amount,
		trim((string)($data['reference'] ?? '')),
		trim((string)($data['note'] ?? '')),
		$cash_id,
		epc_erp_admin_id(),
	));
	return (int)$db->lastInsertId();
}

function epc_procurement_open_orders(PDO $db, $limit = 40): array
{
	require_once __DIR__ . '/../finance/epc_erp_fulfilment.php';
	$limit = (int)$limit;
	$st = $db->query(
		'SELECT o.`id`, o.`time`, o.`user_id`, o.`paid`, o.`status`
		FROM `shop_orders` o
		WHERE o.`successfully_created` = 1
		ORDER BY o.`time` DESC LIMIT ' . $limit
	);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_procurement_get_supplier(PDO $db, int $id): ?array
{
	epc_procurement_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_suppliers` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_procurement_list_advances(PDO $db, int $supplier_id = 0, $limit = 100): array
{
	epc_procurement_ensure_schema($db);
	$sql = 'SELECT a.*, s.`name` AS supplier_name
		FROM `epc_procurement_advances` a
		INNER JOIN `epc_erp_suppliers` s ON s.`id` = a.`supplier_id`
		WHERE a.`active` = 1';
	$params = array();
	if ($supplier_id > 0) {
		$sql .= ' AND a.`supplier_id` = ?';
		$params[] = $supplier_id;
	}
	$sql .= ' ORDER BY a.`time` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_procurement_tab_url($base, $tab, $extra = '')
{
	$url = rtrim($base, '?') . '?tab=' . urlencode($tab);
	if ($extra !== '') {
		$url .= '&' . ltrim($extra, '&');
	}
	return $url;
}
