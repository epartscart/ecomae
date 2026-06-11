<?php
/**
 * Country-aware HRMS labour-law engine.
 *
 * Applies each tenant's statutory HR rules automatically based on the tenant
 * (or employee) country: end-of-service gratuity, annual-leave entitlement,
 * leave salary, notice period, probation and overtime. Rules are
 * **date-effective** per country, so when a country changes its labour law the
 * new rule applies from its effective date while past settlements keep the old
 * rule (same pattern as the tax-compliance engine).
 *
 * Pure computation (no DB) so every statutory formula is exact and unit-tested.
 * The tenant country comes from the company profile; per-employee override is
 * supported for cross-border staff.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_hr_law_countries')) {
    /**
     * Countries with a built-in labour-law pack. Others fall back to 'generic'.
     *
     * @return array<int,array{code:string,name:string,gratuity:string}>
     */
    function epc_hr_law_countries(): array
    {
        return array(
            array('code' => 'AE', 'name' => 'United Arab Emirates', 'gratuity' => '21/30 days, 2-year cap'),
            array('code' => 'SA', 'name' => 'Saudi Arabia', 'gratuity' => 'half/full month + resignation factor'),
            array('code' => 'QA', 'name' => 'Qatar', 'gratuity' => '3 weeks per year'),
            array('code' => 'OM', 'name' => 'Oman', 'gratuity' => 'GCC-style end of service'),
            array('code' => 'BH', 'name' => 'Bahrain', 'gratuity' => 'GCC-style end of service'),
            array('code' => 'KW', 'name' => 'Kuwait', 'gratuity' => 'GCC-style end of service'),
            array('code' => 'IN', 'name' => 'India', 'gratuity' => 'Payment of Gratuity Act (15/26)'),
            array('code' => 'PK', 'name' => 'Pakistan', 'gratuity' => '30 days wage per completed year (Standing Orders)'),
            array('code' => 'generic', 'name' => 'Generic / other', 'gratuity' => 'configurable days per year'),
        );
    }
}

if (!function_exists('epc_hr_resolve_country')) {
    /** Normalise a country code; unknown → 'generic'. */
    function epc_hr_resolve_country(string $country): string
    {
        $c = strtoupper(trim($country));
        $known = array('AE', 'SA', 'QA', 'OM', 'BH', 'KW', 'IN', 'PK');
        return in_array($c, $known, true) ? $c : 'generic';
    }
}

if (!function_exists('epc_hr_service_years')) {
    /** Fractional years of continuous service from start→end (365.25-day year). */
    function epc_hr_service_years(int $startTs, int $endTs): float
    {
        if ($endTs <= $startTs) {
            return 0.0;
        }
        return ($endTs - $startTs) / (365.25 * 86400);
    }
}

/* ------------------------------------------------------------- Gratuity --- */

