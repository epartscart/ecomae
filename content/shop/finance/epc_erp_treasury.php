<?php
/**
 * Advanced ERP — Treasury: daily cash report, cash/bank monitoring, and an
 * AI-from-bank-statement P&L/BS cross-check.
 *
 * - Daily cash report: opening + receipts - payments = closing per account/day,
 *   computed from a set of cash/bank movements.
 * - Cash & bank monitoring: live position across accounts with low-balance /
 *   negative-balance / large-transaction flags and unreconciled ageing.
 * - Statement-derived P&L/BS: classify imported bank-statement lines (deposits
 *   = income, withdrawals = expense unless tagged as capital/transfer), build a
 *   cash-basis P&L + cash movement, then cross-check against the GL P&L/BS and
 *   surface the variance so the books can be verified against real bank money.
 *
 * Additive: reuses bank-rec helpers in epc_erp_finance_suite.php when present;
 * does not alter existing tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_trez_daily_cash')) {
    /**
     * Daily cash report from a list of movements.
     *
     * @param array<int,array{date:int,account:string,type:string,amount:float}> $movements
     *        type: 'receipt' (in) or 'payment' (out)
     * @param float $opening opening balance at start of the day window
     * @return array<string,mixed> {opening, receipts, payments, closing, by_account}
     */
    function epc_trez_daily_cash(array $movements, float $opening = 0.0, int $dayStart = 0, int $dayEnd = 0): array
    {
        $receipts = 0.0;
        $payments = 0.0;
        $byAccount = array();
        foreach ($movements as $m) {
            $d = (int) ($m['date'] ?? 0);
            if ($dayStart > 0 && $d < $dayStart) {
                continue;
            }
            if ($dayEnd > 0 && $d > $dayEnd) {
                continue;
            }
            $acc = (string) ($m['account'] ?? '');
            $amt = round((float) ($m['amount'] ?? 0), 2);
            if (!isset($byAccount[$acc])) {
                $byAccount[$acc] = array('receipts' => 0.0, 'payments' => 0.0, 'net' => 0.0);
            }
            if (($m['type'] ?? '') === 'receipt') {
                $receipts = round($receipts + $amt, 2);
                $byAccount[$acc]['receipts'] = round($byAccount[$acc]['receipts'] + $amt, 2);
                $byAccount[$acc]['net'] = round($byAccount[$acc]['net'] + $amt, 2);
            } else {
                $payments = round($payments + $amt, 2);
                $byAccount[$acc]['payments'] = round($byAccount[$acc]['payments'] + $amt, 2);
                $byAccount[$acc]['net'] = round($byAccount[$acc]['net'] - $amt, 2);
            }
        }
        return array(
            'opening' => round($opening, 2),
            'receipts' => $receipts,
            'payments' => $payments,
            'closing' => round($opening + $receipts - $payments, 2),
            'by_account' => $byAccount,
        );
    }
}

if (!function_exists('epc_trez_monitor')) {
    /**
     * Cash & bank monitoring across accounts. Flags negative balances, balances
     * below a per-account minimum, and large single transactions.
     *
     * @param array<int,array{account:string,balance:float,min_balance?:float,unreconciled?:int}> $accounts
     * @param array<int,array{account:string,amount:float}> $recentTxns
     * @param float $largeTxnThreshold
     * @return array<string,mixed>
     */
    function epc_trez_monitor(array $accounts, array $recentTxns = array(), float $largeTxnThreshold = 50000.0): array
    {
        $total = 0.0;
        $alerts = array();
        foreach ($accounts as $a) {
            $bal = round((float) ($a['balance'] ?? 0), 2);
            $total = round($total + $bal, 2);
            $min = (float) ($a['min_balance'] ?? 0);
            if ($bal < 0) {
                $alerts[] = array('account' => $a['account'], 'severity' => 'critical', 'type' => 'negative_balance', 'balance' => $bal);
            } elseif ($min > 0 && $bal < $min) {
                $alerts[] = array('account' => $a['account'], 'severity' => 'warning', 'type' => 'below_minimum', 'balance' => $bal, 'min' => $min);
            }
            if ((int) ($a['unreconciled'] ?? 0) > 0) {
                $alerts[] = array('account' => $a['account'], 'severity' => 'info', 'type' => 'unreconciled_items', 'count' => (int) $a['unreconciled']);
            }
        }
        foreach ($recentTxns as $t) {
            if (abs((float) ($t['amount'] ?? 0)) >= $largeTxnThreshold) {
                $alerts[] = array('account' => $t['account'] ?? '', 'severity' => 'info', 'type' => 'large_transaction', 'amount' => round((float) $t['amount'], 2));
            }
        }
        return array(
            'total_position' => $total,
            'account_count' => count($accounts),
            'alerts' => $alerts,
            'alert_count' => count($alerts),
        );
    }
}

