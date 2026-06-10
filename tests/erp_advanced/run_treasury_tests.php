<?php
/**
 * CLI tests for Treasury (daily cash, monitoring, statement->P&L/BS crosscheck)
 * and Audit assurance tools.
 *
 *   php tests/erp_advanced/run_treasury_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_treasury.php';
require_once $fin . '/epc_erp_audit_tools.php';

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

section('Daily cash report');
$mov = array(
    array('date' => 100, 'account' => 'CASH', 'type' => 'receipt', 'amount' => 2000),
    array('date' => 100, 'account' => 'CASH', 'type' => 'payment', 'amount' => 500),
    array('date' => 100, 'account' => 'BANK', 'type' => 'receipt', 'amount' => 10000),
    array('date' => 100, 'account' => 'BANK', 'type' => 'payment', 'amount' => 3000),
    array('date' => 200, 'account' => 'CASH', 'type' => 'receipt', 'amount' => 999), // out of window
);
$cash = epc_trez_daily_cash($mov, 5000.0, 100, 100);
check('receipts = 12000', abs($cash['receipts'] - 12000.0) < 0.01);
check('payments = 3500', abs($cash['payments'] - 3500.0) < 0.01);
check('closing = opening 5000 + 12000 - 3500 = 13500', abs($cash['closing'] - 13500.0) < 0.01);
check('per-account CASH net = 1500', abs($cash['by_account']['CASH']['net'] - 1500.0) < 0.01);
check('out-of-window movement excluded', !isset($cash['by_account']['CASH']['receipts']) || abs($cash['by_account']['CASH']['receipts'] - 2000.0) < 0.01);

section('Cash & bank monitoring');
$mon = epc_trez_monitor(
    array(
        array('account' => 'BANK-MAIN', 'balance' => 250000, 'min_balance' => 50000, 'unreconciled' => 3),
        array('account' => 'BANK-FX', 'balance' => -1200),
        array('account' => 'PETTY', 'balance' => 800, 'min_balance' => 1000),
    ),
    array(array('account' => 'BANK-MAIN', 'amount' => 90000)),
    50000.0
);
check('total position summed', abs($mon['total_position'] - (250000 - 1200 + 800)) < 0.01);
$types = array_column($mon['alerts'], 'type');
check('negative balance flagged', in_array('negative_balance', $types, true));
check('below-minimum flagged', in_array('below_minimum', $types, true));
check('unreconciled items flagged', in_array('unreconciled_items', $types, true));
check('large transaction flagged', in_array('large_transaction', $types, true));

section('AI bank statement -> cash-basis P&L');
$lines = array(
    array('date' => 1, 'description' => 'Customer payment', 'amount' => 100000),
    array('date' => 2, 'description' => 'Supplier payment', 'amount' => -60000),
    array('date' => 3, 'description' => 'Salary', 'amount' => -20000),
    array('date' => 4, 'description' => 'Owner capital', 'amount' => 50000, 'tag' => 'capital'),
    array('date' => 5, 'description' => 'Transfer to FX acct', 'amount' => -10000, 'tag' => 'transfer'),
);
$view = epc_trez_classify_statement($lines);
check('income = 100000 (capital excluded)', abs($view['income'] - 100000.0) < 0.01);
check('expense = 80000 (transfer excluded)', abs($view['expense'] - 80000.0) < 0.01);
check('cash-basis profit = 20000', abs($view['net_profit_cash'] - 20000.0) < 0.01);
check('capital_in tracked = 50000', abs($view['capital_in'] - 50000.0) < 0.01);
check('net cash movement = 60000', abs($view['net_cash'] - 60000.0) < 0.01);

section('Statement vs GL cross-check');
// GL agrees (accrual P&L profit 20000) -> reconciled within tolerance.
$ccOk = epc_trez_crosscheck($view, array('total_revenue' => 100000, 'total_expenses' => 80000, 'net_profit' => 20000), 0.0);
check('books tie to bank (profit variance 0)', $ccOk['reconciled'] === true && abs($ccOk['profit_variance']) < 0.01);
// GL shows extra accrued revenue not yet in bank -> variance surfaced.
$ccVar = epc_trez_crosscheck($view, array('total_revenue' => 130000, 'total_expenses' => 80000, 'net_profit' => 50000), 0.0);
check('variance surfaced when GL != bank', $ccVar['reconciled'] === false && abs($ccVar['profit_variance'] - (-30000.0)) < 0.01);
check('income variance = -30000', abs($ccVar['income_variance'] - (-30000.0)) < 0.01);

section('Audit assurance — trial balance & tie-outs');
$tb = epc_audit_trial_balance_check(array(
    array('account' => '1000', 'debit' => 5000),
    array('account' => '4000', 'credit' => 5000),
));
check('balanced trial balance', $tb['balanced'] === true);
$tbBad = epc_audit_trial_balance_check(array(
    array('account' => '1000', 'debit' => 5000),
    array('account' => '4000', 'credit' => 4900),
));
check('unbalanced TB flagged (diff 100)', $tbBad['balanced'] === false && abs($tbBad['difference'] - 100.0) < 0.01);
$tie = epc_audit_control_tieout(120000.0, array(array('balance' => 70000), array('balance' => 50000)));
check('AR control ties to subledger', $tie['tied_out'] === true);
$tieBad = epc_audit_control_tieout(120000.0, array(array('balance' => 70000), array('balance' => 49000)));
check('AR mismatch flagged (diff 1000)', $tieBad['tied_out'] === false && abs($tieBad['difference'] - 1000.0) < 0.01);

section('Audit assurance — gaps, duplicates, post-close');
$gap = epc_audit_sequence_gaps(array(1, 2, 3, 5, 6, 6));
check('missing voucher 4 detected', in_array(4, $gap['missing'], true));
check('duplicate voucher 6 detected', in_array(6, $gap['duplicates'], true));
$dups = epc_audit_find_duplicates(array(
    array('id' => 'A', 'party_id' => 10, 'amount' => 500, 'reference' => 'INV-1'),
    array('id' => 'B', 'party_id' => 10, 'amount' => 500, 'reference' => 'inv-1'),
    array('id' => 'C', 'party_id' => 11, 'amount' => 500, 'reference' => 'INV-1'),
));
check('duplicate invoice pair detected (A,B)', count($dups) === 1 && $dups[0]['count'] === 2);
$viol = epc_audit_post_close_changes(array(
    array('id' => 'X', 'date' => 90),
    array('id' => 'Y', 'date' => 110),
), 100);
check('post-close violation detected (X dated before close)', $viol === array('X'));

section('Audit dashboard roll-up');
$dash = epc_audit_dashboard(array(
    'trial_balance' => $tbBad,
    'ar_tieout' => $tie,
    'crosscheck' => $ccOk,
    'sequence' => $gap,
    'duplicates' => $dups,
));
check('dashboard flags TB, sequence, duplicates as exceptions', in_array('trial_balance', $dash['exceptions'], true) && in_array('sequence', $dash['exceptions'], true) && in_array('duplicates', $dash['exceptions'], true));
check('clean checks not flagged (ar_tieout, crosscheck ok)', !in_array('ar_tieout', $dash['exceptions'], true) && !in_array('crosscheck', $dash['exceptions'], true));

echo "\n========================================\n";
echo "TREASURY + AUDIT TOOLS TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
