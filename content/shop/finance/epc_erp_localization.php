<?php
/**
 * Tenant-country localization resolver — the single source of truth.
 *
 * The tenant's country (set once on the company profile) localizes the WHOLE
 * ERP: currency, language + text direction, tax regime (label + standard rate +
 * e-invoice scheme), fiscal-year start, date format and the HR labour-law pack.
 * Every engine I built (tax toolkit, HR labour law, currency, i18n/RTL) reads
 * from here so a single country setting switches them all together.
 *
 * Pakistan → PKR / Urdu(RTL) / Sales Tax / Jul-Jun / PK gratuity.
 * UAE      → AED / Arabic / VAT 5% (FTA) / Jan-Dec / 21-30 day gratuity.
 * KSA      → SAR / Arabic / VAT 15% (ZATCA) / KSA gratuity.
 * Any other country falls back to a sensible generic profile.
 *
 * Pure (no DB) so it is exact and unit-tested. Per-document / per-employee
 * overrides remain possible for cross-border cases.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_country_profile')) {
    /**
     * Full localization profile for a country (ISO-2). Unknown → generic.
     *
     * @return array{
     *   country:string,name:string,currency:string,language:string,dir:string,
     *   tax_label:string,tax_rate:float,einvoice:string,
     *   fiscal_year_start_month:int,date_format:string,hr_country:string
     * }
     */
    function epc_country_profile(string $country): array
    {
        $c = strtoupper(trim($country));

        // currency, lang, tax_label, tax_rate, einvoice, fy_start_month, date_format
        $packs = array(
            'AE' => array('United Arab Emirates', 'AED', 'ar', 'VAT', 5.0, 'FTA (PINT-AE)', 1, 'd/m/Y'),
            'SA' => array('Saudi Arabia', 'SAR', 'ar', 'VAT', 15.0, 'ZATCA (Fatoora)', 1, 'd/m/Y'),
            'QA' => array('Qatar', 'QAR', 'ar', 'VAT', 0.0, 'GTA', 1, 'd/m/Y'),
            'OM' => array('Oman', 'OMR', 'ar', 'VAT', 5.0, 'OTA', 1, 'd/m/Y'),
            'BH' => array('Bahrain', 'BHD', 'ar', 'VAT', 10.0, 'NBR', 1, 'd/m/Y'),
            'KW' => array('Kuwait', 'KWD', 'ar', 'VAT', 0.0, '', 1, 'd/m/Y'),
            'PK' => array('Pakistan', 'PKR', 'ur', 'Sales Tax', 18.0, 'FBR (IRIS)', 7, 'd-M-Y'),
            'IN' => array('India', 'INR', 'hi', 'GST', 18.0, 'GST IRP (e-invoice)', 4, 'd-m-Y'),
            'GB' => array('United Kingdom', 'GBP', 'en', 'VAT', 20.0, 'HMRC MTD', 4, 'd/m/Y'),
            'US' => array('United States', 'USD', 'en', 'Sales Tax', 0.0, '', 1, 'm/d/Y'),
            'EG' => array('Egypt', 'EGP', 'ar', 'VAT', 14.0, 'ETA', 7, 'd/m/Y'),
            'BD' => array('Bangladesh', 'BDT', 'bn', 'VAT', 15.0, 'NBR', 7, 'd-m-Y'),
            'NG' => array('Nigeria', 'NGN', 'en', 'VAT', 7.5, 'FIRS', 1, 'd/m/Y'),
            'ZA' => array('South Africa', 'ZAR', 'en', 'VAT', 15.0, 'SARS', 3, 'Y/m/d'),
            'TR' => array('Turkey', 'TRY', 'tr', 'KDV', 20.0, 'GIB (e-Fatura)', 1, 'd.m.Y'),
        );

        if (isset($packs[$c])) {
            $p = $packs[$c];
            return array(
                'country' => $c,
                'name' => $p[0],
                'currency' => $p[1],
                'language' => $p[2],
                'dir' => epc_loc_dir($p[2]),
                'tax_label' => $p[3],
                'tax_rate' => (float) $p[4],
                'einvoice' => $p[5],
                'fiscal_year_start_month' => (int) $p[6],
                'date_format' => $p[7],
                'hr_country' => epc_loc_hr_country($c),
            );
        }

        // Generic fallback — never leave a tenant unsupported.
        return array(
            'country' => $c !== '' ? $c : 'XX',
            'name' => 'Generic',
            'currency' => 'USD',
            'language' => 'en',
            'dir' => 'ltr',
            'tax_label' => 'VAT',
            'tax_rate' => 0.0,
            'einvoice' => '',
            'fiscal_year_start_month' => 1,
            'date_format' => 'Y-m-d',
            'hr_country' => 'generic',
        );
    }
}

