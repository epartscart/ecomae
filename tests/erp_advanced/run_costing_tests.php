<?php
/**
 * CLI integration tests for Cost accounting, Rebates, and EAM maintenance.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_costing_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_costing.php';

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

foreach (array('epc_cc_allocations', 'epc_cc_centers', 'epc_rbt_accruals', 'epc_rbt_agreements', 'epc_eam_work_orders', 'epc_eam_plans', 'epc_eam_assets') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Cost accounting — centers & allocation');
$ccA = epc_cc_center_save($db, array('code' => 'CC-ADMIN', 'name' => 'Administration'));
$ccB = epc_cc_center_save($db, array('code' => 'CC-SALES', 'name' => 'Sales'));
$ccC = epc_cc_center_save($db, array('code' => 'CC-WH', 'name' => 'Warehouse'));
check('3 cost centers created', $ccA > 0 && $ccB > 0 && $ccC > 0);
// Allocate 1000 by headcount 5/3/2.
$alloc = epc_cc_allocate(1000.0, array($ccA => 5, $ccB => 3, $ccC => 2));
check('allocation by 5/3/2 = 500/300/200', abs($alloc[$ccA] - 500) < 0.01 && abs($alloc[$ccB] - 300) < 0.01 && abs($alloc[$ccC] - 200) < 0.01);
// Rounding case: 100 across 3 equal -> 33.33/33.33/33.34 sums to 100.
$alloc3 = epc_cc_allocate(100.0, array($ccA => 1, $ccB => 1, $ccC => 1));
check('equal split of 100 reconciles exactly to 100', abs(array_sum($alloc3) - 100.0) < 0.001);
$posted = epc_cc_post_allocation($db, 'JUN2026-OH', 1000.0, array($ccA => 5, $ccB => 3, $ccC => 2));
check('allocation persisted (3 rows)', (int) $db->query("SELECT COUNT(*) FROM epc_cc_allocations WHERE run_label='JUN2026-OH'")->fetchColumn() === 3);
check('persisted amounts sum to source cost', abs((float) $db->query("SELECT SUM(amount) FROM epc_cc_allocations WHERE run_label='JUN2026-OH'")->fetchColumn() - 1000.0) < 0.01);

section('Rebate management — tiered accrual');
$agId = epc_rbt_agreement_save($db, array('party_type' => 'customer', 'party_id' => 42, 'name' => 'Volume rebate', 'basis' => 'value'), array(
    array('threshold' => 0, 'percent' => 0),
    array('threshold' => 100000, 'percent' => 2),
    array('threshold' => 500000, 'percent' => 5),
));
check('agreement created', $agId > 0);
check('turnover 50k -> 0%', abs(epc_rbt_tier_rate(array(array('threshold' => 100000, 'percent' => 2), array('threshold' => 500000, 'percent' => 5)), 50000) - 0.0) < 0.001);
$ac1 = epc_rbt_accrue($db, $agId, 'Q1', 250000);
check('turnover 250k -> 2% = 5000', abs($ac1['rate'] - 2.0) < 0.001 && abs($ac1['rebate_amount'] - 5000.0) < 0.01);
$ac2 = epc_rbt_accrue($db, $agId, 'Q2', 800000);
check('turnover 800k -> 5% = 40000', abs($ac2['rate'] - 5.0) < 0.001 && abs($ac2['rebate_amount'] - 40000.0) < 0.01);
check('2 accruals persisted', (int) $db->query("SELECT COUNT(*) FROM epc_rbt_accruals WHERE agreement_id=$agId")->fetchColumn() === 2);

section('EAM — assets, plans, due dates, work orders');
$asset = epc_eam_asset_save($db, array('code' => 'GEN-01', 'name' => 'Backup Generator', 'location' => 'Roof'));
check('asset created', $asset > 0);
$lastDone = mktime(0, 0, 0, 1, 1, 2026);
$plan = epc_eam_plan_save($db, array('asset_id' => $asset, 'name' => 'Quarterly service', 'interval_days' => 90, 'last_done' => $lastDone));
check('plan created', $plan > 0);
// As-of 60 days later -> not due (90-day interval).
$due60 = epc_eam_next_due($db, $plan, $lastDone + 60 * 86400);
check('not due at 60 days', $due60['is_due'] === false && $due60['days_until'] === 30);
$due120 = epc_eam_next_due($db, $plan, $lastDone + 120 * 86400);
check('due at 120 days', $due120['is_due'] === true);
// Complete a WO -> rolls plan last_done forward.
$wo = epc_eam_wo_create($db, array('asset_id' => $asset, 'plan_id' => $plan, 'type' => 'preventive', 'description' => 'Oil + filter'));
check('maintenance WO created (open)', (int) $db->query("SELECT COUNT(*) FROM epc_eam_work_orders WHERE status='open'")->fetchColumn() === 1);
$completeAt = $lastDone + 120 * 86400;
epc_eam_wo_complete($db, $wo, 350.0, $completeAt);
check('WO completed with cost', (int) $db->query("SELECT COUNT(*) FROM epc_eam_work_orders WHERE status='completed'")->fetchColumn() === 1);
$dueAfter = epc_eam_next_due($db, $plan, $completeAt + 10 * 86400);
check('plan last_done rolled forward (not due 10 days after service)', $dueAfter['is_due'] === false);

echo "\n========================================\n";
echo "COSTING + REBATES + EAM TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
