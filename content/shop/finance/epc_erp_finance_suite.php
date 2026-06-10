<?php
/**
 * Advanced ERP — Finance suite.
 *
 * Additive finance intelligence layered on the existing GL / cash-bank / tax
 * modules. Adds:
 *   - AI-assisted bank reconciliation (auto-match statement lines to cash/bank
 *     entries by amount, direction, date proximity and reference similarity)
 *   - Worldwide VAT return (output vs input tax for a period, net payable)
 *   - Corporate-tax estimate (taxable profit from the P&L x CIT rate)
 *   - A consolidated reporting center with a plain-language narrative
 *
 * Reuses (does not duplicate): epc_erp_gl.php (P&L / balance sheet),
 * epc_erp_bank_statement_lines + epc_erp_cash_bank_entries, and the worldwide
 * tax toolkit. No existing tables are modified.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_advanced.php';

/* ==========================================================================
 * AI bank reconciliation
 * ======================================================================== */

if (!function_exists('epc_fin_text_similarity')) {
    /**
     * Token overlap similarity (0..1) between two reference/description strings.
     */
    function epc_fin_text_similarity(string $a, string $b): float
    {
        $norm = static function (string $s): array {
            $s = strtolower(preg_replace('/[^a-z0-9 ]+/i', ' ', $s) ?? '');
            $parts = preg_split('/\s+/', trim($s)) ?: array();
            return array_values(array_filter($parts, static function ($t) {
                return $t !== '' && strlen($t) >= 2;
            }));
        };
        $ta = $norm($a);
        $tb = $norm($b);
        if (!$ta || !$tb) {
            return 0.0;
        }
        $setB = array_flip($tb);
        $hits = 0;
        foreach (array_unique($ta) as $t) {
            if (isset($setB[$t])) {
                $hits++;
            }
        }
        $denom = max(count(array_unique($ta)), count(array_unique($tb)));
        return $denom > 0 ? $hits / $denom : 0.0;
    }
}

if (!function_exists('epc_fin_bank_match_score')) {
    /**
     * Score a candidate match between a statement line and a cash/bank entry.
     * Returns 0..1; amount+direction must agree to score at all.
     *
     * @param array<string,mixed> $line  bank statement line row
     * @param array<string,mixed> $entry cash/bank entry row
     */
    function epc_fin_bank_match_score(array $line, array $entry, int $tolDays = 5): float
    {
        if ((int) $line['direction'] !== (int) $entry['direction']) {
            return 0.0;
        }
        if (abs((float) $line['amount'] - (float) $entry['amount']) > 0.01) {
            return 0.0;
        }
        $score = 0.7; // exact amount + direction

        $lineDate = (int) $line['line_date'];
        $entryDate = (int) ($entry['time'] ?? 0);
        $daysApart = $lineDate > 0 && $entryDate > 0 ? abs($lineDate - $entryDate) / 86400 : 999;
        if ($daysApart <= $tolDays) {
            $score += 0.2 * (1 - ($daysApart / max(1, $tolDays)));
        }

        $sim = epc_fin_text_similarity(
            (string) ($line['reference'] ?? '') . ' ' . (string) ($line['description'] ?? ''),
            (string) ($entry['reference'] ?? '') . ' ' . (string) ($entry['note'] ?? '')
        );
        $score += 0.1 * $sim;

        return min(1.0, round($score, 4));
    }
}

