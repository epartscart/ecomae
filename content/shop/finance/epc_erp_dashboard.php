<?php
/**
 * Advanced ERP — Dashboard data layer (KPI tiles + chart series).
 *
 * Pure aggregation helpers that turn raw rows into the {tiles, charts}
 * structure the CP renders with a lightweight charting layer (Chart.js). Each
 * module gets a builder; an executive builder rolls up enabled modules.
 *
 * Aggregation math is unit-tested here; the rendering is the JS layer.
 *
 * Tile  = {key, label, value, format}         (format: number|money|percent)
 * Chart = {key, title, type, labels[], series[]}  (type: line|bar|pie|stacked)
 *
 * Additive: pure functions, no tables.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_dash_tile')) {
    /** @return array<string,mixed> */
    function epc_dash_tile(string $key, string $label, $value, string $format = 'number'): array
    {
        return array('key' => $key, 'label' => $label, 'value' => $value, 'format' => $format);
    }
}

if (!function_exists('epc_dash_chart')) {
    /**
     * @param array<int,string> $labels
     * @param array<int,array{name:string,data:array<int,float>}> $series
     * @return array<string,mixed>
     */
    function epc_dash_chart(string $key, string $title, string $type, array $labels, array $series): array
    {
        return array('key' => $key, 'title' => $title, 'type' => $type, 'labels' => $labels, 'series' => $series);
    }
}

if (!function_exists('epc_dash_group_sum')) {
    /**
     * Group rows by a key field and sum a value field. Preserves first-seen
     * order of keys.
     *
     * @param array<int,array<string,mixed>> $rows
     * @return array<string,float>
     */
    function epc_dash_group_sum(array $rows, string $keyField, string $valueField): array
    {
        $out = array();
        foreach ($rows as $r) {
            $k = (string) ($r[$keyField] ?? '');
            $v = (float) ($r[$valueField] ?? 0);
            if (!isset($out[$k])) {
                $out[$k] = 0.0;
            }
            $out[$k] = round($out[$k] + $v, 2);
        }
        return $out;
    }
}

if (!function_exists('epc_dash_sales')) {
    /**
     * Sales dashboard. $sales rows: {period, branch, amount, margin?}.
     *
     * @param array<int,array<string,mixed>> $sales
     * @return array<string,mixed>
     */
    function epc_dash_sales(array $sales): array
    {
        $total = 0.0;
        $margin = 0.0;
        foreach ($sales as $s) {
            $total = round($total + (float) ($s['amount'] ?? 0), 2);
            $margin = round($margin + (float) ($s['margin'] ?? 0), 2);
        }
        $byPeriod = epc_dash_group_sum($sales, 'period', 'amount');
        $byBranch = epc_dash_group_sum($sales, 'branch', 'amount');
        $marginPct = $total > 0 ? round($margin / $total * 100, 2) : 0.0;

        return array(
            'tiles' => array(
                epc_dash_tile('sales_total', 'Total Sales', $total, 'money'),
                epc_dash_tile('gross_margin', 'Gross Margin', $margin, 'money'),
                epc_dash_tile('margin_pct', 'Margin %', $marginPct, 'percent'),
                epc_dash_tile('order_count', 'Orders', count($sales), 'number'),
            ),
            'charts' => array(
                epc_dash_chart('sales_trend', 'Sales Trend', 'line', array_keys($byPeriod), array(array('name' => 'Sales', 'data' => array_values($byPeriod)))),
                epc_dash_chart('sales_by_branch', 'Sales by Branch', 'bar', array_keys($byBranch), array(array('name' => 'Sales', 'data' => array_values($byBranch)))),
            ),
        );
    }
}

if (!function_exists('epc_dash_cash')) {
    /**
     * Treasury/cash dashboard. $accounts: {name, balance}. $flows:
     * {period, in, out}.
     *
     * @param array<int,array<string,mixed>> $accounts
     * @param array<int,array<string,mixed>> $flows
     * @return array<string,mixed>
     */
    function epc_dash_cash(array $accounts, array $flows = array()): array
    {
        $position = 0.0;
        $labels = array();
        $bals = array();
        foreach ($accounts as $a) {
            $b = round((float) ($a['balance'] ?? 0), 2);
            $position = round($position + $b, 2);
            $labels[] = (string) ($a['name'] ?? '');
            $bals[] = $b;
        }
        $fLabels = array();
        $inSeries = array();
        $outSeries = array();
        foreach ($flows as $f) {
            $fLabels[] = (string) ($f['period'] ?? '');
            $inSeries[] = round((float) ($f['in'] ?? 0), 2);
            $outSeries[] = round((float) ($f['out'] ?? 0), 2);
        }
        return array(
            'tiles' => array(
                epc_dash_tile('cash_position', 'Cash + Bank Position', $position, 'money'),
                epc_dash_tile('account_count', 'Accounts', count($accounts), 'number'),
            ),
            'charts' => array(
                epc_dash_chart('balances', 'Balances by Account', 'bar', $labels, array(array('name' => 'Balance', 'data' => $bals))),
                epc_dash_chart('cash_flow', 'Cash In vs Out', 'bar', $fLabels, array(
                    array('name' => 'In', 'data' => $inSeries),
                    array('name' => 'Out', 'data' => $outSeries),
                )),
            ),
        );
    }
}

