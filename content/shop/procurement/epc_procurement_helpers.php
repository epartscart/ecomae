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

/**
 * Normalize a short vendor code (strip legacy " (old)" suffixes).
 */
function epc_procurement_normalize_vendor_code(string $code): string
{
	$code = trim($code);
	if ($code === '') {
		return '';
	}
	$code = preg_replace('/\s*\(old\)\s*$/i', '', $code) ?? $code;
	return trim($code);
}

/**
 * Display label: "Full Legal Name [VENDOR-CODE]".
 */
function epc_procurement_supplier_label(array $s): string
{
	$full = trim((string)($s['full_name'] ?? $s['name'] ?? ''));
	$code = epc_procurement_normalize_vendor_code((string)($s['vendor_code'] ?? ''));
	if ($full === '') {
		$full = $code !== '' ? $code : ('Supplier #' . (int)($s['id'] ?? 0));
	}
	if ($code !== '' && strcasecmp($code, $full) !== 0) {
		return $full . ' [' . $code . ']';
	}
	return $full;
}

/**
 * Enrich a supplier row with warehouse full name + vendor code for CP display.
 */
function epc_procurement_enrich_supplier(PDO $db, array $r): array
{
	$sid = (int)($r['storage_id'] ?? 0);
	$r['warehouse_name'] = '';
	$r['warehouse_short'] = '';
	$whFull = '';
	$whShort = '';
	if ($sid > 0) {
		$st = $db->prepare('SELECT `name`, `short_name` FROM `shop_storages` WHERE `id` = ? LIMIT 1');
		$st->execute(array($sid));
		$wh = $st->fetch(PDO::FETCH_ASSOC) ?: array();
		$whFull = trim((string)($wh['name'] ?? ''));
		$whShort = epc_procurement_normalize_vendor_code((string)($wh['short_name'] ?? ''));
		$r['warehouse_name'] = $whFull;
		$r['warehouse_short'] = $whShort;

		// Prefer approved vendor account full/short when present for this warehouse.
		try {
			$va = $db->prepare(
				'SELECT `vendor_full`, `vendor_short` FROM `epc_vendor_accounts`
				 WHERE `storage_id` = ? AND `status` = \'approved\'
				 ORDER BY `id` DESC LIMIT 1'
			);
			$va->execute(array($sid));
			$acc = $va->fetch(PDO::FETCH_ASSOC);
			if ($acc) {
				$vf = trim((string)($acc['vendor_full'] ?? ''));
				$vs = epc_procurement_normalize_vendor_code((string)($acc['vendor_short'] ?? ''));
				if ($vf !== '') {
					$whFull = $vf;
				}
				if ($vs !== '') {
					$whShort = $vs;
				}
			}
		} catch (Throwable $e) {
		}
	}

	$name = trim((string)($r['name'] ?? ''));
	$code = epc_procurement_normalize_vendor_code((string)($r['vendor_code'] ?? ''));
	if ($code === '') {
		$code = epc_procurement_normalize_vendor_code((string)($r['vendor_account'] ?? ''));
	}
	if ($code === '' && $whShort !== '') {
		$code = $whShort;
	}

	// Full name: keep explicit legal name when it is richer than the short code.
	$full = $name;
	if ($whFull !== '') {
		if ($full === '' || strcasecmp($full, $code) === 0 || strcasecmp($full, $whShort) === 0
			|| (strlen($whFull) > strlen($full) && (stripos($whFull, $full) !== false || strlen($full) <= 12))
		) {
			$full = $whFull;
		}
	}
	if ($full === '') {
		$full = $code !== '' ? $code : ('Supplier #' . (int)($r['id'] ?? 0));
	}

	$r['full_name'] = $full;
	$r['vendor_code'] = $code;
	$r['display_label'] = epc_procurement_supplier_label($r);
	return $r;
}

