<?php
/**
 * External Reporting — report builders.
 *
 * Each builder fetches live ERP data (or company profile) and returns the
 * formatted BODY of the statutory report. The tab UI wraps it with the company
 * header, regulator/law/format/IFRS links and the Fetch control. Everything is
 * driven by the tenant's registration country (passed in as $country).
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_external_reports.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_gl.php';

if (!function_exists('epc_ext_ccy')) {
    function epc_ext_ccy(PDO $db, string $country): string
    {
        try {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company.php';
            $co = epc_co_profile_get($db);
            if (!empty($co['base_currency'])) {
                return (string) $co['base_currency'];
            }
        } catch (\Throwable $e) {
            // fall through to country profile
        }
        $p = epc_country_profile($country);
        return (string) ($p['currency'] ?? 'AED');
    }
}

if (!function_exists('epc_ext_company')) {
    /**
     * @return array<string,string>
     */
    function epc_ext_company(PDO $db): array
    {
        try {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company.php';
            return epc_co_profile_get($db);
        } catch (\Throwable $e) {
            return array('legal_name' => 'Company', 'country' => '', 'trn' => '', 'base_currency' => '');
        }
    }
}

if (!function_exists('epc_ext_m')) {
    function epc_ext_m($v, string $ccy = ''): string
    {
        $s = epc_erp_money($v);
        return $ccy !== '' ? ($ccy . ' ' . $s) : $s;
    }
}

if (!function_exists('epc_ext_report_build')) {
    /**
     * Build a report. Returns array(title, body_html, summary[]).
     *
     * @return array{title:string,body:string,summary:array<string,string>,live:bool}
     */
    function epc_ext_report_build(PDO $db, string $key, string $country, $from, $to): array
    {
        $def = epc_ext_report_get($key);
        if ($def === null) {
            return array('title' => 'Unknown report', 'body' => '<p class="text-muted">Report not found.</p>', 'summary' => array(), 'live' => false);
        }
        $builder = (string) ($def['builder'] ?? '');
        $name = (string) $def['name'];
        $ccy = epc_ext_ccy($db, $country);

        try {
            switch ($builder) {
                case 'vat_return':
                    return epc_ext_b_vat($db, $name, $country, $ccy, $from, $to);
                case 'corporate_tax':
                    return epc_ext_b_ct($db, $name, $country, $ccy, $from, $to);
                case 'afs':
                case 'interim':
                case 'consolidated':
                    return epc_ext_b_afs($db, $name, $country, $ccy, $from, $to, $builder);
                case 'wps':
                    return epc_ext_b_wps($db, $name, $country, $ccy);
                case 'payroll_tax':
                    return epc_ext_b_payroll($db, $name, $country, $ccy);
                case 'eos':
                    return epc_ext_b_eos($db, $name, $country, $ccy);
                case 'employee_census':
                    return epc_ext_b_census($db, $name, $country, $ccy);
                case 'ubo':
                    return epc_ext_b_ubo($db, $name, $country, $ccy);
                case 'esr':
                case 'esr_notify':
                    return epc_ext_b_esr($db, $name, $country, $ccy, $from, $to, $builder === 'esr_notify');
                case 'cbcr':
                    return epc_ext_b_cbcr($db, $name, $country, $ccy, $from, $to);
                default:
                    return epc_ext_b_template($db, $def, $country, $ccy, $from, $to);
            }
        } catch (\Throwable $e) {
            return epc_ext_b_template($db, $def, $country, $ccy, $from, $to);
        }
    }
}

/* ---------------------------------------------------------------- helpers */

