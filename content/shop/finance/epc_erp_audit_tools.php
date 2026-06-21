<?php
/**
 * Advanced ERP — High-level audit assurance tools.
 *
 * Deterministic checks an auditor/controller runs to verify the books:
 *  - Trial balance must net to zero (debits == credits).
 *  - GL control account must tie out to its subledger (AR/AP/bank).
 *  - Voucher sequence gap detection (using allocated voucher numbers).
 *  - Duplicate invoice/payment detection (same party + amount + ref/date).
 *  - Post-close change detection (entries dated into a closed period).
 *  - Exceptions roll-up for a dashboard.
 *
 * Additive: pure functions over supplied data; reads existing audit/GL via
 * other modules when present, never alters them.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_audit_trial_balance_check')) {
    /**
     * @param array<int,array{account:string,debit?:float,credit?:float}> $lines
     * @return array<string,mixed>
     */
    function epc_audit_trial_balance_check(array $lines): array
    {
        $dr = 0.0;
        $cr = 0.0;
        foreach ($lines as $l) {
            $dr = round($dr + (float) ($l['debit'] ?? 0), 2);
            $cr = round($cr + (float) ($l['credit'] ?? 0), 2);
        }
        $diff = round($dr - $cr, 2);
        return array(
            'total_debit' => $dr,
            'total_credit' => $cr,
            'difference' => $diff,
            'balanced' => abs($diff) < 0.01,
        );
    }
}

if (!function_exists('epc_audit_control_tieout')) {
    /**
     * Compare a GL control-account balance to the sum of its subledger.
     *
     * @param array<int,array{balance:float}>|array<int,float> $subledger
     * @return array<string,mixed>
     */
    function epc_audit_control_tieout(float $glControlBalance, array $subledger, float $tolerance = 0.01): array
    {
        $sub = 0.0;
        foreach ($subledger as $row) {
            $sub = round($sub + (is_array($row) ? (float) ($row['balance'] ?? 0) : (float) $row), 2);
        }
        $diff = round($glControlBalance - $sub, 2);
        return array(
            'gl_balance' => round($glControlBalance, 2),
            'subledger_total' => $sub,
            'difference' => $diff,
            'tied_out' => abs($diff) <= $tolerance,
        );
    }
}

if (!function_exists('epc_audit_sequence_gaps')) {
    /**
     * Detect missing numbers in a list of used sequence numbers between the
     * min and max present.
     *
     * @param array<int,int> $usedNumbers
     * @return array<string,mixed> {missing:int[], duplicates:int[], from:int, to:int}
     */
    function epc_audit_sequence_gaps(array $usedNumbers): array
    {
        $missing = array();
        $duplicates = array();
        if (empty($usedNumbers)) {
            return array('missing' => array(), 'duplicates' => array(), 'from' => 0, 'to' => 0);
        }
        $counts = array_count_values(array_map('intval', $usedNumbers));
        foreach ($counts as $n => $c) {
            if ($c > 1) {
                $duplicates[] = (int) $n;
            }
        }
        $nums = array_keys($counts);
        $from = min($nums);
        $to = max($nums);
        for ($i = $from; $i <= $to; $i++) {
            if (!isset($counts[$i])) {
                $missing[] = $i;
            }
        }
        sort($duplicates);
        return array('missing' => $missing, 'duplicates' => $duplicates, 'from' => $from, 'to' => $to);
    }
}

if (!function_exists('epc_audit_find_duplicates')) {
    /**
     * Detect probable duplicate documents by a composite key
     * (party_id + amount + reference). Returns groups with >1 member.
     *
     * @param array<int,array{id:mixed,party_id?:mixed,amount?:float,reference?:string}> $docs
     * @return array<int,array<string,mixed>>
     */
    function epc_audit_find_duplicates(array $docs): array
    {
        $groups = array();
        foreach ($docs as $d) {
            $key = (string) ($d['party_id'] ?? '') . '|' . number_format((float) ($d['amount'] ?? 0), 2, '.', '') . '|' . strtolower(trim((string) ($d['reference'] ?? '')));
            $groups[$key][] = $d;
        }
        $dups = array();
        foreach ($groups as $key => $members) {
            if (count($members) > 1) {
                $dups[] = array('key' => $key, 'count' => count($members), 'ids' => array_column($members, 'id'));
            }
        }
        return $dups;
    }
}

if (!function_exists('epc_audit_post_close_changes')) {
    /**
     * Detect entries dated on/before a period-close date (i.e. posted into a
     * closed period). Each entry: {id, date}.
     *
     * @param array<int,array{id:mixed,date:int}> $entries
     * @return array<int,mixed> ids violating the close
     */
    function epc_audit_post_close_changes(array $entries, int $closeDate): array
    {
        $violations = array();
        foreach ($entries as $e) {
            if ((int) ($e['date'] ?? 0) <= $closeDate) {
                $violations[] = $e['id'];
            }
        }
        return $violations;
    }
}

if (!function_exists('epc_audit_dashboard')) {
    /**
     * Roll up all checks into a single exceptions summary for a dashboard.
     *
     * @param array<string,array<string,mixed>> $checks name => result
     * @return array<string,mixed>
     */
    function epc_audit_dashboard(array $checks): array
    {
        $exceptions = array();
        foreach ($checks as $name => $res) {
            $ok = true;
            if (isset($res['balanced'])) {
                $ok = (bool) $res['balanced'];
            } elseif (isset($res['tied_out'])) {
                $ok = (bool) $res['tied_out'];
            } elseif (isset($res['reconciled'])) {
                $ok = (bool) $res['reconciled'];
            } elseif (isset($res['missing'])) {
                $ok = empty($res['missing']) && empty($res['duplicates']);
            } elseif (is_array($res) && isset($res[0])) {
                $ok = count($res) === 0;
            }
            if (!$ok) {
                $exceptions[] = $name;
            }
        }
        return array(
            'checks_run' => count($checks),
            'exceptions' => $exceptions,
            'exception_count' => count($exceptions),
            'clean' => count($exceptions) === 0,
        );
    }
}
