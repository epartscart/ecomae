<?php
/**
 * CLI tests for the process-flow engine (per-industry document chains).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_flow_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_process_flows.php';

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

foreach (array('epc_flow_documents', 'epc_flow_instances') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Flow registry & descriptions');
$reg = epc_flow_registry();
check('multiple industry flows defined', count($reg) >= 6);
check('jewellery flow present', isset($reg['jewellery_diamond']));
$jw = epc_flow_describe('jewellery_diamond');
check('jewellery flow has numbered steps', count($jw) >= 15 && $jw[0]['no'] === '1');
// First doc is a Purchase Requisition, prepared at procure stage.
check('jewellery step 1 = Purchase Requisition', $jw[0]['doc_code'] === 'PR' && strpos($jw[0]['doc_name'], 'Purchase Requisition') !== false);
// Flow includes WO (making), QC/Hallmark, FG, then sales docs.
$codes = array_column($jw, 'doc_code');
check('jewellery includes WO/QC/FG making chain', in_array('WO', $codes, true) && in_array('QC', $codes, true) && in_array('FG', $codes, true));
check('jewellery includes SO->DO->INV->RV sales chain', in_array('SO', $codes, true) && in_array('DO', $codes, true) && in_array('INV', $codes, true) && in_array('RV', $codes, true));
// Every step resolves a posting impact + role.
$allHavePosting = true;
foreach ($jw as $s) {
    if ($s['posting'] === '' || $s['role'] === '') {
        $allHavePosting = false;
    }
}
check('every step has role + posting impact', $allHavePosting);

section('Trading/import flow includes LC + customs');
$tr = array_column(epc_flow_describe('trading_import'), 'doc_code');
check('trading flow has LC + Bill of Entry', in_array('LC', $tr, true) && in_array('BOE', $tr, true));

section('Construction flow includes BOQ + progress + retention');
$co = array_column(epc_flow_describe('construction'), 'doc_code');
check('construction has BOQ/IPC/RET', in_array('BOQ', $co, true) && in_array('IPC', $co, true) && in_array('RET', $co, true));

section('POS flow includes shift + Z-report');
$pos = array_column(epc_flow_describe('retail_pos'), 'doc_code');
check('POS has SHIFT + ZRPT', in_array('SHIFT', $pos, true) && in_array('ZRPT', $pos, true));

section('Unknown industry falls back to general');
$gen = epc_flow_for_industry('some_new_niche_industry');
check('fallback to general flow', $gen['label'] === 'General Trading / Retail');

section('Flow runtime — ordered document recording');
$inst = epc_flow_start($db, 'general', 'TXN-1');
check('flow instance started', $inst > 0);
$next = epc_flow_next_expected($db, $inst);
check('first expected doc = PR', $next['doc_code'] === 'PR');
$r1 = epc_flow_record_document($db, $inst, 'PR', 'PR-001');
check('PR recorded, advances to step 1', $r1['step_index'] === 1 && $r1['complete'] === false);
// Out-of-sequence: try to jump to INV.
$threw = false;
try {
    epc_flow_record_document($db, $inst, 'INV', 'INV-001');
} catch (Throwable $e) {
    $threw = true;
}
check('out-of-sequence document rejected (strict)', $threw === true);
// Walk the rest of the chain in order.
$order = array('RFQ', 'SQ', 'PO', 'GRN', 'BILL', 'PV', 'EST', 'SO', 'DO', 'INV', 'RV');
$complete = false;
foreach ($order as $code) {
    $res = epc_flow_record_document($db, $inst, $code);
    $complete = $res['complete'];
}
check('full chain completes flow', $complete === true);
$prog = epc_flow_progress($db, $inst);
check('progress = 100% and status complete', abs($prog['percent'] - 100.0) < 0.01 && $prog['status'] === 'complete');
check('documents persisted (12 docs)', (int) $db->query("SELECT COUNT(*) FROM epc_flow_documents WHERE instance_id=$inst")->fetchColumn() === 12);
$nextDone = epc_flow_next_expected($db, $inst);
check('no next expected when complete', $nextDone === null);

section('Non-strict mode allows skipping');
$inst2 = epc_flow_start($db, 'general', 'TXN-2');
$skip = epc_flow_record_document($db, $inst2, 'PO', 'PO-99', false);
check('non-strict allows out-of-order recording', $skip['step_index'] === 1);

echo "\n========================================\n";
echo "PROCESS-FLOW ENGINE TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
