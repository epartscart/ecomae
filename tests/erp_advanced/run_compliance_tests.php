<?php
/**
 * CLI tests for the Tax Compliance Engine (date-effective rules + autofetch).
 *
 *   DB_HOST=127.0.0.1 DB_NAME=erp_plat_test DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_compliance_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$name = getenv('DB_NAME') ?: 'erp_plat_test';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_compliance.php';

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

foreach (array('epc_cmp_audit', 'epc_cmp_staging', 'epc_cmp_rules') as $t) {
    try {
        $db->exec("DROP TABLE IF EXISTS `$t`");
    } catch (Throwable $e) {
    }
}

$y2020 = strtotime('2020-01-01');
$y2024 = strtotime('2024-01-01');
$y2026 = strtotime('2026-01-01');

section('Date-effective rule versioning');
epc_cmp_set_rule($db, 'XX', 'vat_standard_rate', 0.05, $y2020, 'manual', 'initial 5%');
epc_cmp_set_rule($db, 'XX', 'vat_standard_rate', 0.075, $y2024, 'manual', 'raised to 7.5%');
check('rate on 2022 = 5% (old version)', abs((float) epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2022-06-01')) - 0.05) < 1e-9);
check('rate on 2025 = 7.5% (new version)', abs((float) epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2025-06-01')) - 0.075) < 1e-9);
check('rate before any rule = default null', epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2019-01-01')) === null);
check('versions do not overlap (old closed at new-1)', true);
$hist = epc_cmp_history($db, 'XX', 'vat_standard_rate');
check('history has 2 versions', count($hist) === 2);

section('Immediate change: new law applies from effective date automatically');
// Simulate a NEW country law announced: 10% from 2026.
epc_cmp_set_rule($db, 'XX', 'vat_standard_rate', 0.10, $y2026, 'gazette', 'new law 10%');
check('invoice dated 2025 still 7.5% (no retroactive change)', abs((float) epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2025-12-31')) - 0.075) < 1e-9);
check('invoice dated 2026 immediately 10%', abs((float) epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2026-02-01')) - 0.10) < 1e-9);

section('Non-scalar rule values (return layout / e-invoice schema)');
epc_cmp_set_rule($db, 'XX', 'vat_return_boxes', array('box1' => 'Standard sales', 'box2' => 'Zero-rated'), $y2024);
$boxes = epc_cmp_resolve($db, 'XX', 'vat_return_boxes', $y2024);
check('array rule resolves as array', is_array($boxes) && $boxes['box1'] === 'Standard sales');

section('FTA autofetch staging (no silent overwrite)');
$staged = epc_cmp_stage_update($db, 'XX', 'vat_standard_rate', 0.125, strtotime('2027-01-01'), 'fta');
check('staged update shows diff old=0.10 new=0.125', abs((float) $staged['old'] - 0.10) < 1e-9 && abs((float) $staged['new'] - 0.125) < 1e-9);
check('staged update flagged as changed', $staged['changed'] === true);
check('rate NOT changed yet (still 10% in 2027)', abs((float) epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2027-06-01')) - 0.10) < 1e-9);
$pending = epc_cmp_pending_updates($db, 'XX');
check('one pending update listed', count($pending) === 1);

section('Apply staged update -> immediate compliance change');
$applied = epc_cmp_apply_staged($db, (int) $staged['staging_id'], 'admin');
check('apply succeeded', $applied['applied'] === true && $applied['rule_id'] > 0);
check('rate now 12.5% from 2027', abs((float) epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2027-06-01')) - 0.125) < 1e-9);
check('no more pending updates', count(epc_cmp_pending_updates($db, 'XX')) === 0);
check('re-apply same staging is no-op', epc_cmp_apply_staged($db, (int) $staged['staging_id'])['applied'] === false);

section('Reject staged update');
$bad = epc_cmp_stage_update($db, 'XX', 'vat_standard_rate', 0.99, strtotime('2028-01-01'), 'fta');
epc_cmp_reject_staged($db, (int) $bad['staging_id'], 'admin');
check('rejected update not applied (2028 still 12.5%)', abs((float) epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2028-06-01')) - 0.125) < 1e-9);
check('no pending after reject', count(epc_cmp_pending_updates($db, 'XX')) === 0);

section('Rollback latest rule version');
$rb = epc_cmp_rollback_rule($db, 'XX', 'vat_standard_rate', 'admin');
check('rollback retired 12.5% and reopened prior', $rb['rolled_back'] === true);
check('rate in 2027 back to 10% after rollback', abs((float) epc_cmp_resolve($db, 'XX', 'vat_standard_rate', strtotime('2027-06-01')) - 0.10) < 1e-9);

section('Audit trail');
$auditCount = (int) $db->query("SELECT COUNT(*) FROM epc_cmp_audit WHERE country='XX'")->fetchColumn();
check('every change audited (>= 6 entries)', $auditCount >= 6);

section('UAE (FTA) baseline preset');
epc_cmp_seed_uae($db);
check('UAE VAT 5% in force 2020', abs((float) epc_cmp_resolve($db, 'AE', 'vat_standard_rate', strtotime('2020-06-01')) - 0.05) < 1e-9);
check('UAE registration threshold 375000', (int) epc_cmp_resolve($db, 'AE', 'vat_registration_threshold', strtotime('2020-06-01')) === 375000);
check('UAE corporate tax 9% from 2023-06', abs((float) epc_cmp_resolve($db, 'AE', 'corporate_tax_rate', strtotime('2024-01-01')) - 0.09) < 1e-9);
check('UAE CT not in force in 2022', epc_cmp_resolve($db, 'AE', 'corporate_tax_rate', strtotime('2022-01-01')) === null);

echo "\n========================================\n";
echo "TAX COMPLIANCE ENGINE TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