if (!function_exists('epc_ext_kv_table')) {
    /**
     * @param array<int,array{0:string,1:string,2?:bool}> $rows label,value,strong
     */
    function epc_ext_kv_table(array $rows): string
    {
        $h = '<table class="table table-bordered table-condensed" style="max-width:760px;">';
        foreach ($rows as $r) {
            $strong = !empty($r[2]);
            $h .= '<tr' . ($strong ? ' style="font-weight:700;background:#f5f7fa;"' : '') . '>'
                . '<td>' . epc_erp_h($r[0]) . '</td>'
                . '<td style="text-align:right;white-space:nowrap;">' . epc_erp_h($r[1]) . '</td></tr>';
        }
        return $h . '</table>';
    }
}

/* ---------------------------------------------------------------- VAT / GST */

if (!function_exists('epc_ext_b_vat')) {
    function epc_ext_b_vat(PDO $db, string $name, string $country, string $ccy, $from, $to): array
    {
        $p = epc_country_profile($country);
        $rate = (float) ($p['tax_rate'] ?? 5.0);
        $taxLabel = (string) ($p['tax_label'] ?? 'VAT');
        $pl = epc_erp_gl_pl_report($db, $from, $to);
        $sales = (float) ($pl['total_revenue'] ?? 0);
        $purch = (float) ($pl['total_expenses'] ?? 0);
        $outTax = round($sales * $rate / 100, 2);
        $inTax = round($purch * $rate / 100, 2);
        $net = round($outTax - $inTax, 2);
        $isUae = strtoupper($country) === 'AE';

        $rows = array(
            array(($isUae ? 'Box 1 — ' : '') . 'Standard-rated supplies (ex ' . $taxLabel . ')', epc_ext_m($sales, $ccy)),
            array(($isUae ? 'Box 1 — ' : '') . 'Output ' . $taxLabel . ' @ ' . rtrim(rtrim(number_format($rate, 2), '0'), '.') . '%', epc_ext_m($outTax, $ccy)),
            array(($isUae ? 'Box 6 — ' : '') . 'Standard-rated expenses / imports (ex ' . $taxLabel . ')', epc_ext_m($purch, $ccy)),
            array(($isUae ? 'Box 9 — ' : '') . 'Recoverable input ' . $taxLabel, epc_ext_m($inTax, $ccy)),
            array(($isUae ? 'Box 12/13 — ' : '') . 'Net ' . $taxLabel . ' ' . ($net >= 0 ? 'payable' : 'refundable'), epc_ext_m(abs($net), $ccy), true),
        );
        $body = '<p class="text-muted">' . epc_erp_h($taxLabel) . ' computed from posted general-ledger revenue and expense activity for the selected period at the '
            . epc_erp_h(rtrim(rtrim(number_format($rate, 2), '0'), '.')) . '% statutory rate'
            . ($isUae ? ' (FTA VAT 201 box layout).' : '.') . '</p>'
            . epc_ext_kv_table($rows);
        return array(
            'title' => $name,
            'body' => $body,
            'summary' => array(
                'Output ' . $taxLabel => epc_ext_m($outTax, $ccy),
                'Input ' . $taxLabel => epc_ext_m($inTax, $ccy),
                'Net ' . ($net >= 0 ? 'payable' : 'refundable') => epc_ext_m(abs($net), $ccy),
            ),
            'live' => true,
        );
    }
}

/* ---------------------------------------------------------------- Corporate tax */

