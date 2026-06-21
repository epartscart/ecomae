<?php
/**
 * CLI tests for the tenant-country localization resolver. No DB.
 *
 *   php tests/erp_advanced/run_localization_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_i18n.php';
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_hr_law.php';
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_localization.php';

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
function approx(float $a, float $b, float $eps = 0.01): bool
{
    return abs($a - $b) < $eps;
}
function section(string $t): void
{
    echo "\n== $t ==\n";
}

section('Pakistan tenant → PK localization');
$pk = epc_country_profile('PK');
check('currency PKR', $pk['currency'] === 'PKR');
check('language Urdu', $pk['language'] === 'ur');
check('Urdu is RTL', $pk['dir'] === 'rtl');
check('tax label Sales Tax', $pk['tax_label'] === 'Sales Tax');
check('FBR e-invoice', strpos($pk['einvoice'], 'FBR') !== false);
check('fiscal year starts July', $pk['fiscal_year_start_month'] === 7);
check('HR pack PK', $pk['hr_country'] === 'PK');

section('UAE tenant → AE localization');
$ae = epc_country_profile('AE');
check('currency AED', $ae['currency'] === 'AED');
check('VAT 5%', $ae['tax_label'] === 'VAT' && approx($ae['tax_rate'], 5.0));
check('Arabic RTL', $ae['language'] === 'ar' && $ae['dir'] === 'rtl');
check('FTA e-invoice', strpos($ae['einvoice'], 'FTA') !== false);
check('fiscal year Jan', $ae['fiscal_year_start_month'] === 1);
check('HR pack AE', $ae['hr_country'] === 'AE');

section('KSA tenant → SA localization');
$sa = epc_country_profile('SA');
check('currency SAR', $sa['currency'] === 'SAR');
check('VAT 15%', approx($sa['tax_rate'], 15.0));
check('ZATCA e-invoice', strpos($sa['einvoice'], 'ZATCA') !== false);
check('HR pack SA', $sa['hr_country'] === 'SA');

section('India + UK + Turkey spot checks');
$in = epc_country_profile('IN');
check('India GST 18%, INR, FY April', $in['tax_label'] === 'GST' && $in['currency'] === 'INR' && $in['fiscal_year_start_month'] === 4);
$gb = epc_country_profile('GB');
check('UK VAT 20%, GBP', approx($gb['tax_rate'], 20.0) && $gb['currency'] === 'GBP');
$tr = epc_country_profile('TR');
check('Turkey KDV 20%, TRY', $tr['tax_label'] === 'KDV' && $tr['currency'] === 'TRY');

section('Unknown country → generic fallback');
$xx = epc_country_profile('ZZ');
check('generic currency USD', $xx['currency'] === 'USD');
check('generic LTR english', $xx['language'] === 'en' && $xx['dir'] === 'ltr');
check('generic HR pack', $xx['hr_country'] === 'generic');

section('Fiscal-year window by country');
$ts = gmmktime(0, 0, 0, 3, 10, 2026); // 2026-03-10
$pkFy = epc_loc_fiscal_year('PK', $ts); // Jul start → FY 2025-07-01..2026-06-30
check('PK FY start 2025-07-01', $pkFy['start'] === '2025-07-01');
check('PK FY end 2026-06-30', $pkFy['end'] === '2026-06-30');
$aeFy = epc_loc_fiscal_year('AE', $ts); // Jan start → 2026-01-01..2026-12-31
check('AE FY start 2026-01-01', $aeFy['start'] === '2026-01-01');
check('AE FY end 2026-12-31', $aeFy['end'] === '2026-12-31');
$inFy = epc_loc_fiscal_year('IN', $ts); // Apr start, date before Apr → 2025-04-01..2026-03-31
check('IN FY start 2025-04-01', $inFy['start'] === '2025-04-01');

section('One country setting drives tax + gratuity together');
// Tax via localization
$pkTax = epc_loc_tax_amount('PK', 1000);
check('PK tax 18% on 1000 = 180', approx($pkTax['tax'], 180.0) && $pkTax['label'] === 'Sales Tax');
$aeTax = epc_loc_tax_amount('AE', 1000);
check('AE VAT 5% on 1000 = 50', approx($aeTax['tax'], 50.0));
// Gratuity flows through the resolved HR pack
$pkProf = epc_country_profile('PK');
$grat = epc_hr_gratuity($pkProf['hr_country'], 50000, 4.0); // PK: 30 days/yr → 4*30=120 days, daily 50000/30
check('PK gratuity 4y = 120 days', approx($grat['days'], 120.0));
check('PK gratuity amount = 120 * (50000/30) = 200000', approx($grat['amount'], 200000.0));
$aeProf = epc_country_profile('AE');
$gratAe = epc_hr_gratuity($aeProf['hr_country'], 50000, 4.0); // AE: 4*21=84 days
check('AE gratuity 4y = 84 days (differs from PK)', approx($gratAe['days'], 84.0));

section('Tenant company-profile resolution + overrides');
$t1 = epc_localize_tenant(array('country' => 'PK'));
check('tenant PK resolves PKR/Urdu', $t1['currency'] === 'PKR' && $t1['language'] === 'ur');
$t2 = epc_localize_tenant(array('country' => 'AE', 'currency' => 'USD')); // explicit override
check('explicit currency override wins', $t2['currency'] === 'USD' && $t2['country'] === 'AE');
$t3 = epc_localize_tenant(array('country_code' => 'SA'));
check('country_code key accepted', $t3['currency'] === 'SAR');

echo "\n========================================\n";
echo "LOCALIZATION TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
