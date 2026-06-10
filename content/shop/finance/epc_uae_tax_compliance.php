<?php
/**
 * UAE tax compliance — advance payment VAT, invoice adjustment, export/zero-rated,
 * corporate tax (9%) provision, FTA legislation cache.
 *
 * Legal basis (summary): Federal Decree-Law No. 8 of 2017 on VAT — output tax on
 * taxable supplies including advances (Art. 27); tax invoice / credit note rules (Arts. 59–67).
 * Corporate Tax Federal Decree-Law No. 47 of 2022 — 9% on taxable income above threshold.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_uae_vat.php';
require_once __DIR__ . '/epc_erp_schema.php';

function epc_uae_tax_compliance_ensure_schema(PDO $db): void
{
	epc_uae_vat_ensure_settings($db);

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_uae_vat_advance` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`user_id` int(11) NOT NULL DEFAULT 0,
		`ledger_id` int(11) NOT NULL DEFAULT 0,
		`payment_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_rate` decimal(5,2) NOT NULL DEFAULT 5.00,
		`payment_time` int(11) NOT NULL DEFAULT 0,
		`einvoice_document_id` int(11) NOT NULL DEFAULT 0,
		`adjusted` tinyint(1) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_ledger` (`ledger_id`),
		KEY `x_order` (`order_id`,`adjusted`),
		KEY `x_time` (`payment_time`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE VAT on customer advance payments';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_uae_tax_compliance_cache` (
		`cache_key` varchar(64) NOT NULL,
		`payload_json` mediumtext,
		`source_url` varchar(512) DEFAULT NULL,
		`time_fetched` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`cache_key`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='FTA legislation fetch cache';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_uae_tax_legislation_items` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`item_key` varchar(512) NOT NULL,
		`slug` varchar(128) NOT NULL DEFAULT '',
		`title` varchar(512) NOT NULL DEFAULT '',
		`issue_date` varchar(32) NOT NULL DEFAULT '',
		`publish_date` varchar(32) NOT NULL DEFAULT '',
		`category` varchar(128) NOT NULL DEFAULT '',
		`tax_category` varchar(32) NOT NULL DEFAULT 'general',
		`pdf_url` varchar(512) NOT NULL DEFAULT '',
		`erp_summary` mediumtext,
		`compliance_actions_json` mediumtext,
		`pattern_key` varchar(64) NOT NULL DEFAULT '',
		`is_new` tinyint(1) NOT NULL DEFAULT 0,
		`is_updated` tinyint(1) NOT NULL DEFAULT 0,
		`time_synced` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_item_key` (`item_key`(255)),
		KEY `x_tax_cat` (`tax_category`),
		KEY `x_issue` (`issue_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE FTA legislation with ERP summaries';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_uae_ct_adjustments` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`period_key` varchar(32) NOT NULL,
		`adjustment_key` varchar(64) NOT NULL,
		`direction` enum('add','deduct') NOT NULL DEFAULT 'add',
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`notes` varchar(512) NOT NULL DEFAULT '',
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_period_adj` (`period_key`,`adjustment_key`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE CT P&L adjustments by period';");

	epc_erp_schema_add_column_if_missing($db, 'epc_crm_expenses', 'amount_ex_vat', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_crm_expenses', 'vat_amount', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_crm_expenses', 'vat_expense_type', "varchar(64) NOT NULL DEFAULT ''");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_expense_reports', 'amount_ex_vat', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_expense_reports', 'vat_amount', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_expense_reports', 'vat_expense_type', "varchar(64) NOT NULL DEFAULT 'staff_general'");

	epc_erp_schema_add_column_if_missing($db, 'epc_einvoice_documents', 'advance_vat_credit', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_einvoice_documents', 'vat_net_after_advance', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_gl_journals', 'uae_tax_legislation_ref', "varchar(128) NOT NULL DEFAULT ''");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'uae_vat_treatment', "varchar(64) NOT NULL DEFAULT 'standard'");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'uae_tax_legislation_ref', "varchar(128) NOT NULL DEFAULT ''");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_gl_journals', 'uae_vat_treatment', "varchar(64) NOT NULL DEFAULT ''");

	$defaults = array(
		'vat_on_advance_enabled' => '1',
		'corporate_tax_enabled' => '1',
		'corporate_tax_rate' => '9.00',
		'corporate_tax_threshold_aed' => '375000',
	);
	foreach ($defaults as $key => $val) {
		$st = $db->prepare('SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1');
		$st->execute(array($key));
		if ($st->fetchColumn() === false) {
			$db->prepare('INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?)')->execute(array($key, $val));
		}
	}
}

function epc_uae_tax_setting(PDO $db, string $key, $default = '')
{
	$st = $db->prepare('SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1');
	$st->execute(array($key));
	$v = $st->fetchColumn();
	return ($v === false) ? $default : $v;
}

function epc_uae_vat_on_advance_enabled(PDO $db): bool
{
	$v = epc_uae_tax_setting($db, 'vat_on_advance_enabled', '1');
	return $v === '1' || $v === 1 || $v === 'true' || $v === '';
}

function epc_uae_corporate_tax_enabled(PDO $db): bool
{
	$v = epc_uae_tax_setting($db, 'corporate_tax_enabled', '1');
	return $v === '1' || $v === 1 || $v === 'true' || $v === '';
}

function epc_uae_corporate_tax_rate_percent(PDO $db): float
{
	$rate = (float)epc_uae_tax_setting($db, 'corporate_tax_rate', '9.00');
	if ($rate < 0) {
		$rate = 0;
	}
	if ($rate > 100) {
		$rate = 100;
	}
	return round($rate, 2);
}

function epc_uae_corporate_tax_threshold(PDO $db): float
{
	return max(0.0, (float)epc_uae_tax_setting($db, 'corporate_tax_threshold_aed', '375000'));
}

/** Split VAT-inclusive payment into ex-VAT + VAT (UAE standard rate). */
function epc_uae_vat_split_inclusive(float $amount_incl, PDO $db): array
{
	$amount_incl = round(max(0, $amount_incl), 2);
	if ($amount_incl <= 0 || !epc_uae_vat_sales_enabled($db)) {
		return array('amount_ex_vat' => $amount_incl, 'vat_amount' => 0.0, 'vat_rate' => 0.0);
	}
	$rate = epc_uae_vat_rate_percent($db);
	$ex = round($amount_incl / (1 + ($rate / 100)), 2);
	$vat = round($amount_incl - $ex, 2);
	return array('amount_ex_vat' => $ex, 'vat_amount' => $vat, 'vat_rate' => $rate);
}

/**
 * Tax category for a supply line — export / non-UAE buyer → zero-rated (Z).
 */
function epc_uae_vat_supply_tax_category(PDO $db, array $buyer, array $transaction_flags = array()): array
{
	if (!empty($transaction_flags['exports'])) {
		return array('tax_category' => 'Z', 'tax_rate' => 0.0, 'reason' => 'export');
	}
	$country = isset($buyer['buyer_country_code']) ? $buyer['buyer_country_code'] : ($buyer['country_code'] ?? 'AE');
	if (!epc_uae_vat_is_uae_country($country)) {
		return array('tax_category' => 'Z', 'tax_rate' => 0.0, 'reason' => 'non_uae_buyer');
	}
	if (!empty($transaction_flags['margin_scheme'])) {
		return array('tax_category' => 'M', 'tax_rate' => 0.0, 'reason' => 'margin_scheme');
	}
	$rate = epc_uae_vat_rate_percent($db);
	return array('tax_category' => 'S', 'tax_rate' => $rate, 'reason' => 'standard');
}

/** Record output VAT on customer advance when payment is received (income=0 on order). */
function epc_uae_vat_record_advance_on_payment(PDO $db, int $ledger_id, int $order_id, float $payment_amount, int $payment_time = 0): ?array
{
	epc_uae_tax_compliance_ensure_schema($db);
	if (!epc_uae_vat_on_advance_enabled($db) || $ledger_id <= 0 || $order_id <= 0 || $payment_amount <= 0) {
		return null;
	}
	if (!epc_uae_vat_sales_enabled($db)) {
		return null;
	}

	$dup = $db->prepare('SELECT `id` FROM `epc_uae_vat_advance` WHERE `ledger_id` = ? LIMIT 1');
	$dup->execute(array($ledger_id));
	if ($dup->fetchColumn()) {
		return null;
	}

	$ost = $db->prepare('SELECT `user_id` FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1');
	$ost->execute(array($order_id));
	$user_id = (int)$ost->fetchColumn();
	if ($user_id <= 0) {
		return null;
	}

	$split = epc_uae_vat_split_inclusive($payment_amount, $db);
	if ($split['vat_amount'] <= 0) {
		return null;
	}

	$now = $payment_time > 0 ? $payment_time : time();
	$db->prepare(
		'INSERT INTO `epc_uae_vat_advance`
		(`order_id`, `user_id`, `ledger_id`, `payment_amount`, `amount_ex_vat`, `vat_amount`, `vat_rate`, `payment_time`, `time_created`)
		VALUES (?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$order_id,
		$user_id,
		$ledger_id,
		round($payment_amount, 2),
		$split['amount_ex_vat'],
		$split['vat_amount'],
		$split['vat_rate'],
		$now,
		$now,
	));

	return array(
		'advance_id' => (int)$db->lastInsertId(),
		'order_id' => $order_id,
		'vat_amount' => $split['vat_amount'],
		'amount_ex_vat' => $split['amount_ex_vat'],
	);
}

/** Backfill advance VAT rows for active order payments not yet recorded. */
function epc_uae_vat_sync_order_payments(PDO $db, int $order_id): int
{
	epc_uae_tax_compliance_ensure_schema($db);
	if ($order_id <= 0) {
		return 0;
	}
	$q = $db->prepare(
		'SELECT `id`, `amount`, `time` FROM `shop_users_accounting`
		WHERE `active` = 1 AND `income` = 0 AND `order_id` = ? AND `amount` > 0'
	);
	$q->execute(array($order_id));
	$n = 0;
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		if (epc_uae_vat_record_advance_on_payment($db, (int)$row['id'], $order_id, (float)$row['amount'], (int)$row['time'])) {
			$n++;
		}
	}
	return $n;
}

/** Sum unadjusted advance VAT for an order. */
function epc_uae_vat_advance_total_for_order(PDO $db, int $order_id, bool $unadjusted_only = true): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$sql = 'SELECT IFNULL(SUM(`vat_amount`),0) AS vat, IFNULL(SUM(`amount_ex_vat`),0) AS ex, IFNULL(SUM(`payment_amount`),0) AS paid, COUNT(*) AS cnt
		FROM `epc_uae_vat_advance` WHERE `order_id` = ?';
	if ($unadjusted_only) {
		$sql .= ' AND `adjusted` = 0';
	}
	$st = $db->prepare($sql);
	$st->execute(array($order_id));
	$row = $st->fetch(PDO::FETCH_ASSOC) ?: array();
	return array(
		'vat_amount' => round((float)($row['vat'] ?? 0), 2),
		'amount_ex_vat' => round((float)($row['ex'] ?? 0), 2),
		'payment_amount' => round((float)($row['paid'] ?? 0), 2),
		'count' => (int)($row['cnt'] ?? 0),
	);
}

/**
 * On final tax invoice — credit advance VAT against invoice output VAT (FTA adjustment).
 */
function epc_uae_vat_apply_invoice_adjustment(PDO $db, int $document_id, int $order_id = 0): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	require_once __DIR__ . '/epc_einvoice.php';

	$doc = epc_einvoice_get_document($db, $document_id);
	if (!$doc) {
		return array('applied' => false, 'message' => 'Document not found');
	}
	if ($order_id <= 0) {
		$order_id = (int)($doc['order_id'] ?? 0);
	}
	if ($order_id <= 0) {
		return array('applied' => false, 'message' => 'No order linked');
	}

	epc_uae_vat_sync_order_payments($db, $order_id);
	$adv = epc_uae_vat_advance_total_for_order($db, $order_id, true);
	$invoice_vat = round((float)($doc['total_vat'] ?? 0), 2);
	$credit = round(min($invoice_vat, $adv['vat_amount']), 2);
	$net_vat = round(max(0, $invoice_vat - $credit), 2);

	$db->prepare(
		'UPDATE `epc_einvoice_documents` SET `advance_vat_credit` = ?, `vat_net_after_advance` = ?, `time_updated` = ? WHERE `id` = ?'
	)->execute(array($credit, $net_vat, time(), $document_id));

	if ($credit > 0) {
		$db->prepare(
			'UPDATE `epc_uae_vat_advance` SET `adjusted` = 1, `einvoice_document_id` = ? WHERE `order_id` = ? AND `adjusted` = 0'
		)->execute(array($document_id, $order_id));
		epc_einvoice_log_event($db, $document_id, 'vat_advance_adjust', 'info',
			'Advance VAT credited ' . number_format($credit, 2) . ' AED; net output VAT on invoice ' . number_format($net_vat, 2) . ' AED',
			array('advance_vat' => $adv, 'invoice_vat' => $invoice_vat, 'credit' => $credit));
	}

	return array(
		'applied' => true,
		'invoice_vat' => $invoice_vat,
		'advance_vat_credit' => $credit,
		'vat_net_after_advance' => $net_vat,
		'advance_rows' => $adv['count'],
	);
}

/** Period totals for VAT return — advance VAT accounted vs adjusted on invoices. */
function epc_uae_vat_advance_period_summary(PDO $db, int $date_from, int $date_to): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$st = $db->prepare(
		'SELECT IFNULL(SUM(`vat_amount`),0) AS output_on_advances,
			IFNULL(SUM(CASE WHEN `adjusted` = 1 THEN `vat_amount` ELSE 0 END),0) AS adjusted_on_invoices,
			COUNT(*) AS payment_count
		FROM `epc_uae_vat_advance`
		WHERE `payment_time` >= ? AND `payment_time` <= ?'
	);
	$st->execute(array($date_from, $date_to));
	$row = $st->fetch(PDO::FETCH_ASSOC) ?: array();
	$output = round((float)($row['output_on_advances'] ?? 0), 2);
	$adjusted = round((float)($row['adjusted_on_invoices'] ?? 0), 2);
	return array(
		'output_vat_on_advances' => $output,
		'advance_vat_credited_on_invoices' => $adjusted,
		'advance_payment_count' => (int)($row['payment_count'] ?? 0),
		'unadjusted_advance_vat' => round($output - $adjusted, 2),
	);
}