if (!function_exists('epc_loc_dir')) {
    /** Text direction for a language code (delegates to i18n when present). */
    function epc_loc_dir(string $lang): string
    {
        if (function_exists('epc_i18n_dir')) {
            return epc_i18n_dir($lang);
        }
        $rtl = array('ar', 'he', 'fa', 'ur', 'ps', 'sd', 'ug', 'yi', 'ckb', 'dv');
        return in_array(strtolower($lang), $rtl, true) ? 'rtl' : 'ltr';
    }
}

if (!function_exists('epc_loc_hr_country')) {
    /** Map a country to its HR labour-law pack code (delegates to hr_law). */
    function epc_loc_hr_country(string $country): string
    {
        if (function_exists('epc_hr_resolve_country')) {
            return epc_hr_resolve_country($country);
        }
        $known = array('AE', 'SA', 'QA', 'OM', 'BH', 'KW', 'IN', 'PK');
        $c = strtoupper(trim($country));
        return in_array($c, $known, true) ? $c : 'generic';
    }
}

if (!function_exists('epc_loc_fiscal_year')) {
    /**
     * Fiscal-year window containing a date, given the country's FY start month.
     * E.g. Pakistan (Jul) date 2026-03-10 → FY 2025-07-01 .. 2026-06-30.
     *
     * @return array{start:string,end:string,label:string}
     */
    function epc_loc_fiscal_year(string $country, int $ts): array
    {
        $prof = epc_country_profile($country);
        $startMonth = $prof['fiscal_year_start_month'];
        $y = (int) gmdate('Y', $ts);
        $m = (int) gmdate('n', $ts);
        $startYear = ($m >= $startMonth) ? $y : $y - 1;
        $startTs = gmmktime(0, 0, 0, $startMonth, 1, $startYear);
        $endTs = gmmktime(0, 0, 0, $startMonth, 1, $startYear + 1) - 86400;
        return array(
            'start' => gmdate('Y-m-d', $startTs),
            'end' => gmdate('Y-m-d', $endTs),
            'label' => 'FY' . gmdate('Y', $startTs) . '-' . gmdate('y', $endTs),
        );
    }
}

if (!function_exists('epc_localize_tenant')) {
    /**
     * Resolve the localization profile for a tenant from its company profile.
     * Accepts {country} (ISO-2) or {country_code}; everything else derives.
     *
     * @param array<string,mixed> $companyProfile
     * @return array<string,mixed>
     */
    function epc_localize_tenant(array $companyProfile): array
    {
        $country = (string) ($companyProfile['country'] ?? ($companyProfile['country_code'] ?? ''));
        $prof = epc_country_profile($country);
        // explicit company overrides win (e.g. multi-currency base set manually)
        if (!empty($companyProfile['currency'])) {
            $prof['currency'] = (string) $companyProfile['currency'];
        }
        if (!empty($companyProfile['language'])) {
            $prof['language'] = (string) $companyProfile['language'];
            $prof['dir'] = epc_loc_dir($prof['language']);
        }
        return $prof;
    }
}

if (!function_exists('epc_loc_tax_amount')) {
    /**
     * Tax for a base amount using the tenant country's standard rate (quick
     * path; the full tax toolkit still handles customer/category exceptions).
     *
     * @return array{label:string,rate:float,tax:float,total:float}
     */
    function epc_loc_tax_amount(string $country, float $base): array
    {
        $prof = epc_country_profile($country);
        $tax = round($base * $prof['tax_rate'] / 100.0, 2);
        return array(
            'label' => $prof['tax_label'],
            'rate' => $prof['tax_rate'],
            'tax' => $tax,
            'total' => round($base + $tax, 2),
        );
    }
}