if (!function_exists('epc_trez_classify_statement')) {
    /**
     * Classify bank-statement lines into a cash-basis view. Each line:
     *   {date, description, amount (>0 deposit, <0 withdrawal), tag?}
     * tag overrides: 'capital', 'transfer', 'owner', 'loan' -> not P&L.
     *
     * @param array<int,array<string,mixed>> $lines
     * @return array<string,mixed> {income, expense, net_cash, capital_in, capital_out, transfers, lines}
     */
    function epc_trez_classify_statement(array $lines): array
    {
        $income = 0.0;
        $expense = 0.0;
        $capitalIn = 0.0;
        $capitalOut = 0.0;
        $transfers = 0.0;
        $netCash = 0.0;
        $classified = array();
        $nonPl = array('capital', 'transfer', 'owner', 'loan', 'drawings');
        foreach ($lines as $ln) {
            $amt = round((float) ($ln['amount'] ?? 0), 2);
            $tag = strtolower((string) ($ln['tag'] ?? ''));
            $netCash = round($netCash + $amt, 2);
            $cls = '';
            if (in_array($tag, $nonPl, true)) {
                if ($tag === 'transfer') {
                    $transfers = round($transfers + abs($amt), 2);
                    $cls = 'transfer';
                } elseif ($amt >= 0) {
                    $capitalIn = round($capitalIn + $amt, 2);
                    $cls = 'capital_in';
                } else {
                    $capitalOut = round($capitalOut + abs($amt), 2);
                    $cls = 'capital_out';
                }
            } elseif ($amt >= 0) {
                $income = round($income + $amt, 2);
                $cls = 'income';
            } else {
                $expense = round($expense + abs($amt), 2);
                $cls = 'expense';
            }
            $ln['class'] = $cls;
            $classified[] = $ln;
        }
        return array(
            'income' => $income,
            'expense' => $expense,
            'net_profit_cash' => round($income - $expense, 2),
            'net_cash' => $netCash,
            'capital_in' => $capitalIn,
            'capital_out' => $capitalOut,
            'transfers' => $transfers,
            'lines' => $classified,
        );
    }
}

if (!function_exists('epc_trez_crosscheck')) {
    /**
     * Cross-check the statement-derived (cash-basis) P&L against the GL/accrual
     * P&L and surface the variance. A small/zero variance => books agree with
     * the bank; a large variance flags something to investigate (timing,
     * unposted entries, accruals).
     *
     * @param array<string,mixed> $statementView output of epc_trez_classify_statement
     * @param array<string,mixed> $glPl GL P&L report (total_revenue/total_expenses/net_profit)
     * @param float $tolerance acceptable absolute variance
     * @return array<string,mixed>
     */
    function epc_trez_crosscheck(array $statementView, array $glPl, float $tolerance = 0.0): array
    {
        $cashProfit = (float) ($statementView['net_profit_cash'] ?? 0);
        $glRevenue = (float) ($glPl['total_revenue'] ?? 0);
        $glExpense = (float) ($glPl['total_expenses'] ?? 0);
        $glProfit = (float) ($glPl['net_profit'] ?? ($glRevenue - $glExpense));

        $incomeVar = round((float) ($statementView['income'] ?? 0) - $glRevenue, 2);
        $expenseVar = round((float) ($statementView['expense'] ?? 0) - $glExpense, 2);
        $profitVar = round($cashProfit - $glProfit, 2);

        return array(
            'cash_income' => round((float) ($statementView['income'] ?? 0), 2),
            'gl_income' => round($glRevenue, 2),
            'income_variance' => $incomeVar,
            'cash_expense' => round((float) ($statementView['expense'] ?? 0), 2),
            'gl_expense' => round($glExpense, 2),
            'expense_variance' => $expenseVar,
            'cash_profit' => round($cashProfit, 2),
            'gl_profit' => round($glProfit, 2),
            'profit_variance' => $profitVar,
            'reconciled' => abs($profitVar) <= $tolerance,
        );
    }
}