/** CT adjustment field definitions (add-backs and deductions per UAE CT return concepts). */
function epc_uae_ct_adjustment_field_defs(): array
{
	return array(
		'non_deductible_entertainment' => array('label' => 'Non-deductible entertainment / hospitality', 'direction' => 'add', 'hint' => 'Add back input VAT blocked expenses & non-deductible hospitality'),
		'fines_penalties' => array('label' => 'Fines, penalties & non-deductible charges', 'direction' => 'add', 'hint' => 'Not deductible for CT'),
		'book_depreciation_excess' => array('label' => 'Book depreciation in excess of tax depreciation', 'direction' => 'add', 'hint' => 'Timing difference add-back'),
		'related_party_adjustments' => array('label' => 'Related-party / transfer pricing adjustments', 'direction' => 'add', 'hint' => 'Manual add-back per advisor'),
		'other_add_backs' => array('label' => 'Other CT add-backs', 'direction' => 'add', 'hint' => ''),
		'exempt_income' => array('label' => 'Exempt income (remove from profit)', 'direction' => 'deduct', 'hint' => 'Qualifying exempt income per CT law'),
		'foreign_branch_exemption' => array('label' => 'Foreign branch / qualifying income exemption', 'direction' => 'deduct', 'hint' => ''),
		'loss_carryforward' => array('label' => 'Tax loss carry-forward utilised', 'direction' => 'deduct', 'hint' => 'Prior-year losses offset'),
		'qualifying_donations' => array('label' => 'Qualifying donations / incentives', 'direction' => 'deduct', 'hint' => 'If allowed in CT return'),
		'other_deductions' => array('label' => 'Other CT deductions', 'direction' => 'deduct', 'hint' => ''),
	);
}

function epc_uae_ct_period_key(int $date_from, int $date_to): string
{
	return date('Ymd', $date_from) . '_' . date('Ymd', $date_to);
}

function epc_uae_ct_get_adjustments(PDO $db, int $date_from, int $date_to): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$key = epc_uae_ct_period_key($date_from, $date_to);
	$defs = epc_uae_ct_adjustment_field_defs();
	$out = array();
	foreach ($defs as $k => $def) {
		$out[$k] = array_merge($def, array('amount' => 0.0, 'notes' => ''));
	}
	$st = $db->prepare('SELECT * FROM `epc_uae_ct_adjustments` WHERE `period_key` = ?');
	$st->execute(array($key));
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$ak = (string)$row['adjustment_key'];
		if (isset($out[$ak])) {
			$out[$ak]['amount'] = round((float)$row['amount'], 2);
			$out[$ak]['notes'] = (string)($row['notes'] ?? '');
			$out[$ak]['direction'] = (string)$row['direction'];
		}
	}
	return array('period_key' => $key, 'fields' => $out);
}

function epc_uae_ct_save_adjustments(PDO $db, int $date_from, int $date_to, array $amounts, array $notes = array()): void
{
	epc_uae_tax_compliance_ensure_schema($db);
	$key = epc_uae_ct_period_key($date_from, $date_to);
	$defs = epc_uae_ct_adjustment_field_defs();
	$upd = $db->prepare(
		'INSERT INTO `epc_uae_ct_adjustments` (`period_key`, `adjustment_key`, `direction`, `amount`, `notes`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE `amount` = VALUES(`amount`), `notes` = VALUES(`notes`), `time_updated` = VALUES(`time_updated`)'
	);
	$now = time();
	foreach ($defs as $k => $def) {
		$amt = round(max(0, (float)($amounts[$k] ?? 0)), 2);
		$note = trim((string)($notes[$k] ?? ''));
		$upd->execute(array($key, $k, $def['direction'], $amt, $note, $now));
	}
}

/** Corporate tax provision on adjusted GL net profit (UAE CT 9% above threshold). */
function epc_uae_corporate_tax_report(PDO $db, int $date_from, int $date_to): array
{
	require_once __DIR__ . '/epc_erp_gl.php';
	epc_erp_gl_ensure_schema($db);

	$pl = epc_erp_gl_pl_report($db, $date_from, $date_to);
	$accounting_profit = round((float)$pl['net_profit'], 2);
	$enabled = epc_uae_corporate_tax_enabled($db);
	$rate = epc_uae_corporate_tax_rate_percent($db);
	$threshold = epc_uae_corporate_tax_threshold($db);

	$adjPack = epc_uae_ct_get_adjustments($db, $date_from, $date_to);
	$add_total = 0.0;
	$deduct_total = 0.0;
	$adjustment_lines = array();
	foreach ($adjPack['fields'] as $key => $field) {
		$amt = round((float)$field['amount'], 2);
		if ($amt <= 0) {
			continue;
		}
		$adjustment_lines[] = array(
			'key' => $key,
			'label' => $field['label'],
			'direction' => $field['direction'],
			'amount' => $amt,
			'notes' => $field['notes'] ?? '',
		);
		if ($field['direction'] === 'add') {
			$add_total += $amt;
		} else {
			$deduct_total += $amt;
		}
	}
	$add_total = round($add_total, 2);
	$deduct_total = round($deduct_total, 2);

	$adjusted_profit = round($accounting_profit + $add_total - $deduct_total, 2);
	$profit_above_threshold = round(max(0, $adjusted_profit - $threshold), 2);
	$provision = 0.0;
	if ($enabled && $profit_above_threshold > 0) {
		$provision = round($profit_above_threshold * ($rate / 100), 2);
	}
	$profit_after_ct = round($adjusted_profit - $provision, 2);

	return array(
		'date_from' => $date_from,
		'date_to' => $date_to,
		'period_key' => $adjPack['period_key'],
		'enabled' => $enabled,
		'rate_percent' => $rate,
		'small_business_threshold_aed' => $threshold,
		'total_revenue' => round((float)$pl['total_revenue'], 2),
		'total_expenses' => round((float)$pl['total_expenses'], 2),
		'accounting_profit' => $accounting_profit,
		'taxable_profit' => $accounting_profit,
		'ct_add_backs_total' => $add_total,
		'ct_deductions_total' => $deduct_total,
		'adjusted_taxable_profit' => $adjusted_profit,
		'adjustment_lines' => $adjustment_lines,
		'profit_above_threshold' => $profit_above_threshold,
		'corporate_tax_provision' => $provision,
		'profit_after_corporate_tax' => $profit_after_ct,
		'note' => 'Estimate: ' . $rate . '% on adjusted profit above AED ' . number_format($threshold, 0)
			. ' — file CT return on EmaraTax; confirm adjustments with advisor.',
	);
}

/** Single FTA source URL for legislation fetch (user requirement). */
function epc_uae_fta_legislation_url(): string
{
	return 'https://tax.gov.ae/en/legislation.aspx';
}

/** Static ERP compliance summaries keyed by legislation slug (merged with live FTA metadata). */
function epc_uae_tax_legislation_catalog(): array
{
	$patterns = array(
		'vat-decree-8-2017' => array(
			'tax_type' => 'vat',
			'summary' => 'Federal Decree-Law No. 8 of 2017 — UAE VAT law: 5% on taxable supplies, registration thresholds, tax invoices, input tax recovery, advances, exports (zero-rated), and penalties.',
			'erp_apply' => 'Map sales/purchases to VAT accounts 2100/1150; advance VAT on customer deposits; zero-rated export flag on e-invoices.',
		),
		'vat-executive-regulation' => array(
			'tax_type' => 'vat',
			'summary' => 'Cabinet Decision / Executive Regulation of VAT — operational rules for tax invoices, credit notes, record keeping, and designated zones.',
			'erp_apply' => 'Use Tax compliance invoice checklist (PINT-AE fields) before ASP submission.',
		),
		'tax-procedures' => array(
			'tax_type' => 'general',
			'summary' => 'Tax Procedures Law & Cabinet Decision No. 74 of 2023 — registration, tax periods, returns, assessments, reconsideration, voluntary disclosure, record retention, and administrative penalties.',
			'erp_apply' => 'Retain GL, e-invoice event log, and purchase tax invoices for FTA audit periods.',
		),
		'corporate-tax-47-2022' => array(
			'tax_type' => 'corporate_tax',
			'summary' => 'Federal Decree-Law No. 47 of 2022 — UAE Corporate Tax 9% on taxable income above small business relief threshold; free zone and foreign PE rules.',
			'erp_apply' => 'P&L provision estimate + CT adjustment fields (entertainment, exempt income, losses).',
		),
		'ct-executive-regulation' => array(
			'tax_type' => 'corporate_tax',
			'summary' => 'Corporate Tax executive regulations — taxable person definition, tax periods, return filing, and transfer pricing documentation.',
			'erp_apply' => 'Export adjusted profit from ERP P&L tab; file final return on EmaraTax.',
		),
		'excise-decree' => array(
			'tax_type' => 'excise',
			'summary' => 'Excise tax on specified goods (tobacco, energy drinks, carbonated/sweetened beverages) — registration, product listing, returns.',
			'erp_apply' => 'Tag excise SKUs in inventory; excise not auto-calculated in storefront VAT — track separately.',
		),
		'excise-executive' => array(
			'tax_type' => 'excise',
			'summary' => 'Excise executive decisions — rates, designated zones, stock movement, and refund mechanics.',
			'erp_apply' => 'Link import/customs docs for excise imports via Custom & Shipping module.',
		),
		'einvoicing-decision' => array(
			'tax_type' => 'vat',
			'summary' => 'E-invoicing / Peppol (PINT-AE) decisions — structured tax invoice exchange, TRN endpoints 0235:TIN, ASP mandate timelines.',
			'erp_apply' => 'E-Invoicing tab: validate TRN, mandatory fields, XML export, ASP API.',
		),
		'fta-clarification' => array(
			'tax_type' => 'general',
			'summary' => 'FTA decisions on clarifications, public guidance, and administrative penalties — binding process for disputed treatments.',
			'erp_apply' => 'Document non-standard VAT treatments in voucher legislation_ref notes.',
		),
	);
	return $patterns;
}

function epc_uae_tax_legislation_match_pattern(string $title, string $category): string
{
	$hay = strtolower($title . ' ' . $category);
	if (preg_match('/excise/i', $hay)) {
		return preg_match('/executive|cabinet|decision/i', $hay) ? 'excise-executive' : 'excise-decree';
	}
	if (preg_match('/corporate\s*tax|decree.?law\s*no\.?\s*47/i', $hay)) {
		return preg_match('/executive|cabinet/i', $hay) ? 'ct-executive-regulation' : 'corporate-tax-47-2022';
	}
	if (preg_match('/e.?invoice|peppol|pint/i', $hay)) {
		return 'einvoicing-decision';
	}
	if (preg_match('/tax\s*procedures|procedure/i', $hay)) {
		return 'tax-procedures';
	}
	if (preg_match('/clarification|fta\s*decision/i', $hay)) {
		return 'fta-clarification';
	}
	if (preg_match('/decree.?law\s*no\.?\s*8|value\s*added\s*tax|\bvat\b/i', $hay)) {
		return preg_match('/executive|cabinet/i', $hay) ? 'vat-executive-regulation' : 'vat-decree-8-2017';
	}
	return 'tax-procedures';
}

/** ERP modules touched by a legislation item (for compliance map). */
function epc_uae_tax_legislation_erp_modules(array $item): array
{
	$mods = array();
	$tt = (string)($item['tax_type'] ?? $item['tax_category'] ?? 'general');
	$pk = (string)($item['pattern_key'] ?? '');
	if ($tt === 'vat' || $pk === 'einvoicing-decision') {
		$mods[] = 'einvoice';
		$mods[] = 'vat_return';
	}
	if ($tt === 'vat') {
		$mods[] = 'purchases';
		$mods[] = 'gl_journals';
	}
	if ($tt === 'corporate_tax') {
		$mods[] = 'pl_ct';
		$mods[] = 'gl_journals';
	}
	if ($tt === 'excise') {
		$mods[] = 'inventory';
		$mods[] = 'customs';
	}
	if ($tt === 'procedures' || $tt === 'general') {
		$mods[] = 'gl_journals';
		$mods[] = 'records';
	}
	if ($pk === 'einvoicing-decision') {
		$mods[] = 'einvoice';
	}
	return array_values(array_unique($mods));
}

/** Normalize FTA PDF URL (relative /Datafolder paths). */
function epc_uae_fta_normalize_pdf_url(string $pdfUrl): string
{
	$pdfUrl = trim(str_replace('//Datafolder', '/Datafolder', $pdfUrl));
	if ($pdfUrl === '') {
		return '';
	}
	if (preg_match('#^https?://#i', $pdfUrl)) {
		return $pdfUrl;
	}
	if ($pdfUrl[0] === '/') {
		return 'https://tax.gov.ae' . $pdfUrl;
	}
	return 'https://tax.gov.ae/' . ltrim($pdfUrl, '/');
}

/** In-request cache for PDF text extraction (keyed by normalized URL). */
function epc_uae_fta_pdf_text_cache(string $pdfUrl, ?string $text = null): string
{
	static $cache = array();
	$key = epc_uae_fta_normalize_pdf_url($pdfUrl);
	if ($key === '') {
		return '';
	}
	if ($text !== null) {
		$cache[$key] = $text;
		return $text;
	}
	return (string)($cache[$key] ?? '');
}

/** Unescape PDF literal string contents. */
function epc_uae_fta_pdf_unescape_string(string $s): string
{
	$out = '';
	$len = strlen($s);
	for ($i = 0; $i < $len; $i++) {
		$c = $s[$i];
		if ($c !== '\\') {
			$out .= $c;
			continue;
		}
		if (++$i >= $len) {
			break;
		}
		$esc = $s[$i];
		if ($esc === 'n') {
			$out .= "\n";
		} elseif ($esc === 'r') {
			$out .= "\r";
		} elseif ($esc === 't') {
			$out .= "\t";
		} elseif ($esc === 'b') {
			$out .= "\b";
		} elseif ($esc === 'f') {
			$out .= "\f";
		} elseif ($esc === '(' || $esc === ')' || $esc === '\\') {
			$out .= $esc;
		} elseif ($esc >= '0' && $esc <= '7') {
			$oct = $esc;
			for ($j = 0; $j < 2 && ($i + 1) < $len && $s[$i + 1] >= '0' && $s[$i + 1] <= '7'; $j++) {
				$oct .= $s[++$i];
			}
			$out .= chr(octdec($oct));
		} else {
			$out .= $esc;
		}
	}
	return $out;
}

