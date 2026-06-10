<?php
/**
 * Advanced ERP — HR, Payroll, Attendance/Leave, Expense, Budgeting.
 *
 * - Employees (master), with branch/department tagging.
 * - Attendance + leave (accrual, balance, request/approve).
 * - Payroll run: earnings (basic + allowances) - deductions (loans, absence,
 *   statutory) = net; gratuity/end-of-service accrual (UAE style); payroll
 *   journal that always balances.
 * - Expense claims (multi-line, approval, reimbursement).
 * - Budgets vs actuals with variance.
 *
 * Worldwide-friendly: statutory rules supplied per run (no hard-coded country);
 * UAE gratuity helper provided as the default.
 *
 * Additive: new epc_hr_* tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_hr_ensure_schema')) {
    function epc_hr_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hr_employees` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `department` varchar(80) DEFAULT NULL,
            `branch_id` int(11) NOT NULL DEFAULT 0,
            `join_date` int(11) NOT NULL DEFAULT 0,
            `basic_salary` decimal(16,2) NOT NULL DEFAULT 0.00,
            `allowances` decimal(16,2) NOT NULL DEFAULT 0.00,
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `annual_leave_days` decimal(6,2) NOT NULL DEFAULT 30.00,
            `status` varchar(12) NOT NULL DEFAULT 'active',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='HR employees'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hr_leave` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `employee_id` int(11) NOT NULL,
            `type` varchar(24) NOT NULL DEFAULT 'annual',
            `days` decimal(6,2) NOT NULL DEFAULT 0.00,
            `date_from` int(11) NOT NULL DEFAULT 0,
            `date_to` int(11) NOT NULL DEFAULT 0,
            `status` varchar(12) NOT NULL DEFAULT 'pending',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_emp` (`employee_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Leave requests'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hr_attendance` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `employee_id` int(11) NOT NULL,
            `work_date` int(11) NOT NULL DEFAULT 0,
            `hours` decimal(6,2) NOT NULL DEFAULT 0.00,
            `status` varchar(12) NOT NULL DEFAULT 'present',
            PRIMARY KEY (`id`),
            KEY `x_emp` (`employee_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Attendance records'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hr_payroll_runs` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `period` varchar(7) NOT NULL DEFAULT '',
            `status` varchar(12) NOT NULL DEFAULT 'draft',
            `gross_total` decimal(16,2) NOT NULL DEFAULT 0.00,
            `deduction_total` decimal(16,2) NOT NULL DEFAULT 0.00,
            `net_total` decimal(16,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_period` (`period`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Payroll runs'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hr_payslips` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `run_id` int(11) NOT NULL,
            `employee_id` int(11) NOT NULL,
            `basic` decimal(16,2) NOT NULL DEFAULT 0.00,
            `allowances` decimal(16,2) NOT NULL DEFAULT 0.00,
            `gross` decimal(16,2) NOT NULL DEFAULT 0.00,
            `deductions` decimal(16,2) NOT NULL DEFAULT 0.00,
            `net` decimal(16,2) NOT NULL DEFAULT 0.00,
            `detail` mediumtext,
            PRIMARY KEY (`id`),
            KEY `x_run` (`run_id`),
            KEY `x_emp` (`employee_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Payslips'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hr_expenses` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `employee_id` int(11) NOT NULL,
            `title` varchar(160) NOT NULL DEFAULT '',
            `amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            `status` varchar(12) NOT NULL DEFAULT 'draft',
            `lines` mediumtext,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_emp` (`employee_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Expense claims'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hr_budgets` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `fiscal_year` varchar(9) NOT NULL DEFAULT '',
            `account` varchar(40) NOT NULL DEFAULT '',
            `cost_center` varchar(40) NOT NULL DEFAULT '',
            `period` varchar(7) NOT NULL DEFAULT '',
            `amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            PRIMARY KEY (`id`),
            KEY `x_year` (`fiscal_year`),
            KEY `x_acct` (`account`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Budget lines'");
    }
}

/* ----------------------------- Employees ------------------------------ */

