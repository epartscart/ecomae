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
