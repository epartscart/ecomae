<?php
/**
 * ERP — payroll runs, salary lines, payment & reporting.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_schema.php';

function epc_erp_payroll_ensure_schema(PDO $db)
{
	require_once __DIR__ . '/epc_erp_staff.php';
	epc_erp_staff_ensure_schema($db);

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_hr_records', 'basic_salary', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_hr_records', 'allowances', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_hr_records', 'bank_account', 'varchar(64) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_hr_records', 'bank_name', 'varchar(128) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_hr_records', 'days_worked', 'decimal(5,1) NOT NULL DEFAULT 30.0');

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_payroll_runs` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`period_label` varchar(32) NOT NULL,
		`period_start` int(11) NOT NULL DEFAULT 0,
		`period_end` int(11) NOT NULL DEFAULT 0,
		`status` enum('draft','approved','paid') NOT NULL DEFAULT 'draft',
		`total_gross` decimal(14,2) NOT NULL DEFAULT 0.00,
		`total_deductions` decimal(14,2) NOT NULL DEFAULT 0.00,
		`total_net` decimal(14,2) NOT NULL DEFAULT 0.00,
		`cash_account_id` int(11) NOT NULL DEFAULT 0,
		`cash_entry_id` int(11) NOT NULL DEFAULT 0,
		`paid_at` int(11) NOT NULL DEFAULT 0,
		`note` text,
		`created_by` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `period_label` (`period_label`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_payroll_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`run_id` int(11) NOT NULL,
		`staff_profile_id` int(11) NOT NULL,
		`user_id` int(11) NOT NULL DEFAULT 0,
		`department_code` varchar(32) NOT NULL DEFAULT '',
		`display_name` varchar(128) NOT NULL DEFAULT '',
		`job_title` varchar(128) DEFAULT NULL,
		`basic_salary` decimal(14,2) NOT NULL DEFAULT 0.00,
		`allowances` decimal(14,2) NOT NULL DEFAULT 0.00,
		`deductions` decimal(14,2) NOT NULL DEFAULT 0.00,
		`net_pay` decimal(14,2) NOT NULL DEFAULT 0.00,
		`bank_account` varchar(64) DEFAULT NULL,
		`bank_name` varchar(128) DEFAULT NULL,
		`status` enum('draft','paid') NOT NULL DEFAULT 'draft',
		`paid_at` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `run_id` (`run_id`),
		KEY `staff_profile_id` (`staff_profile_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_payroll_runs', 'standard_days', 'int(11) NOT NULL DEFAULT 30');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_payroll_lines', 'monthly_basic', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_payroll_lines', 'monthly_allowances', 'decimal(14,2) NOT NULL DEFAULT 0.00');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_payroll_lines', 'standard_days', 'int(11) NOT NULL DEFAULT 30');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_payroll_lines', 'days_worked', 'decimal(5,1) NOT NULL DEFAULT 30.0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_payroll_lines', 'extra_days', 'decimal(5,1) NOT NULL DEFAULT 0.0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_payroll_lines', 'daily_rate', 'decimal(14,4) NOT NULL DEFAULT 0.0000');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_payroll_lines', 'gross_pay', 'decimal(14,2) NOT NULL DEFAULT 0.00');
}

function epc_erp_payroll_standard_days()
{
	return 30;
}

function epc_erp_payroll_calc($monthlyBasic, $monthlyAllowances, $daysWorked, $standardDays = 30)
{
	$standardDays = max(1, (int)$standardDays);
	$daysWorked = max(0, round((float)$daysWorked, 1));
	$monthlyBasic = round((float)$monthlyBasic, 2);
	$monthlyAllowances = round((float)$monthlyAllowances, 2);
	$monthlyTotal = $monthlyBasic + $monthlyAllowances;
	$dailyTotal = $monthlyTotal / $standardDays;
	$dailyBasic = $monthlyBasic / $standardDays;
	$dailyAllow = $monthlyAllowances / $standardDays;

	// Fixed salary quoted for 30 days; pay = daily rate × actual days worked.
	// Days above 30 are paid at the same daily rate (overtime / extra days).
	$earnedBasic = round($dailyBasic * $daysWorked, 2);
	$earnedAllow = round($dailyAllow * $daysWorked, 2);
	$gross = round($earnedBasic + $earnedAllow, 2);
	$ded = round($gross * epc_erp_payroll_default_deduction_rate(), 2);

	return array(
		'standard_days' => $standardDays,
		'days_worked' => $daysWorked,
		'extra_days' => round(max(0, $daysWorked - $standardDays), 1),
		'daily_rate' => round($dailyTotal, 4),
		'monthly_basic' => $monthlyBasic,
		'monthly_allowances' => $monthlyAllowances,
		'earned_basic' => $earnedBasic,
		'earned_allowances' => $earnedAllow,
		'gross_pay' => $gross,
		'deductions' => $ded,
		'net_pay' => round($gross - $ded, 2),
	);
}

function epc_erp_payroll_default_deduction_rate()
{
	return 0.0;
}

function epc_erp_payroll_list_runs(PDO $db, $limit = 24)
{
	epc_erp_payroll_ensure_schema($db);
	return $db->query(
		'SELECT * FROM `epc_erp_payroll_runs` ORDER BY `period_start` DESC LIMIT ' . (int)$limit
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_payroll_run_lines(PDO $db, $runId)
{
	epc_erp_payroll_ensure_schema($db);
	$st = $db->prepare(
		'SELECT l.*, p.`email`
		 FROM `epc_erp_payroll_lines` l
		 LEFT JOIN `epc_erp_staff_profiles` p ON p.`id` = l.`staff_profile_id`
		 WHERE l.`run_id` = ?
		 ORDER BY l.`department_code`, l.`display_name`'
	);
	$st->execute(array((int)$runId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_payroll_recalc_run_totals(PDO $db, $runId)
{
	$row = $db->prepare(
		'SELECT
			IFNULL(SUM(COALESCE(NULLIF(`gross_pay`, 0), `basic_salary` + `allowances`)), 0) AS gross,
			IFNULL(SUM(`deductions`), 0) AS ded,
			IFNULL(SUM(`net_pay`), 0) AS net
		 FROM `epc_erp_payroll_lines` WHERE `run_id` = ?'
	);
	$row->execute(array((int)$runId));
	$t = $row->fetch(PDO::FETCH_ASSOC);
	$db->prepare(
		'UPDATE `epc_erp_payroll_runs` SET `total_gross` = ?, `total_deductions` = ?, `total_net` = ? WHERE `id` = ?'
	)->execute(array($t['gross'], $t['ded'], $t['net'], (int)$runId));
}

function epc_erp_payroll_generate_run(PDO $db, $periodLabel, $periodStart, $periodEnd, $createdBy = 0, $standardDays = 30)
{
	epc_erp_payroll_ensure_schema($db);
	$standardDays = max(1, (int)$standardDays);
	$periodLabel = trim((string)$periodLabel);
	if ($periodLabel === '') {
		throw new Exception('Period label required');
	}
	$ex = $db->prepare('SELECT `id`, `status` FROM `epc_erp_payroll_runs` WHERE `period_label` = ? LIMIT 1');
	$ex->execute(array($periodLabel));
	$existing = $ex->fetch(PDO::FETCH_ASSOC);
	if ($existing && $existing['status'] === 'paid') {
		throw new Exception('Payroll for ' . $periodLabel . ' is already paid');
	}
	if ($existing) {
		$runId = (int)$existing['id'];
		$db->prepare('DELETE FROM `epc_erp_payroll_lines` WHERE `run_id` = ?')->execute(array($runId));
		$db->prepare(
			'UPDATE `epc_erp_payroll_runs` SET `period_start` = ?, `period_end` = ?, `standard_days` = ?, `status` = \'draft\', `total_gross` = 0, `total_deductions` = 0, `total_net` = 0, `cash_entry_id` = 0, `paid_at` = 0 WHERE `id` = ?'
		)->execute(array((int)$periodStart, (int)$periodEnd, $standardDays, $runId));
	} else {
		$db->prepare(
			'INSERT INTO `epc_erp_payroll_runs` (`period_label`, `period_start`, `period_end`, `standard_days`, `status`, `created_by`, `time_created`)
			 VALUES (?, ?, ?, ?, \'draft\', ?, ?)'
		)->execute(array($periodLabel, (int)$periodStart, (int)$periodEnd, $standardDays, (int)$createdBy, time()));
		$runId = (int)$db->lastInsertId();
	}

	$staff = $db->query(
		'SELECT p.`id` AS profile_id, p.`user_id`, p.`department_code`, p.`display_name`, p.`job_title`,
		 h.`basic_salary`, h.`allowances`, h.`bank_account`, h.`bank_name`, h.`days_worked`
		 FROM `epc_erp_staff_profiles` p
		 INNER JOIN `epc_erp_hr_records` h ON h.`staff_profile_id` = p.`id`
		 WHERE p.`active` = 1
		 ORDER BY p.`department_code`, p.`display_name`'
	)->fetchAll(PDO::FETCH_ASSOC);

	$ins = $db->prepare(
		'INSERT INTO `epc_erp_payroll_lines`
		(`run_id`, `staff_profile_id`, `user_id`, `department_code`, `display_name`, `job_title`,
		 `monthly_basic`, `monthly_allowances`, `standard_days`, `days_worked`, `extra_days`, `daily_rate`,
		 `basic_salary`, `allowances`, `gross_pay`, `deductions`, `net_pay`, `bank_account`, `bank_name`, `status`, `time_created`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, \'draft\', ?)'
	);
	$now = time();
	foreach ($staff as $s) {
		$monthlyBasic = round((float)$s['basic_salary'], 2);
		$monthlyAllow = round((float)$s['allowances'], 2);
		$daysWorked = (float)$s['days_worked'] > 0 ? (float)$s['days_worked'] : (float)$standardDays;
		$calc = epc_erp_payroll_calc($monthlyBasic, $monthlyAllow, $daysWorked, $standardDays);
		$ins->execute(array(
			$runId,
			(int)$s['profile_id'],
			(int)$s['user_id'],
			(string)$s['department_code'],
			(string)$s['display_name'],
			(string)$s['job_title'],
			$calc['monthly_basic'],
			$calc['monthly_allowances'],
			$calc['standard_days'],
			$calc['days_worked'],
			$calc['extra_days'],
			$calc['daily_rate'],
			$calc['earned_basic'],
			$calc['earned_allowances'],
			$calc['gross_pay'],
			$calc['deductions'],
			$calc['net_pay'],
			(string)$s['bank_account'],
			(string)$s['bank_name'],
			$now,
		));
	}
	epc_erp_payroll_recalc_run_totals($db, $runId);
	return $runId;
}

function epc_erp_payroll_update_line_days(PDO $db, $lineId, $daysWorked)
{
	epc_erp_payroll_ensure_schema($db);
	$st = $db->prepare(
		'SELECT l.*, r.`status` AS run_status, r.`standard_days` AS run_standard_days
		 FROM `epc_erp_payroll_lines` l
		 INNER JOIN `epc_erp_payroll_runs` r ON r.`id` = l.`run_id`
		 WHERE l.`id` = ? LIMIT 1'
	);
	$st->execute(array((int)$lineId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row || $row['run_status'] === 'paid') {
		throw new Exception('Cannot edit paid payroll line');
	}
	$standardDays = (int)$row['run_standard_days'] > 0 ? (int)$row['run_standard_days'] : (int)$row['standard_days'];
	$monthlyBasic = (float)$row['monthly_basic'] > 0 ? (float)$row['monthly_basic'] : (float)$row['basic_salary'];
	$monthlyAllow = (float)$row['monthly_allowances'] > 0 ? (float)$row['monthly_allowances'] : (float)$row['allowances'];
	$calc = epc_erp_payroll_calc($monthlyBasic, $monthlyAllow, $daysWorked, $standardDays);
	$db->prepare(
		'UPDATE `epc_erp_payroll_lines` SET
		 `days_worked` = ?, `extra_days` = ?, `daily_rate` = ?,
		 `basic_salary` = ?, `allowances` = ?, `gross_pay` = ?, `deductions` = ?, `net_pay` = ?
		 WHERE `id` = ?'
	)->execute(array(
		$calc['days_worked'],
		$calc['extra_days'],
		$calc['daily_rate'],
		$calc['earned_basic'],
		$calc['earned_allowances'],
		$calc['gross_pay'],
		$calc['deductions'],
		$calc['net_pay'],
		(int)$lineId,
	));
	epc_erp_payroll_recalc_run_totals($db, (int)$row['run_id']);
	return $calc;
}

function epc_erp_payroll_approve_run(PDO $db, $runId)
{
	epc_erp_payroll_ensure_schema($db);
	$st = $db->prepare('SELECT `status` FROM `epc_erp_payroll_runs` WHERE `id` = ? LIMIT 1');
	$st->execute(array((int)$runId));
	$status = (string)$st->fetchColumn();
	if ($status === 'paid') {
		throw new Exception('Already paid');
	}
	if ($status !== 'draft' && $status !== 'approved') {
		throw new Exception('Invalid run');
	}
	$db->prepare('UPDATE `epc_erp_payroll_runs` SET `status` = \'approved\' WHERE `id` = ?')->execute(array((int)$runId));
	return (int)$runId;
}

function epc_erp_payroll_pay_run(PDO $db, $runId, $cashAccountId, $reference = '')
{
	require_once __DIR__ . '/epc_erp_helpers.php';
	epc_erp_payroll_ensure_schema($db);

	$run = $db->prepare('SELECT * FROM `epc_erp_payroll_runs` WHERE `id` = ? LIMIT 1');
	$run->execute(array((int)$runId));
	$row = $run->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Payroll run not found');
	}
	if ($row['status'] === 'paid') {
		throw new Exception('Already paid');
	}
	$totalNet = round((float)$row['total_net'], 2);
	if ($totalNet <= 0) {
		throw new Exception('Nothing to pay');
	}
	$cashAccountId = (int)$cashAccountId;
	if ($cashAccountId <= 0) {
		$cashAccountId = (int)$db->query('SELECT `id` FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1 ORDER BY `account_type` DESC, `id` ASC LIMIT 1')->fetchColumn();
	}
	if ($cashAccountId <= 0) {
		throw new Exception('No cash/bank account');
	}

	$ref = $reference !== '' ? $reference : 'PAYROLL-' . $row['period_label'];
	$entryId = epc_erp_cash_entry($db, array(
		'account_id' => $cashAccountId,
		'direction' => 0,
		'entry_type' => 'payment',
		'amount' => $totalNet,
		'counterparty_type' => 'internal',
		'reference' => $ref,
		'note' => 'Staff payroll ' . $row['period_label'] . ' — net salaries',
	));

	$now = time();
	$db->prepare(
		'UPDATE `epc_erp_payroll_runs` SET `status` = \'paid\', `cash_account_id` = ?, `cash_entry_id` = ?, `paid_at` = ? WHERE `id` = ?'
	)->execute(array($cashAccountId, $entryId, $now, (int)$runId));
	$db->prepare('UPDATE `epc_erp_payroll_lines` SET `status` = \'paid\', `paid_at` = ? WHERE `run_id` = ?')->execute(array($now, (int)$runId));

	try {
		epc_erp_payroll_post_gl($db, (int)$runId, $entryId);
	} catch (Exception $e) {
	}

	return array('run_id' => (int)$runId, 'cash_entry_id' => $entryId, 'total_net' => $totalNet);
}

function epc_erp_payroll_post_gl(PDO $db, $runId, $cashEntryId)
{
	require_once __DIR__ . '/epc_erp_gl.php';
	$run = $db->prepare('SELECT * FROM `epc_erp_payroll_runs` WHERE `id` = ? LIMIT 1');
	$run->execute(array((int)$runId));
	$row = $run->fetch(PDO::FETCH_ASSOC);
	if (!$row || (float)$row['total_net'] <= 0) {
		return 0;
	}
	$coaSalary = epc_erp_gl_coa_by_code($db, '6100');
	$coaBank = epc_erp_gl_coa_by_code($db, '1010');
	if ($coaSalary <= 0 || $coaBank <= 0) {
		return 0;
	}
	$amount = round((float)$row['total_net'], 2);
	return epc_erp_gl_post_journal($db, array(
		'journal_date' => time(),
		'reference' => 'PAYROLL-' . $row['period_label'],
		'description' => 'Staff payroll ' . $row['period_label'],
		'source_type' => 'payment',
		'source_id' => (int)$runId,
	), array(
		array('coa_id' => $coaSalary, 'debit' => $amount, 'credit' => 0, 'line_note' => 'Salaries'),
		array('coa_id' => $coaBank, 'debit' => 0, 'credit' => $amount, 'line_note' => 'Payroll bank #' . (int)$cashEntryId),
	));
}

function epc_erp_payroll_report(PDO $db, $year = 0)
{
	epc_erp_payroll_ensure_schema($db);
	if ($year <= 0) {
		$year = (int)date('Y');
	}
	$runs = $db->prepare(
		'SELECT * FROM `epc_erp_payroll_runs` WHERE `period_label` LIKE ? ORDER BY `period_start` ASC'
	);
	$runs->execute(array($year . '-%'));
	$runRows = $runs->fetchAll(PDO::FETCH_ASSOC);
	$byDept = $db->query(
		'SELECT l.`department_code`, SUM(l.`net_pay`) AS total_net, COUNT(*) AS headcount
		 FROM `epc_erp_payroll_lines` l
		 INNER JOIN `epc_erp_payroll_runs` r ON r.`id` = l.`run_id` AND r.`status` = \'paid\'
		 GROUP BY l.`department_code`'
	)->fetchAll(PDO::FETCH_ASSOC);
	$ytd = $db->prepare(
		'SELECT IFNULL(SUM(`total_net`), 0) FROM `epc_erp_payroll_runs` WHERE `status` = \'paid\' AND `period_label` LIKE ?'
	);
	$ytd->execute(array($year . '-%'));
	return array(
		'year' => $year,
		'runs' => $runRows,
		'by_department' => $byDept,
		'ytd_paid' => round((float)$ytd->fetchColumn(), 2),
	);
}

function epc_erp_payroll_demo_days_map()
{
	return array(
		'admin' => 30,
		'sales' => 28,
		'logistics' => 31,
		'marketing' => 30,
		'finance' => 30,
		'hr' => 29,
		'purchase' => 27,
		'accounts' => 30,
		'it' => 32,
	);
}

function epc_erp_payroll_apply_demo_days(PDO $db)
{
	epc_erp_payroll_ensure_schema($db);
	$st = $db->prepare(
		'UPDATE `epc_erp_hr_records` h
		 INNER JOIN `epc_erp_staff_profiles` p ON p.`id` = h.`staff_profile_id`
		 SET h.`days_worked` = ?
		 WHERE p.`department_code` = ?'
	);
	foreach (epc_erp_payroll_demo_days_map() as $code => $days) {
		$st->execute(array((float)$days, $code));
	}
}

function epc_erp_payroll_seed_demo(PDO $db)
{
	epc_erp_payroll_ensure_schema($db);
	if ((int)$db->query('SELECT COUNT(*) FROM `epc_erp_payroll_runs`')->fetchColumn() > 0) {
		return array('skipped' => true);
	}
	epc_erp_payroll_apply_demo_days($db);
	$aprStart = strtotime('2026-04-01 00:00:00');
	$aprEnd = strtotime('2026-04-30 23:59:59');
	$mayStart = strtotime('2026-05-01 00:00:00');
	$mayEnd = strtotime('2026-05-31 23:59:59');

	$aprId = epc_erp_payroll_generate_run($db, '2026-04', $aprStart, $aprEnd, 0);
	epc_erp_payroll_approve_run($db, $aprId);
	epc_erp_payroll_pay_run($db, $aprId, 0, 'PAYROLL-2026-04');

	$mayId = epc_erp_payroll_generate_run($db, '2026-05', $mayStart, $mayEnd, 0);
	epc_erp_payroll_approve_run($db, $mayId);

	return array('paid_run' => '2026-04', 'draft_run' => '2026-05', 'apr_id' => $aprId, 'may_id' => $mayId);
}

function epc_erp_payroll_demo_report(PDO $db)
{
	return array(
		'runs' => epc_erp_payroll_list_runs($db),
		'report' => epc_erp_payroll_report($db, 2026),
	);
}
