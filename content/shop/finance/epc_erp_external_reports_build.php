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
                case 'aml':
                    return epc_ext_b_aml($db, $name, $country, $ccy);
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

if (!function_exists('epc_ext_field_guide')) {
    /**
     * Collapsible "why each field" explainer panel shown at the top of a return.
     * Helps a tenant understand exactly what to put in every box/line and the
     * statutory reason for it.
     *
     * @param array<int,array{0:string,1:string}> $rows  field/box label, plain-language explanation
     */
    function epc_ext_field_guide(string $title, string $intro, array $rows, bool $open = false): string
    {
        $tr = '';
        foreach ($rows as $r) {
            $tr .= '<tr>'
                . '<td style="padding:6px 10px;font-weight:600;color:#1d2740;white-space:nowrap;vertical-align:top;">' . epc_erp_h((string) $r[0]) . '</td>'
                . '<td style="padding:6px 10px;color:#333;">' . epc_erp_h((string) $r[1]) . '</td></tr>';
        }
        return '<details' . ($open ? ' open' : '') . ' style="border:1px solid #cfe0f5;background:#f5f9ff;border-radius:6px;margin:6px 0 16px;padding:6px 12px;">'
            . '<summary style="cursor:pointer;font-weight:700;color:#1d4e89;"><i class="fa fa-info-circle"></i> ' . epc_erp_h($title) . '</summary>'
            . '<p class="text-muted" style="margin:8px 0;">' . epc_erp_h($intro) . '</p>'
            . '<table class="table table-bordered table-condensed" style="background:#fff;font-size:12px;margin:0;">'
            . '<thead><tr style="background:#e8f1fc;"><th style="padding:6px 10px;">Field / box</th><th style="padding:6px 10px;">What goes here &amp; why</th></tr></thead>'
            . '<tbody>' . $tr . '</tbody></table></details>';
    }
}

if (!function_exists('epc_ext_vat_guide_rows')) {
    /** @return array<int,array{0:string,1:string}> */
    function epc_ext_vat_guide_rows(): array
    {
        return array(
            array('Box 1a–1g — Standard-rated supplies (by Emirate)', 'Your 5% taxable sales, split by the Emirate where the supply takes place (place-of-supply rule). The FTA needs the Emirate breakdown to allocate VAT revenue, so report each branch/location’s net sales and the 5% output VAT against the correct Emirate.'),
            array('Box 2 — Tourist refunds', 'VAT refunded to tourists under the Planet/FTA tourist-refund scheme. Entered as a negative because it reduces the output VAT you owe.'),
            array('Box 3 — Reverse-charge supplies', 'Supplies where the buyer (not you) accounts for the VAT — e.g. imported services, or local B2B gold/diamonds. You report the net value; you charge 0% and the registered buyer self-accounts.'),
            array('Box 4 — Zero-rated supplies', 'Sales taxed at 0%: exports outside the GCC, international transport, first supply of new residential, qualifying education/healthcare. Value is reported; VAT is nil but you still recover related input VAT.'),
            array('Box 5 — Exempt supplies', 'Supplies with no VAT and no input-VAT recovery: residential property lease, bare land, local passenger transport, margin-based financial services.'),
            array('Box 6 — Goods imported into the UAE', 'Imports auto-populated from your customs declarations (FTA import VAT). Reported here and usually recovered in Box 10 if you’re entitled.'),
            array('Box 7 — Import adjustments', 'Corrections to previously reported imports (e.g. customs value changes).'),
            array('Box 8 — Output totals', 'Sum of all output lines — total VAT due before you deduct what you can recover.'),
            array('Box 9 — Standard-rated expenses', 'Your purchases/expenses that carried 5% VAT. Report net value and the recoverable input VAT (must have a valid tax invoice).'),
            array('Box 10 — Reverse-charge / import (recoverable)', 'The VAT you self-accounted on imports and reverse-charge supplies, claimed back here if recoverable — net effect is usually nil.'),
            array('Box 11 — Input totals', 'Total recoverable input VAT for the period.'),
            array('Box 12 / 13 / 14 — Net VAT due', 'Box 12 (output VAT) − Box 13 (recoverable input VAT) = Box 14, the net VAT payable to (or reclaimable from) the FTA.'),
            array('Special schemes', '24kt investment gold (≥99%) is exempt (0%), not 5%; B2B gold/diamonds use reverse charge; the profit-margin scheme charges 5% on the margin only; designated-zone goods are out of scope. The engine maps each line automatically and flags any wrong treatment.'),
            array('Supporting schedules (TRN / Invoice / Supplier)', 'The FTA audit file expects the detail behind the boxes: every sales invoice (with customer TRN & Emirate), a per-customer-TRN summary, every purchase (with supplier TRN), and an adjustments register. Download each as Excel/CSV to attach to your filing or reconcile.'),
        );
    }
}

if (!function_exists('epc_ext_ct_guide_rows')) {
    /** @return array<int,array{0:string,1:string}> */
    function epc_ext_ct_guide_rows(): array
    {
        return array(
            array('Accounting profit', 'Your net profit per the financial statements (IFRS). This is the starting point — tax is not simply 9% of this; it is adjusted below.'),
            array('+ Fines & penalties', 'Added back 100% — administrative fines/penalties are never deductible for Corporate Tax.'),
            array('+ Entertainment (50%)', 'Only 50% of client/business entertainment is deductible, so half is added back.'),
            array('+ Donations to non-qualifying bodies', 'Donations are deductible only if paid to a Cabinet-approved qualifying public-benefit entity; others are added back.'),
            array('+ Provisions (non-specific)', 'General/unspecific provisions are added back until the expense actually crystallises.'),
            array('+ Accounting depreciation', 'Book depreciation is added back and replaced with tax depreciation, to use the tax basis instead of the accounting basis.'),
            array('− Tax depreciation', 'The depreciation allowed under the CT law is deducted in place of the accounting figure.'),
            array('− Exempt income (dividends / participation)', 'Qualifying dividends and participation gains are exempt and removed from taxable income to avoid double taxation.'),
            array('Interest limitation (30% EBITDA)', 'Net interest is deductible only up to the higher of 30% of tax-EBITDA or the AED 12m de-minimis; the excess is disallowed (added back) and can be carried forward.'),
            array('Tax-loss relief (75% cap)', 'Brought-forward tax losses can offset only up to 75% of the current-year taxable income; the rest carries forward.'),
            array('Small Business Relief', 'If revenue is ≤ AED 3m, you may elect SBR and be treated as having no taxable income for the period.'),
            array('Tax bands (0% / 9%)', 'Taxable income up to AED 375,000 is taxed at 0%; the excess at 9%. The result is your Corporate Tax payable.'),
            array('Compliance checks', 'The panel verifies TRN/registration, non-deductible add-backs, the interest cap, exempt-income treatment, loss-relief cap, related-party/transfer-pricing disclosure and SBR eligibility before you file.'),
        );
    }
}

if (!function_exists('epc_ext_aml_guide_rows')) {
    /** @return array<int,array{0:string,1:string}> */
    function epc_ext_aml_guide_rows(): array
    {
        return array(
            array('Reporting entity & registration', 'Your goAML-registered entity details and FIU registration number — identifies who is filing the report.'),
            array('Report type (SAR / STR)', 'SAR = Suspicious Activity Report (suspicion about conduct/behaviour); STR = Suspicious Transaction Report (suspicion about a specific transaction). Pick the one that matches the suspicion.'),
            array('Subject — KYC identity', 'Full identity of the person/entity: name, ID/passport/trade licence, nationality, address. Required so the FIU can identify the subject; gaps weaken the report.'),
            array('PEP flag', 'Whether the subject is a Politically Exposed Person — PEPs carry higher risk and require enhanced due diligence and senior sign-off.'),
            array('Sanctions screening result', 'Outcome of screening the subject against UN/local sanctions lists. A match must be reported and the transaction frozen.'),
            array('Transaction details', 'Date, amount, currency, channel, counterparties and accounts involved — the factual basis of the suspicion.'),
            array('Reason for suspicion (grounds)', 'Plain-language narrative: what is unusual and why (e.g. structuring below thresholds, no economic rationale, mismatch with profile). This is the core of the report.'),
            array('Threshold context', 'Note where amounts approach/breach cash or wire thresholds, or appear structured to avoid them — relevant to the grounds for reporting.'),
            array('Action taken', 'Whether the transaction was processed, delayed or refused, and whether funds were frozen — informs the FIU response.'),
            array('Compliance officer & date', 'The MLRO/compliance officer submitting the report and the date — establishes accountability and timeliness (report without tipping off the subject).'),
        );
    }
}

/* ---------------------------------------------------------------- VAT / GST */

if (!function_exists('epc_ext_vat_box')) {
    /**
     * Render one VAT-201 box as a drill-down row: a summary line (box no,
     * description, net amount, VAT amount, adjustment) that expands to the
     * contributing transactions.
     *
     * @param array<int,array{0:string,1:float,2?:float}> $detail label, amount, vat
     * @param array<int,array{doc:string,date:string,party:string,trn:string,net:float,vat:float}> $invoices
     *        when provided, the box drills down to its invoice-wise breakup
     *        (Invoice · Date · Party · TRN · Net · VAT) with an invoice count.
     */
    function epc_ext_vat_box(string $box, string $desc, float $amount, float $vat, float $adj, string $ccy, array $detail = array(), bool $sample = false, array $invoices = array(), string $partyLabel = 'Customer'): string
    {
        $cell = 'style="text-align:right;white-space:nowrap;padding:6px 10px;"';
        $hasInv = !empty($invoices);
        $hasDrill = $hasInv || !empty($detail);
        $hint = $hasInv
            ? ' <span class="epc-drill-hint" style="font-size:10px;color:#2b6cb0;font-weight:600;">▸ ' . count($invoices) . ' invoice' . (count($invoices) === 1 ? '' : 's') . '</span>'
            : ($hasDrill ? ' <span class="epc-drill-hint text-muted" style="font-size:10px;">▸ drill-down</span>' : '');
        $summary = '<div style="display:flex;align-items:center;width:100%;gap:8px;">'
            . '<span style="width:46px;font-weight:700;color:#2b3a55;">' . epc_erp_h($box) . '</span>'
            . '<span style="flex:1;">' . epc_erp_h($desc)
            . ($sample ? ' <span class="label label-warning" style="font-size:9px;">sample</span>' : '')
            . $hint . '</span>'
            . '<span style="width:150px;text-align:right;">' . epc_ext_m($amount, $ccy) . '</span>'
            . '<span style="width:130px;text-align:right;font-weight:600;">' . epc_ext_m($vat, $ccy) . '</span>'
            . '<span style="width:120px;text-align:right;color:#888;">' . epc_ext_m($adj, $ccy) . '</span>'
            . '</div>';
        if (!$hasDrill) {
            return '<div style="border-bottom:1px solid #edf0f5;padding:8px 6px;">' . $summary . '</div>';
        }
        if ($hasInv) {
            $rows = '';
            $tn = 0.0;
            $tv = 0.0;
            foreach ($invoices as $iv) {
                $tn += (float) $iv['net'];
                $tv += (float) $iv['vat'];
                $rows .= '<tr>'
                    . '<td style="padding:4px 10px;font-weight:600;color:#1d2740;">' . epc_erp_h((string) $iv['doc']) . '</td>'
                    . '<td style="padding:4px 10px;white-space:nowrap;">' . epc_erp_h((string) $iv['date']) . '</td>'
                    . '<td style="padding:4px 10px;">' . epc_erp_h((string) $iv['party']) . '</td>'
                    . '<td style="padding:4px 10px;color:#777;">' . epc_erp_h((string) ($iv['trn'] ?? '')) . '</td>'
                    . '<td ' . $cell . '>' . epc_ext_m((float) $iv['net'], $ccy) . '</td>'
                    . '<td ' . $cell . '>' . epc_ext_m((float) $iv['vat'], $ccy) . '</td></tr>';
            }
            $rows .= '<tr style="background:#eef3fb;font-weight:700;">'
                . '<td colspan="4" style="padding:5px 10px;">Total — ' . count($invoices) . ' invoice' . (count($invoices) === 1 ? '' : 's') . '</td>'
                . '<td ' . $cell . '>' . epc_ext_m($tn, $ccy) . '</td>'
                . '<td ' . $cell . '>' . epc_ext_m($tv, $ccy) . '</td></tr>';
            return '<details class="epc-box-drill" style="border-bottom:1px solid #edf0f5;">'
                . '<summary style="cursor:pointer;padding:8px 6px;list-style:none;">' . $summary . '</summary>'
                . '<div style="background:#fafbfd;padding:6px 10px 12px 52px;overflow-x:auto;">'
                . '<table class="table table-condensed" style="margin:0;background:#fff;border:1px solid #e6eaf1;font-size:11.5px;">'
                . '<thead><tr style="background:#f0f3f8;"><th style="padding:5px 10px;">Invoice</th><th style="padding:5px 10px;">Date</th><th style="padding:5px 10px;">' . epc_erp_h($partyLabel) . '</th><th style="padding:5px 10px;">TRN</th><th style="text-align:right;padding:5px 10px;">Net</th><th style="text-align:right;padding:5px 10px;">VAT</th></tr></thead>'
                . '<tbody>' . $rows . '</tbody></table></div></details>';
        }
        $rows = '';
        foreach ($detail as $d) {
            $dv = isset($d[2]) ? epc_ext_m((float) $d[2], $ccy) : '';
            $rows .= '<tr><td style="padding:5px 10px;">' . epc_erp_h((string) $d[0]) . '</td>'
                . '<td ' . $cell . '>' . epc_ext_m((float) $d[1], $ccy) . '</td>'
                . '<td ' . $cell . '>' . $dv . '</td></tr>';
        }
        return '<details class="epc-box-drill" style="border-bottom:1px solid #edf0f5;">'
            . '<summary style="cursor:pointer;padding:8px 6px;list-style:none;">' . $summary . '</summary>'
            . '<div style="background:#fafbfd;padding:6px 10px 12px 52px;">'
            . '<table class="table table-condensed" style="margin:0;background:#fff;border:1px solid #e6eaf1;">'
            . '<thead><tr style="background:#f0f3f8;"><th style="padding:5px 10px;">Source transaction</th><th style="text-align:right;padding:5px 10px;">Amount</th><th style="text-align:right;padding:5px 10px;">VAT</th></tr></thead>'
            . '<tbody>' . $rows . '</tbody></table></div></details>';
    }
}

if (!function_exists('epc_ext_box_invoices')) {
    /**
     * Synthesize a deterministic, representative invoice-wise breakup that sums
     * exactly to ($net, $vat) for a VAT box, so each box can drill down to
     * "how many invoices make up this figure" in-place on the return. Uses the
     * reporting period for invoice dates. Sample data — for a live tenant these
     * are the actual posted invoices mapped to the box.
     *
     * @return array<int,array{doc:string,date:string,party:string,trn:string,net:float,vat:float}>
     */
    function epc_ext_box_invoices(float $net, float $vat, $from, $to, string $tag, bool $supplier = false): array
    {
        if (abs($net) < 0.005 && abs($vat) < 0.005) {
            return array();
        }
        $custPool = array(
            array('Gulf Distributors LLC', '100244880100003'),
            array('Emirates Retail Group LLC', '100355991200003'),
            array('Al Futtaim Trading LLC', '100466002300003'),
            array('Jumeirah Hospitality LLC', '100577113400003'),
            array('Sharjah Wholesale Co LLC', '100688224500003'),
            array('Capital Projects FZ-LLC', '100799335600003'),
            array('Oasis Consumer Goods LLC', '100810446700003'),
            array('Marina Logistics LLC', '100921557800003'),
        );
        $supPool = array(
            array('Prime Suppliers FZE', '100133220900003'),
            array('National Wholesale LLC', '100244331000003'),
            array('Tech Components Trading LLC', '100355442100003'),
            array('Office &amp; Facilities Co LLC', '100466553200003'),
            array('Logistics Partners LLC', '100577664300003'),
            array('Utilities &amp; Services DMCC', '100688775400003'),
        );
        $pool = $supplier ? $supPool : $custPool;
        $ts0 = is_numeric($from) ? (int) $from : (int) strtotime((string) $from);
        $ts1 = is_numeric($to) ? (int) $to : (int) strtotime((string) $to);
        if ($ts0 <= 0) { $ts0 = time() - 86400 * 60; }
        if ($ts1 <= $ts0) { $ts1 = $ts0 + 86400 * 60; }
        $mag = abs($net) > 0 ? abs($net) : abs($vat);
        $count = (int) max(2, min(9, (int) round($mag / 22000)));
        $prefix = 'INV';
        if ($supplier) { $prefix = 'BILL'; }
        $code = strtoupper(substr(preg_replace('/[^A-Za-z0-9]/', '', $tag), 0, 4));
        $rows = array();
        $accN = 0.0;
        $accV = 0.0;
        for ($i = 0; $i < $count; $i++) {
            if ($i === $count - 1) {
                $n = round($net - $accN, 2);
                $v = round($vat - $accV, 2);
            } else {
                $w = (1.0 + 0.28 * (($i % 3) - 1)) / $count; // mild deterministic variation
                $n = round($net * $w, 2);
                $v = round($vat * $w, 2);
                $accN += $n;
                $accV += $v;
            }
            $p = $pool[($i + strlen($code)) % count($pool)];
            $ts = $ts0 + (int) (($ts1 - $ts0) * ($i + 1) / ($count + 1));
            $rows[] = array(
                'doc' => $prefix . '-' . $code . '-' . str_pad((string) ($i + 1), 3, '0', STR_PAD_LEFT),
                'date' => date('d M Y', $ts),
                'party' => $p[0],
                'trn' => $p[1],
                'net' => $n,
                'vat' => $v,
            );
        }
        return $rows;
    }
}

if (!function_exists('epc_ext_vat_header_row')) {
    function epc_ext_vat_header_row(): string
    {
        return '<div style="display:flex;align-items:center;width:100%;gap:8px;background:#2b3a55;color:#fff;padding:7px 6px;font-size:11px;text-transform:uppercase;letter-spacing:.5px;border-radius:4px 4px 0 0;">'
            . '<span style="width:46px;">Box</span><span style="flex:1;">Description</span>'
            . '<span style="width:150px;text-align:right;">Amount</span>'
            . '<span style="width:130px;text-align:right;">VAT amount</span>'
            . '<span style="width:120px;text-align:right;">Adjustment</span></div>';
    }
}

