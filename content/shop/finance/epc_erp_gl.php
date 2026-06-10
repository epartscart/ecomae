<?php
/**
 * ERP — Chart of Accounts, General Ledger, P&L, Balance Sheet.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_schema.php';

function epc_erp_gl_account_types()
{
	return array(
		'asset' => 'Asset',
		'liability' => 'Liability',
		'equity' => 'Equity',
		'revenue' => 'Revenue',
		'expense' => 'Expense',
	);
}

function epc_erp_full_ensure_schema(PDO $db)
{
	require_once __DIR__ . '/epc_erp_vouchers.php';
	epc_erp_vouchers_ensure_schema($db);
	epc_erp_gl_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_staff.php';
	epc_erp_staff_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_payroll.php';
	epc_erp_payroll_ensure_schema($db);
	require_once __DIR__ . '/epc_einvoice_schema.php';
	epc_einvoice_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_inventory.php';
	epc_erp_inventory_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_fixed_assets.php';
	epc_erp_fa_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_opening.php';
	epc_erp_opening_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_order_fulfillment.php';
	epc_erp_order_fulfillment_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_advances.php';
	epc_erp_advances_ensure_schema($db);
}

function epc_erp_gl_ensure_schema(PDO $db)
{
	epc_erp_ensure_schema($db);

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_coa_accounts` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`code` varchar(16) NOT NULL,
		`name` varchar(255) NOT NULL,
		`account_type` enum('asset','liability','equity','revenue','expense') NOT NULL,
		`normal_side` enum('debit','credit') NOT NULL DEFAULT 'debit',
		`parent_id` int(11) NOT NULL DEFAULT 0,
		`opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00,
		`description` varchar(512) DEFAULT NULL,
		`system_flag` tinyint(1) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_code` (`code`),
		KEY `x_type` (`account_type`,`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP chart of accounts';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_gl_journals` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`journal_no` varchar(32) NOT NULL,
		`journal_date` int(11) NOT NULL,
		`reference` varchar(128) DEFAULT NULL,
		`description` text,
		`source_type` enum('manual','sales','purchase','payment','cash','opening','adjustment') NOT NULL DEFAULT 'manual',
		`source_id` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_journal_no` (`journal_no`),
		KEY `x_date` (`journal_date`),
		KEY `x_source` (`source_type`,`source_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP GL journal headers';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_gl_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`journal_id` int(11) NOT NULL,
		`coa_id` int(11) NOT NULL,
		`debit` decimal(14,2) NOT NULL DEFAULT 0.00,
		`credit` decimal(14,2) NOT NULL DEFAULT 0.00,
		`line_note` varchar(255) DEFAULT NULL,
		PRIMARY KEY (`id`),
		KEY `x_journal` (`journal_id`),
		KEY `x_coa` (`coa_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP GL journal lines';");

	epc_erp_gl_add_column_if_missing($db, 'epc_erp_purchases', 'gl_journal_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_gl_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'gl_journal_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_gl_add_column_if_missing($db, 'epc_erp_cash_bank_accounts', 'coa_id', 'int(11) NOT NULL DEFAULT 0');

	epc_erp_gl_seed_coa($db);
}

function epc_erp_gl_add_column_if_missing(PDO $db, $table, $column, $definition)
{
	try {
		$q = $db->prepare('SHOW COLUMNS FROM `' . $table . '` LIKE ?');
		$q->execute(array($column));
		if (!$q->fetch()) {
			$db->exec('ALTER TABLE `' . $table . '` ADD `' . $column . '` ' . $definition);
		}
	} catch (Exception $e) {
	}
}

function epc_erp_gl_seed_coa(PDO $db)
{
	$cnt = (int)$db->query('SELECT COUNT(*) FROM `epc_erp_coa_accounts`')->fetchColumn();
	if ($cnt > 0) {
		epc_erp_gl_link_cash_coa($db);
		return;
	}
	$now = time();
	$rows = array(
		array('1000', 'Cash on hand', 'asset', 'debit', 'Petty cash and cash drawers'),
		array('1010', 'Bank', 'asset', 'debit', 'Bank current accounts'),
		array('1100', 'Accounts receivable', 'asset', 'debit', 'Customer trade debtors'),
		array('1150', 'VAT input (recoverable)', 'asset', 'debit', 'UAE VAT 5% on purchases'),
		array('2000', 'Accounts payable', 'liability', 'credit', 'Supplier trade creditors'),
		array('2100', 'VAT output (payable)', 'liability', 'credit', 'UAE VAT 5% on sales'),
		array('3000', "Owner's equity", 'equity', 'credit', 'Capital / owner funds'),
		array('3100', 'Retained earnings', 'equity', 'credit', 'Accumulated profit'),
		array('4000', 'Sales revenue', 'revenue', 'credit', 'Parts sales ex VAT'),
		array('5000', 'Cost of goods sold', 'expense', 'debit', 'Purchase cost of parts sold'),
		array('6100', 'General expenses', 'expense', 'debit', 'Operating expenses'),
	);
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_coa_accounts`
		(`code`, `name`, `account_type`, `normal_side`, `description`, `system_flag`, `time_created`)
		VALUES (?, ?, ?, ?, ?, 1, ?)'
	);
	foreach ($rows as $r) {
		$ins->execute(array($r[0], $r[1], $r[2], $r[3], $r[4], $now));
	}
	epc_erp_gl_link_cash_coa($db);
}

function epc_erp_gl_link_cash_coa(PDO $db)
{
	$cashCoa = (int)$db->query("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = '1000' LIMIT 1")->fetchColumn();
	$bankCoa = (int)$db->query("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = '1010' LIMIT 1")->fetchColumn();
	if ($cashCoa <= 0 || $bankCoa <= 0) {
		return;
	}
	$accounts = $db->query('SELECT `id`, `account_type`, `coa_id` FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1')->fetchAll(PDO::FETCH_ASSOC);
	$upd = $db->prepare('UPDATE `epc_erp_cash_bank_accounts` SET `coa_id` = ? WHERE `id` = ? AND `coa_id` = 0');
	foreach ($accounts as $a) {
		$coa = ($a['account_type'] === 'bank') ? $bankCoa : $cashCoa;
		$upd->execute(array($coa, (int)$a['id']));
	}
}

function epc_erp_gl_coa_by_code(PDO $db, $code)
{
	$stmt = $db->prepare('SELECT * FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1');
	$stmt->execute(array((string)$code));
	return $stmt->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_erp_gl_list_coa(PDO $db)
{
	epc_erp_gl_ensure_schema($db);
	$rows = $db->query(
		'SELECT * FROM `epc_erp_coa_accounts` WHERE `active` = 1 ORDER BY `code` ASC'
	)->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$row) {
		$row['balance'] = epc_erp_gl_coa_balance($db, (int)$row['id']);
	}
	unset($row);
	return $rows;
}

function epc_erp_gl_coa_activity(PDO $db, $coa_id, $date_from = 0, $date_to = 0)
{
	$sql = 'SELECT IFNULL(SUM(l.`debit`),0) AS debits, IFNULL(SUM(l.`credit`),0) AS credits
		FROM `epc_erp_gl_lines` l
		INNER JOIN `epc_erp_gl_journals` j ON j.id = l.journal_id
		WHERE l.coa_id = ? AND j.active = 1';
	$params = array((int)$coa_id);
	if ($date_from > 0) {
		$sql .= ' AND j.journal_date >= ?';
		$params[] = (int)$date_from;
	}
	if ($date_to > 0) {
		$sql .= ' AND j.journal_date <= ?';
		$params[] = (int)$date_to;
	}
	$stmt = $db->prepare($sql);
	$stmt->execute($params);
	$row = $stmt->fetch(PDO::FETCH_ASSOC) ?: array('debits' => 0, 'credits' => 0);
	return array(
		'debits' => (float)$row['debits'],
		'credits' => (float)$row['credits'],
	);
}

function epc_erp_gl_signed_balance($opening, $debits, $credits, $normal_side)
{
	if ($normal_side === 'credit') {
		return (float)$opening + (float)$credits - (float)$debits;
	}
	return (float)$opening + (float)$debits - (float)$credits;
}

function epc_erp_gl_coa_balance(PDO $db, $coa_id, $as_of = 0)
{
	$coa = $db->prepare('SELECT * FROM `epc_erp_coa_accounts` WHERE `id` = ? LIMIT 1');
	$coa->execute(array((int)$coa_id));
	$row = $coa->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return 0.0;
	}
	$date_to = $as_of > 0 ? (int)$as_of : time();
	$act = epc_erp_gl_coa_activity($db, (int)$coa_id, 0, $date_to);
	return epc_erp_gl_signed_balance(
		$row['opening_balance'],
		$act['debits'],
		$act['credits'],
		$row['normal_side']
	);
}

function epc_erp_gl_next_journal_no(PDO $db)
{
	require_once __DIR__ . '/epc_erp_vouchers.php';
	epc_erp_vouchers_ensure_schema($db);
	return epc_erp_next_voucher_no($db, 'GV');
}

function epc_erp_gl_post_journal(PDO $db, array $header, array $lines)
{
	epc_erp_gl_ensure_schema($db);
	require_once __DIR__ . '/epc_uae_tax_compliance.php';
	$vatCheck = epc_uae_vat_apply_to_voucher($db, 'gl', array(
		'lines' => $lines,
		'legislation_ref' => (string)($header['uae_tax_legislation_ref'] ?? ''),
		'source_type' => (string)($header['source_type'] ?? ''),
	));
	if (!$vatCheck['ok']) {
		throw new Exception(implode('; ', $vatCheck['errors']));
	}
	if (empty($lines)) {
		throw new Exception('Journal must have lines');
	}
	$total_dr = 0.0;
	$total_cr = 0.0;
	foreach ($lines as $line) {
		$total_dr += round((float)($line['debit'] ?? 0), 2);
		$total_cr += round((float)($line['credit'] ?? 0), 2);
	}
	if (abs($total_dr - $total_cr) > 0.009) {
		throw new Exception('Journal not balanced: debit ' . $total_dr . ' vs credit ' . $total_cr);
	}
	if ($total_dr <= 0) {
		throw new Exception('Journal amount must be greater than zero');
	}

	if (!$db->beginTransaction()) {
		throw new Exception('Transaction start failed');
	}
	try {
		$journal_no = !empty($header['journal_no']) ? (string)$header['journal_no'] : epc_erp_gl_next_journal_no($db);
		$jdate = !empty($header['journal_date']) ? (int)$header['journal_date'] : time();
		$legRef = (string)($header['uae_tax_legislation_ref'] ?? $vatCheck['legislation_ref'] ?? '');
		$vatTreatment = trim((string)($header['uae_vat_treatment'] ?? ''));
		$stmt = $db->prepare(
			'INSERT INTO `epc_erp_gl_journals`
			(`journal_no`, `journal_date`, `reference`, `description`, `source_type`, `source_id`, `uae_tax_legislation_ref`, `uae_vat_treatment`, `admin_id`, `time_created`)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
		);
		$admin_id = function_exists('epc_erp_admin_id') ? epc_erp_admin_id() : 0;
		$stmt->execute(array(
			$journal_no,
			$jdate,
			trim((string)($header['reference'] ?? '')),
			trim((string)($header['description'] ?? '')),
			(string)($header['source_type'] ?? 'manual'),
			(int)($header['source_id'] ?? 0),
			$legRef,
			$vatTreatment,
			$admin_id,
			time(),
		));
		$journal_id = (int)$db->lastInsertId();
		$ins = $db->prepare(
			'INSERT INTO `epc_erp_gl_lines` (`journal_id`, `coa_id`, `debit`, `credit`, `line_note`) VALUES (?, ?, ?, ?, ?)'
		);
		foreach ($lines as $line) {
			$ins->execute(array(
				$journal_id,
				(int)$line['coa_id'],
				round((float)($line['debit'] ?? 0), 2),
				round((float)($line['credit'] ?? 0), 2),
				trim((string)($line['line_note'] ?? '')),
			));
		}
		$db->commit();
		return $journal_id;
	} catch (Exception $e) {
		if ($db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
}

function epc_erp_gl_create_coa(PDO $db, array $data)
{
	epc_erp_gl_ensure_schema($db);
	$type = (string)($data['account_type'] ?? 'expense');
	$types = epc_erp_gl_account_types();
	if (!isset($types[$type])) {
		throw new Exception('Invalid account type');
	}
	$normal = in_array($type, array('liability', 'equity', 'revenue'), true) ? 'credit' : 'debit';
	if (!empty($data['normal_side']) && in_array($data['normal_side'], array('debit', 'credit'), true)) {
		$normal = $data['normal_side'];
	}
	$code = trim((string)$data['code']);
	if ($code === '') {
		throw new Exception('Account code required');
	}
	$stmt = $db->prepare(
		'INSERT INTO `epc_erp_coa_accounts`
		(`code`, `name`, `account_type`, `normal_side`, `parent_id`, `opening_balance`, `description`, `time_created`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
	);
	$stmt->execute(array(
		$code,
		trim((string)$data['name']),
		$type,
		$normal,
		(int)($data['parent_id'] ?? 0),
		round((float)($data['opening_balance'] ?? 0), 2),
		trim((string)($data['description'] ?? '')),
		time(),
	));
	return (int)$db->lastInsertId();
}

function epc_erp_gl_list_journals(PDO $db, $date_from = 0, $date_to = 0, $limit = 200)
{
	epc_erp_gl_ensure_schema($db);
	$sql = 'SELECT j.*,
		(SELECT IFNULL(SUM(`debit`),0) FROM `epc_erp_gl_lines` WHERE `journal_id` = j.id) AS total_debit
		FROM `epc_erp_gl_journals` j WHERE j.active = 1';
	$params = array();
	if ($date_from > 0) {
		$sql .= ' AND j.journal_date >= ?';
		$params[] = (int)$date_from;
	}
	if ($date_to > 0) {
		$sql .= ' AND j.journal_date <= ?';
		$params[] = (int)$date_to;
	}
	$sql .= ' ORDER BY j.journal_date DESC, j.id DESC LIMIT ' . (int)$limit;
	$stmt = $db->prepare($sql);
	$stmt->execute($params);
	return $stmt->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_gl_journal_lines(PDO $db, $journal_id)
{
	$stmt = $db->prepare(
		'SELECT l.*, c.code AS coa_code, c.name AS coa_name, c.account_type
		FROM `epc_erp_gl_lines` l
		INNER JOIN `epc_erp_coa_accounts` c ON c.id = l.coa_id
		WHERE l.journal_id = ?
		ORDER BY l.id ASC'
	);
	$stmt->execute(array((int)$journal_id));
	return $stmt->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_gl_manual_entry(PDO $db, array $data)
{
	$lines_in = json_decode((string)($data['lines_json'] ?? '[]'), true);
	if (!is_array($lines_in) || empty($lines_in)) {
		throw new Exception('Add at least two GL lines');
	}
	$lines = array();
	foreach ($lines_in as $row) {
		$lines[] = array(
			'coa_id' => (int)($row['coa_id'] ?? 0),
			'debit' => (float)($row['debit'] ?? 0),
			'credit' => (float)($row['credit'] ?? 0),
			'line_note' => (string)($row['line_note'] ?? ''),
		);
	}
	$jdate = !empty($data['journal_date']) ? strtotime((string)$data['journal_date'] . ' 12:00:00') : time();
	return epc_erp_gl_post_journal($db, array(
		'journal_date' => $jdate ?: time(),
		'reference' => (string)($data['reference'] ?? ''),
		'description' => (string)($data['description'] ?? 'Manual journal entry'),
		'source_type' => 'manual',
	), $lines);
}

function epc_erp_gl_post_purchase(PDO $db, $purchase_id)
{
	epc_erp_gl_ensure_schema($db);
	$q = $db->prepare('SELECT * FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$q->execute(array((int)$purchase_id));
	$p = $q->fetch(PDO::FETCH_ASSOC);
	if (!$p) {
		throw new Exception('Purchase not found');
	}
	if ((int)$p['gl_journal_id'] > 0) {
		return (int)$p['gl_journal_id'];
	}
	$dup = $db->prepare(
		"SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type` = 'purchase' AND `source_id` = ? AND `active` = 1 LIMIT 1"
	);
	$dup->execute(array((int)$purchase_id));
	$existingJ = (int)$dup->fetchColumn();
	if ($existingJ > 0) {
		$db->prepare('UPDATE `epc_erp_purchases` SET `gl_journal_id` = ? WHERE `id` = ?')->execute(array($existingJ, (int)$purchase_id));
		return $existingJ;
	}
	$cogs = epc_erp_gl_coa_by_code($db, '5000');
	$vat_in = epc_erp_gl_coa_by_code($db, '1150');
	$ap = epc_erp_gl_coa_by_code($db, '2000');
	if (!$cogs || !$ap) {
		throw new Exception('COA accounts missing — run ERP setup');
	}
	$ex = round((float)$p['amount_ex_vat'], 2);
	$vat = round((float)$p['vat_amount'], 2);
	$total = round((float)$p['total_amount'], 2);
	$lines = array(
		array('coa_id' => (int)$cogs['id'], 'debit' => $ex, 'credit' => 0, 'line_note' => 'Purchase ex VAT'),
	);
	if ($vat > 0 && $vat_in) {
		$lines[] = array('coa_id' => (int)$vat_in['id'], 'debit' => $vat, 'credit' => 0, 'line_note' => 'VAT input');
	}
	$lines[] = array('coa_id' => (int)$ap['id'], 'debit' => 0, 'credit' => $total, 'line_note' => 'Accounts payable');
	$jid = epc_erp_gl_post_journal($db, array(
		'journal_date' => (int)$p['purchase_date'],
		'reference' => (string)$p['invoice_number'],
		'description' => 'Purchase invoice #' . (int)$purchase_id,
		'source_type' => 'purchase',
		'source_id' => (int)$purchase_id,
		'uae_vat_treatment' => (string)($p['uae_vat_treatment'] ?? 'standard'),
		'uae_tax_legislation_ref' => (string)($p['uae_tax_legislation_ref'] ?? 'vat-decree-8-2017'),
	), $lines);
	$db->prepare('UPDATE `epc_erp_purchases` SET `gl_journal_id` = ? WHERE `id` = ?')->execute(array($jid, (int)$purchase_id));
	return $jid;
}

function epc_erp_gl_post_cash_entry(PDO $db, $entry_id)
{
	epc_erp_gl_ensure_schema($db);
	$q = $db->prepare(
		'SELECT e.*, a.coa_id, a.account_type, a.name AS cash_account_name
		FROM `epc_erp_cash_bank_entries` e
		INNER JOIN `epc_erp_cash_bank_accounts` a ON a.id = e.account_id
		WHERE e.id = ? AND e.active = 1 LIMIT 1'
	);
	$q->execute(array((int)$entry_id));
	$e = $q->fetch(PDO::FETCH_ASSOC);
	if (!$e) {
		throw new Exception('Cash entry not found');
	}
	if ((int)$e['gl_journal_id'] > 0) {
		return (int)$e['gl_journal_id'];
	}
	$cash_coa_id = (int)$e['coa_id'];
	if ($cash_coa_id <= 0) {
		$fallback = epc_erp_gl_coa_by_code($db, $e['account_type'] === 'bank' ? '1010' : '1000');
		$cash_coa_id = $fallback ? (int)$fallback['id'] : 0;
	}
	if ($cash_coa_id <= 0) {
		throw new Exception('Cash COA not linked');
	}
	$amount = round((float)$e['amount'], 2);
	$expense = epc_erp_gl_coa_by_code($db, '6100');
	$ar = epc_erp_gl_coa_by_code($db, '1100');
	$ap = epc_erp_gl_coa_by_code($db, '2000');
	$lines = array();
	$desc = 'Cash/bank entry #' . (int)$entry_id;

	if ((int)$e['direction'] === 1) {
		$lines[] = array('coa_id' => $cash_coa_id, 'debit' => $amount, 'credit' => 0, 'line_note' => 'Receipt');
		if ($e['counterparty_type'] === 'customer' && $ar) {
			$lines[] = array('coa_id' => (int)$ar['id'], 'debit' => 0, 'credit' => $amount, 'line_note' => 'Customer receipt');
		} else {
			$rev = epc_erp_gl_coa_by_code($db, '4000');
			$lines[] = array('coa_id' => (int)$rev['id'], 'debit' => 0, 'credit' => $amount, 'line_note' => 'Other income');
		}
	} else {
		$lines[] = array('coa_id' => $cash_coa_id, 'debit' => 0, 'credit' => $amount, 'line_note' => 'Payment');
		if ($e['counterparty_type'] === 'supplier' && $ap) {
			$lines[] = array('coa_id' => (int)$ap['id'], 'debit' => $amount, 'credit' => 0, 'line_note' => 'Supplier payment');
		} elseif ($expense) {
			$lines[] = array('coa_id' => (int)$expense['id'], 'debit' => $amount, 'credit' => 0, 'line_note' => 'Expense');
		}
	}

	$jid = epc_erp_gl_post_journal($db, array(
		'journal_date' => (int)$e['time'],
		'reference' => (string)$e['reference'],
		'description' => $desc,
		'source_type' => 'cash',
		'source_id' => (int)$entry_id,
	), $lines);
	$db->prepare('UPDATE `epc_erp_cash_bank_entries` SET `gl_journal_id` = ? WHERE `id` = ?')->execute(array($jid, (int)$entry_id));
	return $jid;
}

function epc_erp_gl_post_sales_orders(PDO $db, $date_from, $date_to)
{
	epc_erp_gl_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_helpers.php';
	$orders = epc_erp_revenue_report($db, $date_from, $date_to, 500);
	$ar = epc_erp_gl_coa_by_code($db, '1100');
	$rev = epc_erp_gl_coa_by_code($db, '4000');
	$vat_out = epc_erp_gl_coa_by_code($db, '2100');
	if (!$ar || !$rev) {
		throw new Exception('Sales COA accounts missing');
	}
	$posted = 0;
	foreach ($orders as $o) {
		$sale = round((float)$o['sale_ex_vat'], 2);
		if ($sale <= 0) {
			continue;
		}
		$chk = $db->prepare(
			"SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type` = 'sales' AND `source_id` = ? AND `active` = 1 LIMIT 1"
		);
		$chk->execute(array((int)$o['id']));
		if ((int)$chk->fetchColumn() > 0) {
			continue;
		}
		require_once __DIR__ . '/epc_uae_vat.php';
		$calc = epc_uae_vat_calc_on_exclusive($sale, $db);
		$vat = round((float)$calc['vat_amount'], 2);
		$total = round((float)$calc['total_incl_vat'], 2);
		$salesTreatment = epc_uae_vat_sales_treatment_for_order($db, (int)($o['user_id'] ?? 0));
		$lines = array(
			array('coa_id' => (int)$ar['id'], 'debit' => $total, 'credit' => 0, 'line_note' => 'Order #' . (int)$o['id']),
			array('coa_id' => (int)$rev['id'], 'debit' => 0, 'credit' => $sale, 'line_note' => 'Sales revenue'),
		);
		if ($vat > 0 && $vat_out) {
			$lines[] = array('coa_id' => (int)$vat_out['id'], 'debit' => 0, 'credit' => $vat, 'line_note' => 'VAT output (' . $salesTreatment . ')');
		}
		epc_erp_gl_post_journal($db, array(
			'journal_date' => (int)$o['time'],
			'reference' => 'ORD-' . (int)$o['id'],
			'description' => 'Sales recognition order #' . (int)$o['id'],
			'source_type' => 'sales',
			'source_id' => (int)$o['id'],
			'uae_vat_treatment' => $salesTreatment,
			'uae_tax_legislation_ref' => 'vat-decree-8-2017',
		), $lines);
		$posted++;
	}
	return $posted;
}

function epc_erp_gl_sync_unposted(PDO $db)
{
	$count = 0;
	$purchases = $db->query('SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `gl_journal_id` = 0')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($purchases as $p) {
		try {
			epc_erp_gl_post_purchase($db, (int)$p['id']);
			$count++;
		} catch (Exception $e) {
		}
	}
	$entries = $db->query('SELECT `id` FROM `epc_erp_cash_bank_entries` WHERE `active` = 1 AND `gl_journal_id` = 0')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($entries as $e) {
		try {
			epc_erp_gl_post_cash_entry($db, (int)$e['id']);
			$count++;
		} catch (Exception $ex) {
		}
	}
	return $count;
}

function epc_erp_gl_pl_report(PDO $db, $date_from, $date_to)
{
	epc_erp_gl_ensure_schema($db);
	$accounts = $db->query(
		"SELECT * FROM `epc_erp_coa_accounts` WHERE `active` = 1 AND `account_type` IN ('revenue','expense') ORDER BY `code`"
	)->fetchAll(PDO::FETCH_ASSOC);
	$revenue = array();
	$expenses = array();
	$total_rev = 0.0;
	$total_exp = 0.0;
	foreach ($accounts as $a) {
		$act = epc_erp_gl_coa_activity($db, (int)$a['id'], $date_from, $date_to);
		$period = epc_erp_gl_signed_balance(0, $act['debits'], $act['credits'], $a['normal_side']);
		if (abs($period) < 0.005) {
			continue;
		}
		$row = array(
			'code' => $a['code'],
			'name' => $a['name'],
			'amount' => $period,
		);
		if ($a['account_type'] === 'revenue') {
			$revenue[] = $row;
			$total_rev += $period;
		} else {
			$expenses[] = $row;
			$total_exp += $period;
		}
	}
	return array(
		'date_from' => $date_from,
		'date_to' => $date_to,
		'revenue' => $revenue,
		'expenses' => $expenses,
		'total_revenue' => $total_rev,
		'total_expenses' => $total_exp,
		'net_profit' => $total_rev - $total_exp,
	);
}

function epc_erp_gl_balance_sheet(PDO $db, $as_of)
{
	epc_erp_gl_ensure_schema($db);
	$types = array('asset' => array(), 'liability' => array(), 'equity' => array());
	$totals = array('asset' => 0.0, 'liability' => 0.0, 'equity' => 0.0);
	$accounts = $db->query(
		"SELECT * FROM `epc_erp_coa_accounts` WHERE `active` = 1 AND `account_type` IN ('asset','liability','equity') ORDER BY `code`"
	)->fetchAll(PDO::FETCH_ASSOC);
	foreach ($accounts as $a) {
		$bal = epc_erp_gl_coa_balance($db, (int)$a['id'], $as_of);
		if (abs($bal) < 0.005) {
			continue;
		}
		$types[$a['account_type']][] = array(
			'code' => $a['code'],
			'name' => $a['name'],
			'balance' => $bal,
		);
		$totals[$a['account_type']] += $bal;
	}
	$pl = epc_erp_gl_pl_report($db, 0, $as_of);
	$current_earnings = $pl['net_profit'];
	$totals['equity'] += $current_earnings;
	return array(
		'as_of' => $as_of,
		'assets' => $types['asset'],
		'liabilities' => $types['liability'],
		'equity' => $types['equity'],
		'current_earnings' => $current_earnings,
		'total_assets' => $totals['asset'],
		'total_liabilities' => $totals['liability'],
		'total_equity' => $totals['equity'],
		'total_liabilities_equity' => $totals['liability'] + $totals['equity'],
	);
}

function epc_erp_gl_trial_balance(PDO $db, $as_of)
{
	epc_erp_gl_ensure_schema($db);
	$rows = epc_erp_gl_list_coa($db);
	$out = array();
	$td = 0.0;
	$tc = 0.0;
	foreach ($rows as $a) {
		$bal = epc_erp_gl_coa_balance($db, (int)$a['id'], $as_of);
		if (abs($bal) < 0.005) {
			continue;
		}
		$dr = $bal > 0 && $a['normal_side'] === 'debit' ? $bal : ($bal < 0 && $a['normal_side'] === 'credit' ? abs($bal) : 0);
		$cr = $bal > 0 && $a['normal_side'] === 'credit' ? $bal : ($bal < 0 && $a['normal_side'] === 'debit' ? abs($bal) : 0);
		if ($a['normal_side'] === 'debit' && $bal < 0) {
			$cr = abs($bal);
			$dr = 0;
		}
		if ($a['normal_side'] === 'credit' && $bal < 0) {
			$dr = abs($bal);
			$cr = 0;
		}
		$out[] = array(
			'code' => $a['code'],
			'name' => $a['name'],
			'account_type' => $a['account_type'],
			'balance' => $bal,
			'debit' => $dr,
			'credit' => $cr,
		);
		$td += $dr;
		$tc += $cr;
	}
	return array('rows' => $out, 'total_debit' => $td, 'total_credit' => $tc, 'as_of' => $as_of);
}

function epc_erp_gl_post_ar_settlement(PDO $db, array $data)
{
	epc_erp_gl_ensure_schema($db);
	$ar = epc_erp_gl_coa_by_code($db, '1100');
	$exp = epc_erp_gl_coa_by_code($db, '6100');
	if (!$ar || !$exp) {
		throw new Exception('COA 1100/6100 missing');
	}
	$amount = round((float)$data['amount'], 2);
	$income = (int)($data['income'] ?? 0);
	$entry_kind = (string)($data['entry_kind'] ?? 'adjustment');
	$lines = array();
	if ($income === 1) {
		$lines[] = array('coa_id' => (int)$exp['id'], 'debit' => $amount, 'credit' => 0, 'line_note' => 'Customer credit — ' . $entry_kind);
		$lines[] = array('coa_id' => (int)$ar['id'], 'debit' => 0, 'credit' => $amount, 'line_note' => 'Customer ledger credit');
	} else {
		$lines[] = array('coa_id' => (int)$ar['id'], 'debit' => $amount, 'credit' => 0, 'line_note' => 'Customer ledger debit');
		$lines[] = array('coa_id' => (int)$exp['id'], 'debit' => 0, 'credit' => $amount, 'line_note' => 'Customer debit — ' . $entry_kind);
	}
	return epc_erp_gl_post_journal($db, array(
		'journal_date' => (int)($data['time'] ?? time()),
		'reference' => (string)($data['reference'] ?? ''),
		'description' => (string)($data['note'] ?? 'Customer AR settlement'),
		'source_type' => 'adjustment',
		'source_id' => (int)($data['ledger_id'] ?? 0),
	), $lines);
}

function epc_erp_gl_post_ap_settlement(PDO $db, array $data)
{
	epc_erp_gl_ensure_schema($db);
	$ap = epc_erp_gl_coa_by_code($db, '2000');
	$exp = epc_erp_gl_coa_by_code($db, '6100');
	if (!$ap || !$exp) {
		throw new Exception('COA 2000/6100 missing');
	}
	$amount = round((float)$data['amount'], 2);
	$is_credit = (int)($data['is_credit'] ?? 0);
	$entry_kind = (string)($data['entry_kind'] ?? 'adjustment');
	$lines = array();
	if ($is_credit === 1) {
		$lines[] = array('coa_id' => (int)$exp['id'], 'debit' => $amount, 'credit' => 0, 'line_note' => 'AP increase — ' . $entry_kind);
		$lines[] = array('coa_id' => (int)$ap['id'], 'debit' => 0, 'credit' => $amount, 'line_note' => 'Supplier payable up');
	} else {
		$lines[] = array('coa_id' => (int)$ap['id'], 'debit' => $amount, 'credit' => 0, 'line_note' => 'Supplier payable down');
		$lines[] = array('coa_id' => (int)$exp['id'], 'debit' => 0, 'credit' => $amount, 'line_note' => 'AP decrease — ' . $entry_kind);
	}
	return epc_erp_gl_post_journal($db, array(
		'journal_date' => (int)($data['time'] ?? time()),
		'reference' => (string)($data['reference'] ?? ''),
		'description' => (string)($data['note'] ?? 'Supplier AP settlement'),
		'source_type' => 'adjustment',
		'source_id' => (int)($data['ledger_id'] ?? 0),
	), $lines);
}