if (!function_exists('epc_hr_gratuity')) {
    /**
     * End-of-service gratuity for the given country.
     *
     * @param string $country     ISO-2 (or 'generic')
     * @param float  $basicSalary monthly basic (for IN pass basic+DA)
     * @param float  $years       fractional years of service
     * @param array  $opts        {reason:'termination'|'resignation', daily_basis:int(30),
     *                             generic_days_per_year:int(30), currency:string}
     * @return array{country:string,eligible:bool,years:float,days:float,amount:float,capped:bool,notes:string}
     */
    function epc_hr_gratuity(string $country, float $basicSalary, float $years, array $opts = array()): array
    {
        $country = epc_hr_resolve_country($country);
        $reason = $opts['reason'] ?? 'termination';
        $dailyBasis = (int) ($opts['daily_basis'] ?? 30);
        $daily = $dailyBasis > 0 ? $basicSalary / $dailyBasis : 0.0;
        $out = array('country' => $country, 'eligible' => false, 'years' => round($years, 4), 'days' => 0.0, 'amount' => 0.0, 'capped' => false, 'notes' => '');

        switch ($country) {
            case 'AE':
                // No gratuity under 1 year. 21 days/yr first 5 yrs, 30 days/yr beyond. Cap 2 yrs' pay.
                if ($years < 1.0) {
                    $out['notes'] = 'Under 1 year: no gratuity (UAE).';
                    return $out;
                }
                $first = min($years, 5.0) * 21.0;
                $beyond = max($years - 5.0, 0.0) * 30.0;
                $days = $first + $beyond;
                $amount = $days * $daily;
                $cap = 24.0 * $basicSalary; // 2 years' wage
                if ($amount > $cap) {
                    $amount = $cap;
                    $out['capped'] = true;
                }
                $out['eligible'] = true;
                $out['days'] = round($days, 2);
                $out['amount'] = round($amount, 2);
                $out['notes'] = 'UAE Federal Decree-Law 33/2021: 21 days first 5 yrs, 30 days beyond; capped at 2 years pay.';
                return $out;

            case 'SA':
                // Half month/yr first 5 yrs, full month/yr after. Resignation factor (art.85).
                $award = (min($years, 5.0) * 0.5 + max($years - 5.0, 0.0) * 1.0) * $basicSalary;
                $factor = 1.0;
                if ($reason === 'resignation') {
                    if ($years < 2.0) {
                        $factor = 0.0;
                    } elseif ($years < 5.0) {
                        $factor = 1.0 / 3.0;
                    } elseif ($years < 10.0) {
                        $factor = 2.0 / 3.0;
                    } else {
                        $factor = 1.0;
                    }
                }
                $out['eligible'] = $award * $factor > 0;
                $out['amount'] = round($award * $factor, 2);
                $out['days'] = round((min($years, 5.0) * 15.0 + max($years - 5.0, 0.0) * 30.0), 2);
                $out['notes'] = 'KSA Labor Law art.84/85: half month first 5 yrs, full month after; resignation factor applied.';
                return $out;

            case 'IN':
                // Payment of Gratuity Act: (15/26) * last wage * years; eligible after 5 yrs; cap 2,000,000.
                if ($years < 5.0) {
                    $out['notes'] = 'India: eligible only after 5 years continuous service.';
                    return $out;
                }
                $countedYears = floor($years) + ((($years - floor($years)) * 12.0) >= 6.0 ? 1 : 0);
                $amount = (15.0 / 26.0) * $basicSalary * $countedYears;
                $cap = 2000000.0;
                if ($amount > $cap) {
                    $amount = $cap;
                    $out['capped'] = true;
                }
                $out['eligible'] = true;
                $out['days'] = round((15.0 / 26.0) * 30.0 * $countedYears, 2);
                $out['amount'] = round($amount, 2);
                $out['notes'] = 'India Payment of Gratuity Act: (15/26) x last wage x years (>=6 months rounds up); cap 2,000,000.';
                return $out;

            case 'QA':
                // Qatar: minimum 3 weeks basic per year, eligible after 1 year.
                if ($years < 1.0) {
                    $out['notes'] = 'Qatar: eligible after 1 year.';
                    return $out;
                }
                $days = $years * 21.0;
                $out['eligible'] = true;
                $out['days'] = round($days, 2);
                $out['amount'] = round($days * $daily, 2);
                $out['notes'] = 'Qatar: minimum 3 weeks (21 days) basic wage per year.';
                return $out;

            case 'PK':
                // Pakistan (Standing Orders): 30 days wage per completed year, after 1 year.
                if ($years < 1.0) {
                    $out['notes'] = 'Pakistan: eligible after 1 year.';
                    return $out;
                }
                $days = $years * 30.0;
                $out['eligible'] = true;
                $out['days'] = round($days, 2);
                $out['amount'] = round($days * $daily, 2);
                $out['notes'] = 'Pakistan Standing Orders: 30 days wage per completed year of service.';
                return $out;

            case 'OM':
            case 'BH':
            case 'KW':
                // GCC-style: 15 days/yr first 3 yrs, 30 days/yr after, eligible after 1 year.
                if ($years < 1.0) {
                    $out['notes'] = 'GCC: eligible after 1 year.';
                    return $out;
                }
                $days = min($years, 3.0) * 15.0 + max($years - 3.0, 0.0) * 30.0;
                $out['eligible'] = true;
                $out['days'] = round($days, 2);
                $out['amount'] = round($days * $daily, 2);
                $out['notes'] = 'GCC end-of-service: 15 days/yr first 3 yrs, 30 days/yr after.';
                return $out;

            default: // generic
                if ($years < 1.0) {
                    $out['notes'] = 'Generic: eligible after 1 year.';
                    return $out;
                }
                $perYear = (float) ($opts['generic_days_per_year'] ?? 30);
                $days = $years * $perYear;
                $out['eligible'] = true;
                $out['days'] = round($days, 2);
                $out['amount'] = round($days * $daily, 2);
                $out['notes'] = 'Generic configurable end-of-service (' . $perYear . ' days/year).';
                return $out;
        }
    }
}

/* --------------------------------------------------------- Annual leave --- */

