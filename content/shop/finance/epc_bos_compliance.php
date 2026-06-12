<?php
defined('_ASTEXE_') or die('No access');

/**
 * BOS Compliance pillar — obligations engine, filing calendar, document retention.
 *
 * Config-driven and per-tenant: every obligation, filing instance and retention
 * rule lives in the tenant's own DB. Sensible defaults are seeded once from the
 * tenant's tax/region context but are fully editable afterwards (idempotent,
 * never overwrites an admin edit). Nothing is hard-coded to a single tenant.
 *
 * This is the operating-system "compliance" layer that complements the UAE FTA
 * tax library: it tracks *what must be filed, by when, with which documents, and
 * how long records must be kept* — across any regime a tenant operates under.
 */

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';

function epc_bos_compliance_admin_id(): int
{
	return function_exists('epc_erp_admin_id') ? (int) epc_erp_admin_id() : 0;
}

function epc_bos_compliance_ensure_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_bos_compliance_obligations` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`code` varchar(48) NOT NULL,
		`title` varchar(160) NOT NULL,
		`regime` varchar(64) NOT NULL DEFAULT 'general',
		`authority` varchar(120) DEFAULT NULL,
		`frequency` enum('monthly','quarterly','annual','one_off') NOT NULL DEFAULT 'monthly',
		`lead_days` int(11) NOT NULL DEFAULT 28,
		`doc_requirements` text,
		`notes` text,
		`is_seed` tinyint(1) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `u_code` (`code`),
		KEY `x_regime` (`regime`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS compliance obligations';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_bos_compliance_filings` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`obligation_id` int(11) NOT NULL DEFAULT 0,
		`period_label` varchar(48) NOT NULL,
		`period_start` int(11) NOT NULL DEFAULT 0,
		`period_end` int(11) NOT NULL DEFAULT 0,
		`due_date` int(11) NOT NULL DEFAULT 0,
		`status` enum('open','filed','waived') NOT NULL DEFAULT 'open',
		`filed_at` int(11) NOT NULL DEFAULT 0,
		`reference` varchar(120) DEFAULT NULL,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `u_period` (`obligation_id`,`period_label`),
		KEY `x_due` (`due_date`),
		KEY `x_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS compliance filing calendar';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_bos_retention_rules` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`doc_type` varchar(64) NOT NULL,
		`label` varchar(160) NOT NULL,
		`retention_years` int(11) NOT NULL DEFAULT 5,
		`basis` varchar(160) DEFAULT NULL,
		`legal_ref` varchar(160) DEFAULT NULL,
		`is_seed` tinyint(1) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `u_doc` (`doc_type`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS document retention rules';");
}

/**
 * Default obligations seeded from the tenant's region/tax context.
 * Region is detected from the company country (defaults to AE) so the same code
 * works for any tenant; admins can add, edit or disable any obligation after.
 *
 * @return array<int,array<string,mixed>>
 */
function epc_bos_compliance_seed_obligations(PDO $db): array
{
	$country = strtoupper((string) epc_bos_compliance_company_country($db));
	$common = array(
		array('code' => 'einvoice', 'title' => 'E-invoicing transmission', 'regime' => 'e-invoicing', 'authority' => 'Tax authority / Peppol', 'frequency' => 'monthly', 'lead_days' => 5, 'doc_requirements' => 'Issued sales invoices in approved XML/JSON; clearance/reporting receipts.'),
		array('code' => 'payroll', 'title' => 'Payroll / wage protection run', 'regime' => 'labour', 'authority' => 'Labour / WPS', 'frequency' => 'monthly', 'lead_days' => 10, 'doc_requirements' => 'Salary register, bank/WPS file, employee acknowledgements.'),
	);
	if ($country === 'AE') {
		$region = array(
			array('code' => 'vat_return', 'title' => 'VAT return (FTA)', 'regime' => 'VAT', 'authority' => 'UAE FTA (EmaraTax)', 'frequency' => 'quarterly', 'lead_days' => 28, 'doc_requirements' => 'Sales/purchase ledgers, output & input VAT summary, adjustments.'),
			array('code' => 'corporate_tax', 'title' => 'Corporate Tax return', 'regime' => 'corporate-tax', 'authority' => 'UAE FTA', 'frequency' => 'annual', 'lead_days' => 270, 'doc_requirements' => 'Financial statements, tax computation, transfer-pricing disclosures.'),
			array('code' => 'esr', 'title' => 'Economic Substance notification', 'regime' => 'ESR', 'authority' => 'Ministry of Finance', 'frequency' => 'annual', 'lead_days' => 180, 'doc_requirements' => 'Relevant-activity assessment, substance evidence.'),
		);
	} else {
		$region = array(
			array('code' => 'vat_return', 'title' => 'VAT / GST return', 'regime' => 'VAT', 'authority' => 'Tax authority', 'frequency' => 'quarterly', 'lead_days' => 28, 'doc_requirements' => 'Sales/purchase ledgers, output & input tax summary.'),
			array('code' => 'corporate_tax', 'title' => 'Corporate income tax return', 'regime' => 'corporate-tax', 'authority' => 'Tax authority', 'frequency' => 'annual', 'lead_days' => 270, 'doc_requirements' => 'Financial statements, tax computation.'),
		);
	}
	return array_merge($region, $common);
}

