<?php
/**
 * UAE advance payments — customer receipts & supplier payments, statement summaries, GL.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_schema.php';
require_once __DIR__ . '/epc_erp_gl.php';
require_once __DIR__ . '/epc_uae_tax_compliance.php';

function epc_erp_advances_ensure_schema(PDO $db): void
{
	epc_erp_ensure_schema($db);
	epc_erp_gl_ensure_schema($db);
	epc_uae_tax_compliance_ensure_schema($db);

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'is_advance', 'tinyint(1) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'purchase_order_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_uae_vat_advance', 'cash_entry_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_uae_vat_advance', 'sales_order_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_uae_vat_advance', 'source_type', "varchar(16) NOT NULL DEFAULT 'order'");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_uae_vat_supplier_advance` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`supplier_id` int(11) NOT NULL DEFAULT 0,
		`cash_entry_id` int(11) NOT NULL DEFAULT 0,
		`purchase_order_id` int(11) NOT NULL DEFAULT 0,
		`payment_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_rate` decimal(5,2) NOT NULL DEFAULT 5.00,
		`payment_time` int(11) NOT NULL DEFAULT 0,
		`purchase_id` int(11) NOT NULL DEFAULT 0,
		`adjusted` tinyint(1) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_cash` (`cash_entry_id`),
		KEY `x_supplier` (`supplier_id`,`adjusted`),
		KEY `x_time` (`payment_time`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE VAT on supplier advance payments';");

	epc_erp_advances_seed_coa($db);
}

function epc_erp_advances_seed_coa(PDO $db): void
{
	$now = time();
	$rows = array(
		array('2050', 'Customer advances received', 'liability', 'credit', 'UAE customer prepayments until invoiced'),
		array('2060', 'Supplier advance payments', 'asset', 'debit', 'UAE supplier prepayments until purchase invoice'),
	);
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_coa_accounts`
		(`code`, `name`, `account_type`, `normal_side`, `description`, `system_flag`, `time_created`)
		SELECT ?, ?, ?, ?, ?, 1, ?
		FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1)'
	);
	foreach ($rows as $r) {
		$ins->execute(array($r[0], $r[1], $r[2], $r[3], $r[4], $now, $r[0]));
	}
}

/** Customer statement summary: advance, open SO, invoiced, net receivable. */
function epc_erp_customer_statement_summary(PDO $db, int $user_id, int $from = 0, int $to = 0): array
{
	epc_erp_advances_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_vouchers.php';
	epc_erp_vouchers_ensure_schema($db);
	$user_id = (int) $user_id;
	if ($user_id <= 0) {
		return array();
	}
	if ($to <= 0) {
		$to = time();
	}
	if ($from <= 0) {
		$from = 0;
	}

	$st = $db->prepare(
		'SELECT IFNULL(SUM(`total_amount`),0) FROM `epc_erp_sales_orders`
		 WHERE `customer_user_id` = ? AND `status` IN (\'draft\',\'confirmed\')
		   AND `time_created` >= ? AND `time_created` <= ?'
	);
	$st->execute(array($user_id, $from, $to));
	$open_so = round((float) $st->fetchColumn(), 2);

	$inv = $db->prepare(
		'SELECT IFNULL(SUM(`total_incl_vat`),0) FROM `epc_einvoice_documents`
		 WHERE `user_id` = ? AND `active` = 1 AND `issue_date` >= ? AND `issue_date` <= ?'
	);
	$inv->execute(array($user_id, $from, $to));
	$invoiced = round((float) $inv->fetchColumn(), 2);

	$adv = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_cash_bank_entries`
		 WHERE `active` = 1 AND `counterparty_type` = \'customer\' AND `counterparty_id` = ?
		   AND `entry_type` = \'receipt\' AND `is_advance` = 1
		   AND `time` >= ? AND `time` <= ?'
	);
	$adv->execute(array($user_id, $from, $to));
	$advance_received = round((float) $adv->fetchColumn(), 2);

	$pay = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_cash_bank_entries`
		 WHERE `active` = 1 AND `counterparty_type` = \'customer\' AND `counterparty_id` = ?
		   AND `entry_type` = \'receipt\' AND `is_advance` = 0
		   AND `time` >= ? AND `time` <= ?'
	);
	$pay->execute(array($user_id, $from, $to));
	$other_payments = round((float) $pay->fetchColumn(), 2);

	$lc = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting`
		 WHERE `user_id` = ? AND `active` = 1 AND `income` = 1 AND `time` >= ? AND `time` <= ?'
	);
	$lc->execute(array($user_id, $from, $to));
	$ledger_credits = round((float) $lc->fetchColumn(), 2);

	$ld = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting`
		 WHERE `user_id` = ? AND `active` = 1 AND `income` = 0 AND `time` >= ? AND `time` <= ?'
	);
	$ld->execute(array($user_id, $from, $to));
	$ledger_debits = round((float) $ld->fetchColumn(), 2);

	$invoiced_unpaid = round(max(0, $invoiced + $ledger_credits - $other_payments - $ledger_debits), 2);
	$gross_receivable = round($open_so + $invoiced_unpaid, 2);
	$net_receivable = round(max(0, $gross_receivable - $advance_received), 2);
	$closing_balance = round($gross_receivable - $advance_received - $other_payments, 2);

	return array(
		'user_id' => $user_id,
		'advance_received' => $advance_received,
		'open_so_value' => $open_so,
		'invoiced_total' => $invoiced,
		'invoiced_unpaid' => $invoiced_unpaid,
		'other_payments' => $other_payments,
		'gross_receivable' => $gross_receivable,
		'net_receivable' => $net_receivable,
		'closing_balance' => $closing_balance,
		'date_from' => $from,
		'date_to' => $to,
	);
}

