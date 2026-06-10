<?php
/**
 * CLI integration tests for Industry Packs, Worldwide Currency, and a
 * tenant-ISOLATION proof (two separate databases cannot see each other's rows).
 *
 *   DB_HOST=127.0.0.1 DB_USER=erp DB_PASS=erp \
 *     php tests/erp_advanced/run_industry_currency_tests.php
 *
 * Creates/drops two scratch DBs: erp_tenantA_test, erp_tenantB_test.
 */

declare(strict_types=1);

define('_ASTEXE_', 1);

$host = getenv('DB_HOST') ?: '127.0.0.1';
$user = getenv('DB_USER') ?: 'erp';
$pass = getenv('DB_PASS') ?: 'erp';

$fin = dirname(__DIR__, 2) . '/content/shop/finance';
require_once $fin . '/epc_erp_industry_packs.php';
require_once $fin . '/epc_erp_currency.php';
require_once $fin . '/epc_erp_credit.php';

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

function dbconn(string $host, string $name, string $user, string $pass): PDO
{
    return new PDO("mysql:host=$host;dbname=$name;charset=utf8", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

section('Industry packs registry');
$packs = epc_erp_industry_packs();
check('registry has 18+ specialized industries', count($packs) >= 18);
$required = array('label', 'costing', 'uoms', 'process_flow', 'coa_presets', 'posting_rules', 'features');
$allGood = true;
$validCosting = array_keys(epc_ind_costing_methods());
foreach ($packs as $key => $p) {
    foreach ($required as $r) {
        if (!isset($p[$r])) {
            $allGood = false;
            echo "    missing '$r' in pack '$key'\n";
        }
    }
    if (!in_array($p['costing'], $validCosting, true)) {
        $allGood = false;
        echo "    invalid costing in '$key'\n";
    }
    if (empty($p['process_flow']) || empty($p['coa_presets'])) {
        $allGood = false;
        echo "    empty flow/coa in '$key'\n";
    }
}
check('every pack has all required keys + valid costing + non-empty flow/COA', $allGood);
check('jewellery_diamond present with gold features', isset($packs['jewellery_diamond']) && in_array('gold_rate_valuation', $packs['jewellery_diamond']['features'], true));
check('oil_gas present with joint_venture feature', isset($packs['oil_gas']) && in_array('joint_venture', $packs['oil_gas']['features'], true));
check('retail_pos + wholesale + trading present', isset($packs['retail_pos'], $packs['wholesale_distribution'], $packs['trading_import_export']));

section('Specialized accounting helpers');
// Jewellery: 10g of 22K, rate 240/g 24K, making 30/g, stone 500, 5% VAT on making+stone
$j = epc_ind_jewellery_value(10.0, 22.0, 240.0, 30.0, 500.0, 5.0, false);
// metal = 10 * (22/24) * 240 = 2200.00 ; making = 300 ; stone = 500 ; vat = 5% of 800 = 40 ; total = 3040
check('jewellery metal value = 2200.00', abs($j['metal_value'] - 2200.00) < 0.01);
check('jewellery making = 300.00', abs($j['making_charge'] - 300.00) < 0.01);
check('jewellery VAT on making+stone = 40.00', abs($j['vat'] - 40.00) < 0.01);
check('jewellery total = 3040.00', abs($j['total'] - 3040.00) < 0.01);

// Construction: 1,000,000 contract, 40% complete, 250,000 previously certified, 5% retention, 5% VAT
$c = epc_ind_construction_progress_bill(1000000.0, 40.0, 250000.0, 5.0, 5.0);
// work to date = 400000 ; this period gross = 150000 ; retention = 7500 ; net = 142500 ; vat = 7125 ; payable = 149625
check('construction work-to-date = 400000', abs($c['work_done_to_date'] - 400000.0) < 0.01);
check('construction this-period gross = 150000', abs($c['this_period_gross'] - 150000.0) < 0.01);
check('construction retention = 7500', abs($c['retention'] - 7500.0) < 0.01);
check('construction net payable = 149625', abs($c['net_payable'] - 149625.0) < 0.01);

// Oil & gas JV split: 100000 across 50/30/20
$jv = epc_ind_oilgas_jv_split(100000.0, array('OperatorA' => 50, 'PartnerB' => 30, 'PartnerC' => 20));
check('JV split sums to total exactly', abs(array_sum($jv) - 100000.0) < 0.001);
check('JV operator share = 50000', abs($jv['OperatorA'] - 50000.0) < 0.01);
// rounding remainder test: 100 across 3 equal
$jv2 = epc_ind_oilgas_jv_split(100.0, array('A' => 1, 'B' => 1, 'C' => 1));
check('JV uneven split still reconciles to 100.00', abs(array_sum($jv2) - 100.0) < 0.001);

// POS shift reconcile
$pos = epc_ind_pos_shift_reconcile(array('float_amount' => 500, 'cash_sales' => 2000, 'payouts' => 100, 'counted_cash' => 2380, 'card_sales' => 1500, 'counted_card' => 1500));
// expected cash = 500 + 2000 - 100 = 2400 ; counted 2380 -> short 20
check('POS expected cash = 2400', abs($pos['expected_cash'] - 2400.0) < 0.01);
check('POS cash short by 20', abs($pos['cash_variance'] + 20.0) < 0.01 && $pos['status'] === 'short');

// Rental accrual: 100/day, 10-day period, asOf day 4
$start = 1000000;
$rent = epc_ind_rental_accrual(100.0, $start, $start + 10 * 86400, $start + 4 * 86400);
check('rental accrued 4 days = 400', abs($rent['accrued'] - 400.0) < 0.01);
check('rental remaining = 600', abs($rent['remaining'] - 600.0) < 0.01);

section('Worldwide currency');
check('catalog has 150+ currencies', count(epc_ccy_catalog()) >= 150);
check('JPY has 0 decimals', epc_ccy_info('JPY')['decimals'] === 0);
check('KWD has 3 decimals', epc_ccy_info('KWD')['decimals'] === 3);
check('USD symbol = $', epc_ccy_info('USD')['symbol'] === '$');
check('format JPY rounds to whole', epc_ccy_format(1234.56, 'JPY') === '¥ 1,235');
check('format BHD 3dp', epc_ccy_format(12.3456, 'BHD') === '.د.ب 12.346');

$dbA = dbconn($host, 'erp_tenantA_test', $user, $pass);
epc_ccy_set_config($dbA, 'AED', array('AED', 'USD', 'EUR', 'INR'), true);
$cfg = epc_ccy_get_config($dbA);
check('tenant base currency AED', $cfg['base_currency'] === 'AED');
check('4 enabled currencies', count($cfg['enabled']) === 4);

$t = 1700000000;
epc_ccy_set_rate($dbA, 'USD', 'AED', 3.6725, $t);
epc_ccy_set_rate($dbA, 'EUR', 'AED', 4.0, $t);
$conv = epc_ccy_convert($dbA, 100.0, 'USD', 'AED', $t + 86400);
check('100 USD -> 367.25 AED', abs($conv['amount'] - 367.25) < 0.01);
$inv = epc_ccy_convert($dbA, 367.25, 'AED', 'USD', $t + 86400);
check('inverse AED -> USD ~100', abs($inv['amount'] - 100.0) < 0.02);
// triangulation USD->EUR via AED base
$tri = epc_ccy_get_rate($dbA, 'USD', 'EUR', $t + 86400);
check('USD->EUR triangulated via AED (~0.9181)', $tri !== null && abs($tri - (3.6725 / 4.0)) < 0.0001);
// FX gain/loss: 100 USD booked @3.60, settled @3.6725 -> gain 7.25 AED
$fx = epc_ccy_fx_gain_loss(100.0, 3.60, 3.6725, 'AED');
check('FX gain = 7.25 AED', abs($fx['gain_loss'] - 7.25) < 0.01);

section('TENANT ISOLATION proof (two separate databases)');
$dbB = dbconn($host, 'erp_tenantB_test', $user, $pass);
// Configure tenant B differently.
epc_ccy_set_config($dbB, 'USD', array('USD', 'GBP'), true);
// Write credit profiles in each tenant with the SAME customer id.
epc_credit_set_profile($dbA, 7, array('credit_limit' => 11111, 'terms_days' => 30));
epc_credit_set_profile($dbB, 7, array('credit_limit' => 99999, 'terms_days' => 60));

$cfgA = epc_ccy_get_config($dbA);
$cfgB = epc_ccy_get_config($dbB);
check('tenant A base stays AED', $cfgA['base_currency'] === 'AED');
check('tenant B base is USD (independent)', $cfgB['base_currency'] === 'USD');

$pA = epc_credit_get_profile($dbA, 7);
$pB = epc_credit_get_profile($dbB, 7);
check('tenant A sees only its own credit limit (11111)', (int) $pA['credit_limit'] === 11111);
check('tenant B sees only its own credit limit (99999)', (int) $pB['credit_limit'] === 99999);

// Tenant A's FX rate must NOT exist in tenant B's DB.
$rateInB = epc_ccy_get_rate($dbB, 'USD', 'AED', $t + 86400);
check('tenant A FX rate is INVISIBLE to tenant B (null)', $rateInB === null);

// Confirm row counts are independent.
$ratesA = (int) $dbA->query("SELECT COUNT(*) FROM epc_ccy_rates")->fetchColumn();
$ratesB = (int) $dbB->query("SELECT COUNT(*) FROM epc_ccy_rates")->fetchColumn();
check('tenant A has FX rates, tenant B has none', $ratesA >= 2 && $ratesB === 0);

echo "\n========================================\n";
echo "INDUSTRY+CURRENCY+ISOLATION TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
