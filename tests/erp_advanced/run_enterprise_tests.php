<?php
/**
 * CLI tests for enterprise-parity gap modules (epc_erp_enterprise). No DB.
 *
 *   php tests/erp_advanced/run_enterprise_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_enterprise.php';

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

section('MRP — net requirements (SAP MRP)');
check('shortfall computed', abs(epc_mrp_net_requirement(100, 30, 10, 5) - 65.0) < 0.001); // 100-(30-5)-10=65
check('no requirement when covered', epc_mrp_net_requirement(20, 50, 0, 0) === 0.0);
check('safety stock reserved', abs(epc_mrp_net_requirement(50, 60, 0, 20) - 10.0) < 0.001); // 50-(60-20)=10
check('on-order reduces need', epc_mrp_net_requirement(40, 10, 40, 0) === 0.0); // 40-(10+40)<0 ->0

section('MRP run — planned orders rounded to lot');
$items = array(
    array('sku' => 'A', 'on_hand' => 5, 'demand' => 100, 'on_order' => 0, 'safety_stock' => 0, 'reorder_qty' => 50, 'source' => 'buy', 'lead_time_days' => 7),
    array('sku' => 'B', 'on_hand' => 200, 'demand' => 50, 'reorder_qty' => 10, 'source' => 'buy'),
    array('sku' => 'C', 'on_hand' => 0, 'demand' => 12, 'reorder_qty' => 0, 'source' => 'make'),
);
$plan = epc_mrp_run($items);
check('only short items planned (A and C)', count($plan) === 2);
check('A net 95 -> rounded up to lot 50 => 100', $plan[0]['planned_qty'] === 100.0);
check('A is purchase order', $plan[0]['order_type'] === 'planned_purchase_order');
check('C is production order', $plan[1]['order_type'] === 'planned_production_order');
check('C no lot -> exact 12', $plan[1]['planned_qty'] === 12.0);
check('lead time carried', $plan[0]['lead_time_days'] === 7);

section('Depreciation — straight line (SAP AA)');
$sl = epc_depreciation_schedule(12000, 0, 12, 'straight_line');
check('12 periods', count($sl) === 12);
check('monthly 1000', abs($sl[0]['depreciation'] - 1000.0) < 0.001);
check('accumulated grows', abs($sl[5]['accumulated'] - 6000.0) < 0.001);
check('ends at salvage (0)', abs($sl[11]['book_value'] - 0.0) < 0.001);
$sl2 = epc_depreciation_schedule(10000, 1000, 36, 'straight_line');
check('with salvage ends at 1000', abs($sl2[35]['book_value'] - 1000.0) < 0.01);

section('Depreciation — reducing balance');
$rb = epc_depreciation_schedule(10000, 0, 24, 'reducing_balance', 20.0);
check('24 periods', count($rb) === 24);
check('first period < cost', $rb[0]['depreciation'] > 0 && $rb[0]['depreciation'] < 10000);
check('book value declines monotonically', $rb[0]['book_value'] > $rb[1]['book_value']);
check('book value never below 0', $rb[23]['book_value'] >= 0);

section('Asset disposal gain/loss');
$g = epc_asset_disposal(2000, 2500);
check('gain detected', $g['type'] === 'gain' && abs($g['result'] - 500.0) < 0.001);
$l = epc_asset_disposal(2000, 1500);
check('loss detected', $l['type'] === 'loss' && abs($l['result'] + 500.0) < 0.001);

section('Available-to-Promise (ATP)');
$a = epc_atp(100, 30, 20, 60);
check('available = 100-30+20 = 90', abs($a['available'] - 90.0) < 0.001);
check('can promise 60', $a['can_promise'] === true && $a['shortfall'] === 0.0);
$b = epc_atp(10, 5, 0, 20);
check('cannot promise over-demand', $b['can_promise'] === false);
check('shortfall = 15', abs($b['shortfall'] - 15.0) < 0.001);

section('Intercompany + consolidation elimination');
$ic = epc_intercompany_entry('CO1', 'CO2', 5000);
check('balanced double entry across companies', $ic['balanced'] === true);
check('4 lines (2 per company)', count($ic['lines']) === 4);
check('2 lines tagged intercompany', count(array_filter($ic['lines'], static function ($l) {
    return !empty($l['ic']);
})) === 2);
$con = epc_consolidation_eliminate($ic['lines']);
check('gross debit 10000 (IC AR + expense)', abs($con['gross_debit'] - 10000.0) < 0.001);
check('eliminated 10000 (IC AR 5000 + IC AP 5000)', abs($con['eliminated'] - 10000.0) < 0.001);
check('consolidated debit excludes IC (5000)', abs($con['consolidated_debit'] - 5000.0) < 0.001);
check('consolidated still balanced', abs($con['consolidated_debit'] - $con['consolidated_credit']) < 0.001);

echo "\n========================================\n";
echo "ENTERPRISE PARITY TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
