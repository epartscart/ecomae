<?php
/**
 * CLI tests for the dashboard data layer (KPI tiles + chart series).
 *
 *   php tests/erp_advanced/run_dashboard_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_dashboard.php';

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

section('Group-sum helper');
$g = epc_dash_group_sum(array(
    array('k' => 'A', 'v' => 10),
    array('k' => 'B', 'v' => 5),
    array('k' => 'A', 'v' => 7),
), 'k', 'v');
check('grouped A=17, B=5', abs($g['A'] - 17.0) < 0.01 && abs($g['B'] - 5.0) < 0.01);
check('first-seen order preserved (A before B)', array_keys($g) === array('A', 'B'));

section('Sales dashboard');
$sales = array(
    array('period' => '2026-01', 'branch' => 'Dubai', 'amount' => 1000, 'margin' => 300),
    array('period' => '2026-01', 'branch' => 'Abu Dhabi', 'amount' => 500, 'margin' => 100),
    array('period' => '2026-02', 'branch' => 'Dubai', 'amount' => 1500, 'margin' => 600),
);
$ds = epc_dash_sales($sales);
$tileMap = array();
foreach ($ds['tiles'] as $t) {
    $tileMap[$t['key']] = $t['value'];
}
check('total sales tile = 3000', abs($tileMap['sales_total'] - 3000.0) < 0.01);
check('gross margin tile = 1000', abs($tileMap['gross_margin'] - 1000.0) < 0.01);
check('margin % tile = 33.33', abs($tileMap['margin_pct'] - 33.33) < 0.01);
check('orders tile = 3', $tileMap['order_count'] === 3);
$trend = null;
$byBranch = null;
foreach ($ds['charts'] as $c) {
    if ($c['key'] === 'sales_trend') {
        $trend = $c;
    }
    if ($c['key'] === 'sales_by_branch') {
        $byBranch = $c;
    }
}
check('sales trend is a line chart over 2 periods', $trend['type'] === 'line' && $trend['labels'] === array('2026-01', '2026-02'));
check('trend values 1500 then 1500', $trend['series'][0]['data'] === array(1500.0, 1500.0));
check('sales by branch is a bar chart', $byBranch['type'] === 'bar' && in_array('Dubai', $byBranch['labels'], true));

section('Cash dashboard');
$dc = epc_dash_cash(
    array(array('name' => 'Bank', 'balance' => 100000), array('name' => 'Cash', 'balance' => 5000)),
    array(array('period' => 'W1', 'in' => 20000, 'out' => 8000), array('period' => 'W2', 'in' => 15000, 'out' => 12000))
);
$cashTiles = array();
foreach ($dc['tiles'] as $t) {
    $cashTiles[$t['key']] = $t['value'];
}
check('cash position = 105000', abs($cashTiles['cash_position'] - 105000.0) < 0.01);
$flow = null;
foreach ($dc['charts'] as $c) {
    if ($c['key'] === 'cash_flow') {
        $flow = $c;
    }
}
check('cash flow has in + out series', count($flow['series']) === 2 && $flow['series'][0]['data'] === array(20000.0, 15000.0));

section('AR ageing dashboard');
$age = epc_dash_ar_ageing(array(
    array('amount' => 1000, 'days_overdue' => 0),
    array('amount' => 500, 'days_overdue' => 15),
    array('amount' => 300, 'days_overdue' => 45),
    array('amount' => 200, 'days_overdue' => 120),
));
check('current bucket = 1000', abs($age['buckets']['Current'] - 1000.0) < 0.01);
check('1-30 bucket = 500', abs($age['buckets']['1-30'] - 500.0) < 0.01);
check('31-60 bucket = 300', abs($age['buckets']['31-60'] - 300.0) < 0.01);
check('90+ bucket = 200', abs($age['buckets']['90+'] - 200.0) < 0.01);
$arTiles = array();
foreach ($age['tiles'] as $t) {
    $arTiles[$t['key']] = $t['value'];
}
check('AR total = 2000', abs($arTiles['ar_total'] - 2000.0) < 0.01);
check('AR overdue = 1000 (excludes current)', abs($arTiles['ar_overdue'] - 1000.0) < 0.01);

section('AP ageing dashboard');
$ap = epc_dash_ap_ageing(array(
    array('amount' => 2000, 'days_overdue' => 0),
    array('amount' => 800, 'days_overdue' => 20),
    array('amount' => 400, 'days_overdue' => 75),
    array('amount' => 100, 'days_overdue' => 200),
));
check('AP current = 2000', abs($ap['buckets']['Current'] - 2000.0) < 0.01);
check('AP 61-90 = 400', abs($ap['buckets']['61-90'] - 400.0) < 0.01);
check('AP 90+ = 100', abs($ap['buckets']['90+'] - 100.0) < 0.01);
$apTiles = array();
foreach ($ap['tiles'] as $t) {
    $apTiles[$t['key']] = $t['value'];
}
check('AP total = 3300', abs($apTiles['ap_total'] - 3300.0) < 0.01);
check('AP overdue = 1300 (excludes current)', abs($apTiles['ap_overdue'] - 1300.0) < 0.01);

section('Inventory ageing + slow/dead stock');
$inv = epc_dash_inventory_ageing(array(
    array('name' => 'Fast item', 'value' => 5000, 'days_on_hand' => 10),
    array('name' => 'OK item', 'value' => 3000, 'days_on_hand' => 70),
    array('name' => 'Slow item', 'value' => 2000, 'days_on_hand' => 120),
    array('name' => 'Dead item', 'value' => 1000, 'days_on_hand' => 250),
));
check('inv 0-30 bucket = 5000', abs($inv['buckets']['0-30'] - 5000.0) < 0.01);
check('inv 61-90 bucket = 3000', abs($inv['buckets']['61-90'] - 3000.0) < 0.01);
check('inv 91-180 bucket = 2000', abs($inv['buckets']['91-180'] - 2000.0) < 0.01);
check('inv 180+ bucket = 1000', abs($inv['buckets']['180+'] - 1000.0) < 0.01);
$invTiles = array();
foreach ($inv['tiles'] as $t) {
    $invTiles[$t['key']] = $t['value'];
}
check('stock value = 11000', abs($invTiles['stock_value'] - 11000.0) < 0.01);
check('slow-moving value = 2000 (>=90 <180)', abs($invTiles['slow_moving_value'] - 2000.0) < 0.01);
check('dead stock value = 1000 (>=180)', abs($invTiles['dead_stock_value'] - 1000.0) < 0.01);
check('2 items flagged slow/dead', count($inv['slow_moving']) === 2);

section('Inventory turnover + days on hand');
$turn = epc_dash_inventory_turnover(120000, 30000);
$turnTiles = array();
foreach ($turn['tiles'] as $t) {
    $turnTiles[$t['key']] = $t['value'];
}
check('turnover = 4', abs($turnTiles['inv_turnover'] - 4.0) < 0.01);
check('days on hand = 91.25', abs($turnTiles['days_on_hand'] - 91.3) < 0.1);
check('zero inventory -> zero turnover (no div error)', abs(epc_dash_inventory_turnover(100, 0)['tiles'][0]['value']) < 0.01);

section('Expense breakdown');
$exp = epc_dash_expense_breakdown(array(
    array('category' => 'Rent', 'amount' => 5000),
    array('category' => 'Salaries', 'amount' => 20000),
    array('category' => 'Rent', 'amount' => 1000),
));
$expChart = $exp['charts'][0];
check('expense pie chart type', $expChart['type'] === 'pie');
check('Rent grouped to 6000', $expChart['series'][0]['data'][array_search('Rent', $expChart['labels'], true)] === 6000.0);

section('Executive roll-up (entitlement-aware)');
$exec = epc_dash_executive(array(
    'sales' => $ds,
    'cash' => $dc,
    // a payroll-only client would NOT pass sales/cash; only its enabled modules.
));
check('executive merges tiles from both modules', count($exec['tiles']) === count($ds['tiles']) + count($dc['tiles']));
check('executive tracks module list', $exec['modules'] === array('sales', 'cash'));
$tileModules = array_unique(array_column($exec['tiles'], 'module'));
check('tiles tagged with their module', in_array('sales', $tileModules, true) && in_array('cash', $tileModules, true));
$execOnlyPayroll = epc_dash_executive(array('cash' => $dc));
check('entitlement-aware: only enabled module shows', $execOnlyPayroll['modules'] === array('cash'));

echo "\n========================================\n";
echo "DASHBOARD DATA-LAYER TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
