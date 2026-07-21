<?php
/**
 * ERP document lifecycle — Edit / Delete / Void (international practice).
 *
 * Rules (IFRS / GAAP-aligned audit trail):
 *  - Draft / unposted: Edit + hard Delete allowed.
 *  - Posted / confirmed: never hard-delete; Void creates reversing GL and soft-flags
 *    the source document (active=0, status=voided) while keeping the voucher number.
 *  - Posted narrative (reference / note): Amend only — amounts stay locked.
 *  - Tax invoices that are submitted/accepted: Credit note (existing), not void.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';
require_once __DIR__ . '/epc_erp_schema.php';

function epc_erp_doc_lifecycle_ensure_schema(PDO $db): void
{
	static $done = array();
	$oid = spl_object_id($db);
	if (isset($done[$oid])) {
		return;
	}
	$done[$oid] = true;

	epc_erp_ensure_schema($db);
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'voided_at', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'void_reason', 'varchar(255) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'voided_by', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'reversal_journal_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'voided_at', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'void_reason', 'varchar(255) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'voided_by', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'reversal_journal_id', 'int(11) NOT NULL DEFAULT 0');
	try {
		$db->exec(
			"ALTER TABLE `epc_erp_purchases`
			 MODIFY `status` enum('draft','confirmed','paid','partial','voided') NOT NULL DEFAULT 'confirmed'"
		);
	} catch (Exception $e) {
	}
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_gl_journals', 'reversed_by_journal_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_settlement_allocations', 'active', 'tinyint(1) NOT NULL DEFAULT 1');
}

/**
 * Can the document be hard-deleted?
 *
 * @param array<string,mixed> $row
 */
function epc_erp_doc_can_delete(string $type, array $row): bool
{
	$type = strtolower($type);
	if ($type === 'cash' || $type === 'voucher' || $type === 'rv' || $type === 'pv' || $type === 'tv') {
		// Cash vouchers post immediately — never hard-delete.
		return false;
	}
	if ($type === 'purchase' || $type === 'pi') {
		$status = (string) ($row['status'] ?? '');
		$gl = (int) ($row['gl_journal_id'] ?? 0);
		return $status === 'draft' && $gl <= 0 && (int) ($row['active'] ?? 1) === 1;
	}
	if ($type === 'sales_order' || $type === 'so') {
		return (string) ($row['status'] ?? '') === 'draft';
	}
	if ($type === 'purchase_order' || $type === 'po') {
		return (string) ($row['status'] ?? '') === 'draft';
	}
	if ($type === 'invoice' || $type === 'si') {
		$status = (string) ($row['status'] ?? '');
		return in_array($status, array('draft', 'validated', 'rejected'), true)
			&& !in_array($status, array('submitted', 'accepted', 'queued'), true);
	}
	return false;
}

/**
 * Can the document be voided (posted → reversing entry)?
 *
 * @param array<string,mixed> $row
 */
function epc_erp_doc_can_void(string $type, array $row): bool
{
	if ((int) ($row['active'] ?? 1) !== 1) {
		return false;
	}
	if (!empty($row['voided_at'])) {
		return false;
	}
	$type = strtolower($type);
	if ($type === 'cash' || $type === 'voucher' || $type === 'rv' || $type === 'pv' || $type === 'tv') {
		return true;
	}
	if ($type === 'purchase' || $type === 'pi') {
		$status = (string) ($row['status'] ?? '');
		return in_array($status, array('confirmed', 'paid', 'partial', 'draft'), true);
	}
	if ($type === 'sales_order' || $type === 'so') {
		return in_array((string) ($row['status'] ?? ''), array('draft', 'confirmed'), true);
	}
	if ($type === 'purchase_order' || $type === 'po') {
		return in_array((string) ($row['status'] ?? ''), array('draft', 'approved', 'partial', 'received'), true);
	}
	if ($type === 'invoice' || $type === 'si') {
		// Submitted e-invoices must use credit note; draft/validated may cancel.
		return in_array((string) ($row['status'] ?? ''), array('draft', 'validated', 'rejected'), true);
	}
	return false;
}

