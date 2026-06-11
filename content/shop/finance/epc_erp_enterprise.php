<?php
/**
 * Enterprise-parity gap modules (SAP / Oracle / Dynamics 365 equivalents that
 * weren't yet explicit):
 *
 *   - MRP / demand planning  (SAP MRP)            : net requirements + planned orders
 *   - Fixed-asset depreciation (SAP AA)           : straight-line & reducing-balance
 *   - Available-to-Promise (SAP ATP / GATP)       : promise check on sales orders
 *   - Intercompany & consolidation eliminations   : multi-company postings
 *
 * Pure computation (no DB) so it is exact and unit-testable; the CP wires these
 * to live item/asset/company data. Additive + entitlement-gated.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

/* ------------------------------------------------------------------ MRP --- */

if (!function_exists('epc_mrp_net_requirement')) {
    /**
     * Net requirement = demand - (on-hand - safety stock) - on-order.
     * Never negative.
     */
    function epc_mrp_net_requirement(float $demand, float $onHand, float $onOrder = 0.0, float $safetyStock = 0.0): float
    {
        $available = ($onHand - $safetyStock) + $onOrder;
        $net = $demand - $available;
        return $net > 0 ? round($net, 4) : 0.0;
    }
}

if (!function_exists('epc_mrp_run')) {
    /**
     * MRP run over a set of items. Each item:
     *   {sku, on_hand, demand, on_order, safety_stock, reorder_qty,
     *    lead_time_days, source('buy'|'make')}
     * Returns planned orders for items that fall short, rounded up to the
     * reorder/lot multiple.
     *
     * @param array<int,array<string,mixed>> $items
     * @return array<int,array<string,mixed>>
     */
    function epc_mrp_run(array $items): array
    {
        $orders = array();
        foreach ($items as $it) {
            $net = epc_mrp_net_requirement(
                (float) ($it['demand'] ?? 0),
                (float) ($it['on_hand'] ?? 0),
                (float) ($it['on_order'] ?? 0),
                (float) ($it['safety_stock'] ?? 0)
            );
            if ($net <= 0) {
                continue;
            }
            $lot = (float) ($it['reorder_qty'] ?? 0);
            $qty = $lot > 0 ? ceil($net / $lot) * $lot : $net;
            $orders[] = array(
                'sku' => (string) ($it['sku'] ?? ''),
                'net_requirement' => $net,
                'planned_qty' => $qty,
                'order_type' => (($it['source'] ?? 'buy') === 'make') ? 'planned_production_order' : 'planned_purchase_order',
                'lead_time_days' => (int) ($it['lead_time_days'] ?? 0),
            );
        }
        return $orders;
    }
}

/* ------------------------------------------------------- Depreciation (AA) - */

if (!function_exists('epc_depreciation_schedule')) {
    /**
     * Monthly depreciation schedule.
     *
     * @param float  $cost        acquisition cost
     * @param float  $salvage     residual value
     * @param int    $lifeMonths  useful life in months
     * @param string $method      'straight_line' | 'reducing_balance'
     * @param float  $rate        annual % for reducing-balance (e.g. 20.0)
     * @return array<int,array{period:int,depreciation:float,accumulated:float,book_value:float}>
     */
    function epc_depreciation_schedule(float $cost, float $salvage, int $lifeMonths, string $method = 'straight_line', float $rate = 0.0): array
    {
        $schedule = array();
        if ($lifeMonths <= 0 || $cost <= 0) {
            return $schedule;
        }
        $book = $cost;
        $accumulated = 0.0;
        if ($method === 'reducing_balance') {
            $monthlyRate = ($rate > 0 ? $rate : (100.0 / ($lifeMonths / 12.0))) / 100.0 / 12.0;
            for ($p = 1; $p <= $lifeMonths; $p++) {
                $dep = round($book * $monthlyRate, 2);
                if ($book - $dep < $salvage) {
                    $dep = round($book - $salvage, 2);
                }
                if ($dep < 0) {
                    $dep = 0.0;
                }
                $book = round($book - $dep, 2);
                $accumulated = round($accumulated + $dep, 2);
                $schedule[] = array('period' => $p, 'depreciation' => $dep, 'accumulated' => $accumulated, 'book_value' => $book);
            }
        } else {
            $monthly = round(($cost - $salvage) / $lifeMonths, 2);
            for ($p = 1; $p <= $lifeMonths; $p++) {
                $dep = $monthly;
                if ($p === $lifeMonths) {
                    // final period absorbs rounding so book == salvage
                    $dep = round($book - $salvage, 2);
                }
                $book = round($book - $dep, 2);
                $accumulated = round($accumulated + $dep, 2);
                $schedule[] = array('period' => $p, 'depreciation' => $dep, 'accumulated' => $accumulated, 'book_value' => $book);
            }
        }
        return $schedule;
    }
}

