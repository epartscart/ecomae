<?php
/**
 * CLI tests for Financial depth: period management, allocation split, accrual
 * schedule, FX revaluation, and persistence + multi-company scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_fin_advanced_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_fin_advanced.php';

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

foreach (array('epc_fin_fx_run', 'epc_fin_accrual', 'epc_fin_alloc_run', 'epc_fin_alloc_rule', 'epc_fin_periods') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_fin_adv_ensure_schema($db);

$CO = 1;

section('Pure: period dates');
$pd = epc_fin_period_dates(2026, 1);
check('12 periods generated', count($pd) === 12);
check('period 1 starts Jan 2026', date('Y-m', $pd[0]['start_date']) === '2026-01');
check('period 12 starts Dec 2026', date('Y-m', $pd[11]['start_date']) === '2026-12');
$pdApr = epc_fin_period_dates(2026, 4);
check('fiscal start April → P1 = Apr', date('Y-m', $pdApr[0]['start_date']) === '2026-04');
check('fiscal start April → P12 = Mar next year', date('Y-m', $pdApr[11]['start_date']) === '2027-03');

section('Period management + posting lock');
check('generate creates 12 rows', epc_fin_periods_generate($db, $CO, 2026, 1) === 12);
check('regenerate idempotent (still 12)', (function () use ($db, $CO) { epc_fin_periods_generate($db, $CO, 2026, 1); return count(epc_fin_periods_list($db, $CO, 2026)) === 12; })());
$jan15 = mktime(0, 0, 0, 1, 15, 2026);
check('posting allowed in open period', epc_fin_posting_allowed($db, $CO, $jan15) === true);
epc_fin_period_set_status($db, $CO, 2026, 1, 'closed');
check('posting blocked in closed period', epc_fin_posting_allowed($db, $CO, $jan15) === false);
epc_fin_period_set_status($db, $CO, 2026, 1, 'on_hold');
check('posting blocked when on hold', epc_fin_posting_allowed($db, $CO, $jan15) === false);
epc_fin_period_set_status($db, $CO, 2026, 1, 'open');
check('posting re-allowed when reopened', epc_fin_posting_allowed($db, $CO, $jan15) === true);
check('unknown date (no period) allowed', epc_fin_posting_allowed($db, $CO, mktime(0, 0, 0, 1, 1, 2099)) === true);
check('invalid status rejected', (function () use ($db, $CO) { try { epc_fin_period_set_status($db, $CO, 2026, 1, 'bogus'); return false; } catch (Throwable $e) { return true; } })());

section('Pure: allocation split');
$split = epc_fin_alloc_split(1000, array('A' => 1, 'B' => 1, 'C' => 1));
check('1000 / 3 ways sums exactly to 1000', abs(array_sum($split) - 1000.0) < 0.001);
check('remainder lands so split balances (333.34 + 333.33 + 333.33)', max($split) === 333.34 || max($split) === 333.34);
$split2 = epc_fin_alloc_split(1000, array('HQ' => 3, 'BR' => 1));
check('weighted split 750/250', abs($split2['HQ'] - 750) < 0.001 && abs($split2['BR'] - 250) < 0.001);
check('zero weights → empty', epc_fin_alloc_split(100, array('X' => 0)) === array());

section('Allocation rule + run (persist)');
$rule = epc_fin_alloc_rule_save($db, array('company_id' => $CO, 'code' => 'OH-DIST', 'name' => 'Overhead', 'source_account' => '6000', 'basis' => array('DEPT-A' => 2, 'DEPT-B' => 1)));
check('rule saved with basis', $rule > 0 && epc_fin_alloc_rules($db, $CO)[0]['basis_arr']['DEPT-A'] === 2);
$runLines = epc_fin_alloc_run($db, $rule, 900);
check('run splits 600/300 by 2:1', abs($runLines['DEPT-A'] - 600) < 0.001 && abs($runLines['DEPT-B'] - 300) < 0.001);
check('run persisted', (int) $db->query("SELECT COUNT(*) FROM `epc_fin_alloc_run` WHERE rule_id=" . $rule)->fetchColumn() === 1);

section('Pure: accrual schedule');
$sch = epc_fin_accrual_schedule(1200, 12);
check('1200 over 12 = 100 each', count($sch) === 12 && abs($sch[0] - 100) < 0.001);
check('accrual schedule sums to total', abs(array_sum($sch) - 1200.0) < 0.001);
$sch2 = epc_fin_accrual_schedule(1000, 3);
check('1000 over 3 sums exactly (remainder on last)', abs(array_sum($sch2) - 1000.0) < 0.001 && $sch2[2] !== $sch2[0]);

section('Accrual scheme (persist + period mapping)');
$acc = epc_fin_accrual_save($db, array('company_id' => $CO, 'code' => 'PREPAID-INS', 'description' => 'Prepaid insurance', 'total_amount' => 1200, 'periods' => 12, 'start_fy' => 2026, 'start_period' => 1));
$accRow = epc_fin_accruals($db, $CO)[0];
check('accrual saved with 12-row schedule', $acc > 0 && count($accRow['schedule']) === 12);
check('accrual schedule maps periods 1..12', (int) $accRow['schedule'][0]['period_no'] === 1 && (int) $accRow['schedule'][11]['period_no'] === 12);
$acc2 = epc_fin_accrual_save($db, array('company_id' => $CO, 'code' => 'X', 'total_amount' => 300, 'periods' => 4, 'start_fy' => 2026, 'start_period' => 11));
$acc2Row = epc_fin_accruals($db, $CO)[0];
check('accrual crossing year-end rolls fy', (int) $acc2Row['schedule'][2]['fy'] === 2027 && (int) $acc2Row['schedule'][2]['period_no'] === 1);

section('Pure: FX revaluation delta');
$g = epc_fin_fx_reval_delta(1000, 3600, 3.75); // book 3600, now 3750
check('gain delta = +150', abs($g['delta'] - 150) < 0.001 && abs($g['revalued_lc'] - 3750) < 0.001);
$l = epc_fin_fx_reval_delta(1000, 3800, 3.75); // book 3800, now 3750
check('loss delta = -50', abs($l['delta'] + 50) < 0.001);

section('FX revaluation run (persist)');
$fx = epc_fin_fx_revalue($db, $CO, array(
    array('account' => '1200', 'currency' => 'USD', 'fc_amount' => 1000, 'book_lc' => 3600, 'rate' => 3.75),
    array('account' => '2100', 'currency' => 'EUR', 'fc_amount' => 500, 'book_lc' => 2050, 'rate' => 4.00),
), time());
check('fx run computes per-line effect', $fx['lines'][0]['effect'] === 'gain' && abs($fx['lines'][0]['delta'] - 150) < 0.001);
check('fx total delta aggregates (150 - 50)', abs($fx['total_delta'] - 100) < 0.001);
check('fx run persisted', count(epc_fin_fx_runs($db, $CO)) === 1);

section('Summary + multi-company');
epc_fin_periods_generate($db, 2, 2026, 1);
check('company 2 sees only its periods', count(epc_fin_periods_list($db, 2)) === 12);
$sum = epc_fin_adv_summary($db, $CO);
check('summary counts alloc rules', $sum['alloc_rules'] === 1);
check('summary counts accruals', $sum['accruals'] === 2);
check('summary counts fx runs', $sum['fx_runs'] === 1);
check('summary open+closed periods = 12', ($sum['open_periods'] + $sum['closed_periods']) === 12);

echo "\n========================================\n";
echo 'FIN ADVANCED TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