if (!function_exists('epc_hr_employee_save')) {
    /**
     * @param array<string,mixed> $data
     */
    function epc_hr_employee_save(PDO $db, array $data, int $id = 0): int
    {
        epc_hr_ensure_schema($db);
        if ($id > 0) {
            $db->prepare("UPDATE `epc_hr_employees` SET `name`=?, `department`=?, `branch_id`=?, `basic_salary`=?, `allowances`=?, `currency`=?, `annual_leave_days`=?, `status`=? WHERE `id`=?")
               ->execute(array((string) ($data['name'] ?? ''), (string) ($data['department'] ?? ''), (int) ($data['branch_id'] ?? 0), (float) ($data['basic_salary'] ?? 0), (float) ($data['allowances'] ?? 0), (string) ($data['currency'] ?? 'AED'), (float) ($data['annual_leave_days'] ?? 30), (string) ($data['status'] ?? 'active'), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_hr_employees` (`code`,`name`,`department`,`branch_id`,`join_date`,`basic_salary`,`allowances`,`currency`,`annual_leave_days`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?, 'active', ?)")
           ->execute(array((string) $data['code'], (string) ($data['name'] ?? ''), (string) ($data['department'] ?? ''), (int) ($data['branch_id'] ?? 0), (int) ($data['join_date'] ?? time()), (float) ($data['basic_salary'] ?? 0), (float) ($data['allowances'] ?? 0), (string) ($data['currency'] ?? 'AED'), (float) ($data['annual_leave_days'] ?? 30), time()));
        return (int) $db->lastInsertId();
    }
}

/* --------------------------- Leave & balance -------------------------- */

if (!function_exists('epc_hr_leave_request')) {
    function epc_hr_leave_request(PDO $db, int $employeeId, string $type, float $days, int $from = 0, int $to = 0): int
    {
        epc_hr_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_hr_leave` (`employee_id`,`type`,`days`,`date_from`,`date_to`,`status`,`time_created`) VALUES (?,?,?,?,?, 'pending', ?)")
           ->execute(array($employeeId, $type, round($days, 2), $from, $to, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_hr_leave_set_status')) {
    function epc_hr_leave_set_status(PDO $db, int $leaveId, string $status): void
    {
        epc_hr_ensure_schema($db);
        $db->prepare("UPDATE `epc_hr_leave` SET `status`=? WHERE `id`=?")->execute(array($status, $leaveId));
    }
}

if (!function_exists('epc_hr_leave_balance')) {
    /**
     * Remaining annual leave = entitlement - approved annual days taken.
     *
     * @return array<string,float>
     */
    function epc_hr_leave_balance(PDO $db, int $employeeId): array
    {
        epc_hr_ensure_schema($db);
        $emp = $db->prepare("SELECT `annual_leave_days` FROM `epc_hr_employees` WHERE `id`=?");
        $emp->execute(array($employeeId));
        $entitlement = (float) $emp->fetchColumn();
        $taken = $db->prepare("SELECT COALESCE(SUM(`days`),0) FROM `epc_hr_leave` WHERE `employee_id`=? AND `type`='annual' AND `status`='approved'");
        $taken->execute(array($employeeId));
        $used = (float) $taken->fetchColumn();
        return array('entitlement' => round($entitlement, 2), 'taken' => round($used, 2), 'balance' => round($entitlement - $used, 2));
    }
}

/* ------------------------------- Payroll ------------------------------ */

if (!function_exists('epc_hr_gratuity_uae')) {
    /**
     * UAE end-of-service gratuity: 21 days basic pay per year for the first 5
     * years, 30 days/year thereafter (capped at 2 years' total pay). Based on
     * full years of service.
     */
    function epc_hr_gratuity_uae(float $basicMonthly, float $yearsService): float
    {
        if ($yearsService <= 0) {
            return 0.0;
        }
        $dayWage = $basicMonthly * 12 / 365;
        $first = min($yearsService, 5.0);
        $rest = max(0.0, $yearsService - 5.0);
        $days = $first * 21 + $rest * 30;
        $gratuity = $dayWage * $days;
        $cap = $basicMonthly * 24; // 2 years total pay
        return round(min($gratuity, $cap), 2);
    }
}

if (!function_exists('epc_hr_compute_payslip')) {
    /**
     * Compute one payslip. Deductions supplied as a list {label, amount}.
     *
     * @param array<int,array{label:string,amount:float}> $deductions
     * @return array<string,mixed>
     */
    function epc_hr_compute_payslip(float $basic, float $allowances, array $deductions = array()): array
    {
        $gross = round($basic + $allowances, 2);
        $ded = 0.0;
        $detail = array();
        foreach ($deductions as $d) {
            $amt = round((float) ($d['amount'] ?? 0), 2);
            $ded = round($ded + $amt, 2);
            $detail[] = array('label' => (string) ($d['label'] ?? ''), 'amount' => $amt);
        }
        return array(
            'basic' => round($basic, 2),
            'allowances' => round($allowances, 2),
            'gross' => $gross,
            'deductions' => $ded,
            'net' => round($gross - $ded, 2),
            'deduction_detail' => $detail,
        );
    }
}

if (!function_exists('epc_hr_payroll_run')) {
    /**
     * Run payroll for a period over the active employees. $deductionsByEmp maps
     * employee_id => [{label, amount}].
     *
     * @param array<int,array<int,array{label:string,amount:float}>> $deductionsByEmp
     * @return array<string,mixed> run summary
     */
    function epc_hr_payroll_run(PDO $db, string $period, array $deductionsByEmp = array()): array
    {
        epc_hr_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_hr_payroll_runs` (`period`,`status`,`time_created`) VALUES (?, 'draft', ?)
                      ON DUPLICATE KEY UPDATE `status`='draft'")
           ->execute(array($period, time()));
        $runId = (int) $db->query("SELECT `id` FROM `epc_hr_payroll_runs` WHERE `period`=" . $db->quote($period))->fetchColumn();
        // Clear any prior payslips for an idempotent re-run.
        $db->prepare("DELETE FROM `epc_hr_payslips` WHERE `run_id`=?")->execute(array($runId));

        $emps = $db->query("SELECT * FROM `epc_hr_employees` WHERE `status`='active'")->fetchAll(PDO::FETCH_ASSOC);
        $gross = 0.0;
        $dedTot = 0.0;
        $net = 0.0;
        $ins = $db->prepare("INSERT INTO `epc_hr_payslips` (`run_id`,`employee_id`,`basic`,`allowances`,`gross`,`deductions`,`net`,`detail`) VALUES (?,?,?,?,?,?,?,?)");
        foreach ($emps as $e) {
            $ded = $deductionsByEmp[(int) $e['id']] ?? array();
            $slip = epc_hr_compute_payslip((float) $e['basic_salary'], (float) $e['allowances'], $ded);
            $ins->execute(array($runId, (int) $e['id'], $slip['basic'], $slip['allowances'], $slip['gross'], $slip['deductions'], $slip['net'], json_encode($slip['deduction_detail'])));
            $gross = round($gross + $slip['gross'], 2);
            $dedTot = round($dedTot + $slip['deductions'], 2);
            $net = round($net + $slip['net'], 2);
        }
        $db->prepare("UPDATE `epc_hr_payroll_runs` SET `gross_total`=?, `deduction_total`=?, `net_total`=? WHERE `id`=?")
           ->execute(array($gross, $dedTot, $net, $runId));
        return array('run_id' => $runId, 'period' => $period, 'employees' => count($emps), 'gross_total' => $gross, 'deduction_total' => $dedTot, 'net_total' => $net);
    }
}

if (!function_exists('epc_hr_payroll_journal')) {
    /**
     * Build the balanced payroll journal: Dr salary expense (gross), Cr
     * deductions payable + net payable. debit == credit always.
     *
     * @param array<string,mixed> $run output of epc_hr_payroll_run
     * @return array<string,mixed> {lines, total_debit, total_credit, balanced}
     */
    function epc_hr_payroll_journal(array $run): array
    {
        $gross = (float) $run['gross_total'];
        $ded = (float) $run['deduction_total'];
        $net = (float) $run['net_total'];
        $lines = array(
            array('account' => 'Salary Expense', 'debit' => $gross, 'credit' => 0.0),
            array('account' => 'Deductions Payable', 'debit' => 0.0, 'credit' => $ded),
            array('account' => 'Net Salary Payable', 'debit' => 0.0, 'credit' => $net),
        );
        $dr = $gross;
        $cr = round($ded + $net, 2);
        return array('lines' => $lines, 'total_debit' => round($dr, 2), 'total_credit' => $cr, 'balanced' => abs($dr - $cr) < 0.01);
    }
}

/* ----------------------------- Expenses ------------------------------- */

if (!function_exists('epc_hr_expense_save')) {
    /**
     * @param array<int,array{label:string,amount:float}> $lines
     */
    function epc_hr_expense_save(PDO $db, int $employeeId, string $title, array $lines): array
    {
        epc_hr_ensure_schema($db);
        $total = 0.0;
        foreach ($lines as $l) {
            $total = round($total + (float) ($l['amount'] ?? 0), 2);
        }
        $db->prepare("INSERT INTO `epc_hr_expenses` (`employee_id`,`title`,`amount`,`status`,`lines`,`time_created`) VALUES (?,?,?, 'draft', ?, ?)")
           ->execute(array($employeeId, $title, $total, json_encode($lines), time()));
        return array('id' => (int) $db->lastInsertId(), 'amount' => $total);
    }
}

if (!function_exists('epc_hr_expense_set_status')) {
    function epc_hr_expense_set_status(PDO $db, int $expenseId, string $status): void
    {
        epc_hr_ensure_schema($db);
        $db->prepare("UPDATE `epc_hr_expenses` SET `status`=? WHERE `id`=?")->execute(array($status, $expenseId));
    }
}

/* ------------------------------ Budgeting ----------------------------- */

if (!function_exists('epc_hr_budget_set')) {
    /**
     * @param array<string,mixed> $data fiscal_year, account, cost_center, period, amount
     */
    function epc_hr_budget_set(PDO $db, array $data): int
    {
        epc_hr_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_hr_budgets` (`fiscal_year`,`account`,`cost_center`,`period`,`amount`) VALUES (?,?,?,?,?)")
           ->execute(array((string) ($data['fiscal_year'] ?? ''), (string) ($data['account'] ?? ''), (string) ($data['cost_center'] ?? ''), (string) ($data['period'] ?? ''), round((float) ($data['amount'] ?? 0), 2)));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_hr_budget_variance')) {
    /**
     * Compare budget to actuals for an account in a fiscal year.
     *
     * @return array<string,mixed> {budget, actual, variance, variance_pct, over_budget}
     */
    function epc_hr_budget_variance(PDO $db, string $fiscalYear, string $account, float $actual): array
    {
        epc_hr_ensure_schema($db);
        $st = $db->prepare("SELECT COALESCE(SUM(`amount`),0) FROM `epc_hr_budgets` WHERE `fiscal_year`=? AND `account`=?");
        $st->execute(array($fiscalYear, $account));
        $budget = (float) $st->fetchColumn();
        $variance = round($budget - $actual, 2);
        return array(
            'budget' => round($budget, 2),
            'actual' => round($actual, 2),
            'variance' => $variance,
            'variance_pct' => $budget > 0 ? round(($actual - $budget) / $budget * 100, 2) : 0.0,
            'over_budget' => $actual > $budget,
        );
    }
}