if (!function_exists('epc_asset_disposal')) {
    /**
     * Gain/loss on disposal: proceeds - net book value.
     *
     * @return array{book_value:float,proceeds:float,result:float,type:string}
     */
    function epc_asset_disposal(float $bookValue, float $proceeds): array
    {
        $result = round($proceeds - $bookValue, 2);
        return array(
            'book_value' => round($bookValue, 2),
            'proceeds' => round($proceeds, 2),
            'result' => $result,
            'type' => $result >= 0 ? 'gain' : 'loss',
        );
    }
}

/* -------------------------------------------------- Available-to-Promise --- */

if (!function_exists('epc_atp')) {
    /**
     * Available-to-Promise = on_hand - reserved + incoming.
     *
     * @return array{available:float,requested:float,can_promise:bool,shortfall:float}
     */
    function epc_atp(float $onHand, float $reserved, float $incoming, float $requested): array
    {
        $available = round(($onHand - $reserved) + $incoming, 4);
        $shortfall = $requested > $available ? round($requested - $available, 4) : 0.0;
        return array(
            'available' => $available,
            'requested' => round($requested, 4),
            'can_promise' => $requested <= $available,
            'shortfall' => $shortfall,
        );
    }
}

/* --------------------------------------------- Intercompany / elimination - */

if (!function_exists('epc_intercompany_entry')) {
    /**
     * Build a balanced intercompany journal pair (one in each company) and tag
     * it so consolidation can eliminate it.
     *
     * @return array{ref:string,lines:array<int,array<string,mixed>>,balanced:bool}
     */
    function epc_intercompany_entry(string $fromCo, string $toCo, float $amount, string $ref = ''): array
    {
        $ref = $ref !== '' ? $ref : ('IC-' . $fromCo . '-' . $toCo);
        $amount = round($amount, 2);
        $lines = array(
            array('company' => $fromCo, 'account' => 'IC Receivable (' . $toCo . ')', 'debit' => $amount, 'credit' => 0.0, 'ic' => true, 'ref' => $ref),
            array('company' => $fromCo, 'account' => 'Revenue/Transfer', 'debit' => 0.0, 'credit' => $amount, 'ic' => false, 'ref' => $ref),
            array('company' => $toCo, 'account' => 'Expense/Transfer', 'debit' => $amount, 'credit' => 0.0, 'ic' => false, 'ref' => $ref),
            array('company' => $toCo, 'account' => 'IC Payable (' . $fromCo . ')', 'debit' => 0.0, 'credit' => $amount, 'ic' => true, 'ref' => $ref),
        );
        $dr = 0.0;
        $cr = 0.0;
        foreach ($lines as $l) {
            $dr += $l['debit'];
            $cr += $l['credit'];
        }
        return array('ref' => $ref, 'lines' => $lines, 'balanced' => abs($dr - $cr) < 0.001);
    }
}

if (!function_exists('epc_consolidation_eliminate')) {
    /**
     * Consolidate company ledgers and remove intercompany (ic=true) lines.
     *
     * @param array<int,array<string,mixed>> $lines flat list of journal lines
     * @return array{gross_debit:float,gross_credit:float,eliminated:float,consolidated_debit:float,consolidated_credit:float}
     */
    function epc_consolidation_eliminate(array $lines): array
    {
        $grossDr = 0.0;
        $grossCr = 0.0;
        $elim = 0.0;
        $conDr = 0.0;
        $conCr = 0.0;
        foreach ($lines as $l) {
            $d = (float) ($l['debit'] ?? 0);
            $c = (float) ($l['credit'] ?? 0);
            $grossDr += $d;
            $grossCr += $c;
            if (!empty($l['ic'])) {
                $elim += $d + $c;
                continue;
            }
            $conDr += $d;
            $conCr += $c;
        }
        return array(
            'gross_debit' => round($grossDr, 2),
            'gross_credit' => round($grossCr, 2),
            'eliminated' => round($elim, 2),
            'consolidated_debit' => round($conDr, 2),
            'consolidated_credit' => round($conCr, 2),
        );
    }
}