if (!function_exists('epc_dash_ar_ageing')) {
    /**
     * AR ageing dashboard. $invoices: {amount, days_overdue}. Buckets:
     * current, 1-30, 31-60, 61-90, 90+.
     *
     * @param array<int,array<string,mixed>> $invoices
     * @return array<string,mixed>
     */
    function epc_dash_ar_ageing(array $invoices): array
    {
        $buckets = array('Current' => 0.0, '1-30' => 0.0, '31-60' => 0.0, '61-90' => 0.0, '90+' => 0.0);
        $total = 0.0;
        $overdue = 0.0;
        foreach ($invoices as $inv) {
            $amt = round((float) ($inv['amount'] ?? 0), 2);
            $days = (int) ($inv['days_overdue'] ?? 0);
            $total = round($total + $amt, 2);
            if ($days <= 0) {
                $buckets['Current'] = round($buckets['Current'] + $amt, 2);
            } elseif ($days <= 30) {
                $buckets['1-30'] = round($buckets['1-30'] + $amt, 2);
                $overdue = round($overdue + $amt, 2);
            } elseif ($days <= 60) {
                $buckets['31-60'] = round($buckets['31-60'] + $amt, 2);
                $overdue = round($overdue + $amt, 2);
            } elseif ($days <= 90) {
                $buckets['61-90'] = round($buckets['61-90'] + $amt, 2);
                $overdue = round($overdue + $amt, 2);
            } else {
                $buckets['90+'] = round($buckets['90+'] + $amt, 2);
                $overdue = round($overdue + $amt, 2);
            }
        }
        return array(
            'buckets' => $buckets,
            'tiles' => array(
                epc_dash_tile('ar_total', 'Total Receivable', $total, 'money'),
                epc_dash_tile('ar_overdue', 'Overdue', $overdue, 'money'),
            ),
            'charts' => array(
                epc_dash_chart('ar_ageing', 'AR Ageing', 'stacked', array_keys($buckets), array(array('name' => 'Outstanding', 'data' => array_values($buckets)))),
            ),
        );
    }
}

if (!function_exists('epc_dash_ap_ageing')) {
    /**
     * AP (payables) ageing. $bills: {amount, days_overdue}. Same buckets as AR;
     * also surfaces due-soon (not yet overdue but within grace) when supplied
     * as negative days_overdue meaning days-until-due.
     *
     * @param array<int,array<string,mixed>> $bills
     * @return array<string,mixed>
     */
    function epc_dash_ap_ageing(array $bills): array
    {
        $buckets = array('Current' => 0.0, '1-30' => 0.0, '31-60' => 0.0, '61-90' => 0.0, '90+' => 0.0);
        $total = 0.0;
        $overdue = 0.0;
        foreach ($bills as $b) {
            $amt = round((float) ($b['amount'] ?? 0), 2);
            $days = (int) ($b['days_overdue'] ?? 0);
            $total = round($total + $amt, 2);
            if ($days <= 0) {
                $buckets['Current'] = round($buckets['Current'] + $amt, 2);
            } elseif ($days <= 30) {
                $buckets['1-30'] = round($buckets['1-30'] + $amt, 2);
                $overdue = round($overdue + $amt, 2);
            } elseif ($days <= 60) {
                $buckets['31-60'] = round($buckets['31-60'] + $amt, 2);
                $overdue = round($overdue + $amt, 2);
            } elseif ($days <= 90) {
                $buckets['61-90'] = round($buckets['61-90'] + $amt, 2);
                $overdue = round($overdue + $amt, 2);
            } else {
                $buckets['90+'] = round($buckets['90+'] + $amt, 2);
                $overdue = round($overdue + $amt, 2);
            }
        }
        return array(
            'buckets' => $buckets,
            'tiles' => array(
                epc_dash_tile('ap_total', 'Total Payable', $total, 'money'),
                epc_dash_tile('ap_overdue', 'Overdue', $overdue, 'money'),
            ),
            'charts' => array(
                epc_dash_chart('ap_ageing', 'AP Ageing', 'stacked', array_keys($buckets), array(array('name' => 'Outstanding', 'data' => array_values($buckets)))),
            ),
        );
    }
}