function epc_procurement_list_suppliers(PDO $db): array
{
	epc_procurement_ensure_schema($db);
	$rows = epc_erp_list_suppliers($db);
	foreach ($rows as &$r) {
		$r = epc_procurement_enrich_supplier($db, $r);
	}
	unset($r);
	return $rows;
}

/**
 * Create missing warehouse→supplier links and refresh full name + vendor code.
 *
 * @return array{created:int,updated:int}
 */
function epc_procurement_sync_suppliers_from_warehouses(PDO $db): array
{
	epc_procurement_ensure_schema($db);
	$created = (int) epc_erp_sync_suppliers_from_storages($db);
	$updated = 0;
	$storages = $db->query('SELECT `id`, `name`, `short_name` FROM `shop_storages`')->fetchAll(PDO::FETCH_ASSOC);
	$sel = $db->prepare('SELECT `id`, `name`, `vendor_code`, `vendor_account` FROM `epc_erp_suppliers` WHERE `storage_id` = ? AND `active` = 1 LIMIT 1');
	$upd = $db->prepare('UPDATE `epc_erp_suppliers` SET `name` = ?, `vendor_code` = ? WHERE `id` = ? AND `active` = 1');
	foreach ($storages as $s) {
		$sid = (int) $s['id'];
		$full = trim((string) ($s['name'] ?? ''));
		$code = epc_procurement_normalize_vendor_code((string) ($s['short_name'] ?? ''));
		if ($full === '' && $code === '') {
			continue;
		}
		if ($full === '') {
			$full = $code;
		}
		$sel->execute(array($sid));
		$row = $sel->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			continue;
		}
		$curName = trim((string) ($row['name'] ?? ''));
		$curCode = epc_procurement_normalize_vendor_code((string) ($row['vendor_code'] ?? ''));
		if ($curCode === '') {
			$curCode = epc_procurement_normalize_vendor_code((string) ($row['vendor_account'] ?? ''));
		}
		$newName = $curName;
		// Refresh short warehouse codes / empty names to the warehouse full name.
		if ($curName === '' || strcasecmp($curName, $code) === 0 || strcasecmp($curName, $curCode) === 0
			|| (strlen($full) > strlen($curName) && strlen($curName) <= 16)
		) {
			$newName = $full;
		}
		$newCode = $code !== '' ? $code : $curCode;
		if ($newName !== $curName || $newCode !== epc_procurement_normalize_vendor_code((string) ($row['vendor_code'] ?? ''))) {
			$upd->execute(array($newName, $newCode !== '' ? $newCode : null, (int) $row['id']));
			$updated += (int) $upd->rowCount();
		}
	}
	return array('created' => $created, 'updated' => $updated);
}

/**
 * Customer orders that used this supplier's warehouse (for purchase reconciliation).
 *
 * @return list<array<string,mixed>>
 */
