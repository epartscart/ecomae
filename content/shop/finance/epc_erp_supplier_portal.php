<?php
/**
 * Supplier Portal / Supplier Performance.
 *
 * Computes per-supplier performance scorecards from existing procurement data
 * (purchase orders, goods receipts, RFQs, payables) and exposes a per-supplier
 * detail view. Read-only analytics over the live ERP tables.
 */

if (!defined('EPC_ERP_BOOTSTRAP')) {
    // allow standalone include in the ERP shell
}

if (!function_exists('epc_sp_safe_query')) {
    /**
     * Run a query, returning [] on any failure (missing table, etc.).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_sp_safe_query(PDO $db, string $sql, array $args = array()): array
    {
        try {
            $st = $db->prepare($sql);
            $st->execute($args);
            return $st->fetchAll(PDO::FETCH_ASSOC);
        } catch (Throwable $e) {
            return array();
        }
    }
}

if (!function_exists('epc_sp_scorecards')) {
    /**
     * Per-supplier performance scorecard.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_sp_scorecards(PDO $db): array
    {
        $suppliers = epc_sp_safe_query($db, 'SELECT `id`, `name`, `contact_email`, `contact_phone` FROM `epc_erp_suppliers` WHERE `active` = 1 ORDER BY `name`');
        if (empty($suppliers)) {
            return array();
        }

        // Aggregate purchase orders per supplier.
        $poAgg = array();
        $poRows = epc_sp_safe_query($db, 'SELECT `supplier_id`, COUNT(*) c, SUM(`total_amount`) spend,
            SUM(CASE WHEN `status` = "received" THEN 1 ELSE 0 END) received,
            SUM(CASE WHEN `status` = "received" AND `received_at` > 0 AND `approved_at` > 0 THEN (`received_at` - `approved_at`) ELSE 0 END) lead_sum,
            SUM(CASE WHEN `status` = "received" AND `received_at` > 0 AND `approved_at` > 0 THEN 1 ELSE 0 END) lead_n,
            SUM(CASE WHEN `status` = "received" AND `received_at` > 0 AND `approved_at` > 0 AND (`received_at` - `approved_at`) <= 2592000 THEN 1 ELSE 0 END) ontime
            FROM `epc_erp_purchase_orders` GROUP BY `supplier_id`');
        foreach ($poRows as $r) {
            $poAgg[(int) $r['supplier_id']] = $r;
        }

        // RFQ response (quoted/accepted vs sent) per supplier.
        $rfqAgg = array();
        $rfqRows = epc_sp_safe_query($db, 'SELECT `supplier_id`, COUNT(*) c,
            SUM(CASE WHEN `status` IN ("quoted","accepted","rejected") THEN 1 ELSE 0 END) responded,
            SUM(CASE WHEN `status` = "accepted" THEN 1 ELSE 0 END) won
            FROM `epc_erp_rfq` GROUP BY `supplier_id`');
        foreach ($rfqRows as $r) {
            $rfqAgg[(int) $r['supplier_id']] = $r;
        }

        // Outstanding payable balance per supplier.
        $balAgg = array();
        $balRows = epc_sp_safe_query($db, 'SELECT `supplier_id`,
            SUM(CASE WHEN `is_credit` = 1 THEN `amount` ELSE -`amount` END) bal
            FROM `epc_erp_supplier_accounting` WHERE `active` = 1 GROUP BY `supplier_id`');
        foreach ($balRows as $r) {
            $balAgg[(int) $r['supplier_id']] = (float) $r['bal'];
        }

        $out = array();
        foreach ($suppliers as $s) {
            $id = (int) $s['id'];
            $po = $poAgg[$id] ?? array('c' => 0, 'spend' => 0, 'received' => 0, 'lead_sum' => 0, 'lead_n' => 0, 'ontime' => 0);
            $rfq = $rfqAgg[$id] ?? array('c' => 0, 'responded' => 0, 'won' => 0);

            $poCount = (int) $po['c'];
            $received = (int) $po['received'];
            $leadN = (int) $po['lead_n'];
            $ontime = (int) $po['ontime'];
            $spend = (float) $po['spend'];
            $avgLeadDays = $leadN > 0 ? ((int) $po['lead_sum'] / $leadN / 86400.0) : 0.0;
            $ontimePct = $leadN > 0 ? (100.0 * $ontime / $leadN) : null;

            $rfqCount = (int) $rfq['c'];
            $responded = (int) $rfq['responded'];
            $won = (int) $rfq['won'];
            $responsePct = $rfqCount > 0 ? (100.0 * $responded / $rfqCount) : null;
            $winPct = $responded > 0 ? (100.0 * $won / $responded) : null;

            // Composite score (0-100): delivery 40 + responsiveness 30 + activity 20 + win 10.
            $delivery = $ontimePct !== null ? ($ontimePct * 0.40) : 28.0; // neutral if no data
            $resp = $responsePct !== null ? ($responsePct * 0.30) : 21.0;
            $activity = min(20.0, $poCount * 2.0);
            $winScore = $winPct !== null ? ($winPct * 0.10) : 7.0;
            $score = round($delivery + $resp + $activity + $winScore, 1);
            if ($score > 100) {
                $score = 100.0;
            }
            $rating = $score >= 80 ? 'A' : ($score >= 65 ? 'B' : ($score >= 50 ? 'C' : 'D'));

            $out[] = array(
                'id' => $id,
                'name' => (string) $s['name'],
                'email' => (string) ($s['contact_email'] ?? ''),
                'phone' => (string) ($s['contact_phone'] ?? ''),
                'po_count' => $poCount,
                'received' => $received,
                'spend' => round($spend, 2),
                'avg_lead_days' => round($avgLeadDays, 1),
                'ontime_pct' => $ontimePct === null ? null : round($ontimePct, 1),
                'rfq_count' => $rfqCount,
                'response_pct' => $responsePct === null ? null : round($responsePct, 1),
                'win_pct' => $winPct === null ? null : round($winPct, 1),
                'balance' => round($balAgg[$id] ?? 0.0, 2),
                'score' => $score,
                'rating' => $rating,
            );
        }

        usort($out, static function ($a, $b) {
            return $b['score'] <=> $a['score'];
        });
        return $out;
    }
}

if (!function_exists('epc_sp_supplier_detail')) {
    /**
     * Detailed activity for one supplier.
     *
     * @return array<string,mixed>
     */
    function epc_sp_supplier_detail(PDO $db, int $supplierId): array
    {
        $cards = epc_sp_scorecards($db);
        $card = null;
        foreach ($cards as $c) {
            if ($c['id'] === $supplierId) {
                $card = $c;
                break;
            }
        }
        $pos = epc_sp_safe_query($db, 'SELECT `po_no`, `title`, `total_amount`, `status`, `approved_at`, `received_at`, `time_created`
            FROM `epc_erp_purchase_orders` WHERE `supplier_id` = ? ORDER BY `id` DESC LIMIT 25', array($supplierId));
        $rfqs = epc_sp_safe_query($db, 'SELECT `rfq_no`, `title`, `amount_est`, `status`, `due_date`, `time_created`
            FROM `epc_erp_rfq` WHERE `supplier_id` = ? ORDER BY `id` DESC LIMIT 25', array($supplierId));
        $bills = epc_sp_safe_query($db, 'SELECT `id`, `invoice_number`, `total_amount`, `purchase_date`, `status` FROM `epc_erp_purchases`
            WHERE `supplier_id` = ? AND `active` = 1 ORDER BY `purchase_date` DESC, `id` DESC LIMIT 25', array($supplierId));
        return array('card' => $card, 'pos' => $pos, 'rfqs' => $rfqs, 'bills' => $bills);
    }
}