/** Best-effort text extraction from raw PDF bytes (pdftotext or stream parsing). */
function epc_uae_fta_extract_text_from_pdf_binary(string $binary): string
{
	if ($binary === '' || strncmp($binary, '%PDF', 4) !== 0) {
		return '';
	}
	$tmpIn = tempnam(sys_get_temp_dir(), 'epc_pdf_');
	if ($tmpIn) {
		$tmpOut = $tmpIn . '.txt';
		if (@file_put_contents($tmpIn, $binary) !== false) {
			@shell_exec('pdftotext -layout -q ' . escapeshellarg($tmpIn) . ' ' . escapeshellarg($tmpOut) . ' 2>/dev/null');
			if (is_file($tmpOut)) {
				$viaTool = trim((string)@file_get_contents($tmpOut));
				@unlink($tmpOut);
				@unlink($tmpIn);
				if ($viaTool !== '') {
					return $viaTool;
				}
			}
			@unlink($tmpIn);
		}
	}
	$parts = array();
	if (preg_match_all('/\((?:[^\\\\()]|\\\\.)*\)\s*(?:Tj|\'|\')/s', $binary, $tj)) {
		foreach ($tj[0] as $raw) {
			if (preg_match('/\((.*)\)\s*(?:Tj|\'|\')/s', $raw, $sm)) {
				$parts[] = epc_uae_fta_pdf_unescape_string($sm[1]);
			}
		}
	}
	if (preg_match_all('/<([0-9A-Fa-f\s]+)>\s*Tj/', $binary, $hex)) {
		foreach ($hex[1] as $h) {
			$h = preg_replace('/\s+/', '', $h);
			if ($h === '' || (strlen($h) % 2) !== 0) {
				continue;
			}
			$chunk = '';
			for ($i = 0; $i < strlen($h); $i += 2) {
				$chunk .= chr(hexdec(substr($h, $i, 2)));
			}
			$parts[] = $chunk;
		}
	}
	if (preg_match_all('/stream\r?\n(.*?)\r?\nendstream/s', $binary, $streams)) {
		foreach ($streams[1] as $stream) {
			$decoded = @gzuncompress($stream);
			if (!is_string($decoded) || $decoded === '') {
				$decoded = @gzuncompress(substr($stream, 2));
			}
			if (!is_string($decoded) || $decoded === '') {
				continue;
			}
			if (preg_match_all('/\((?:[^\\\\()]|\\\\.)*\)\s*(?:Tj|\'|\')/s', $decoded, $inner)) {
				foreach ($inner[0] as $raw) {
					if (preg_match('/\((.*)\)\s*(?:Tj|\'|\')/s', $raw, $sm)) {
						$parts[] = epc_uae_fta_pdf_unescape_string($sm[1]);
					}
				}
			}
		}
	}
	return trim(preg_replace('/\s+/', ' ', implode(' ', $parts)));
}

/** Trim PDF/plain text to first 2–3 sentences (max 800 chars). */
function epc_uae_fta_trim_pdf_excerpt(string $text, int $maxChars = 800): string
{
	$text = trim(preg_replace('/\s+/', ' ', html_entity_decode(strip_tags($text), ENT_QUOTES | ENT_HTML5, 'UTF-8')));
	if ($text === '') {
		return '';
	}
	$sentences = preg_split('/(?<=[.!?])\s+(?=[A-Z0-9"\(])/', $text);
	$excerpt = implode(' ', array_slice($sentences ?: array($text), 0, 3));
	if (strlen($excerpt) > $maxChars) {
		$excerpt = rtrim(substr($excerpt, 0, $maxChars - 3)) . '...';
	}
	return $excerpt;
}

/**
 * Fetch legislation PDF (or HTML wrapper) and return plain-text excerpt.
 * Falls back to empty string — caller must use title/category template.
 */
function epc_uae_fta_fetch_pdf_text(string $pdfUrl): string
{
	$url = epc_uae_fta_normalize_pdf_url($pdfUrl);
	if ($url === '') {
		return '';
	}
	$cached = epc_uae_fta_pdf_text_cache($url);
	if ($cached !== '') {
		return $cached;
	}
	$body = epc_uae_fta_http_get($url);
	if ($body === '') {
		return epc_uae_fta_pdf_text_cache($url, '');
	}
	$text = '';
	if (strncmp($body, '%PDF', 4) === 0) {
		$text = epc_uae_fta_trim_pdf_excerpt(epc_uae_fta_extract_text_from_pdf_binary($body));
	} elseif (stripos($body, '<html') !== false) {
		if (preg_match('/href=["\']([^"\']+\.pdf)["\']/i', $body, $pm)) {
			$nested = epc_uae_fta_fetch_pdf_text($pm[1]);
			if ($nested !== '') {
				return epc_uae_fta_pdf_text_cache($url, $nested);
			}
		}
		$plain = trim(preg_replace('/\s+/', ' ', strip_tags($body)));
		$text = epc_uae_fta_trim_pdf_excerpt($plain);
	}
	return epc_uae_fta_pdf_text_cache($url, $text);
}

/** Curated family bullets for overall summary cards. */
function epc_uae_tax_overall_family_bullets(): array
{
	return array(
		'vat' => array(
			'Federal Decree-Law No. 8 of 2017 — 5% VAT on taxable supplies in the UAE.',
			'Mandatory VAT registration when taxable supplies exceed AED 375,000 in 12 months.',
			'Tax invoices, credit notes (381), input VAT recovery, and export zero-rating (category Z).',
			'Output VAT on customer advance payments; credit against final tax invoice in ERP.',
			'Map sales/purchases to GL accounts 2100 (output) and 1150 (input); file returns on EmaraTax.',
		),
		'corporate_tax' => array(
			'Federal Decree-Law No. 47 of 2022 — 9% Corporate Tax on adjusted taxable income.',
			'Small business relief: no CT on taxable income up to AED 375,000 (threshold configurable in ERP).',
			'Free zone persons and qualifying income rules — confirm treatment with tax advisor.',
			'Transfer pricing and related-party adjustments via P&L CT add-back fields.',
			'Estimate CT provision on ERP P&L tab; file final return on EmaraTax.',
		),
		'excise' => array(
			'Federal Decree-Law No. 7 of 2017 — excise on tobacco, energy drinks, carbonated/sweetened beverages.',
			'Rates typically 50% (carbonated) or 100% (tobacco, energy drinks) — separate from 5% VAT.',
			'Excise registration, product listing, and returns on EmaraTax (not storefront VAT).',
			'Tag excise-affected SKUs in inventory; attach customs/import evidence where required.',
			'Stock movements in designated zones governed by excise executive decisions.',
		),
		'procedures' => array(
			'Cabinet Decision No. 74 of 2023 — Tax Procedures Law executive regulation.',
			'Registration, tax periods, return filing deadlines, and record retention (5–7 years).',
			'Voluntary disclosure, reconsideration, and administrative penalty framework.',
			'Retain GL journals, e-invoice event logs, and purchase tax invoices for FTA audit.',
			'Assessments and tax agent rules — document non-standard treatments in legislation_ref.',
		),
		'einvoicing' => array(
			'PINT-AE structured tax invoice standard — mandatory fields and XML schema.',
			'Seller TRN (15 digits), Peppol endpoint 0235:TIN, invoice type 380 / credit note 381.',
			'Exchange via accredited ASP on Peppol network — validate before submission.',
			'ERP E-Invoicing tab: TRN, mandatory fields, XML export, ASP API integration.',
			'Phased go-live per Cabinet/Ministerial decisions — monitor FTA announcements.',
		),
	);
}

/** Rule-based ERP summary paragraph from title, category, pattern, and optional PDF excerpt. */
function epc_uae_tax_legislation_build_summary(array $item): array
{
	$title = trim((string)($item['title'] ?? ''));
	$category = trim((string)($item['category'] ?? ''));
	$issueDate = trim((string)($item['issue_date'] ?? ''));
	$patterns = epc_uae_tax_legislation_catalog();
	$patternKey = epc_uae_tax_legislation_match_pattern($title, $category);
	$pat = $patterns[$patternKey] ?? $patterns['tax-procedures'];
	$base = (string)$pat['summary'];
	$hay = strtolower($title . ' ' . $category);

	$docType = 'legislation';
	if (preg_match('/federal\s+decree.?law/i', $hay)) {
		$docType = 'Federal Decree-Law';
	} elseif (preg_match('/cabinet\s+decision|ministerial\s+decision/i', $hay)) {
		$docType = 'Cabinet/Ministerial decision';
	} elseif (preg_match('/executive\s+regulation/i', $hay)) {
		$docType = 'Executive regulation';
	} elseif (preg_match('/clarification|public\s+clarification/i', $hay)) {
		$docType = 'FTA clarification';
	}

	$year = '';
	if (preg_match('/\b(20\d{2})\b/', $title, $ym)) {
		$year = $ym[1];
	} elseif ($issueDate !== '' && preg_match('/\b(20\d{2})\b/', $issueDate, $ym2)) {
		$year = $ym2[1];
	}

	$specific = '';
	if (preg_match('/amend/i', $hay)) {
		$specific = 'Amends prior provisions — review whether ERP tax codes, invoice templates, or CT adjustments need updating.';
	} elseif (preg_match('/penalt|fine|sanction/i', $hay)) {
		$specific = 'Covers penalties and enforcement — ensure audit trail and voluntary disclosure process in ERP records.';
	} elseif (preg_match('/refund|recovery|credit\s*note/i', $hay)) {
		$specific = 'Addresses refunds/credits — map credit notes (381) and input VAT recovery in purchases and VAT return.';
	} elseif (preg_match('/designated\s*zone|free\s*zone/i', $hay)) {
		$specific = 'Designated/free zone rules — verify supply location and zero-rated/exempt flags on affected transactions.';
	} elseif (preg_match('/reverse\s*charge|import/i', $hay)) {
		$specific = 'Import/reverse-charge treatment — set purchase VAT treatment to reverse_charge or import_rc where applicable.';
	}

	$pdfExcerpt = trim((string)($item['pdf_excerpt'] ?? ''));
	if ($pdfExcerpt === '' && !empty($item['_fetch_pdf'])) {
		$pdfUrl = trim((string)($item['pdf_url'] ?? ''));
		if ($pdfUrl !== '') {
			$pdfExcerpt = epc_uae_fta_fetch_pdf_text($pdfUrl);
		}
	}

	$parts = array();
	$parts[] = $docType . ($year !== '' ? ' (' . $year . ')' : '') . ': ' . ($title !== '' ? $title : 'FTA legislation') . '.';
	$parts[] = $base;
	if ($pdfExcerpt !== '') {
		$parts[] = 'Document excerpt: ' . $pdfExcerpt;
	}
	if ($specific !== '') {
		$parts[] = $specific;
	}
	$parts[] = (string)$pat['erp_apply'];
	if ($issueDate !== '') {
		$parts[] = 'FTA issue date: ' . $issueDate . '.';
	}
	if ($category !== '') {
		$parts[] = 'Category: ' . $category . '.';
	}

	$erpSummary = trim(preg_replace('/\s+/', ' ', implode(' ', array_filter($parts))));
	if ($erpSummary === '') {
		$erpSummary = 'UAE FTA legislation — apply ERP tax settings per advisor guidance. Category: ' . ($category !== '' ? $category : 'general') . '.';
	}
	if (!empty($item['is_new'])) {
		$erpSummary = '[NEW since last fetch] ' . $erpSummary;
	} elseif (!empty($item['is_changed']) || !empty($item['is_updated'])) {
		$erpSummary = '[UPDATED — issue date changed] ' . $erpSummary;
	}

	return array(
		'pattern_key' => $patternKey,
		'tax_type' => (string)$pat['tax_type'],
		'erp_summary' => $erpSummary,
		'erp_apply' => (string)$pat['erp_apply'],
		'pdf_excerpt' => $pdfExcerpt,
	);
}

/** Compliance checklist bullets — what the tenant must do in ERP for this item. */
function epc_uae_tax_legislation_build_compliance_actions(array $item): array
{
	$built = epc_uae_tax_legislation_build_summary($item);
	$tt = (string)($built['tax_type'] ?? 'general');
	$pk = (string)($built['pattern_key'] ?? '');
	$actions = array();
	$mods = epc_uae_tax_legislation_erp_modules(array_merge($item, $built));

	$moduleActions = array(
		'einvoice' => 'Validate tax invoice mandatory fields (PINT-AE) before ASP submission.',
		'vat_return' => 'Include affected supplies/deductions in UAE VAT return for the correct period.',
		'purchases' => 'Set purchase VAT treatment (standard, zero-rated, reverse charge) per this instrument.',
		'gl_journals' => 'Post to VAT/CT GL accounts (2100 output, 1150 input) with legislation_ref on vouchers.',
		'pl_ct' => 'Review P&L Corporate Tax provision and CT adjustment fields if CT-related.',
		'inventory' => 'Tag excise-affected SKUs; excise is tracked separately from storefront VAT.',
		'customs' => 'Attach customs/import evidence for excise or export zero-rating.',
		'records' => 'Retain PDF, ERP event log, and supporting docs for FTA record-keeping period.',
	);
	foreach ($mods as $m) {
		if (isset($moduleActions[$m])) {
			$actions[] = $moduleActions[$m];
		}
	}

	if ($tt === 'vat') {
		$actions[] = 'Apply 5% VAT on taxable supplies; zero-rate exports (category Z) where conditions met.';
		if ($pk === 'vat-decree-8-2017') {
			$actions[] = 'Record output VAT on customer advance payments; credit on final tax invoice.';
		}
	} elseif ($tt === 'corporate_tax') {
		$actions[] = 'Estimate 9% CT on adjusted profit above AED 375,000 threshold (P&L tab).';
	} elseif ($tt === 'excise') {
		$actions[] = 'Register excise products on EmaraTax; file excise returns outside storefront VAT.';
	} elseif ($pk === 'tax-procedures') {
		$actions[] = 'File returns and voluntary disclosures on EmaraTax by published deadlines.';
	} elseif ($pk === 'einvoicing-decision') {
		$actions[] = 'Configure seller TRN, Peppol endpoint 0235:TIN, and XML export for mandated go-live.';
	}

	$hay = strtolower((string)($item['title'] ?? ''));
	if (preg_match('/credit\s*note/i', $hay)) {
		$actions[] = 'Issue credit notes (type 381) referencing original invoice number and date.';
	}
	if (preg_match('/transfer\s*pric/i', $hay)) {
		$actions[] = 'Document related-party transactions; CT add-back field for transfer pricing adjustments.';
	}

	return array_values(array_unique($actions));
}

function epc_uae_tax_legislation_enrich_item(array $item): array
{
	$built = epc_uae_tax_legislation_build_summary($item);
	$item['pattern_key'] = $built['pattern_key'];
	$tc = epc_uae_fta_legislation_tax_category(
		(string)($item['title'] ?? ''),
		(string)($item['category'] ?? '')
	);
	$item['tax_category'] = $tc;
	$item['tax_type'] = in_array($tc, array('einvoicing', 'procedures'), true) ? $tc : $built['tax_type'];
	$item['erp_summary'] = $built['erp_summary'];
	$item['summary'] = $built['erp_summary'];
	$item['pdf_excerpt'] = (string)($built['pdf_excerpt'] ?? '');
	$item['erp_apply'] = $built['erp_apply'];
	$item['compliance_actions'] = epc_uae_tax_legislation_build_compliance_actions($item);
	$item['erp_modules'] = epc_uae_tax_legislation_erp_modules($item);
	if (!empty($item['is_changed'])) {
		$item['is_updated'] = true;
	}
	return $item;
}