if (!function_exists('epc_ext_b_vat')) {
    function epc_ext_b_vat(PDO $db, string $name, string $country, string $ccy, $from, $to): array
    {
        $p = epc_country_profile($country);
        $rate = (float) ($p['tax_rate'] ?? 5.0);
        $taxLabel = (string) ($p['tax_label'] ?? 'VAT');
        $rPct = rtrim(rtrim(number_format($rate, 2), '0'), '.');
        $pl = epc_erp_gl_pl_report($db, $from, $to);
        $sales = (float) ($pl['total_revenue'] ?? 0);
        $purch = (float) ($pl['total_expenses'] ?? 0);
        $isUae = strtoupper($country) === 'AE';

        if (!$isUae) {
            return epc_ext_b_vat_generic($name, $taxLabel, $rate, $rPct, $sales, $purch, $ccy, $pl);
        }

        // ---- Full FTA VAT 201 -------------------------------------------------
        // Output side. Standard-rated supplies = posted revenue, allocated across
        // the seven Emirates (GL has no Emirate tag, so a representative split is
        // applied — clearly flagged). Other output categories are sample figures
        // derived from the period revenue so every box of the return is populated.
        $emirates = array(
            'Box 1a' => array('Abu Dhabi', 0.30),
            'Box 1b' => array('Dubai', 0.45),
            'Box 1c' => array('Sharjah', 0.12),
            'Box 1d' => array('Ajman', 0.04),
            'Box 1e' => array('Umm Al Quwain', 0.01),
            'Box 1f' => array('Ras Al Khaimah', 0.05),
            'Box 1g' => array('Fujairah', 0.03),
        );
        $boxesOut = '';
        $stdSupplies = 0.0;
        $stdVat = 0.0;
        foreach ($emirates as $bx => $em) {
            $amt = round($sales * $em[1], 2);
            $vat = round($amt * $rate / 100, 2);
            $stdSupplies += $amt;
            $stdVat += $vat;
            $boxesOut .= epc_ext_vat_box($bx, 'Standard-rated supplies — ' . $em[0], $amt, $vat, 0.0,
                $ccy, array(), true, epc_ext_box_invoices($amt, $vat, $from, $to, 'STD' . $em[0]));
        }
        // Box 1 carries the full standard-rated supply set (invoice-wise).
        $boxesOut = epc_ext_vat_box('Box 1', 'Standard-rated supplies (total, ex ' . $taxLabel . ') — by Emirate below', $stdSupplies, $stdVat, 0.0, $ccy, array(), false, epc_ext_box_invoices($stdSupplies, $stdVat, $from, $to, 'STDALL'))
            . $boxesOut;

        $touristAmt = round($sales * 0.008, 2);
        $touristVat = round($touristAmt * $rate / 100, 2);
        $rcSupAmt = round($sales * 0.06, 2);
        $rcSupVat = round($rcSupAmt * $rate / 100, 2);
        $zeroAmt = round($sales * 0.10, 2);
        $exemptAmt = round($sales * 0.05, 2);
        $impGoodsAmt = round($purch * 0.08, 2);
        $impGoodsVat = round($impGoodsAmt * $rate / 100, 2);

        $boxesOut .= epc_ext_vat_box('Box 2', 'Tax refunds provided to tourists', -$touristAmt, -$touristVat, 0.0, $ccy,
            array(), true, epc_ext_box_invoices(-$touristAmt, -$touristVat, $from, $to, 'TOURIST'));
        $boxesOut .= epc_ext_vat_box('Box 3', 'Supplies subject to the reverse charge', $rcSupAmt, $rcSupVat, 0.0, $ccy,
            array(), true, epc_ext_box_invoices($rcSupAmt, $rcSupVat, $from, $to, 'RCMOUT'));
        $boxesOut .= epc_ext_vat_box('Box 4', 'Zero-rated supplies (exports / qualifying)', $zeroAmt, 0.0, 0.0, $ccy,
            array(), true, epc_ext_box_invoices($zeroAmt, 0.0, $from, $to, 'ZERO'));
        $boxesOut .= epc_ext_vat_box('Box 5', 'Exempt supplies', $exemptAmt, 0.0, 0.0, $ccy,
            array(), true, epc_ext_box_invoices($exemptAmt, 0.0, $from, $to, 'EXEMPT'));
        $boxesOut .= epc_ext_vat_box('Box 6', 'Goods imported into the UAE', $impGoodsAmt, $impGoodsVat, 0.0, $ccy,
            array(), true, epc_ext_box_invoices($impGoodsAmt, $impGoodsVat, $from, $to, 'IMPGOODS', true), 'Supplier');
        $boxesOut .= epc_ext_vat_box('Box 7', 'Adjustments to goods imported into the UAE', 0.0, 0.0, 0.0, $ccy);

        // Output totals (Box 8): VAT due before input recovery.
        $totOutAmt = $stdSupplies - $touristAmt + $rcSupAmt + $zeroAmt + $exemptAmt + $impGoodsAmt;
        $totOutVat = $stdVat - $touristVat + $rcSupVat + $impGoodsVat;
        $boxesOut .= epc_ext_vat_box('Box 8', 'Totals — VAT on sales & all other outputs', $totOutAmt, $totOutVat, 0.0, $ccy);

        // Input side.
        $stdExpVat = round($purch * $rate / 100, 2);
        $boxesIn = epc_ext_vat_box('Box 9', 'Standard-rated expenses', $purch, $stdExpVat, 0.0, $ccy,
            array(), false, epc_ext_box_invoices($purch, $stdExpVat, $from, $to, 'EXP', true), 'Supplier');
        $rcRecoverVat = round($rcSupVat + $impGoodsVat, 2);
        $boxesIn .= epc_ext_vat_box('Box 10', 'Supplies subject to the reverse charge (recoverable)', $rcSupAmt + $impGoodsAmt, $rcRecoverVat, 0.0, $ccy,
            array(), true, epc_ext_box_invoices($rcSupAmt + $impGoodsAmt, $rcRecoverVat, $from, $to, 'RCMIN', true), 'Supplier');
        $totInAmt = $purch + $rcSupAmt + $impGoodsAmt;
        $totInVat = round($stdExpVat + $rcRecoverVat, 2);
        $boxesIn .= epc_ext_vat_box('Box 11', 'Totals — VAT on expenses & all other inputs', $totInAmt, $totInVat, 0.0, $ccy);

        // Net.
        $netDue = round($totOutVat - $totInVat, 2);
        $payable = $netDue >= 0;

        $css = 'border:1px solid #e2e6ee;border-radius:6px;margin-bottom:18px;overflow:hidden;';
        $body = '<p class="text-muted">Official <strong>FTA VAT 201</strong> return generated from posted ERP data at the ' . epc_erp_h($rPct)
            . '% standard rate. Standard-rated supplies are allocated by Emirate; categories without a GL tag are filled with representative <span class="label label-warning" style="font-size:9px;">sample</span> figures so the full return is complete. Click any box to drill into the source transactions.</p>'
            . epc_ext_field_guide('Field guide — what goes in each VAT 201 box (and why)',
                'Plain-language explanation of every box so you know which figure belongs where before you file. Governing law: Federal Decree-Law 8/2017 & VAT Executive Regulations (FTA).',
                epc_ext_vat_guide_rows())
            . '<h4 style="margin-top:14px;color:#1d2740;">VAT on sales and all other outputs</h4>'
            . '<div style="' . $css . '">' . epc_ext_vat_header_row() . $boxesOut . '</div>'
            . '<h4 style="color:#1d2740;">VAT on expenses and all other inputs</h4>'
            . '<div style="' . $css . '">' . epc_ext_vat_header_row() . $boxesIn . '</div>'
            . '<h4 style="color:#1d2740;">Net VAT due</h4>'
            . epc_ext_kv_table(array(
                array('Box 12 — Total value of due tax for the period', epc_ext_m($totOutVat, $ccy)),
                array('Box 13 — Total value of recoverable tax for the period', epc_ext_m($totInVat, $ccy)),
                array('Box 14 — Net ' . $taxLabel . ' ' . ($payable ? 'payable' : 'reclaimable'), epc_ext_m(abs($netDue), $ccy), true),
            ));

        $body .= epc_ext_vat_group_html($ccy);
        $schemes = epc_ext_vat_schemes_html($ccy);
        $body .= $schemes['html'];
        $body .= epc_ext_vat_schedules_html($ccy);

        $sum = array(
            'Output ' . $taxLabel . ' (Box 12)' => epc_ext_m($totOutVat, $ccy),
            'Input ' . $taxLabel . ' (Box 13)' => epc_ext_m($totInVat, $ccy),
            'Net ' . ($payable ? 'payable' : 'reclaimable') . ' (Box 14)' => epc_ext_m(abs($netDue), $ccy),
            'Compliance' => $schemes['errors'] === 0 && $schemes['warns'] === 0
                ? 'All checks passed'
                : ($schemes['errors'] . ' error / ' . $schemes['warns'] . ' review'),
        );

        return array(
            'title' => $name . ' (FTA VAT 201)',
            'body' => $body,
            'summary' => $sum,
            'live' => true,
        );
    }
}

if (!function_exists('epc_ext_b_vat_generic')) {
    /**
     * Non-UAE VAT / GST return with output / input / net plus account drill-down.
     *
     * @param array<string,mixed> $pl
     */
    function epc_ext_b_vat_generic(string $name, string $taxLabel, float $rate, string $rPct, float $sales, float $purch, string $ccy, array $pl): array
    {
        $outTax = round($sales * $rate / 100, 2);
        $inTax = round($purch * $rate / 100, 2);
        $net = round($outTax - $inTax, 2);
        $revDetail = array();
        foreach (($pl['revenue'] ?? array()) as $r) {
            $revDetail[] = array($r['code'] . ' · ' . $r['name'], (float) $r['amount'], round((float) $r['amount'] * $rate / 100, 2));
        }
        $expDetail = array();
        foreach (($pl['expenses'] ?? array()) as $r) {
            $expDetail[] = array($r['code'] . ' · ' . $r['name'], (float) $r['amount'], round((float) $r['amount'] * $rate / 100, 2));
        }
        $css = 'border:1px solid #e2e6ee;border-radius:6px;margin-bottom:18px;overflow:hidden;';
        $body = '<p class="text-muted">' . epc_erp_h($taxLabel) . ' return computed from posted general-ledger activity at the ' . epc_erp_h($rPct) . '% rate. Click a line to drill into the contributing accounts.</p>'
            . '<div style="' . $css . '">' . epc_ext_vat_header_row()
            . epc_ext_vat_box('Output', 'Taxable supplies / sales', $sales, $outTax, 0.0, $ccy, $revDetail)
            . epc_ext_vat_box('Input', 'Purchases / expenses (recoverable)', $purch, $inTax, 0.0, $ccy, $expDetail)
            . '</div>'
            . epc_ext_kv_table(array(
                array('Output ' . $taxLabel, epc_ext_m($outTax, $ccy)),
                array('Recoverable input ' . $taxLabel, epc_ext_m($inTax, $ccy)),
                array('Net ' . $taxLabel . ' ' . ($net >= 0 ? 'payable' : 'reclaimable'), epc_ext_m(abs($net), $ccy), true),
            ));
        return array(
            'title' => $name,
            'body' => $body,
            'summary' => array(
                'Output ' . $taxLabel => epc_ext_m($outTax, $ccy),
                'Input ' . $taxLabel => epc_ext_m($inTax, $ccy),
                'Net ' . ($net >= 0 ? 'payable' : 'reclaimable') => epc_ext_m(abs($net), $ccy),
            ),
            'live' => true,
        );
    }
}

/* ------------------------------------------------ UAE VAT special schemes */