function epc_procurement_supplier_order_links(PDO $db, int $supplier_id, int $limit = 40): array
{
	epc_procurement_ensure_schema($db);
	if ($supplier_id <= 0) {
		return array();
	}
	$st = $db->prepare('SELECT `storage_id` FROM `epc_erp_suppliers` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($supplier_id));
	$storageId = (int) $st->fetchColumn();
	$limit = max(1, min(100, $limit));
	$rows = array();

	if ($storageId > 0) {
		$sql = 'SELECT o.`id` AS order_id, o.`time`, o.`user_id`, o.`paid`, o.`status`,
				u.`email` AS customer_email,
				COUNT(oi.`id`) AS line_count,
				IFNULL(SUM(oi.`t2_price_purchase` * oi.`count_need`), 0) AS purchase_ex,
				IFNULL(SUM(oi.`price` * oi.`count_need`), 0) AS sale_ex,
				(SELECT COUNT(*) FROM `epc_erp_purchases` p
				 WHERE p.`active` = 1 AND p.`order_id` = o.`id` AND p.`supplier_id` = ?) AS purchase_bills
			FROM `shop_orders` o
			INNER JOIN `shop_orders_items` oi ON oi.`order_id` = o.`id` AND oi.`t2_storage_id` = ?
			LEFT JOIN `users` u ON u.`user_id` = o.`user_id`
			WHERE o.`successfully_created` = 1
			GROUP BY o.`id`, o.`time`, o.`user_id`, o.`paid`, o.`status`, u.`email`
			ORDER BY o.`time` DESC
			LIMIT ' . $limit;
		$q = $db->prepare($sql);
		$q->execute(array($supplier_id, $storageId));
		$rows = $q->fetchAll(PDO::FETCH_ASSOC);
	}

	// Also include orders linked only via purchase bills (no warehouse match).
	$billSql = 'SELECT o.`id` AS order_id, o.`time`, o.`user_id`, o.`paid`, o.`status`,
			u.`email` AS customer_email,
			0 AS line_count,
			IFNULL(SUM(p.`amount_ex_vat`), 0) AS purchase_ex,
			0 AS sale_ex,
			COUNT(p.`id`) AS purchase_bills
		FROM `epc_erp_purchases` p
		INNER JOIN `shop_orders` o ON o.`id` = p.`order_id` AND o.`successfully_created` = 1
		LEFT JOIN `users` u ON u.`user_id` = o.`user_id`
		WHERE p.`active` = 1 AND p.`supplier_id` = ? AND p.`order_id` > 0
		GROUP BY o.`id`, o.`time`, o.`user_id`, o.`paid`, o.`status`, u.`email`
		ORDER BY o.`time` DESC
		LIMIT ' . $limit;
	try {
		$bq = $db->prepare($billSql);
		$bq->execute(array($supplier_id));
		$seen = array();
		foreach ($rows as $r) {
			$seen[(int) $r['order_id']] = true;
		}
		foreach ($bq->fetchAll(PDO::FETCH_ASSOC) as $r) {
			$oid = (int) $r['order_id'];
			if (isset($seen[$oid])) {
				continue;
			}
			$rows[] = $r;
			$seen[$oid] = true;
		}
	} catch (Throwable $e) {
	}

	usort($rows, static function ($a, $b) {
		return ((int) ($b['time'] ?? 0)) <=> ((int) ($a['time'] ?? 0));
	});
	return array_slice($rows, 0, $limit);
}

function epc_procurement_list_warehouses(PDO $db): array
{
	$rows = $db->query(
		'SELECT s.*,
			(SELECT `id` FROM `epc_erp_suppliers` WHERE `storage_id` = s.`id` AND `active` = 1 LIMIT 1) AS supplier_id
		FROM `shop_storages` s ORDER BY s.`name`'
	)->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$w) {
		$w['vendor_code'] = epc_procurement_normalize_vendor_code((string) ($w['short_name'] ?? ''));
	}
	unset($w);
	return $rows;
}

function epc_procurement_update_supplier(PDO $db, int $id, array $data): void
{
	epc_procurement_ensure_schema($db);
	if ($id <= 0) {
		throw new Exception('Invalid supplier');
	}
	$country = epc_uae_vat_normalize_country($data['country_code'] ?? 'AE');
	$vatReg = !empty($data['vat_registered']) ? 1 : 0;
	$vendorCode = epc_procurement_normalize_vendor_code((string)($data['vendor_code'] ?? ($data['vendor_account'] ?? '')));
	$db->prepare(
		'UPDATE `epc_erp_suppliers` SET
		`name` = ?, `vendor_code` = ?, `contact_email` = ?, `contact_phone` = ?, `trn` = ?,
		`country_code` = ?, `vat_registered` = ?, `storage_id` = ?,
		`legal_reg_no` = ?, `legal_reg_type` = ?, `authority_name` = ?,
		`address_line1` = ?, `city` = ?, `emirate` = ?, `payment_terms` = ?, `notes` = ?
		WHERE `id` = ? AND `active` = 1'
	)->execute(array(
		trim((string)$data['name']),
		$vendorCode !== '' ? $vendorCode : null,
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
	if (!$row) {
		return null;
	}
	return epc_procurement_enrich_supplier($db, $row);
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