/** Persist enriched legislation rows (upsert by item_key). */
function epc_uae_tax_legislation_sync_items(PDO $db, array $items): int
{
	epc_uae_tax_compliance_ensure_schema($db);
	$upd = $db->prepare(
		'INSERT INTO `epc_uae_tax_legislation_items`
		(`item_key`, `slug`, `title`, `issue_date`, `publish_date`, `category`, `tax_category`, `pdf_url`,
		 `erp_summary`, `compliance_actions_json`, `pattern_key`, `is_new`, `is_updated`, `time_synced`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE
		 `slug` = VALUES(`slug`), `title` = VALUES(`title`), `issue_date` = VALUES(`issue_date`),
		 `publish_date` = VALUES(`publish_date`), `category` = VALUES(`category`), `tax_category` = VALUES(`tax_category`),
		 `pdf_url` = VALUES(`pdf_url`), `erp_summary` = VALUES(`erp_summary`),
		 `compliance_actions_json` = VALUES(`compliance_actions_json`), `pattern_key` = VALUES(`pattern_key`),
		 `is_new` = VALUES(`is_new`), `is_updated` = VALUES(`is_updated`), `time_synced` = VALUES(`time_synced`)'
	);
	$now = time();
	$n = 0;
	foreach ($items as $item) {
		$key = (string)($item['item_key'] ?? '');
		if ($key === '') {
			continue;
		}
		$actions = $item['compliance_actions'] ?? array();
		if (!is_array($actions)) {
			$actions = array();
		}
		$upd->execute(array(
			$key,
			(string)($item['slug'] ?? ''),
			(string)($item['title'] ?? ''),
			(string)($item['issue_date'] ?? ''),
			(string)($item['publish_date'] ?? ''),
			(string)($item['category'] ?? ''),
			(string)($item['tax_category'] ?? $item['tax_type'] ?? 'general'),
			(string)($item['pdf_url'] ?? ''),
			(string)($item['erp_summary'] ?? $item['summary'] ?? ''),
			json_encode($actions, JSON_UNESCAPED_UNICODE),
			(string)($item['pattern_key'] ?? ''),
			!empty($item['is_new']) ? 1 : 0,
			(!empty($item['is_updated']) || !empty($item['is_changed'])) ? 1 : 0,
			$now,
		));
		$n++;
	}
	return $n;
}

/** Aggregate overall summaries by law family for dashboard cards. */
function epc_uae_tax_overall_summaries(PDO $db, ?array $legislation = null): array
{
	if ($legislation === null) {
		$cached = epc_uae_fta_get_cached_legislation($db);
		$legislation = $cached['legislation'] ?? $cached['items'] ?? array();
	}
	$families = array(
		'vat' => array(
			'key' => 'vat',
			'title' => 'Value Added Tax (VAT)',
			'rate_label' => '5%',
			'rate_hint' => 'Standard rate on taxable supplies',
			'icon' => 'fa-percent',
			'color' => '#2563eb',
		),
		'corporate_tax' => array(
			'key' => 'corporate_tax',
			'title' => 'Corporate Tax (CT)',
			'rate_label' => '9%',
			'rate_hint' => 'On taxable income above AED 375,000',
			'icon' => 'fa-building',
			'color' => '#7c3aed',
		),
		'excise' => array(
			'key' => 'excise',
			'title' => 'Excise Tax',
			'rate_label' => '50–100%',
			'rate_hint' => 'Tobacco, energy drinks, sweetened beverages',
			'icon' => 'fa-flask',
			'color' => '#ea580c',
		),
		'procedures' => array(
			'key' => 'procedures',
			'title' => 'Tax Procedures & Penalties',
			'rate_label' => '—',
			'rate_hint' => 'Registration, returns, assessments, record retention',
			'icon' => 'fa-gavel',
			'color' => '#64748b',
		),
		'einvoicing' => array(
			'key' => 'einvoicing',
			'title' => 'E-Invoicing (PINT-AE)',
			'rate_label' => 'ASP',
			'rate_hint' => 'Structured tax invoice exchange via Peppol',
			'icon' => 'fa-file-code-o',
			'color' => '#059669',
		),
	);
	$counts = array_fill_keys(array_keys($families), 0);
	$recent = array_fill_keys(array_keys($families), array());
	$newCounts = array_fill_keys(array_keys($families), 0);

	foreach ($legislation as $leg) {
		$leg = epc_uae_tax_legislation_enrich_item($leg);
		$pk = (string)($leg['pattern_key'] ?? '');
		$tt = (string)($leg['tax_type'] ?? $leg['tax_category'] ?? 'general');
		$family = 'procedures';
		if ($pk === 'einvoicing-decision') {
			$family = 'einvoicing';
		} elseif ($tt === 'vat') {
			$family = 'vat';
		} elseif ($tt === 'corporate_tax') {
			$family = 'corporate_tax';
		} elseif ($tt === 'excise') {
			$family = 'excise';
		} elseif ($pk === 'tax-procedures' || $tt === 'general' || $tt === 'procedures') {
			$family = 'procedures';
		}
		$counts[$family]++;
		if (!empty($leg['is_new'])) {
			$newCounts[$family]++;
		}
		if (count($recent[$family]) < 5 && (!empty($leg['is_new']) || !empty($leg['is_updated']) || !empty($leg['is_changed']))) {
			$recent[$family][] = array(
				'title' => (string)($leg['title'] ?? ''),
				'issue_date' => (string)($leg['issue_date'] ?? ''),
				'is_new' => !empty($leg['is_new']),
			);
		}
	}

	$out = array();
	$familyBullets = epc_uae_tax_overall_family_bullets();
	foreach ($families as $fk => $meta) {
		$cnt = $counts[$fk];
		$bullets = $familyBullets[$fk] ?? array('Review FTA instruments in the legislation library.');
		$bullets = array_slice($bullets, 0, 5);
		array_unshift($bullets, $cnt . ' FTA instrument(s) in the legislation library.');
		if ($newCounts[$fk] > 0) {
			$bullets[] = $newCounts[$fk] . ' new since last fetch — expand rows below for ERP checklist.';
		}
		$summaryParts = $bullets;
		$out[$fk] = array_merge($meta, array(
			'item_count' => $cnt,
			'new_count' => $newCounts[$fk],
			'summary' => implode(' ', $summaryParts),
			'bullets' => $bullets,
			'recent_changes' => $recent[$fk],
		));
	}
	return $out;
}

/** Chart/timeline stats for legislation panel (pure PHP — no JS lib required). */
function epc_uae_tax_legislation_chart_stats(array $legislation): array
{
	$catCounts = array('vat' => 0, 'corporate_tax' => 0, 'excise' => 0, 'procedures' => 0, 'einvoicing' => 0, 'general' => 0);
	$byYear = array();
	$moduleHits = array(
		'einvoice' => 0, 'vat_return' => 0, 'pl_ct' => 0, 'purchases' => 0, 'gl_journals' => 0, 'inventory' => 0,
	);
	foreach ($legislation as $leg) {
		$leg = epc_uae_tax_legislation_enrich_item($leg);
		$pk = (string)($leg['pattern_key'] ?? '');
		$tt = (string)($leg['tax_type'] ?? $leg['tax_category'] ?? 'general');
		$bucket = 'general';
		if ($pk === 'einvoicing-decision') {
			$bucket = 'einvoicing';
		} elseif (isset($catCounts[$tt])) {
			$bucket = $tt;
		} elseif ($pk === 'tax-procedures') {
			$bucket = 'procedures';
		}
		$catCounts[$bucket]++;
		$yr = 'Unknown';
		if (preg_match('/\b(20\d{2})\b/', (string)($leg['issue_date'] ?? ''), $ym)) {
			$yr = $ym[1];
		} elseif (preg_match('/\b(20\d{2})\b/', (string)($leg['title'] ?? ''), $ym2)) {
			$yr = $ym2[1];
		}
		if (!isset($byYear[$yr])) {
			$byYear[$yr] = 0;
		}
		$byYear[$yr]++;
		foreach ($leg['erp_modules'] ?? array() as $mod) {
			if (isset($moduleHits[$mod])) {
				$moduleHits[$mod]++;
			}
		}
	}
	krsort($byYear);
	$total = max(1, array_sum($catCounts));
	$catPct = array();
	foreach ($catCounts as $k => $v) {
		if ($v > 0) {
			$catPct[$k] = round(100 * $v / $total, 1);
		}
	}
	return array(
		'category_counts' => $catCounts,
		'category_percent' => $catPct,
		'timeline_by_year' => $byYear,
		'erp_module_hits' => $moduleHits,
		'total' => array_sum($catCounts),
	);
}

function epc_uae_tax_legislation_rehydrate_payload(array $payload): array
{
	$legs = $payload['legislation'] ?? $payload['items'] ?? array();
	if (empty($legs)) {
		return $payload;
	}
	$rebuilt = array();
	$anyChanged = false;
	foreach ($legs as $leg) {
		$summary = trim((string)($leg['erp_summary'] ?? $leg['summary'] ?? ''));
		$actions = $leg['compliance_actions'] ?? null;
		if (!is_array($actions)) {
			$decoded = json_decode((string)$actions, true);
			$actions = is_array($decoded) ? $decoded : array();
		}
		if ($summary === '' || empty($actions)) {
			$rebuilt[] = epc_uae_tax_legislation_enrich_item($leg);
			$anyChanged = true;
		} else {
			$rebuilt[] = $leg;
		}
	}
	if ($anyChanged) {
		$payload['legislation'] = $rebuilt;
		$payload['items'] = $rebuilt;
	}
	return $payload;
}

function epc_uae_tax_legislation_merge_catalog(array $fetchedItems): array
{
	$out = array();
	foreach ($fetchedItems as $item) {
		$out[] = epc_uae_tax_legislation_enrich_item($item);
	}
	usort($out, function ($a, $b) {
		$da = strtotime((string)($a['issue_date'] ?? '')) ?: 0;
		$db = strtotime((string)($b['issue_date'] ?? '')) ?: 0;
		if ($da === $db) {
			return strcmp((string)$a['title'], (string)$b['title']);
		}
		return $db <=> $da;
	});
	return $out;
}

/**
 * Regenerate ERP summaries for all legislation rows (DB + cache).
 * @param bool $fetchPdfs When true, curl each PDF for excerpt (slower; use for backfill).
 */
function epc_uae_tax_legislation_backfill_summaries(PDO $db, bool $fetchPdfs = false): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$items = array();
	$st = $db->query('SELECT * FROM `epc_uae_tax_legislation_items` ORDER BY `issue_date` DESC, `id` ASC');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$items[] = array(
			'item_key' => (string)($row['item_key'] ?? ''),
			'slug' => (string)($row['slug'] ?? ''),
			'title' => (string)($row['title'] ?? ''),
			'issue_date' => (string)($row['issue_date'] ?? ''),
			'publish_date' => (string)($row['publish_date'] ?? ''),
			'category' => (string)($row['category'] ?? ''),
			'tax_category' => (string)($row['tax_category'] ?? 'general'),
			'pdf_url' => (string)($row['pdf_url'] ?? ''),
			'is_new' => !empty($row['is_new']),
			'is_updated' => !empty($row['is_updated']),
			'_fetch_pdf' => $fetchPdfs,
		);
	}
	if (empty($items)) {
		$cached = epc_uae_fta_get_cached_legislation($db);
		$items = $cached['legislation'] ?? $cached['items'] ?? array();
		foreach ($items as &$it) {
			$it['_fetch_pdf'] = $fetchPdfs;
		}
		unset($it);
	}
	$enriched = array();
	$pdfHits = 0;
	foreach ($items as $item) {
		$item['_fetch_pdf'] = $fetchPdfs;
		$row = epc_uae_tax_legislation_enrich_item($item);
		if (!empty($row['pdf_excerpt']) || (!empty($item['pdf_excerpt']))) {
			$pdfHits++;
		}
		$enriched[] = $row;
	}
	$synced = epc_uae_tax_legislation_sync_items($db, $enriched);
	$overallSummaries = epc_uae_tax_overall_summaries($db, $enriched);
	$chartStats = epc_uae_tax_legislation_chart_stats($enriched);

	$cacheKey = 'fta_legislation_index';
	$stCache = $db->prepare('SELECT * FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1');
	$stCache->execute(array($cacheKey));
	$cached = $stCache->fetch(PDO::FETCH_ASSOC);
	$payload = array();
	if ($cached) {
		$payload = json_decode((string)$cached['payload_json'], true) ?: array();
	}
	$payload['legislation'] = $enriched;
	$payload['items'] = $enriched;
	$payload['overall_summaries'] = $overallSummaries;
	$payload['chart_stats'] = $chartStats;
	$payload['items_synced'] = $synced;
	$payload['summaries_backfilled_at'] = time();
	$payload['summaries_backfilled_label'] = date('d M Y H:i T');
	$now = time();
	$json = json_encode($payload, JSON_UNESCAPED_UNICODE);
	$url = epc_uae_fta_legislation_url();
	foreach (array('fta_legislation_index', 'fta_site_updates') as $ck) {
		$db->prepare(
			'INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `source_url`, `time_fetched`)
			VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `time_fetched` = VALUES(`time_fetched`)'
		)->execute(array($ck, $json, $url, $now));
	}
	$db->prepare(
		'INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `source_url`, `time_fetched`)
		VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `time_fetched` = VALUES(`time_fetched`)'
	)->execute(array('fta_overall_tax_summary', json_encode($overallSummaries, JSON_UNESCAPED_UNICODE), $url, $now));

	$samples = array();
	foreach (array_slice($enriched, 0, 3) as $s) {
		$samples[] = array(
			'title' => (string)($s['title'] ?? ''),
			'erp_summary' => (string)($s['erp_summary'] ?? ''),
			'compliance_actions' => array_slice((array)($s['compliance_actions'] ?? array()), 0, 3),
		);
	}
	$vatCard = $overallSummaries['vat'] ?? array();

	return array(
		'ok' => true,
		'status' => true,
		'updated' => count($enriched),
		'synced' => $synced,
		'pdf_excerpts' => $pdfHits,
		'fetch_pdfs' => $fetchPdfs,
		'samples' => $samples,
		'vat_overall' => array(
			'title' => (string)($vatCard['title'] ?? 'VAT'),
			'bullets' => (array)($vatCard['bullets'] ?? array()),
			'summary' => (string)($vatCard['summary'] ?? ''),
		),
		'message' => 'Regenerated summaries for ' . count($enriched) . ' legislation item(s)'
			. ($fetchPdfs ? (' — ' . $pdfHits . ' PDF excerpt(s)') : '') . '.',
	);
}