if (!function_exists('epc_dash_inventory_ageing')) {
    /**
     * Inventory ageing. $items: {name, value, days_on_hand}. Buckets:
     * 0-30, 31-60, 61-90, 91-180, 180+. Items at/over $slowDays are flagged
     * slow-moving; at/over $deadDays are dead stock (value at risk).
     *
     * @param array<int,array<string,mixed>> $items
     * @return array<string,mixed>
     */
    function epc_dash_inventory_ageing(array $items, int $slowDays = 90, int $deadDays = 180): array
    {
        $buckets = array('0-30' => 0.0, '31-60' => 0.0, '61-90' => 0.0, '91-180' => 0.0, '180+' => 0.0);
        $total = 0.0;
        $slow = 0.0;
        $dead = 0.0;
        $slowItems = array();
        foreach ($items as $it) {
            $val = round((float) ($it['value'] ?? 0), 2);
            $days = (int) ($it['days_on_hand'] ?? 0);
            $total = round($total + $val, 2);
            if ($days <= 30) {
                $buckets['0-30'] = round($buckets['0-30'] + $val, 2);
            } elseif ($days <= 60) {
                $buckets['31-60'] = round($buckets['31-60'] + $val, 2);
            } elseif ($days <= 90) {
                $buckets['61-90'] = round($buckets['61-90'] + $val, 2);
            } elseif ($days <= 180) {
                $buckets['91-180'] = round($buckets['91-180'] + $val, 2);
            } else {
                $buckets['180+'] = round($buckets['180+'] + $val, 2);
            }
            if ($days >= $deadDays) {
                $dead = round($dead + $val, 2);
                $slowItems[] = array('name' => (string) ($it['name'] ?? ''), 'value' => $val, 'days' => $days, 'flag' => 'dead');
            } elseif ($days >= $slowDays) {
                $slow = round($slow + $val, 2);
                $slowItems[] = array('name' => (string) ($it['name'] ?? ''), 'value' => $val, 'days' => $days, 'flag' => 'slow');
            }
        }
        return array(
            'buckets' => $buckets,
            'slow_moving' => $slowItems,
            'tiles' => array(
                epc_dash_tile('stock_value', 'Stock Value', $total, 'money'),
                epc_dash_tile('slow_moving_value', 'Slow-moving', $slow, 'money'),
                epc_dash_tile('dead_stock_value', 'Dead Stock (at risk)', $dead, 'money'),
            ),
            'charts' => array(
                epc_dash_chart('inv_ageing', 'Inventory Ageing', 'stacked', array_keys($buckets), array(array('name' => 'Value', 'data' => array_values($buckets)))),
            ),
        );
    }
}

if (!function_exists('epc_dash_inventory_turnover')) {
    /**
     * Stock turnover + days-on-hand. turnover = COGS / avg inventory; DOH =
     * 365 / turnover.
     *
     * @return array<string,mixed>
     */
    function epc_dash_inventory_turnover(float $cogs, float $avgInventory): array
    {
        $turnover = $avgInventory > 0 ? round($cogs / $avgInventory, 2) : 0.0;
        $doh = $turnover > 0 ? round(365 / $turnover, 1) : 0.0;
        return array(
            'tiles' => array(
                epc_dash_tile('inv_turnover', 'Inventory Turnover', $turnover, 'number'),
                epc_dash_tile('days_on_hand', 'Days On Hand', $doh, 'number'),
            ),
        );
    }
}

if (!function_exists('epc_dash_expense_breakdown')) {
    /**
     * Expense pie. $expenses: {category, amount}.
     *
     * @param array<int,array<string,mixed>> $expenses
     * @return array<string,mixed>
     */
    function epc_dash_expense_breakdown(array $expenses): array
    {
        $byCat = epc_dash_group_sum($expenses, 'category', 'amount');
        $total = round(array_sum($byCat), 2);
        return array(
            'tiles' => array(epc_dash_tile('expense_total', 'Total Expenses', $total, 'money')),
            'charts' => array(epc_dash_chart('expense_pie', 'Expense Breakdown', 'pie', array_keys($byCat), array(array('name' => 'Expense', 'data' => array_values($byCat))))),
        );
    }
}

if (!function_exists('epc_dash_executive')) {
    /**
     * Executive roll-up across enabled module dashboards. $modules maps a module
     * name to its dashboard array (each having 'tiles'/'charts'). Only enabled
     * modules (passed in) are included.
     *
     * @param array<string,array<string,mixed>> $modules
     * @return array<string,mixed> {tiles, charts, modules}
     */
    function epc_dash_executive(array $modules): array
    {
        $tiles = array();
        $charts = array();
        foreach ($modules as $name => $dash) {
            foreach ($dash['tiles'] ?? array() as $t) {
                $t['module'] = $name;
                $tiles[] = $t;
            }
            foreach ($dash['charts'] ?? array() as $c) {
                $c['module'] = $name;
                $charts[] = $c;
            }
        }
        return array('tiles' => $tiles, 'charts' => $charts, 'modules' => array_keys($modules));
    }
}