/** Supplier statement aggregate — PO, PI, PV/advance (mirrors customer SO/SI/RV). */
function epc_erp_supplier_statement_aggregate(PDO $db, int $supplier_id, int $from = 0, int $to = 0, int $limit = 200): array
{
	epc_erp_advances_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_extended.php';
	epc_erp_extended_ensure_schema($db);
	$supplier_id = (int) $supplier_id;
	if ($supplier_id <= 0) {
		return array();
	}
	if ($to <= 0) {
		$to = time();
	}
	if ($from <= 0) {
		$from = strtotime('-2 years', $to);
	}
	$rows = array();

	$po = $db->prepare(
		'SELECT `id`, `po_no` AS voucher_no, \'PO\' AS voucher_type, `time_created` AS `time`,
			`title` AS description, `total_amount` AS amount, `status`
		 FROM `epc_erp_purchase_orders`
		 WHERE `supplier_id` = ? AND `purchase_id` = 0 AND `time_created` >= ? AND `time_created` <= ?
		 ORDER BY `time_created` DESC'
	);
	$po->execute(array($supplier_id, $from, $to));
	while ($r = $po->fetch(PDO::FETCH_ASSOC)) {
		$open = in_array($r['status'], array('draft', 'approved', 'partial'), true);
		$rows[] = array(
			'time' => (int) $r['time'],
			'voucher_type' => 'PO',
			'voucher_no' => $r['voucher_no'],
			'description' => $r['description'] . ' (' . $r['status'] . ')',
			'debit' => 0.0,
			'credit' => $open ? (float) $r['amount'] : 0.0,
			'source' => 'purchase_order',
			'source_id' => (int) $r['id'],
		);
	}

	$pi = $db->prepare(
		'SELECT p.`id`, p.`voucher_no`, p.`purchase_date` AS `time`, p.`total_amount` AS amount, p.`status`, p.`invoice_number`
		 FROM `epc_erp_purchases` p
		 WHERE p.`supplier_id` = ? AND p.`active` = 1 AND p.`purchase_date` >= ? AND p.`purchase_date` <= ?
		 ORDER BY p.`purchase_date` DESC'
	);
	$pi->execute(array($supplier_id, $from, $to));
	while ($r = $pi->fetch(PDO::FETCH_ASSOC)) {
		$vno = $r['voucher_no'] ?: $r['invoice_number'];
		$rows[] = array(
			'time' => (int) $r['time'],
			'voucher_type' => 'PI',
			'voucher_no' => $vno,
			'description' => 'Purchase invoice (' . $r['status'] . ')',
			'debit' => 0.0,
			'credit' => (float) $r['amount'],
			'source' => 'purchase_invoice',
			'source_id' => (int) $r['id'],
		);
	}

	$cash = $db->prepare(
		'SELECT `id`, `voucher_no`, `time`, `amount`, `reference`, `note`, `is_advance`
		 FROM `epc_erp_cash_bank_entries`
		 WHERE `active` = 1 AND `counterparty_type` = \'supplier\' AND `counterparty_id` = ?
		   AND `entry_type` = \'payment\' AND `time` >= ? AND `time` <= ?
		 ORDER BY `time` DESC'
	);
	$cash->execute(array($supplier_id, $from, $to));
	while ($r = $cash->fetch(PDO::FETCH_ASSOC)) {
		$vno = $r['voucher_no'] ?: $r['reference'];
		$isAdv = !empty($r['is_advance']);
		$rows[] = array(
			'time' => (int) $r['time'],
			'voucher_type' => $isAdv ? 'ADV' : 'PV',
			'voucher_no' => $vno,
			'description' => ($isAdv ? 'Advance payment — ' : 'Payment voucher — ') . ($r['note'] ?: ''),
			'debit' => (float) $r['amount'],
			'credit' => 0.0,
			'source' => $isAdv ? 'advance_payment' : 'cash_entry',
			'source_id' => (int) $r['id'],
			'is_advance' => $isAdv ? 1 : 0,
		);
	}

	$legacy = $db->prepare(
		'SELECT * FROM `epc_erp_supplier_accounting`
		 WHERE `supplier_id` = ? AND `active` = 1 AND `cash_entry_id` = 0
		   AND (`entry_kind` IS NULL OR `entry_kind` NOT IN (\'invoice\',\'payment\'))
		   AND `purchase_id` = 0 AND `time` >= ? AND `time` <= ?
		 ORDER BY `time` DESC'
	);
	$legacy->execute(array($supplier_id, $from, $to));
	while ($line = $legacy->fetch(PDO::FETCH_ASSOC)) {
		$isCredit = ((int) $line['is_credit'] === 1);
		$kind = (string) ($line['entry_kind'] ?? ($isCredit ? 'invoice' : 'payment'));
		$rows[] = array(
			'time' => (int) $line['time'],
			'voucher_type' => strtoupper(substr($kind, 0, 3)),
			'voucher_no' => (string) ($line['reference'] ?: ('LEDGER-' . (int) $line['id'])),
			'description' => (string) ($line['note'] ?: epc_erp_supplier_entry_kind_label($kind)),
			'debit' => $isCredit ? 0.0 : (float) $line['amount'],
			'credit' => $isCredit ? (float) $line['amount'] : 0.0,
			'source' => 'supplier_accounting',
			'source_id' => (int) $line['id'],
		);
	}

	usort($rows, function ($a, $b) {
		return ($b['time'] ?? 0) <=> ($a['time'] ?? 0);
	});
	return array_slice($rows, 0, (int) $limit);
}

/** Supplier statement summary: advance paid, open PO, invoiced AP, net advance/payable. */
function epc_erp_supplier_statement_summary(PDO $db, int $supplier_id, int $from = 0, int $to = 0): array
{
	epc_erp_advances_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_extended.php';
	epc_erp_extended_ensure_schema($db);
	$supplier_id = (int) $supplier_id;
	if ($supplier_id <= 0) {
		return array();
	}
	if ($to <= 0) {
		$to = time();
	}
	if ($from <= 0) {
		$from = 0;
	}

	$open_po = $db->prepare(
		'SELECT IFNULL(SUM(`total_amount`),0) FROM `epc_erp_purchase_orders`
		 WHERE `supplier_id` = ? AND `purchase_id` = 0 AND `status` IN (\'draft\',\'approved\',\'partial\')
		   AND `time_created` >= ? AND `time_created` <= ?'
	);
	$open_po->execute(array($supplier_id, $from, $to));
	$open_po_value = round((float) $open_po->fetchColumn(), 2);

	$inv = $db->prepare(
		'SELECT IFNULL(SUM(`total_amount`),0) FROM `epc_erp_purchases`
		 WHERE `supplier_id` = ? AND `active` = 1 AND `purchase_date` >= ? AND `purchase_date` <= ?'
	);
	$inv->execute(array($supplier_id, $from, $to));
	$invoiced_ap = round((float) $inv->fetchColumn(), 2);

	$adv = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_cash_bank_entries`
		 WHERE `active` = 1 AND `counterparty_type` = \'supplier\' AND `counterparty_id` = ?
		   AND `entry_type` = \'payment\' AND `is_advance` = 1
		   AND `time` >= ? AND `time` <= ?'
	);
	$adv->execute(array($supplier_id, $from, $to));
	$advance_paid = round((float) $adv->fetchColumn(), 2);

	$pay = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_cash_bank_entries`
		 WHERE `active` = 1 AND `counterparty_type` = \'supplier\' AND `counterparty_id` = ?
		   AND `entry_type` = \'payment\' AND `is_advance` = 0
		   AND `time` >= ? AND `time` <= ?'
	);
	$pay->execute(array($supplier_id, $from, $to));
	$other_payments = round((float) $pay->fetchColumn(), 2);

	$exclude = epc_erp_supplier_balance_sql($db);
	$bal_up = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_supplier_accounting`
		 WHERE `supplier_id` = ? AND `active` = 1 AND `is_credit` = 1' . $exclude
	);
	$bal_up->execute(array($supplier_id));
	$payable_up = round((float) $bal_up->fetchColumn(), 2);
	$bal_dn = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_supplier_accounting`
		 WHERE `supplier_id` = ? AND `active` = 1 AND `is_credit` = 0' . $exclude
	);
	$bal_dn->execute(array($supplier_id));
	$payable_down = round((float) $bal_dn->fetchColumn(), 2);
	$ledger_payable = round($payable_up - $payable_down, 2);

	$invoiced_unpaid = round(max(0, $invoiced_ap - $other_payments), 2);
	$gross_commitment = round($open_po_value + $invoiced_unpaid, 2);
	$net_advance_with_supplier = round($advance_paid - $gross_commitment, 2);
	$net_payable = round(max(0, $gross_commitment - $advance_paid), 2);
	$closing_balance = round($ledger_payable, 2);

	return array(
		'supplier_id' => $supplier_id,
		'advance_paid' => $advance_paid,
		'open_po_value' => $open_po_value,
		'invoiced_ap' => $invoiced_ap,
		'invoiced_unpaid' => $invoiced_unpaid,
		'other_payments' => $other_payments,
		'gross_commitment' => $gross_commitment,
		'net_advance_with_supplier' => $net_advance_with_supplier,
		'net_payable' => $net_payable,
		'ledger_payable' => $ledger_payable,
		'closing_balance' => $closing_balance,
		'date_from' => $from,
		'date_to' => $to,
	);
}

/** Record UAE output VAT on ERP customer advance receipt (RV). */
function epc_uae_vat_record_advance_on_receipt(PDO $db, int $cash_entry_id, int $user_id, float $payment_amount, int $sales_order_id = 0, int $payment_time = 0): ?array
{
	epc_uae_tax_compliance_ensure_schema($db);
	if (!epc_uae_vat_on_advance_enabled($db) || $cash_entry_id <= 0 || $user_id <= 0 || $payment_amount <= 0) {
		return null;
	}
	if (!epc_uae_vat_sales_enabled($db)) {
		return null;
	}
	$dup = $db->prepare('SELECT `id` FROM `epc_uae_vat_advance` WHERE `cash_entry_id` = ? LIMIT 1');
	$dup->execute(array($cash_entry_id));
	if ($dup->fetchColumn()) {
		return null;
	}
	$split = epc_uae_vat_split_inclusive($payment_amount, $db);
	if ($split['vat_amount'] <= 0) {
		return null;
	}
	$now = $payment_time > 0 ? $payment_time : time();
	$order_id = 0;
	if ($sales_order_id > 0) {
		$ost = $db->prepare('SELECT `shop_order_id` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1');
		$ost->execute(array($sales_order_id));
		$order_id = (int) $ost->fetchColumn();
	}
	$db->prepare(
		'INSERT INTO `epc_uae_vat_advance`
		(`order_id`, `user_id`, `ledger_id`, `cash_entry_id`, `sales_order_id`, `source_type`,
		 `payment_amount`, `amount_ex_vat`, `vat_amount`, `vat_rate`, `payment_time`, `time_created`)
		VALUES (?,?,0,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$order_id,
		$user_id,
		$cash_entry_id,
		$sales_order_id,
		'receipt',
		round($payment_amount, 2),
		$split['amount_ex_vat'],
		$split['vat_amount'],
		$split['vat_rate'],
		$now,
		$now,
	));
	return array(
		'advance_id' => (int) $db->lastInsertId(),
		'vat_amount' => $split['vat_amount'],
		'amount_ex_vat' => $split['amount_ex_vat'],
	);
}

/** Record UAE input VAT on supplier advance payment (PV). */
function epc_uae_vat_record_supplier_advance_on_payment(PDO $db, int $cash_entry_id, int $supplier_id, float $payment_amount, int $purchase_order_id = 0, int $payment_time = 0): ?array
{
	epc_uae_tax_compliance_ensure_schema($db);
	epc_erp_advances_ensure_schema($db);
	if ($cash_entry_id <= 0 || $supplier_id <= 0 || $payment_amount <= 0) {
		return null;
	}
	require_once __DIR__ . '/epc_uae_vat.php';
	$supplier = epc_uae_vat_get_supplier($db, $supplier_id);
	if (!$supplier || empty($supplier['vat_registered']) || !epc_uae_vat_is_uae_country($supplier['country_code'] ?? 'AE')) {
		return null;
	}
	$dup = $db->prepare('SELECT `id` FROM `epc_uae_vat_supplier_advance` WHERE `cash_entry_id` = ? LIMIT 1');
	$dup->execute(array($cash_entry_id));
	if ($dup->fetchColumn()) {
		return null;
	}
	$split = epc_uae_vat_split_inclusive($payment_amount, $db);
	if ($split['vat_amount'] <= 0) {
		return null;
	}
	$now = $payment_time > 0 ? $payment_time : time();
	$db->prepare(
		'INSERT INTO `epc_uae_vat_supplier_advance`
		(`supplier_id`, `cash_entry_id`, `purchase_order_id`, `payment_amount`, `amount_ex_vat`, `vat_amount`, `vat_rate`, `payment_time`, `time_created`)
		VALUES (?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$supplier_id,
		$cash_entry_id,
		$purchase_order_id,
		round($payment_amount, 2),
		$split['amount_ex_vat'],
		$split['vat_amount'],
		$split['vat_rate'],
		$now,
		$now,
	));
	return array(
		'supplier_advance_id' => (int) $db->lastInsertId(),
		'vat_amount' => $split['vat_amount'],
		'amount_ex_vat' => $split['amount_ex_vat'],
	);
}

/** GL: customer advance receipt — Dr cash, Cr 2050 ex-VAT, Cr 2100 VAT output. */
function epc_erp_gl_post_advance_receipt(PDO $db, int $entry_id): int
{
	epc_erp_gl_ensure_schema($db);
	epc_erp_advances_ensure_schema($db);
	$q = $db->prepare(
		'SELECT e.*, a.coa_id, a.account_type
		FROM `epc_erp_cash_bank_entries` e
		INNER JOIN `epc_erp_cash_bank_accounts` a ON a.id = e.account_id
		WHERE e.id = ? AND e.active = 1 AND e.is_advance = 1 AND e.entry_type = \'receipt\' LIMIT 1'
	);
	$q->execute(array($entry_id));
	$e = $q->fetch(PDO::FETCH_ASSOC);
	if (!$e) {
		throw new Exception('Advance receipt entry not found');
	}
	if ((int) $e['gl_journal_id'] > 0) {
		return (int) $e['gl_journal_id'];
	}
	$cash_coa_id = (int) $e['coa_id'];
	if ($cash_coa_id <= 0) {
		$fallback = epc_erp_gl_coa_by_code($db, $e['account_type'] === 'bank' ? '1010' : '1000');
		$cash_coa_id = $fallback ? (int) $fallback['id'] : 0;
	}
	$adv = epc_erp_gl_coa_by_code($db, '2050');
	$vat_out = epc_erp_gl_coa_by_code($db, '2100');
	if ($cash_coa_id <= 0 || !$adv) {
		throw new Exception('COA cash/2050 missing');
	}
	$amount = round((float) $e['amount'], 2);
	$split = epc_uae_vat_split_inclusive($amount, $db);
	$ex = round((float) $split['amount_ex_vat'], 2);
	$vat = round((float) $split['vat_amount'], 2);
	if ($vat <= 0) {
		$ex = $amount;
	}
	$lines = array(
		array('coa_id' => $cash_coa_id, 'debit' => $amount, 'credit' => 0, 'line_note' => 'Customer advance receipt'),
		array('coa_id' => (int) $adv['id'], 'debit' => 0, 'credit' => $ex, 'line_note' => 'Customer advance liability'),
	);
	if ($vat > 0 && $vat_out) {
		$lines[] = array('coa_id' => (int) $vat_out['id'], 'debit' => 0, 'credit' => $vat, 'line_note' => 'UAE VAT output on advance');
	}
	$jid = epc_erp_gl_post_journal($db, array(
		'journal_date' => (int) $e['time'],
		'reference' => (string) ($e['voucher_no'] ?: $e['reference']),
		'description' => 'Customer advance receipt #' . $entry_id,
		'source_type' => 'cash',
		'source_id' => $entry_id,
		'uae_vat_treatment' => 'advance_receipt',
		'uae_tax_legislation_ref' => 'vat-decree-8-2017-art27',
	), $lines);
	$db->prepare('UPDATE `epc_erp_cash_bank_entries` SET `gl_journal_id` = ? WHERE `id` = ?')->execute(array($jid, $entry_id));
	return $jid;
}

/** GL: supplier advance payment — Dr 2060 ex-VAT, Dr 1150 VAT input, Cr cash. */
function epc_erp_gl_post_advance_payment(PDO $db, int $entry_id): int
{
	epc_erp_gl_ensure_schema($db);
	epc_erp_advances_ensure_schema($db);
	$q = $db->prepare(
		'SELECT e.*, a.coa_id, a.account_type
		FROM `epc_erp_cash_bank_entries` e
		INNER JOIN `epc_erp_cash_bank_accounts` a ON a.id = e.account_id
		WHERE e.id = ? AND e.active = 1 AND e.is_advance = 1 AND e.entry_type = \'payment\' LIMIT 1'
	);
	$q->execute(array($entry_id));
	$e = $q->fetch(PDO::FETCH_ASSOC);
	if (!$e) {
		throw new Exception('Advance payment entry not found');
	}
	if ((int) $e['gl_journal_id'] > 0) {
		return (int) $e['gl_journal_id'];
	}
	$cash_coa_id = (int) $e['coa_id'];
	if ($cash_coa_id <= 0) {
		$fallback = epc_erp_gl_coa_by_code($db, $e['account_type'] === 'bank' ? '1010' : '1000');
		$cash_coa_id = $fallback ? (int) $fallback['id'] : 0;
	}
	$prepay = epc_erp_gl_coa_by_code($db, '2060');
	$vat_in = epc_erp_gl_coa_by_code($db, '1150');
	if ($cash_coa_id <= 0 || !$prepay) {
		throw new Exception('COA cash/2060 missing');
	}
	$amount = round((float) $e['amount'], 2);
	$vat_row = $db->prepare('SELECT `amount_ex_vat`, `vat_amount` FROM `epc_uae_vat_supplier_advance` WHERE `cash_entry_id` = ? LIMIT 1');
	$vat_row->execute(array($entry_id));
	$vr = $vat_row->fetch(PDO::FETCH_ASSOC);
	if ($vr) {
		$ex = round((float) $vr['amount_ex_vat'], 2);
		$vat = round((float) $vr['vat_amount'], 2);
	} else {
		$split = epc_uae_vat_split_inclusive($amount, $db);
		$ex = round((float) $split['amount_ex_vat'], 2);
		$vat = round((float) $split['vat_amount'], 2);
	}
	if ($vat <= 0) {
		$ex = $amount;
	}
	$lines = array(
		array('coa_id' => (int) $prepay['id'], 'debit' => $ex, 'credit' => 0, 'line_note' => 'Supplier advance prepayment'),
	);
	if ($vat > 0 && $vat_in) {
		$lines[] = array('coa_id' => (int) $vat_in['id'], 'debit' => $vat, 'credit' => 0, 'line_note' => 'UAE VAT input on advance');
	}
	$lines[] = array('coa_id' => $cash_coa_id, 'debit' => 0, 'credit' => $amount, 'line_note' => 'Supplier advance payment');
	$jid = epc_erp_gl_post_journal($db, array(
		'journal_date' => (int) $e['time'],
		'reference' => (string) ($e['voucher_no'] ?: $e['reference']),
		'description' => 'Supplier advance payment #' . $entry_id,
		'source_type' => 'cash',
		'source_id' => $entry_id,
		'uae_vat_treatment' => 'advance_payment',
		'uae_tax_legislation_ref' => 'vat-decree-8-2017-art27',
	), $lines);
	$db->prepare('UPDATE `epc_erp_cash_bank_entries` SET `gl_journal_id` = ? WHERE `id` = ?')->execute(array($jid, $entry_id));
	return $jid;
}