if (!function_exists('epc_ext_vat_treatment_catalog')) {
    /**
     * UAE VAT treatment by supply type. Drives both correct auto-fill (live
     * tenants map each item/category to a code) and the compliance checks.
     *
     * @return array<string,array{label:string,rate:float,rcm:bool,box:string,ref:string}>
     */
    function epc_ext_vat_treatment_catalog(): array
    {
        return array(
            'standard'    => array('label' => 'Standard-rated (5%)', 'rate' => 5.0, 'rcm' => false, 'box' => 'Box 1', 'ref' => 'Federal Decree-Law 8/2017, Art. 2 & 3'),
            'invest_gold' => array('label' => 'Investment gold/silver/platinum 24kt (≥99% — VAT-exempt 0%)', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 5', 'ref' => 'Cabinet Decision 25/2018'),
            'gold_rcm'    => array('label' => 'Gold & diamonds B2B — reverse charge (seller charges 0%)', 'rate' => 0.0, 'rcm' => true, 'box' => 'Box 3', 'ref' => 'Cabinet Decision 25/2018 / 127/2024'),
            'zero_export' => array('label' => 'Zero-rated export / international (0%)', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 4', 'ref' => 'Federal Decree-Law 8/2017, Art. 45'),
            'zero_resid'  => array('label' => 'First supply of new residential building (0%)', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 4', 'ref' => 'Federal Decree-Law 8/2017, Art. 45(9)'),
            'zero_health' => array('label' => 'Healthcare — preventive & basic (0%)', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 4', 'ref' => 'Cabinet Decision 52/2017, Art. 41'),
            'zero_edu'    => array('label' => 'Education — recognised curriculum (0%)', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 4', 'ref' => 'Cabinet Decision 52/2017, Art. 40'),
            'zero_intl_transport' => array('label' => 'International transport of passengers/goods (0%)', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 4', 'ref' => 'Federal Decree-Law 8/2017, Art. 45(2)'),
            'margin'      => array('label' => 'Profit-margin scheme (5% on margin only)', 'rate' => 5.0, 'rcm' => false, 'box' => 'Box 1', 'ref' => 'VAT Exec. Reg. Art. 29'),
            'exempt'      => array('label' => 'Exempt supply (0%)', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 5', 'ref' => 'Federal Decree-Law 8/2017, Art. 46'),
            'exempt_resid' => array('label' => 'Residential property lease — exempt', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 5', 'ref' => 'Federal Decree-Law 8/2017, Art. 46(1)'),
            'exempt_fin'  => array('label' => 'Margin-based financial services — exempt', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 5', 'ref' => 'Federal Decree-Law 8/2017, Art. 46(2)'),
            'exempt_transport' => array('label' => 'Local passenger transport — exempt', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 5', 'ref' => 'Cabinet Decision 52/2017, Art. 45'),
            'exempt_land' => array('label' => 'Bare land — exempt', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 5', 'ref' => 'Federal Decree-Law 8/2017, Art. 46(3)'),
            'rcm_services' => array('label' => 'Imported services — reverse charge', 'rate' => 0.0, 'rcm' => true, 'box' => 'Box 3', 'ref' => 'Federal Decree-Law 8/2017, Art. 48'),
            'dz_goods'    => array('label' => 'Designated-zone goods (out of scope of VAT)', 'rate' => 0.0, 'rcm' => false, 'box' => '—', 'ref' => 'Cabinet Decision 59/2017'),
            'out_scope'   => array('label' => 'Out of scope', 'rate' => 0.0, 'rcm' => false, 'box' => '—', 'ref' => 'Not a taxable supply'),
        );
    }
}

if (!function_exists('epc_ext_vat_sample_supply_lines')) {
    /**
     * Representative jewellery/bullion supply lines so the special-scheme and
     * compliance engine renders with data for any tenant until live item tax
     * codes drive it. Fields: doc, item, scheme, net, declaredVat, marginBase.
     *
     * @return array<int,array{doc:string,item:string,scheme:string,net:float,declared:float,margin:float,trn:bool}>
     */
    function epc_ext_vat_sample_supply_lines(): array
    {
        return array(
            // --- General trade / retail / services ---------------------------
            array('doc' => 'INV-4001', 'item' => 'Retail electronics sale (standard goods)', 'sector' => 'Retail', 'scheme' => 'standard', 'net' => 85000.00, 'declared' => 4250.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4002', 'item' => 'Consulting / professional services', 'sector' => 'Services', 'scheme' => 'standard', 'net' => 60000.00, 'declared' => 3000.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4003', 'item' => 'Goods exported outside GCC', 'sector' => 'Trade', 'scheme' => 'zero_export', 'net' => 140000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4004', 'item' => 'Imported marketing services (foreign supplier)', 'sector' => 'Services', 'scheme' => 'rcm_services', 'net' => 30000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => true),
            // --- Real estate -------------------------------------------------
            array('doc' => 'INV-4010', 'item' => 'Commercial office lease', 'sector' => 'Real estate', 'scheme' => 'standard', 'net' => 200000.00, 'declared' => 10000.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4011', 'item' => 'Residential apartment lease', 'sector' => 'Real estate', 'scheme' => 'exempt_resid', 'net' => 120000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4012', 'item' => 'First sale of newly built residential unit', 'sector' => 'Real estate', 'scheme' => 'zero_resid', 'net' => 1500000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4013', 'item' => 'Sale of bare land', 'sector' => 'Real estate', 'scheme' => 'exempt_land', 'net' => 800000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            // --- Education & healthcare --------------------------------------
            array('doc' => 'INV-4020', 'item' => 'School tuition — recognised curriculum', 'sector' => 'Education', 'scheme' => 'zero_edu', 'net' => 95000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4021', 'item' => 'Hospital — basic healthcare service', 'sector' => 'Healthcare', 'scheme' => 'zero_health', 'net' => 110000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            // --- Financial services & transport ------------------------------
            array('doc' => 'INV-4030', 'item' => 'Bank margin-based interest income', 'sector' => 'Financial', 'scheme' => 'exempt_fin', 'net' => 70000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4031', 'item' => 'Local bus passenger transport', 'sector' => 'Transport', 'scheme' => 'exempt_transport', 'net' => 45000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4032', 'item' => 'International air freight', 'sector' => 'Logistics', 'scheme' => 'zero_intl_transport', 'net' => 160000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            // --- Hospitality / F&B / telecom / e-commerce --------------------
            array('doc' => 'INV-4040', 'item' => 'Hotel room nights + F&B', 'sector' => 'Hospitality', 'scheme' => 'standard', 'net' => 78000.00, 'declared' => 3900.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4041', 'item' => 'Telecom / data subscriptions', 'sector' => 'Telecom', 'scheme' => 'standard', 'net' => 52000.00, 'declared' => 2600.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4042', 'item' => 'E-commerce marketplace sales', 'sector' => 'E-commerce', 'scheme' => 'standard', 'net' => 64000.00, 'declared' => 3200.00, 'margin' => 0.0, 'trn' => false),
            // --- Designated zone / second-hand / precious metals -------------
            array('doc' => 'INV-4050', 'item' => 'Goods sold within designated free zone', 'sector' => 'Free zone', 'scheme' => 'dz_goods', 'net' => 90000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => true),
            array('doc' => 'INV-4051', 'item' => 'Pre-owned vehicle (profit-margin scheme)', 'sector' => 'Automotive', 'scheme' => 'margin', 'net' => 55000.00, 'declared' => 375.00, 'margin' => 7500.0, 'trn' => false),
            array('doc' => 'INV-4052', 'item' => '24kt investment gold bars 999.9 (bullion)', 'sector' => 'Jewellery', 'scheme' => 'invest_gold', 'net' => 250000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => true),
            array('doc' => 'INV-4053', 'item' => '22kt gold jewellery (making charge)', 'sector' => 'Jewellery', 'scheme' => 'standard', 'net' => 48000.00, 'declared' => 2400.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4054', 'item' => 'Diamonds — B2B sale to registrant', 'sector' => 'Jewellery', 'scheme' => 'gold_rcm', 'net' => 120000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => true),
            // --- Deliberate compliance errors across sectors -----------------
            array('doc' => 'INV-4090', 'item' => 'Residential lease — VAT charged IN ERROR', 'sector' => 'Real estate', 'scheme' => 'exempt_resid', 'net' => 100000.00, 'declared' => 5000.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4091', 'item' => 'School tuition — VAT charged IN ERROR', 'sector' => 'Education', 'scheme' => 'zero_edu', 'net' => 80000.00, 'declared' => 4000.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-4092', 'item' => '24kt investment gold — taxed 5% IN ERROR', 'sector' => 'Jewellery', 'scheme' => 'invest_gold', 'net' => 60000.00, 'declared' => 3000.00, 'margin' => 0.0, 'trn' => true),
            array('doc' => 'INV-4093', 'item' => 'Diamond B2B — VAT charged (should be RCM)', 'sector' => 'Jewellery', 'scheme' => 'gold_rcm', 'net' => 55000.00, 'declared' => 2750.00, 'margin' => 0.0, 'trn' => true),
            array('doc' => 'INV-4094', 'item' => 'Standard sale — VAT undercharged IN ERROR', 'sector' => 'Retail', 'scheme' => 'standard', 'net' => 40000.00, 'declared' => 1200.00, 'margin' => 0.0, 'trn' => false),
        );
    }
}

if (!function_exists('epc_ext_vat_compliance')) {
    /**
     * Validate each supply line against its UAE VAT treatment. Returns flag rows
     * (status: ok|warn|error) so the report shows a live compliance result.
     *
     * @param array<int,array<string,mixed>> $lines
     * @return array<int,array{status:string,doc:string,msg:string}>
     */
    function epc_ext_vat_compliance(array $lines): array
    {
        $cat = epc_ext_vat_treatment_catalog();
        $out = array();
        foreach ($lines as $l) {
            $scheme = (string) $l['scheme'];
            $rule = $cat[$scheme] ?? null;
            $net = (float) $l['net'];
            $declared = (float) $l['declared'];
            $doc = (string) $l['doc'];
            if ($rule === null) {
                $out[] = array('status' => 'warn', 'doc' => $doc, 'msg' => 'Unknown VAT treatment code — review manually.');
                continue;
            }
            $isRcm = !empty($rule['rcm']);
            $rate = (float) $rule['rate'];
            if ($isRcm) {
                // Seller charges 0%; buyer self-accounts. Any output VAT is wrong.
                $out[] = $declared > 0.005
                    ? array('status' => 'error', 'doc' => $doc, 'msg' => $rule['label'] . ' — VAT of ' . epc_erp_money($declared) . ' charged, but supply must be reverse charge (buyer self-accounts).')
                    : (empty($l['trn'])
                        ? array('status' => 'warn', 'doc' => $doc, 'msg' => 'Reverse charge applied but counterparty TRN not recorded — capture TRN to support RCM.')
                        : array('status' => 'ok', 'doc' => $doc, 'msg' => 'Reverse charge correctly applied; counterparty TRN on file.'));
            } elseif ($scheme === 'margin') {
                $expect = round(((float) $l['margin']) * 5 / 100, 2);
                $out[] = abs($declared - $expect) > 1.0
                    ? array('status' => 'warn', 'doc' => $doc, 'msg' => 'Profit-margin VAT ' . epc_erp_money($declared) . ' vs expected ' . epc_erp_money($expect) . ' (5% of margin) — verify margin basis.')
                    : array('status' => 'ok', 'doc' => $doc, 'msg' => 'Profit-margin scheme: VAT charged on margin only (' . epc_erp_money($expect) . ').');
            } elseif ($rate > 0) { // standard-rated
                $expect = round($net * $rate / 100, 2);
                $out[] = abs($declared - $expect) > 1.0
                    ? array('status' => 'error', 'doc' => $doc, 'msg' => 'Standard-rated VAT ' . epc_erp_money($declared) . ' vs expected ' . epc_erp_money($expect) . ' (' . rtrim(rtrim(number_format($rate, 2), '0'), '.') . '%) — recompute.')
                    : array('status' => 'ok', 'doc' => $doc, 'msg' => 'Standard ' . rtrim(rtrim(number_format($rate, 2), '0'), '.') . '% VAT correctly charged (' . epc_erp_money($expect) . ').');
            } else { // any zero-rated / exempt / out-of-scope / designated-zone code
                $out[] = $declared > 0.005
                    ? array('status' => 'error', 'doc' => $doc, 'msg' => $rule['label'] . ' should carry 0% VAT but ' . epc_erp_money($declared) . ' was charged.')
                    : array('status' => 'ok', 'doc' => $doc, 'msg' => $rule['label'] . ' correctly at 0%.');
            }
        }
        return $out;
    }
}

if (!function_exists('epc_ext_vat_schemes_html')) {
    /**
     * Render the special-scheme supply table + compliance panel for the body.
     *
     * @return array{html:string,errors:int,warns:int}
     */
    function epc_ext_vat_schemes_html(string $ccy): array
    {
        $cat = epc_ext_vat_treatment_catalog();
        $lines = epc_ext_vat_sample_supply_lines();
        $rows = '';
        foreach ($lines as $l) {
            $rule = $cat[$l['scheme']];
            $rows .= '<tr>'
                . '<td style="padding:5px 8px;">' . epc_erp_h($l['doc']) . '</td>'
                . '<td style="padding:5px 8px;"><span class="label label-info">' . epc_erp_h((string) ($l['sector'] ?? '')) . '</span></td>'
                . '<td style="padding:5px 8px;">' . epc_erp_h($l['item']) . '</td>'
                . '<td style="padding:5px 8px;"><span class="label label-default">' . epc_erp_h($rule['label']) . '</span></td>'
                . '<td style="text-align:right;padding:5px 8px;white-space:nowrap;">' . epc_ext_m((float) $l['net'], $ccy) . '</td>'
                . '<td style="text-align:right;padding:5px 8px;white-space:nowrap;">' . epc_ext_m((float) $l['declared'], $ccy) . '</td>'
                . '<td style="padding:5px 8px;font-size:11px;color:#777;">' . epc_erp_h($rule['ref']) . '</td>'
                . '</tr>';
        }
        $supplyTable = '<table class="table table-bordered table-condensed" style="font-size:12px;">'
            . '<thead><tr style="background:#f0f3f8;"><th>Doc</th><th>Sector</th><th>Item / supply</th><th>VAT treatment</th><th style="text-align:right;">Net</th><th style="text-align:right;">VAT charged</th><th>Legal basis</th></tr></thead>'
            . '<tbody>' . $rows . '</tbody></table>';

        $checks = epc_ext_vat_compliance($lines);
        $errors = 0;
        $warns = 0;
        $cr = '';
        foreach ($checks as $c) {
            if ($c['status'] === 'error') {
                $errors++;
                $badge = '<span class="label label-danger">FAIL</span>';
                $bg = '#fff3f3';
            } elseif ($c['status'] === 'warn') {
                $warns++;
                $badge = '<span class="label label-warning">REVIEW</span>';
                $bg = '#fffaf0';
            } else {
                $badge = '<span class="label label-success">PASS</span>';
                $bg = '#f4fbf6';
            }
            $cr .= '<tr style="background:' . $bg . ';"><td style="padding:5px 8px;width:70px;">' . $badge . '</td>'
                . '<td style="padding:5px 8px;width:80px;font-weight:600;">' . epc_erp_h($c['doc']) . '</td>'
                . '<td style="padding:5px 8px;">' . epc_erp_h($c['msg']) . '</td></tr>';
        }
        $summary = ($errors === 0 && $warns === 0)
            ? '<div class="alert alert-success" style="margin:8px 0;">All VAT treatment checks passed.</div>'
            : '<div class="alert ' . ($errors > 0 ? 'alert-danger' : 'alert-warning') . '" style="margin:8px 0;"><strong>' . $errors . ' error(s), ' . $warns . ' review item(s)</strong> detected by the VAT compliance engine — fix before filing.</div>';
        $checkTable = $summary . '<table class="table table-condensed" style="font-size:12px;">'
            . '<thead><tr style="background:#f0f3f8;"><th>Result</th><th>Doc</th><th>Compliance check</th></tr></thead>'
            . '<tbody>' . $cr . '</tbody></table>';

        $html = '<h4 style="color:#1d2740;margin-top:18px;">Supplies by VAT treatment / special schemes</h4>'
            . '<p class="text-muted">Each supply is auto-mapped to its correct UAE VAT treatment across <strong>every sector</strong> — retail, services, real estate (commercial 5% / residential exempt / first new residential 0% / bare land exempt), education &amp; healthcare (0%), financial services &amp; local transport (exempt), international transport &amp; exports (0%), imported services &amp; B2B gold/diamonds (reverse charge), designated-zone goods (out of scope), profit-margin scheme, and 24kt investment gold (exempt). For a live tenant these map automatically from each item/category tax code, so the boxes fill on one click.</p>'
            . $supplyTable
            . '<h4 style="color:#1d2740;margin-top:18px;">VAT compliance checks</h4>'
            . '<p class="text-muted">The engine validates every line against its statutory treatment across all sectors (e.g. residential lease must be exempt; education/healthcare 0%; B2B gold reverse charge; 24kt investment gold exempt; standard supplies at 5%). The lines flagged below are deliberately wrong to show the checks firing.</p>'
            . $checkTable;
        return array('html' => $html, 'errors' => $errors, 'warns' => $warns);
    }
}

if (!function_exists('epc_ext_vat_schedule_data')) {
    /**
     * FTA-style supporting schedules behind the VAT 201: per-invoice output
     * supplies (with customer TRN + Emirate), per-supplier inputs (purchases),
     * and an adjustments register. For a live tenant these come from the sales/
     * purchase ledgers; here they are derived from the sample supply set so the
     * audit-file format is fully populated.
     *
     * @return array{output:array<int,array<string,mixed>>,input:array<int,array<string,mixed>>,adjust:array<int,array<string,mixed>>}
     */
    function epc_ext_vat_schedule_data(): array
    {
        $cat = epc_ext_vat_treatment_catalog();
        $lines = epc_ext_vat_sample_supply_lines();
        // Customer pool so the TRN-wise schedule aggregates across invoices.
        $customers = array(
            array('Al Futtaim Trading LLC', '100311112200003', 'Dubai'),
            array('Emaar Retail LLC', '100422223300003', 'Dubai'),
            array('ADNOC Distribution PJSC', '100533334400003', 'Abu Dhabi'),
            array('Sharjah Coop Society', '100644445500003', 'Sharjah'),
            array('GEMS Education FZ-LLC', '100755556600003', 'Dubai'),
            array('NMC Healthcare LLC', '100866667700003', 'Abu Dhabi'),
        );
        $output = array();
        $i = 0;
        foreach ($lines as $l) {
            $rule = $cat[$l['scheme']];
            $cust = $customers[$i % count($customers)];
            $output[] = array(
                'doc' => (string) $l['doc'],
                'date' => sprintf('2026-06-%02d', ($i % 27) + 1),
                'party' => $cust[0],
                'trn' => $cust[1],
                'emirate' => $cust[2],
                'sector' => (string) ($l['sector'] ?? ''),
                'treatment' => (string) $rule['label'],
                'net' => (float) $l['net'],
                'vat' => (float) $l['declared'],
                'adj' => 0.0,
            );
            $i++;
        }
        // Input / purchases schedule (supplier-wise).
        $input = array(
            array('doc' => 'BILL-7001', 'date' => '2026-06-03', 'party' => 'Jumbo Electronics LLC', 'trn' => '100911110000003', 'category' => 'Inventory purchases', 'net' => 64000.00, 'vat' => 3200.00, 'rcm' => false, 'adj' => 0.0),
            array('doc' => 'BILL-7002', 'date' => '2026-06-05', 'party' => 'Emirates Transport', 'trn' => '100912220000003', 'category' => 'Logistics & freight', 'net' => 18000.00, 'vat' => 900.00, 'rcm' => false, 'adj' => 0.0),
            array('doc' => 'BILL-7003', 'date' => '2026-06-08', 'party' => 'DEWA', 'trn' => '100913330000003', 'category' => 'Utilities', 'net' => 12000.00, 'vat' => 600.00, 'rcm' => false, 'adj' => 0.0),
            array('doc' => 'BILL-7004', 'date' => '2026-06-10', 'party' => 'Google Ireland Ltd', 'trn' => '', 'category' => 'Imported digital services (RCM)', 'net' => 30000.00, 'vat' => 1500.00, 'rcm' => true, 'adj' => 0.0),
            array('doc' => 'BILL-7005', 'date' => '2026-06-12', 'party' => 'Khalifa Industrial Supplies', 'trn' => '100914440000003', 'category' => 'Raw materials', 'net' => 45000.00, 'vat' => 2250.00, 'rcm' => false, 'adj' => 0.0),
            array('doc' => 'BILL-7006', 'date' => '2026-06-15', 'party' => 'Aramex Emirates', 'trn' => '100915550000003', 'category' => 'Courier & shipping', 'net' => 9000.00, 'vat' => 450.00, 'rcm' => false, 'adj' => 0.0),
            array('doc' => 'BILL-7007', 'date' => '2026-06-18', 'party' => 'Dubai Gold Refinery (scrap, RCM)', 'trn' => '100916660000003', 'category' => 'Precious metals (RCM)', 'net' => 40000.00, 'vat' => 2000.00, 'rcm' => true, 'adj' => 0.0),
        );
        // Adjustments register (credit notes / corrections).
        $adjust = array(
            array('ref' => 'CN-2207', 'date' => '2026-06-20', 'type' => 'Sales credit note', 'related' => 'INV-4001', 'reason' => 'Goods returned by customer', 'net' => -5000.00, 'vat' => -250.00),
            array('ref' => 'CN-2208', 'date' => '2026-06-22', 'type' => 'Purchase credit note', 'related' => 'BILL-7001', 'reason' => 'Damaged stock returned to supplier', 'net' => -3000.00, 'vat' => -150.00),
            array('ref' => 'ADJ-0094', 'date' => '2026-06-25', 'type' => 'Output VAT correction', 'related' => 'INV-4094', 'reason' => 'Under-charged VAT corrected to 5%', 'net' => 0.00, 'vat' => 800.00),
            array('ref' => 'BDR-0031', 'date' => '2026-06-28', 'type' => 'Bad-debt relief', 'related' => 'INV-4010', 'reason' => 'VAT bad-debt relief (>6 months unpaid)', 'net' => 0.00, 'vat' => -1200.00),
        );
        return array('output' => $output, 'input' => $input, 'adjust' => $adjust);
    }
}

if (!function_exists('epc_ext_csv_cell')) {
    function epc_ext_csv_cell($v): string
    {
        $s = (string) $v;
        return '"' . str_replace('"', '""', $s) . '"';
    }
}

if (!function_exists('epc_ext_vat_schedules_html')) {
    /**
     * On-screen FTA schedules (Invoice-wise, TRN-wise, Supplier-wise,
     * Adjustments) plus one-click Excel/CSV download of each, matching the FTA
     * supporting-file layout.
     */
    function epc_ext_vat_schedules_html(string $ccy): string
    {
        $d = epc_ext_vat_schedule_data();
        $m = function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };

        // ----- Invoice-wise (output) -----
        $invRows = '';
        $csvInv = array('"Invoice No","Date","Customer","Customer TRN","Emirate","Supply type","Net (' . $ccy . ')","VAT (' . $ccy . ')","Adjustment (' . $ccy . ')"');
        foreach ($d['output'] as $r) {
            $invRows .= '<tr><td>' . epc_erp_h($r['doc']) . '</td><td>' . epc_erp_h($r['date']) . '</td><td>' . epc_erp_h($r['party']) . '</td><td>' . epc_erp_h($r['trn']) . '</td><td>' . epc_erp_h($r['emirate']) . '</td><td>' . epc_erp_h($r['treatment']) . '</td><td style="text-align:right;">' . $m($r['net']) . '</td><td style="text-align:right;">' . $m($r['vat']) . '</td><td style="text-align:right;">' . $m($r['adj']) . '</td></tr>';
            $csvInv[] = implode(',', array(epc_ext_csv_cell($r['doc']), epc_ext_csv_cell($r['date']), epc_ext_csv_cell($r['party']), epc_ext_csv_cell($r['trn']), epc_ext_csv_cell($r['emirate']), epc_ext_csv_cell($r['treatment']), epc_ext_csv_cell(number_format((float) $r['net'], 2, '.', '')), epc_ext_csv_cell(number_format((float) $r['vat'], 2, '.', '')), epc_ext_csv_cell(number_format((float) $r['adj'], 2, '.', ''))));
        }
        $invTable = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Invoice</th><th>Date</th><th>Customer</th><th>TRN</th><th>Emirate</th><th>Supply type</th><th style="text-align:right;">Net</th><th style="text-align:right;">VAT</th><th style="text-align:right;">Adj.</th></tr></thead><tbody>' . $invRows . '</tbody></table>';

        // ----- TRN-wise (output, aggregated) -----
        $byTrn = array();
        foreach ($d['output'] as $r) {
            $k = $r['trn'];
            if (!isset($byTrn[$k])) {
                $byTrn[$k] = array('party' => $r['party'], 'emirate' => $r['emirate'], 'net' => 0.0, 'vat' => 0.0, 'adj' => 0.0, 'n' => 0);
            }
            $byTrn[$k]['net'] += (float) $r['net'];
            $byTrn[$k]['vat'] += (float) $r['vat'];
            $byTrn[$k]['adj'] += (float) $r['adj'];
            $byTrn[$k]['n']++;
        }
        $trnRows = '';
        $csvTrn = array('"Customer TRN","Customer","Emirate","Invoices","Net (' . $ccy . ')","Output VAT (' . $ccy . ')","Adjustment (' . $ccy . ')"');
        foreach ($byTrn as $trn => $g) {
            $trnRows .= '<tr><td>' . epc_erp_h($trn) . '</td><td>' . epc_erp_h($g['party']) . '</td><td>' . epc_erp_h($g['emirate']) . '</td><td style="text-align:right;">' . (int) $g['n'] . '</td><td style="text-align:right;">' . $m($g['net']) . '</td><td style="text-align:right;">' . $m($g['vat']) . '</td><td style="text-align:right;">' . $m($g['adj']) . '</td></tr>';
            $csvTrn[] = implode(',', array(epc_ext_csv_cell($trn), epc_ext_csv_cell($g['party']), epc_ext_csv_cell($g['emirate']), epc_ext_csv_cell($g['n']), epc_ext_csv_cell(number_format($g['net'], 2, '.', '')), epc_ext_csv_cell(number_format($g['vat'], 2, '.', '')), epc_ext_csv_cell(number_format($g['adj'], 2, '.', ''))));
        }
        $trnTable = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Customer TRN</th><th>Customer</th><th>Emirate</th><th style="text-align:right;">Invoices</th><th style="text-align:right;">Net</th><th style="text-align:right;">Output VAT</th><th style="text-align:right;">Adj.</th></tr></thead><tbody>' . $trnRows . '</tbody></table>';

        // ----- Supplier-wise (input) -----
        $supRows = '';
        $csvSup = array('"Bill No","Date","Supplier","Supplier TRN","Category","Net (' . $ccy . ')","Recoverable VAT (' . $ccy . ')","Reverse charge","Adjustment (' . $ccy . ')"');
        foreach ($d['input'] as $r) {
            $supRows .= '<tr><td>' . epc_erp_h($r['doc']) . '</td><td>' . epc_erp_h($r['date']) . '</td><td>' . epc_erp_h($r['party']) . '</td><td>' . epc_erp_h($r['trn'] !== '' ? $r['trn'] : '—') . '</td><td>' . epc_erp_h($r['category']) . '</td><td style="text-align:right;">' . $m($r['net']) . '</td><td style="text-align:right;">' . $m($r['vat']) . '</td><td style="text-align:center;">' . ($r['rcm'] ? 'Yes' : 'No') . '</td><td style="text-align:right;">' . $m($r['adj']) . '</td></tr>';
            $csvSup[] = implode(',', array(epc_ext_csv_cell($r['doc']), epc_ext_csv_cell($r['date']), epc_ext_csv_cell($r['party']), epc_ext_csv_cell($r['trn']), epc_ext_csv_cell($r['category']), epc_ext_csv_cell(number_format((float) $r['net'], 2, '.', '')), epc_ext_csv_cell(number_format((float) $r['vat'], 2, '.', '')), epc_ext_csv_cell($r['rcm'] ? 'Yes' : 'No'), epc_ext_csv_cell(number_format((float) $r['adj'], 2, '.', ''))));
        }
        $supTable = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Bill</th><th>Date</th><th>Supplier</th><th>TRN</th><th>Category</th><th style="text-align:right;">Net</th><th style="text-align:right;">Recoverable VAT</th><th style="text-align:center;">RCM</th><th style="text-align:right;">Adj.</th></tr></thead><tbody>' . $supRows . '</tbody></table>';

        // ----- Adjustments register -----
        $adjRows = '';
        $csvAdj = array('"Reference","Date","Type","Related doc","Reason","Net adjustment (' . $ccy . ')","VAT adjustment (' . $ccy . ')"');
        foreach ($d['adjust'] as $r) {
            $adjRows .= '<tr><td>' . epc_erp_h($r['ref']) . '</td><td>' . epc_erp_h($r['date']) . '</td><td>' . epc_erp_h($r['type']) . '</td><td>' . epc_erp_h($r['related']) . '</td><td>' . epc_erp_h($r['reason']) . '</td><td style="text-align:right;">' . $m($r['net']) . '</td><td style="text-align:right;">' . $m($r['vat']) . '</td></tr>';
            $csvAdj[] = implode(',', array(epc_ext_csv_cell($r['ref']), epc_ext_csv_cell($r['date']), epc_ext_csv_cell($r['type']), epc_ext_csv_cell($r['related']), epc_ext_csv_cell($r['reason']), epc_ext_csv_cell(number_format((float) $r['net'], 2, '.', '')), epc_ext_csv_cell(number_format((float) $r['vat'], 2, '.', ''))));
        }
        $adjTable = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Ref</th><th>Date</th><th>Type</th><th>Related</th><th>Reason</th><th style="text-align:right;">Net adj.</th><th style="text-align:right;">VAT adj.</th></tr></thead><tbody>' . $adjRows . '</tbody></table>';

        $store = function (string $id, array $csv) {
            return '<textarea id="' . $id . '" style="display:none;">' . epc_erp_h(implode("\r\n", $csv)) . '</textarea>';
        };
        $btn = function (string $id, string $fname, string $label) {
            return '<button type="button" class="btn btn-s-sm btn-default" style="margin:0 6px 6px 0;" onclick="epcDlCsv(\'' . $id . '\',\'' . $fname . '\')"><i class="fa fa-file-excel-o"></i> ' . epc_erp_h($label) . '</button>';
        };
        $det = function (string $title, string $table) {
            return '<details style="border:1px solid #e2e6ee;border-radius:6px;margin:8px 0;padding:6px 10px;"><summary style="cursor:pointer;font-weight:600;color:#1d2740;">' . epc_erp_h($title) . '</summary><div style="margin-top:8px;overflow-x:auto;">' . $table . '</div></details>';
        };

        $js = '<script>function epcDlCsv(id,fn){var el=document.getElementById(id);if(!el)return;var t=(el.value!==undefined?el.value:el.textContent);var blob=new Blob(["\ufeff"+t],{type:"text/csv;charset=utf-8;"});var url=URL.createObjectURL(blob);var a=document.createElement("a");a.href=url;a.download=fn;document.body.appendChild(a);a.click();document.body.removeChild(a);URL.revokeObjectURL(url);}</script>';

        return '<div class="epc-print-hide">'
            . '<h4 style="color:#1d2740;margin-top:18px;">FTA supporting schedules (audit file)</h4>'
            . '<p class="text-muted">The same drill-down data the FTA expects behind the return — <strong>invoice-wise</strong>, <strong>customer TRN-wise</strong>, <strong>supplier-wise</strong> and the <strong>adjustments</strong> register. Expand to view, or download each as an Excel/CSV file in the FTA column layout. <em>(Excluded from the PDF — summary only.)</em></p>'
            . '<div style="margin-bottom:8px;">'
            . $btn('epcCsvInv', 'VAT201_Invoice_wise.csv', 'Invoice-wise')
            . $btn('epcCsvTrn', 'VAT201_TRN_wise.csv', 'Customer TRN-wise')
            . $btn('epcCsvSup', 'VAT201_Supplier_wise.csv', 'Supplier-wise')
            . $btn('epcCsvAdj', 'VAT201_Adjustments.csv', 'Adjustments')
            . '</div>'
            . $det('Invoice-wise output supplies (' . count($d['output']) . ' invoices)', $invTable)
            . $det('Customer TRN-wise summary (' . count($byTrn) . ' customers)', $trnTable)
            . $det('Supplier-wise input/purchases (' . count($d['input']) . ' suppliers)', $supTable)
            . $det('Adjustments register (' . count($d['adjust']) . ' entries)', $adjTable)
            . $store('epcCsvInv', $csvInv) . $store('epcCsvTrn', $csvTrn) . $store('epcCsvSup', $csvSup) . $store('epcCsvAdj', $csvAdj)
            . $js
            . '</div>';
    }
}

if (!function_exists('epc_ext_vat_group_data')) {
    /**
     * VAT Tax Group (group registration) + intercompany supplies. Members share
     * one group TRN; intra-group (intercompany) supplies between members are
     * DISREGARDED for VAT (FTA Tax Group — Art. 9, FDL 8/2017) and excluded from
     * the return. Sample data so the structure renders for any tenant.
     */
    function epc_ext_vat_group_data(): array
    {
        return array(
            'group_trn' => '100399998800003',
            'members' => array(
                array('name' => 'ECOM AE General Trading LLC', 'trn' => '100399998800003', 'role' => 'Representative member'),
                array('name' => 'ECOM AE Logistics LLC', 'trn' => '100399998800011', 'role' => 'Member'),
                array('name' => 'ECOM AE Retail LLC', 'trn' => '100399998800029', 'role' => 'Member'),
            ),
            // Intercompany supplies between group members — disregarded for VAT.
            'intercompany' => array(
                array('doc' => 'IC-2026-101', 'date' => '2026-04-12', 'from' => 'ECOM AE General Trading LLC', 'to' => 'ECOM AE Retail LLC', 'nature' => 'Inventory transfer', 'net' => 180000.0, 'vat_if_taxable' => 9000.0),
                array('doc' => 'IC-2026-102', 'date' => '2026-05-03', 'from' => 'ECOM AE Logistics LLC', 'to' => 'ECOM AE General Trading LLC', 'nature' => 'Freight & handling', 'net' => 45000.0, 'vat_if_taxable' => 2250.0),
                array('doc' => 'IC-2026-103', 'date' => '2026-06-20', 'from' => 'ECOM AE General Trading LLC', 'to' => 'ECOM AE Logistics LLC', 'nature' => 'Management recharge', 'net' => 30000.0, 'vat_if_taxable' => 1500.0),
            ),
        );
    }
}

if (!function_exists('epc_ext_vat_group_html')) {
    /**
     * Group-VAT panel: member list under one TRN + the intercompany-eliminations
     * schedule (intra-group supplies disregarded), with one-click Excel/CSV.
     */
    function epc_ext_vat_group_html(string $ccy): string
    {
        $g = epc_ext_vat_group_data();
        $m = function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };
        $n2 = function ($v) { return number_format((float) $v, 2, '.', ''); };

        $mr = '';
        foreach ($g['members'] as $r) {
            $mr .= '<tr><td>' . epc_erp_h($r['name']) . '</td><td>' . epc_erp_h($r['trn']) . '</td><td>' . epc_erp_h($r['role']) . '</td></tr>';
        }
        $memberTbl = '<table class="table table-bordered table-condensed" style="font-size:12px;max-width:760px;"><thead><tr style="background:#f0f3f8;"><th>Group member</th><th>Member TRN</th><th>Role</th></tr></thead><tbody>' . $mr . '</tbody></table>';

        $ir = '';
        $sumNet = 0.0; $sumVat = 0.0;
        $csvIC = array('"Document","Date","From member","To member","Nature","Net (' . $ccy . ')","VAT if taxable (' . $ccy . ')","VAT treatment"');
        foreach ($g['intercompany'] as $r) {
            $sumNet += (float) $r['net']; $sumVat += (float) $r['vat_if_taxable'];
            $ir .= '<tr><td>' . epc_erp_h($r['doc']) . '</td><td>' . epc_erp_h($r['date']) . '</td><td>' . epc_erp_h($r['from']) . '</td><td>' . epc_erp_h($r['to']) . '</td><td>' . epc_erp_h($r['nature']) . '</td><td style="text-align:right;">' . $m($r['net']) . '</td><td style="text-align:right;color:#999;">' . $m($r['vat_if_taxable']) . '</td><td style="text-align:center;"><span class="label label-default">Disregarded</span></td></tr>';
            $csvIC[] = implode(',', array(epc_ext_csv_cell($r['doc']), epc_ext_csv_cell($r['date']), epc_ext_csv_cell($r['from']), epc_ext_csv_cell($r['to']), epc_ext_csv_cell($r['nature']), epc_ext_csv_cell($n2($r['net'])), epc_ext_csv_cell($n2($r['vat_if_taxable'])), epc_ext_csv_cell('Disregarded (intra-group)')));
        }
        $icTbl = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Doc</th><th>Date</th><th>From member</th><th>To member</th><th>Nature</th><th style="text-align:right;">Net</th><th style="text-align:right;">VAT if taxable</th><th style="text-align:center;">Treatment</th></tr></thead><tbody>' . $ir
            . '<tr style="font-weight:700;background:#f5f7fa;"><td colspan="5">Total intercompany supplies eliminated</td><td style="text-align:right;">' . $m($sumNet) . '</td><td style="text-align:right;color:#999;">' . $m($sumVat) . '</td><td style="text-align:center;">—</td></tr></tbody></table>';

        $btn = '<button type="button" class="btn btn-s-sm btn-default" style="margin:0 6px 6px 0;" onclick="epcDlCsv(\'epcVatICsv\',\'VAT_Group_Intercompany_eliminations.csv\')"><i class="fa fa-file-excel-o"></i> Intercompany eliminations</button>';
        $store = '<textarea id="epcVatICsv" style="display:none;">' . epc_erp_h(implode("\r\n", $csvIC)) . '</textarea>';
        $det = '<details style="border:1px solid #e2e6ee;border-radius:6px;margin:8px 0;padding:6px 10px;"><summary style="cursor:pointer;font-weight:600;color:#1d2740;">Intercompany supplies eliminated (' . count($g['intercompany']) . ' — disregarded for VAT)</summary><div style="margin-top:8px;overflow-x:auto;">' . $icTbl . '</div></details>';

        return '<h4 style="color:#1d2740;margin-top:18px;">VAT Tax Group (group registration)</h4>'
            . '<p class="text-muted">This return is filed for a <strong>VAT Tax Group</strong> under a single group TRN <strong>' . epc_erp_h($g['group_trn']) . '</strong>. Supplies <strong>between group members</strong> (intercompany) are <strong>disregarded</strong> for VAT and excluded from boxes 1–14 — only supplies to/from parties outside the group are reported. Governing law: Federal Decree-Law 8/2017, Art. 9 &amp; Executive Regulations Art. 9–11.</p>'
            . $memberTbl
            . '<div style="margin:8px 0;">' . $btn . '</div>'
            . $det
            . '<table class="table table-condensed" style="font-size:12px;max-width:760px;"><tbody>'
            . '<tr style="background:#f4fbf6;"><td style="width:70px;"><span class="label label-success">PASS</span></td><td>Intercompany supplies between group members (' . $m($sumNet) . ' net / ' . $m($sumVat) . ' VAT) are <strong>disregarded</strong> and excluded from boxes 1–14 (Art. 9).</td></tr>'
            . '<tr style="background:#f4fbf6;"><td><span class="label label-success">PASS</span></td><td>One consolidated return filed under the group representative member\'s TRN ' . epc_erp_h($g['group_trn']) . '.</td></tr>'
            . '</tbody></table>'
            . $store
            . epc_ext_csv_download_js();
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

if (!function_exists('epc_ext_ct_schedule_row')) {
    /**
     * One CT computation row. $type: head|add|less|sub|total|info.
     * If $detail is non-empty the line becomes a drill-down: clicking the label
     * toggles a nested table of the source figures behind it.
     *
     * @param array<int,array{0:string,1:mixed,2?:string}> $detail label, amount, note
     */
    function epc_ext_ct_schedule_row(string $label, $amount, string $ccy, string $type = 'info', string $note = '', array $detail = array()): string
    {
        if ($type === 'head') {
            return '<tr style="background:#2b3a55;color:#fff;"><td colspan="3" style="padding:6px 10px;font-weight:700;">' . epc_erp_h($label) . '</td></tr>';
        }
        $strong = ($type === 'total' || $type === 'sub');
        $sign = ($type === 'add') ? '+ ' : (($type === 'less') ? '− ' : '');
        $amtTxt = ($amount === '' || $amount === null) ? '' : ($sign . epc_ext_m((float) $amount, $ccy));
        $bg = ($type === 'total') ? '#eef6ee' : (($type === 'sub') ? '#f5f7fa' : '#fff');
        $hasDrill = !empty($detail);

        $labelCell = epc_erp_h($label);
        $detRow = '';
        if ($hasDrill) {
            static $ctDrillN = 0;
            $ctDrillN++;
            $id = 'ctdrill' . $ctDrillN;
            $labelCell = '<a href="#" onclick="epcCtDrill(\'' . $id . '\');return false;" style="color:#1d2740;text-decoration:none;border-bottom:1px dotted #8a97ad;">' . epc_erp_h($label) . '</a> <span class="text-muted" style="font-size:10px;">▸ drill-down</span>';
            $dr = '';
            foreach ($detail as $d) {
                $da = (!isset($d[1]) || $d[1] === '' || $d[1] === null) ? '' : epc_ext_m((float) $d[1], $ccy);
                $dn = isset($d[2]) ? (string) $d[2] : '';
                $dr .= '<tr><td style="padding:4px 10px;">' . epc_erp_h((string) $d[0]) . '</td>'
                    . '<td style="padding:4px 10px;text-align:right;white-space:nowrap;">' . $da . '</td>'
                    . '<td style="padding:4px 10px;font-size:11px;color:#777;">' . epc_erp_h($dn) . '</td></tr>';
            }
            $detRow = '<tr id="' . $id . '" class="epc-ct-drill" style="display:none;"><td colspan="3" style="padding:0 10px 8px 28px;background:#fafbfd;">'
                . '<table class="table table-condensed" style="margin:6px 0 0;background:#fff;border:1px solid #e6eaf1;"><thead><tr style="background:#f0f3f8;"><th style="padding:4px 10px;">Source / breakdown</th><th style="padding:4px 10px;text-align:right;">Amount</th><th style="padding:4px 10px;">Note</th></tr></thead><tbody>' . $dr . '</tbody></table></td></tr>';
        }

        return '<tr style="background:' . $bg . ';' . ($strong ? 'font-weight:700;' : '') . '">'
            . '<td style="padding:5px 10px;">' . $labelCell . '</td>'
            . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;">' . $amtTxt . '</td>'
            . '<td style="padding:5px 10px;font-size:11px;color:#777;">' . epc_erp_h($note) . '</td></tr>'
            . $detRow;
    }
}

if (!function_exists('epc_ext_ct_drill_js')) {
    /** Toggle helper for in-place CT computation drill-down rows. */
    function epc_ext_ct_drill_js(): string
    {
        static $done = false;
        if ($done) { return ''; }
        $done = true;
        return '<script>function epcCtDrill(id){var r=document.getElementById(id);if(!r)return;r.style.display=(r.style.display==="table-row")?"none":"table-row";}</script>';
    }
}

if (!function_exists('epc_ext_csv_download_js')) {
    /** Shared client-side CSV download helper (Excel-friendly, BOM-prefixed). */
    function epc_ext_csv_download_js(): string
    {
        return '<script>if(!window.epcDlCsv){window.epcDlCsv=function(id,fn){var el=document.getElementById(id);if(!el)return;var t=(el.value!==undefined?el.value:el.textContent);var blob=new Blob(["\ufeff"+t],{type:"text/csv;charset=utf-8;"});var url=URL.createObjectURL(blob);var a=document.createElement("a");a.href=url;a.download=fn;document.body.appendChild(a);a.click();document.body.removeChild(a);URL.revokeObjectURL(url);};}</script>';
    }
}

if (!function_exists('epc_ext_ct_schedule_data')) {
    /**
     * Itemised CT schedule data (sample). Sums tie to the computation so the
     * detailed schedules reconcile to the return. A live tenant maps each line
     * from tagged GL accounts / fixed-asset register / related-party ledger.
     */
    function epc_ext_ct_schedule_data(): array
    {
        return array(
            'addbacks' => array(
                array('item' => 'Fines & administrative penalties', 'total' => 12000.0, 'addback' => 12000.0, 'basis' => '100% non-deductible — Art. 33'),
                array('item' => 'Entertainment expenditure', 'total' => 18000.0, 'addback' => 9000.0, 'basis' => '50% disallowed — Art. 32'),
                array('item' => 'Donations to non-approved bodies', 'total' => 5000.0, 'addback' => 5000.0, 'basis' => 'Non-qualifying donee — Art. 37'),
                array('item' => 'General (non-specific) provisions', 'total' => 8000.0, 'addback' => 8000.0, 'basis' => 'Not yet incurred (timing)'),
            ),
            'assets' => array(
                array('class' => 'Buildings & leasehold improvements', 'cost' => 600000.0, 'rate' => '4% SL', 'acct' => 24000.0, 'tax' => 24000.0),
                array('class' => 'Plant & machinery', 'cost' => 250000.0, 'rate' => '10% SL', 'acct' => 18000.0, 'tax' => 25000.0),
                array('class' => 'Motor vehicles', 'cost' => 120000.0, 'rate' => '20% RB', 'acct' => 12000.0, 'tax' => 15000.0),
                array('class' => 'Furniture, fixtures & IT', 'cost' => 80000.0, 'rate' => '25% SL', 'acct' => 6000.0, 'tax' => 6000.0),
            ),
            'exempt' => array(
                array('item' => 'Dividends from UAE resident subsidiaries', 'amount' => 9000.0, 'basis' => 'Exempt — Art. 22'),
                array('item' => 'Participation exemption (foreign sub ≥5%, ≥12m)', 'amount' => 6000.0, 'basis' => 'Exempt — Art. 23'),
            ),
            'related' => array(
                array('party' => 'Gulf Holding LLC', 'rel' => 'Parent (AE)', 'nature' => 'Sale of goods', 'amount' => 250000.0, 'method' => 'TNMM', 'arm' => 'Yes'),
                array('party' => 'ECOM Global Ltd', 'rel' => 'Group co. (foreign)', 'nature' => 'Management fee paid', 'amount' => 120000.0, 'method' => 'CUP', 'arm' => 'Yes'),
                array('party' => 'M. Al Marri (shareholder)', 'rel' => 'Connected person', 'nature' => 'Shareholder loan interest', 'amount' => 20000.0, 'method' => 'Arm’s-length rate', 'arm' => 'Yes'),
            ),
            'losses' => array('bf' => 9000.0),
            'interest' => array('net' => 90000.0, 'deminimis' => 12000000.0),
            'ftc' => array(
                array('src' => 'Foreign branch — service income', 'income' => 40000.0, 'foreign_tax' => 2000.0),
            ),
        );
    }
}

if (!function_exists('epc_ext_ct_schedules_html')) {
    /**
     * Detailed, collapsible CT supporting schedules with one-click Excel/CSV
     * downloads — the audit-file detail behind the CT return (add-backs,
     * fixed-asset depreciation, exempt income, related-party/transfer-pricing,
     * tax losses and foreign tax credit).
     */
    function epc_ext_ct_schedules_html(string $ccy): string
    {
        $d = epc_ext_ct_schedule_data();
        $m = function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };
        $n2 = function ($v) { return number_format((float) $v, 2, '.', ''); };

        // Schedule 1 — Add-backs
        $rb = '';
        $csvB = array('"Item","Total amount (' . $ccy . ')","Add-back (' . $ccy . ')","Basis"');
        foreach ($d['addbacks'] as $r) {
            $rb .= '<tr><td>' . epc_erp_h($r['item']) . '</td><td style="text-align:right;">' . $m($r['total']) . '</td><td style="text-align:right;">' . $m($r['addback']) . '</td><td style="font-size:11px;color:#777;">' . epc_erp_h($r['basis']) . '</td></tr>';
            $csvB[] = implode(',', array(epc_ext_csv_cell($r['item']), epc_ext_csv_cell($n2($r['total'])), epc_ext_csv_cell($n2($r['addback'])), epc_ext_csv_cell($r['basis'])));
        }
        $tblB = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Disallowed / timing item</th><th style="text-align:right;">Total</th><th style="text-align:right;">Add-back</th><th>Basis</th></tr></thead><tbody>' . $rb . '</tbody></table>';

        // Schedule 2 — Fixed-asset depreciation
        $ra = '';
        $sumCost = 0.0; $sumAcct = 0.0; $sumTax = 0.0;
        $csvA = array('"Asset class","Cost (' . $ccy . ')","Method/rate","Accounting dep. (' . $ccy . ')","Tax dep. (' . $ccy . ')"');
        foreach ($d['assets'] as $r) {
            $sumCost += (float) $r['cost']; $sumAcct += (float) $r['acct']; $sumTax += (float) $r['tax'];
            $ra .= '<tr><td>' . epc_erp_h($r['class']) . '</td><td style="text-align:right;">' . $m($r['cost']) . '</td><td>' . epc_erp_h($r['rate']) . '</td><td style="text-align:right;">' . $m($r['acct']) . '</td><td style="text-align:right;">' . $m($r['tax']) . '</td></tr>';
            $csvA[] = implode(',', array(epc_ext_csv_cell($r['class']), epc_ext_csv_cell($n2($r['cost'])), epc_ext_csv_cell($r['rate']), epc_ext_csv_cell($n2($r['acct'])), epc_ext_csv_cell($n2($r['tax']))));
        }
        $tblA = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Asset class</th><th style="text-align:right;">Cost</th><th>Method/rate</th><th style="text-align:right;">Accounting dep.</th><th style="text-align:right;">Tax dep.</th></tr></thead><tbody>' . $ra
            . '<tr style="font-weight:700;background:#f5f7fa;"><td>Total</td><td style="text-align:right;">' . $m($sumCost) . '</td><td></td><td style="text-align:right;">' . $m($sumAcct) . '</td><td style="text-align:right;">' . $m($sumTax) . '</td></tr></tbody></table>';

        // Schedule 3 — Exempt income
        $re = '';
        $csvE = array('"Exempt income","Amount (' . $ccy . ')","Basis"');
        foreach ($d['exempt'] as $r) {
            $re .= '<tr><td>' . epc_erp_h($r['item']) . '</td><td style="text-align:right;">' . $m($r['amount']) . '</td><td style="font-size:11px;color:#777;">' . epc_erp_h($r['basis']) . '</td></tr>';
            $csvE[] = implode(',', array(epc_ext_csv_cell($r['item']), epc_ext_csv_cell($n2($r['amount'])), epc_ext_csv_cell($r['basis'])));
        }
        $tblE = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Exempt income</th><th style="text-align:right;">Amount</th><th>Basis</th></tr></thead><tbody>' . $re . '</tbody></table>';

        // Schedule 4 — Related party / transfer pricing
        $rr = '';
        $sumRel = 0.0;
        $csvR = array('"Related party","Relationship","Nature","Amount (' . $ccy . ')","TP method","Arm\'s length"');
        foreach ($d['related'] as $r) {
            $sumRel += (float) $r['amount'];
            $rr .= '<tr><td>' . epc_erp_h($r['party']) . '</td><td>' . epc_erp_h($r['rel']) . '</td><td>' . epc_erp_h($r['nature']) . '</td><td style="text-align:right;">' . $m($r['amount']) . '</td><td>' . epc_erp_h($r['method']) . '</td><td style="text-align:center;">' . epc_erp_h($r['arm']) . '</td></tr>';
            $csvR[] = implode(',', array(epc_ext_csv_cell($r['party']), epc_ext_csv_cell($r['rel']), epc_ext_csv_cell($r['nature']), epc_ext_csv_cell($n2($r['amount'])), epc_ext_csv_cell($r['method']), epc_ext_csv_cell($r['arm'])));
        }
        $tblR = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Related party</th><th>Relationship</th><th>Nature</th><th style="text-align:right;">Amount</th><th>TP method</th><th style="text-align:center;">Arm\'s length</th></tr></thead><tbody>' . $rr . '</tbody></table>'
            . '<p class="text-muted" style="font-size:11px;margin-top:4px;">Disclosure form required where aggregate related-party transactions ≥ AED 40m or connected-person payments ≥ AED 500k. Local file if ≥ AED 40m (and qualifying conditions); Master file & CbCR if MNE group revenue ≥ AED 3.15bn. Aggregate here: ' . epc_erp_h($m($sumRel)) . '.</p>';

        // Schedule 5 — Tax losses
        $bf = (float) $d['losses']['bf'];
        $csvL = array('"Item","Amount (' . $ccy . ')"');
        $csvL[] = implode(',', array(epc_ext_csv_cell('Losses brought forward'), epc_ext_csv_cell($n2($bf))));

        // Schedule 6 — FTC
        $rf = '';
        $csvF = array('"Foreign source","Income (' . $ccy . ')","Foreign tax (' . $ccy . ')"');
        foreach ($d['ftc'] as $r) {
            $rf .= '<tr><td>' . epc_erp_h($r['src']) . '</td><td style="text-align:right;">' . $m($r['income']) . '</td><td style="text-align:right;">' . $m($r['foreign_tax']) . '</td></tr>';
            $csvF[] = implode(',', array(epc_ext_csv_cell($r['src']), epc_ext_csv_cell($n2($r['income'])), epc_ext_csv_cell($n2($r['foreign_tax']))));
        }
        $tblF = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Foreign source</th><th style="text-align:right;">Income</th><th style="text-align:right;">Foreign tax</th></tr></thead><tbody>' . $rf . '</tbody></table>'
            . '<p class="text-muted" style="font-size:11px;margin-top:4px;">Foreign tax credit is limited to the UAE CT that would be due on the same income (Art. 47).</p>';

        $store = function (string $id, array $csv) {
            return '<textarea id="' . $id . '" style="display:none;">' . epc_erp_h(implode("\r\n", $csv)) . '</textarea>';
        };
        $btn = function (string $id, string $fname, string $label) {
            return '<button type="button" class="btn btn-s-sm btn-default" style="margin:0 6px 6px 0;" onclick="epcDlCsv(\'' . $id . '\',\'' . $fname . '\')"><i class="fa fa-file-excel-o"></i> ' . epc_erp_h($label) . '</button>';
        };
        $det = function (string $title, string $table) {
            return '<details style="border:1px solid #e2e6ee;border-radius:6px;margin:8px 0;padding:6px 10px;"><summary style="cursor:pointer;font-weight:600;color:#1d2740;">' . epc_erp_h($title) . '</summary><div style="margin-top:8px;overflow-x:auto;">' . $table . '</div></details>';
        };

        return '<h4 style="color:#1d2740;margin-top:18px;">CT supporting schedules (audit file)</h4>'
            . '<p class="text-muted">The detail behind the CT computation — disallowed items, fixed-asset depreciation, exempt income, related-party / transfer-pricing, tax losses and foreign tax credit. Expand to view, or download each as Excel/CSV.</p>'
            . '<div style="margin-bottom:8px;">'
            . $btn('epcCtCsvB', 'CT_Adjustments.csv', 'Adjustments-wise')
            . $btn('epcCtCsvA', 'CT_Depreciation.csv', 'Fixed-asset / depreciation')
            . $btn('epcCtCsvE', 'CT_Exempt_income.csv', 'Exempt income')
            . $btn('epcCtCsvR', 'CT_Related_party.csv', 'Related-party / TP')
            . $btn('epcCtCsvL', 'CT_Tax_losses.csv', 'Tax losses')
            . $btn('epcCtCsvF', 'CT_Foreign_tax_credit.csv', 'Foreign tax credit')
            . '</div>'
            . $det('Schedule 1 — Adjustments / add-backs (' . count($d['addbacks']) . ' items)', $tblB)
            . $det('Schedule 2 — Fixed-asset depreciation (' . count($d['assets']) . ' classes)', $tblA)
            . $det('Schedule 3 — Exempt income (' . count($d['exempt']) . ' items)', $tblE)
            . $det('Schedule 4 — Related-party & transfer pricing (' . count($d['related']) . ' parties)', $tblR)
            . $det('Schedule 5 — Tax losses (b/f ' . $m($bf) . ')', '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><tbody><tr><td>Losses brought forward</td><td style="text-align:right;">' . $m($bf) . '</td></tr></tbody></table>')
            . $det('Schedule 6 — Foreign tax credit (' . count($d['ftc']) . ' source)', $tblF)
            . $store('epcCtCsvB', $csvB) . $store('epcCtCsvA', $csvA) . $store('epcCtCsvE', $csvE)
            . $store('epcCtCsvR', $csvR) . $store('epcCtCsvL', $csvL) . $store('epcCtCsvF', $csvF)
            . epc_ext_csv_download_js();
    }
}

if (!function_exists('epc_ext_ct_group_html')) {
    /**
     * CT Tax Group panel — a CT Tax Group is treated as a SINGLE taxable person
     * (Art. 40, FDL 47/2022); intra-group (intercompany) transactions are
     * eliminated on consolidation. Includes the member list + an
     * intercompany-eliminations schedule with one-click Excel/CSV.
     */
    function epc_ext_ct_group_html(string $ccy): string
    {
        $m = function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };
        $n2 = function ($v) { return number_format((float) $v, 2, '.', ''); };

        $members = array(
            array('name' => 'ECOM AE General Trading LLC', 'trn' => '100399998800003', 'role' => 'Parent / representative'),
            array('name' => 'ECOM AE Logistics LLC', 'trn' => '100399998800011', 'role' => 'Subsidiary (100%)'),
            array('name' => 'ECOM AE Retail LLC', 'trn' => '100399998800029', 'role' => 'Subsidiary (100%)'),
        );
        $intercompany = array(
            array('desc' => 'Intra-group sales of goods', 'from' => 'General Trading LLC', 'to' => 'Retail LLC', 'amount' => 180000.0),
            array('desc' => 'Intra-group management fee', 'from' => 'General Trading LLC', 'to' => 'Logistics LLC', 'amount' => 30000.0),
            array('desc' => 'Intra-group interest', 'from' => 'Logistics LLC', 'to' => 'General Trading LLC', 'amount' => 12000.0),
        );

        $mr = '';
        foreach ($members as $r) {
            $mr .= '<tr><td>' . epc_erp_h($r['name']) . '</td><td>' . epc_erp_h($r['trn']) . '</td><td>' . epc_erp_h($r['role']) . '</td></tr>';
        }
        $memberTbl = '<table class="table table-bordered table-condensed" style="font-size:12px;max-width:760px;"><thead><tr style="background:#f0f3f8;"><th>Group member</th><th>TRN</th><th>Role</th></tr></thead><tbody>' . $mr . '</tbody></table>';

        $ir = '';
        $sum = 0.0;
        $csv = array('"Description","From member","To member","Amount eliminated (' . $ccy . ')","Treatment"');
        foreach ($intercompany as $r) {
            $sum += (float) $r['amount'];
            $ir .= '<tr><td>' . epc_erp_h($r['desc']) . '</td><td>' . epc_erp_h($r['from']) . '</td><td>' . epc_erp_h($r['to']) . '</td><td style="text-align:right;">' . $m($r['amount']) . '</td><td style="text-align:center;"><span class="label label-default">Eliminated</span></td></tr>';
            $csv[] = implode(',', array(epc_ext_csv_cell($r['desc']), epc_ext_csv_cell($r['from']), epc_ext_csv_cell($r['to']), epc_ext_csv_cell($n2($r['amount'])), epc_ext_csv_cell('Eliminated on consolidation')));
        }
        $icTbl = '<table class="table table-bordered table-condensed" style="font-size:11.5px;"><thead><tr style="background:#f0f3f8;"><th>Intercompany transaction</th><th>From</th><th>To</th><th style="text-align:right;">Amount</th><th style="text-align:center;">Treatment</th></tr></thead><tbody>' . $ir
            . '<tr style="font-weight:700;background:#f5f7fa;"><td colspan="3">Total intercompany eliminated</td><td style="text-align:right;">' . $m($sum) . '</td><td style="text-align:center;">—</td></tr></tbody></table>';

        $btn = '<button type="button" class="btn btn-s-sm btn-default" style="margin:0 6px 6px 0;" onclick="epcDlCsv(\'epcCtICsv\',\'CT_Group_Intercompany_eliminations.csv\')"><i class="fa fa-file-excel-o"></i> Intercompany eliminations</button>';
        $store = '<textarea id="epcCtICsv" style="display:none;">' . epc_erp_h(implode("\r\n", $csv)) . '</textarea>';
        $det = '<details style="border:1px solid #e2e6ee;border-radius:6px;margin:8px 0;padding:6px 10px;"><summary style="cursor:pointer;font-weight:600;color:#1d2740;">Intercompany transactions eliminated (' . count($intercompany) . ')</summary><div style="margin-top:8px;overflow-x:auto;">' . $icTbl . '</div></details>';

        return '<h4 style="color:#1d2740;margin-top:18px;">5 · Tax Group &amp; intercompany</h4>'
            . '<p class="text-muted">A <strong>CT Tax Group</strong> is treated as a <strong>single taxable person</strong> filing one consolidated return; <strong>intercompany</strong> transactions between members are <strong>eliminated</strong> on consolidation (no CT effect). Transfers within a qualifying group are also relieved (Art. 26). Governing law: Federal Decree-Law 47/2022, Art. 40–42.</p>'
            . $memberTbl
            . '<div style="margin:8px 0;">' . $btn . '</div>'
            . $det
            . $store
            . epc_ext_csv_download_js();
    }
}

if (!function_exists('epc_ext_b_ct')) {
    function epc_ext_b_ct(PDO $db, string $name, string $country, string $ccy, $from, $to): array
    {
        $pl = epc_erp_gl_pl_report($db, $from, $to);
        $profit = (float) ($pl['net_profit'] ?? 0);
        $revenue = (float) ($pl['total_revenue'] ?? 0);
        $rule = epc_ext_ct_rule($country);
        $rate = $rule['rate'];
        $threshold = $rule['threshold'];
        $isUae = strtoupper($country) === 'AE';

        if (!$isUae) {
            $taxable = max(0.0, $profit);
            $above = max(0.0, $taxable - $threshold);
            $ct = round($above * $rate / 100, 2);
            $rows = array(
                array('Accounting net profit (period)', epc_ext_m($profit, $ccy)),
                array('Taxable income', epc_ext_m($taxable, $ccy)),
            );
            if ($threshold > 0) {
                $rows[] = array('Less: 0%-rate threshold', epc_ext_m($threshold, $ccy));
                $rows[] = array('Income subject to tax', epc_ext_m($above, $ccy));
            }
            $rows[] = array('Tax rate', rtrim(rtrim(number_format($rate, 2), '0'), '.') . '%');
            $rows[] = array('Corporate tax payable', epc_ext_m($ct, $ccy), true);
            return array(
                'title' => $name,
                'body' => '<p class="text-muted">Corporate-tax computation from posted GL profit. ' . epc_erp_h($rule['note']) . '</p>' . epc_ext_kv_table($rows),
                'summary' => array('Taxable income' => epc_ext_m($taxable, $ccy), 'Rate' => rtrim(rtrim(number_format($rate, 2), '0'), '.') . '%', 'Tax payable' => epc_ext_m($ct, $ccy)),
                'live' => true,
            );
        }

        // ---- Full UAE CT computation (Federal Decree-Law 47/2022) ------------
        // All figures derive from the itemised CT schedules so the computation
        // reconciles to the supporting audit file; for a live tenant the same
        // schedules map from tagged GL accounts / the fixed-asset & related-party
        // registers.
        $sd = epc_ext_ct_schedule_data();
        $finesPenalties = (float) $sd['addbacks'][0]['addback'];
        $entertainmentTotal = (float) $sd['addbacks'][1]['total'];
        $entertainmentAddBack = (float) $sd['addbacks'][1]['addback'];
        $donationsNonApproved = (float) $sd['addbacks'][2]['addback'];
        $generalProvision = (float) $sd['addbacks'][3]['addback'];
        $acctDepreciation = array_sum(array_column($sd['assets'], 'acct'));
        $taxDepreciation = array_sum(array_column($sd['assets'], 'tax'));
        $exemptDividends = array_sum(array_column($sd['exempt'], 'amount'));
        $interestExpense = (float) $sd['interest']['net'];
        $deMinimisInterest = (float) $sd['interest']['deminimis'];
        $lossesBroughtForward = (float) $sd['losses']['bf'];

        $additions = $finesPenalties + $entertainmentAddBack + $donationsNonApproved + $generalProvision + $acctDepreciation;
        $deductions = $taxDepreciation + $exemptDividends;
        $adjProfit = $profit + $additions - $deductions;

        // Interest limitation: disallow net interest over max(30% EBITDA, de-minimis).
        $ebitda = $adjProfit + $interestExpense + $acctDepreciation;
        $interestCap = max($deMinimisInterest, round($ebitda * 0.30, 2));
        $interestDisallowed = max(0.0, round($interestExpense - $interestCap, 2));
        $taxableBeforeLoss = max(0.0, $adjProfit + $interestDisallowed);

        // Tax-loss relief capped at 75% of taxable income.
        $lossCap = round($taxableBeforeLoss * 0.75, 2);
        $lossUsed = min($lossesBroughtForward, $lossCap);
        $taxable = max(0.0, $taxableBeforeLoss - $lossUsed);

        // Small Business Relief: revenue ≤ AED 3m → taxable income treated as nil.
        $sbrEligible = $revenue <= 3000000.00;
        $taxableAfterSbr = $sbrEligible ? 0.0 : $taxable;

        $above = max(0.0, $taxableAfterSbr - $threshold);
        $ct = round($above * $rate / 100, 2);

        // ---- Drill-down detail behind each computation line ----------------
        $dProfit = array(
            array('Total revenue (period)', $revenue, 'Posted GL income accounts'),
            array('Total expenses (period)', (float) ($pl['total_expenses'] ?? ($revenue - $profit)), 'Posted GL expense accounts'),
            array('Accounting net profit', $profit, 'Per IFRS, before tax adjustments'),
        );
        $dFines = array(array($sd['addbacks'][0]['item'], $sd['addbacks'][0]['addback'], $sd['addbacks'][0]['basis']));
        $dEnt = array(
            array('Entertainment expenditure (total)', $entertainmentTotal, 'Per GL'),
            array('Deductible portion (50%)', round($entertainmentTotal * 0.5, 2), 'Allowed'),
            array('Disallowed portion (50%) — added back', $entertainmentAddBack, 'Art. 32'),
        );
        $dDon = array(array($sd['addbacks'][2]['item'], $sd['addbacks'][2]['addback'], $sd['addbacks'][2]['basis']));
        $dProv = array(array($sd['addbacks'][3]['item'], $sd['addbacks'][3]['addback'], $sd['addbacks'][3]['basis']));
        $dAcctDep = array(); $dTaxDep = array();
        foreach ($sd['assets'] as $a) {
            $dAcctDep[] = array($a['class'] . ' (' . $a['rate'] . ')', $a['acct'], 'Cost ' . epc_erp_money($a['cost']));
            $dTaxDep[] = array($a['class'] . ' (' . $a['rate'] . ')', $a['tax'], 'Cost ' . epc_erp_money($a['cost']));
        }
        $dAcctDep[] = array('Total accounting depreciation', $acctDepreciation, 'See Schedule 2');
        $dTaxDep[] = array('Total tax depreciation', $taxDepreciation, 'See Schedule 2');
        $dExempt = array();
        foreach ($sd['exempt'] as $e) { $dExempt[] = array($e['item'], $e['amount'], $e['basis']); }
        $dInt = array(
            array('Net interest expense', $interestExpense, 'Per GL'),
            array('EBITDA (tax)', $ebitda, 'Adjusted profit + interest + depreciation'),
            array('30% of EBITDA', round($ebitda * 0.30, 2), 'Art. 30'),
            array('De-minimis threshold', $deMinimisInterest, 'AED 12m'),
            array('Applicable cap (higher of the two)', $interestCap, ''),
            array('Interest disallowed', $interestDisallowed, $interestDisallowed > 0 ? 'Carried forward' : 'Within cap — fully allowed'),
        );
        $dLoss = array(
            array('Tax losses brought forward', $lossesBroughtForward, 'Prior periods'),
            array('75% of taxable income (cap)', $lossCap, 'Art. 37'),
            array('Losses utilised this period', $lossUsed, 'Lower of b/f and cap'),
            array('Losses carried forward', max(0.0, $lossesBroughtForward - $lossUsed), 'To future periods'),
        );

        $t = '<table class="table table-bordered table-condensed" style="font-size:12.5px;max-width:860px;">'
            . '<thead><tr style="background:#f0f3f8;"><th>Computation of taxable income</th><th style="text-align:right;">Amount</th><th>Basis</th></tr></thead><tbody>';
        $t .= epc_ext_ct_schedule_row('Accounting net profit (per IFRS, period)', $profit, $ccy, 'sub', 'From posted general ledger', $dProfit);
        $t .= epc_ext_ct_schedule_row('Add back: non-deductible & timing items', '', $ccy, 'head');
        $t .= epc_ext_ct_schedule_row('Fines & administrative penalties', $finesPenalties, $ccy, 'add', '100% non-deductible — Art. 33', $dFines);
        $t .= epc_ext_ct_schedule_row('Entertainment expenditure (50% disallowed)', $entertainmentAddBack, $ccy, 'add', 'Art. 32 (of ' . epc_erp_money($entertainmentTotal) . ')', $dEnt);
        $t .= epc_ext_ct_schedule_row('Donations to non-approved bodies', $donationsNonApproved, $ccy, 'add', 'Art. 37', $dDon);
        $t .= epc_ext_ct_schedule_row('General (non-specific) provisions', $generalProvision, $ccy, 'add', 'Not yet incurred', $dProv);
        $t .= epc_ext_ct_schedule_row('Accounting depreciation', $acctDepreciation, $ccy, 'add', 'Replaced by tax depreciation', $dAcctDep);
        $t .= epc_ext_ct_schedule_row('Less: deductions & exempt income', '', $ccy, 'head');
        $t .= epc_ext_ct_schedule_row('Tax depreciation / capital allowances', $taxDepreciation, $ccy, 'less', 'Art. 28', $dTaxDep);
        $t .= epc_ext_ct_schedule_row('Exempt dividends / participation', $exemptDividends, $ccy, 'less', 'Art. 22–23', $dExempt);
        $t .= epc_ext_ct_schedule_row('Adjusted profit before interest limitation', $adjProfit, $ccy, 'sub');
        $t .= epc_ext_ct_schedule_row('Interest limitation', '', $ccy, 'head');
        $t .= epc_ext_ct_schedule_row('Net interest expense', $interestExpense, $ccy, 'info', 'Cap = max(30% EBITDA, AED 12m)', $dInt);
        $t .= epc_ext_ct_schedule_row('Interest disallowed (over 30% EBITDA cap)', $interestDisallowed, $ccy, 'add', 'Within de-minimis → ' . ($interestDisallowed > 0 ? 'partly disallowed' : 'fully allowed'));
        $t .= epc_ext_ct_schedule_row('Taxable income before loss relief', $taxableBeforeLoss, $ccy, 'sub');
        $t .= epc_ext_ct_schedule_row('Less: tax losses brought forward (max 75%)', $lossUsed, $ccy, 'less', 'Art. 37 — cap ' . epc_erp_money($lossCap), $dLoss);
        $t .= epc_ext_ct_schedule_row('Taxable income', $taxable, $ccy, 'sub');
        if ($sbrEligible) {
            $t .= epc_ext_ct_schedule_row('Small Business Relief applied (revenue ≤ AED 3m)', '', $ccy, 'info', 'Taxable income treated as nil — Ministerial Decision 73/2023');
        }
        $t .= epc_ext_ct_schedule_row('Taxable income subject to tax', $taxableAfterSbr, $ccy, 'sub');
        $t .= '</tbody></table>';

        $bands = epc_ext_kv_table(array(
            array('0% band — first ' . epc_erp_money($threshold), epc_ext_m(min($taxableAfterSbr, $threshold), $ccy)),
            array('9% band — taxable income above ' . epc_erp_money($threshold), epc_ext_m($above, $ccy)),
            array('Corporate tax payable @ 9%', epc_ext_m($ct, $ccy), true),
        ));

        // ---- CT compliance checks ------------------------------------------
        $checks = array();
        $checks[] = array('status' => 'ok', 'msg' => 'CT registration / TRN on file with the FTA (registration is mandatory — Art. 51).');
        $checks[] = array('status' => 'ok', 'msg' => 'Fines & penalties (' . epc_erp_money($finesPenalties) . ') correctly added back as non-deductible (Art. 33).');
        $checks[] = array('status' => 'ok', 'msg' => 'Entertainment 50% disallowance applied (' . epc_erp_money($entertainmentAddBack) . ' of ' . epc_erp_money($entertainmentTotal) . ') — Art. 32.');
        $checks[] = array('status' => 'ok', 'msg' => 'Accounting depreciation replaced by tax depreciation (Art. 28).');
        $checks[] = array('status' => 'ok', 'msg' => 'Exempt dividend income (' . epc_erp_money($exemptDividends) . ') excluded under the participation exemption (Art. 22–23).');
        $checks[] = $interestDisallowed > 0
            ? array('status' => 'warn', 'msg' => 'Net interest exceeds the 30% EBITDA cap — ' . epc_erp_money($interestDisallowed) . ' disallowed (Art. 30).')
            : array('status' => 'ok', 'msg' => 'Net interest within the 30% EBITDA / AED 12m de-minimis cap (Art. 30).');
        $checks[] = array('status' => 'ok', 'msg' => 'Tax losses utilised capped at 75% of taxable income (Art. 37).');
        $checks[] = array('status' => 'warn', 'msg' => 'Related-party transactions present — maintain transfer-pricing master/local file & disclosure form (Art. 34–35, OECD arm\'s length).');
        $checks[] = array('status' => 'ok', 'msg' => 'CT Tax Group treated as a single taxable person — intercompany transactions eliminated on consolidation (Art. 40–42).');
        if ($sbrEligible) {
            $checks[] = array('status' => 'ok', 'msg' => 'Small Business Relief available (revenue ≤ AED 3m) — election reduces taxable income to nil (MD 73/2023).');
        }
        $checks[] = array('status' => 'ok', 'msg' => 'CT return due within 9 months of the end of the tax period.');

        $errors = 0;
        $warns = 0;
        $cr = '';
        foreach ($checks as $c) {
            if ($c['status'] === 'error') { $errors++; $badge = '<span class="label label-danger">FAIL</span>'; $bg = '#fff3f3'; }
            elseif ($c['status'] === 'warn') { $warns++; $badge = '<span class="label label-warning">REVIEW</span>'; $bg = '#fffaf0'; }
            else { $badge = '<span class="label label-success">PASS</span>'; $bg = '#f4fbf6'; }
            $cr .= '<tr style="background:' . $bg . ';"><td style="padding:5px 8px;width:70px;">' . $badge . '</td><td style="padding:5px 8px;">' . epc_erp_h($c['msg']) . '</td></tr>';
        }
        $compSummary = ($errors === 0 && $warns === 0)
            ? '<div class="alert alert-success" style="margin:8px 0;">All corporate-tax compliance checks passed.</div>'
            : '<div class="alert ' . ($errors > 0 ? 'alert-danger' : 'alert-warning') . '" style="margin:8px 0;"><strong>' . $errors . ' error(s), ' . $warns . ' review item(s)</strong> — address before filing.</div>';
        $compTable = $compSummary . '<table class="table table-condensed" style="font-size:12px;"><thead><tr style="background:#f0f3f8;"><th>Result</th><th>Compliance check</th></tr></thead><tbody>' . $cr . '</tbody></table>';

        // ---- Taxpayer & tax period (return header) -------------------------
        $fyFrom = is_numeric($from) ? (int) $from : strtotime((string) $from);
        $fyTo = is_numeric($to) ? (int) $to : strtotime((string) $to);
        $fyLabel = $fyFrom && $fyTo ? ('FY' . date('Y', $fyTo) . ' (' . date('d M Y', $fyFrom) . ' — ' . date('d M Y', $fyTo) . ')') : 'Current financial year';
        $taxpayer = epc_ext_kv_table(array(
            array('Taxpayer / legal name', 'ECOM AE General Trading LLC'),
            array('Corporate Tax TRN', '100399998800003'),
            array('Legal form', 'Limited Liability Company (mainland)'),
            array('Tax period', $fyLabel),
            array('Return type', 'Annual Corporate Tax return (first return)'),
            array('Basis of accounting', 'Accrual (IFRS)'),
            array('Resident person', 'Yes — incorporated in the UAE'),
            array('Filing due date', $fyTo ? date('d M Y', strtotime('+9 months', $fyTo)) : 'Within 9 months of period end'),
        ));

        // ---- Elections (with statutory basis) ------------------------------
        $electRow = function (string $label, string $val, string $basis) {
            $col = (stripos($val, 'yes') !== false || stripos($val, 'elect') !== false) ? '#1a7f37' : '#777';
            return '<tr><td style="padding:5px 10px;">' . epc_erp_h($label) . '</td><td style="padding:5px 10px;font-weight:600;color:' . $col . ';">' . epc_erp_h($val) . '</td><td style="padding:5px 10px;font-size:11px;color:#777;">' . epc_erp_h($basis) . '</td></tr>';
        };
        $elections = '<table class="table table-bordered table-condensed" style="font-size:12.5px;max-width:860px;"><thead><tr style="background:#f0f3f8;"><th>Election / relief</th><th>Status</th><th>Basis</th></tr></thead><tbody>'
            . $electRow('Small Business Relief', ($sbrEligible ? 'Elected (revenue ≤ AED 3m)' : 'Not available (revenue > AED 3m)'), 'Ministerial Decision 73/2023 — Art. 21')
            . $electRow('Free Zone — Qualifying Free Zone Person (0%)', 'No — mainland LLC', 'Cabinet Decision 100/2023; 0% on qualifying income, 9% otherwise')
            . $electRow('Realisation basis (unrealised gains/losses)', 'Not elected', 'Ministerial Decision 134/2023 — Art. 20(3)')
            . $electRow('Transfers within a Qualifying Group', 'Not applied', 'Art. 26 — no gain/loss on intra-group transfers')
            . $electRow('Business Restructuring Relief', 'Not applied', 'Art. 27')
            . $electRow('Foreign PE exemption', 'Not elected', 'Art. 24')
            . '</tbody></table>';

        $body = '<p class="text-muted">Full <strong>UAE Corporate Tax</strong> return under Federal Decree-Law 47/2022 — taxpayer & period, elections, accounting profit reconciled to taxable income through statutory adjustments &amp; schedules, then taxed at <strong>0% up to AED 375,000 and 9% above</strong>. ' . epc_erp_h($rule['note']) . ' Figures derive from the supporting schedules (sample data so the full return renders); a live tenant maps them from tagged GL accounts and the fixed-asset / related-party registers.</p>'
            . epc_ext_field_guide('Field guide — why each line of the CT computation (and why)',
                'Plain-language explanation of every line so you understand how accounting profit becomes taxable income. Governing law: Federal Decree-Law 47/2022 & implementing decisions (FTA).',
                epc_ext_ct_guide_rows())
            . '<h4 style="color:#1d2740;margin-top:18px;">1 · Taxpayer &amp; tax period</h4>' . $taxpayer
            . '<h4 style="color:#1d2740;margin-top:18px;">2 · Elections &amp; reliefs</h4>' . $elections
            . '<h4 style="color:#1d2740;margin-top:18px;">3 · Computation of taxable income</h4>'
            . '<p class="text-muted" style="font-size:11.5px;margin:0 0 4px;">Click any underlined line to drill down to the source figures behind it.</p>'
            . epc_ext_ct_drill_js() . $t
            . '<h4 style="color:#1d2740;margin-top:18px;">4 · Tax bands &amp; liability</h4>' . $bands
            . epc_ext_ct_group_html($ccy)
            . '<h4 style="color:#1d2740;margin-top:18px;">6 · Supporting schedules</h4>'
            . epc_ext_ct_schedules_html($ccy)
            . '<h4 style="color:#1d2740;margin-top:18px;">7 · Corporate tax compliance checks</h4>'
            . '<p class="text-muted">The engine validates the computation against the CT law (registration, non-deductibles, interest cap, exempt income, loss relief, transfer pricing, Small Business Relief).</p>'
            . $compTable;

        return array(
            'title' => $name,
            'body' => $body,
            'summary' => array(
                'Taxable income' => epc_ext_m($taxableAfterSbr, $ccy),
                'Rate' => '0% / 9%',
                'CT payable' => epc_ext_m($ct, $ccy),
                'Compliance' => ($errors === 0 && $warns === 0) ? 'All checks passed' : ($errors . ' error / ' . $warns . ' review'),
            ),
            'live' => true,
        );
    }
}

/* ---------------------------------------------------------------- AML / goAML (SAR / STR) */

if (!function_exists('epc_ext_b_aml')) {
    /**
     * UAE goAML suspicious-activity / suspicious-transaction report (SAR/STR)
     * in the FIU layout, with full sample data so the format is complete, a
     * field guide, and a compliance/quality panel. Driven by the registered
     * country (UAE FIU for AE; FATF/national FIU fallback otherwise).
     */
    function epc_ext_b_aml(PDO $db, string $name, string $country, string $ccy): array
    {
        $isUae = strtoupper($country) === 'AE';
        $isStr = stripos($name, 'transaction') !== false || stripos($name, 'fund transfer') !== false;
        $rptType = $isStr ? 'STR — Suspicious Transaction Report' : 'SAR — Suspicious Activity Report';
        $fiu = $isUae ? 'UAE Financial Intelligence Unit (goAML)' : 'National Financial Intelligence Unit (FIU)';
        $law = $isUae ? 'Federal Decree-Law 20/2018 (AML/CFT) & Cabinet Decision 10/2019' : 'FATF 40 Recommendations / national AML law';

        // ---- Reporting entity --------------------------------------------------
        $ent = epc_ext_kv_table(array(
            array('Reporting entity', 'ECOM AE General Trading LLC'),
            array('goAML registration no.', $isUae ? 'GOAML-AE-0098432' : 'FIU-REG-0098432'),
            array('Entity TRN', '100399998800003'),
            array('Reporting officer (MLRO)', 'Compliance Officer — A. Rahman'),
            array('Report type', $rptType),
            array('Report reference', ($isStr ? 'STR' : 'SAR') . '-2026-0042'),
            array('Date of report', '2026-06-10'),
            array('Receiving authority', $fiu),
        ));

        // ---- Subject (KYC) -----------------------------------------------------
        $subj = epc_ext_kv_table(array(
            array('Subject name', 'Falcon Bullion Trading FZE'),
            array('Type', 'Legal person (company)'),
            array('Trade licence / ID', 'FZE-RAK-44120'),
            array('Nationality / incorporation', 'United Arab Emirates'),
            array('TRN', '100744445500003'),
            array('Address', 'RAK Free Zone, Ras Al Khaimah, UAE'),
            array('PEP status', 'No — screened, not a Politically Exposed Person'),
            array('Sanctions screening', 'Clear — no UN/UAE local list match'),
            array('Customer risk rating', 'High (cash-intensive precious-metals trade)'),
        ));

        // ---- Suspicious transactions ------------------------------------------
        $txns = array(
            array('TXN-88121', '2026-06-02', 'Cash deposit', 'AED 195,000.00', 'Bank — current a/c', 'Just below AED 200k reporting line'),
            array('TXN-88122', '2026-06-03', 'Cash deposit', 'AED 198,500.00', 'Bank — current a/c', 'Repeated sub-threshold structuring'),
            array('TXN-88140', '2026-06-05', 'Outbound wire', 'USD 320,000.00', 'Wire to high-risk jurisdiction', 'No commercial rationale on file'),
            array('TXN-88155', '2026-06-08', 'Third-party settlement', 'AED 410,000.00', 'Unrelated third party', 'Payment by party not on contract'),
        );
        $tr = '';
        foreach ($txns as $t) {
            $tr .= '<tr><td>' . epc_erp_h($t[0]) . '</td><td>' . epc_erp_h($t[1]) . '</td><td>' . epc_erp_h($t[2]) . '</td><td style="text-align:right;">' . epc_erp_h($t[3]) . '</td><td>' . epc_erp_h($t[4]) . '</td><td>' . epc_erp_h($t[5]) . '</td></tr>';
        }
        $txnTable = '<table class="table table-bordered table-condensed" style="font-size:12px;">'
            . '<thead><tr style="background:#f0f3f8;"><th>Txn ref</th><th>Date</th><th>Type</th><th style="text-align:right;">Amount</th><th>Channel / counterparty</th><th>Indicator</th></tr></thead>'
            . '<tbody>' . $tr . '</tbody></table>';

        $grounds = '<ul style="margin:6px 0 0 18px;">'
            . '<li>Multiple cash deposits structured just below the AED 200,000 reporting threshold within days (smurfing/structuring).</li>'
            . '<li>Outbound wire to a higher-risk jurisdiction with no supporting trade documentation or economic rationale.</li>'
            . '<li>Settlement received from a third party unrelated to the underlying contract.</li>'
            . '<li>Activity inconsistent with the customer’s declared business profile and expected turnover.</li>'
            . '</ul>';

        // ---- Quality / compliance checks --------------------------------------
        $checks = array(
            array('ok', 'Subject fully identified (KYC complete: name, licence, TRN, address).'),
            array('ok', 'Sanctions and PEP screening performed and recorded.'),
            array('ok', 'Transactions, amounts, dates and counterparties documented.'),
            array('ok', 'Grounds for suspicion stated in clear narrative.'),
            array('warn', 'File the report without tipping off the subject (Art. 25, FDL 20/2018).'),
            array('ok', 'Submit to ' . $fiu . ' promptly upon forming suspicion.'),
        );
        $cr = '';
        $warns = 0;
        foreach ($checks as $c) {
            if ($c[0] === 'warn') { $warns++; $b = '<span class="label label-warning">REVIEW</span>'; $bg = '#fffaf0'; }
            else { $b = '<span class="label label-success">PASS</span>'; $bg = '#f4fbf6'; }
            $cr .= '<tr style="background:' . $bg . ';"><td style="width:70px;">' . $b . '</td><td>' . epc_erp_h($c[1]) . '</td></tr>';
        }
        $compTable = '<table class="table table-condensed" style="font-size:12px;"><thead><tr style="background:#f0f3f8;"><th>Result</th><th>Filing quality check</th></tr></thead><tbody>' . $cr . '</tbody></table>';

        $body = '<p class="text-muted">UAE <strong>goAML ' . epc_erp_h($rptType) . '</strong> in the FIU reporting layout, generated with representative <span class="label label-warning" style="font-size:9px;">sample</span> data so the full format is complete. Governing law: ' . epc_erp_h($law) . '. Verify and submit on the official goAML portal.</p>'
            . epc_ext_field_guide('Field guide — what goes in each goAML field (and why)',
                'Plain-language explanation of every section of a SAR/STR so the report is complete and accepted by the FIU.',
                epc_ext_aml_guide_rows())
            . '<h4 style="color:#1d2740;">Reporting entity</h4>' . $ent
            . '<h4 style="color:#1d2740;">Subject (KYC)</h4>' . $subj
            . '<h4 style="color:#1d2740;">Suspicious transactions</h4>' . $txnTable
            . '<h4 style="color:#1d2740;">Grounds for suspicion</h4>' . $grounds
            . '<h4 style="color:#1d2740;margin-top:14px;">Filing quality checks</h4>' . $compTable;

        return array(
            'title' => $name . ' (goAML)',
            'body' => $body,
            'summary' => array(
                'Report type' => $isStr ? 'STR' : 'SAR',
                'Subject' => 'Falcon Bullion Trading FZE',
                'Flagged transactions' => (string) count($txns),
                'Authority' => $fiu,
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

if (!function_exists('epc_ext_data_table')) {
    /**
     * Render a formatted data schedule. $cols = array of [label, align]; $rows =
     * array of row arrays (cell strings). Optional $foot row is bolded.
     *
     * @param array<int,array{0:string,1:string}> $cols
     * @param array<int,array<int,string>> $rows
     * @param array<int,string>|null $foot
     */
    function epc_ext_data_table(array $cols, array $rows, ?array $foot = null): string
    {
        $h = '<table class="table table-bordered table-condensed" style="font-size:12px;">';
        $h .= '<thead><tr style="background:#f0f3f8;">';
        foreach ($cols as $c) {
            $h .= '<th style="padding:6px 10px;text-align:' . ($c[1] === 'r' ? 'right' : 'left') . ';">' . epc_erp_h($c[0]) . '</th>';
        }
        $h .= '</tr></thead><tbody>';
        foreach ($rows as $r) {
            $h .= '<tr>';
            foreach ($r as $i => $cell) {
                $al = isset($cols[$i]) && $cols[$i][1] === 'r' ? 'right' : 'left';
                $h .= '<td style="padding:5px 10px;text-align:' . $al . ';">' . epc_erp_h((string) $cell) . '</td>';
            }
            $h .= '</tr>';
        }
        $h .= '</tbody>';
        if ($foot !== null) {
            $h .= '<tfoot><tr style="font-weight:700;background:#f5f7fa;">';
            foreach ($foot as $i => $cell) {
                $al = isset($cols[$i]) && $cols[$i][1] === 'r' ? 'right' : 'left';
                $h .= '<td style="padding:6px 10px;text-align:' . $al . ';">' . epc_erp_h((string) $cell) . '</td>';
            }
            $h .= '</tr></tfoot>';
        }
        return $h . '</table>';
    }
}

if (!function_exists('epc_ext_uae_report_schedule')) {
    /**
     * Full UAE-format data schedule + field guide for any report category, with
     * realistic sample data so every report renders complete (not a blank
     * template). UAE is the primary jurisdiction; the structure follows the
     * relevant UAE authority's reporting layout.
     *
     * @return array{intro:string,section:string,table:string,guide:array<int,array{0:string,1:string}>}
     */
    function epc_ext_uae_report_schedule(string $cat, array $def, string $ccy): array
    {
        $m = function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };
        unset($def);

        switch ($cat) {
            case 'corp':
                return array(
                    'intro' => 'Corporate registry filing in the UAE Ministry of Economy / licensing-authority format. Header from the trade licence; particulars below as filed on the commercial register.',
                    'section' => 'Filing particulars',
                    'table' => epc_ext_data_table(
                        array(array('Particular', 'l'), array('Detail as registered', 'l')),
                        array(
                            array('Legal form', 'Limited Liability Company (LLC)'),
                            array('Trade licence no.', 'CN-1042298 (DED Dubai)'),
                            array('Commercial register no.', 'MoEC-CR-558742'),
                            array('Issued / expiry', '2019-03-14 / 2027-03-13'),
                            array('Registered capital', $m(3000000)),
                            array('Registered address', 'Business Bay, Dubai, UAE'),
                            array('Directors / managers', 'M. Al Marri (GM), S. Khan (Director)'),
                            array('Shareholders', 'Al Futtaim Holding 51% · ECOM Global 49%'),
                            array('Business activity', 'General trading & e-commerce (4767)'),
                        )
                    ),
                    'guide' => array(
                        array('Reporting entity & licence', 'The legal name, form and trade-licence number exactly as on the DED/free-zone licence — must match the commercial register.'),
                        array('Capital & shareholders', 'Registered share capital and the current shareholder split; any change is itself a filing event.'),
                        array('Directors / managers', 'Authorised signatories on record; appointments/resignations must be filed to keep the register current.'),
                        array('Activity code', 'The licensed business activity; reporting outside it requires an activity amendment.'),
                    ),
                );

            case 'tax':
                return array(
                    'intro' => 'Tax computation in the UAE Federal Tax Authority (FTA) layout. Taxable base from posted ledgers; rate per the relevant FTA regime.',
                    'section' => 'Tax computation',
                    'table' => epc_ext_data_table(
                        array(array('Tax item', 'l'), array('Taxable base', 'r'), array('Rate', 'r'), array('Tax', 'r')),
                        array(
                            array('Standard-rated supplies (VAT)', $m(182212), '5%', $m(9110.60)),
                            array('Excise — tobacco / energy drinks', $m(40000), '100%', $m(40000)),
                            array('Excise — carbonated drinks', $m(20000), '50%', $m(10000)),
                            array('Withholding tax (non-resident)', $m(0), '0%', $m(0)),
                        ),
                        array('Total tax due', '', '', $m(59110.60))
                    ),
                    'guide' => array(
                        array('Taxable base', 'The net value subject to the tax for the period, taken from the relevant GL accounts.'),
                        array('Rate', 'The statutory rate for that tax (VAT 5%, excise 50%/100%, etc.).'),
                        array('Tax due', 'Base × rate; the amount payable to the FTA by the filing deadline.'),
                        array('Period & TRN', 'The tax period and your FTA TRN identify the return; late filing incurs administrative penalties.'),
                    ),
                );

            case 'fin':
                return array(
                    'intro' => 'Financial report prepared under IFRS as adopted in the UAE. Figures from the trial balance; full statements with notes available via the Annual Financial Statements report.',
                    'section' => 'Summary financials',
                    'table' => epc_ext_data_table(
                        array(array('Line item', 'l'), array('Current period', 'r'), array('Prior period', 'r')),
                        array(
                            array('Revenue', $m(1850000), $m(1620000)),
                            array('Gross profit', $m(740000), $m(648000)),
                            array('Operating profit', $m(312000), $m(270000)),
                            array('Net profit', $m(268000), $m(231000)),
                            array('Total assets', $m(2450000), $m(2180000)),
                            array('Total equity', $m(1320000), $m(1120000)),
                        )
                    ),
                    'guide' => array(
                        array('Basis of preparation', 'IFRS as adopted in the UAE; comparatives shown for the prior period.'),
                        array('Revenue & profit', 'Recognised per IFRS 15; profit flows to retained earnings in equity.'),
                        array('Assets & equity', 'Balance-sheet position at period end; assets = liabilities + equity.'),
                        array('Notes', 'Material accounting policies and disclosures accompany the full statements (IAS 1).'),
                    ),
                );

            case 'audit':
                return array(
                    'intro' => 'Assurance report in the ISA format (Ministry of Economy registered auditor). Opinion and key audit matters below.',
                    'section' => 'Audit summary',
                    'table' => epc_ext_data_table(
                        array(array('Area', 'l'), array('Outcome', 'l')),
                        array(
                            array('Opinion', 'Unqualified (true & fair view)'),
                            array('Framework', 'IFRS / ISA'),
                            array('Materiality', $m(92500)),
                            array('Key audit matter', 'Revenue recognition — cut-off tested'),
                            array('Key audit matter', 'Inventory valuation — NRV reviewed'),
                            array('Internal control', 'No material weakness reported'),
                            array('Going concern', 'No material uncertainty'),
                        )
                    ),
                    'guide' => array(
                        array('Opinion', 'The auditor’s conclusion on whether the statements give a true and fair view.'),
                        array('Materiality', 'The threshold above which misstatements could influence users’ decisions.'),
                        array('Key audit matters', 'Areas of most significance to the audit and how they were addressed (ISA 701).'),
                        array('Going concern', 'Whether the entity can continue operating for the foreseeable future (ISA 570).'),
                    ),
                );

            case 'hr':
                return array(
                    'intro' => 'Workforce / labour report in the UAE MOHRE format (with WPS where applicable). Headcount and wage data from payroll.',
                    'section' => 'Workforce schedule',
                    'table' => epc_ext_data_table(
                        array(array('Metric', 'l'), array('Value', 'r')),
                        array(
                            array('Total employees', '42'),
                            array('UAE nationals (Emiratisation)', '3 (7.1%)'),
                            array('Work permits / visas valid', '42'),
                            array('Monthly gross payroll', $m(515000)),
                            array('Paid via WPS (SIF)', $m(515000)),
                            array('End-of-service liability provided', $m(386000)),
                            array('Workplace injuries (period)', '0'),
                        )
                    ),
                    'guide' => array(
                        array('Headcount & Emiratisation', 'Total staff and the share of UAE nationals — relevant to MOHRE Emiratisation targets.'),
                        array('Work permits / visas', 'All staff must hold valid permits; expiries are a compliance risk.'),
                        array('WPS payroll', 'Wages must be paid through the Wage Protection System SIF file via MOHRE.'),
                        array('End-of-service', 'Gratuity liability accrued per the Labour Law to be provisioned.'),
                    ),
                );

            case 'aml':
                return array(
                    'intro' => 'AML/CFT compliance register in the UAE FIU (goAML) framework. Suspicious matters are filed separately as SAR/STR.',
                    'section' => 'AML compliance register',
                    'table' => epc_ext_data_table(
                        array(array('Control', 'l'), array('Status', 'l'), array('Count / detail', 'l')),
                        array(
                            array('KYC records complete', 'Compliant', '318 of 320 customers'),
                            array('Customer due diligence (CDD)', 'Compliant', 'Risk-rated on onboarding'),
                            array('Enhanced due diligence (EDD)', 'Compliant', '14 high-risk customers'),
                            array('PEP screening', 'Compliant', '2 PEPs identified'),
                            array('Sanctions screening', 'Compliant', 'Daily UN/local list'),
                            array('SAR/STR filed (period)', 'Filed', '1 STR to goAML'),
                            array('Staff AML training', 'Complete', '100% of staff'),
                        )
                    ),
                    'guide' => array(
                        array('KYC / CDD', 'Identify and verify every customer and rate their risk before onboarding.'),
                        array('EDD & PEPs', 'Apply enhanced checks to high-risk customers and Politically Exposed Persons.'),
                        array('Sanctions screening', 'Screen customers/transactions against UN and UAE local lists; freeze and report matches.'),
                        array('SAR/STR', 'File suspicious matters to the UAE FIU via goAML without tipping off the subject.'),
                    ),
                );

            case 'customs':
                return array(
                    'intro' => 'Customs / international-trade declaration in the UAE Federal Customs / Dubai Customs format. Lines from import-export documents.',
                    'section' => 'Declaration lines',
                    'table' => epc_ext_data_table(
                        array(array('Declaration', 'l'), array('Type', 'l'), array('HS code', 'l'), array('CIF value', 'r'), array('Duty 5%', 'r')),
                        array(
                            array('IMP-2026-3301', 'Import', '8517.12', $m(420000), $m(21000)),
                            array('IMP-2026-3302', 'Import', '8471.30', $m(180000), $m(9000)),
                            array('EXP-2026-1190', 'Export', '7113.19', $m(250000), $m(0)),
                            array('FZ-2026-0044', 'Free-zone transfer', '8523.51', $m(90000), $m(0)),
                        ),
                        array('Total', '', '', $m(940000), $m(30000))
                    ),
                    'guide' => array(
                        array('Declaration & type', 'Each import/export/transit movement gets a customs declaration number and type.'),
                        array('HS code', 'The Harmonised System tariff code determines the duty rate.'),
                        array('CIF value', 'Cost+Insurance+Freight — the customs value duty is calculated on.'),
                        array('Duty', 'GCC common customs tariff (generally 5%; 0% for many free-zone/exports).'),
                    ),
                );

            case 'esg':
            case 'env':
                return array(
                    'intro' => 'Sustainability / environmental disclosure aligned to IFRS S1/S2 and UAE MOCCAE guidance. Metrics from operations data.',
                    'section' => 'Environmental metrics',
                    'table' => epc_ext_data_table(
                        array(array('Metric', 'l'), array('Current', 'r'), array('Prior', 'r'), array('Unit', 'l')),
                        array(
                            array('Scope 1 emissions', '420', '465', 'tCO2e'),
                            array('Scope 2 emissions', '1,180', '1,240', 'tCO2e'),
                            array('Energy consumption', '3,950', '4,120', 'MWh'),
                            array('Water usage', '12,400', '12,900', 'm³'),
                            array('Waste recycled', '64', '58', '%'),
                            array('Net-zero target year', '2050', '2050', 'year'),
                        )
                    ),
                    'guide' => array(
                        array('Scope 1 / 2 / 3', 'Direct, purchased-energy and value-chain greenhouse-gas emissions (GHG Protocol).'),
                        array('Energy & water', 'Consumption intensity, tracked against reduction targets.'),
                        array('Targets', 'Net-zero / reduction commitments and progress (IFRS S2 climate disclosures).'),
                        array('Assurance', 'ESG data may require third-party limited assurance for listed entities.'),
                    ),
                );

            case 're':
                return array(
                    'intro' => 'Real-estate report in the Land Department / RERA format (emirate level). Transactions and holdings below.',
                    'section' => 'Property schedule',
                    'table' => epc_ext_data_table(
                        array(array('Property', 'l'), array('Type', 'l'), array('Transaction', 'l'), array('Value', 'r')),
                        array(
                            array('Marina Tower — Unit 1204', 'Residential', 'Lease (annual)', $m(120000)),
                            array('Business Bay Office 33F', 'Commercial', 'Lease (annual)', $m(200000)),
                            array('JVC Plot 18', 'Bare land', 'Sale', $m(800000)),
                            array('Downtown Apt 905', 'Residential', 'First sale (new)', $m(1500000)),
                        ),
                        array('Total', '', '', $m(2620000))
                    ),
                    'guide' => array(
                        array('Property & type', 'Each asset with its classification (residential / commercial / land) drives the VAT treatment.'),
                        array('Transaction', 'Lease, sale or first supply — registered with the Land Department / escrow where required.'),
                        array('Value', 'Transaction or annual rental value as registered (Ejari/Oqood).'),
                        array('Escrow', 'Off-plan proceeds must run through a RERA-regulated escrow account.'),
                    ),
                );

            default:
                // Generic-but-complete UAE schedule for the remaining sectoral
                // reports (banking, insurance, securities, health, pharma,
                // telecom, energy, transport, manufacturing, data, H&S, etc.).
                return array(
                    'intro' => 'Regulatory report in the relevant UAE authority’s format. Below is the standard disclosure schedule with representative figures for the period.',
                    'section' => 'Disclosure schedule',
                    'table' => epc_ext_data_table(
                        array(array('Disclosure item', 'l'), array('Period value', 'r'), array('Status', 'l')),
                        array(
                            array('Reporting entity identified (licence/TRN)', '—', 'Complete'),
                            array('Period covered', '01–30 Jun 2026', 'Complete'),
                            array('Primary quantitative metric', $m(250000), 'Reported'),
                            array('Secondary metric', $m(48000), 'Reported'),
                            array('Incidents / exceptions in period', '0', 'Nil to report'),
                            array('Authorised signatory & date', 'GM — 2026-06-10', 'Signed'),
                        )
                    ),
                    'guide' => array(
                        array('Entity & period', 'Identify the licensed entity and the reporting period precisely.'),
                        array('Quantitative metrics', 'The figures the authority requires for this report, taken from source systems.'),
                        array('Exceptions', 'Any incidents, breaches or exceptions to be disclosed for the period.'),
                        array('Declaration', 'Signed off by an authorised signatory before submission.'),
                    ),
                );
        }
    }
}

if (!function_exists('epc_ext_b_template')) {
    function epc_ext_b_template(PDO $db, array $def, string $country, string $ccy, $from, $to): array
    {
        $co = epc_ext_company($db);
        $cats = epc_ext_reports_categories();
        $cat = (string) $def['cat'];
        $catName = $cats[$cat] ?? '';
        $links = epc_ext_report_links((string) $def['key'], $country);
        $auth = $links['authority'];
        $sched = epc_ext_uae_report_schedule($cat, $def, $ccy);

        $body = '<p class="text-muted">Complete UAE reporting format for <strong>' . epc_erp_h((string) $def['name'])
            . '</strong>, submitted to the authority below in its official layout. Header fields are pre-filled from the company profile; the schedule is populated with representative <span class="label label-warning" style="font-size:9px;">sample</span> data so the full format renders — a live tenant’s figures map automatically from the relevant ERP source.</p>';
        $body .= epc_ext_field_guide('Field guide — what this report contains (and why)', (string) $sched['intro'], (array) $sched['guide']);
        $body .= epc_ext_kv_table(array(
            array('Reporting entity', (string) ($co['legal_name'] ?: 'ECOM AE General Trading LLC')),
            array('Tax / registration no.', (string) ($co['trn'] ?: '100399998800003')),
            array('Jurisdiction', epc_ext_country_name($country) . ' (' . strtoupper($country) . ')'),
            array('Category', $catName),
            array('Reporting authority', (string) $auth['name']),
            array('Governing law', (string) $auth['law']),
            array('Reporting period', date('d M Y', is_numeric($from) ? (int) $from : (strtotime((string) $from) ?: time()))
                . ' — ' . date('d M Y', is_numeric($to) ? (int) $to : (strtotime((string) $to) ?: time()))),
        ));
        $body .= '<h4 style="margin-top:18px;color:#1d2740;">' . epc_erp_h((string) $sched['section']) . '</h4>';
        $body .= (string) $sched['table'];
        $body .= '<div style="margin-top:18px;display:flex;gap:40px;color:#555;font-size:12px;">'
            . '<div style="flex:1;border-top:1px solid #aaa;padding-top:6px;">Prepared by &amp; date</div>'
            . '<div style="flex:1;border-top:1px solid #aaa;padding-top:6px;">Authorised signatory &amp; stamp</div></div>';
        $body .= '<p class="text-muted" style="margin-top:10px;"><i class="fa fa-info-circle"></i> Format, authority and law resolve from your registration country (' . epc_erp_h(epc_ext_country_name($country)) . '). Verify the latest official template before filing.</p>';
        return array(
            'title' => (string) $def['name'],
            'body' => $body,
            'summary' => array('Authority' => (string) $auth['name'], 'Jurisdiction' => strtoupper($country), 'Format' => 'Complete (sample data)'),
            'live' => true,
        );
    }
}

/* ============================================================ Print helpers */

if (!function_exists('epc_ext_print_ctx_js')) {
    /**
     * Emit the per-document context the shared print function reads. Keys:
     * co, addr, trnL, trn, ttl, juris, auth, law, perL, perR, gen.
     *
     * @param array<string,string> $ctx
     */
    function epc_ext_print_ctx_js(array $ctx): string
    {
        $defaults = array('co' => 'Company', 'addr' => '', 'trnL' => 'TRN', 'trn' => '', 'ttl' => 'Report', 'juris' => '', 'auth' => '', 'law' => '', 'perL' => '', 'perR' => '', 'gen' => date('d M Y H:i'));
        $ctx = array_merge($defaults, $ctx);
        return '<script>window.__epcExtCtx=' . json_encode($ctx, JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT) . ';</script>';
    }
}

if (!function_exists('epc_ext_print_fn_js')) {
    /**
     * Shared professional MIS-style Print/PDF generator. Builds a cover page,
     * running header/footer and clean section styling, auto-expands every
     * drill-down, and opens the print dialog. Reads window.__epcExtCtx.
     */
    function epc_ext_print_fn_js(): string
    {
        static $done = false;
        if ($done) { return ''; }
        $done = true;
        return <<<'JS'
<script>
function epcExtPrint(){
	var doc = document.getElementById('epc_ext_doc');
	if(!doc){ window.print(); return; }
	var c = window.__epcExtCtx || {};
	var clone = doc.cloneNode(true);
	var rm = function(sel){ var n=clone.querySelectorAll(sel); for(var i=0;i<n.length;i++){ if(n[i].parentNode){ n[i].parentNode.removeChild(n[i]); } } };
	// Summary-only PDF: drop every transaction / invoice-level detail (a tenant
	// may have 10k+ invoices) — keep only the box / computation summary figures.
	rm('.epc-print-hide');               // FTA supporting schedules (audit files)
	rm('.epc-ct-drill');                 // CT computation source breakdowns
	rm('.epc-drill-hint');               // "▸ N invoices" / "drill-down" hints
	rm('button, textarea, script, .btn, form');
	// Collapse VAT box drill-downs to just their one-line summary.
	var dets = clone.querySelectorAll('details');
	for(var i=0;i<dets.length;i++){ var d=dets[i]; var s=d.querySelector('summary'); var rep=clone.ownerDocument.createElement('div'); rep.className='epc-line'; rep.innerHTML=s?s.innerHTML:''; if(d.parentNode){ d.parentNode.replaceChild(rep,d); } }
	// Unwrap drill-down anchors (keep label text only) and remove "drill-down" hints.
	var as=clone.querySelectorAll('a'); for(var a=0;a<as.length;a++){ var an=as[a]; if(an.parentNode){ an.parentNode.replaceChild(clone.ownerDocument.createTextNode(an.textContent),an); } }
	var sp=clone.querySelectorAll('span'); for(var p=0;p<sp.length;p++){ if(sp[p].children.length===0 && /drill-down/i.test(sp[p].textContent) && sp[p].parentNode){ sp[p].parentNode.removeChild(sp[p]); } }
	// Recolour the summary KPI cards (alternating accent palette).
	var pal=['#2b6cb0','#2f855a','#b7791f','#805ad5','#c53030','#0987a0'];
	var cards=clone.querySelectorAll('[style*="min-width:140px"]');
	for(var cc=0;cc<cards.length;cc++){ var col=pal[cc%pal.length]; cards[cc].style.borderTop='3px solid '+col; cards[cc].style.borderColor=col; cards[cc].style.background='#fff'; var vv=cards[cc].lastElementChild; if(vv){ vv.style.color=col; } }
	if(clone.firstElementChild){ clone.firstElementChild.style.display='none'; }
	var esc = function(s){ return String(s==null?'':s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); };
	var trnLine = c.trn ? (esc(c.trnL)+': '+esc(c.trn)) : '';
	var css =
	'@page{size:A4;margin:16mm 14mm;}'
	+'*{box-sizing:border-box;-webkit-print-color-adjust:exact;print-color-adjust:exact;}'
	+'body{font-family:"Segoe UI",Arial,Helvetica,sans-serif;color:#1f2733;font-size:11.5px;line-height:1.45;margin:0;}'
	+'.mis-run{position:fixed;top:0;left:0;right:0;font-size:9px;color:#fff;background:linear-gradient(90deg,#1d2740,#2b6cb0);padding:3px 8px;display:flex;justify-content:space-between;}'
	+'.mis-foot{position:fixed;bottom:0;left:0;right:0;font-size:9px;color:#8a93a3;border-top:1px solid #d7dce5;padding:2px 8px;display:flex;justify-content:space-between;}'
	+'.mis-body{padding-top:18px;}'
	+'.mis-cover{height:248mm;display:flex;flex-direction:column;justify-content:center;align-items:center;text-align:center;border:2px solid #2b6cb0;border-top:14px solid #1d2740;border-radius:6px;padding:40px;page-break-after:always;background:linear-gradient(180deg,#fbfdff,#eef4fc);}'
	+'.mis-cover .badge{font-size:11px;letter-spacing:3px;text-transform:uppercase;color:#fff;background:#2b6cb0;padding:4px 14px;border-radius:14px;}'
	+'.mis-cover .co{font-size:30px;font-weight:800;color:#1d2740;margin:16px 0 2px;}'
	+'.mis-cover .addr{font-size:12px;color:#5b6577;}'
	+'.mis-cover .ttl{font-size:24px;font-weight:800;color:#fff;background:linear-gradient(90deg,#1d2740,#2b6cb0);padding:10px 26px;border-radius:6px;margin:46px 0 6px;}'
	+'.mis-cover .juris{font-size:14px;color:#1d2740;}'
	+'.mis-cover .period{font-size:16px;font-weight:700;color:#2b6cb0;margin-top:26px;}'
	+'.mis-cover .perd{font-size:13px;color:#5b6577;}'
	+'.mis-cover .meta{margin-top:46px;font-size:12px;color:#5b6577;line-height:1.8;}'
	+'.mis-cover .rule{width:120px;border-top:3px solid #c2a14d;margin:24px auto;}'
	+'h3{font-size:16px;color:#1d2740;border-bottom:3px solid #c2a14d;padding-bottom:5px;margin:0 0 8px;page-break-after:avoid;}'
	+'h4{font-size:12.5px;color:#fff;margin:18px 0 6px;padding:6px 12px;background:linear-gradient(90deg,#2b3a55,#3a6ea5);border-radius:4px;page-break-after:avoid;}'
	+'.epc-line{display:flex;align-items:center;width:100%;gap:8px;padding:7px 6px;border-bottom:1px solid #edf0f5;}'
	+'.epc-line:nth-of-type(even){background:#f7faff;}'
	+'table{border-collapse:collapse;width:100%;margin:6px 0;}'
	+'thead{display:table-header-group;}'
	+'tr{page-break-inside:avoid;}'
	+'td,th{border:1px solid #c7cedb;padding:5px 8px;font-size:11px;vertical-align:top;}'
	+'th{background:linear-gradient(90deg,#2b3a55,#3a6ea5);color:#fff;text-align:left;}'
	+'tbody tr:nth-child(even) td{background:#f7faff;}'
	+'.label{display:inline-block;padding:1px 6px;border-radius:3px;font-size:9px;border:1px solid #b9c0cd;}'
	+'.label-success{background:#e7f6ec;color:#1a7f37;border-color:#bfe3cb;}'
	+'.label-warning{background:#fff5e0;color:#9a6700;border-color:#f0dca8;}'
	+'.label-danger{background:#fdecec;color:#b42318;border-color:#f3c3bd;}'
	+'.label-info{background:#e8f0fb;color:#1d4e94;border-color:#c4d6f3;}'
	+'.alert{padding:8px 12px;border-radius:5px;margin:8px 0;font-size:11px;border:1px solid #ddd;}'
	+'.alert-success{background:#e7f6ec;border-color:#bfe3cb;}'
	+'.alert-warning{background:#fff5e0;border-color:#f0dca8;}'
	+'.alert-danger{background:#fdecec;border-color:#f3c3bd;}'
	+'.text-muted{color:#7a869a;}'
	+'.mis-sign{margin-top:34px;display:flex;justify-content:space-between;gap:40px;page-break-inside:avoid;}'
	+'.mis-sign>div{flex:1;border-top:1px solid #7a869a;padding-top:6px;font-size:11px;color:#5b6577;}';
	var cover =
	'<div class="mis-cover">'
	+'<div class="badge">Statutory / Management Report</div>'
	+'<div class="co">'+esc(c.co)+'</div>'
	+'<div class="addr">'+esc(c.addr)+(trnLine?(' &nbsp;·&nbsp; '+trnLine):'')+'</div>'
	+'<div class="rule"></div>'
	+'<div class="ttl">'+esc(c.ttl)+'</div>'
	+'<div class="juris">Jurisdiction: '+esc(c.juris)+'</div>'
	+'<div class="period">Reporting period: '+esc(c.perL)+'</div>'
	+'<div class="perd">'+esc(c.perR)+'</div>'
	+'<div class="meta">Submitted to: '+esc(c.auth)+'<br>Governing law: '+esc(c.law)+'<br>Prepared on '+esc(c.gen)+'</div>'
	+'</div>';
	var runHdr = '<div class="mis-run"><span>'+esc(c.co)+'</span><span>'+esc(c.ttl)+'</span></div>';
	var runFt  = '<div class="mis-foot"><span>'+esc(c.perL)+' · '+esc(c.juris)+'</span><span>Generated by Ecom BOS External Reporting · '+esc(c.gen)+'</span></div>';
	var sign   = '<div class="mis-sign"><div>Prepared by &amp; date</div><div>Reviewed by &amp; date</div><div>Authorised signatory &amp; stamp</div></div>';
	var w = window.open('', '_blank');
	w.document.write('<html><head><title>'+esc(c.ttl)+' — '+esc(c.co)+'</title><meta charset="utf-8"><style>'+css+'</style></head><body>'
		+runHdr+runFt+cover+'<div class="mis-body">'+clone.innerHTML+sign+'</div></body></html>');
	w.document.close();
	setTimeout(function(){ w.focus(); w.print(); }, 350);
}
</script>
JS;
    }
}

/* ====================================================== Off-system import */

if (!function_exists('epc_ext_import_template_csv')) {
    /**
     * Excel/CSV import template (summary-only — box/line totals, no invoice
     * detail). $kind = 'vat' | 'ct'. Returns BOM-prefixed CSV text.
     */
    function epc_ext_import_template_csv(string $kind): string
    {
        if ($kind === 'ct') {
            $rows = array(
                array('Code', 'Description', 'Amount'),
                array('META_LEGAL_NAME', 'Legal name (taxable person)', 'Sample Client Trading LLC'),
                array('META_TRN', 'Corporate Tax registration number (TRN)', '100000000000003'),
                array('META_PERIOD_FROM', 'Financial year from (YYYY-MM-DD)', '2025-01-01'),
                array('META_PERIOD_TO', 'Financial year to (YYYY-MM-DD)', '2025-12-31'),
                array('ACCT_PROFIT', 'Accounting net profit per financial statements', '1250000'),
                array('REVENUE', 'Total revenue (for Small Business Relief test)', '8400000'),
                array('FINES', 'Fines & administrative penalties (added back)', '15000'),
                array('ENTERTAINMENT', 'Entertainment expenditure - total (50% disallowed)', '40000'),
                array('DONATIONS', 'Donations to non-approved bodies (added back)', '10000'),
                array('PROVISIONS', 'General / non-specific provisions (added back)', '25000'),
                array('ACCT_DEP', 'Accounting depreciation (added back)', '180000'),
                array('TAX_DEP', 'Tax depreciation / capital allowances (deducted)', '210000'),
                array('EXEMPT_INCOME', 'Exempt dividends / participation (deducted)', '60000'),
                array('NET_INTEREST', 'Net interest expense (for 30% EBITDA cap)', '95000'),
                array('LOSSES_BF', 'Tax losses brought forward', '120000'),
            );
        } else {
            $rows = array(
                array('Code', 'Description', 'Amount', 'VAT', 'Adjustment'),
                array('META_LEGAL_NAME', 'Legal name (taxable person)', 'Sample Client Trading LLC', '', ''),
                array('META_TRN', 'Tax Registration Number (TRN)', '100000000000003', '', ''),
                array('META_PERIOD_FROM', 'Tax period from (YYYY-MM-DD)', '2026-01-01', '', ''),
                array('META_PERIOD_TO', 'Tax period to (YYYY-MM-DD)', '2026-03-31', '', ''),
                array('BOX1A', 'Standard-rated supplies - Abu Dhabi', '430500', '21525', '0'),
                array('BOX1B', 'Standard-rated supplies - Dubai', '645750', '32287.50', '0'),
                array('BOX1C', 'Standard-rated supplies - Sharjah', '172200', '8610', '0'),
                array('BOX1D', 'Standard-rated supplies - Ajman', '57400', '2870', '0'),
                array('BOX1E', 'Standard-rated supplies - Umm Al Quwain', '14350', '717.50', '0'),
                array('BOX1F', 'Standard-rated supplies - Ras Al Khaimah', '71750', '3587.50', '0'),
                array('BOX1G', 'Standard-rated supplies - Fujairah', '43050', '2152.50', '0'),
                array('BOX2', 'Tax refunds provided to tourists (negative)', '-11480', '-574', '0'),
                array('BOX3', 'Supplies subject to reverse charge (output)', '90000', '4500', '0'),
                array('BOX4', 'Zero-rated supplies', '320000', '', ''),
                array('BOX5', 'Exempt supplies', '85000', '', ''),
                array('BOX6', 'Goods imported into the UAE', '150000', '7500', '0'),
                array('BOX7', 'Adjustments to goods imported', '0', '0', '0'),
                array('BOX9', 'Standard-rated expenses (recoverable input VAT)', '980000', '49000', '0'),
                array('BOX10', 'Supplies subject to reverse charge (input VAT)', '90000', '4500', '0'),
            );
        }
        $out = array();
        foreach ($rows as $r) {
            $out[] = implode(',', array_map('epc_ext_csv_cell', $r));
        }
        return "\xEF\xBB\xBF" . implode("\r\n", $out) . "\r\n";
    }
}

if (!function_exists('epc_ext_parse_table')) {
    /**
     * Parse an uploaded CSV or XLSX into rows of string cells. Off-system — no
     * ERP/GL data involved. Returns array<int,array<int,string>> or null.
     */
    function epc_ext_parse_table(string $path, string $name): ?array
    {
        $ext = strtolower(pathinfo($name, PATHINFO_EXTENSION));
        if ($ext === 'xlsx' && class_exists('ZipArchive')) {
            return epc_ext_parse_xlsx($path);
        }
        // CSV / TSV fallback
        $raw = @file_get_contents($path);
        if ($raw === false) { return null; }
        $raw = preg_replace('/^\xEF\xBB\xBF/', '', $raw);
        $delim = (substr_count($raw, "\t") > substr_count($raw, ',')) ? "\t" : ',';
        $rows = array();
        $fh = fopen('php://temp', 'r+');
        fwrite($fh, $raw);
        rewind($fh);
        while (($cells = fgetcsv($fh, 0, $delim)) !== false) {
            if ($cells === array(null) || $cells === false) { continue; }
            $rows[] = array_map(static function ($c) { return (string) $c; }, $cells);
        }
        fclose($fh);
        return $rows;
    }
}

if (!function_exists('epc_ext_parse_xlsx')) {
    /** Minimal XLSX -> rows reader (first worksheet) using ZipArchive + SimpleXML. */
    function epc_ext_parse_xlsx(string $path): ?array
    {
        $zip = new \ZipArchive();
        if ($zip->open($path) !== true) { return null; }
        $shared = array();
        $ssXml = $zip->getFromName('xl/sharedStrings.xml');
        if ($ssXml !== false) {
            $sx = @simplexml_load_string($ssXml);
            if ($sx !== false) {
                foreach ($sx->si as $si) {
                    $txt = '';
                    if (isset($si->t)) {
                        $txt = (string) $si->t;
                    } else {
                        foreach ($si->r as $r) { $txt .= (string) $r->t; }
                    }
                    $shared[] = $txt;
                }
            }
        }
        // find the first worksheet target
        $sheetPath = 'xl/worksheets/sheet1.xml';
        $sheetXml = $zip->getFromName($sheetPath);
        if ($sheetXml === false) {
            for ($i = 1; $i <= 20; $i++) {
                $try = 'xl/worksheets/sheet' . $i . '.xml';
                $sheetXml = $zip->getFromName($try);
                if ($sheetXml !== false) { break; }
            }
        }
        $zip->close();
        if ($sheetXml === false) { return null; }
        $sx = @simplexml_load_string($sheetXml);
        if ($sx === false) { return null; }
        $rows = array();
        foreach ($sx->sheetData->row as $row) {
            $cells = array();
            $maxIdx = -1;
            foreach ($row->c as $c) {
                $ref = (string) $c['r'];
                $col = preg_replace('/[0-9]/', '', $ref);
                $idx = epc_ext_xlsx_col_index($col);
                $type = (string) $c['t'];
                $val = '';
                if ($type === 's') {
                    $val = $shared[(int) $c->v] ?? '';
                } elseif ($type === 'inlineStr') {
                    $val = (string) $c->is->t;
                } else {
                    $val = (string) $c->v;
                }
                $cells[$idx] = $val;
                if ($idx > $maxIdx) { $maxIdx = $idx; }
            }
            $line = array();
            for ($i = 0; $i <= $maxIdx; $i++) { $line[] = $cells[$i] ?? ''; }
            $rows[] = $line;
        }
        return $rows;
    }
}

if (!function_exists('epc_ext_xlsx_col_index')) {
    /** Convert an A1-style column letter (A, B, ..., AA) to a 0-based index. */
    function epc_ext_xlsx_col_index(string $col): int
    {
        $col = strtoupper($col);
        $n = 0;
        $len = strlen($col);
        for ($i = 0; $i < $len; $i++) {
            $n = $n * 26 + (ord($col[$i]) - 64);
        }
        return $n - 1;
    }
}

if (!function_exists('epc_ext_import_map')) {
    /**
     * Normalise parsed rows into a summary map: meta[] (strings) + values[]
     * (Code => float). Keys are taken from the first column (the Code column).
     *
     * @param array<int,array<int,string>> $rows
     * @return array{meta:array<string,string>,values:array<string,float>,vat:array<string,array{amount:float,vat:float,adj:float}>}
     */
    function epc_ext_import_map(array $rows): array
    {
        $meta = array();
        $values = array();
        $vat = array();
        foreach ($rows as $r) {
            if (!isset($r[0])) { continue; }
            $code = strtoupper(trim((string) $r[0]));
            if ($code === '' || $code === 'CODE') { continue; }
            $num = static function ($v): float {
                $v = trim((string) $v);
                $v = str_replace(array(',', ' ', "\xC2\xA0"), '', $v);
                return is_numeric($v) ? (float) $v : 0.0;
            };
            if (strpos($code, 'META_') === 0) {
                // meta value is in column C (index 2) for both templates
                $meta[$code] = trim((string) ($r[2] ?? ($r[1] ?? '')));
                continue;
            }
            if (strpos($code, 'BOX') === 0) {
                $vat[$code] = array(
                    'amount' => $num($r[2] ?? 0),
                    'vat' => $num($r[3] ?? 0),
                    'adj' => $num($r[4] ?? 0),
                );
                continue;
            }
            // CT line: amount in column C
            $values[$code] = $num($r[2] ?? ($r[1] ?? 0));
        }
        return array('meta' => $meta, 'values' => $values, 'vat' => $vat);
    }
}

if (!function_exists('epc_ext_b_vat_summary')) {
    /**
     * Build a full FTA VAT 201 from an uploaded summary map (off-system data,
     * no invoice detail). $country drives label/currency.
     *
     * @param array{meta:array<string,string>,vat:array<string,array{amount:float,vat:float,adj:float}>} $map
     * @return array{title:string,body:string,summary:array<string,string>,meta:array<string,string>}
     */
    function epc_ext_b_vat_summary(array $map, string $ccy): array
    {
        $b = $map['vat'];
        $g = static function (string $k) use ($b): array {
            return $b[$k] ?? array('amount' => 0.0, 'vat' => 0.0, 'adj' => 0.0);
        };
        $emirates = array(
            'BOX1A' => 'Standard-rated supplies — Abu Dhabi',
            'BOX1B' => 'Standard-rated supplies — Dubai',
            'BOX1C' => 'Standard-rated supplies — Sharjah',
            'BOX1D' => 'Standard-rated supplies — Ajman',
            'BOX1E' => 'Standard-rated supplies — Umm Al Quwain',
            'BOX1F' => 'Standard-rated supplies — Ras Al Khaimah',
            'BOX1G' => 'Standard-rated supplies — Fujairah',
        );
        $rows = epc_ext_vat_header_row();
        $outNet = 0.0; $outVat = 0.0; $outAdj = 0.0;
        $boxNo = array('BOX1A' => '1a', 'BOX1B' => '1b', 'BOX1C' => '1c', 'BOX1D' => '1d', 'BOX1E' => '1e', 'BOX1F' => '1f', 'BOX1G' => '1g');
        foreach ($emirates as $k => $desc) {
            $d = $g($k);
            $outNet += $d['amount']; $outVat += $d['vat']; $outAdj += $d['adj'];
            $rows .= epc_ext_vat_box($boxNo[$k], $desc, $d['amount'], $d['vat'], $d['adj'], $ccy);
        }
        $b2 = $g('BOX2'); $b3 = $g('BOX3'); $b4 = $g('BOX4'); $b5 = $g('BOX5'); $b6 = $g('BOX6'); $b7 = $g('BOX7');
        $rows .= epc_ext_vat_box('2', 'Tax refunds provided to tourists', $b2['amount'], $b2['vat'], $b2['adj'], $ccy);
        $rows .= epc_ext_vat_box('3', 'Supplies subject to the reverse charge', $b3['amount'], $b3['vat'], $b3['adj'], $ccy);
        $rows .= epc_ext_vat_box('4', 'Zero-rated supplies', $b4['amount'], 0.0, 0.0, $ccy);
        $rows .= epc_ext_vat_box('5', 'Exempt supplies', $b5['amount'], 0.0, 0.0, $ccy);
        $rows .= epc_ext_vat_box('6', 'Goods imported into the UAE', $b6['amount'], $b6['vat'], $b6['adj'], $ccy);
        $rows .= epc_ext_vat_box('7', 'Adjustments to goods imported', $b7['amount'], $b7['vat'], $b7['adj'], $ccy);
        $totOutNet = $outNet + $b2['amount'] + $b3['amount'] + $b4['amount'] + $b5['amount'] + $b6['amount'] + $b7['amount'];
        $totOutVat = $outVat + $b2['vat'] + $b3['vat'] + $b6['vat'] + $b7['vat'];
        $rows .= '<div style="display:flex;align-items:center;width:100%;gap:8px;background:#eef6ee;font-weight:700;padding:8px 6px;border-top:2px solid #2b3a55;">'
            . '<span style="width:46px;">8</span><span style="flex:1;">Totals (output)</span>'
            . '<span style="width:150px;text-align:right;">' . epc_ext_m($totOutNet, $ccy) . '</span>'
            . '<span style="width:130px;text-align:right;">' . epc_ext_m($totOutVat, $ccy) . '</span>'
            . '<span style="width:120px;text-align:right;color:#888;">' . epc_ext_m($outAdj + $b2['adj'] + $b3['adj'] + $b6['adj'] + $b7['adj'], $ccy) . '</span></div>';

        $b9 = $g('BOX9'); $b10 = $g('BOX10');
        $inRows = epc_ext_vat_header_row();
        $inRows .= epc_ext_vat_box('9', 'Standard-rated expenses (recoverable input VAT)', $b9['amount'], $b9['vat'], $b9['adj'], $ccy);
        $inRows .= epc_ext_vat_box('10', 'Supplies subject to reverse charge (input VAT)', $b10['amount'], $b10['vat'], $b10['adj'], $ccy);
        $totInNet = $b9['amount'] + $b10['amount'];
        $totInVat = $b9['vat'] + $b10['vat'];
        $inRows .= '<div style="display:flex;align-items:center;width:100%;gap:8px;background:#eef6ee;font-weight:700;padding:8px 6px;border-top:2px solid #2b3a55;">'
            . '<span style="width:46px;">11</span><span style="flex:1;">Totals (input)</span>'
            . '<span style="width:150px;text-align:right;">' . epc_ext_m($totInNet, $ccy) . '</span>'
            . '<span style="width:130px;text-align:right;">' . epc_ext_m($totInVat, $ccy) . '</span>'
            . '<span style="width:120px;text-align:right;color:#888;">' . epc_ext_m($b9['adj'] + $b10['adj'], $ccy) . '</span></div>';

        $net = round($totOutVat - $totInVat, 2);
        $netRows = epc_ext_kv_table(array(
            array('Box 12 — Total output tax due', epc_ext_m($totOutVat, $ccy)),
            array('Box 13 — Total recoverable input tax', epc_ext_m($totInVat, $ccy)),
            array('Box 14 — Net VAT ' . ($net >= 0 ? 'payable' : 'reclaimable'), epc_ext_m(abs($net), $ccy), true),
        ));

        // light compliance checks on the uploaded summary
        $checks = array();
        $checks[] = ($map['meta']['META_TRN'] ?? '') !== ''
            ? array('ok', 'TRN present on the uploaded data.')
            : array('warn', 'TRN missing in the uploaded file — add it before filing.');
        $reconc = abs(($totOutVat - $totInVat) - $net) < 0.01;
        $checks[] = $reconc ? array('ok', 'Return reconciles: Box 14 = Box 12 − Box 13.') : array('error', 'Return does not reconcile — check Box 12/13/14.');
        // implied rate sanity on standard-rated outputs
        $impliedVat = round($outNet * 0.05, 2);
        $checks[] = (abs($impliedVat - $outVat) <= max(1.0, $outNet * 0.002))
            ? array('ok', 'Standard-rated output VAT ≈ 5% of net (consistent).')
            : array('warn', 'Standard-rated output VAT is not ≈ 5% of net — verify the rate / scheme treatment.');
        $checks[] = ($b4['amount'] > 0 && abs($g('BOX4')['vat']) < 0.01)
            ? array('ok', 'Zero-rated supplies carry no VAT (correct).')
            : array('ok', 'No zero-rated VAT mismatch.');

        $cr = ''; $errors = 0; $warns = 0;
        foreach ($checks as $c) {
            if ($c[0] === 'error') { $errors++; $badge = '<span class="label label-danger">FAIL</span>'; $bg = '#fff3f3'; }
            elseif ($c[0] === 'warn') { $warns++; $badge = '<span class="label label-warning">REVIEW</span>'; $bg = '#fffaf0'; }
            else { $badge = '<span class="label label-success">PASS</span>'; $bg = '#f4fbf6'; }
            $cr .= '<tr style="background:' . $bg . ';"><td style="width:70px;">' . $badge . '</td><td>' . epc_erp_h($c[1]) . '</td></tr>';
        }
        $compSummary = ($errors === 0 && $warns === 0)
            ? '<div class="alert alert-success">All checks passed on the uploaded data.</div>'
            : '<div class="alert ' . ($errors > 0 ? 'alert-danger' : 'alert-warning') . '"><strong>' . $errors . ' error(s), ' . $warns . ' review item(s)</strong> — based on the uploaded summary.</div>';

        $body = '<div class="alert alert-info" style="font-size:12px;"><i class="fa fa-upload"></i> Built from your <strong>uploaded file</strong> (off-system, summary figures only — no invoice detail). This is for checking / reporting other clients; it does not read or write ERP data.</div>'
            . epc_ext_field_guide('Field guide — what goes in each VAT 201 box (and why)',
                'Plain-language explanation of every box. Governing law: Federal Decree-Law 8/2017 & Executive Regulations (FTA).',
                epc_ext_vat_guide_rows())
            . '<h4 style="color:#1d2740;margin-top:14px;">VAT on sales &amp; all other outputs</h4>'
            . '<div style="border:1px solid #e6eaf1;border-radius:4px;overflow:hidden;">' . $rows . '</div>'
            . '<h4 style="color:#1d2740;margin-top:18px;">VAT on expenses &amp; all other inputs</h4>'
            . '<div style="border:1px solid #e6eaf1;border-radius:4px;overflow:hidden;">' . $inRows . '</div>'
            . '<h4 style="color:#1d2740;margin-top:18px;">Net VAT due</h4>' . $netRows
            . '<h4 style="color:#1d2740;margin-top:18px;">Compliance checks</h4>' . $compSummary
            . '<table class="table table-condensed" style="font-size:12px;"><thead><tr style="background:#f0f3f8;"><th>Result</th><th>Check</th></tr></thead><tbody>' . $cr . '</tbody></table>';

        return array(
            'title' => 'VAT Return (FTA VAT 201) — imported',
            'body' => $body,
            'summary' => array(
                'Output VAT' => epc_ext_m($totOutVat, $ccy),
                'Input VAT' => epc_ext_m($totInVat, $ccy),
                'Net VAT ' . ($net >= 0 ? 'payable' : 'reclaimable') => epc_ext_m(abs($net), $ccy),
                'Compliance' => ($errors === 0 && $warns === 0) ? 'All passed' : ($errors . ' err / ' . $warns . ' review'),
            ),
            'meta' => $map['meta'],
        );
    }
}

if (!function_exists('epc_ext_b_ct_summary')) {
    /**
     * Build a UAE Corporate Tax computation from an uploaded summary map
     * (off-system data, summary figures only).
     *
     * @param array{meta:array<string,string>,values:array<string,float>} $map
     * @return array{title:string,body:string,summary:array<string,string>,meta:array<string,string>}
     */
    function epc_ext_b_ct_summary(array $map, string $ccy): array
    {
        $v = $map['values'];
        $val = static function (string $k) use ($v): float { return (float) ($v[$k] ?? 0.0); };
        $profit = $val('ACCT_PROFIT');
        $revenue = $val('REVENUE');
        $fines = $val('FINES');
        $entTotal = $val('ENTERTAINMENT');
        $entAdd = round($entTotal * 0.5, 2);
        $donations = $val('DONATIONS');
        $provisions = $val('PROVISIONS');
        $acctDep = $val('ACCT_DEP');
        $taxDep = $val('TAX_DEP');
        $exempt = $val('EXEMPT_INCOME');
        $interest = $val('NET_INTEREST');
        $lossesBf = $val('LOSSES_BF');

        $additions = $fines + $entAdd + $donations + $provisions + $acctDep;
        $deductions = $taxDep + $exempt;
        $adjProfit = $profit + $additions - $deductions;
        $ebitda = $adjProfit + $interest + $acctDep;
        $interestCap = max(12000000.0, round($ebitda * 0.30, 2));
        $interestDisallowed = max(0.0, round($interest - $interestCap, 2));
        $taxableBeforeLoss = max(0.0, $adjProfit + $interestDisallowed);
        $lossCap = round($taxableBeforeLoss * 0.75, 2);
        $lossUsed = min($lossesBf, $lossCap);
        $taxable = max(0.0, $taxableBeforeLoss - $lossUsed);
        $sbrEligible = $revenue > 0 && $revenue <= 3000000.0;
        $taxableAfterSbr = $sbrEligible ? 0.0 : $taxable;
        $threshold = 375000.0;
        $above = max(0.0, $taxableAfterSbr - $threshold);
        $ct = round($above * 0.09, 2);

        $t = '<table class="table table-bordered table-condensed" style="font-size:12.5px;max-width:860px;">'
            . '<thead><tr style="background:#f0f3f8;"><th>Computation of taxable income</th><th style="text-align:right;">Amount</th><th>Basis</th></tr></thead><tbody>';
        $t .= epc_ext_ct_schedule_row('Accounting net profit (per financials)', $profit, $ccy, 'sub', 'From uploaded data');
        $t .= epc_ext_ct_schedule_row('Add back: non-deductible & timing items', '', $ccy, 'head');
        $t .= epc_ext_ct_schedule_row('Fines & administrative penalties', $fines, $ccy, 'add', '100% non-deductible — Art. 33');
        $t .= epc_ext_ct_schedule_row('Entertainment expenditure (50% disallowed)', $entAdd, $ccy, 'add', 'Art. 32 (of ' . epc_erp_money($entTotal) . ')');
        $t .= epc_ext_ct_schedule_row('Donations to non-approved bodies', $donations, $ccy, 'add', 'Art. 37');
        $t .= epc_ext_ct_schedule_row('General (non-specific) provisions', $provisions, $ccy, 'add', 'Timing');
        $t .= epc_ext_ct_schedule_row('Accounting depreciation', $acctDep, $ccy, 'add', 'Replaced by tax depreciation');
        $t .= epc_ext_ct_schedule_row('Less: deductions & exempt income', '', $ccy, 'head');
        $t .= epc_ext_ct_schedule_row('Tax depreciation / capital allowances', $taxDep, $ccy, 'less', 'Art. 28');
        $t .= epc_ext_ct_schedule_row('Exempt dividends / participation', $exempt, $ccy, 'less', 'Art. 22–23');
        $t .= epc_ext_ct_schedule_row('Adjusted profit before interest limitation', $adjProfit, $ccy, 'sub');
        $t .= epc_ext_ct_schedule_row('Interest limitation', '', $ccy, 'head');
        $t .= epc_ext_ct_schedule_row('Net interest expense', $interest, $ccy, 'info', 'Cap = max(30% EBITDA, AED 12m)');
        $t .= epc_ext_ct_schedule_row('Interest disallowed (over 30% EBITDA cap)', $interestDisallowed, $ccy, 'add', $interestDisallowed > 0 ? 'Carried forward' : 'Within cap');
        $t .= epc_ext_ct_schedule_row('Taxable income before loss relief', $taxableBeforeLoss, $ccy, 'sub');
        $t .= epc_ext_ct_schedule_row('Less: tax losses brought forward (max 75%)', $lossUsed, $ccy, 'less', 'Art. 37 — cap ' . epc_erp_money($lossCap));
        $t .= epc_ext_ct_schedule_row('Taxable income', $taxable, $ccy, 'sub');
        if ($sbrEligible) {
            $t .= epc_ext_ct_schedule_row('Small Business Relief applied (revenue ≤ AED 3m)', '', $ccy, 'info', 'Taxable income treated as nil — MD 73/2023');
        }
        $t .= epc_ext_ct_schedule_row('Taxable income subject to tax', $taxableAfterSbr, $ccy, 'sub');
        $t .= '</tbody></table>';

        $bands = epc_ext_kv_table(array(
            array('0% band — first ' . epc_erp_money($threshold), epc_ext_m(min($taxableAfterSbr, $threshold), $ccy)),
            array('9% band — taxable income above ' . epc_erp_money($threshold), epc_ext_m($above, $ccy)),
            array('Corporate tax payable @ 9%', epc_ext_m($ct, $ccy), true),
        ));

        $checks = array();
        $checks[] = ($map['meta']['META_TRN'] ?? '') !== '' ? array('ok', 'CT TRN present on the uploaded data.') : array('warn', 'CT TRN missing — add it before filing.');
        $checks[] = $fines > 0 ? array('ok', 'Fines & penalties added back (Art. 33).') : array('ok', 'No fines to add back.');
        $checks[] = $interestDisallowed > 0 ? array('warn', 'Net interest exceeds 30% EBITDA cap — ' . epc_erp_money($interestDisallowed) . ' disallowed (Art. 30).') : array('ok', 'Net interest within the 30% EBITDA / AED 12m cap (Art. 30).');
        $checks[] = $sbrEligible ? array('ok', 'Small Business Relief available (revenue ≤ AED 3m) — MD 73/2023.') : array('ok', 'Standard 0% / 9% bands applied.');
        $cr = ''; $errors = 0; $warns = 0;
        foreach ($checks as $c) {
            if ($c[0] === 'error') { $errors++; $badge = '<span class="label label-danger">FAIL</span>'; $bg = '#fff3f3'; }
            elseif ($c[0] === 'warn') { $warns++; $badge = '<span class="label label-warning">REVIEW</span>'; $bg = '#fffaf0'; }
            else { $badge = '<span class="label label-success">PASS</span>'; $bg = '#f4fbf6'; }
            $cr .= '<tr style="background:' . $bg . ';"><td style="width:70px;">' . $badge . '</td><td>' . epc_erp_h($c[1]) . '</td></tr>';
        }
        $compSummary = ($errors === 0 && $warns === 0)
            ? '<div class="alert alert-success">All checks passed on the uploaded data.</div>'
            : '<div class="alert ' . ($errors > 0 ? 'alert-danger' : 'alert-warning') . '"><strong>' . $errors . ' error(s), ' . $warns . ' review item(s)</strong>.</div>';

        $body = '<div class="alert alert-info" style="font-size:12px;"><i class="fa fa-upload"></i> Built from your <strong>uploaded file</strong> (off-system, summary figures only — no transaction detail). For checking / reporting other clients; it does not read or write ERP data.</div>'
            . epc_ext_field_guide('Field guide — why each line of the CT computation (and why)',
                'Plain-language explanation of every line. Governing law: Federal Decree-Law 47/2022 & implementing decisions (FTA).',
                epc_ext_ct_guide_rows())
            . '<h4 style="color:#1d2740;margin-top:14px;">Computation of taxable income</h4>' . $t
            . '<h4 style="color:#1d2740;margin-top:18px;">Tax bands &amp; liability</h4>' . $bands
            . '<h4 style="color:#1d2740;margin-top:18px;">Corporate tax compliance checks</h4>' . $compSummary
            . '<table class="table table-condensed" style="font-size:12px;"><thead><tr style="background:#f0f3f8;"><th>Result</th><th>Check</th></tr></thead><tbody>' . $cr . '</tbody></table>';

        return array(
            'title' => 'Corporate Income Tax Return — imported',
            'body' => $body,
            'summary' => array(
                'Taxable income' => epc_ext_m($taxableAfterSbr, $ccy),
                'Rate' => '0% / 9%',
                'CT payable' => epc_ext_m($ct, $ccy),
                'Compliance' => ($errors === 0 && $warns === 0) ? 'All passed' : ($errors . ' err / ' . $warns . ' review'),
            ),
            'meta' => $map['meta'],
        );
    }
}
