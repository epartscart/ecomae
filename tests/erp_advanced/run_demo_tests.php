<?php
/**
 * CLI tests for the multi-industry demo data (epc_erp_demo). No DB required.
 *
 *   php tests/erp_advanced/run_demo_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_demo.php';

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

section('Industry catalogue');
$inds = epc_demo_industries();
check('5 industries', count($inds) === 5);
$codes = array_map(static function ($i) {
    return $i['code'];
}, $inds);
check('includes jewellery, trading, construction, retail, manufacturing',
    !array_diff(array('jewellery', 'trading', 'construction', 'retail', 'manufacturing'), $codes));

section('Datasets are complete');
foreach ($codes as $code) {
    $ds = epc_demo_dataset($code);
    check("$code has company+trn", !empty($ds['company']['name']) && !empty($ds['company']['trn']));
    check("$code has products", count($ds['products']) >= 3);
    check("$code has customers", count($ds['customers']) >= 3);
    check("$code has orders", count($ds['orders']) >= 3);
    check("$code has a document chain", count($ds['doc_chain']) >= 4);
}

section('KPI math (jewellery)');
// GR 1800c1500 x2 paid; DP 5200c4100 x1 unpaid; SB 320c210 x5 paid
$k = epc_demo_kpis('jewellery');
check('revenue = 3600+5200+1600 = 10400', abs($k['revenue'] - 10400.0) < 0.01);
check('cogs = 3000+4100+1050 = 8150', abs($k['cogs'] - 8150.0) < 0.01);
check('gross margin = 2250', abs($k['gross_margin'] - 2250.0) < 0.01);
check('ar outstanding = 5200 (unpaid pendant)', abs($k['ar_outstanding'] - 5200.0) < 0.01);
check('paid 2 / unpaid 1', $k['paid_orders'] === 2 && $k['unpaid_orders'] === 1);
check('3 customers / 3 products', $k['customers'] === 3 && $k['products'] === 3);
check('stock value computed', $k['stock_value'] > 0);
check('margin pct computed', $k['gross_margin_pct'] > 0 && $k['gross_margin_pct'] < 100);
check('doc chain carried into kpis', in_array('Tax Invoice', $k['doc_chain'], true));

section('All-industry KPI roll-up');
$all = epc_demo_all_kpis();
check('kpis for all 5 industries', count($all) === 5);
foreach ($all as $code => $kp) {
    check("$code revenue positive", $kp['revenue'] > 0);
    check("$code currency set", $kp['currency'] === 'AED');
}

section('Isolation / safety');
check('datasets are pure data (no DB) — distinct companies per industry',
    epc_demo_dataset('jewellery')['company']['name'] !== epc_demo_dataset('retail')['company']['name']);
check('unknown industry falls back to a valid dataset', !empty(epc_demo_dataset('zzz')['products']));

echo "\n========================================\n";
echo "DEMO DATA TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