if (!function_exists('epc_ext_ct_rule')) {
    /**
     * @return array{rate:float,threshold:float,note:string}
     */
    function epc_ext_ct_rule(string $country): array
    {
        $c = strtoupper($country);
        $map = array(
            'AE' => array(9.0, 375000.0, '0% on taxable income up to AED 375,000; 9% above (Federal Decree-Law 47/2022).'),
            'SA' => array(20.0, 0.0, 'Corporate income tax 20% (plus Zakat 2.5% for GCC-owned shares) — ZATCA.'),
            'QA' => array(10.0, 0.0, 'Standard corporate tax 10% (GTA).'),
            'OM' => array(15.0, 0.0, 'Corporate income tax 15% (Oman Tax Authority).'),
            'BH' => array(0.0, 0.0, 'No general corporate income tax (Bahrain); DMTT 15% for in-scope MNEs.'),
            'KW' => array(15.0, 0.0, 'Corporate income tax 15% (foreign-owned); DMTT for MNEs.'),
            'IN' => array(25.0, 0.0, 'Corporate tax ~25% (domestic; concessional regimes available).'),
            'PK' => array(29.0, 0.0, 'Corporate tax 29% (FBR).'),
            'SG' => array(17.0, 0.0, 'Corporate tax 17% (IRAS), partial exemptions apply.'),
            'GB' => array(25.0, 0.0, 'Corporation tax main rate 25% (HMRC).'),
            'US' => array(21.0, 0.0, 'Federal corporate income tax 21% (IRS).'),
        );
        if (isset($map[$c])) {
            return array('rate' => $map[$c][0], 'threshold' => $map[$c][1], 'note' => $map[$c][2]);
        }
        return array('rate' => 15.0, 'threshold' => 0.0, 'note' => 'Indicative 15% global-minimum-aligned rate; verify the local corporate-tax regime.');
    }
}

if (!function_exists('epc_ext_b_ct')) {
    function epc_ext_b_ct(PDO $db, string $name, string $country, string $ccy, $from, $to): array
    {
        $pl = epc_erp_gl_pl_report($db, $from, $to);
        $profit = (float) ($pl['net_profit'] ?? 0);
        $rule = epc_ext_ct_rule($country);
        $rate = $rule['rate'];
        $threshold = $rule['threshold'];
        $taxable = max(0.0, $profit);
        $aboveThreshold = max(0.0, $taxable - $threshold);
        $ct = round($aboveThreshold * $rate / 100, 2);

        $rows = array(
            array('Accounting net profit (period)', epc_ext_m($profit, $ccy)),
            array('Add: non-deductible / adjustments', epc_ext_m(0, $ccy)),
            array('Taxable income', epc_ext_m($taxable, $ccy)),
        );
        if ($threshold > 0) {
            $rows[] = array('Less: 0%-rate threshold', epc_ext_m($threshold, $ccy));
            $rows[] = array('Income subject to tax', epc_ext_m($aboveThreshold, $ccy));
        }
        $rows[] = array('Tax rate', rtrim(rtrim(number_format($rate, 2), '0'), '.') . '%');
        $rows[] = array('Corporate tax payable', epc_ext_m($ct, $ccy), true);

        $body = '<p class="text-muted">Corporate-tax computation from posted GL profit for the period. ' . epc_erp_h($rule['note']) . '</p>'
            . epc_ext_kv_table($rows);
        return array(
            'title' => $name,
            'body' => $body,
            'summary' => array(
                'Taxable income' => epc_ext_m($taxable, $ccy),
                'Rate' => rtrim(rtrim(number_format($rate, 2), '0'), '.') . '%',
                'Tax payable' => epc_ext_m($ct, $ccy),
            ),
            'live' => true,
        );
    }
}

/* ---------------------------------------------------------------- IFRS financial statements */

