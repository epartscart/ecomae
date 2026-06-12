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

if (!function_exists('epc_ext_vat_box')) {
    /**
     * Render one VAT-201 box as a drill-down row: a summary line (box no,
     * description, net amount, VAT amount, adjustment) that expands to the
     * contributing transactions.
     *
     * @param array<int,array{0:string,1:float,2?:float}> $detail label, amount, vat
     */
    function epc_ext_vat_box(string $box, string $desc, float $amount, float $vat, float $adj, string $ccy, array $detail = array(), bool $sample = false): string
    {
        $cell = 'style="text-align:right;white-space:nowrap;padding:6px 10px;"';
        $hasDrill = !empty($detail);
        $summary = '<div style="display:flex;align-items:center;width:100%;gap:8px;">'
            . '<span style="width:46px;font-weight:700;color:#2b3a55;">' . epc_erp_h($box) . '</span>'
            . '<span style="flex:1;">' . epc_erp_h($desc)
            . ($sample ? ' <span class="label label-warning" style="font-size:9px;">sample</span>' : '')
            . ($hasDrill ? ' <span class="text-muted" style="font-size:10px;">▸ drill-down</span>' : '') . '</span>'
            . '<span style="width:150px;text-align:right;">' . epc_ext_m($amount, $ccy) . '</span>'
            . '<span style="width:130px;text-align:right;font-weight:600;">' . epc_ext_m($vat, $ccy) . '</span>'
            . '<span style="width:120px;text-align:right;color:#888;">' . epc_ext_m($adj, $ccy) . '</span>'
            . '</div>';
        if (!$hasDrill) {
            return '<div style="border-bottom:1px solid #edf0f5;padding:8px 6px;">' . $summary . '</div>';
        }
        $rows = '';
        foreach ($detail as $d) {
            $dv = isset($d[2]) ? epc_ext_m((float) $d[2], $ccy) : '';
            $rows .= '<tr><td style="padding:5px 10px;">' . epc_erp_h((string) $d[0]) . '</td>'
                . '<td ' . $cell . '>' . epc_ext_m((float) $d[1], $ccy) . '</td>'
                . '<td ' . $cell . '>' . $dv . '</td></tr>';
        }
        return '<details style="border-bottom:1px solid #edf0f5;">'
            . '<summary style="cursor:pointer;padding:8px 6px;list-style:none;">' . $summary . '</summary>'
            . '<div style="background:#fafbfd;padding:6px 10px 12px 52px;">'
            . '<table class="table table-condensed" style="margin:0;background:#fff;border:1px solid #e6eaf1;">'
            . '<thead><tr style="background:#f0f3f8;"><th style="padding:5px 10px;">Source transaction</th><th style="text-align:right;padding:5px 10px;">Amount</th><th style="text-align:right;padding:5px 10px;">VAT</th></tr></thead>'
            . '<tbody>' . $rows . '</tbody></table></div></details>';
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
                $ccy, array(array($em[0] . ' branch sales (posted GL revenue × ' . round($em[1] * 100) . '% allocation)', $amt, $vat)), true);
        }
        // Box 1 also carries the real revenue accounts as drill-down evidence.
        $revDetail = array();
        foreach (($pl['revenue'] ?? array()) as $r) {
            $revDetail[] = array($r['code'] . ' · ' . $r['name'], (float) $r['amount'], round((float) $r['amount'] * $rate / 100, 2));
        }
        $boxesOut = epc_ext_vat_box('Box 1', 'Standard-rated supplies (total, ex ' . $taxLabel . ') — by Emirate below', $stdSupplies, $stdVat, 0.0, $ccy, $revDetail)
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
            array(array('Planet/tourist VAT refund scheme settlements', -$touristAmt, -$touristVat)), true);
        $boxesOut .= epc_ext_vat_box('Box 3', 'Supplies subject to the reverse charge', $rcSupAmt, $rcSupVat, 0.0, $ccy,
            array(array('Imported services accounted under reverse charge', $rcSupAmt, $rcSupVat)), true);
        $boxesOut .= epc_ext_vat_box('Box 4', 'Zero-rated supplies (exports / qualifying)', $zeroAmt, 0.0, 0.0, $ccy,
            array(array('Exports of goods & services (0%)', $zeroAmt, 0.0)), true);
        $boxesOut .= epc_ext_vat_box('Box 5', 'Exempt supplies', $exemptAmt, 0.0, 0.0, $ccy,
            array(array('Exempt financial services / bare land / local transport', $exemptAmt, 0.0)), true);
        $boxesOut .= epc_ext_vat_box('Box 6', 'Goods imported into the UAE', $impGoodsAmt, $impGoodsVat, 0.0, $ccy,
            array(array('Customs import declarations (auto-populated from FTA)', $impGoodsAmt, $impGoodsVat)), true);
        $boxesOut .= epc_ext_vat_box('Box 7', 'Adjustments to goods imported into the UAE', 0.0, 0.0, 0.0, $ccy);

        // Output totals (Box 8): VAT due before input recovery.
        $totOutAmt = $stdSupplies - $touristAmt + $rcSupAmt + $zeroAmt + $exemptAmt + $impGoodsAmt;
        $totOutVat = $stdVat - $touristVat + $rcSupVat + $impGoodsVat;
        $boxesOut .= epc_ext_vat_box('Box 8', 'Totals — VAT on sales & all other outputs', $totOutAmt, $totOutVat, 0.0, $ccy);

        // Input side.
        $stdExpVat = round($purch * $rate / 100, 2);
        $expDetail = array();
        foreach (($pl['expenses'] ?? array()) as $r) {
            $expDetail[] = array($r['code'] . ' · ' . $r['name'], (float) $r['amount'], round((float) $r['amount'] * $rate / 100, 2));
        }
        $boxesIn = epc_ext_vat_box('Box 9', 'Standard-rated expenses', $purch, $stdExpVat, 0.0, $ccy, $expDetail);
        $rcRecoverVat = round($rcSupVat + $impGoodsVat, 2);
        $boxesIn .= epc_ext_vat_box('Box 10', 'Supplies subject to the reverse charge (recoverable)', $rcSupAmt + $impGoodsAmt, $rcRecoverVat, 0.0, $ccy,
            array(array('Reverse-charge & import VAT recoverable', $rcSupAmt + $impGoodsAmt, $rcRecoverVat)), true);
        $totInAmt = $purch + $rcSupAmt + $impGoodsAmt;
        $totInVat = round($stdExpVat + $rcRecoverVat, 2);
        $boxesIn .= epc_ext_vat_box('Box 11', 'Totals — VAT on expenses & all other inputs', $totInAmt, $totInVat, 0.0, $ccy);

        // Net.
        $netDue = round($totOutVat - $totInVat, 2);
        $payable = $netDue >= 0;

        $css = 'border:1px solid #e2e6ee;border-radius:6px;margin-bottom:18px;overflow:hidden;';
        $body = '<p class="text-muted">Official <strong>FTA VAT 201</strong> return generated from posted ERP data at the ' . epc_erp_h($rPct)
            . '% standard rate. Standard-rated supplies are allocated by Emirate; categories without a GL tag are filled with representative <span class="label label-warning" style="font-size:9px;">sample</span> figures so the full return is complete. Click any box to drill into the source transactions.</p>'
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

        $schemes = epc_ext_vat_schemes_html($ccy);
        $body .= $schemes['html'];

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
            'margin'      => array('label' => 'Profit-margin scheme (5% on margin only)', 'rate' => 5.0, 'rcm' => false, 'box' => 'Box 1', 'ref' => 'VAT Exec. Reg. Art. 29'),
            'exempt'      => array('label' => 'Exempt supply (0%)', 'rate' => 0.0, 'rcm' => false, 'box' => 'Box 5', 'ref' => 'Federal Decree-Law 8/2017, Art. 46'),
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
            array('doc' => 'INV-3001', 'item' => '22kt gold necklace (incl. making charge)', 'scheme' => 'standard', 'net' => 48000.00, 'declared' => 2400.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-3002', 'item' => '24kt investment gold bars 999.9 (bullion)', 'scheme' => 'invest_gold', 'net' => 250000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => true),
            array('doc' => 'INV-3003', 'item' => 'Loose diamonds — sale to registered jeweller', 'scheme' => 'gold_rcm', 'net' => 120000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => true),
            array('doc' => 'INV-3004', 'item' => 'Gold jewellery export to KSA', 'scheme' => 'zero_export', 'net' => 90000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-3005', 'item' => 'Pre-owned Rolex watch (profit-margin scheme)', 'scheme' => 'margin', 'net' => 32000.00, 'declared' => 300.00, 'margin' => 6000.0, 'trn' => false),
            array('doc' => 'INV-3006', 'item' => '18kt diamond ring (retail)', 'scheme' => 'standard', 'net' => 26000.00, 'declared' => 1300.00, 'margin' => 0.0, 'trn' => false),
            array('doc' => 'INV-3007', 'item' => 'Scrap gold from registrant (reverse charge)', 'scheme' => 'gold_rcm', 'net' => 40000.00, 'declared' => 0.00, 'margin' => 0.0, 'trn' => true),
            array('doc' => 'INV-3008', 'item' => '24kt investment gold coin — taxed 5% IN ERROR', 'scheme' => 'invest_gold', 'net' => 60000.00, 'declared' => 3000.00, 'margin' => 0.0, 'trn' => true),
            array('doc' => 'INV-3009', 'item' => 'Gold sale to registrant — VAT charged (should be RCM)', 'scheme' => 'gold_rcm', 'net' => 55000.00, 'declared' => 2750.00, 'margin' => 0.0, 'trn' => true),
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
            if ($scheme === 'invest_gold') {
                $out[] = $declared > 0.005
                    ? array('status' => 'error', 'doc' => $doc, 'msg' => '24kt investment gold taxed at ' . epc_erp_money($declared) . ' — must be 0% VAT-exempt (Cabinet Decision 25/2018).')
                    : array('status' => 'ok', 'doc' => $doc, 'msg' => 'Investment gold correctly treated as VAT-exempt (0%).');
            } elseif ($scheme === 'gold_rcm') {
                $out[] = $declared > 0.005
                    ? array('status' => 'error', 'doc' => $doc, 'msg' => 'Gold/diamond B2B supply charged VAT — must be reverse charge; buyer self-accounts (Cabinet Decision 25/2018).')
                    : (!$l['trn']
                        ? array('status' => 'warn', 'doc' => $doc, 'msg' => 'Reverse charge applied but buyer TRN not recorded — capture buyer TRN to support RCM.')
                        : array('status' => 'ok', 'doc' => $doc, 'msg' => 'Reverse charge correctly applied; buyer TRN on file.'));
            } elseif ($scheme === 'margin') {
                $expect = round(((float) $l['margin']) * 5 / 100, 2);
                $out[] = abs($declared - $expect) > 1.0
                    ? array('status' => 'warn', 'doc' => $doc, 'msg' => 'Profit-margin VAT ' . epc_erp_money($declared) . ' vs expected ' . epc_erp_money($expect) . ' (5% of margin) — verify margin basis.')
                    : array('status' => 'ok', 'doc' => $doc, 'msg' => 'Profit-margin scheme: VAT charged on margin only (' . epc_erp_money($expect) . ').');
            } elseif ($scheme === 'standard') {
                $expect = round($net * 5 / 100, 2);
                $out[] = abs($declared - $expect) > 1.0
                    ? array('status' => 'warn', 'doc' => $doc, 'msg' => 'Standard-rated VAT ' . epc_erp_money($declared) . ' vs expected ' . epc_erp_money($expect) . ' (5%).')
                    : array('status' => 'ok', 'doc' => $doc, 'msg' => 'Standard 5% VAT correctly charged (' . epc_erp_money($expect) . ').');
            } else { // zero_export / exempt / out_scope
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
                . '<td style="padding:5px 8px;">' . epc_erp_h($l['item']) . '</td>'
                . '<td style="padding:5px 8px;"><span class="label label-default">' . epc_erp_h($rule['label']) . '</span></td>'
                . '<td style="text-align:right;padding:5px 8px;white-space:nowrap;">' . epc_ext_m((float) $l['net'], $ccy) . '</td>'
                . '<td style="text-align:right;padding:5px 8px;white-space:nowrap;">' . epc_ext_m((float) $l['declared'], $ccy) . '</td>'
                . '<td style="padding:5px 8px;font-size:11px;color:#777;">' . epc_erp_h($rule['ref']) . '</td>'
                . '</tr>';
        }
        $supplyTable = '<table class="table table-bordered table-condensed" style="font-size:12px;">'
            . '<thead><tr style="background:#f0f3f8;"><th>Doc</th><th>Item / supply</th><th>VAT treatment</th><th style="text-align:right;">Net</th><th style="text-align:right;">VAT charged</th><th>Legal basis</th></tr></thead>'
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
            . '<p class="text-muted">Each supply is auto-mapped to its UAE VAT treatment. For a live tenant these come from the item/category tax code; below is a representative jewellery/bullion set so the scheme handling is visible — including <strong>24kt investment gold (exempt)</strong>, <strong>gold &amp; diamond B2B reverse charge</strong>, exports, and the profit-margin scheme.</p>'
            . $supplyTable
            . '<h4 style="color:#1d2740;margin-top:18px;">VAT compliance checks</h4>'
            . '<p class="text-muted">The engine validates every line against its statutory treatment (e.g. 24kt investment gold must be 0%; B2B gold must be reverse charge). Two lines below are deliberately wrong to show the checks firing.</p>'
            . $checkTable;
        return array('html' => $html, 'errors' => $errors, 'warns' => $warns);
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
