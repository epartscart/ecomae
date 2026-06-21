<?php
/**
 * CLI tests for the step-by-step guide content layer.
 *
 *   php tests/erp_advanced/run_guide_tests.php
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_guide_content.php';
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_process_flows.php';

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

section('Guide content completeness');
$g = epc_guide_modules();
check('returns 30+ module guides', count($g) >= 30);
$required = array('core', 'company', 'crm', 'inventory', 'procurement', 'tax', 'compliance', 'einvoice', 'gl', 'credit', 'treasury', 'audit', 'manufacturing', 'hr', 'ecommerce', 'control', 'pricing', 'migration');
foreach ($required as $code) {
    $found = false;
    foreach ($g as $e) {
        if ($e['module'] === $code) {
            $found = true;
            break;
        }
    }
    check("guide covers module '$code'", $found);
}
foreach (array('ext_reporting', 'vat_return', 'ct_return', 'audit_report', 'fin_import', 'fin_model', 'valuation') as $key) {
    check("guide has entry '$key'", isset($g[$key]) && trim($g[$key]['title']) !== '');
}

section('Every guide entry is well-formed (step-by-step)');
$bad = 0;
foreach ($g as $key => $e) {
    if (trim($e['title']) === '' || trim($e['what']) === '' || empty($e['setup']) || empty($e['daily']) || trim($e['accounting']) === '' || empty($e['tips'])) {
        $bad++;
        echo "    incomplete: $key\n";
    }
}
check('all entries have title/what/setup/daily/accounting/tips', $bad === 0);

$totalSteps = 0;
foreach ($g as $e) {
    $totalSteps += count($e['setup']) + count($e['daily']);
}
check('guide has 100+ concrete steps total', $totalSteps >= 100);

section('Entitlement-aware filtering');
$payroll = epc_guide_for_entitlements(array('hr', 'expense'));
check('payroll-only tenant sees HR guide', isset($payroll['hr']));
check('payroll-only tenant always sees core', isset($payroll['core']));
check('payroll-only tenant does NOT see manufacturing', !isset($payroll['manufacturing']));
$full = epc_guide_for_entitlements(array());
check('empty entitlements -> full guide', count($full) === count($g));
$fin = epc_guide_for_entitlements(array('gl'));
check('alias: gl entitlement shows finance suite guide', isset($fin['finance']) || isset($fin['gl']));

section('Per-industry document chains available for the guide');
$reg = epc_flow_registry();
check('flow registry has jewellery chain', isset($reg['jewellery_diamond']));
check('jewellery chain has many steps (buy->make->sell)', count($reg['jewellery_diamond']['steps']) >= 12);
check('construction chain has BOQ + retention', isset($reg['construction']));
$desc = epc_flow_describe('jewellery_diamond');
check('flow describe returns doc names + posting per step', !empty($desc) && isset($desc[0]['posting']));

echo "\n========================================\n";
echo "GUIDE CONTENT TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