if (!function_exists('epc_fin_bank_auto_reconcile')) {
    /**
     * Auto-match unreconciled statement lines for an account against open
     * cash/bank entries. Lines scoring >= $threshold are matched (greedy, best
     * first). Unmatched lines are returned as suggestions.
     *
     * @return array<string,mixed>
     */
    function epc_fin_bank_auto_reconcile(PDO $db, int $accountId, float $threshold = 0.8, int $tolDays = 5, bool $commit = true): array
    {
        $ls = $db->prepare(
            "SELECT * FROM `epc_erp_bank_statement_lines` WHERE `account_id` = ? AND `matched_entry_id` = 0 ORDER BY `line_date`"
        );
        $ls->execute(array($accountId));
        $lines = $ls->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $es = $db->prepare(
            "SELECT e.* FROM `epc_erp_cash_bank_entries` e
             WHERE e.`account_id` = ? AND e.`active` = 1
               AND NOT EXISTS (
                   SELECT 1 FROM `epc_erp_bank_statement_lines` b
                   WHERE b.`matched_entry_id` = e.`id`
               )"
        );
        $es->execute(array($accountId));
        $entries = $es->fetchAll(PDO::FETCH_ASSOC) ?: array();

        $usedEntry = array();
        $matched = array();
        $suggestions = array();

        foreach ($lines as $line) {
            $best = null;
            $bestScore = 0.0;
            foreach ($entries as $entry) {
                if (isset($usedEntry[(int) $entry['id']])) {
                    continue;
                }
                $sc = epc_fin_bank_match_score($line, $entry, $tolDays);
                if ($sc > $bestScore) {
                    $bestScore = $sc;
                    $best = $entry;
                }
            }
            if ($best !== null && $bestScore >= $threshold) {
                $usedEntry[(int) $best['id']] = true;
                $matched[] = array(
                    'line_id' => (int) $line['id'],
                    'entry_id' => (int) $best['id'],
                    'amount' => (float) $line['amount'],
                    'score' => $bestScore,
                );
                if ($commit) {
                    epc_fin_bank_set_match($db, (int) $line['id'], (int) $best['id']);
                }
            } else {
                $suggestions[] = array(
                    'line_id' => (int) $line['id'],
                    'amount' => (float) $line['amount'],
                    'best_entry_id' => $best ? (int) $best['id'] : 0,
                    'best_score' => $bestScore,
                );
            }
        }

        return array(
            'account_id' => $accountId,
            'lines_scanned' => count($lines),
            'matched' => $matched,
            'matched_count' => count($matched),
            'unmatched' => $suggestions,
            'unmatched_count' => count($suggestions),
            'committed' => $commit,
        );
    }
}

if (!function_exists('epc_fin_bank_set_match')) {
    function epc_fin_bank_set_match(PDO $db, int $lineId, int $entryId): void
    {
        $db->prepare('UPDATE `epc_erp_bank_statement_lines` SET `matched_entry_id` = ? WHERE `id` = ?')
           ->execute(array($entryId, $lineId));
    }
}

if (!function_exists('epc_fin_bank_recon_summary')) {
    /**
     * @return array<string,mixed>
     */
    function epc_fin_bank_recon_summary(PDO $db, int $accountId): array
    {
        $st = $db->prepare(
            "SELECT
                COUNT(*) AS total,
                SUM(CASE WHEN `matched_entry_id` > 0 THEN 1 ELSE 0 END) AS matched,
                SUM(CASE WHEN `matched_entry_id` = 0 THEN 1 ELSE 0 END) AS unmatched,
                COALESCE(SUM(CASE WHEN `matched_entry_id` = 0 THEN (CASE WHEN `direction`=1 THEN `amount` ELSE -`amount` END) ELSE 0 END),0) AS unmatched_net
             FROM `epc_erp_bank_statement_lines` WHERE `account_id` = ?"
        );
        $st->execute(array($accountId));
        $r = $st->fetch(PDO::FETCH_ASSOC) ?: array();
        $total = (int) ($r['total'] ?? 0);
        $matched = (int) ($r['matched'] ?? 0);
        return array(
            'account_id' => $accountId,
            'total_lines' => $total,
            'matched' => $matched,
            'unmatched' => (int) ($r['unmatched'] ?? 0),
            'match_rate' => $total > 0 ? round($matched / $total * 100, 1) : 0.0,
            'unmatched_net' => round((float) ($r['unmatched_net'] ?? 0), 2),
        );
    }
}

/* ==========================================================================
 * Worldwide VAT / GST return
 * ======================================================================== */

