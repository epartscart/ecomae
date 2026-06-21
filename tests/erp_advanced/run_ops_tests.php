<?php
/**
 * CLI integration tests for Manufacturing, After-sales, and Module entitlements.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_ops_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_manufacturing.php';
require_once $fin . '/epc_erp_aftersales.php';
require_once $fin . '/epc_erp_modules.php';

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

// Clean slate for the ops tables.
foreach (array('epc_mfg_bom_lines', 'epc_mfg_bom', 'epc_mfg_work_orders', 'epc_as_rma_lines', 'epc_as_rma', 'epc_as_warranty', 'epc_as_job_lines', 'epc_as_jobs', 'epc_mod_entitlements') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Manufacturing — BOM & requirements');
// BOM: 1 output (item 100) needs 2x item 201 (+10% scrap) + 3x item 202.
$bomId = epc_mfg_bom_save($db, array('product_item_id' => 100, 'name' => 'Widget', 'output_qty' => 1, 'labour_cost' => 20, 'overhead_cost' => 10), array(
    array('component_item_id' => 201, 'qty_per' => 2, 'scrap_percent' => 10),
    array('component_item_id' => 202, 'qty_per' => 3, 'scrap_percent' => 0),
));
check('BOM created', $bomId > 0);
$reqs = epc_mfg_bom_requirements($db, $bomId, 10.0);
$map = array();
foreach ($reqs as $r) {
    $map[$r['component_item_id']] = $r['qty_required'];
}
// 10 builds: 201 = 2*10*1.1 = 22 ; 202 = 3*10 = 30
check('component 201 req = 22 (incl 10% scrap)', abs($map[201] - 22.0) < 0.001);
check('component 202 req = 30', abs($map[202] - 30.0) < 0.001);

section('Manufacturing — work order costing');
$woId = epc_mfg_wo_create($db, array('bom_id' => $bomId, 'qty_planned' => 10, 'warehouse_id' => 0, 'wo_no' => 'WO-T1'));
check('work order created (planned)', $woId > 0 && epc_mfg_wo_get($db, $woId)['status'] === 'planned');
// Issue materials with explicit unit costs: 201@5, 202@4 -> 22*5 + 30*4 = 110 + 120 = 230
$iss = epc_mfg_wo_issue_materials($db, $woId, array(201 => 5.0, 202 => 4.0));
check('material cost = 230.00', abs($iss['material_cost'] - 230.0) < 0.01);
check('status now in_progress', epc_mfg_wo_get($db, $woId)['status'] === 'in_progress');
// Complete 10 units; labour/overhead default from BOM scaled (output 1 -> scale 10): labour 200, overhead 100
$done = epc_mfg_wo_complete($db, $woId, 10.0);
check('labour scaled = 200', abs($done['labour_cost'] - 200.0) < 0.01);
check('overhead scaled = 100', abs($done['overhead_cost'] - 100.0) < 0.01);
check('total cost = 530', abs($done['total_cost'] - 530.0) < 0.01);
check('unit cost = 53.00', abs($done['unit_cost'] - 53.0) < 0.01);
check('WO completed', epc_mfg_wo_get($db, $woId)['status'] === 'completed');

section('After-sales — RMA');
$rmaId = epc_as_rma_create($db, array('customer_id' => 5, 'source_type' => 'sales_order', 'source_id' => 88, 'reason' => 'Defective'), array(
    array('item_id' => 201, 'qty' => 2, 'unit_price' => 50, 'condition_note' => 'DOA'),
    array('item_id' => 202, 'qty' => 1, 'unit_price' => 30),
));
check('RMA created', $rmaId > 0);
$res = epc_as_rma_resolve($db, $rmaId, 'refund');
// lines total = 2*50 + 1*30 = 130
check('RMA refund defaults to lines total 130', abs($res['refund_amount'] - 130.0) < 0.01);
check('RMA restock flagged on refund', $res['restocked'] === true);
$rmaRej = epc_as_rma_create($db, array('customer_id' => 6), array(array('item_id' => 201, 'qty' => 1, 'unit_price' => 50)));
$resRej = epc_as_rma_resolve($db, $rmaRej, 'reject');
check('rejected RMA has zero refund + no restock', abs($resRej['refund_amount']) < 0.01 && $resRej['restocked'] === false);

section('After-sales — warranty');
$start = mktime(0, 0, 0, 1, 1, 2026);
epc_as_warranty_register($db, array('item_id' => 300, 'serial_no' => 'SN-001', 'customer_id' => 5, 'start_date' => $start, 'months' => 12));
$wOk = epc_as_warranty_check($db, 300, 'SN-001', mktime(0, 0, 0, 6, 1, 2026));
check('serial under warranty mid-term', $wOk['covered'] === true && $wOk['days_left'] > 0);
$wExp = epc_as_warranty_check($db, 300, 'SN-001', mktime(0, 0, 0, 6, 1, 2027));
check('warranty expired after 12 months', $wExp['covered'] === false);
$wNone = epc_as_warranty_check($db, 999, 'NOPE');
check('unknown item not covered', $wNone['covered'] === false);

section('After-sales — service job costing');
$jobId = epc_as_job_create($db, array('customer_id' => 5, 'asset_ref' => 'CAR-XYZ', 'complaint' => 'Brake noise', 'under_warranty' => 0));
epc_as_job_add_line($db, $jobId, array('line_type' => 'part', 'description' => 'Brake pad', 'item_id' => 201, 'qty' => 2, 'unit_price' => 60, 'tax_percent' => 5, 'chargeable' => 1));
epc_as_job_add_line($db, $jobId, array('line_type' => 'labour', 'description' => 'Fitting', 'qty' => 1, 'unit_price' => 100, 'tax_percent' => 5, 'chargeable' => 1));
$totals = epc_as_job_add_line($db, $jobId, array('line_type' => 'part', 'description' => 'Warranty sensor', 'item_id' => 202, 'qty' => 1, 'unit_price' => 80, 'tax_percent' => 5, 'chargeable' => 0));
// chargeable: parts 120, labour 100, tax 5% of 220 = 11, grand 231 (warranty line excluded)
$j = epc_as_job_recalc($db, $jobId);
check('job parts total = 120 (excl warranty line)', abs($j['parts_total'] - 120.0) < 0.01);
check('job labour total = 100', abs($j['labour_total'] - 100.0) < 0.01);
check('job tax = 11.00 (on chargeable only)', abs($j['tax_total'] - 11.0) < 0.01);
check('job grand total = 231.00', abs($j['grand_total'] - 231.0) < 0.01);

section('Module entitlements (pick-and-choose per tenant)');
// Fresh tenant: no rows -> default-on policy.
check('default-when-unset = enabled', epc_mod_enabled($db, 'einvoice', true) === true);
check('default-when-unset = disabled honored', epc_mod_enabled($db, 'einvoice', false) === false);

// E-invoice-only client.
$codes = epc_mod_apply_bundle($db, 'einvoice_only', true);
check('einvoice_only enables einvoice', epc_mod_enabled($db, 'einvoice'));
check('einvoice_only pulls core dependency', epc_mod_enabled($db, 'core'));
check('einvoice_only does NOT enable payroll', epc_mod_enabled($db, 'payroll') === false);

// Switch to payroll-only (exclusive) — einvoice must turn off.
epc_mod_apply_bundle($db, 'payroll_only', true);
check('payroll_only enables payroll', epc_mod_enabled($db, 'payroll'));
check('exclusive bundle disabled einvoice', epc_mod_enabled($db, 'einvoice') === false);

// Customs+logistics bundle resolves dependency (customs requires logistics).
$cl = epc_mod_apply_bundle($db, 'customs_logistics', true);
check('customs enabled', epc_mod_enabled($db, 'customs'));
check('customs auto-requires logistics', epc_mod_enabled($db, 'logistics'));
check('customs+logistics has no manufacturing', epc_mod_enabled($db, 'manufacturing') === false);

// Expiry handling.
epc_mod_set($db, 'reporting', true, time() - 10); // already expired
check('expired entitlement reads as disabled', epc_mod_enabled($db, 'reporting') === false);

// Full bundle.
epc_mod_apply_bundle($db, 'full', true);
check('full bundle enables manufacturing + payroll + einvoice', epc_mod_enabled($db, 'manufacturing') && epc_mod_enabled($db, 'payroll') && epc_mod_enabled($db, 'einvoice'));
check('enabled_list returns all registry modules under full', count(epc_mod_enabled_list($db)) === count(epc_mod_registry()));

echo "\n========================================\n";
echo "OPS (MFG + AFTERSALES + MODULES) TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