/** Mark is_new / is_changed on raw FTA rows vs previous fetch snapshot (by item_key + issue_date). */
function epc_uae_tax_legislation_mark_diff_flags(array &$items, array $prevKeys): array
{
	$newSince = array();
	$changedSince = array();
	foreach ($items as &$item) {
		$key = (string)($item['item_key'] ?? '');
		if ($key === '') {
			continue;
		}
		if (!isset($prevKeys[$key])) {
			$item['is_new'] = true;
			$newSince[] = $item;
		} elseif ($prevKeys[$key] !== (string)($item['issue_date'] ?? '')) {
			$item['is_changed'] = true;
			$item['is_updated'] = true;
			$changedSince[] = $item;
		}
	}
	unset($item);
	return array('new' => $newSince, 'changed' => $changedSince);
}

/** True when cached legislation rows lack ERP summaries or compliance checklists. */
function epc_uae_tax_legislation_summaries_need_regen(array $legislation): bool
{
	if (empty($legislation)) {
		return false;
	}
	foreach ($legislation as $leg) {
		if (trim((string)($leg['erp_summary'] ?? $leg['summary'] ?? '')) === '') {
			return true;
		}
		$actions = $leg['compliance_actions'] ?? null;
		if (!is_array($actions)) {
			$decoded = json_decode((string)$actions, true);
			$actions = is_array($decoded) ? $decoded : array();
		}
		if (empty($actions)) {
			return true;
		}
	}
	return false;
}

/**
 * Merge catalog, force-regenerate summaries for new/changed rows, rebuild family + chart aggregates.
 */
function epc_uae_tax_legislation_build_fetch_payload(PDO $db, array $raw, array $diff, array $fetchMeta, string $url): array
{
	$legislation = epc_uae_tax_legislation_merge_catalog($raw);
	$summariesRegenerated = 0;
	foreach ($legislation as &$leg) {
		if (!empty($leg['is_new']) || !empty($leg['is_changed']) || !empty($leg['is_updated'])) {
			$leg = epc_uae_tax_legislation_enrich_item($leg);
			$summariesRegenerated++;
		}
	}
	unset($leg);

	$overallSummaries = epc_uae_tax_overall_summaries($db, $legislation);
	$chartStats = epc_uae_tax_legislation_chart_stats($legislation);
	$synced = epc_uae_tax_legislation_sync_items($db, $legislation);
	$newSince = epc_uae_tax_legislation_merge_catalog($diff['new']);
	$changedSince = epc_uae_tax_legislation_merge_catalog($diff['changed']);
	$newCount = count($diff['new']);
	$changedCount = count($diff['changed']);

	return array(
		'ok' => !empty($legislation),
		'status' => !empty($legislation),
		'time_fetched' => time(),
		'time_fetched_label' => date('d M Y H:i T'),
		'source_url' => $url,
		'total_reported' => (int)($fetchMeta['total_reported'] ?? 0),
		'page_count' => (int)($fetchMeta['page_count'] ?? 0),
		'legislation' => $legislation,
		'items' => $legislation,
		'overall_summaries' => $overallSummaries,
		'chart_stats' => $chartStats,
		'items_synced' => $synced,
		'new_since_last' => $newSince,
		'changed_since_last' => $changedSince,
		'new_count' => $newCount,
		'changed_count' => $changedCount,
		'summaries_regenerated' => $summariesRegenerated,
		'summaries_need_regen' => false,
		'errors' => $fetchMeta['errors'] ?? array(),
		'message' => empty($legislation)
			? ('Could not parse legislation from ' . $url . '. ' . implode('; ', (array)($fetchMeta['errors'] ?? array())))
			: ('Fetched ' . count($legislation) . ' legislation item(s) from legislation.aspx'
				. ($newCount ? (' — ' . $newCount . ' new since last fetch') : '')
				. ($changedCount ? (' — ' . $changedCount . ' updated') : '')
				. ($summariesRegenerated ? (' — regenerated ' . $summariesRegenerated . ' summary(ies)') : '') . '.'),
	);
}

/**
 * Daily cron entry: fetch legislation (respects cache unless force=1).
 */
function epc_uae_fta_cron_fetch_legislation(PDO $db, bool $force = false): array
{
	$payload = epc_uae_fta_fetch_legislation_updates($db, $force);
	return array(
		'ok' => (bool)($payload['ok'] ?? $payload['status'] ?? false),
		'timestamp' => time(),
		'time_fetched_label' => (string)($payload['time_fetched_label'] ?? ''),
		'legislation_count' => count($payload['legislation'] ?? array()),
		'new_count' => (int)($payload['new_count'] ?? count($payload['new_since_last'] ?? array())),
		'changed_count' => (int)($payload['changed_count'] ?? count($payload['changed_since_last'] ?? array())),
		'summaries_regenerated' => (int)($payload['summaries_regenerated'] ?? 0),
		'message' => (string)($payload['message'] ?? ''),
		'errors' => $payload['errors'] ?? array(),
	);
}

/**
 * Fetch ALL legislation entries from legislation.aspx only (paginated ASP.NET postback).
 */
function epc_uae_fta_fetch_legislation_updates(PDO $db, bool $force = false): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$url = epc_uae_fta_legislation_url();
	$cacheKey = 'fta_legislation_index';
	$st = $db->prepare('SELECT * FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1');
	$st->execute(array($cacheKey));
	$cached = $st->fetch(PDO::FETCH_ASSOC);
	$maxAge = 86400;
	if (!$force && $cached && (time() - (int)$cached['time_fetched']) < $maxAge) {
		$payload = json_decode((string)$cached['payload_json'], true);
		if (is_array($payload) && !empty($payload['legislation'])) {
			return $payload;
		}
	}

	$prevSt = $db->prepare('SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1');
	$prevSt->execute(array($cacheKey . '_prev'));
	$prevRow = $prevSt->fetch(PDO::FETCH_ASSOC);
	$prevKeys = array();
	if ($prevRow) {
		$prevPayload = json_decode((string)$prevRow['payload_json'], true);
		if (is_array($prevPayload) && !empty($prevPayload['legislation'])) {
			foreach ($prevPayload['legislation'] as $p) {
				$prevKeys[(string)($p['item_key'] ?? $p['slug'] ?? '')] = (string)($p['issue_date'] ?? '');
			}
		}
	}

	$fetch = epc_uae_fta_fetch_all_legislation_pages($url);
	$raw = $fetch['items'];
	$diff = epc_uae_tax_legislation_mark_diff_flags($raw, $prevKeys);
	$payload = epc_uae_tax_legislation_build_fetch_payload($db, $raw, $diff, $fetch, $url);
	$overallSummaries = $payload['overall_summaries'];

	if ($cached) {
		$db->prepare(
			'INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `source_url`, `time_fetched`)
			VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `source_url` = VALUES(`source_url`), `time_fetched` = VALUES(`time_fetched`)'
		)->execute(array($cacheKey . '_prev', (string)$cached['payload_json'], $url, (int)$cached['time_fetched']));
	}

	$db->prepare(
		'INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `source_url`, `time_fetched`)
		VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `source_url` = VALUES(`source_url`), `time_fetched` = VALUES(`time_fetched`)'
	)->execute(array($cacheKey, json_encode($payload, JSON_UNESCAPED_UNICODE), $url, time()));

	// Legacy alias
	$db->prepare(
		'INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `source_url`, `time_fetched`)
		VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `source_url` = VALUES(`source_url`), `time_fetched` = VALUES(`time_fetched`)'
	)->execute(array('fta_site_updates', json_encode($payload, JSON_UNESCAPED_UNICODE), $url, time()));

	$db->prepare(
		'INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `source_url`, `time_fetched`)
		VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `source_url` = VALUES(`source_url`), `time_fetched` = VALUES(`time_fetched`)'
	)->execute(array('fta_overall_tax_summary', json_encode($overallSummaries, JSON_UNESCAPED_UNICODE), $url, time()));

	if (!empty($legislation)) {
		require_once __DIR__ . '/epc_uae_tax_knowledge.php';
		epc_uae_tax_legislation_seed_kb($db, $legislation);
	}

	return $payload;
}

function epc_uae_fta_fetch_all_legislation_pages(string $url): array
{
	$errors = array();
	$html = epc_uae_fta_http_get($url);
	if ($html === '') {
		return array('items' => array(), 'errors' => array('GET failed for ' . $url), 'total_reported' => 0, 'page_count' => 0);
	}
	$totalReported = 0;
	if (preg_match('/(\d+)\s+Items found/i', $html, $tm)) {
		$totalReported = (int)$tm[1];
	}
	$pageCount = 1;
	if (preg_match('/"_pageCount":(\d+)/', $html, $pm)) {
		$pageCount = max(1, (int)$pm[1]);
	}
	$all = array();
	$seen = array();
	$page = 1;
	foreach (epc_uae_fta_parse_legislation_list_html($html) as $item) {
		$k = (string)$item['item_key'];
		if ($k !== '' && !isset($seen[$k])) {
			$seen[$k] = true;
			$all[] = $item;
		}
	}
	$fields = epc_uae_fta_extract_form_fields($html);
	$nextTarget = 'ctl00$ctrlContentArea$ctlOpenData$pgList$ctl00$LinkButtonNext';
	while ($page < $pageCount) {
		$fields['__EVENTTARGET'] = $nextTarget;
		$fields['__EVENTARGUMENT'] = '';
		unset($fields['ctl00$ctrlContentArea$ctlOpenData$btnSearch']);
		$html = epc_uae_fta_http_post($url, $fields);
		if ($html === '') {
			$errors[] = 'POST page ' . ($page + 1) . ' failed';
			break;
		}
		$page++;
		foreach (epc_uae_fta_parse_legislation_list_html($html) as $item) {
			$k = (string)$item['item_key'];
			if ($k !== '' && !isset($seen[$k])) {
				$seen[$k] = true;
				$all[] = $item;
			}
		}
		$fields = epc_uae_fta_extract_form_fields($html);
	}
	return array(
		'items' => $all,
		'errors' => $errors,
		'total_reported' => $totalReported,
		'page_count' => $pageCount,
	);
}

function epc_uae_fta_parse_legislation_list_html(string $html): array
{
	$items = array();
	$start = strpos($html, 'headerTable');
	$end = strpos($html, 'ctrlContentArea_ctlOpenData_dvPager');
	if ($end === false) {
		$end = strpos($html, 'dvPager');
	}
	$chunk = ($start !== false && $end !== false && $end > $start) ? substr($html, $start, $end - $start) : $html;
	if (!preg_match_all('/<span\s+class="tag_category">([^<]+)<\/span>/i', $chunk, $cats, PREG_OFFSET_CAPTURE)) {
		return $items;
	}
	foreach ($cats[1] as $idx => $catMatch) {
		$cat = trim(html_entity_decode($catMatch[0], ENT_QUOTES | ENT_HTML5, 'UTF-8'));
		$catPos = $cats[0][$idx][1];
		$blockStart = max(0, $catPos - 3500);
		$block = substr($chunk, $blockStart, ($catPos - $blockStart) + 1200);
		$title = '';
		if (preg_match('/<div class="d-flex">\s*(.*?)(?:<div id=|<div class="clear5")/is', $block, $tm)) {
			$title = trim(preg_replace('/\s+/', ' ', strip_tags($tm[1])));
		}
		if ($title === '' || strlen($title) < 8) {
			continue;
		}
		$issueDate = '';
		$publishDate = '';
		if (preg_match('/Issue Date\s*:<\/span>\s*<span[^>]*class="lastmodifiedDate">([^<]+)<\/span>/i', $block, $im)) {
			$issueDate = trim($im[1]);
		}
		if (preg_match('/Publish Date\s*:<\/span>\s*<span[^>]*class="lastmodifiedDate">([^<]+)<\/span>/i', $block, $pm)) {
			$publishDate = trim($pm[1]);
		}
		$pdfUrl = '';
		$tail = substr($chunk, $catPos, 1200);
		if (preg_match('/<a href="([^"]+\.pdf)"/i', $tail, $pdfm)) {
			$pdfUrl = str_replace('//Datafolder', '/Datafolder', html_entity_decode($pdfm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8'));
		}
		$isNew = (stripos($block, 'class="newicon"') !== false || stripos($block, '>New<') !== false);
		$slugBase = preg_replace('/[^a-z0-9]+/', '-', strtolower($title . '-' . $issueDate . '-' . $publishDate));
		$slugBase = trim($slugBase, '-');
		if (strlen($slugBase) > 100) {
			$slugBase = substr($slugBase, 0, 100);
		}
		$slug = $slugBase;
		if ($pdfUrl !== '') {
			$pdfPart = preg_replace('/[^a-z0-9]+/', '-', strtolower(basename($pdfUrl)));
			$slug = trim(substr($pdfPart . '-' . $slugBase, 0, 120), '-');
		}
		$itemKey = $pdfUrl !== '' ? $pdfUrl : ($title . '|' . $issueDate . '|' . $publishDate);
		$items[] = array(
			'slug' => $slug,
			'item_key' => $itemKey,
			'title' => $title,
			'issue_date' => $issueDate,
			'publish_date' => $publishDate,
			'category' => $cat,
			'tax_category' => epc_uae_fta_legislation_tax_category($title, $cat),
			'pdf_url' => $pdfUrl,
			'href' => $pdfUrl !== '' ? $pdfUrl : epc_uae_fta_legislation_url(),
			'fta_new_badge' => $isNew,
		);
	}
	return $items;
}

function epc_uae_fta_legislation_tax_category(string $title, string $category): string
{
	$hay = strtolower($title . ' ' . $category);
	if (preg_match('/excise/i', $hay)) {
		return 'excise';
	}
	if (preg_match('/corporate\s*tax|decree.?law\s*no\.?\s*47/i', $hay)) {
		return 'corporate_tax';
	}
	if (preg_match('/e.?invoice|peppol|pint/i', $hay)) {
		return 'einvoicing';
	}
	if (preg_match('/tax\s*procedures|procedure/i', $hay)) {
		return 'procedures';
	}
	if (preg_match('/value\s*added|vat|decree.?law\s*no\.?\s*8/i', $hay)) {
		return 'vat';
	}
	return 'general';
}

function epc_uae_fta_extract_form_fields(string $html): array
{
	$fields = array();
	if (!preg_match_all('/<input[^>]+name="([^"]+)"[^>]*>/i', $html, $matches, PREG_SET_ORDER)) {
		return $fields;
	}
	foreach ($matches as $m) {
		$tag = $m[0];
		$name = $m[1];
		if (preg_match('/type\s*=\s*"(?:submit|button|image)"/i', $tag)) {
			continue;
		}
		$val = '';
		if (preg_match('/value="([^"]*)"/i', $tag, $vm)) {
			$val = $vm[1];
		}
		$fields[$name] = $val;
	}
	return $fields;
}

function epc_uae_fta_get_cached_legislation(PDO $db): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	foreach (array('fta_legislation_index', 'fta_site_updates') as $cacheKey) {
		$st = $db->prepare('SELECT * FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1');
		$st->execute(array($cacheKey));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			$payload = json_decode((string)$row['payload_json'], true);
			if (is_array($payload)) {
				if (empty($payload['legislation']) && !empty($payload['items'])) {
					$payload['legislation'] = $payload['items'];
				}
				$payload = epc_uae_tax_legislation_rehydrate_payload($payload);
				if (empty($payload['overall_summaries'])) {
					$osSt = $db->prepare('SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1');
					$osSt->execute(array('fta_overall_tax_summary'));
					$osRow = $osSt->fetch(PDO::FETCH_ASSOC);
					if ($osRow) {
						$os = json_decode((string)$osRow['payload_json'], true);
						if (is_array($os)) {
							$payload['overall_summaries'] = $os;
						}
					}
				}
				if (empty($payload['overall_summaries']) && !empty($payload['legislation'])) {
					$payload['overall_summaries'] = epc_uae_tax_overall_summaries($db, $payload['legislation']);
					$payload['chart_stats'] = epc_uae_tax_legislation_chart_stats($payload['legislation']);
				}
				if (empty($payload['chart_stats']) && !empty($payload['legislation'])) {
					$payload['chart_stats'] = epc_uae_tax_legislation_chart_stats($payload['legislation']);
				}
				if (!isset($payload['summaries_need_regen']) && !empty($payload['legislation'])) {
					$payload['summaries_need_regen'] = epc_uae_tax_legislation_summaries_need_regen($payload['legislation']);
				}
				if (!isset($payload['new_count']) && !empty($payload['new_since_last'])) {
					$payload['new_count'] = count($payload['new_since_last']);
				}
				return $payload;
			}
		}
	}
	return array(
		'ok' => false,
		'legislation' => array(),
		'items' => array(),
		'new_since_last' => array(),
		'overall_summaries' => epc_uae_tax_overall_summaries($db, array()),
		'chart_stats' => epc_uae_tax_legislation_chart_stats(array()),
		'message' => 'No cache — click Fetch legislation updates',
	);
}

