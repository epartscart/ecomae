<?php
/**
 * CLI tests for Projects/QC/Contracts + Price lists/Promotions/Loyalty.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_projects_pricing_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_projects.php';
require_once $fin . '/epc_erp_pricing.php';

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

foreach (array('epc_loy_ledger', 'epc_loy_accounts', 'epc_promo_promotions', 'epc_pl_prices', 'epc_pl_lists', 'epc_con_contracts', 'epc_qc_inspections', 'epc_prj_timesheets', 'epc_prj_tasks', 'epc_prj_projects') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Projects — tasks, timesheets, T&M summary');
$proj = epc_prj_save($db, array('code' => 'PRJ-1', 'name' => 'ERP Rollout', 'customer_id' => 5, 'billing_type' => 'tm', 'budget_cost' => 10000));
$t1 = epc_prj_task_save($db, $proj, array('name' => 'Design', 'planned_hours' => 40, 'percent_complete' => 100));
$t2 = epc_prj_task_save($db, $proj, array('name' => 'Build', 'planned_hours' => 80, 'percent_complete' => 50));
epc_prj_log_time($db, $proj, array('task_id' => $t1, 'hours' => 40, 'cost_rate' => 50, 'bill_rate' => 120, 'billable' => 1));
epc_prj_log_time($db, $proj, array('task_id' => $t2, 'hours' => 30, 'cost_rate' => 50, 'bill_rate' => 120, 'billable' => 1));
epc_prj_log_time($db, $proj, array('task_id' => $t2, 'hours' => 10, 'cost_rate' => 50, 'bill_rate' => 0, 'billable' => 0)); // non-billable
$sum = epc_prj_summary($db, $proj);
check('total hours = 80', abs($sum['hours'] - 80.0) < 0.01);
check('cost = 80*50 = 4000', abs($sum['cost'] - 4000.0) < 0.01);
check('billable value = 70*120 = 8400 (non-billable excluded)', abs($sum['billable_value'] - 8400.0) < 0.01);
check('not over budget (4000 < 10000)', $sum['over_budget'] === false);
check('percent complete = avg(100,50)=75', abs($sum['percent_complete'] - 75.0) < 0.01);
check('T&M margin = 8400 - 4000 = 4400', abs($sum['margin'] - 4400.0) < 0.01);

section('Projects — fixed-price revenue recognition');
$fp = epc_prj_save($db, array('code' => 'PRJ-2', 'name' => 'Fixed job', 'billing_type' => 'fixed', 'budget_cost' => 5000, 'contract_value' => 20000));
epc_prj_task_save($db, $fp, array('name' => 'Phase', 'planned_hours' => 10, 'percent_complete' => 40));
epc_prj_log_time($db, $fp, array('hours' => 10, 'cost_rate' => 100, 'bill_rate' => 0, 'billable' => 0));
$sumFp = epc_prj_summary($db, $fp);
check('fixed revenue recognized = 20000*40% = 8000', abs($sumFp['revenue_recognized'] - 8000.0) < 0.01);

section('Quality control — accept/reject by AQL acceptance number');
$qcOk = epc_qc_inspect($db, array('reference' => 'QC-1', 'entity_type' => 'grn', 'entity_id' => 9, 'lot_size' => 1000, 'sample_size' => 80, 'defects' => 2, 'accept_on' => 3));
check('2 defects <= accept 3 -> accepted', $qcOk['result'] === 'accepted');
$qcBad = epc_qc_inspect($db, array('reference' => 'QC-2', 'entity_type' => 'grn', 'entity_id' => 10, 'lot_size' => 1000, 'sample_size' => 80, 'defects' => 5, 'accept_on' => 3));
check('5 defects > accept 3 -> rejected', $qcBad['result'] === 'rejected');

section('Contracts — save + expiry alerting');
epc_con_save($db, array('code' => 'CON-1', 'title' => 'Annual support', 'party_type' => 'customer', 'party_id' => 5, 'value' => 50000, 'start_date' => 1000, 'end_date' => 1000 + 20 * 86400, 'auto_renew' => 1), array(array('name' => 'Q1', 'amount' => 12500)));
epc_con_save($db, array('code' => 'CON-2', 'title' => 'Far future', 'party_id' => 6, 'value' => 1000, 'start_date' => 1000, 'end_date' => 1000 + 400 * 86400));
$expiring = epc_con_expiring($db, 30, 1000);
check('one contract expiring within 30 days', count($expiring) === 1 && $expiring[0]['code'] === 'CON-1');

section('Price lists — customer + qty-break resolution');
$base = epc_pl_list_save($db, array('code' => 'BASE', 'name' => 'Base', 'currency' => 'AED', 'customer_id' => 0, 'priority' => 1));
$vip = epc_pl_list_save($db, array('code' => 'VIP', 'name' => 'VIP', 'currency' => 'AED', 'customer_id' => 5, 'priority' => 5));
epc_pl_price_set($db, array('list_id' => $base, 'item_id' => 100, 'min_qty' => 0, 'price' => 100));
epc_pl_price_set($db, array('list_id' => $base, 'item_id' => 100, 'min_qty' => 10, 'price' => 90)); // qty break
epc_pl_price_set($db, array('list_id' => $vip, 'item_id' => 100, 'min_qty' => 0, 'price' => 80));
$pBase1 = epc_pl_resolve_price($db, 100, 1, 0);
check('base price qty 1 = 100', $pBase1 !== null && abs($pBase1['price'] - 100.0) < 0.01);
$pBase10 = epc_pl_resolve_price($db, 100, 10, 0);
check('base qty-break at 10 = 90', $pBase10 !== null && abs($pBase10['price'] - 90.0) < 0.01);
$pVip = epc_pl_resolve_price($db, 100, 1, 5);
check('VIP customer gets 80 (customer list wins)', $pVip !== null && abs($pVip['price'] - 80.0) < 0.01);
$pNone = epc_pl_resolve_price($db, 999, 1, 0);
check('unknown item -> null', $pNone === null);

section('Promotions — percent/fixed/validity/min-spend');
epc_promo_save($db, array('code' => 'SAVE10', 'name' => '10% off', 'type' => 'percent', 'value' => 10, 'min_spend' => 100, 'valid_from' => 0, 'valid_to' => 0));
epc_promo_save($db, array('code' => 'FLAT50', 'name' => 'AED 50 off', 'type' => 'fixed', 'value' => 50, 'min_spend' => 200));
epc_promo_save($db, array('code' => 'OLD', 'name' => 'expired', 'type' => 'percent', 'value' => 50, 'valid_from' => 100, 'valid_to' => 200));
$p10 = epc_promo_apply($db, 'SAVE10', 500);
check('10% of 500 = 50 discount, net 450', $p10['applied'] && abs($p10['discount'] - 50.0) < 0.01 && abs($p10['net'] - 450.0) < 0.01);
$pFlat = epc_promo_apply($db, 'FLAT50', 300);
check('fixed 50 off 300 -> net 250', $pFlat['applied'] && abs($pFlat['net'] - 250.0) < 0.01);
$pMin = epc_promo_apply($db, 'FLAT50', 150);
check('below min spend -> not applied', $pMin['applied'] === false && $pMin['reason'] === 'below_min_spend');
$pExp = epc_promo_apply($db, 'OLD', 500, 999999999);
check('expired promo -> not applied', $pExp['applied'] === false && $pExp['reason'] === 'expired');
$pMissing = epc_promo_apply($db, 'NOPE', 500);
check('unknown promo -> not_found', $pMissing['applied'] === false && $pMissing['reason'] === 'not_found');

section('Loyalty — earn, balance, redeem cap');
$b1 = epc_loy_earn($db, 5, 1000, 1.0, 'INV-1'); // 1000 points
check('earn 1000 points', abs($b1 - 1000.0) < 0.01);
$b2 = epc_loy_earn($db, 5, 500, 1.0, 'INV-2'); // +500
check('balance accumulates to 1500', abs($b2 - 1500.0) < 0.01);
$r = epc_loy_redeem($db, 5, 600, 0.05, 'INV-3');
check('redeem 600 points -> value 30, balance 900', abs($r['value'] - 30.0) < 0.01 && abs($r['balance'] - 900.0) < 0.01);
$rCap = epc_loy_redeem($db, 5, 5000, 0.05); // cap at 900
check('redemption capped at balance (900)', abs($rCap['redeemed_points'] - 900.0) < 0.01 && abs($rCap['balance']) < 0.01);

echo "\n========================================\n";
echo "PROJECTS + PRICING + LOYALTY TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
