<?php
/**
 * CLI tests for Manufacturing depth: work centers, routes/operations, finite &
 * infinite capacity scheduling, and regenerative multi-level MRP with
 * level-by-level netting against on-hand.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_mfg_routing_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_mfg_routing.php';

$db = new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

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

foreach (array('epc_mfg_planned', 'epc_mfg_route_op', 'epc_mfg_route', 'epc_mfg_wc', 'epc_mfg_bom_lines', 'epc_mfg_bom', 'epc_mfg_work_orders') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}
epc_mfgr_ensure_schema($db);

$CO = 1;

section('Pure: operation load + scheduling');
check('op load = setup + run*qty', abs(epc_mfgr_op_load_min(30, 2, 100) - 230.0) < 0.001);
$ops = array(
    array('op_no' => 10, 'workcenter_id' => 1, 'setup_min' => 30, 'run_min_per_unit' => 1, 'capacity_min_per_day' => 480, 'wc_code' => 'CUT'),
    array('op_no' => 20, 'workcenter_id' => 2, 'setup_min' => 20, 'run_min_per_unit' => 2, 'capacity_min_per_day' => 480, 'wc_code' => 'ASM'),
);
$sched = epc_mfgr_schedule($ops, 100, 0, true);
check('schedule has 2 operations', count($sched['operations']) === 2);
check('op1 load 130, op2 load 220', abs($sched['operations'][0]['load_min'] - 130) < 0.001 && abs($sched['operations'][1]['load_min'] - 220) < 0.001);
check('op2 starts after op1 (sequential)', $sched['operations'][1]['start_min'] >= $sched['operations'][0]['end_min'] - 0.001);
check('total minutes = 350', abs($sched['total_min'] - 350) < 0.001);
$schedBig = epc_mfgr_schedule(array(array('op_no' => 10, 'setup_min' => 0, 'run_min_per_unit' => 1, 'capacity_min_per_day' => 480)), 1000, 0, true);
check('finite: 1000min over 480/day spans >=3 days', $schedBig['operations'][0]['days'] >= 3);
$schedInf = epc_mfgr_schedule(array(array('op_no' => 10, 'setup_min' => 0, 'run_min_per_unit' => 1, 'capacity_min_per_day' => 480)), 1000, 0, false);
check('infinite: single-day load', $schedInf['operations'][0]['days'] === 1 && $schedInf['finite'] === false);

section('Work centers');
$wcCut = epc_mfgr_wc_save($db, array('company_id' => $CO, 'code' => 'cut', 'name' => 'Cutting', 'capacity_min_per_day' => 480, 'cost_per_hour' => 50));
$wcAsm = epc_mfgr_wc_save($db, array('company_id' => $CO, 'code' => 'asm', 'name' => 'Assembly', 'capacity_min_per_day' => 960, 'cost_per_hour' => 40));
check('two work centers created', $wcCut > 0 && $wcAsm > 0);
check('code upper-cased', epc_mfgr_wc_get($db, $wcCut)['code'] === 'CUT');
check('list scoped to company', count(epc_mfgr_wc_list($db, $CO)) === 2);
check('empty code rejected', (function () use ($db, $CO) { try { epc_mfgr_wc_save($db, array('company_id' => $CO, 'code' => '')); return false; } catch (Throwable $e) { return true; } })());

section('Routes / operations');
$FG = 100;
$route = epc_mfgr_route_save($db, array('company_id' => $CO, 'product_item_id' => $FG, 'name' => 'FG route'), array(
    array('op_no' => 10, 'workcenter_id' => $wcCut, 'description' => 'Cut', 'setup_min' => 30, 'run_min_per_unit' => 1),
    array('op_no' => 20, 'workcenter_id' => $wcAsm, 'description' => 'Assemble', 'setup_min' => 20, 'run_min_per_unit' => 2),
));
check('route created with ops', $route > 0 && count(epc_mfgr_route_ops($db, $route)) === 2);
check('route ops join work center code', epc_mfgr_route_ops($db, $route)[0]['wc_code'] === 'CUT');
check('route_for_item resolves active route', (int) epc_mfgr_route_for_item($db, $CO, $FG)['id'] === $route);
$schedR = epc_mfgr_schedule_route($db, $route, 100, 0, true);
check('schedule_route uses work-center capacity', $schedR['total_min'] > 0 && count($schedR['operations']) === 2);
$route = epc_mfgr_route_save($db, array('company_id' => $CO, 'product_item_id' => $FG, 'name' => 'FG route v2'), array(
    array('op_no' => 10, 'workcenter_id' => $wcCut, 'setup_min' => 10, 'run_min_per_unit' => 1),
), $route);
check('route edit replaces ops (now 1)', count(epc_mfgr_route_ops($db, $route)) === 1);

section('Multi-level MRP (pure netting)');
// FG(100) -> 2x SUB(200) + 1x RAW(100); SUB(200) -> 3x RAW(100)
$SUB = 200;
$RAW = 300;
$resolver = static function (int $itemId) use ($FG, $SUB): ?array {
    if ($itemId === $FG) {
        return array('output_qty' => 1, 'lines' => array(
            array('component_item_id' => 200, 'qty_per' => 2, 'scrap_percent' => 0),
            array('component_item_id' => 300, 'qty_per' => 1, 'scrap_percent' => 0),
        ));
    }
    if ($itemId === $SUB) {
        return array('output_qty' => 1, 'lines' => array(
            array('component_item_id' => 300, 'qty_per' => 3, 'scrap_percent' => 0),
        ));
    }
    return null; // RAW is purchased
};
$orders = array();
$oh = array();
epc_mfgr_mrp_net_explode($FG, 10, $oh, $resolver, $orders);
// FG net 10 -> production; SUB req 20 -> production; RAW from FG 10 + RAW from SUB 60 = purchase 70
$byItem = array();
foreach ($orders as $o) {
    $byItem[$o['item_id']] = ($byItem[$o['item_id']] ?? 0) + $o['qty'];
}
check('FG planned production 10', abs(($byItem[$FG] ?? 0) - 10) < 0.001);
check('SUB planned production 20 (2x10)', abs(($byItem[$SUB] ?? 0) - 20) < 0.001);
check('RAW total 70 (10 direct + 60 via SUB)', abs(($byItem[$RAW] ?? 0) - 70) < 0.001);
$fgOrder = array_values(array_filter($orders, static function ($o) use ($FG) { return $o['item_id'] === $FG; }))[0];
$rawOrder = array_values(array_filter($orders, static function ($o) use ($RAW) { return $o['item_id'] === $RAW; }))[0];
check('FG is production, RAW is purchase', $fgOrder['order_type'] === 'production' && $rawOrder['order_type'] === 'purchase');

section('MRP netting consumes on-hand level by level');
$orders2 = array();
$oh2 = array($FG => 4, $SUB => 5, $RAW => 100);
epc_mfgr_mrp_net_explode($FG, 10, $oh2, $resolver, $orders2);
$byItem2 = array();
foreach ($orders2 as $o) {
    $byItem2[$o['item_id']] = ($byItem2[$o['item_id']] ?? 0) + $o['qty'];
}
// FG net = 10-4=6 production; SUB gross 12, on-hand 5 -> net 7 production; RAW gross = 6(FG) + 21(SUB 7x3) = 27, on-hand 100 -> 0
check('FG net 6 after 4 on-hand', abs(($byItem2[$FG] ?? 0) - 6) < 0.001);
check('SUB net 7 after 5 on-hand', abs(($byItem2[$SUB] ?? 0) - 7) < 0.001);
check('RAW fully covered by on-hand (no order)', !isset($byItem2[$RAW]));

section('MRP regeneration (DB persist + aggregate)');
// Build DB BOMs to mirror the resolver
$bomFg = epc_mfg_bom_save($db, array('product_item_id' => $FG, 'name' => 'FG', 'output_qty' => 1), array(
    array('component_item_id' => $SUB, 'qty_per' => 2, 'scrap_percent' => 0),
    array('component_item_id' => $RAW, 'qty_per' => 1, 'scrap_percent' => 0),
));
$bomSub = epc_mfg_bom_save($db, array('product_item_id' => $SUB, 'name' => 'SUB', 'output_qty' => 1), array(
    array('component_item_id' => $RAW, 'qty_per' => 3, 'scrap_percent' => 0),
));
check('DB BOMs created', $bomFg > 0 && $bomSub > 0);
$run = epc_mfgr_mrp_run($db, $CO, array($FG => 10), array(), time());
$persisted = epc_mfgr_planned_list($db, $CO);
check('MRP persisted 3 planned orders', count($persisted) === 3);
$rawRow = array_values(array_filter($persisted, static function ($r) use ($RAW) { return (int) $r['item_id'] === $RAW; }))[0];
check('persisted RAW qty 70 as purchase', abs((float) $rawRow['qty'] - 70) < 0.001 && $rawRow['order_type'] === 'purchase');
check('lowest-level (RAW) sequenced first', (int) $persisted[0]['item_id'] === $RAW);
// regenerative: rerun should not duplicate
epc_mfgr_mrp_run($db, $CO, array($FG => 10), array(), time());
check('regenerative rerun keeps 3 (no dupes)', count(epc_mfgr_planned_list($db, $CO)) === 3);
// firm one
epc_mfgr_planned_firm($db, (int) $persisted[0]['id']);
$run2 = epc_mfgr_mrp_run($db, $CO, array($FG => 5), array(), time());
check('rerun with new demand reduces RAW to 35', (function () use ($db, $CO, $RAW) {
    foreach (epc_mfgr_planned_list($db, $CO) as $r) {
        if ((int) $r['item_id'] === $RAW) {
            return abs((float) $r['qty'] - 35) < 0.001;
        }
    }
    return false;
})());

section('Summary + multi-company');
epc_mfgr_wc_save($db, array('company_id' => 2, 'code' => 'WELD', 'capacity_min_per_day' => 480));
check('company 2 sees only its work center', count(epc_mfgr_wc_list($db, 2)) === 1);
$sum = epc_mfgr_summary($db, $CO);
check('summary work centers = 2', $sum['work_centers'] === 2);
check('summary planned production+purchase', $sum['planned'] === ($sum['planned_production'] + $sum['planned_purchase']));
check('summary capacity = 480+960', $sum['capacity_min'] === 1440);

echo "\n========================================\n";
echo 'MFG ROUTING/MRP TESTS: ' . $pass_count . ' passed, ' . $fail_count . " failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
