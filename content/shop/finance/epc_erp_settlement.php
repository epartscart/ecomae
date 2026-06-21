<?php
defined('_ASTEXE_') or die('No access');

/**
 * AR/AP settlement & allocation engine.
 *
 * Lets a receipt voucher (RV) settle one or more open customer invoices and a
 * payment voucher (PV) settle one or more open supplier bills, with per-document
 * knock-off, partial settlement, FIFO auto-allocation, over-payment handling
 * (remainder kept on account / as an advance) and a full allocation audit trail.
 *
 * This closes the leakage where receipts/payments only moved the net party
 * balance but never reduced the underlying invoice/bill outstanding — so the
 * AR/AP aging never aged the documents out even after they were paid.
 *
 * Accounting stays clean: the cash entry GL posts the real movement
 * (receipt: Dr Bank / Cr AR; payment: Dr AP / Cr Bank); the allocation rows are
 * sub-ledger detail that knock the invoice/bill outstanding down.
 */

function epc_erp_settlement_admin_id(): int
{
	if (function_exists('epc_erp_admin_id')) {
		return (int) epc_erp_admin_id();
	}
	return 0;
}

function epc_erp_settlement_ensure_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_settlement_allocations` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`doc_type` enum('ar','ap') NOT NULL,
		`cash_entry_id` int(11) NOT NULL DEFAULT 0,
		`voucher_no` varchar(64) DEFAULT NULL,
		`counterparty_id` int(11) NOT NULL DEFAULT 0,
		`invoice_id` int(11) NOT NULL DEFAULT 0,
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`time` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_cash` (`cash_entry_id`),
		KEY `x_doc` (`doc_type`,`invoice_id`),
		KEY `x_party` (`doc_type`,`counterparty_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP receipt/payment to invoice/bill allocations';");
}

/** Open AR invoices for a customer (outstanding > 0), oldest due first. */
function epc_erp_open_customer_invoices(PDO $db, int $userId): array
{
	if ($userId <= 0) {
		return array();
	}
	$has = $db->query("SHOW TABLES LIKE 'epc_einvoice_documents'")->fetchColumn();
	if (!$has) {
		return array();
	}
	$st = $db->prepare(
		"SELECT `id`, `invoice_number`, `issue_date`, `payment_due_date`,
			`total_incl_vat`, `paid_amount`,
			ROUND(`total_incl_vat` - `paid_amount`, 2) AS outstanding
		FROM `epc_einvoice_documents`
		WHERE `active` = 1 AND `status` <> 'cancelled'
		  AND `doc_category` IN ('tax_invoice','commercial_invoice')
		  AND `user_id` = ?
		  AND ROUND(`total_incl_vat` - `paid_amount`, 2) > 0.005
		ORDER BY (CASE WHEN `payment_due_date` > 0 THEN `payment_due_date` ELSE `issue_date` END) ASC, `id` ASC"
	);
	$st->execute(array($userId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

/** Open AP bills for a supplier (outstanding > 0), oldest first. */
function epc_erp_open_supplier_bills(PDO $db, int $supplierId): array
{
	if ($supplierId <= 0) {
		return array();
	}
	$has = $db->query("SHOW TABLES LIKE 'epc_erp_purchases'")->fetchColumn();
	if (!$has) {
		return array();
	}
	$st = $db->prepare(
		"SELECT p.`id`, p.`invoice_number`, p.`purchase_date`, p.`total_amount`,
			IFNULL((SELECT SUM(a.`amount`) FROM `epc_erp_supplier_accounting` a
				WHERE a.`purchase_id` = p.`id` AND a.`active` = 1 AND a.`is_credit` = 0), 0) AS paid,
			ROUND(p.`total_amount` - IFNULL((SELECT SUM(a.`amount`) FROM `epc_erp_supplier_accounting` a
				WHERE a.`purchase_id` = p.`id` AND a.`active` = 1 AND a.`is_credit` = 0), 0), 2) AS outstanding
		FROM `epc_erp_purchases` p
		WHERE p.`active` = 1 AND p.`status` <> 'draft' AND p.`supplier_id` = ?
		HAVING outstanding > 0.005
		ORDER BY p.`purchase_date` ASC, p.`id` ASC"
	);
	$st->execute(array($supplierId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

/**
 * Normalise allocation input from POST into [invoice_id => amount].
 * Accepts parallel arrays alloc_invoice_id[] + alloc_amount[].
 */
function epc_erp_settlement_parse_allocations(array $post): array
{
	$out = array();
	if (isset($post['alloc_invoice_id']) && is_array($post['alloc_invoice_id'])) {
		$ids = array_values($post['alloc_invoice_id']);
		$ams = (isset($post['alloc_amount']) && is_array($post['alloc_amount'])) ? array_values($post['alloc_amount']) : array();
		foreach ($ids as $i => $rawId) {
			$id = (int) $rawId;
			$am = round((float) ($ams[$i] ?? 0), 2);
			if ($id > 0 && $am > 0.005) {
				$out[$id] = round(($out[$id] ?? 0) + $am, 2);
			}
		}
	}
	return $out;
}

/** Build a FIFO allocation [id => amount] for $amount across open docs (oldest first). */
function epc_erp_settlement_fifo(array $openDocs, float $amount): array
{
	$alloc = array();
	$left = round($amount, 2);
	foreach ($openDocs as $d) {
		if ($left <= 0.005) {
			break;
		}
		$out = round((float) $d['outstanding'], 2);
		$take = min($out, $left);
		if ($take > 0.005) {
			$alloc[(int) $d['id']] = $take;
			$left = round($left - $take, 2);
		}
	}
	return $alloc;
}

/**
 * Apply receipt allocations: knock off customer invoices (paid_amount up),
 * write allocation rows. Caps each line at the invoice outstanding and the
 * whole run at $cap (the cash received). Returns total amount actually applied.
 */
function epc_erp_apply_receipt_allocations(PDO $db, int $cashEntryId, string $voucherNo, int $userId, array $alloc, int $time, float $cap): float
{
	epc_erp_settlement_ensure_schema($db);
	$total = 0.0;
	$capLeft = round($cap, 2);
	$ins = $db->prepare(
		"INSERT INTO `epc_erp_settlement_allocations`
		(`doc_type`,`cash_entry_id`,`voucher_no`,`counterparty_id`,`invoice_id`,`amount`,`time`,`admin_id`)
		VALUES ('ar',?,?,?,?,?,?,?)"
	);
	foreach ($alloc as $invId => $amt) {
		$invId = (int) $invId;
		$amt = round((float) $amt, 2);
		if ($invId <= 0 || $amt <= 0.005 || $capLeft <= 0.005) {
			continue;
		}
		$row = $db->prepare(
			"SELECT `total_incl_vat`, `paid_amount` FROM `epc_einvoice_documents`
			WHERE `id` = ? AND `user_id` = ? AND `active` = 1 LIMIT 1"
		);
		$row->execute(array($invId, $userId));
		$doc = $row->fetch(PDO::FETCH_ASSOC);
		if (!$doc) {
			continue;
		}
		$out = round((float) $doc['total_incl_vat'] - (float) $doc['paid_amount'], 2);
		if ($out <= 0.005) {
			continue;
		}
		$apply = min($amt, $out, $capLeft);
		if ($apply <= 0.005) {
			continue;
		}
		$db->prepare(
			"UPDATE `epc_einvoice_documents`
			SET `paid_amount` = ROUND(`paid_amount` + ?, 2),
				`amount_due` = ROUND(`total_incl_vat` - (`paid_amount` + ?), 2),
				`time_updated` = ?
			WHERE `id` = ?"
		)->execute(array($apply, $apply, $time, $invId));
		$ins->execute(array($cashEntryId, $voucherNo, $userId, $invId, $apply, $time, epc_erp_settlement_admin_id()));
		$total = round($total + $apply, 2);
		$capLeft = round($capLeft - $apply, 2);
	}
	return $total;
}

/**
 * Apply payment allocations: knock off supplier bills (supplier ledger payment
 * rows tagged with purchase_id), write allocation rows, update bill status.
 * Caps each line at the bill outstanding and the whole run at $cap (cash paid).
 */
function epc_erp_apply_payment_allocations(PDO $db, int $cashEntryId, string $voucherNo, int $supplierId, array $alloc, int $time, float $cap): float
{
	epc_erp_settlement_ensure_schema($db);
	$total = 0.0;
	$capLeft = round($cap, 2);
	$insAcc = $db->prepare(
		"INSERT INTO `epc_erp_supplier_accounting`
		(`supplier_id`,`time`,`is_credit`,`amount`,`purchase_id`,`cash_entry_id`,`reference`,`note`,`admin_id`,`entry_kind`)
		VALUES (?,?,0,?,?,?,?,?,?, 'payment')"
	);
	$insAlloc = $db->prepare(
		"INSERT INTO `epc_erp_settlement_allocations`
		(`doc_type`,`cash_entry_id`,`voucher_no`,`counterparty_id`,`invoice_id`,`amount`,`time`,`admin_id`)
		VALUES ('ap',?,?,?,?,?,?,?)"
	);
	foreach ($alloc as $billId => $amt) {
		$billId = (int) $billId;
		$amt = round((float) $amt, 2);
		if ($billId <= 0 || $amt <= 0.005 || $capLeft <= 0.005) {
			continue;
		}
		$row = $db->prepare(
			"SELECT p.`total_amount`,
				IFNULL((SELECT SUM(a.`amount`) FROM `epc_erp_supplier_accounting` a
					WHERE a.`purchase_id` = p.`id` AND a.`active` = 1 AND a.`is_credit` = 0), 0) AS paid
			FROM `epc_erp_purchases` p
			WHERE p.`id` = ? AND p.`supplier_id` = ? AND p.`active` = 1 LIMIT 1"
		);
		$row->execute(array($billId, $supplierId));
		$bill = $row->fetch(PDO::FETCH_ASSOC);
		if (!$bill) {
			continue;
		}
		$out = round((float) $bill['total_amount'] - (float) $bill['paid'], 2);
		if ($out <= 0.005) {
			continue;
		}
		$apply = min($amt, $out, $capLeft);
		if ($apply <= 0.005) {
			continue;
		}
		$insAcc->execute(array(
			$supplierId, $time, $apply, $billId, $cashEntryId, $voucherNo,
			'Bill settlement ' . $voucherNo, epc_erp_settlement_admin_id(),
		));
		$insAlloc->execute(array($cashEntryId, $voucherNo, $supplierId, $billId, $apply, $time, epc_erp_settlement_admin_id()));
		$newPaid = round((float) $bill['paid'] + $apply, 2);
		$status = ($newPaid + 0.005 >= (float) $bill['total_amount']) ? 'paid' : 'partial';
		$db->prepare("UPDATE `epc_erp_purchases` SET `status` = ? WHERE `id` = ?")->execute(array($status, $billId));
		$total = round($total + $apply, 2);
		$capLeft = round($capLeft - $apply, 2);
	}
	return $total;
}