/** @return array<int,array<string,mixed>> */
function epc_bos_compliance_seed_retention(PDO $db): array
{
	$country = strtoupper((string) epc_bos_compliance_company_country($db));
	$invY = $country === 'AE' ? 5 : 6;
	$ctY = 7;
	return array(
		array('doc_type' => 'tax_invoice', 'label' => 'Tax invoices (issued & received)', 'retention_years' => $invY, 'basis' => 'From end of tax period', 'legal_ref' => $country === 'AE' ? 'UAE VAT Law' : 'Local VAT law'),
		array('doc_type' => 'accounting_records', 'label' => 'Accounting books & records', 'retention_years' => $ctY, 'basis' => 'From end of financial year', 'legal_ref' => $country === 'AE' ? 'UAE CT Law' : 'Companies law'),
		array('doc_type' => 'payroll', 'label' => 'Payroll & employee records', 'retention_years' => 5, 'basis' => 'From end of employment', 'legal_ref' => 'Labour law'),
		array('doc_type' => 'customs', 'label' => 'Customs / import-export docs', 'retention_years' => 5, 'basis' => 'From declaration date', 'legal_ref' => 'Customs law'),
		array('doc_type' => 'contracts', 'label' => 'Contracts & agreements', 'retention_years' => 7, 'basis' => 'From contract end', 'legal_ref' => 'General'),
	);
}

function epc_bos_compliance_company_country(PDO $db): string
{
	if (function_exists('epc_erp_adv_get_setting')) {
		$c = trim((string) epc_erp_adv_get_setting($db, 'erp_company_country', ''));
		if ($c !== '') {
			return $c;
		}
	}
	return 'AE';
}

/** Idempotent additive seed: inserts seed rows only when missing. */
function epc_bos_compliance_seed(PDO $db): void
{
	epc_bos_compliance_ensure_schema($db);
	$now = time();
	$obl = $db->prepare(
		'INSERT INTO `epc_bos_compliance_obligations`
			(`code`,`title`,`regime`,`authority`,`frequency`,`lead_days`,`doc_requirements`,`is_seed`,`active`,`time`)
		 VALUES (?,?,?,?,?,?,?,1,1,?)
		 ON DUPLICATE KEY UPDATE `id` = `id`'
	);
	foreach (epc_bos_compliance_seed_obligations($db) as $o) {
		$obl->execute(array(
			$o['code'], $o['title'], $o['regime'], $o['authority'],
			$o['frequency'], (int) $o['lead_days'], $o['doc_requirements'], $now,
		));
	}
	$ret = $db->prepare(
		'INSERT INTO `epc_bos_retention_rules`
			(`doc_type`,`label`,`retention_years`,`basis`,`legal_ref`,`is_seed`,`active`,`time`)
		 VALUES (?,?,?,?,?,1,1,?)
		 ON DUPLICATE KEY UPDATE `id` = `id`'
	);
	foreach (epc_bos_compliance_seed_retention($db) as $r) {
		$ret->execute(array(
			$r['doc_type'], $r['label'], (int) $r['retention_years'],
			$r['basis'], $r['legal_ref'], $now,
		));
	}
}

/** @return array<int,array<string,mixed>> */
function epc_bos_compliance_obligations(PDO $db): array
{
	epc_bos_compliance_ensure_schema($db);
	$rows = array();
	foreach ($db->query("SELECT * FROM `epc_bos_compliance_obligations` WHERE `active` = 1 ORDER BY `regime`, `title`") as $r) {
		$rows[] = $r;
	}
	return $rows;
}

/** @return array<int,array<string,mixed>> */
function epc_bos_retention_rules(PDO $db): array
{
	epc_bos_compliance_ensure_schema($db);
	$rows = array();
	foreach ($db->query("SELECT * FROM `epc_bos_retention_rules` WHERE `active` = 1 ORDER BY `label`") as $r) {
		$rows[] = $r;
	}
	return $rows;
}