function epc_uae_fta_http_get(string $url): string
{
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_TIMEOUT => 45,
			CURLOPT_SSL_VERIFYPEER => true,
			CURLOPT_USERAGENT => 'ePartsCart-ERP-UAE-Tax-Compliance/2.0',
		));
		$body = curl_exec($ch);
		curl_close($ch);
		if (is_string($body) && $body !== '') {
			return $body;
		}
	}
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => 45,
			'user_agent' => 'ePartsCart-ERP-UAE-Tax-Compliance/2.0',
		),
		'ssl' => array('verify_peer' => true, 'verify_peer_name' => true),
	));
	$body = @file_get_contents($url, false, $ctx);
	return is_string($body) ? $body : '';
}

function epc_uae_fta_http_post(string $url, array $fields): string
{
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_POST => true,
			CURLOPT_POSTFIELDS => http_build_query($fields),
			CURLOPT_TIMEOUT => 45,
			CURLOPT_SSL_VERIFYPEER => true,
			CURLOPT_USERAGENT => 'ePartsCart-ERP-UAE-Tax-Compliance/2.0',
			CURLOPT_HTTPHEADER => array('Content-Type: application/x-www-form-urlencoded'),
		));
		$body = curl_exec($ch);
		curl_close($ch);
		if (is_string($body) && $body !== '') {
			return $body;
		}
	}
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => 'POST',
			'header' => "Content-Type: application/x-www-form-urlencoded\r\n",
			'content' => http_build_query($fields),
			'timeout' => 45,
			'user_agent' => 'ePartsCart-ERP-UAE-Tax-Compliance/2.0',
		),
		'ssl' => array('verify_peer' => true, 'verify_peer_name' => true),
	));
	$body = @file_get_contents($url, false, $ctx);
	return is_string($body) ? $body : '';
}

/** Validate UAE TRN format (15 digits per FTA). */
function epc_uae_validate_trn($trn, bool $required = true): bool
{
	$trn = preg_replace('/\D/', '', (string)$trn);
	if ($trn === '') {
		return !$required;
	}
	return (bool)preg_match('/^\d{15}$/', $trn);
}

/**
 * Voucher-level VAT/CT treatment check — GL journals, purchases, e-invoice context.
 *
 * @param string $voucher_type manual|purchase|sales|einvoice|gl
 */
function epc_uae_vat_apply_to_voucher(PDO $db, string $voucher_type, array $context): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$errors = array();
	$legRef = trim((string)($context['legislation_ref'] ?? 'vat-decree-8-2017'));
	if ($legRef === '') {
		$legRef = 'vat-decree-8-2017';
	}

	if ($voucher_type === 'purchase') {
		$ex = round((float)($context['amount_ex_vat'] ?? 0), 2);
		$vat = round((float)($context['vat_amount'] ?? 0), 2);
		$total = round((float)($context['total_amount'] ?? 0), 2);
		if ($ex > 0 && !empty($context['vat_applicable']) && abs(($ex + $vat) - $total) > 0.05) {
			$errors[] = 'Purchase VAT split does not match total (ex-VAT + VAT = total)';
		}
		$treatment = (string)($context['uae_vat_treatment'] ?? 'standard');
		$allowed = array('standard', 'zero_rated', 'exempt', 'reverse_charge', 'import_rc', 'export');
		if (!in_array($treatment, $allowed, true)) {
			$errors[] = 'Invalid UAE VAT treatment on purchase voucher';
		}
		if ($treatment === 'reverse_charge' || $treatment === 'import_rc') {
			$legRef = 'vat-executive-regulation';
		}
		$co = epc_uae_company_profile($db);
		if ($ex > 0 && !empty($context['vat_applicable']) && empty($co['fta_ready'])) {
			$errors[] = 'Tenant UAE registration incomplete (AE country + valid TRN) — cannot claim input VAT';
		}
		$supplier = $context['supplier'] ?? null;
		if (!$supplier && !empty($context['supplier_id'])) {
			$supplier = epc_uae_vat_get_supplier($db, (int)$context['supplier_id']);
		}
		if ($ex > 0 && !empty($context['vat_applicable']) && $supplier) {
			$supCountry = epc_uae_vat_normalize_country($supplier['country_code'] ?? 'AE');
			if (epc_uae_vat_is_uae_country($supCountry)) {
				$supTrn = preg_replace('/\D/', '', (string)($supplier['trn'] ?? ''));
				if (!epc_uae_company_trn_valid($supTrn)) {
					$errors[] = 'UAE supplier TRN required (15 digits) to recover input VAT on this purchase';
				}
				if (isset($supplier['vat_registered']) && (int)$supplier['vat_registered'] !== 1) {
					$errors[] = 'Supplier must be flagged VAT-registered for UAE input VAT recovery';
				}
			}
		}
	}

	if ($voucher_type === 'sales') {
		$co = epc_uae_company_profile($db);
		if (empty($co['fta_ready'])) {
			$errors[] = 'Seller UAE TRN and AE registration required before FTA output VAT / tax invoices';
		}
	}

	if ($voucher_type === 'manual' || $voucher_type === 'gl') {
		foreach (($context['lines'] ?? array()) as $ln) {
			if (stripos((string)($ln['line_note'] ?? ''), 'vat') !== false) {
				$legRef = 'vat-decree-8-2017';
				break;
			}
		}
	}

	if ($voucher_type === 'einvoice') {
		$seller = $context['seller'] ?? array();
		$buyer = $context['buyer'] ?? array();
		$co = epc_uae_company_profile($db);
		$sellerCountry = epc_uae_vat_normalize_country($seller['seller_country_code'] ?? $co['country_code'] ?? 'AE');
		if ($sellerCountry !== 'AE') {
			$errors[] = 'Seller country must be AE for UAE FTA tax invoices';
		}
		if (empty($co['vat_registered'])) {
			$errors[] = 'Tenant must be VAT-registered in UAE to issue FTA tax invoices';
		}
		if (!epc_uae_validate_trn($seller['seller_trn'] ?? $seller['trn'] ?? '', true)) {
			$errors[] = 'Seller TRN must be exactly 15 digits (FTA)';
		}
		$buyerCountry = strtoupper((string)($buyer['buyer_country_code'] ?? $buyer['country_code'] ?? 'AE'));
		if ($buyerCountry === 'AE') {
			$bc = preg_replace('/\D/', '', (string)($buyer['buyer_trn'] ?? $buyer['trn'] ?? ''));
			if ($bc !== '' && !epc_uae_validate_trn($bc, true)) {
				$errors[] = 'UAE buyer TRN must be exactly 15 digits when provided';
			}
		}
		$invNo = (string)($context['invoice_number'] ?? '');
		if ($invNo !== '' && !preg_match('/^EINV-\d{4}-\d{5}$/', $invNo) && !preg_match('/^[A-Z0-9][A-Z0-9\-\/]{2,48}$/i', $invNo)) {
			$errors[] = 'Invoice number must be a unique serial (use EINV-YYYY-NNNNN sequence)';
		}
		$adv = round((float)($context['advance_vat_credit'] ?? 0), 2);
		$invVat = round((float)($context['total_vat'] ?? 0), 2);
		if ($adv > $invVat + 0.01) {
			$errors[] = 'Advance VAT credit cannot exceed invoice total VAT';
		}
		$legRef = 'einvoicing-decision';
	}

	return array(
		'ok' => empty($errors),
		'errors' => $errors,
		'legislation_ref' => $legRef,
		'voucher_type' => $voucher_type,
	);
}

function epc_uae_fta_parse_legislation_links(string $html, string $baseUrl): array
{
	$out = array();
	if (!preg_match_all('/<a\s+[^>]*href\s*=\s*["\']([^"\']+)["\'][^>]*>(.*?)<\/a>/is', $html, $matches, PREG_SET_ORDER)) {
		return $out;
	}
	$host = parse_url($baseUrl, PHP_URL_SCHEME) . '://' . parse_url($baseUrl, PHP_URL_HOST);
	foreach ($matches as $m) {
		$href = html_entity_decode(trim($m[1]), ENT_QUOTES | ENT_HTML5, 'UTF-8');
		$title = trim(strip_tags($m[2]));
		if ($title === '' || strlen($title) < 4) {
			continue;
		}
		if (stripos($href, 'javascript:') === 0 || $href === '#') {
			continue;
		}
		if (strpos($href, 'http') !== 0) {
			if ($href[0] === '/') {
				$href = $host . $href;
			} else {
				continue;
			}
		}
		if (stripos($href, 'tax.gov.ae') === false) {
			continue;
		}
		if (!preg_match('/legislation|law|decree|cabinet|guide|vat|corporate/i', $href . ' ' . $title)) {
			continue;
		}
		$out[] = array('title' => $title, 'href' => $href);
	}
	return $out;
}

function epc_uae_tax_compliance_guide_sections(): array
{
	return array(
		'advance_vat' => array(
			'title' => 'VAT on advance payments',
			'points' => array(
				'When a customer pays before the final tax invoice, output VAT is calculated on the VAT-inclusive payment (5/105 split).',
				'Each payment is stored in epc_uae_vat_advance and included in the UAE VAT return period when received.',
				'When you issue the tax invoice (E-Invoicing / Invoices tab), advance VAT is credited against invoice VAT — net payable is vat_net_after_advance.',
				'Issue the tax invoice when the supply is made or per your commercial terms; do not double-count VAT on the same amount.',
			),
		),
		'tax_invoice_format' => array(
			'title' => 'UAE tax invoice format (PINT-AE / FTA)',
			'points' => array(
				'Header: unique invoice number, issue date, invoice type code 380, currency AED, transaction type code, payment due date.',
				'Seller: legal name, TRN (15 digits), trade licence no., address, emirate, country AE, Peppol endpoint 0235:TIN.',
				'Buyer: name, TRN (B2B UAE), address, emirate, country; export/zero-rated → buyer TRN may differ per FTA rules.',
				'Lines: description, qty, UOM, unit price ex VAT, net, tax category (S/Z/O/AE), rate, VAT amount AED, gross.',
				'Totals: subtotal ex VAT, total VAT, total incl VAT, amount due; show advance VAT credit when applicable.',
				'Credit notes (381) must reference original invoice; validate in E-Invoicing before ASP submission.',
			),
		),
		'expense_vat' => array(
			'title' => 'Input VAT on expense types',
			'points' => array(
				'Supplier purchases (inventory/services) — recoverable if UAE VAT-registered supplier and valid tax invoice.',
				'Staff travel & business expenses — recoverable when supported by tax invoice; map category on CRM expense.',
				'Entertainment & personal — generally blocked input VAT (0% recoverable); still record for CT add-back.',
				'Motor vehicle / fuel — may be partially recoverable; use motor_vehicle type and verify with advisor.',
				'Import / reverse charge (AE) — self-account output + input on designated imported services.',
				'Capital assets — recoverable per FTA capital scheme; capitalise asset and VAT in GL.',
			),
		),
		'export' => array(
			'title' => 'Exports & zero-rated supplies',
			'points' => array(
				'Tick Exports on the e-invoice transaction flags for goods/services outside the UAE (zero-rated, category Z).',
				'Non-UAE buyer country on the buyer profile also forces zero-rated lines automatically.',
				'Keep export evidence: customs declaration, bill of lading, proof of goods leaving UAE (see Custom & Shipping module).',
				'Reverse charge (category AE) applies to designated imported services — confirm with your advisor.',
			),
		),
		'corporate_tax' => array(
			'title' => 'UAE Corporate Tax (9%)',
			'points' => array(
				'Federal CT rate is 9% on taxable income above the small business relief threshold (default AED 375,000 in settings).',
				'P&L tab: accounting profit → CT add-backs → CT deductions → adjusted profit → threshold → 9% provision.',
				'Use Tax compliance tab to enter add-backs (entertainment, fines, depreciation) and deductions (exempt income, losses).',
				'Adjust for transfers, foreign PE, and qualifying free zone income in your CT return — ERP figure is indicative only.',
			),
		),
		'pl' => array(
			'title' => 'P&L interpretation',
			'points' => array(
				'Revenue and expenses come from posted GL journals (COA types revenue/expense).',
				'Net profit before CT = total revenue − total expenses for the period.',
				'Post sales and purchase invoices to GL for complete figures; order margin on Dashboard is operational, not statutory accounts.',
			),
		),
	);
}

