<?php
/**
 * CLI integration tests for Org structure + voucher numbering, Fiscal periods
 * + year-end closing, and Consolidation.
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_org_closing_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_org.php';
require_once $fin . '/epc_erp_closing.php';

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

foreach (array('epc_org_sequences', 'epc_org_units', 'epc_org_companies', 'epc_fy_periods', 'epc_fy_years') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

section('Org structure: company / BU / branch / warehouse');
$coId = epc_org_company_save($db, array('code' => 'CO1', 'name' => 'Spare247 LLC', 'base_currency' => 'AED', 'country' => 'AE'));
check('company created', $coId > 0);
$buId = epc_org_unit_save($db, array('company_id' => $coId, 'type' => 'bu', 'code' => 'BU-PARTS', 'name' => 'Spare Parts BU'));
$brDxb = epc_org_unit_save($db, array('company_id' => $coId, 'parent_id' => $buId, 'type' => 'branch', 'code' => 'BR-DXB', 'name' => 'Dubai Branch'));
$brAuh = epc_org_unit_save($db, array('company_id' => $coId, 'parent_id' => $buId, 'type' => 'branch', 'code' => 'BR-AUH', 'name' => 'Abu Dhabi Branch'));
epc_org_unit_save($db, array('company_id' => $coId, 'parent_id' => $brDxb, 'type' => 'warehouse', 'code' => 'WH-DXB1', 'name' => 'Dubai Main WH'));
check('2 branches created', count(epc_org_units($db, 'branch')) === 2);
check('1 warehouse created', count(epc_org_units($db, 'warehouse')) === 1);
$tree = epc_org_branch_tree($db);
check('branch tree groups under company', count($tree) === 1 && count($tree[0]['units']) === 4);

section('Voucher numbering sequences');
epc_org_sequence_config($db, 'invoice', $brDxb, array('prefix' => 'INV-', 'year_token' => 'Y', 'pad' => 5, 'next_no' => 1, 'reset_yearly' => 1));
$t2026 = mktime(0, 0, 0, 6, 1, 2026);
$v1 = epc_org_next_voucher($db, 'invoice', $brDxb, $t2026);
$v2 = epc_org_next_voucher($db, 'invoice', $brDxb, $t2026);
check('first voucher INV-2026-00001', $v1['number'] === 'INV-2026-00001');
check('second voucher INV-2026-00002 (gapless)', $v2['number'] === 'INV-2026-00002');
// Different branch keeps its own series.
$va = epc_org_next_voucher($db, 'invoice', $brAuh, $t2026);
check('other branch starts its own series at 1', $va['sequence_no'] === 1);
// Year reset.
$t2027 = mktime(0, 0, 0, 1, 5, 2027);
$v2027 = epc_org_next_voucher($db, 'invoice', $brDxb, $t2027);
check('new year resets to INV-2027-00001', $v2027['number'] === 'INV-2027-00001');
// Auto-created sequence for an unconfigured doc type.
$jv = epc_org_next_voucher($db, 'journal', 0, $t2026);
check('auto sequence for new doc type', $jv['sequence_no'] === 1 && strpos($jv['number'], 'JOU') === 0);

section('Fiscal periods & posting guard');
$start2026 = mktime(0, 0, 0, 1, 1, 2026);
$end2026 = mktime(23, 59, 59, 12, 31, 2026);
$yId = epc_fy_create_year($db, 'FY2026', $start2026, $end2026, true);
check('fiscal year created', $yId > 0);
check('12 periods created', (int) $db->query("SELECT COUNT(*) FROM epc_fy_periods WHERE year_id=$yId")->fetchColumn() === 12);
check('mid-year date is open for posting', epc_fy_is_open($db, mktime(0, 0, 0, 6, 15, 2026)) === true);
check('date outside any year is blocked', epc_fy_is_open($db, mktime(0, 0, 0, 6, 15, 2030)) === false);
epc_fy_set_period_status($db, $yId, 6, 'locked');
check('locked period blocks posting', epc_fy_is_open($db, mktime(0, 0, 0, 6, 15, 2026)) === false);
check('open period still allows posting', epc_fy_is_open($db, mktime(0, 0, 0, 7, 15, 2026)) === true);

section('Year-end closing');
$close = epc_fy_close_year($db, $yId, 125000.00, '3200', '3900');
check('close computes profit', $close['result'] === 'profit' && abs($close['net_pl'] - 125000.0) < 0.01);
// Profit closing entry: debit P&L clearing, credit retained earnings; balanced.
$entry = $close['closing_entry'];
$dr = array_sum(array_column($entry['lines'], 'debit'));
$cr = array_sum(array_column($entry['lines'], 'credit'));
check('closing entry balances (dr=cr=125000)', abs($dr - 125000.0) < 0.01 && abs($cr - 125000.0) < 0.01);
$retLine = null;
foreach ($entry['lines'] as $l) {
    if ($l['account'] === '3200') {
        $retLine = $l;
    }
}
check('profit credited to retained earnings', $retLine !== null && abs($retLine['credit'] - 125000.0) < 0.01);
check('year now closed -> posting blocked', epc_fy_is_open($db, mktime(0, 0, 0, 7, 15, 2026)) === false);

section('Carry-forward');
$cf = epc_fy_carry_forward(array(
    array('account' => '1000', 'type' => 'asset', 'balance' => 50000),
    array('account' => '2000', 'type' => 'liability', 'balance' => 20000),
    array('account' => '3000', 'type' => 'equity', 'balance' => 30000),
    array('account' => '4000', 'type' => 'income', 'balance' => 99999),
    array('account' => '5000', 'type' => 'expense', 'balance' => 77777),
));
check('only balance-sheet accounts carry forward (3 of 5)', count($cf) === 3);
$accs = array_column($cf, 'account');
check('P&L accounts excluded from carry-forward', !in_array('4000', $accs, true) && !in_array('5000', $accs, true));

section('Consolidation across branches/companies + FX + elimination');
$entities = array(
    array('key' => 'DXB', 'currency' => 'AED', 'rate' => 1.0, 'accounts' => array(
        array('account' => 'Sales', 'type' => 'income', 'balance' => 1000000),
        array('account' => 'COGS', 'type' => 'expense', 'balance' => 600000),
        array('account' => 'Cash', 'type' => 'asset', 'balance' => 300000),
    )),
    array('key' => 'UK', 'currency' => 'GBP', 'rate' => 4.6, 'accounts' => array(
        array('account' => 'Sales', 'type' => 'income', 'balance' => 100000), // 460,000 AED
        array('account' => 'COGS', 'type' => 'expense', 'balance' => 50000),  // 230,000 AED
        array('account' => 'Cash', 'type' => 'asset', 'balance' => 20000),    // 92,000 AED
    )),
);
$consol = epc_consol_rollup($entities, 'AED');
// Sales = 1,000,000 + 460,000 = 1,460,000 ; COGS = 600,000 + 230,000 = 830,000 ; net = 630,000
check('FX-translated consolidated income = 1,460,000', abs($consol['total_income'] - 1460000.0) < 0.01);
check('consolidated net profit = 630,000', abs($consol['net_profit'] - 630000.0) < 0.01);
check('consolidated assets = 392,000', abs($consol['total_assets'] - 392000.0) < 0.01);
check('per-entity totals tracked', isset($consol['by_entity']['DXB'], $consol['by_entity']['UK']));
// Eliminate 100,000 inter-branch sales.
$elim = epc_consol_eliminate($consol, array(array('account' => 'Sales', 'amount' => 100000)));
check('elimination reduces income to 1,360,000', abs($elim['total_income'] - 1360000.0) < 0.01);
check('net profit after elimination = 530,000', abs($elim['net_profit'] - 530000.0) < 0.01);

echo "\n========================================\n";
echo "ORG + CLOSING + CONSOLIDATION TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