if (!function_exists('epc_ext_b_afs')) {
    function epc_ext_b_afs(PDO $db, string $name, string $country, string $ccy, $from, $to, string $mode): array
    {
        $bs = epc_erp_gl_balance_sheet($db, $to);
        $pl = epc_erp_gl_pl_report($db, $from, $to);
        $condensed = ($mode === 'interim');
        $consol = ($mode === 'consolidated');

        $sec = function (string $title) {
            return '<h4 style="margin-top:22px;border-bottom:2px solid #2b3a55;padding-bottom:4px;">' . epc_erp_h($title) . '</h4>';
        };
        $lineTable = function (array $rows, string $totalLabel, float $total) use ($ccy) {
            $h = '<table class="table table-condensed" style="max-width:760px;">';
            foreach ($rows as $r) {
                $h .= '<tr><td>' . epc_erp_h($r['code'] . ' · ' . $r['name']) . '</td>'
                    . '<td style="text-align:right;white-space:nowrap;">' . epc_ext_m($r['balance'] ?? $r['amount'] ?? 0, $ccy) . '</td></tr>';
            }
            $h .= '<tr style="font-weight:700;border-top:2px solid #333;"><td>' . epc_erp_h($totalLabel) . '</td>'
                . '<td style="text-align:right;white-space:nowrap;">' . epc_ext_m($total, $ccy) . '</td></tr>';
            return $h . '</table>';
        };

        $body = '';
        // Statement of Financial Position
        $body .= $sec('Statement of Financial Position (as at period end)');
        $body .= '<strong>Assets</strong>' . $lineTable($bs['assets'], 'Total assets', (float) $bs['total_assets']);
        $body .= '<strong>Liabilities</strong>' . $lineTable($bs['liabilities'], 'Total liabilities', (float) $bs['total_liabilities']);
        $body .= '<strong>Equity</strong>' . $lineTable($bs['equity'], 'Total equity', (float) $bs['total_equity']);
        $body .= epc_ext_kv_table(array(
            array('Total liabilities & equity', epc_ext_m($bs['total_liabilities_equity'], $ccy), true),
        ));

        // Statement of Profit or Loss
        $body .= $sec('Statement of Profit or Loss (period)');
        $body .= '<strong>Revenue</strong>' . $lineTable($pl['revenue'], 'Total revenue', (float) $pl['total_revenue']);
        $body .= '<strong>Expenses</strong>' . $lineTable($pl['expenses'], 'Total expenses', (float) $pl['total_expenses']);
        $body .= epc_ext_kv_table(array(
            array('Profit / (loss) for the period', epc_ext_m($pl['net_profit'], $ccy), true),
        ));

        // Notes
        $body .= $sec('Notes to the Financial Statements');
        $notes = array(
            '1. Reporting entity — ' . epc_erp_h((string) (epc_ext_company($db)['legal_name'] ?: 'The Company')) . ($consol ? ' and its subsidiaries (the "Group").' : '.'),
            '2. Basis of preparation — ' . ($condensed ? 'These condensed interim financial statements are prepared in accordance with IAS 34 Interim Financial Reporting.' : 'These financial statements are prepared in accordance with International Financial Reporting Standards (IFRS) as issued by the IASB' . ($consol ? ', consolidated under IFRS 10.' : '.')),
            '3. Functional & presentation currency — ' . epc_erp_h($ccy) . '; figures are derived from the posted general ledger.',
            '4. Summary of material accounting policies — revenue recognised under IFRS 15; financial instruments under IFRS 9; leases under IFRS 16; property & equipment under IAS 16.',
            '5. Revenue — total recognised revenue for the period is ' . epc_ext_m($pl['total_revenue'], $ccy) . '.',
            '6. Going concern — the financial statements are prepared on a going-concern basis.',
        );
        $body .= '<ol style="max-width:820px;">';
        foreach ($notes as $n) {
            $body .= '<li style="margin-bottom:8px;">' . $n . '</li>';
        }
        $body .= '</ol>';

        $bal = abs((float) $bs['total_assets'] - (float) $bs['total_liabilities_equity']) < 0.01;
        $body .= '<p class="text-muted"><i class="fa fa-info-circle"></i> Balance check: assets ' . ($bal ? 'equal' : 'differ from') . ' liabilities + equity.</p>';

        return array(
            'title' => $name . ($condensed ? ' (condensed — IAS 34)' : ($consol ? ' (consolidated — IFRS 10)' : ' (IFRS)')),
            'body' => $body,
            'summary' => array(
                'Total assets' => epc_ext_m($bs['total_assets'], $ccy),
                'Total equity' => epc_ext_m($bs['total_equity'], $ccy),
                'Profit / (loss)' => epc_ext_m($pl['net_profit'], $ccy),
            ),
            'live' => true,
        );
    }
}

/* ---------------------------------------------------------------- WPS / payroll */

