<?php
/**
 * Executive dashboard — cross-module KPI cockpit.
 *
 * Reuses the BOS intelligence KPI engine and the operational dashboard to
 * present a single executive view: headline KPIs with health, a revenue/profit
 * trend, working-capital snapshot, top suppliers by spend and planning alerts.
 */

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_intelligence.php';

if (!function_exists('epc_exec_trend')) {
    /**
     * Monthly revenue/profit trend for the last N months (oldest first).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_exec_trend(PDO $db, int $months = 6): array
    {
        $out = array();
        for ($i = $months - 1; $i >= 0; $i--) {
            $from = strtotime(date('Y-m-01 00:00:00', strtotime("-{$i} months")));
            $to = strtotime(date('Y-m-t 23:59:59', strtotime("-{$i} months")));
            try {
                $d = epc_erp_dashboard($db, $from, $to);
            } catch (Throwable $e) {
                $d = array();
            }
            $out[] = array(
                'label' => date('M y', $from),
                'revenue' => (float) ($d['revenue_ex_vat'] ?? 0),
                'profit' => (float) ($d['profit_ex_vat'] ?? 0),
            );
        }
        return $out;
    }
}

if (!function_exists('epc_exec_top_suppliers')) {
    /**
     * Top suppliers by spend (reuses the supplier scorecards).
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_exec_top_suppliers(PDO $db, int $limit = 5): array
    {
        $file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_supplier_portal.php';
        if (!function_exists('epc_sp_scorecards') && is_file($file)) {
            require_once $file;
        }
        if (!function_exists('epc_sp_scorecards')) {
            return array();
        }
        $cards = epc_sp_scorecards($db);
        usort($cards, static function ($a, $b) {
            return $b['spend'] <=> $a['spend'];
        });
        return array_slice($cards, 0, $limit);
    }
}

if (!function_exists('epc_exec_planning_alerts')) {
    /**
     * Order-planning alert counts by severity.
     *
     * @return array<string,int>
     */
    function epc_exec_planning_alerts(PDO $db): array
    {
        $file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_order_planning.php';
        if (!function_exists('epc_opl_exceptions') && is_file($file)) {
            require_once $file;
        }
        $counts = array('danger' => 0, 'warning' => 0, 'info' => 0, 'default' => 0, 'total' => 0);
        if (!function_exists('epc_opl_exceptions')) {
            return $counts;
        }
        try {
            $rows = epc_opl_exceptions($db, 0);
        } catch (Throwable $e) {
            return $counts;
        }
        foreach ($rows as $r) {
            $sev = (string) ($r['sev'] ?? 'default');
            if (!isset($counts[$sev])) {
                $counts[$sev] = 0;
            }
            $counts[$sev]++;
            $counts['total']++;
        }
        return $counts;
    }
}

if (!function_exists('epc_exec_working_capital')) {
    /**
     * Working capital snapshot: AR balance, AP balance, inventory value, cash.
     *
     * @return array{ar:float,ap:float,inventory:float,cash:float,net_wc:float,current_ratio:float}
     */
    function epc_exec_working_capital(PDO $db): array
    {
        $ar = 0.0;
        $ap = 0.0;
        $inv = 0.0;
        $cash = 0.0;

        try {
            $st = $db->query("SELECT COALESCE(SUM(`total_amount`), 0) FROM `epc_erp_sales_orders` WHERE `status` IN ('confirmed','partial') AND `active` = 1");
            $ar = (float) $st->fetchColumn();
        } catch (Throwable $e) {}

        try {
            $st = $db->query("SELECT COALESCE(SUM(`total_amount`), 0) FROM `epc_erp_purchases` WHERE `status` IN ('confirmed','partial') AND `active` = 1");
            $ap = (float) $st->fetchColumn();
        } catch (Throwable $e) {}

        try {
            $st = $db->query("SELECT COALESCE(SUM(`qty_on_hand` * `avg_cost`), 0) FROM `epc_erp_inv_items` WHERE `active` = 1");
            $inv = (float) $st->fetchColumn();
        } catch (Throwable $e) {}

        try {
            $st = $db->query("SELECT COALESCE(SUM(`opening_balance`), 0) FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1");
            $cash = (float) $st->fetchColumn();
            $st2 = $db->query("SELECT COALESCE(SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE -`amount` END), 0) FROM `epc_erp_cash_bank_entries` WHERE `active` = 1");
            $cash += (float) $st2->fetchColumn();
        } catch (Throwable $e) {}

        $currentAssets = $ar + $inv + $cash;
        $currentLiabilities = $ap > 0 ? $ap : 1;
        return array(
            'ar' => $ar,
            'ap' => $ap,
            'inventory' => $inv,
            'cash' => $cash,
            'net_wc' => $currentAssets - $ap,
            'current_ratio' => round($currentAssets / $currentLiabilities, 2),
        );
    }
}