/** UAE tax invoice mandatory field groups (PINT-AE / FTA e-invoicing). */
function epc_uae_tax_invoice_format_groups(): array
{
	require_once __DIR__ . '/epc_einvoice.php';
	return array(
		'Document header' => array(
			'invoice_number', 'issue_date', 'invoice_type_code', 'currency_code',
			'transaction_type_code', 'payment_due_date', 'business_process', 'specification_id', 'payment_means_code',
		),
		'Seller (supplier)' => array(
			'seller_name', 'seller_trn', 'seller_legal_reg_no', 'seller_legal_reg_type',
			'seller_peppol_endpoint', 'seller_address_line1', 'seller_city', 'seller_emirate', 'seller_country_code',
		),
		'Buyer (customer)' => array(
			'buyer_name', 'buyer_trn', 'buyer_peppol_endpoint',
			'buyer_address_line1', 'buyer_city', 'buyer_emirate', 'buyer_country_code',
		),
		'Line & totals' => array(
			'subtotal_ex_vat', 'total_ex_vat', 'total_vat', 'total_incl_vat', 'amount_due',
		),
	);
}

function epc_uae_tax_invoice_format_checklist(): array
{
	$groups = epc_uae_tax_invoice_format_groups();
	require_once __DIR__ . '/epc_einvoice.php';
	$labels = epc_einvoice_mandatory_field_map();
	$out = array();
	foreach ($groups as $group => $keys) {
		$rows = array();
		foreach ($keys as $k) {
			$rows[] = array('key' => $k, 'label' => $labels[$k] ?? $k);
		}
		$out[] = array('group' => $group, 'fields' => $rows);
	}
	$out[] = array(
		'group' => 'Advance payment adjustment',
		'fields' => array(
			array('key' => 'advance_vat_credit', 'label' => 'VAT already paid on advance(s) — credit on final tax invoice'),
			array('key' => 'vat_net_after_advance', 'label' => 'Net output VAT due after advance credit'),
		),
	);
	return $out;
}

function epc_uae_vat_expense_type_defs(): array
{
	return array(
		'supplier_purchase' => array('label' => 'Supplier purchases (inventory / COGS)', 'recoverable_percent' => 100, 'crm_categories' => array()),
		'professional_services' => array('label' => 'Professional & consulting fees', 'recoverable_percent' => 100, 'crm_categories' => array('consulting', 'legal', 'accounting')),
		'rent_utilities' => array('label' => 'Rent, utilities & facilities', 'recoverable_percent' => 100, 'crm_categories' => array('rent', 'utilities')),
		'staff_travel' => array('label' => 'Staff travel & business mileage', 'recoverable_percent' => 100, 'crm_categories' => array('travel', 'mileage', 'transport')),
		'marketing' => array('label' => 'Marketing & advertising', 'recoverable_percent' => 100, 'crm_categories' => array('marketing', 'advertising')),
		'staff_general' => array('label' => 'General staff / office expenses', 'recoverable_percent' => 100, 'crm_categories' => array('office', 'supplies', 'general')),
		'motor_vehicle' => array('label' => 'Motor vehicle & fuel (partial recovery)', 'recoverable_percent' => 50, 'crm_categories' => array('fuel', 'vehicle', 'motor')),
		'entertainment' => array('label' => 'Entertainment & hospitality (blocked)', 'recoverable_percent' => 0, 'crm_categories' => array('entertainment', 'meals', 'hospitality')),
		'import_reverse_charge' => array('label' => 'Import services — reverse charge (AE)', 'recoverable_percent' => 100, 'crm_categories' => array('import', 'reverse_charge')),
		'capital_asset' => array('label' => 'Capital assets / equipment', 'recoverable_percent' => 100, 'crm_categories' => array('equipment', 'capex')),
	);
}

function epc_uae_vat_resolve_expense_type(string $category, string $explicit = ''): string
{
	$explicit = trim($explicit);
	if ($explicit !== '' && isset(epc_uae_vat_expense_type_defs()[$explicit])) {
		return $explicit;
	}
	$cat = strtolower(trim($category));
	foreach (epc_uae_vat_expense_type_defs() as $type => $def) {
		foreach ($def['crm_categories'] as $c) {
			if ($cat === $c || strpos($cat, $c) !== false) {
				return $type;
			}
		}
	}
	return 'staff_general';
}

function epc_uae_vat_calc_expense_input(float $amount, PDO $db, string $expense_type): array
{
	$defs = epc_uae_vat_expense_type_defs();
	$def = $defs[$expense_type] ?? $defs['staff_general'];
	$split = epc_uae_vat_split_inclusive($amount, $db);
	$recoverable_pct = (float)$def['recoverable_percent'];
	$recoverable_vat = round($split['vat_amount'] * ($recoverable_pct / 100), 2);
	$blocked_vat = round($split['vat_amount'] - $recoverable_vat, 2);
	return array_merge($split, array(
		'expense_type' => $expense_type,
		'expense_label' => $def['label'],
		'recoverable_percent' => $recoverable_pct,
		'recoverable_vat' => $recoverable_vat,
		'blocked_vat' => $blocked_vat,
	));
}

function epc_uae_vat_input_expenses_report(PDO $db, int $date_from, int $date_to): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$defs = epc_uae_vat_expense_type_defs();
	$by_type = array();
	foreach ($defs as $k => $d) {
		$by_type[$k] = array(
			'type' => $k, 'label' => $d['label'], 'recoverable_percent' => $d['recoverable_percent'],
			'gross_amount' => 0.0, 'amount_ex_vat' => 0.0, 'vat_amount' => 0.0,
			'recoverable_vat' => 0.0, 'blocked_vat' => 0.0, 'transaction_count' => 0,
		);
	}

	$pq = $db->prepare(
		'SELECT IFNULL(SUM(`amount_ex_vat`),0) AS ex, IFNULL(SUM(`vat_amount`),0) AS vat, IFNULL(SUM(`total_amount`),0) AS tot, COUNT(*) AS cnt
		FROM `epc_erp_purchases` WHERE `active` = 1 AND `purchase_date` >= ? AND `purchase_date` <= ?'
	);
	$pq->execute(array($date_from, $date_to));
	$pr = $pq->fetch(PDO::FETCH_ASSOC) ?: array();
	$by_type['supplier_purchase']['amount_ex_vat'] = round((float)($pr['ex'] ?? 0), 2);
	$by_type['supplier_purchase']['vat_amount'] = round((float)($pr['vat'] ?? 0), 2);
	$by_type['supplier_purchase']['gross_amount'] = round((float)($pr['tot'] ?? 0), 2);
	$by_type['supplier_purchase']['recoverable_vat'] = $by_type['supplier_purchase']['vat_amount'];
	$by_type['supplier_purchase']['transaction_count'] = (int)($pr['cnt'] ?? 0);

	if (epc_uae_tax_table_exists($db, 'epc_crm_expenses')) {
		$eq = $db->prepare(
			"SELECT * FROM `epc_crm_expenses` WHERE `active` = 1 AND `status` IN ('approved','paid') AND `time_updated` >= ? AND `time_updated` <= ?"
		);
		$eq->execute(array($date_from, $date_to));
		while ($row = $eq->fetch(PDO::FETCH_ASSOC)) {
			$type = epc_uae_vat_resolve_expense_type((string)($row['category'] ?? ''), (string)($row['vat_expense_type'] ?? ''));
			$amt = (float)($row['amount'] ?? 0);
			if ((float)($row['vat_amount'] ?? 0) > 0) {
				$pct = (float)(epc_uae_vat_expense_type_defs()[$type]['recoverable_percent'] ?? 100);
				$vat = (float)$row['vat_amount'];
				$calc = array(
					'amount_ex_vat' => (float)$row['amount_ex_vat'],
					'vat_amount' => $vat,
					'recoverable_vat' => round($vat * ($pct / 100), 2),
					'blocked_vat' => round($vat * (1 - $pct / 100), 2),
				);
			} else {
				$calc = epc_uae_vat_calc_expense_input($amt, $db, $type);
			}
			$by_type[$type]['gross_amount'] += $amt;
			$by_type[$type]['amount_ex_vat'] += $calc['amount_ex_vat'];
			$by_type[$type]['vat_amount'] += $calc['vat_amount'];
			$by_type[$type]['recoverable_vat'] += $calc['recoverable_vat'];
			$by_type[$type]['blocked_vat'] += $calc['blocked_vat'];
			$by_type[$type]['transaction_count']++;
		}
	}

	if (epc_uae_tax_table_exists($db, 'epc_erp_expense_reports')) {
		$rq = $db->prepare(
			"SELECT * FROM `epc_erp_expense_reports` WHERE `status` IN ('approved','paid') AND `time_updated` >= ? AND `time_updated` <= ?"
		);
		$rq->execute(array($date_from, $date_to));
		while ($row = $rq->fetch(PDO::FETCH_ASSOC)) {
			$type = epc_uae_vat_resolve_expense_type('', (string)($row['vat_expense_type'] ?? 'staff_general'));
			$calc = epc_uae_vat_calc_expense_input((float)($row['total_amount'] ?? 0), $db, $type);
			$by_type[$type]['gross_amount'] += (float)($row['total_amount'] ?? 0);
			$by_type[$type]['amount_ex_vat'] += $calc['amount_ex_vat'];
			$by_type[$type]['vat_amount'] += $calc['vat_amount'];
			$by_type[$type]['recoverable_vat'] += $calc['recoverable_vat'];
			$by_type[$type]['blocked_vat'] += $calc['blocked_vat'];
			$by_type[$type]['transaction_count']++;
		}
	}

	$totals = array('gross_amount' => 0.0, 'amount_ex_vat' => 0.0, 'vat_amount' => 0.0, 'recoverable_vat' => 0.0, 'blocked_vat' => 0.0, 'transaction_count' => 0);
	$lines = array();
	foreach ($by_type as $row) {
		foreach (array('gross_amount', 'amount_ex_vat', 'vat_amount', 'recoverable_vat', 'blocked_vat') as $f) {
			$row[$f] = round($row[$f], 2);
		}
		if ($row['transaction_count'] <= 0 && $row['vat_amount'] <= 0) {
			continue;
		}
		$lines[] = $row;
		foreach ($totals as $k => $_) {
			$totals[$k] += ($k === 'transaction_count') ? $row[$k] : $row[$k];
		}
	}
	foreach ($totals as $k => $v) {
		$totals[$k] = $k === 'transaction_count' ? (int)$v : round($v, 2);
	}
	return array('date_from' => $date_from, 'date_to' => $date_to, 'lines' => $lines, 'totals' => $totals);
}

function epc_uae_tax_table_exists(PDO $db, string $table): bool
{
	try {
		$st = $db->prepare('SHOW TABLES LIKE ?');
		$st->execute(array($table));
		return (bool)$st->fetchColumn();
	} catch (Exception $e) {
		return false;
	}
}

/** English stopwords for legislation Q&A tokenization. */
function epc_uae_tax_legislation_stopwords(): array
{
	return array(
		'a', 'an', 'the', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for', 'of', 'with', 'by', 'from',
		'is', 'are', 'was', 'were', 'be', 'been', 'being', 'have', 'has', 'had', 'do', 'does', 'did',
		'will', 'would', 'should', 'could', 'may', 'might', 'must', 'shall', 'can', 'need', 'd',
		'i', 'me', 'my', 'we', 'our', 'you', 'your', 'he', 'she', 'it', 'they', 'them', 'their',
		'this', 'that', 'these', 'those', 'what', 'which', 'who', 'whom', 'how', 'when', 'where', 'why',
		'about', 'into', 'through', 'during', 'before', 'after', 'above', 'below', 'between', 'under',
		'over', 'again', 'further', 'then', 'once', 'here', 'there', 'all', 'each', 'few', 'more',
		'most', 'other', 'some', 'such', 'no', 'nor', 'not', 'only', 'own', 'same', 'so', 'than',
		'too', 'very', 'just', 'also', 'now', 'tell', 'explain', 'please', 'give', 'know', 'want',
	);
}

/** Tokenize text for legislation search (lowercase, alphanumeric, min length 2). */
function epc_uae_tax_legislation_tokenize(string $text): array
{
	$text = strtolower(trim($text));
	$text = preg_replace('/[^a-z0-9\s%]/', ' ', $text);
	$parts = preg_split('/\s+/', $text, -1, PREG_SPLIT_NO_EMPTY);
	$stop = array_flip(epc_uae_tax_legislation_stopwords());
	$out = array();
	foreach ($parts as $p) {
		if (strlen($p) < 2 || isset($stop[$p])) {
			continue;
		}
		$out[] = $p;
	}
	return array_values(array_unique($out));
}

/** Intent keyword boosts — pattern_key => score bonus when question matches. */
function epc_uae_tax_legislation_intent_boosts(string $question): array
{
	$q = strtolower($question);
	$boosts = array();
	$rules = array(
		array('pattern' => '/\b(vat\s*rate|standard\s*rate|5\s*%|five\s*percent)\b/i', 'keys' => array('vat-decree-8-2017'), 'bonus' => 25, 'tax' => 'vat'),
		array('pattern' => '/\b(advance\s*payment|deposit|pre.?payment|customer\s*advance)\b/i', 'keys' => array('vat-decree-8-2017'), 'bonus' => 22, 'tax' => 'vat'),
		array('pattern' => '/\b(export|zero.?rat|zero\s*rating|outside\s*uae|international\s*supply)\b/i', 'keys' => array('vat-decree-8-2017', 'vat-executive-regulation'), 'bonus' => 22, 'tax' => 'vat'),
		array('pattern' => '/\b(trn|tax\s*registration\s*number|0235)\b/i', 'keys' => array('einvoicing-decision', 'tax-procedures'), 'bonus' => 20, 'tax' => 'einvoicing'),
		array('pattern' => '/\b(corporate\s*tax|ct\s*rate|9\s*%|nine\s*percent|taxable\s*income|375\s*000|375000)\b/i', 'keys' => array('corporate-tax-47-2022', 'ct-executive-regulation'), 'bonus' => 24, 'tax' => 'corporate_tax'),
		array('pattern' => '/\b(excise|tobacco|energy\s*drink|sweetened|carbonated)\b/i', 'keys' => array('excise-decree', 'excise-executive'), 'bonus' => 22, 'tax' => 'excise'),
		array('pattern' => '/\b(registr|register|mandatory\s*registration|375\s*000)\b/i', 'keys' => array('tax-procedures', 'vat-decree-8-2017'), 'bonus' => 18, 'tax' => 'procedures'),
		array('pattern' => '/\b(filing|deadline|return\s*due|tax\s*period|emaratax)\b/i', 'keys' => array('tax-procedures'), 'bonus' => 18, 'tax' => 'procedures'),
		array('pattern' => '/\b(input\s*vat|recover|recovery|deduction|credit\s*note)\b/i', 'keys' => array('vat-decree-8-2017', 'vat-executive-regulation'), 'bonus' => 20, 'tax' => 'vat'),
		array('pattern' => '/\b(entertainment|blocked|non.?deduct|add.?back)\b/i', 'keys' => array('corporate-tax-47-2022', 'ct-executive-regulation'), 'bonus' => 20, 'tax' => 'corporate_tax'),
		array('pattern' => '/\b(e.?invoice|peppol|pint|asp)\b/i', 'keys' => array('einvoicing-decision'), 'bonus' => 20, 'tax' => 'einvoicing'),
		array('pattern' => '/\b(reverse\s*charge|import\s*vat|customs)\b/i', 'keys' => array('vat-decree-8-2017', 'vat-executive-regulation'), 'bonus' => 16, 'tax' => 'vat'),
	);
	foreach ($rules as $rule) {
		if (preg_match($rule['pattern'], $q)) {
			foreach ($rule['keys'] as $pk) {
				$boosts[$pk] = max((int)($boosts[$pk] ?? 0), (int)$rule['bonus']);
			}
			if (!empty($rule['tax'])) {
				$boosts['__tax_' . $rule['tax']] = max((int)($boosts['__tax_' . $rule['tax']] ?? 0), (int)$rule['bonus'] - 5);
			}
		}
	}
	return $boosts;
}