/**
 * Can amounts/header be fully edited?
 *
 * @param array<string,mixed> $row
 */
function epc_erp_doc_can_edit(string $type, array $row): bool
{
	$type = strtolower($type);
	if ((int) ($row['active'] ?? 1) !== 1 || !empty($row['voided_at'])) {
		return false;
	}
	if ($type === 'purchase' || $type === 'pi') {
		return (string) ($row['status'] ?? '') === 'draft' && (int) ($row['gl_journal_id'] ?? 0) <= 0;
	}
	if ($type === 'sales_order' || $type === 'so') {
		return in_array((string) ($row['status'] ?? ''), array('draft', 'confirmed'), true)
			&& (int) ($row['sales_invoice_id'] ?? 0) <= 0;
	}
	if ($type === 'purchase_order' || $type === 'po') {
		return in_array((string) ($row['status'] ?? ''), array('draft'), true);
	}
	if ($type === 'invoice' || $type === 'si') {
		return !in_array((string) ($row['status'] ?? ''), array('submitted', 'accepted', 'queued', 'cancelled'), true);
	}
	// Posted cash vouchers: narrative amend only (not full edit).
	return false;
}

/** Posted vouchers may amend reference/note only. */
function epc_erp_doc_can_amend(string $type, array $row): bool
{
	$type = strtolower($type);
	if ((int) ($row['active'] ?? 1) !== 1 || !empty($row['voided_at'])) {
		return false;
	}
	if (in_array($type, array('cash', 'voucher', 'rv', 'pv', 'tv', 'purchase', 'pi'), true)) {
		return true;
	}
	return false;
}

/**
 * Reverse linked GL journal(s) and stamp reversed_by on the original.
 *
 * @return array<int,int> new reversing journal ids
 */
function epc_erp_doc_reverse_journals(PDO $db, array $journalIds, string $note = ''): array
{
	require_once __DIR__ . '/epc_erp_gl.php';
	$out = array();
	$journalIds = array_values(array_unique(array_filter(array_map('intval', $journalIds))));
	foreach ($journalIds as $jid) {
		if ($jid <= 0) {
			continue;
		}
		// Skip if already reversed.
		try {
			$chk = $db->prepare('SELECT `reversed_by_journal_id` FROM `epc_erp_gl_journals` WHERE `id` = ? LIMIT 1');
			$chk->execute(array($jid));
			$already = (int) $chk->fetchColumn();
			if ($already > 0) {
				$out[] = $already;
				continue;
			}
		} catch (Exception $e) {
		}
		$revId = (int) epc_erp_gl_reverse_journal($db, $jid, time(), $note !== '' ? $note : 'Document void');
		if ($revId > 0) {
			try {
				$db->prepare('UPDATE `epc_erp_gl_journals` SET `reversed_by_journal_id` = ? WHERE `id` = ?')
					->execute(array($revId, $jid));
			} catch (Exception $e) {
			}
			$out[] = $revId;
		}
	}
	return $out;
}

/**
 * Undo AR/AP settlement allocations for a cash voucher being voided.
 */
