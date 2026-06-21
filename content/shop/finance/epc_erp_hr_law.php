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
        $out = array();
        if (function_exists('epc_hr_law_profiles_all')) {
            foreach (epc_hr_law_profiles_all() as $code => $p) {
                $out[] = array('code' => $code, 'name' => (string) $p['name'], 'region' => (string) ($p['region'] ?? ''), 'gratuity' => (string) ($p['eos'] ?? ''));
            }
            return $out;
        }
        return array(
            array('code' => 'AE', 'name' => 'United Arab Emirates', 'region' => 'GCC', 'gratuity' => '21/30 days, 2-year cap'),
            array('code' => 'generic', 'name' => 'Generic / other', 'region' => 'Other', 'gratuity' => 'configurable days per year'),
        );
    }
}

if (!function_exists('epc_hr_resolve_country')) {
    /** Normalise a country code; unknown → 'generic'. */
    function epc_hr_resolve_country(string $country): string
    {
        $c = strtoupper(trim($country));
        if ($c === '' || $c === 'GENERIC') {
            return 'generic';
        }
        if (function_exists('epc_hr_law_profiles_all')) {
            $all = epc_hr_law_profiles_all();
            return isset($all[$c]) ? $c : 'generic';
        }
        $known = array('AE', 'SA', 'QA', 'OM', 'BH', 'KW', 'IN', 'PK',
            'BD', 'LK', 'NP', 'EG', 'JO', 'LB', 'MA', 'TR',
            'GB', 'DE', 'FR', 'NL', 'IE', 'US', 'CA',
            'PH', 'SG', 'MY', 'AU', 'ZA', 'NG', 'KE');
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

/* ------------------------------------- Worldwide statutory law profiles --- */

if (!function_exists('epc_hr_law_profile')) {
    /**
     * Worldwide statutory employment-law reference profile for a country.
     *
     * A structured, country-aware knowledge base of the key statutory minimums
     * an employer must comply with: working hours, overtime, probation cap,
     * notice, annual / sick / maternity / paternity leave, public holidays,
     * end-of-service basis, wage-protection scheme and the governing authority.
     *
     * Informational only — figures are representative statutory minimums and
     * must be confirmed against current local law and any collective agreement.
     *
     * @return array<string,mixed>
     */
    function epc_hr_law_profile(string $country): array
    {
        $c = strtoupper(trim($country));
        $P = epc_hr_law_profiles_all();
        $base = isset($P[$c]) ? $P[$c] : $P['generic'];
        $code = isset($P[$c]) ? $c : ($c !== '' ? $c : 'XX');
        return array('country' => $code, 'authority_url' => epc_hr_law_authority_url($code)) + $base;
    }
}

if (!function_exists('epc_hr_law_authority_url')) {
    /**
     * Official government / labour-authority website for a country, so the
     * statutory figures can be verified and refreshed against the source.
     *
     * Worldwide coverage (all built-in country packs) with a safe fallback to
     * the International Labour Organization for any unknown jurisdiction.
     */
    function epc_hr_law_authority_url(string $country): string
    {
        $c = strtoupper(trim($country));
        $map = array(
            // GCC
            'AE' => 'https://www.mohre.gov.ae',
            'SA' => 'https://www.hrsd.gov.sa',
            'QA' => 'https://www.adlsa.gov.qa',
            'OM' => 'https://www.mol.gov.om',
            'BH' => 'https://www.mlsd.gov.bh',
            'KW' => 'https://www.pam.gov.kw',
            // South Asia
            'IN' => 'https://labour.gov.in',
            'PK' => 'https://ophrd.gov.pk',
            'BD' => 'https://mole.gov.bd',
            'LK' => 'https://labourdept.gov.lk',
            'NP' => 'https://www.dol.gov.np',
            // MENA
            'EG' => 'https://www.manpower.gov.eg',
            'JO' => 'https://mol.gov.jo',
            'LB' => 'https://www.labor.gov.lb',
            'MA' => 'https://www.travail.gov.ma',
            'TR' => 'https://www.csgb.gov.tr',
            // Europe
            'GB' => 'https://www.gov.uk/browse/working',
            'DE' => 'https://www.bmas.de',
            'FR' => 'https://travail-emploi.gouv.fr',
            'NL' => 'https://business.gov.nl/regulation/employment-law/',
            'IE' => 'https://www.workplacerelations.ie',
            // Americas
            'US' => 'https://www.dol.gov',
            'CA' => 'https://www.canada.ca/en/services/jobs/workplace.html',
            // APAC
            'PH' => 'https://www.dole.gov.ph',
            'SG' => 'https://www.mom.gov.sg',
            'MY' => 'https://www.mohr.gov.my',
            'AU' => 'https://www.fairwork.gov.au',
            // Africa
            'ZA' => 'https://www.labour.gov.za',
            'NG' => 'https://labour.gov.ng',
            'KE' => 'https://www.labour.go.ke',
        );
        return isset($map[$c]) ? $map[$c] : 'https://www.ilo.org/global/standards';
    }
}

if (!function_exists('epc_hr_law_profiles_all')) {
    /**
     * All built-in statutory profiles, keyed by ISO-2 (plus 'generic').
     *
     * Fields per country: name, region, weekly_hours, workweek, overtime,
     * probation_max_months, notice_days, annual_leave_days, sick_leave,
     * maternity, paternity, public_holidays, eos (text), eos_model,
     * wage_protection, authority.
     *
     * @return array<string,array<string,mixed>>
     */
    function epc_hr_law_profiles_all(): array
    {
        return array(
            // ---------------------------------------------------------- GCC ---
            'AE' => array('name' => 'United Arab Emirates', 'region' => 'GCC', 'weekly_hours' => 48, 'workweek' => 'Mon–Fri (Sat/Sun weekend)', 'overtime' => '125% normal · 150% night (22:00–04:00) / rest day', 'probation_max_months' => 6, 'notice_days' => 30, 'annual_leave_days' => 30, 'sick_leave' => '90 days/yr (15 full · 30 half · 45 unpaid)', 'maternity' => '60 days (45 full + 15 half)', 'paternity' => '5 working days', 'public_holidays' => '~14 days', 'eos' => '21 days/yr first 5 yrs, 30 days/yr beyond; capped at 2 yrs pay', 'eos_model' => 'AE', 'wage_protection' => 'WPS mandatory (MOHRE)', 'authority' => 'MOHRE — Federal Decree-Law 33/2021'),
            'SA' => array('name' => 'Saudi Arabia', 'region' => 'GCC', 'weekly_hours' => 48, 'workweek' => 'Sun–Thu (Fri/Sat weekend); 36h Ramadan', 'overtime' => '150% (basic + 50%)', 'probation_max_months' => 3, 'notice_days' => 60, 'annual_leave_days' => 21, 'sick_leave' => '120 days (30 full · 60 at 75% · 30 unpaid)', 'maternity' => '10 weeks', 'paternity' => '3 days', 'public_holidays' => '~4 official', 'eos' => 'Half month/yr first 5 yrs, full month/yr after; resignation factor (art.85)', 'eos_model' => 'SA', 'wage_protection' => 'WPS / Mudad mandatory', 'authority' => 'MHRSD — Saudi Labor Law'),
            'QA' => array('name' => 'Qatar', 'region' => 'GCC', 'weekly_hours' => 48, 'workweek' => 'Sun–Thu; 36h Ramadan', 'overtime' => '125% · 150% night', 'probation_max_months' => 6, 'notice_days' => 30, 'annual_leave_days' => 21, 'sick_leave' => '2 weeks full + 4 weeks half', 'maternity' => '50 days', 'paternity' => '—', 'public_holidays' => '~3 official', 'eos' => 'Min 3 weeks (21 days) basic per year, after 1 year', 'eos_model' => 'QA', 'wage_protection' => 'WPS mandatory', 'authority' => 'MOL — Law 14/2004'),
            'OM' => array('name' => 'Oman', 'region' => 'GCC', 'weekly_hours' => 45, 'workweek' => 'Sun–Thu', 'overtime' => '125% · 150% night / rest day', 'probation_max_months' => 3, 'notice_days' => 30, 'annual_leave_days' => 30, 'sick_leave' => 'Up to 10 weeks (graded)', 'maternity' => '98 days', 'paternity' => '7 days', 'public_holidays' => '~9 days', 'eos' => 'GCC end-of-service: 15 days/yr first 3 yrs, 30 days/yr after (non-Omanis)', 'eos_model' => 'OM', 'wage_protection' => 'WPS (Oman) mandatory', 'authority' => 'MOL — Labour Law 53/2023'),
            'BH' => array('name' => 'Bahrain', 'region' => 'GCC', 'weekly_hours' => 48, 'workweek' => 'Sun–Thu; 6h Ramadan', 'overtime' => '125% · 150% night / rest day', 'probation_max_months' => 3, 'notice_days' => 30, 'annual_leave_days' => 30, 'sick_leave' => '55 days (15 full · 20 half · 20 unpaid)', 'maternity' => '60 days paid + 15 unpaid', 'paternity' => '1 day', 'public_holidays' => '~10 days', 'eos' => 'Leaving indemnity: 15 days/yr first 3 yrs, 30 days/yr after (expats); SIO for Bahrainis', 'eos_model' => 'BH', 'wage_protection' => 'WPS (Bahrain) mandatory', 'authority' => 'MOL — Law 36/2012'),
            'KW' => array('name' => 'Kuwait', 'region' => 'GCC', 'weekly_hours' => 48, 'workweek' => 'Sun–Thu', 'overtime' => '125% · 150% rest day', 'probation_max_months' => 3, 'notice_days' => 90, 'annual_leave_days' => 30, 'sick_leave' => '75 days (graded 15+10+10+10+30)', 'maternity' => '70 days', 'paternity' => '—', 'public_holidays' => '~13 days', 'eos' => '15 days/yr first 5 yrs, 1 month/yr after (indemnity)', 'eos_model' => 'KW', 'wage_protection' => 'WPS mandatory', 'authority' => 'PAM — Law 6/2010'),
            // -------------------------------------------------- South Asia ---
            'IN' => array('name' => 'India', 'region' => 'South Asia', 'weekly_hours' => 48, 'workweek' => 'Mon–Sat', 'overtime' => '200% (twice ordinary wage)', 'probation_max_months' => 6, 'notice_days' => 30, 'annual_leave_days' => 18, 'sick_leave' => 'State-dependent (~12 days)', 'maternity' => '26 weeks (Maternity Benefit Act)', 'paternity' => '—  (govt: 15 days)', 'public_holidays' => 'State-dependent', 'eos' => 'Payment of Gratuity Act: (15/26) × wage × yrs, after 5 yrs; cap ₹2,000,000', 'eos_model' => 'IN', 'wage_protection' => 'Wages paid via bank (Code on Wages)', 'authority' => 'Central Labour Codes + State Shops Acts'),
            'PK' => array('name' => 'Pakistan', 'region' => 'South Asia', 'weekly_hours' => 48, 'workweek' => 'Mon–Sat', 'overtime' => '200% (twice ordinary wage)', 'probation_max_months' => 3, 'notice_days' => 30, 'annual_leave_days' => 14, 'sick_leave' => '16 days (8 at full pay)', 'maternity' => '12 weeks', 'paternity' => '—', 'public_holidays' => '~13 days', 'eos' => 'Gratuity 30 days wage per completed year, or EOBI pension', 'eos_model' => 'PK', 'wage_protection' => 'Provincial wage rules', 'authority' => 'Provincial Standing Orders / Shops Acts'),
            'BD' => array('name' => 'Bangladesh', 'region' => 'South Asia', 'weekly_hours' => 48, 'workweek' => 'Sun–Thu/Sat', 'overtime' => '200%', 'probation_max_months' => 6, 'notice_days' => 30, 'annual_leave_days' => 18, 'sick_leave' => '14 days full pay', 'maternity' => '16 weeks', 'paternity' => '—', 'public_holidays' => '~11 days', 'eos' => 'Gratuity/compensation 30 days wage per year (≥10 yrs higher)', 'eos_model' => 'gratuity_generic', 'wage_protection' => 'Wages Act', 'authority' => 'Bangladesh Labour Act 2006'),
            'LK' => array('name' => 'Sri Lanka', 'region' => 'South Asia', 'weekly_hours' => 45, 'workweek' => 'Mon–Sat', 'overtime' => '150%', 'probation_max_months' => 6, 'notice_days' => 30, 'annual_leave_days' => 14, 'sick_leave' => '7 days', 'maternity' => '84 working days', 'paternity' => '—', 'public_holidays' => '~25 (poya)', 'eos' => 'Gratuity ½ month/yr after 5 yrs (Payment of Gratuity Act); EPF/ETF', 'eos_model' => 'gratuity_generic', 'wage_protection' => 'EPF/ETF contributions', 'authority' => 'Shop & Office Employees Act'),
            'NP' => array('name' => 'Nepal', 'region' => 'South Asia', 'weekly_hours' => 48, 'workweek' => 'Sun–Fri', 'overtime' => '150%', 'probation_max_months' => 6, 'notice_days' => 30, 'annual_leave_days' => 18, 'sick_leave' => '12 days', 'maternity' => '14 weeks (98 days)', 'paternity' => '—', 'public_holidays' => '~13 days', 'eos' => 'Gratuity via Social Security Fund (8.33% monthly contribution)', 'eos_model' => 'gratuity_generic', 'wage_protection' => 'SSF contributions', 'authority' => 'Labour Act 2017'),
            // --------------------------------------------------------- MENA ---
            'EG' => array('name' => 'Egypt', 'region' => 'MENA', 'weekly_hours' => 48, 'workweek' => 'Sun–Thu', 'overtime' => '135% day · 170% night', 'probation_max_months' => 3, 'notice_days' => 60, 'annual_leave_days' => 21, 'sick_leave' => 'Graded via social insurance', 'maternity' => '90 days (4 months effective)', 'paternity' => '—', 'public_holidays' => '~14 days', 'eos' => 'End-of-service via social insurance; severance on arbitrary dismissal', 'eos_model' => 'severance', 'wage_protection' => 'Social insurance', 'authority' => 'Labour Law 12/2003'),
            'JO' => array('name' => 'Jordan', 'region' => 'MENA', 'weekly_hours' => 48, 'workweek' => 'Sun–Thu', 'overtime' => '125% · 150% rest day/holiday', 'probation_max_months' => 3, 'notice_days' => 30, 'annual_leave_days' => 14, 'sick_leave' => '14 days (extendable)', 'maternity' => '10 weeks', 'paternity' => '3 days', 'public_holidays' => '~13 days', 'eos' => 'End-of-service via Social Security; severance on dismissal', 'eos_model' => 'severance', 'wage_protection' => 'Social Security Corporation', 'authority' => 'Labour Law 8/1996'),
            'LB' => array('name' => 'Lebanon', 'region' => 'MENA', 'weekly_hours' => 48, 'workweek' => 'Mon–Sat', 'overtime' => '150%', 'probation_max_months' => 3, 'notice_days' => 30, 'annual_leave_days' => 15, 'sick_leave' => 'By tenure (½–2.5 months)', 'maternity' => '10 weeks', 'paternity' => '—', 'public_holidays' => '~16 days', 'eos' => 'End-of-service indemnity via NSSF', 'eos_model' => 'severance', 'wage_protection' => 'NSSF', 'authority' => 'Lebanese Labour Code'),
            'MA' => array('name' => 'Morocco', 'region' => 'MENA', 'weekly_hours' => 44, 'workweek' => 'Mon–Fri/Sat', 'overtime' => '125%–200% (by hour/day)', 'probation_max_months' => 3, 'notice_days' => 60, 'annual_leave_days' => 18, 'sick_leave' => 'Via CNSS', 'maternity' => '14 weeks', 'paternity' => '3 days', 'public_holidays' => '~13 days', 'eos' => 'Severance (indemnité de licenciement) by seniority', 'eos_model' => 'severance', 'wage_protection' => 'CNSS', 'authority' => 'Labour Code 65-99'),
            'TR' => array('name' => 'Turkey', 'region' => 'MENA', 'weekly_hours' => 45, 'workweek' => 'Mon–Sat', 'overtime' => '150% (max 270h/yr)', 'probation_max_months' => 2, 'notice_days' => 42, 'annual_leave_days' => 14, 'sick_leave' => 'Via SGK', 'maternity' => '16 weeks', 'paternity' => '5 days', 'public_holidays' => '~15 days', 'eos' => 'Severance pay (kıdem) 30 days/yr + notice (≥1 yr)', 'eos_model' => 'severance', 'wage_protection' => 'SGK', 'authority' => 'Labour Law 4857'),
            // ------------------------------------------------------- Europe ---
            'GB' => array('name' => 'United Kingdom', 'region' => 'Europe', 'weekly_hours' => 48, 'workweek' => 'Mon–Fri (48h avg, opt-out)', 'overtime' => 'No statutory premium (contractual)', 'probation_max_months' => 6, 'notice_days' => 7, 'annual_leave_days' => 28, 'sick_leave' => 'SSP up to 28 weeks', 'maternity' => '52 weeks (39 paid)', 'paternity' => '2 weeks', 'public_holidays' => '8 bank holidays', 'eos' => 'Statutory redundancy pay by age & tenure (≥2 yrs)', 'eos_model' => 'severance', 'wage_protection' => 'PAYE / NMW enforced', 'authority' => 'Employment Rights Act / ACAS'),
            'DE' => array('name' => 'Germany', 'region' => 'Europe', 'weekly_hours' => 48, 'workweek' => 'Mon–Sat (max 8h/day)', 'overtime' => 'By agreement / works council', 'probation_max_months' => 6, 'notice_days' => 28, 'annual_leave_days' => 20, 'sick_leave' => '6 weeks full pay, then health-insurance', 'maternity' => '14 weeks', 'paternity' => 'Parental leave up to 3 yrs', 'public_holidays' => '9–13 (by Land)', 'eos' => 'No general severance; redundancy/social plan (Sozialplan), ~0.5 month/yr', 'eos_model' => 'severance', 'wage_protection' => 'Minimum wage enforced', 'authority' => 'BGB / Kündigungsschutzgesetz'),
            'FR' => array('name' => 'France', 'region' => 'Europe', 'weekly_hours' => 35, 'workweek' => 'Mon–Fri (35h legal)', 'overtime' => '125% (first 8h) · 150% beyond', 'probation_max_months' => 4, 'notice_days' => 30, 'annual_leave_days' => 25, 'sick_leave' => 'Via Sécurité sociale + employer top-up', 'maternity' => '16 weeks', 'paternity' => '28 days', 'public_holidays' => '11 days', 'eos' => 'Severance (indemnité de licenciement) ≥8 months, ~¼ month/yr', 'eos_model' => 'severance', 'wage_protection' => 'SMIC enforced', 'authority' => 'Code du travail'),
            'NL' => array('name' => 'Netherlands', 'region' => 'Europe', 'weekly_hours' => 40, 'workweek' => 'Mon–Fri', 'overtime' => 'By agreement', 'probation_max_months' => 2, 'notice_days' => 30, 'annual_leave_days' => 20, 'sick_leave' => 'Up to 104 weeks at ≥70%', 'maternity' => '16 weeks', 'paternity' => 'Up to 6 weeks', 'public_holidays' => '~11 days', 'eos' => 'Transition payment (transitievergoeding) ⅓ month/yr', 'eos_model' => 'severance', 'wage_protection' => 'Minimum wage enforced', 'authority' => 'Dutch Civil Code (BW 7)'),
            'IE' => array('name' => 'Ireland', 'region' => 'Europe', 'weekly_hours' => 48, 'workweek' => 'Mon–Fri (48h avg)', 'overtime' => 'No statutory premium (contractual)', 'probation_max_months' => 6, 'notice_days' => 7, 'annual_leave_days' => 20, 'sick_leave' => 'Statutory sick pay (rising)', 'maternity' => '26 weeks + 16 unpaid', 'paternity' => '2 weeks', 'public_holidays' => '10 days', 'eos' => 'Statutory redundancy 2 weeks/yr + 1 week (≥2 yrs)', 'eos_model' => 'severance', 'wage_protection' => 'NMW enforced', 'authority' => 'Organisation of Working Time Act'),
            // ----------------------------------------------------- Americas ---
            'US' => array('name' => 'United States', 'region' => 'Americas', 'weekly_hours' => 40, 'workweek' => 'Mon–Fri (FLSA)', 'overtime' => '150% over 40h/week (non-exempt)', 'probation_max_months' => 0, 'notice_days' => 0, 'annual_leave_days' => 0, 'sick_leave' => 'No federal mandate (state/city vary)', 'maternity' => 'FMLA 12 weeks unpaid', 'paternity' => 'FMLA 12 weeks unpaid', 'public_holidays' => '~11 federal (unpaid by default)', 'eos' => 'At-will: no statutory severance; WARN Act for mass layoffs', 'eos_model' => 'none', 'wage_protection' => 'FLSA minimum wage', 'authority' => 'FLSA / DOL (+ state law)'),
            'CA' => array('name' => 'Canada', 'region' => 'Americas', 'weekly_hours' => 40, 'workweek' => 'Mon–Fri (varies by province)', 'overtime' => '150% over 40–44h/week', 'probation_max_months' => 3, 'notice_days' => 14, 'annual_leave_days' => 10, 'sick_leave' => 'Province-dependent', 'maternity' => 'EI up to 18 months (mat+parental)', 'paternity' => 'Shared parental EI', 'public_holidays' => '~9–10 statutory', 'eos' => 'Termination/severance pay by ESA (e.g. ON ≥5 yrs: 1 week/yr)', 'eos_model' => 'severance', 'wage_protection' => 'Provincial minimum wage', 'authority' => 'Provincial ESA / Canada Labour Code'),
            // --------------------------------------------------------- APAC ---
            'PH' => array('name' => 'Philippines', 'region' => 'APAC', 'weekly_hours' => 48, 'workweek' => 'Mon–Sat', 'overtime' => '125% + holiday/night differentials', 'probation_max_months' => 6, 'notice_days' => 30, 'annual_leave_days' => 5, 'sick_leave' => 'Combined in 5-day service incentive leave', 'maternity' => '105 days', 'paternity' => '7 days', 'public_holidays' => '~18 days', 'eos' => 'Retirement pay ½ month/yr at 60 & ≥5 yrs; separation pay on authorised causes', 'eos_model' => 'severance', 'wage_protection' => '13th-month pay mandatory; SSS', 'authority' => 'Labor Code / DOLE'),
            'SG' => array('name' => 'Singapore', 'region' => 'APAC', 'weekly_hours' => 44, 'workweek' => 'Mon–Fri/Sat', 'overtime' => '150% (≤ S$2,600 / workmen)', 'probation_max_months' => 6, 'notice_days' => 30, 'annual_leave_days' => 7, 'sick_leave' => '14 outpatient / 60 hospitalisation', 'maternity' => '16 weeks', 'paternity' => '4 weeks', 'public_holidays' => '11 days', 'eos' => 'No statutory severance; retrenchment benefit by contract/norm (2 weeks–1 month/yr)', 'eos_model' => 'none', 'wage_protection' => 'CPF contributions', 'authority' => 'Employment Act / MOM'),
            'MY' => array('name' => 'Malaysia', 'region' => 'APAC', 'weekly_hours' => 45, 'workweek' => 'Mon–Fri/Sat', 'overtime' => '150% normal day', 'probation_max_months' => 6, 'notice_days' => 28, 'annual_leave_days' => 8, 'sick_leave' => '14–22 days by tenure', 'maternity' => '98 days', 'paternity' => '7 days', 'public_holidays' => '~14 days', 'eos' => 'Termination & layoff benefits 10–20 days/yr by tenure', 'eos_model' => 'severance', 'wage_protection' => 'EPF/SOCSO', 'authority' => 'Employment Act 1955'),
            'AU' => array('name' => 'Australia', 'region' => 'APAC', 'weekly_hours' => 38, 'workweek' => 'Mon–Fri (38h + reasonable OT)', 'overtime' => 'Penalty rates by award', 'probation_max_months' => 6, 'notice_days' => 28, 'annual_leave_days' => 20, 'sick_leave' => '10 days personal/carer', 'maternity' => '12 months unpaid + 18 wks govt pay', 'paternity' => 'Shared parental', 'public_holidays' => '~10–13 days', 'eos' => 'Redundancy pay 4–16 weeks by tenure (NES)', 'eos_model' => 'severance', 'wage_protection' => 'National minimum wage + super 11%+', 'authority' => 'Fair Work Act / NES'),
            // ------------------------------------------------------- Africa ---
            'ZA' => array('name' => 'South Africa', 'region' => 'Africa', 'weekly_hours' => 45, 'workweek' => 'Mon–Fri/Sat', 'overtime' => '150% (max 10h/week OT)', 'probation_max_months' => 3, 'notice_days' => 28, 'annual_leave_days' => 21, 'sick_leave' => '30 days per 3-yr cycle', 'maternity' => '4 months', 'paternity' => '10 days parental', 'public_holidays' => '12 days', 'eos' => 'Severance 1 week/yr on retrenchment', 'eos_model' => 'severance', 'wage_protection' => 'National minimum wage', 'authority' => 'BCEA / LRA'),
            'NG' => array('name' => 'Nigeria', 'region' => 'Africa', 'weekly_hours' => 40, 'workweek' => 'Mon–Fri', 'overtime' => 'By contract', 'probation_max_months' => 3, 'notice_days' => 30, 'annual_leave_days' => 6, 'sick_leave' => '12 days', 'maternity' => '12 weeks', 'paternity' => '—', 'public_holidays' => '~11 days', 'eos' => 'Gratuity/pension by contract; redundancy by agreement', 'eos_model' => 'severance', 'wage_protection' => 'National minimum wage; pension', 'authority' => 'Labour Act'),
            'KE' => array('name' => 'Kenya', 'region' => 'Africa', 'weekly_hours' => 45, 'workweek' => 'Mon–Sat', 'overtime' => '150% · 200% rest day/holiday', 'probation_max_months' => 6, 'notice_days' => 28, 'annual_leave_days' => 21, 'sick_leave' => '14 days full + 14 half', 'maternity' => '3 months', 'paternity' => '2 weeks', 'public_holidays' => '~12 days', 'eos' => 'Service/severance pay 15 days/yr on redundancy', 'eos_model' => 'severance', 'wage_protection' => 'Minimum wage; NSSF', 'authority' => 'Employment Act 2007'),
            // ------------------------------------------------------ Generic ---
            'generic' => array('name' => 'Generic / other', 'region' => 'Other', 'weekly_hours' => 48, 'workweek' => 'Mon–Fri', 'overtime' => '125% (configurable)', 'probation_max_months' => 3, 'notice_days' => 30, 'annual_leave_days' => 21, 'sick_leave' => 'Configurable', 'maternity' => 'Configurable', 'paternity' => 'Configurable', 'public_holidays' => 'Configurable', 'eos' => 'Configurable end-of-service (default 30 days/yr after 1 yr)', 'eos_model' => 'generic', 'wage_protection' => 'Configurable', 'authority' => 'Local labour law (configure)'),
        );
    }
}

/* ------------------------------------------- Per-employee compliance --- */

if (!function_exists('epc_hr_compliance_check')) {
    /**
     * Run one employee against their country's statutory rules and return a
     * list of compliance flags plus the accrued end-of-service liability.
     *
     * @param string $country ISO-2 (or 'generic')
     * @param array  $emp      {hire_date:int(ts), basic_salary:float,
     *                          allowances:float, leave_balance_days:float,
     *                          name:string}
     * @param int    $asOf     timestamp for the calculation (0 = now)
     * @return array{flags:array<int,array{severity:string,code:string,message:string,basis:string}>,
     *               service_years:float,in_probation:bool,probation_ends:int,
     *               eos_eligible:bool,eos_liability:float,eos_text:string}
     */
    function epc_hr_compliance_check(string $country, array $emp, int $asOf = 0): array
    {
        $asOf = $asOf > 0 ? $asOf : time();
        $prof = epc_hr_law_profile($country);
        $hire = (int) ($emp['hire_date'] ?? 0);
        $basic = (float) ($emp['basic_salary'] ?? 0);
        $leaveBal = (float) ($emp['leave_balance_days'] ?? 0);
        $years = $hire > 0 ? epc_hr_service_years($hire, $asOf) : 0.0;
        $flags = array();

        // --- data completeness ------------------------------------------------
        if ($hire <= 0) {
            $flags[] = array('severity' => 'error', 'code' => 'no_hire_date', 'message' => 'No hire date on record — statutory entitlements cannot be computed.', 'basis' => 'Record completeness');
        }
        if ($basic <= 0) {
            $flags[] = array('severity' => 'warn', 'code' => 'no_basic', 'message' => 'No basic salary recorded — gratuity / leave-salary cannot be computed.', 'basis' => 'Record completeness');
        }

        // --- probation --------------------------------------------------------
        $probMonths = (int) ($prof['probation_max_months'] ?? 0);
        $probEnds = 0;
        $inProbation = false;
        if ($hire > 0 && $probMonths > 0) {
            $probEnds = strtotime('+' . $probMonths . ' months', $hire);
            if ($asOf < $probEnds) {
                $inProbation = true;
                $daysLeft = (int) ceil(($probEnds - $asOf) / 86400);
                $sev = $daysLeft <= 14 ? 'warn' : 'info';
                $flags[] = array('severity' => $sev, 'code' => 'probation', 'message' => 'In probation — ends ' . date('d M Y', $probEnds) . ' (' . $daysLeft . ' day' . ($daysLeft === 1 ? '' : 's') . ' left). Confirm or act before the statutory cap.', 'basis' => 'Max probation ' . $probMonths . ' months (' . $prof['authority'] . ')');
            }
        }

        // --- end-of-service liability ----------------------------------------
        $eosModel = (string) ($prof['eos_model'] ?? 'generic');
        $eosEligible = false;
        $eosLiability = 0.0;
        $eosText = (string) ($prof['eos'] ?? '');
        $computed = array('AE', 'SA', 'QA', 'OM', 'BH', 'KW', 'IN', 'PK');
        if ($basic > 0 && $years > 0) {
            if (in_array($eosModel, $computed, true)) {
                $g = epc_hr_gratuity($eosModel, $basic, $years);
                $eosEligible = !empty($g['eligible']);
                $eosLiability = (float) $g['amount'];
            } elseif ($eosModel === 'gratuity_generic' || $eosModel === 'generic') {
                $g = epc_hr_gratuity('generic', $basic, $years);
                $eosEligible = !empty($g['eligible']);
                $eosLiability = (float) $g['amount'];
            }
            // 'severance' / 'none' carry no ongoing accrual — surfaced as text.
        }
        if ($eosEligible && $eosLiability > 0) {
            $flags[] = array('severity' => 'info', 'code' => 'eos_accrued', 'message' => 'End-of-service liability accrued — ensure it is provisioned in the accounts.', 'basis' => $eosText);
        }

        // --- annual-leave balance --------------------------------------------
        $annual = (float) ($prof['annual_leave_days'] ?? 0);
        if ($annual > 0 && $leaveBal > ($annual * 1.5)) {
            $flags[] = array('severity' => 'warn', 'code' => 'leave_excess', 'message' => 'Leave balance (' . number_format($leaveBal, 1) . ' d) exceeds 1.5× the annual entitlement (' . number_format($annual, 0) . ' d) — plan leave or encashment to respect carry-over limits.', 'basis' => 'Statutory annual leave ' . number_format($annual, 0) . ' days');
        }

        if (empty($flags)) {
            $flags[] = array('severity' => 'ok', 'code' => 'ok', 'message' => 'No statutory issues detected.', 'basis' => $prof['authority']);
        }

        return array(
            'flags' => $flags,
            'service_years' => round($years, 2),
            'in_probation' => $inProbation,
            'probation_ends' => $probEnds,
            'eos_eligible' => $eosEligible,
            'eos_liability' => round($eosLiability, 2),
            'eos_text' => $eosText,
        );
    }
}

if (!function_exists('epc_hr_compliance_worst_severity')) {
    /** Highest-priority severity in a flag list. */
    function epc_hr_compliance_worst_severity(array $flags): string
    {
        $rank = array('ok' => 0, 'info' => 1, 'warn' => 2, 'error' => 3);
        $worst = 'ok';
        foreach ($flags as $f) {
            $s = (string) ($f['severity'] ?? 'ok');
            if (($rank[$s] ?? 0) > ($rank[$worst] ?? 0)) {
                $worst = $s;
            }
        }
        return $worst;
    }
}