/** Load legislation items for search (DB table preferred, cache fallback). */
function epc_uae_tax_legislation_load_search_items(PDO $db): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$items = array();
	try {
		$st = $db->query('SELECT * FROM `epc_uae_tax_legislation_items` ORDER BY `issue_date` DESC, `id` ASC');
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$actions = json_decode((string)($row['compliance_actions_json'] ?? ''), true);
			if (!is_array($actions)) {
				$actions = array();
			}
			$item = array(
				'item_key' => (string)($row['item_key'] ?? ''),
				'slug' => (string)($row['slug'] ?? ''),
				'title' => (string)($row['title'] ?? ''),
				'issue_date' => (string)($row['issue_date'] ?? ''),
				'category' => (string)($row['category'] ?? ''),
				'tax_category' => (string)($row['tax_category'] ?? 'general'),
				'pdf_url' => (string)($row['pdf_url'] ?? ''),
				'erp_summary' => (string)($row['erp_summary'] ?? ''),
				'pattern_key' => (string)($row['pattern_key'] ?? ''),
				'compliance_actions' => $actions,
			);
			if (trim($item['erp_summary']) === '' || empty($item['compliance_actions'])) {
				$item = epc_uae_tax_legislation_enrich_item($item);
			} else {
				$item['tax_type'] = $item['tax_category'];
			}
			$items[] = $item;
		}
	} catch (Exception $e) {
		$items = array();
	}
	if (empty($items)) {
		$cached = epc_uae_fta_get_cached_legislation($db);
		foreach ($cached['legislation'] ?? $cached['items'] ?? array() as $leg) {
			$items[] = epc_uae_tax_legislation_enrich_item($leg);
		}
	}
	return $items;
}

/** Score one legislation item against question tokens and intent boosts. */
function epc_uae_tax_legislation_score_item(array $item, array $tokens, array $boosts): float
{
	if (empty($tokens)) {
		return 0.0;
	}
	$actions = $item['compliance_actions'] ?? array();
	if (!is_array($actions)) {
		$actions = json_decode((string)$actions, true) ?: array();
	}
	$fields = array(
		'title' => 3.0,
		'erp_summary' => 2.0,
		'category' => 1.5,
		'tax_category' => 2.0,
	);
	$hayParts = array(
		'title' => strtolower((string)($item['title'] ?? '')),
		'erp_summary' => strtolower((string)($item['erp_summary'] ?? $item['summary'] ?? '')),
		'category' => strtolower((string)($item['category'] ?? '')),
		'tax_category' => strtolower((string)($item['tax_category'] ?? $item['tax_type'] ?? '')),
	);
	$hayParts['actions'] = strtolower(implode(' ', $actions));
	$score = 0.0;
	foreach ($tokens as $tok) {
		foreach ($fields as $fk => $weight) {
			if ($tok !== '' && strpos($hayParts[$fk], $tok) !== false) {
				$score += $weight;
			}
		}
		if ($tok !== '' && strpos($hayParts['actions'], $tok) !== false) {
			$score += 1.5;
		}
	}
	$pk = (string)($item['pattern_key'] ?? '');
	if ($pk !== '' && isset($boosts[$pk])) {
		$score += (float)$boosts[$pk];
	}
	$tt = (string)($item['tax_type'] ?? $item['tax_category'] ?? '');
	$taxBoostKey = '__tax_' . $tt;
	if ($tt !== '' && isset($boosts[$taxBoostKey])) {
		$score += (float)$boosts[$taxBoostKey];
	}
	if (preg_match('/decree.?law\s*no\.?\s*8|value\s*added\s*tax/i', $hayParts['title'])) {
		foreach (array('vat', 'rate', '5', 'percent', 'taxable') as $kw) {
			if (in_array($kw, $tokens, true)) {
				$score += 2.0;
			}
		}
	}
	return round($score, 2);
}

/** Extract concise bullet lines from summary text (max 2 sentences each). */
function epc_uae_tax_legislation_extract_bullets(string $text, int $maxBullets = 3): array
{
	$text = trim(preg_replace('/\s+/', ' ', $text));
	if ($text === '') {
		return array();
	}
	$sentences = preg_split('/(?<=[.!?])\s+(?=[A-Z0-9"\(])/', $text);
	$bullets = array();
	foreach ($sentences ?: array($text) as $s) {
		$s = trim($s);
		if ($s === '' || strlen($s) < 20) {
			continue;
		}
		$bullets[] = $s;
		if (count($bullets) >= $maxBullets) {
			break;
		}
	}
	return $bullets;
}

/** Synthesize answer bullets from top matched legislation items (no external AI). */
function epc_uae_tax_legislation_synthesize_answer(string $question, array $matches, array $boosts): array
{
	$bullets = array();
	$q = strtolower($question);
	$catalog = epc_uae_tax_legislation_catalog();
	$familyBullets = epc_uae_tax_overall_family_bullets();

	if (preg_match('/\b(vat\s*rate|standard\s*rate|what.*rate)\b/i', $q)) {
		$bullets[] = 'UAE standard VAT rate is 5% on taxable supplies (Federal Decree-Law No. 8 of 2017).';
		$bullets[] = 'Zero-rated (0%) and exempt supplies are treated separately — see export and designated-zone rules in the matched legislation.';
	}
	if (preg_match('/\b(advance\s*payment|deposit|pre.?payment)\b/i', $q)) {
		$bullets[] = 'Output VAT is due on taxable advance payments received before supply — record in ERP and credit against the final tax invoice.';
	}
	if (preg_match('/\b(export|zero.?rat)\b/i', $q)) {
		$bullets[] = 'Exports of goods/services outside the GCC implementing states may be zero-rated (category Z) when conditions in VAT law and executive regulations are met.';
	}
	if (preg_match('/\b(corporate\s*tax|ct\s*rate|9\s*%|threshold|375)\b/i', $q)) {
		$bullets[] = 'UAE Corporate Tax is 9% on taxable income above AED 375,000 (Federal Decree-Law No. 47 of 2022); small business relief applies at or below the threshold.';
	}
	if (preg_match('/\b(trn)\b/i', $q)) {
		$bullets[] = 'UAE TRN is 15 digits; mandatory on tax invoices and Peppol endpoint 0235:TIN for e-invoicing.';
	}
	if (preg_match('/\b(input\s*vat|recover)\b/i', $q)) {
		$bullets[] = 'Input VAT is recoverable on business expenses with valid tax invoices, subject to blocked categories (e.g. entertainment) per VAT law.';
	}
	if (preg_match('/\b(entertainment|blocked)\b/i', $q)) {
		$bullets[] = 'Entertainment expenses are generally blocked for input VAT recovery; Corporate Tax may require add-back adjustments in ERP P&L CT fields.';
	}

	foreach ($matches as $m) {
		$pk = (string)($m['pattern_key'] ?? '');
		if ($pk !== '' && isset($catalog[$pk]['summary'])) {
			$bullets[] = (string)$catalog[$pk]['summary'];
		}
		$summary = trim((string)($m['erp_summary'] ?? $m['summary'] ?? ''));
		foreach (epc_uae_tax_legislation_extract_bullets($summary, 2) as $sb) {
			$bullets[] = $sb;
		}
		$actions = $m['compliance_actions'] ?? array();
		if (is_array($actions)) {
			foreach (array_slice($actions, 0, 2) as $act) {
				$bullets[] = (string)$act;
			}
		}
		$tt = (string)($m['tax_type'] ?? $m['tax_category'] ?? '');
		if ($tt !== '' && !empty($familyBullets[$tt])) {
			$bullets[] = (string)$familyBullets[$tt][0];
		}
	}

	$seen = array();
	$unique = array();
	foreach ($bullets as $b) {
		$b = trim(preg_replace('/\s+/', ' ', $b));
		if ($b === '' || strlen($b) < 15) {
			continue;
		}
		$key = strtolower(substr($b, 0, 80));
		if (isset($seen[$key])) {
			continue;
		}
		$seen[$key] = true;
		$unique[] = $b;
		if (count($unique) >= 8) {
			break;
		}
	}
	if (empty($unique)) {
		$unique[] = 'No strong match in the legislation library — try rephrasing or fetch the latest FTA legislation updates.';
	}
	return $unique;
}

/** Append Q&A turn to cache history (last 10). */
function epc_uae_tax_legislation_qa_history_append(PDO $db, string $question, array $result): void
{
	epc_uae_tax_compliance_ensure_schema($db);
	$cacheKey = 'legislation_qa_history';
	$st = $db->prepare('SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1');
	$st->execute(array($cacheKey));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$history = array();
	if ($row) {
		$decoded = json_decode((string)$row['payload_json'], true);
		if (is_array($decoded)) {
			$history = $decoded;
		}
	}
	array_unshift($history, array(
		'question' => $question,
		'answer' => $result['answer'] ?? array(),
		'citations' => $result['citations'] ?? array(),
		'confidence' => $result['confidence'] ?? 0,
		'time' => time(),
	));
	$history = array_slice($history, 0, 10);
	$payload = json_encode($history, JSON_UNESCAPED_UNICODE);
	$db->prepare(
		'INSERT INTO `epc_uae_tax_compliance_cache` (`cache_key`, `payload_json`, `time_fetched`)
		 VALUES (?, ?, ?)
		 ON DUPLICATE KEY UPDATE `payload_json` = VALUES(`payload_json`), `time_fetched` = VALUES(`time_fetched`)'
	)->execute(array($cacheKey, $payload, time()));
}

/** Read last Q&A history from cache. */
function epc_uae_tax_legislation_qa_history_get(PDO $db): array
{
	epc_uae_tax_compliance_ensure_schema($db);
	$st = $db->prepare('SELECT `payload_json` FROM `epc_uae_tax_compliance_cache` WHERE `cache_key` = ? LIMIT 1');
	$st->execute(array('legislation_qa_history'));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array();
	}
	$decoded = json_decode((string)$row['payload_json'], true);
	return is_array($decoded) ? $decoded : array();
}

/**
 * Answer a natural-language UAE tax question from stored legislation summaries.
 * Search + extract only — no external AI API.
 */
function epc_uae_tax_legislation_ask(PDO $db, string $question): array
{
	$question = trim($question);
	$disclaimer = 'Indicative only; confirm with tax advisor and FTA EmaraTax.';
	if ($question === '') {
		return array(
			'ok' => false,
			'answer' => array('Please enter a tax question about UAE VAT, Corporate Tax, excise, or FTA procedures.'),
			'citations' => array(),
			'confidence' => 0,
			'disclaimer' => $disclaimer,
			'message' => 'Empty question',
		);
	}

	$items = epc_uae_tax_legislation_load_search_items($db);
	if (empty($items)) {
		return array(
			'ok' => false,
			'answer' => array('Legislation library is empty — fetch updates from tax.gov.ae/legislation.aspx first.'),
			'citations' => array(),
			'confidence' => 0,
			'disclaimer' => $disclaimer,
			'message' => 'No legislation items',
		);
	}

	$tokens = epc_uae_tax_legislation_tokenize($question);
	$boosts = epc_uae_tax_legislation_intent_boosts($question);
	$scored = array();
	foreach ($items as $item) {
		$s = epc_uae_tax_legislation_score_item($item, $tokens, $boosts);
		if ($s > 0) {
			$scored[] = array('item' => $item, 'score' => $s);
		}
	}
	usort($scored, function ($a, $b) {
		if ($a['score'] === $b['score']) {
			return strcmp((string)($a['item']['title'] ?? ''), (string)($b['item']['title'] ?? ''));
		}
		return $b['score'] <=> $a['score'];
	});

	$topN = min(5, max(3, count($scored)));
	$topMatches = array_slice($scored, 0, $topN);
	if (empty($topMatches)) {
		$fallbackPk = array_keys($boosts);
		foreach ($items as $item) {
			$pk = (string)($item['pattern_key'] ?? '');
			if ($pk !== '' && in_array($pk, $fallbackPk, true)) {
				$topMatches[] = array('item' => $item, 'score' => (float)($boosts[$pk] ?? 5));
			}
		}
		usort($topMatches, function ($a, $b) {
			return $b['score'] <=> $a['score'];
		});
		$topMatches = array_slice($topMatches, 0, 5);
	}
	if (empty($topMatches)) {
		foreach (array_slice($items, 0, 3) as $item) {
			$topMatches[] = array('item' => $item, 'score' => 1.0);
		}
	}

	$citations = array();
	$matchItems = array();
	foreach ($topMatches as $tm) {
		$item = $tm['item'];
		$matchItems[] = $item;
		$legKey = (string)($item['slug'] ?? '');
		if ($legKey === '') {
			$legKey = (string)($item['item_key'] ?? '');
		}
		$citations[] = array(
			'title' => (string)($item['title'] ?? ''),
			'issue_date' => (string)($item['issue_date'] ?? ''),
			'pdf_url' => epc_uae_fta_normalize_pdf_url((string)($item['pdf_url'] ?? '')),
			'legislation_key' => $legKey,
			'score' => (float)$tm['score'],
			'summary_excerpt' => mb_substr(trim((string)($item['erp_summary'] ?? '')), 0, 280),
		);
	}

	$confidence = empty($topMatches) ? 0.0 : round((float)$topMatches[0]['score'], 2);
	$answer = epc_uae_tax_legislation_synthesize_answer($question, $matchItems, $boosts);

	$result = array(
		'ok' => true,
		'answer' => $answer,
		'citations' => $citations,
		'confidence' => $confidence,
		'disclaimer' => $disclaimer,
		'match_count' => count($topMatches),
		'tokens' => $tokens,
	);

	epc_uae_tax_legislation_qa_history_append($db, $question, $result);

	return $result;
}