function epc_erp_doc_unwind_settlements(PDO $db, int $cashEntryId): void
{
	if ($cashEntryId <= 0) {
		return;
	}
	require_once __DIR__ . '/epc_erp_settlement.php';
	epc_erp_settlement_ensure_schema($db);
	$st = $db->prepare(
		'SELECT * FROM `epc_erp_settlement_allocations`
		 WHERE `cash_entry_id` = ? AND `active` = 1'
	);
	$st->execute(array($cashEntryId));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as $row) {
		$amt = round((float) ($row['amount'] ?? 0), 2);
		$invId = (int) ($row['invoice_id'] ?? 0);
		$docType = (string) ($row['doc_type'] ?? '');
		if ($amt > 0.005 && $invId > 0 && $docType === 'ar') {
			try {
				$db->prepare(
					"UPDATE `epc_einvoice_documents`
					 SET `paid_amount` = GREATEST(0, ROUND(`paid_amount` - ?, 2)),
						 `amount_due` = ROUND(`total_incl_vat` - GREATEST(0, `paid_amount` - ?), 2),
						 `time_updated` = ?
					 WHERE `id` = ?"
				)->execute(array($amt, $amt, time(), $invId));
			} catch (Exception $e) {
			}
		}
		$db->prepare('UPDATE `epc_erp_settlement_allocations` SET `active` = 0 WHERE `id` = ?')
			->execute(array((int) $row['id']));
	}
	// Soft-deactivate supplier ledger rows tied to this cash entry.
	try {
		$db->prepare('UPDATE `epc_erp_supplier_accounting` SET `active` = 0 WHERE `cash_entry_id` = ? AND `active` = 1')
			->execute(array($cashEntryId));
	} catch (Exception $e) {
	}
	try {
		$db->prepare('UPDATE `shop_users_accounting` SET `active` = 0 WHERE `cash_entry_id` = ? AND `active` = 1')
			->execute(array($cashEntryId));
	} catch (Exception $e) {
	}
}

/**
 * Void a posted cash / bank voucher (RV / PV / TV / manual entry).
 *
 * @return array{ok:bool,reversal_journal_ids:array<int,int>,voided_ids:array<int,int>}
 */
function epc_erp_cash_voucher_void(PDO $db, int $entryId, string $reason = ''): array
{
	epc_erp_doc_lifecycle_ensure_schema($db);
	$entryId = (int) $entryId;
	$st = $db->prepare('SELECT * FROM `epc_erp_cash_bank_entries` WHERE `id` = ? LIMIT 1');
	$st->execute(array($entryId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Voucher / cash entry not found');
	}
	if (!epc_erp_doc_can_void('cash', $row)) {
		throw new Exception('This voucher cannot be voided (already voided or inactive)');
	}
	$reason = trim($reason);
	if ($reason === '') {
		$reason = 'Voided by operator';
	}
	$ids = array($entryId);
	$pair = (int) ($row['transfer_pair_id'] ?? 0);
	if ($pair > 0) {
		$ids[] = $pair;
	}
	$ids = array_values(array_unique($ids));

	$journals = array();
	foreach ($ids as $id) {
		$q = $db->prepare('SELECT `gl_journal_id` FROM `epc_erp_cash_bank_entries` WHERE `id` = ? LIMIT 1');
		$q->execute(array($id));
		$jid = (int) $q->fetchColumn();
		if ($jid > 0) {
			$journals[] = $jid;
		} else {
			// Fallback: find by source.
			try {
				$jq = $db->prepare(
					"SELECT `id` FROM `epc_erp_gl_journals`
					 WHERE `source_type` = 'cash' AND `source_id` = ? AND `active` = 1
					   AND COALESCE(`reversed_by_journal_id`,0) = 0
					 LIMIT 1"
				);
				$jq->execute(array($id));
				$found = (int) $jq->fetchColumn();
				if ($found > 0) {
					$journals[] = $found;
				}
			} catch (Exception $e) {
			}
		}
	}

	$revIds = epc_erp_doc_reverse_journals(
		$db,
		$journals,
		'Void ' . trim((string) ($row['voucher_no'] ?? $row['reference'] ?? ('#' . $entryId))) . ' — ' . $reason
	);
	$primaryRev = $revIds[0] ?? 0;
	$adminId = function_exists('epc_erp_admin_id') ? (int) epc_erp_admin_id() : 0;
	$now = time();

	foreach ($ids as $id) {
		epc_erp_doc_unwind_settlements($db, $id);
		$db->prepare(
			'UPDATE `epc_erp_cash_bank_entries`
			 SET `active` = 0, `voided_at` = ?, `void_reason` = ?, `voided_by` = ?, `reversal_journal_id` = ?
			 WHERE `id` = ?'
		)->execute(array($now, substr($reason, 0, 255), $adminId, $primaryRev, $id));
	}

	if (function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'void', 'cash_entry', $entryId, $reason, array(
			'voided_ids' => $ids,
			'reversal_journal_ids' => $revIds,
		));
	}

	return array(
		'ok' => true,
		'reversal_journal_ids' => $revIds,
		'voided_ids' => $ids,
	);
}

