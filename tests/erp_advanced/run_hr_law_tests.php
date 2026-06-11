<?php
/**
 * CLI tests for the country-aware HRMS labour-law engine. No DB.
 *
 *   php tests/erp_advanced/run_hr_law_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
require_once dirname(__DIR__, 2) . '/content/shop/finance/epc_erp_hr_law.php';

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

section('Country resolution');
check('AE known', epc_hr_resolve_country('ae') === 'AE');
check('unknown -> generic', epc_hr_resolve_country('ZZ') === 'generic');
check('catalogue has UAE + India + generic', (function () {
    $codes = array_column(epc_hr_law_countries(), 'code');
    return in_array('AE', $codes, true) && in_array('IN', $codes, true) && in_array('generic', $codes, true);
})());

section('UAE gratuity (21/30 days, 2-year cap)');
// basic 6000, 6 years: 5*21 + 1*30 = 135 days; daily 200 => 27,000
$g = epc_hr_gratuity('AE', 6000, 6.0);
check('eligible at 6 years', $g['eligible'] === true);
check('135 days', approx($g['days'], 135.0));
check('amount 27,000', approx($g['amount'], 27000.0));
// exactly 5 years: 5*21 = 105 days; daily 200 => 21,000
$g5 = epc_hr_gratuity('AE', 6000, 5.0);
check('5 years -> 105 days', approx($g5['days'], 105.0));
check('5 years -> 21,000', approx($g5['amount'], 21000.0));
// under 1 year: nothing
$g0 = epc_hr_gratuity('AE', 6000, 0.5);
check('under 1 year -> not eligible, 0', $g0['eligible'] === false && approx($g0['amount'], 0.0));
// huge tenure hits 2-year cap (24 * basic)
$gcap = epc_hr_gratuity('AE', 6000, 60.0);
check('very long service is capped', $gcap['capped'] === true && approx($gcap['amount'], 24 * 6000.0));
// partial year pro-rate: 2.5 years -> 2.5*21 = 52.5 days
$gp = epc_hr_gratuity('AE', 3000, 2.5);
check('2.5 yrs -> 52.5 days pro-rated', approx($gp['days'], 52.5));

section('KSA gratuity + resignation factor');
// 6 years, termination: 5*0.5 + 1*1 = 3.5 months * basic
$ksa = epc_hr_gratuity('SA', 4000, 6.0, array('reason' => 'termination'));
check('KSA termination 3.5 months', approx($ksa['amount'], 3.5 * 4000.0));
// 6 years resignation (5-10y -> 2/3)
$ksaR = epc_hr_gratuity('SA', 4000, 6.0, array('reason' => 'resignation'));
check('KSA resignation 5-10y factor 2/3', approx($ksaR['amount'], 3.5 * 4000.0 * (2.0 / 3.0)));
// resignation under 2 years -> 0
$ksaU = epc_hr_gratuity('SA', 4000, 1.5, array('reason' => 'resignation'));
check('KSA resignation <2y -> 0', approx($ksaU['amount'], 0.0));

section('India gratuity (15/26, 5-year eligibility, cap)');
$inOk = epc_hr_gratuity('IN', 26000, 10.0);
check('India 10y -> (15/26)*26000*10 = 150000', approx($inOk['amount'], 150000.0));
$inNo = epc_hr_gratuity('IN', 26000, 4.0);
check('India <5y not eligible', $inNo['eligible'] === false);
// 6 years 7 months -> rounds to 7 years
$inRound = epc_hr_gratuity('IN', 26000, 6.6);
check('India 6.6y rounds to 7', approx($inRound['amount'], (15.0 / 26.0) * 26000 * 7));
$inCap = epc_hr_gratuity('IN', 5000000, 30.0);
check('India hits 2,000,000 cap', $inCap['capped'] === true && approx($inCap['amount'], 2000000.0));

section('GCC + generic gratuity');
$qa = epc_hr_gratuity('QA', 3000, 4.0); // 4*21=84 days, daily 100 => 8400
check('Qatar 3 weeks/yr', approx($qa['amount'], 84 * 100.0));
$om = epc_hr_gratuity('OM', 3000, 5.0); // 3*15 + 2*30 = 105 days, daily 100 => 10500
check('Oman 15/30 split', approx($om['amount'], 105 * 100.0));
$gen = epc_hr_gratuity('FR', 3000, 4.0, array('generic_days_per_year' => 30)); // 4*30=120 days, daily 100 =>12000
check('generic fallback (30 days/yr)', approx($gen['amount'], 120 * 100.0));

section('Annual leave entitlement & leave salary');
$lUnder = epc_hr_leave_entitlement('AE', 4.0);
check('UAE <6 months -> 0 accrued', approx($lUnder['accrued_days'], 0.0));
$lMid = epc_hr_leave_entitlement('AE', 9.0);
check('UAE 9 months -> 18 days (2/month)', approx($lMid['accrued_days'], 18.0));
$lFull = epc_hr_leave_entitlement('AE', 12.0);
check('UAE 1 year -> 30 days', approx($lFull['accrued_days'], 30.0) && approx($lFull['annual_days'], 30.0));
$ls = epc_hr_leave_salary(6000, 30);
check('leave salary 30 days basic 6000 = 6000', approx($ls['amount'], 6000.0));

section('Policy (notice / probation / hours) & overtime');
$ae = epc_hr_policy('AE');
check('UAE notice 30, probation 6, 48h', $ae['notice_days'] === 30 && $ae['max_probation_months'] === 6 && $ae['weekly_hours'] === 48);
$kw = epc_hr_policy('KW');
check('Kuwait notice 90', $kw['notice_days'] === 90);
// OT: basic 5760, daily 192, hourly 24; 10h normal @125% = 24*1.25*10 = 300
$ot = epc_hr_overtime('AE', 5760, 10, false);
check('UAE OT normal 125%', approx($ot['amount'], 300.0) && approx($ot['rate'], 1.25));
$otn = epc_hr_overtime('AE', 5760, 10, true);
check('UAE OT night/rest 150%', approx($otn['rate'], 1.5) && approx($otn['amount'], 360.0));
$oti = epc_hr_overtime('IN', 5760, 10, false);
check('India OT 2x', approx($oti['rate'], 2.0));

section('Date-effective law versioning (auto-apply new law)');
$versions = array(
    array('valid_from' => 0, 'valid_to' => 1640995199, 'days_first5' => 21, 'label' => 'old'),       // until 2021-12-31
    array('valid_from' => 1640995200, 'valid_to' => null, 'days_first5' => 21, 'label' => 'new'),     // from 2022-01-01
);
$oldRule = epc_hr_resolve_rule_version($versions, 1600000000); // 2020
$newRule = epc_hr_resolve_rule_version($versions, 1700000000); // 2023
check('pre-2022 resolves OLD law', $oldRule !== null && $oldRule['label'] === 'old');
check('post-2022 resolves NEW law', $newRule !== null && $newRule['label'] === 'new');
check('country switch: AE vs IN give different gratuity', !approx(
    epc_hr_gratuity('AE', 26000, 10.0)['amount'],
    epc_hr_gratuity('IN', 26000, 10.0)['amount']
));

echo "\n========================================\n";
echo "HRMS LABOUR-LAW TESTS: {$pass_count} passed, {$fail_count} failed\n";
echo "========================================\n";
exit($fail_count > 0 ? 1 : 0);