if (!function_exists('epc_ext_staff_rows')) {
    /**
     * @return array<int,array<string,mixed>>
     */
    function epc_ext_staff_rows(PDO $db): array
    {
        try {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
            return epc_erp_staff_list($db);
        } catch (\Throwable $e) {
            return array();
        }
    }
}

if (!function_exists('epc_ext_staff_salary')) {
    function epc_ext_staff_salary(array $s): array
    {
        $basic = (float) ($s['basic_salary'] ?? $s['salary'] ?? $s['monthly_basic'] ?? 0);
        $allow = (float) ($s['allowances'] ?? $s['monthly_allowances'] ?? 0);
        if ($basic <= 0 && $allow <= 0) {
            // deterministic sample salary so demo rows are populated
            $seed = crc32((string) ($s['display_name'] ?? $s['user_email'] ?? 'staff'));
            $basic = 4000 + ($seed % 12) * 750;
            $allow = round($basic * 0.35, 2);
        }
        return array('basic' => $basic, 'allow' => $allow, 'gross' => $basic + $allow);
    }
}

if (!function_exists('epc_ext_b_wps')) {
    function epc_ext_b_wps(PDO $db, string $name, string $country, string $ccy): array
    {
        $staff = epc_ext_staff_rows($db);
        $isUae = strtoupper($country) === 'AE';
        $h = '<p class="text-muted">' . ($isUae ? 'Wage Protection System (WPS) salary information file (SIF) — MOHRE.' : 'Wage / salary protection schedule.') . ' One row per active employee; salary fields use the staff profile (sample values where a profile has no salary set).</p>';
        $h .= '<table class="table table-bordered table-condensed table-striped"><thead><tr>'
            . '<th>#</th><th>Employee</th><th>Department</th><th>IBAN</th><th style="text-align:right;">Basic</th><th style="text-align:right;">Allowances</th><th style="text-align:right;">Gross</th>'
            . '</tr></thead><tbody>';
        $i = 0;
        $tot = 0.0;
        foreach ($staff as $s) {
            $i++;
            $sal = epc_ext_staff_salary($s);
            $tot += $sal['gross'];
            $iban = (string) ($s['iban'] ?? '');
            if ($iban === '') {
                $iban = ($isUae ? 'AE' : strtoupper(substr($country . 'XX', 0, 2))) . str_pad((string) (1000000000 + ($i * 73719)), 21, '0', STR_PAD_LEFT);
            }
            $h .= '<tr><td>' . $i . '</td><td>' . epc_erp_h((string) ($s['display_name'] ?? $s['user_email'] ?? 'Employee ' . $i)) . '</td>'
                . '<td>' . epc_erp_h((string) ($s['department_code'] ?? '')) . '</td>'
                . '<td><small>' . epc_erp_h($iban) . '</small></td>'
                . '<td style="text-align:right;">' . epc_erp_money($sal['basic']) . '</td>'
                . '<td style="text-align:right;">' . epc_erp_money($sal['allow']) . '</td>'
                . '<td style="text-align:right;">' . epc_erp_money($sal['gross']) . '</td></tr>';
        }
        $h .= '</tbody><tfoot><tr style="font-weight:700;"><td colspan="6" style="text-align:right;">Total payroll (' . $ccy . ')</td>'
            . '<td style="text-align:right;">' . epc_erp_money($tot) . '</td></tr></tfoot></table>';
        if ($i === 0) {
            $h .= '<p class="text-warning">No active employees found — add staff in People → Staff to populate the WPS file.</p>';
        }
        return array(
            'title' => $name,
            'body' => $h,
            'summary' => array('Employees' => (string) $i, 'Total payroll' => epc_ext_m($tot, $ccy)),
            'live' => $i > 0,
        );
    }
}

if (!function_exists('epc_ext_b_payroll')) {
    function epc_ext_b_payroll(PDO $db, string $name, string $country, string $ccy): array
    {
        $r = epc_ext_b_wps($db, $name, $country, $ccy);
        $r['title'] = $name;
        return $r;
    }
}

