<?php
/**
 * CLI tests for Project accounting depth: budgets, transactions/actuals, pure
 * percent-complete + recognition (PoC / completed / straight-line) + WIP,
 * recognition runs, project P&L variance, summary, multi-company scope.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_project_accounting_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_project_accounting.php';

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

foreach (array('epc_prja_recognition', 'epc_prja_txn', 'epc_prja_budget') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_prja_ensure_schema($db);

$CO = 1;
$PRJ = 700;

section('Pure: percent complete (cost-to-cost)');
check('50% when half the cost incurred', abs(epc_prja_pct_complete(50000, 100000) - 0.5) < 0.0001);
check('clamped to 100% when over', abs(epc_prja_pct_complete(120000, 100000) - 1.0) < 0.0001);
check('0% with no cost', abs(epc_prja_pct_complete(0, 100000) - 0.0) < 0.0001);
check('unknown estimate + cost = 100%', abs(epc_prja_pct_complete(5000, 0) - 1.0) < 0.0001);

section('Pure: recognition methods + WIP');
$poc = epc_prja_recognize('poc', 200000, 50000, 100000, 30000); // 50% complete
check('PoC recognizes 50% revenue = 100000', abs($poc['recognized_revenue'] - 100000) < 0.001);
check('PoC WIP = recognized - billed = 70000', abs($poc['wip'] - 70000) < 0.001);
$comp1 = epc_prja_recognize('completed', 200000, 50000, 100000, 30000);
check('completed recognizes 0 before done', abs($comp1['recognized_revenue']) < 0.001);
$comp2 = epc_prja_recognize('completed', 200000, 100000, 100000, 30000);
check('completed recognizes full when 100%', abs($comp2['recognized_revenue'] - 200000) < 0.001 && abs($comp2['recognized_cost'] - 100000) < 0.001);
$sl = epc_prja_recognize('straight_line', 120000, 40000, 100000, 0, 0.25);
check('straight-line recognizes by elapsed fraction (25% = 30000)', abs($sl['recognized_revenue'] - 30000) < 0.001);
$adv = epc_prja_recognize('poc', 100000, 10000, 100000, 40000); // billed in advance
check('billed-in-advance gives negative WIP (deferred)', $adv['wip'] < 0 && abs($adv['wip'] + 30000) < 0.001);
check('unknown method falls back to poc', epc_prja_recognize('zzz', 100000, 50000, 100000, 0)['method'] === 'poc');

section('Budgets');
$b1 = epc_prja_budget_save($db, array('company_id' => $CO, 'project_id' => $PRJ, 'category' => 'labour', 'cost_budget' => 60000, 'revenue_budget' => 120000));
$b2 = epc_prja_budget_save($db, array('company_id' => $CO, 'project_id' => $PRJ, 'category' => 'materials', 'cost_budget' => 40000, 'revenue_budget' => 80000));
check('two budget lines saved', $b1 > 0 && $b2 > 0 && count(epc_prja_budgets($db, $PRJ)) === 2);
$bt = epc_prja_budget_totals($db, $PRJ);
check('budget totals 100000 cost / 200000 revenue', abs($bt['cost_budget'] - 100000) < 0.001 && abs($bt['revenue_budget'] - 200000) < 0.001);

section('Transactions + actuals');
epc_prja_txn_add($db, array('company_id' => $CO, 'project_id' => $PRJ, 'txn_type' => 'cost', 'category' => 'labour', 'amount' => 30000));
epc_prja_txn_add($db, array('company_id' => $CO, 'project_id' => $PRJ, 'txn_type' => 'cost', 'category' => 'materials', 'amount' => 20000));
epc_prja_txn_add($db, array('company_id' => $CO, 'project_id' => $PRJ, 'txn_type' => 'billing', 'amount' => 30000));
check('three transactions logged', count(epc_prja_txns($db, $PRJ)) === 3);
$act = epc_prja_actuals($db, $PRJ);
check('actual cost = 50000, billing = 30000', abs($act['cost'] - 50000) < 0.001 && abs($act['billing'] - 30000) < 0.001);
check('invalid txn type rejected', (function () use ($db, $CO, $PRJ) { try { epc_prja_txn_add($db, array('company_id' => $CO, 'project_id' => $PRJ, 'txn_type' => 'bogus', 'amount' => 1)); return false; } catch (Throwable $e) { return true; } })());

section('Recognition run (persist, uses budget + actuals)');
$run = epc_prja_recognition_run($db, $CO, $PRJ, 'poc');
check('run: 50% complete from 50k/100k cost', abs((float) $run['pct_complete'] - 0.5) < 0.0001);
check('run: recognized revenue = 100000 (50% of 200k)', abs($run['recognized_revenue'] - 100000) < 0.001);
check('run: WIP = 100000 - 30000 billed = 70000', abs($run['wip'] - 70000) < 0.001);
check('run persisted', count(epc_prja_recognitions($db, $PRJ)) === 1);

section('Project P&L variance');
$pnl = epc_prja_pnl($db, $PRJ);
check('budget margin 100000', abs($pnl['budget_margin'] - 100000) < 0.001);
check('cost variance 50000 (100k budget - 50k actual)', abs($pnl['cost_variance'] - 50000) < 0.001);
check('not over budget', $pnl['over_budget'] === false);
epc_prja_txn_add($db, array('company_id' => $CO, 'project_id' => $PRJ, 'txn_type' => 'cost', 'amount' => 60000));
check('flips to over budget after extra cost', epc_prja_pnl($db, $PRJ)['over_budget'] === true);

section('Summary + multi-company');
epc_prja_budget_save($db, array('company_id' => 2, 'project_id' => 999, 'category' => 'x', 'cost_budget' => 5, 'revenue_budget' => 10));
check('company 2 isolated in summary', epc_prja_summary($db, 2)['projects_with_budget'] === 1);
$sum = epc_prja_summary($db, $CO);
check('summary projects_with_budget = 1', $sum['projects_with_budget'] === 1);
check('summary cost_budget = 100000', abs($sum['cost_budget'] - 100000) < 0.001);
check('summary reflects latest WIP', abs($sum['wip'] - 70000) < 0.001);

echo "\n========================================\n";
echo 'PROJECT ACCOUNTING TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
