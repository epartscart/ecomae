<?php
/**
 * CLI tests for HR / Payroll / Leave / Expense / Budgeting.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_hr_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_hr.php';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

$pass_count = 0;
$fail_count = 0;
function check(string $label, bool $cond): void
{
    global $pass_count, $fail_count;
    if ($cond) {
        $pass_count++;
        echo "  PASS  $label\n";
    } else {
        $fail_count++;
        echo "  FAIL  $label\n";
    }
}
function section(string $t): void
{
    echo "\n== $t ==\n";
}

foreach (array('epc_hr_budgets', 'epc_hr_expenses', 'epc_hr_payslips', 'epc_hr_payroll_runs', 'epc_hr_attendance', 'epc_hr_leave', 'epc_hr_employees') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Employees');
$e1 = epc_hr_employee_save($db, array('code' => 'E001', 'name' => 'Ahmed', 'department' => 'Sales', 'branch_id' => 1, 'basic_salary' => 6000, 'allowances' => 2000, 'annual_leave_days' => 30));
$e2 = epc_hr_employee_save($db, array('code' => 'E002', 'name' => 'Bilal', 'department' => 'Warehouse', 'branch_id' => 1, 'basic_salary' => 4000, 'allowances' => 1000, 'annual_leave_days' => 30));
check('two employees created', $e1 > 0 && $e2 > 0);

section('Leave request, approve, balance');
$lv = epc_hr_leave_request($db, $e1, 'annual', 5, 100, 105);
epc_hr_leave_set_status($db, $lv, 'approved');
$lv2 = epc_hr_leave_request($db, $e1, 'annual', 3, 200, 203); // pending, should not count
$bal = epc_hr_leave_balance($db, $e1);
check('entitlement 30', abs($bal['entitlement'] - 30.0) < 0.01);
check('approved leave taken = 5 (pending excluded)', abs($bal['taken'] - 5.0) < 0.01);
check('balance = 25', abs($bal['balance'] - 25.0) < 0.01);

section('Payslip computation');
$slip = epc_hr_compute_payslip(6000, 2000, array(array('label' => 'Loan', 'amount' => 500), array('label' => 'Absence', 'amount' => 200)));
check('gross = 8000', abs($slip['gross'] - 8000.0) < 0.01);
check('deductions = 700', abs($slip['deductions'] - 700.0) < 0.01);
check('net = 7300', abs($slip['net'] - 7300.0) < 0.01);

section('UAE gratuity (end of service)');
// 3 years @ 6000 basic: dayWage=6000*12/365=197.26; 3*21=63 days; 63*197.26=12427.4
$g3 = epc_hr_gratuity_uae(6000, 3);
check('3 yrs gratuity ~ 12427', abs($g3 - round(6000 * 12 / 365 * 63, 2)) < 0.01);
// 7 years: 5*21 + 2*30 = 165 days
$g7 = epc_hr_gratuity_uae(6000, 7);
check('7 yrs gratuity uses 21+30 day tiers', abs($g7 - round(6000 * 12 / 365 * 165, 2)) < 0.01);
check('gratuity capped at 24 months pay', epc_hr_gratuity_uae(6000, 60) <= 6000 * 24 + 0.01);
check('zero service -> zero gratuity', abs(epc_hr_gratuity_uae(6000, 0)) < 0.01);

section('Payroll run + balanced journal');
$run = epc_hr_payroll_run($db, '2026-01', array($e1 => array(array('label' => 'Loan', 'amount' => 500))));
check('2 employees in run', $run['employees'] === 2);
check('gross = 8000 + 5000 = 13000', abs($run['gross_total'] - 13000.0) < 0.01);
check('deductions = 500', abs($run['deduction_total'] - 500.0) < 0.01);
check('net = 12500', abs($run['net_total'] - 12500.0) < 0.01);
check('payslips persisted (2)', (int) $db->query("SELECT COUNT(*) FROM epc_hr_payslips WHERE run_id=" . $run['run_id'])->fetchColumn() === 2);
// Idempotent re-run does not duplicate payslips.
$run2 = epc_hr_payroll_run($db, '2026-01', array($e1 => array(array('label' => 'Loan', 'amount' => 500))));
check('re-run is idempotent (still 2 payslips)', (int) $db->query("SELECT COUNT(*) FROM epc_hr_payslips WHERE run_id=" . $run2['run_id'])->fetchColumn() === 2);
$jrnl = epc_hr_payroll_journal($run);
check('payroll journal balances (Dr gross = Cr ded+net)', $jrnl['balanced'] === true);
check('journal debit = 13000', abs($jrnl['total_debit'] - 13000.0) < 0.01);

section('Expense claims');
$exp = epc_hr_expense_save($db, $e1, 'Client trip', array(array('label' => 'Taxi', 'amount' => 120), array('label' => 'Meals', 'amount' => 80)));
check('expense total = 200', abs($exp['amount'] - 200.0) < 0.01);
epc_hr_expense_set_status($db, $exp['id'], 'approved');
check('expense approved', (string) $db->query("SELECT status FROM epc_hr_expenses WHERE id=" . $exp['id'])->fetchColumn() === 'approved');

section('Budget vs actual variance');
epc_hr_budget_set($db, array('fiscal_year' => '2026', 'account' => 'Marketing', 'period' => '2026-01', 'amount' => 10000));
epc_hr_budget_set($db, array('fiscal_year' => '2026', 'account' => 'Marketing', 'period' => '2026-02', 'amount' => 10000));
$varUnder = epc_hr_budget_variance($db, '2026', 'Marketing', 15000);
check('budget summed across periods = 20000', abs($varUnder['budget'] - 20000.0) < 0.01);
check('under budget: not over', $varUnder['over_budget'] === false && abs($varUnder['variance'] - 5000.0) < 0.01);
$varOver = epc_hr_budget_variance($db, '2026', 'Marketing', 25000);
check('over budget flagged', $varOver['over_budget'] === true && abs($varOver['variance'] - (-5000.0)) < 0.01);

echo "\n========================================\n";
echo "HR / PAYROLL TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