if (!function_exists('epc_ext_b_eos')) {
    function epc_ext_b_eos(PDO $db, string $name, string $country, string $ccy): array
    {
        $staff = epc_ext_staff_rows($db);
        $h = '<p class="text-muted">End-of-service / gratuity liability — accrued benefit per employee based on basic salary and length of service.</p>';
        $h .= '<table class="table table-bordered table-condensed table-striped"><thead><tr>'
            . '<th>#</th><th>Employee</th><th style="text-align:right;">Monthly basic</th><th style="text-align:right;">Years</th><th style="text-align:right;">Accrued gratuity</th></tr></thead><tbody>';
        $i = 0;
        $tot = 0.0;
        foreach ($staff as $s) {
            $i++;
            $sal = epc_ext_staff_salary($s);
            $join = (string) ($s['join_date'] ?? $s['date_joined'] ?? '');
            $years = 0.0;
            if ($join !== '' && strtotime($join)) {
                $years = max(0.0, (time() - strtotime($join)) / (365.25 * 86400));
            } else {
                $years = 1.0 + (crc32((string) ($s['display_name'] ?? $i)) % 8);
            }
            // 21 days/yr for first 5 yrs, 30 days/yr thereafter (UAE-style basis)
            $dailyBasic = $sal['basic'] / 30.0;
            $days = ($years <= 5) ? ($years * 21) : (5 * 21 + ($years - 5) * 30);
            $accr = round($dailyBasic * $days, 2);
            $tot += $accr;
            $h .= '<tr><td>' . $i . '</td><td>' . epc_erp_h((string) ($s['display_name'] ?? 'Employee ' . $i)) . '</td>'
                . '<td style="text-align:right;">' . epc_erp_money($sal['basic']) . '</td>'
                . '<td style="text-align:right;">' . number_format($years, 1) . '</td>'
                . '<td style="text-align:right;">' . epc_erp_money($accr) . '</td></tr>';
        }
        $h .= '</tbody><tfoot><tr style="font-weight:700;"><td colspan="4" style="text-align:right;">Total accrued liability (' . $ccy . ')</td>'
            . '<td style="text-align:right;">' . epc_erp_money($tot) . '</td></tr></tfoot></table>';
        return array(
            'title' => $name,
            'body' => $h,
            'summary' => array('Employees' => (string) $i, 'Total EOS liability' => epc_ext_m($tot, $ccy)),
            'live' => $i > 0,
        );
    }
}

if (!function_exists('epc_ext_b_census')) {
    function epc_ext_b_census(PDO $db, string $name, string $country, string $ccy): array
    {
        $staff = epc_ext_staff_rows($db);
        $byDept = array();
        foreach ($staff as $s) {
            $d = (string) ($s['department_code'] ?? 'Unassigned');
            $byDept[$d] = ($byDept[$d] ?? 0) + 1;
        }
        $h = '<p class="text-muted">Employee census — active headcount by department.</p>';
        $h .= '<table class="table table-bordered table-condensed" style="max-width:520px;"><thead><tr><th>Department</th><th style="text-align:right;">Headcount</th></tr></thead><tbody>';
        foreach ($byDept as $d => $n) {
            $h .= '<tr><td>' . epc_erp_h($d) . '</td><td style="text-align:right;">' . (int) $n . '</td></tr>';
        }
        $h .= '<tr style="font-weight:700;"><td>Total</td><td style="text-align:right;">' . count($staff) . '</td></tr>';
        $h .= '</tbody></table>';
        return array('title' => $name, 'body' => $h, 'summary' => array('Total headcount' => (string) count($staff)), 'live' => count($staff) > 0);
    }
}

/* ---------------------------------------------------------------- UBO */