if (!function_exists('epc_exec_ar_aging')) {
    /**
     * AR aging buckets: current, 30, 60, 90, 90+ days.
     *
     * @return array{current:float,d30:float,d60:float,d90:float,over90:float}
     */
    function epc_exec_ar_aging(PDO $db): array
    {
        $buckets = array('current' => 0.0, 'd30' => 0.0, 'd60' => 0.0, 'd90' => 0.0, 'over90' => 0.0);
        try {
            $now = time();
            $st = $db->query("SELECT `total_amount`, `order_date` FROM `epc_erp_sales_orders` WHERE `status` IN ('confirmed','partial') AND `active` = 1");
            while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
                $age = ($now - (int) $row['order_date']) / 86400;
                $amt = (float) $row['total_amount'];
                if ($age <= 30) {
                    $buckets['current'] += $amt;
                } elseif ($age <= 60) {
                    $buckets['d30'] += $amt;
                } elseif ($age <= 90) {
                    $buckets['d60'] += $amt;
                } elseif ($age <= 120) {
                    $buckets['d90'] += $amt;
                } else {
                    $buckets['over90'] += $amt;
                }
            }
        } catch (Throwable $e) {}
        return $buckets;
    }
}

if (!function_exists('epc_exec_cash_flow_forecast')) {
    /**
     * Simplified 3-month cash flow forecast based on historical trends.
     *
     * @return array<int,array{month:string,inflow:float,outflow:float,net:float}>
     */
    function epc_exec_cash_flow_forecast(PDO $db): array
    {
        $months = array();
        $avgInflow = 0.0;
        $avgOutflow = 0.0;
        $histMonths = 3;

        for ($i = $histMonths; $i >= 1; $i--) {
            $from = strtotime(date('Y-m-01 00:00:00', strtotime("-{$i} months")));
            $to = strtotime(date('Y-m-t 23:59:59', strtotime("-{$i} months")));
            $inflow = 0.0;
            $outflow = 0.0;
            try {
                $st = $db->prepare("SELECT COALESCE(SUM(`amount`), 0) FROM `epc_erp_cash_bank_entries` WHERE `direction` = 1 AND `active` = 1 AND `time` BETWEEN ? AND ?");
                $st->execute(array($from, $to));
                $inflow = (float) $st->fetchColumn();
            } catch (Throwable $e) {}
            try {
                $st = $db->prepare("SELECT COALESCE(SUM(`amount`), 0) FROM `epc_erp_cash_bank_entries` WHERE `direction` = 0 AND `active` = 1 AND `time` BETWEEN ? AND ?");
                $st->execute(array($from, $to));
                $outflow = (float) $st->fetchColumn();
            } catch (Throwable $e) {}
            $avgInflow += $inflow;
            $avgOutflow += $outflow;
        }

        $avgInflow = $avgInflow / max($histMonths, 1);
        $avgOutflow = $avgOutflow / max($histMonths, 1);

        for ($i = 1; $i <= 3; $i++) {
            $months[] = array(
                'month' => date('M Y', strtotime("+{$i} months")),
                'inflow' => round($avgInflow, 2),
                'outflow' => round($avgOutflow, 2),
                'net' => round($avgInflow - $avgOutflow, 2),
            );
        }
        return $months;
    }
}