/**
 * Amend narrative fields on a posted (non-voided) cash voucher.
 * Amounts, accounts, and directions stay locked.
 */
function epc_erp_cash_voucher_amend(PDO $db, int $entryId, array $data): bool
{
	epc_erp_doc_lifecycle_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_cash_bank_entries` WHERE `id` = ? LIMIT 1');
	$st->execute(array($entryId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row || !epc_erp_doc_can_amend('cash', $row)) {
		throw new Exception('Voucher cannot be amended');
	}
	$reference = array_key_exists('reference', $data)
		? trim((string) $data['reference'])
		: (string) ($row['reference'] ?? '');
	$note = array_key_exists('note', $data)
		? trim((string) $data['note'])
		: (string) ($row['note'] ?? '');
	$db->prepare('UPDATE `epc_erp_cash_bank_entries` SET `reference` = ?, `note` = ? WHERE `id` = ? AND `active` = 1')
		->execute(array($reference, $note, $entryId));
	if (function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'amend', 'cash_entry', $entryId, 'Narrative amended', array(),
			array('reference' => $row['reference'] ?? '', 'note' => $row['note'] ?? ''),
			array('reference' => $reference, 'note' => $note)
		);
	}
	return true;
}

/**
 * Void a purchase invoice: reverse GL + soft-deactivate payable ledger.
 */
function epc_erp_purchase_void(PDO $db, int $purchaseId, string $reason = ''): array
{
	epc_erp_doc_lifecycle_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1');
	$st->execute(array($purchaseId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Purchase invoice not found');
	}
	if (!epc_erp_doc_can_void('purchase', $row)) {
		throw new Exception('Purchase invoice cannot be voided');
	}
	$reason = trim($reason) !== '' ? trim($reason) : 'Voided by operator';

	$journals = array();
	$jid = (int) ($row['gl_journal_id'] ?? 0);
	if ($jid > 0) {
		$journals[] = $jid;
	} else {
		try {
			$jq = $db->prepare(
				"SELECT `id` FROM `epc_erp_gl_journals`
				 WHERE `source_type` = 'purchase' AND `source_id` = ? AND `active` = 1
				   AND COALESCE(`reversed_by_journal_id`,0) = 0 LIMIT 1"
			);
			$jq->execute(array($purchaseId));
			$found = (int) $jq->fetchColumn();
			if ($found > 0) {
				$journals[] = $found;
			}
		} catch (Exception $e) {
		}
	}

	$revIds = epc_erp_doc_reverse_journals(
		$db,
		$journals,
		'Void PI ' . trim((string) ($row['invoice_number'] ?? $row['voucher_no'] ?? ('#' . $purchaseId))) . ' — ' . $reason
	);
	$adminId = function_exists('epc_erp_admin_id') ? (int) epc_erp_admin_id() : 0;
	$now = time();

	$db->prepare(
		'UPDATE `epc_erp_supplier_accounting` SET `active` = 0
		 WHERE `purchase_id` = ? AND `active` = 1'
	)->execute(array($purchaseId));

	$db->prepare(
		"UPDATE `epc_erp_purchases`
		 SET `active` = 0, `status` = 'voided', `voided_at` = ?, `void_reason` = ?, `voided_by` = ?,
			 `reversal_journal_id` = ?
		 WHERE `id` = ?"
	)->execute(array($now, substr($reason, 0, 255), $adminId, $revIds[0] ?? 0, $purchaseId));

	if (function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'void', 'purchase', $purchaseId, $reason, array('reversal_journal_ids' => $revIds));
	}

	return array('ok' => true, 'reversal_journal_ids' => $revIds);
}

/** Hard-delete a draft purchase (no GL, never posted). */
function epc_erp_purchase_delete(PDO $db, int $purchaseId): bool
{
	epc_erp_doc_lifecycle_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1');
	$st->execute(array($purchaseId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Purchase invoice not found');
	}
	if (!epc_erp_doc_can_delete('purchase', $row)) {
		throw new Exception('Only draft unposted purchases can be deleted — use Void for posted invoices');
	}
	$db->prepare('DELETE FROM `epc_erp_supplier_accounting` WHERE `purchase_id` = ?')->execute(array($purchaseId));
	$db->prepare('DELETE FROM `epc_erp_purchases` WHERE `id` = ?')->execute(array($purchaseId));
	if (function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'delete', 'purchase', $purchaseId, 'Draft purchase deleted');
	}
	return true;
}

/** Amend purchase note/reference (posted) or full amounts (draft only). */
function epc_erp_purchase_amend(PDO $db, int $purchaseId, array $data): bool
{
	epc_erp_doc_lifecycle_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1');
	$st->execute(array($purchaseId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row || (int) ($row['active'] ?? 1) !== 1 || !empty($row['voided_at'])) {
		throw new Exception('Purchase cannot be amended');
	}
	$note = array_key_exists('note', $data) ? trim((string) $data['note']) : (string) ($row['note'] ?? '');
	$invNo = array_key_exists('invoice_number', $data)
		? trim((string) $data['invoice_number'])
		: (string) ($row['invoice_number'] ?? '');

	if (epc_erp_doc_can_edit('purchase', $row) && isset($data['amount_ex_vat'])) {
		// Draft full edit — recalculate VAT via tax toolkit.
		require_once __DIR__ . '/epc_tax_toolkit.php';
		$amountEx = round((float) $data['amount_ex_vat'], 2);
		$vatCalc = epc_tax_toolkit_purchase_amounts($db, $amountEx, (int) $row['supplier_id'], array());
		$vat = round((float) $vatCalc['vat_amount'], 2);
		$total = round((float) $vatCalc['total_amount'], 2);
		$db->prepare(
			'UPDATE `epc_erp_purchases`
			 SET `invoice_number` = ?, `amount_ex_vat` = ?, `vat_amount` = ?, `total_amount` = ?,
				 `vat_rate` = ?, `note` = ?
			 WHERE `id` = ? AND `status` = \'draft\''
		)->execute(array(
			$invNo, $amountEx, $vat, $total, (float) $vatCalc['vat_rate'], $note, $purchaseId,
		));
	} else {
		if (!epc_erp_doc_can_amend('purchase', $row)) {
			throw new Exception('Purchase cannot be amended');
		}
		$db->prepare('UPDATE `epc_erp_purchases` SET `invoice_number` = ?, `note` = ? WHERE `id` = ? AND `active` = 1')
			->execute(array($invNo, $note, $purchaseId));
	}
	if (function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'amend', 'purchase', $purchaseId, 'Purchase amended');
	}
	return true;
}

/** Delete draft purchase order. */
function epc_erp_po_delete(PDO $db, int $poId): bool
{
	require_once __DIR__ . '/epc_erp_extended.php';
	epc_erp_extended_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($poId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Purchase order not found');
	}
	if (!epc_erp_doc_can_delete('po', $row)) {
		throw new Exception('Only draft purchase orders can be deleted — cancel posted ones instead');
	}
	$db->beginTransaction();
	try {
		$db->prepare('DELETE FROM `epc_erp_po_lines` WHERE `po_id` = ?')->execute(array($poId));
		try {
			$db->prepare('DELETE FROM `epc_erp_po_receipts` WHERE `po_id` = ?')->execute(array($poId));
		} catch (Exception $e) {
		}
		$db->prepare('DELETE FROM `epc_erp_purchase_orders` WHERE `id` = ?')->execute(array($poId));
		$db->commit();
	} catch (Exception $e) {
		if ($db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
	if (function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'delete', 'purchase_order', $poId, 'Draft PO deleted');
	}
	return true;
}

/** Cancel (void-equivalent) a sales order that is not yet invoiced. */
function epc_erp_sales_order_cancel(PDO $db, int $soId, string $reason = ''): bool
{
	require_once __DIR__ . '/epc_erp_vouchers.php';
	epc_erp_vouchers_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1');
	$st->execute(array($soId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Sales order not found');
	}
	if (!epc_erp_doc_can_void('so', $row)) {
		throw new Exception('Sales order cannot be cancelled');
	}
	if ((int) ($row['sales_invoice_id'] ?? 0) > 0 || (string) ($row['status'] ?? '') === 'invoiced') {
		throw new Exception('Invoiced sales orders cannot be cancelled — issue a credit note on the invoice');
	}
	epc_erp_sales_order_set_status($db, $soId, 'cancelled');
	if ($reason !== '' && function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'cancel', 'sales_order', $soId, $reason);
	}
	return true;
}

/** Soft-cancel a draft/validated sales tax invoice (not yet submitted). */
function epc_erp_invoice_cancel(PDO $db, int $invoiceId, string $reason = ''): bool
{
	require_once __DIR__ . '/epc_erp_invoices.php';
	epc_erp_invoices_ensure_schema($db);
	$doc = function_exists('epc_einvoice_get_document') ? epc_einvoice_get_document($db, $invoiceId) : null;
	if (!$doc) {
		throw new Exception('Invoice not found');
	}
	if (!epc_erp_doc_can_void('invoice', $doc)) {
		throw new Exception('Submitted invoices cannot be cancelled — issue a credit note instead');
	}
	$db->prepare(
		"UPDATE `epc_einvoice_documents`
		 SET `status` = 'cancelled', `active` = 0, `time_updated` = ?
		 WHERE `id` = ? AND `status` NOT IN ('submitted','accepted','queued')"
	)->execute(array(time(), $invoiceId));
	if (function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'cancel', 'invoice', $invoiceId, $reason !== '' ? $reason : 'Invoice cancelled');
	}
	return true;
}

/** Hard-delete a draft invoice that was never validated/submitted. */
function epc_erp_invoice_delete(PDO $db, int $invoiceId): bool
{
	require_once __DIR__ . '/epc_erp_invoices.php';
	epc_erp_invoices_ensure_schema($db);
	$doc = function_exists('epc_einvoice_get_document') ? epc_einvoice_get_document($db, $invoiceId) : null;
	if (!$doc) {
		throw new Exception('Invoice not found');
	}
	if ((string) ($doc['status'] ?? '') !== 'draft') {
		throw new Exception('Only draft invoices can be deleted — cancel or credit-note others');
	}
	$db->prepare('DELETE FROM `epc_einvoice_lines` WHERE `document_id` = ?')->execute(array($invoiceId));
	$db->prepare('DELETE FROM `epc_einvoice_documents` WHERE `id` = ? AND `status` = \'draft\'')->execute(array($invoiceId));
	if (function_exists('epc_erp_audit_log')) {
		require_once __DIR__ . '/epc_erp_audit.php';
		epc_erp_audit_log($db, 'delete', 'invoice', $invoiceId, 'Draft invoice deleted');
	}
	return true;
}

/**
 * Render compact action buttons for list rows (server-side HTML).
 *
 * @param array<string,mixed> $opts
 */
function epc_erp_doc_actions_html(string $type, array $row, string $csrf, array $opts = array()): string
{
	$id = (int) ($row['id'] ?? 0);
	if ($id <= 0) {
		return '';
	}
	$h = static function ($v): string {
		return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
	};
	$bits = array();
	$viewUrl = (string) ($opts['view_url'] ?? '');
	$editUrl = (string) ($opts['edit_url'] ?? '');

	if ($viewUrl !== '') {
		$bits[] = '<a class="btn btn-default btn-xs" href="' . $h($viewUrl) . '">View</a>';
	}
	if ($editUrl !== '' && epc_erp_doc_can_edit($type, $row)) {
		$bits[] = '<a class="btn btn-default btn-xs" href="' . $h($editUrl) . '"><i class="fa fa-pencil"></i> Edit</a>';
	}

	$idField = (string) ($opts['id_field'] ?? 'id');
	if (epc_erp_doc_can_amend($type, $row) && empty($opts['hide_amend']) && in_array(strtolower($type), array('cash', 'voucher', 'rv', 'pv', 'tv', 'purchase', 'pi'), true)) {
		$action = in_array(strtolower($type), array('purchase', 'pi'), true) ? 'purchase_amend' : 'cash_voucher_amend';
		$bits[] = '<button type="button" class="btn btn-default btn-xs epc-erp-doc-amend" data-action="' . $h($action) . '" data-id-field="' . $h($idField) . '" data-id="' . $id . '" title="Amend reference / note"><i class="fa fa-pencil"></i> Edit</button>';
	}
	if (epc_erp_doc_can_delete($type, $row)) {
		$delAction = (string) ($opts['delete_action'] ?? '');
		if ($delAction === '') {
			$map = array(
				'purchase' => 'purchase_delete', 'pi' => 'purchase_delete',
				'so' => 'so_delete', 'sales_order' => 'so_delete',
				'po' => 'po_delete', 'purchase_order' => 'po_delete',
				'invoice' => 'invoice_delete', 'si' => 'invoice_delete',
			);
			$delAction = $map[strtolower($type)] ?? '';
		}
		if ($delAction !== '') {
			$bits[] = '<button type="button" class="btn btn-danger btn-xs epc-erp-doc-delete" data-action="' . $h($delAction) . '" data-id-field="' . $h($idField) . '" data-id="' . $id . '"><i class="fa fa-trash"></i> Delete</button>';
		}
	}
	if (epc_erp_doc_can_void($type, $row)) {
		$voidAction = (string) ($opts['void_action'] ?? '');
		if ($voidAction === '') {
			$map = array(
				'cash' => 'cash_voucher_void', 'voucher' => 'cash_voucher_void',
				'rv' => 'cash_voucher_void', 'pv' => 'cash_voucher_void', 'tv' => 'cash_voucher_void',
				'purchase' => 'purchase_void', 'pi' => 'purchase_void',
				'so' => 'so_cancel', 'sales_order' => 'so_cancel',
				'po' => 'po_status', 'purchase_order' => 'po_status',
				'invoice' => 'invoice_cancel', 'si' => 'invoice_cancel',
			);
			$voidAction = $map[strtolower($type)] ?? '';
		}
		$label = in_array(strtolower($type), array('so', 'sales_order', 'po', 'purchase_order', 'invoice', 'si'), true)
			? 'Cancel' : 'Void';
		$extra = '';
		if ($voidAction === 'po_status') {
			$extra = ' data-status="cancelled"';
		}
		if ($voidAction !== '') {
			$bits[] = '<button type="button" class="btn btn-warning btn-xs epc-erp-doc-void" data-action="' . $h($voidAction) . '" data-id-field="' . $h($idField) . '" data-id="' . $id . '"' . $extra . ' title="Void / cancel with audit trail"><i class="fa fa-ban"></i> ' . $h($label) . '</button>';
		}
	}
	return $bits ? '<span class="epc-erp-doc-actions" style="white-space:nowrap;">' . implode(' ', $bits) . '</span>' : '';
}