/**
 * Period anchors for a frequency relative to an as-of timestamp.
 * Returns the current and next N period windows with computed due dates.
 *
 * @return array<int,array<string,mixed>>
 */
function epc_bos_compliance_periods(string $frequency, int $asOf, int $count = 4): array
{
	$out = array();
	$y = (int) date('Y', $asOf);
	$m = (int) date('n', $asOf);
	if ($frequency === 'monthly') {
		for ($i = 0; $i < $count; $i++) {
			$pm = $m + $i;
			$py = $y + intdiv($pm - 1, 12);
			$pmm = (($pm - 1) % 12) + 1;
			$start = mktime(0, 0, 0, $pmm, 1, $py);
			$end = mktime(23, 59, 59, $pmm + 1, 0, $py);
			$out[] = array('label' => date('M Y', $start), 'start' => $start, 'end' => $end);
		}
	} elseif ($frequency === 'quarterly') {
		$q = intdiv($m - 1, 3);
		for ($i = 0; $i < $count; $i++) {
			$qq = $q + $i;
			$py = $y + intdiv($qq, 4);
			$qm = (($qq % 4) * 3) + 1;
			$start = mktime(0, 0, 0, $qm, 1, $py);
			$end = mktime(23, 59, 59, $qm + 3, 0, $py);
			$out[] = array('label' => 'Q' . (intdiv($qm - 1, 3) + 1) . ' ' . $py, 'start' => $start, 'end' => $end);
		}
	} elseif ($frequency === 'annual') {
		for ($i = 0; $i < min($count, 2); $i++) {
			$py = $y + $i;
			$start = mktime(0, 0, 0, 1, 1, $py);
			$end = mktime(23, 59, 59, 12, 31, $py);
			$out[] = array('label' => 'FY' . $py, 'start' => $start, 'end' => $end);
		}
	} else { // one_off
		$out[] = array('label' => date('Y', $asOf), 'start' => $asOf, 'end' => $asOf);
	}
	return $out;
}

/**
 * Build the live filing calendar: for each active obligation, project upcoming
 * periods, merge any saved filing status, and compute status (open/due/overdue/filed).
 *
 * @return array<int,array<string,mixed>>
 */
function epc_bos_compliance_calendar(PDO $db, int $asOf): array
{
	$obligations = epc_bos_compliance_obligations($db);
	$saved = array();
	foreach ($db->query("SELECT * FROM `epc_bos_compliance_filings`") as $f) {
		$saved[(int) $f['obligation_id'] . '|' . $f['period_label']] = $f;
	}
	$cal = array();
	foreach ($obligations as $o) {
		$periods = epc_bos_compliance_periods((string) $o['frequency'], $asOf, 4);
		foreach ($periods as $p) {
			$due = (int) $p['end'] + ((int) $o['lead_days'] * 86400);
			$key = (int) $o['id'] . '|' . $p['label'];
			$row = $saved[$key] ?? null;
			$status = 'open';
			$filedAt = 0;
			$reference = '';
			if ($row) {
				$status = (string) $row['status'];
				$filedAt = (int) $row['filed_at'];
				$reference = (string) $row['reference'];
			}
			if ($status === 'open') {
				if ($due < $asOf) {
					$status = 'overdue';
				} elseif ($due - $asOf <= 14 * 86400) {
					$status = 'due_soon';
				}
			}
			$cal[] = array(
				'obligation_id' => (int) $o['id'],
				'code' => (string) $o['code'],
				'title' => (string) $o['title'],
				'regime' => (string) $o['regime'],
				'authority' => (string) $o['authority'],
				'frequency' => (string) $o['frequency'],
				'period_label' => (string) $p['label'],
				'period_start' => (int) $p['start'],
				'period_end' => (int) $p['end'],
				'due_date' => $due,
				'status' => $status,
				'filed_at' => $filedAt,
				'reference' => $reference,
				'doc_requirements' => (string) $o['doc_requirements'],
			);
		}
	}
	usort($cal, function ($a, $b) {
		return $a['due_date'] <=> $b['due_date'];
	});
	return $cal;
}

/** Mark a filing period filed/open/waived (upsert). */
function epc_bos_compliance_set_filing(PDO $db, int $obligationId, string $periodLabel, int $periodEnd, int $dueDate, string $status, string $reference = '', string $notes = ''): void
{
	epc_bos_compliance_ensure_schema($db);
	$status = in_array($status, array('open', 'filed', 'waived'), true) ? $status : 'open';
	$now = time();
	$filedAt = $status === 'filed' ? $now : 0;
	$st = $db->prepare(
		'INSERT INTO `epc_bos_compliance_filings`
			(`obligation_id`,`period_label`,`period_end`,`due_date`,`status`,`filed_at`,`reference`,`notes`,`admin_id`,`time`)
		 VALUES (?,?,?,?,?,?,?,?,?,?)
		 ON DUPLICATE KEY UPDATE `status` = VALUES(`status`), `filed_at` = VALUES(`filed_at`),
			`reference` = VALUES(`reference`), `notes` = VALUES(`notes`), `time` = VALUES(`time`)'
	);
	$st->execute(array(
		$obligationId, $periodLabel, $periodEnd, $dueDate, $status, $filedAt,
		$reference, $notes, epc_bos_compliance_admin_id(), $now,
	));
}