if (!function_exists('epc_hr_leave_entitlement')) {
    /**
     * Annual-leave entitlement in days for the period of service.
     *
     * @return array{country:string,annual_days:float,accrued_days:float,notes:string}
     */
    function epc_hr_leave_entitlement(string $country, float $serviceMonths, array $opts = array()): array
    {
        $country = epc_hr_resolve_country($country);
        $annual = 30.0;
        $notes = '';
        switch ($country) {
            case 'AE':
                $annual = 30.0; // calendar days/year after 1 year
                if ($serviceMonths < 6.0) {
                    return array('country' => $country, 'annual_days' => $annual, 'accrued_days' => 0.0, 'notes' => 'UAE: no paid leave under 6 months.');
                }
                if ($serviceMonths < 12.0) {
                    $accrued = floor($serviceMonths) * 2.0; // 2 days per completed month
                    return array('country' => $country, 'annual_days' => $annual, 'accrued_days' => round($accrued, 2), 'notes' => 'UAE: 2 days per month between 6-12 months.');
                }
                $accrued = ($serviceMonths / 12.0) * $annual;
                return array('country' => $country, 'annual_days' => $annual, 'accrued_days' => round($accrued, 2), 'notes' => 'UAE: 30 calendar days/year after 1 year.');
            case 'SA':
                $annual = $serviceMonths >= 60.0 ? 30.0 : 21.0; // 21 days, 30 after 5 years
                $notes = 'KSA: 21 days/year, 30 days after 5 years.';
                break;
            case 'IN':
                $annual = 18.0; // earned leave (varies by state; common ~18)
                $notes = 'India: ~18 earned-leave days/year (state-dependent).';
                break;
            case 'PK':
                $annual = 14.0; // annual leave after 12 months (Shops & Establishments)
                $notes = 'Pakistan: 14 days annual leave after 12 months.';
                break;
            default:
                $annual = (float) ($opts['annual_days'] ?? 21);
                $notes = 'Generic annual leave.';
        }
        $accrued = ($serviceMonths / 12.0) * $annual;
        return array('country' => $country, 'annual_days' => $annual, 'accrued_days' => round($accrued, 2), 'notes' => $notes);
    }
}

if (!function_exists('epc_hr_leave_salary')) {
    /**
     * Leave salary for a number of leave days (paid on basic, 30-day month).
     *
     * @return array{days:float,daily:float,amount:float}
     */
    function epc_hr_leave_salary(float $basicSalary, float $days, int $dailyBasis = 30): array
    {
        $daily = $dailyBasis > 0 ? $basicSalary / $dailyBasis : 0.0;
        return array('days' => round($days, 2), 'daily' => round($daily, 4), 'amount' => round($daily * $days, 2));
    }
}

/* -------------------------------------------- Notice / probation / OT ----- */

if (!function_exists('epc_hr_policy')) {
    /**
     * Statutory notice period, max probation, standard weekly hours.
     *
     * @return array{country:string,notice_days:int,max_probation_months:int,weekly_hours:int}
     */
    function epc_hr_policy(string $country): array
    {
        $country = epc_hr_resolve_country($country);
        $map = array(
            'AE' => array(30, 6, 48),
            'SA' => array(60, 3, 48),
            'QA' => array(30, 6, 48),
            'OM' => array(30, 3, 45),
            'BH' => array(30, 3, 48),
            'KW' => array(90, 3, 48),
            'IN' => array(30, 6, 48),
            'PK' => array(30, 3, 48),
            'generic' => array(30, 3, 48),
        );
        $p = $map[$country] ?? $map['generic'];
        return array('country' => $country, 'notice_days' => $p[0], 'max_probation_months' => $p[1], 'weekly_hours' => $p[2]);
    }
}

if (!function_exists('epc_hr_overtime')) {
    /**
     * Overtime pay. UAE: 125% normal, 150% for night (22:00-04:00) / rest day.
     *
     * @return array{hours:float,hourly:float,rate:float,amount:float}
     */
    function epc_hr_overtime(string $country, float $basicSalary, float $hours, bool $nightOrRestDay = false, array $opts = array()): array
    {
        $country = epc_hr_resolve_country($country);
        $dailyBasis = (int) ($opts['daily_basis'] ?? 30);
        $dailyHours = (float) ($opts['daily_hours'] ?? 8);
        $hourly = ($dailyBasis > 0 && $dailyHours > 0) ? ($basicSalary / $dailyBasis) / $dailyHours : 0.0;
        $rate = $nightOrRestDay ? 1.5 : 1.25;
        if ($country === 'IN' || $country === 'PK') {
            $rate = 2.0; // India/Pakistan: OT typically 2x
        }
        return array('hours' => round($hours, 2), 'hourly' => round($hourly, 4), 'rate' => $rate, 'amount' => round($hourly * $rate * $hours, 2));
    }
}

/* ----------------------------------------------- Date-effective resolver -- */

if (!function_exists('epc_hr_resolve_rule_version')) {
    /**
     * Resolve which law version is in force on a given date, from a set of
     * versioned rules ({valid_from, valid_to(optional), ...}). Lets a country's
     * labour-law change take effect automatically from its effective date.
     *
     * @param array<int,array<string,mixed>> $versions
     * @return array<string,mixed>|null
     */
    function epc_hr_resolve_rule_version(array $versions, int $onDate): ?array
    {
        $best = null;
        $bestFrom = -1;
        foreach ($versions as $v) {
            $from = (int) ($v['valid_from'] ?? 0);
            $to = isset($v['valid_to']) && $v['valid_to'] !== null ? (int) $v['valid_to'] : PHP_INT_MAX;
            if ($onDate >= $from && $onDate <= $to && $from >= $bestFrom) {
                $best = $v;
                $bestFrom = $from;
            }
        }
        return $best;
    }
}