if (!function_exists('epc_fin_vat_return')) {
    /**
     * Build a VAT/GST return for a period from sales orders (output tax) and
     * purchase invoices (input tax). Worldwide: uses the amounts already stored
     * on each document (computed by the tax toolkit at entry time).
     *
     * @return array<string,mixed>
     */
    function epc_fin_vat_return(PDO $db, int $dateFrom, int $dateTo): array
    {
        $out = array(
            'period_from' => $dateFrom,
            'period_to' => $dateTo,
            'output' => array('net' => 0.0, 'tax' => 0.0, 'count' => 0),
            'input' => array('net' => 0.0, 'tax' => 0.0, 'count' => 0),
        );

        // Output tax: confirmed/invoiced sales orders in period.
        try {
            $st = $db->prepare(
                "SELECT COUNT(*) c, COALESCE(SUM(`amount_ex_vat`),0) net, COALESCE(SUM(`vat_amount`),0) tax
                 FROM `epc_erp_sales_orders`
                 WHERE `status` IN ('confirmed','invoiced') AND `time_created` BETWEEN ? AND ?"
            );
            $st->execute(array($dateFrom, $dateTo));
            $r = $st->fetch(PDO::FETCH_ASSOC) ?: array();
            $out['output'] = array('net' => round((float) $r['net'], 2), 'tax' => round((float) $r['tax'], 2), 'count' => (int) $r['c']);
        } catch (Throwable $e) {
            // table absent -> leave zeros
        }

        // Input tax: purchase invoices in period.
        try {
            $st = $db->prepare(
                "SELECT COUNT(*) c, COALESCE(SUM(`amount_ex_vat`),0) net, COALESCE(SUM(`vat_amount`),0) tax
                 FROM `epc_erp_purchases`
                 WHERE `status` IN ('confirmed','paid','partial') AND `purchase_date` BETWEEN ? AND ?"
            );
            $st->execute(array($dateFrom, $dateTo));
            $r = $st->fetch(PDO::FETCH_ASSOC) ?: array();
            $out['input'] = array('net' => round((float) $r['net'], 2), 'tax' => round((float) $r['tax'], 2), 'count' => (int) $r['c']);
        } catch (Throwable $e) {
            // table absent -> leave zeros
        }

        $netPayable = round($out['output']['tax'] - $out['input']['tax'], 2);
        $out['net_tax_payable'] = $netPayable;
        $out['position'] = $netPayable >= 0 ? 'payable' : 'refundable';

        // Box-style mapping (generic, country-neutral).
        $out['boxes'] = array(
            'standard_rated_sales_net' => $out['output']['net'],
            'output_tax' => $out['output']['tax'],
            'purchases_net' => $out['input']['net'],
            'input_tax' => $out['input']['tax'],
            'net_tax_due' => $netPayable,
        );
        return $out;
    }
}

/* ==========================================================================
 * Corporate tax estimate
 * ======================================================================== */

if (!function_exists('epc_fin_corporate_tax')) {
    /**
     * Estimate corporate income tax for a period: taxable profit (from the GL
     * P&L) above a threshold, times a CIT rate. Rate/threshold can be passed in
     * or default to UAE-style (9% over 375,000) when not supplied.
     *
     * @return array<string,mixed>
     */
    function epc_fin_corporate_tax(PDO $db, int $dateFrom, int $dateTo, ?float $ratePercent = null, float $threshold = 0.0): array
    {
        $netProfit = 0.0;
        $revenue = 0.0;
        $expenses = 0.0;
        if (function_exists('epc_erp_gl_pl_report')) {
            try {
                $pl = epc_erp_gl_pl_report($db, $dateFrom, $dateTo);
                $revenue = (float) ($pl['total_revenue'] ?? 0);
                $expenses = (float) ($pl['total_expenses'] ?? 0);
                $netProfit = (float) ($pl['net_profit'] ?? ($revenue - $expenses));
            } catch (Throwable $e) {
                $netProfit = 0.0;
            }
        }

        if ($ratePercent === null) {
            $ratePercent = 9.0;
            $threshold = $threshold > 0 ? $threshold : 375000.0;
        }

        $taxable = max(0.0, $netProfit - max(0.0, $threshold));
        $tax = round($taxable * ($ratePercent / 100), 2);

        return array(
            'period_from' => $dateFrom,
            'period_to' => $dateTo,
            'revenue' => round($revenue, 2),
            'expenses' => round($expenses, 2),
            'net_profit' => round($netProfit, 2),
            'threshold' => round($threshold, 2),
            'rate_percent' => $ratePercent,
            'taxable_profit' => round($taxable, 2),
            'corporate_tax' => $tax,
        );
    }
}

/* ==========================================================================
 * Consolidated reporting center + narrative
 * ======================================================================== */