function epc_bos_compliance_add_obligation(PDO $db, array $post): int
{
	epc_bos_compliance_ensure_schema($db);
	$title = trim((string) ($post['title'] ?? ''));
	if ($title === '') {
		return 0;
	}
	$code = trim((string) ($post['code'] ?? ''));
	if ($code === '') {
		$code = 'obl_' . substr(preg_replace('/[^a-z0-9]+/', '_', strtolower($title)), 0, 36) . '_' . substr((string) time(), -4);
	}
	$freq = (string) ($post['frequency'] ?? 'monthly');
	$freq = in_array($freq, array('monthly', 'quarterly', 'annual', 'one_off'), true) ? $freq : 'monthly';
	$st = $db->prepare(
		'INSERT INTO `epc_bos_compliance_obligations`
			(`code`,`title`,`regime`,`authority`,`frequency`,`lead_days`,`doc_requirements`,`is_seed`,`active`,`admin_id`,`time`)
		 VALUES (?,?,?,?,?,?,?,0,1,?,?)
		 ON DUPLICATE KEY UPDATE `title` = VALUES(`title`), `regime` = VALUES(`regime`),
			`authority` = VALUES(`authority`), `frequency` = VALUES(`frequency`),
			`lead_days` = VALUES(`lead_days`), `doc_requirements` = VALUES(`doc_requirements`), `active` = 1'
	);
	$st->execute(array(
		substr($code, 0, 48), $title,
		substr(trim((string) ($post['regime'] ?? 'general')), 0, 64),
		substr(trim((string) ($post['authority'] ?? '')), 0, 120),
		$freq, max(0, (int) ($post['lead_days'] ?? 28)),
		trim((string) ($post['doc_requirements'] ?? '')),
		epc_bos_compliance_admin_id(), time(),
	));
	return (int) $db->lastInsertId();
}

function epc_bos_compliance_disable_obligation(PDO $db, int $id): void
{
	$st = $db->prepare("UPDATE `epc_bos_compliance_obligations` SET `active` = 0 WHERE `id` = ?");
	$st->execute(array($id));
}

function epc_bos_retention_save(PDO $db, array $post): int
{
	epc_bos_compliance_ensure_schema($db);
	$label = trim((string) ($post['label'] ?? ''));
	$docType = trim((string) ($post['doc_type'] ?? ''));
	if ($label === '') {
		return 0;
	}
	if ($docType === '') {
		$docType = substr(preg_replace('/[^a-z0-9]+/', '_', strtolower($label)), 0, 60);
	}
	$st = $db->prepare(
		'INSERT INTO `epc_bos_retention_rules`
			(`doc_type`,`label`,`retention_years`,`basis`,`legal_ref`,`is_seed`,`active`,`admin_id`,`time`)
		 VALUES (?,?,?,?,?,0,1,?,?)
		 ON DUPLICATE KEY UPDATE `label` = VALUES(`label`), `retention_years` = VALUES(`retention_years`),
			`basis` = VALUES(`basis`), `legal_ref` = VALUES(`legal_ref`), `active` = 1'
	);
	$st->execute(array(
		substr($docType, 0, 64), $label, max(0, (int) ($post['retention_years'] ?? 5)),
		substr(trim((string) ($post['basis'] ?? '')), 0, 160),
		substr(trim((string) ($post['legal_ref'] ?? '')), 0, 160),
		epc_bos_compliance_admin_id(), time(),
	));
	return (int) $db->lastInsertId();
}

/** @return array<string,int> headline counts for the dashboard tiles. */
function epc_bos_compliance_summary(PDO $db, int $asOf): array
{
	$cal = epc_bos_compliance_calendar($db, $asOf);
	$sum = array('total' => 0, 'overdue' => 0, 'due_soon' => 0, 'filed' => 0, 'open' => 0);
	foreach ($cal as $c) {
		$sum['total']++;
		if (isset($sum[$c['status']])) {
			$sum[$c['status']]++;
		} elseif ($c['status'] === 'open') {
			$sum['open']++;
		}
	}
	return $sum;
}