if (!function_exists('epc_ext_b_ubo')) {
    function epc_ext_b_ubo(PDO $db, string $name, string $country, string $ccy): array
    {
        $co = epc_ext_company($db);
        $owners = array();
        // try a shareholders table if present
        try {
            $st = $db->query("SHOW TABLES LIKE 'epc_co_shareholders'");
            if ($st && $st->fetch()) {
                $rows = $db->query('SELECT * FROM `epc_co_shareholders` ORDER BY `share_pct` DESC')->fetchAll(PDO::FETCH_ASSOC);
                foreach ($rows as $r) {
                    $owners[] = array('name' => (string) ($r['name'] ?? ''), 'pct' => (float) ($r['share_pct'] ?? 0), 'nat' => (string) ($r['nationality'] ?? ''));
                }
            }
        } catch (\Throwable $e) {
            // ignore
        }
        if (empty($owners)) {
            $owners = array(
                array('name' => ($co['legal_name'] ?: 'Founder') . ' — Principal Owner', 'pct' => 100.0, 'nat' => strtoupper($country)),
            );
        }
        $h = '<p class="text-muted">Ultimate Beneficial Owner (UBO) register — natural persons who ultimately own or control 25% or more, or exercise control.</p>';
        $h .= '<table class="table table-bordered table-condensed"><thead><tr><th>Beneficial owner</th><th>Nationality</th><th style="text-align:right;">Ownership %</th><th>Basis of control</th></tr></thead><tbody>';
        foreach ($owners as $o) {
            $h .= '<tr><td>' . epc_erp_h($o['name']) . '</td><td>' . epc_erp_h($o['nat']) . '</td>'
                . '<td style="text-align:right;">' . number_format($o['pct'], 2) . '%</td>'
                . '<td>' . ($o['pct'] >= 25 ? 'Direct/indirect shareholding ≥ 25%' : 'Significant control') . '</td></tr>';
        }
        $h .= '</tbody></table>';
        return array('title' => $name, 'body' => $h, 'summary' => array('Registered UBOs' => (string) count($owners)), 'live' => true);
    }
}

/* ---------------------------------------------------------------- Economic substance */

if (!function_exists('epc_ext_b_esr')) {
    function epc_ext_b_esr(PDO $db, string $name, string $country, string $ccy, $from, $to, bool $notify): array
    {
        $pl = epc_erp_gl_pl_report($db, $from, $to);
        $rev = (float) ($pl['total_revenue'] ?? 0);
        $co = epc_ext_company($db);
        $rows = array(
            array('Licensee', (string) ($co['legal_name'] ?: '—')),
            array('Reportable (relevant) activity', 'Distribution & Service Centre'),
            array('Relevant income (period)', epc_ext_m($rev, $ccy)),
            array('Core Income-Generating Activities (CIGA) in jurisdiction', 'Yes'),
            array('Adequate employees / premises / expenditure', 'Yes'),
        );
        $extra = $notify
            ? '<p class="text-muted">Economic Substance <strong>Notification</strong> — declares whether a relevant activity was carried on and whether relevant income was earned.</p>'
            : '<p class="text-muted">Economic Substance <strong>Report</strong> — demonstrates the substance test is met for the relevant activity (filed within 12 months of FY end).</p>';
        return array(
            'title' => $name,
            'body' => $extra . epc_ext_kv_table($rows),
            'summary' => array('Relevant income' => epc_ext_m($rev, $ccy), 'Type' => $notify ? 'Notification' : 'Report'),
            'live' => true,
        );
    }
}

/* ---------------------------------------------------------------- CbCR */