if (!function_exists('epc_fin_report_center')) {
    /**
     * One-call consolidated report payload across finance, tax, inventory, CRM
     * and SCM. Each section degrades gracefully if its module is unavailable.
     *
     * @return array<string,mixed>
     */
    function epc_fin_report_center(PDO $db, int $dateFrom, int $dateTo): array
    {
        $report = array('period_from' => $dateFrom, 'period_to' => $dateTo, 'sections' => array());

        if (function_exists('epc_erp_gl_pl_report')) {
            try {
                $report['sections']['profit_and_loss'] = epc_erp_gl_pl_report($db, $dateFrom, $dateTo);
            } catch (Throwable $e) {
            }
        }
        if (function_exists('epc_erp_gl_balance_sheet')) {
            try {
                $report['sections']['balance_sheet'] = epc_erp_gl_balance_sheet($db, $dateTo);
            } catch (Throwable $e) {
            }
        }
        try {
            $report['sections']['vat_return'] = epc_fin_vat_return($db, $dateFrom, $dateTo);
        } catch (Throwable $e) {
        }
        try {
            $report['sections']['corporate_tax'] = epc_fin_corporate_tax($db, $dateFrom, $dateTo);
        } catch (Throwable $e) {
        }
        if (function_exists('epc_erp_inventory_valuation_total')) {
            try {
                $report['sections']['inventory_value'] = epc_erp_inventory_valuation_total($db);
            } catch (Throwable $e) {
            }
        }
        if (function_exists('epc_crm_adv_pipeline_forecast')) {
            try {
                $report['sections']['crm_forecast'] = epc_crm_adv_pipeline_forecast($db);
            } catch (Throwable $e) {
            }
        }
        if (function_exists('epc_scm_logistics_dashboard')) {
            try {
                $report['sections']['logistics'] = epc_scm_logistics_dashboard($db);
            } catch (Throwable $e) {
            }
        }

        $report['narrative'] = epc_fin_report_narrative($report);
        return $report;
    }
}

if (!function_exists('epc_fin_report_narrative')) {
    /**
     * Deterministic plain-language summary of a report-center payload. (No
     * external LLM — readable insights generated from the figures.)
     *
     * @param array<string,mixed> $report
     * @return array<int,string>
     */
    function epc_fin_report_narrative(array $report): array
    {
        $lines = array();
        $s = $report['sections'] ?? array();

        if (isset($s['profit_and_loss'])) {
            $pl = $s['profit_and_loss'];
            $rev = (float) ($pl['total_revenue'] ?? 0);
            $np = (float) ($pl['net_profit'] ?? 0);
            $margin = $rev > 0 ? round($np / $rev * 100, 1) : 0.0;
            $word = $np >= 0 ? 'profit' : 'loss';
            $lines[] = "Revenue was " . number_format($rev, 2) . " with a net {$word} of " . number_format($np, 2) . " ({$margin}% margin).";
        }
        if (isset($s['vat_return'])) {
            $v = $s['vat_return'];
            $lines[] = "VAT/GST for the period: output tax " . number_format((float) $v['output']['tax'], 2)
                . " minus input tax " . number_format((float) $v['input']['tax'], 2)
                . " = " . number_format((float) $v['net_tax_payable'], 2) . " " . (string) $v['position'] . ".";
        }
        if (isset($s['corporate_tax'])) {
            $ct = $s['corporate_tax'];
            if ((float) $ct['taxable_profit'] > 0) {
                $lines[] = "Estimated corporate tax is " . number_format((float) $ct['corporate_tax'], 2)
                    . " on taxable profit of " . number_format((float) $ct['taxable_profit'], 2)
                    . " at " . (float) $ct['rate_percent'] . "%.";
            } else {
                $lines[] = "No corporate tax is estimated (profit below the threshold).";
            }
        }
        if (isset($s['crm_forecast'])) {
            $f = $s['crm_forecast'];
            $lines[] = "CRM pipeline holds " . (int) ($f['open_count'] ?? 0) . " open opportunities worth "
                . number_format((float) ($f['open_value'] ?? 0), 2) . " (weighted "
                . number_format((float) ($f['weighted_value'] ?? 0), 2) . ").";
        }
        if (isset($s['logistics']) && (int) ($s['logistics']['overdue'] ?? 0) > 0) {
            $lines[] = "Attention: " . (int) $s['logistics']['overdue'] . " shipment(s) are overdue.";
        }
        if (!$lines) {
            $lines[] = "No financial activity recorded for this period yet.";
        }
        return $lines;
    }
}