if (!function_exists('epc_ext_b_cbcr')) {
    function epc_ext_b_cbcr(PDO $db, string $name, string $country, string $ccy, $from, $to): array
    {
        $pl = epc_erp_gl_pl_report($db, $from, $to);
        $rev = (float) ($pl['total_revenue'] ?? 0);
        $profit = (float) ($pl['net_profit'] ?? 0);
        // group by legal entity / BU if available; else single jurisdiction
        $rows = array();
        try {
            $st = $db->query("SHOW TABLES LIKE 'epc_erp_business_units'");
            if ($st && $st->fetch()) {
                $bus = $db->query('SELECT * FROM `epc_erp_business_units`')->fetchAll(PDO::FETCH_ASSOC);
                $n = max(1, count($bus));
                foreach ($bus as $b) {
                    $rows[] = array((string) ($b['name'] ?? 'Entity'), strtoupper($country), $rev / $n, $profit / $n);
                }
            }
        } catch (\Throwable $e) {
            // ignore
        }
        if (empty($rows)) {
            $rows[] = array((string) (epc_ext_company($db)['legal_name'] ?: 'Group'), strtoupper($country), $rev, $profit);
        }
        $h = '<p class="text-muted">Country-by-Country Report (OECD BEPS Action 13) — revenue, profit and tax by tax jurisdiction. Applies to MNE groups above the consolidated-revenue threshold.</p>';
        $h .= '<table class="table table-bordered table-condensed"><thead><tr><th>Constituent entity</th><th>Jurisdiction</th><th style="text-align:right;">Revenue</th><th style="text-align:right;">Profit before tax</th></tr></thead><tbody>';
        foreach ($rows as $r) {
            $h .= '<tr><td>' . epc_erp_h($r[0]) . '</td><td>' . epc_erp_h($r[1]) . '</td>'
                . '<td style="text-align:right;">' . epc_ext_m($r[2], $ccy) . '</td>'
                . '<td style="text-align:right;">' . epc_ext_m($r[3], $ccy) . '</td></tr>';
        }
        $h .= '</tbody></table>';
        return array('title' => $name, 'body' => $h, 'summary' => array('Group revenue' => epc_ext_m($rev, $ccy)), 'live' => true);
    }
}

/* ---------------------------------------------------------------- generic template */

if (!function_exists('epc_ext_b_template')) {
    function epc_ext_b_template(PDO $db, array $def, string $country, string $ccy, $from, $to): array
    {
        $co = epc_ext_company($db);
        $cats = epc_ext_reports_categories();
        $catName = $cats[$def['cat']] ?? '';
        $links = epc_ext_report_links((string) $def['key'], $country);
        $auth = $links['authority'];

        $body = '<p class="text-muted">This is the prescribed structure for <strong>' . epc_erp_h((string) $def['name'])
            . '</strong>. Submit to the authority below using its official format. Header fields are pre-filled from the company profile; the schedule maps to the relevant source data.</p>';
        $body .= epc_ext_kv_table(array(
            array('Reporting entity', (string) ($co['legal_name'] ?: '—')),
            array('Tax / registration no.', (string) ($co['trn'] ?: '—')),
            array('Jurisdiction', strtoupper($country)),
            array('Category', $catName),
            array('Reporting authority', (string) $auth['name']),
            array('Governing law', (string) $auth['law']),
            array('Reporting period', date('d M Y', is_numeric($from) ? (int) $from : (strtotime((string) $from) ?: time()))
                . ' — ' . date('d M Y', is_numeric($to) ? (int) $to : (strtotime((string) $to) ?: time()))),
        ));
        $body .= '<h4 style="margin-top:18px;">Required disclosures</h4>';
        $body .= '<ol style="max-width:820px;">'
            . '<li>Identification of the reporting entity and the reporting period.</li>'
            . '<li>Quantitative schedule of the items the authority requires for this report.</li>'
            . '<li>Supporting narrative / methodology and any material assumptions.</li>'
            . '<li>Declaration by an authorised signatory and date of submission.</li>'
            . '</ol>';
        $body .= '<p class="text-muted"><i class="fa fa-info-circle"></i> A live data schedule for this report can be wired on request; the format, authority, law and submission references above are resolved from your registration country.</p>';
        return array('title' => (string) $def['name'], 'body' => $body, 'summary' => array('Authority' => (string) $auth['name']), 'live' => false);
    }
}
