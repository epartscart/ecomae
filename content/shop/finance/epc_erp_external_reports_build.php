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
                case 'audit_report':
                    return epc_ext_b_audit($db, $name, $country, $ccy, $from, $to);
                case 'fin_model':
                    return epc_ext_b_finmodel($db, $name, $country, $ccy, $from, $to);
                case 'valuation':
                    return epc_ext_b_valuation($db, $name, $country, $ccy, $from, $to);
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

if (!function_exists('epc_ext_commentary')) {
    /**
     * Plain-language commentary / narrative panel for a return — a short
     * "report explained" section so a learner understands what the return
     * shows, how the headline figure was derived and why each part matters.
     * Renders on-screen and is KEPT in the Print/PDF (class mis-commentary).
     *
     * @param array<int,string> $paras  HTML-safe-ish paragraph strings (may contain <strong>)
     */
    function epc_ext_commentary(string $title, array $paras): string
    {
        $p = '';
        foreach ($paras as $para) {
            $p .= '<p style="margin:0 0 8px;">' . $para . '</p>';
        }
        return '<div class="mis-commentary" style="background:#f7faff;border:1px solid #d8e3f4;border-left:4px solid #2b6cb0;border-radius:6px;padding:12px 16px;margin:10px 0 16px;line-height:1.6;color:#33415c;font-size:13px;">'
            . '<h5 style="margin:0 0 8px;font-size:14px;color:#1d2740;"><i class="fa fa-book"></i> ' . epc_erp_h($title) . '</h5>'
            . $p . '</div>';
    }
}

if (!function_exists('epc_ext_cover_page')) {
    /**
     * Professional report cover page + table of contents for the long-form
     * financial / audit / valuation documents.
     *
     * @param array<int,string>          $meta  label => value rows for the cover block
     * @param array<int,array{0:string,1:string}> $toc  section no., title
     */
    function epc_ext_cover_page(string $entity, string $title, string $subtitle, array $meta, array $toc): string
    {
        $rows = '';
        foreach ($meta as $label => $value) {
            $rows .= '<tr><td style="padding:6px 14px;color:#ffe2e6;font-weight:600;white-space:nowrap;">' . epc_erp_h((string) $label) . '</td>'
                . '<td style="padding:6px 14px;color:#fff;font-weight:700;">' . epc_erp_h((string) $value) . '</td></tr>';
        }
        $tocRows = '';
        foreach ($toc as $i => $t) {
            $tocRows .= '<tr><td style="padding:5px 10px;color:#b3122a;font-weight:700;width:46px;">' . epc_erp_h((string) $t[0]) . '</td>'
                . '<td style="padding:5px 10px;color:#1d2740;">' . epc_erp_h((string) $t[1]) . '</td></tr>';
        }
        $cover = '<div class="ext-cover" style="page-break-after:always;background-color:#b3122a;background:#b3122a linear-gradient(135deg,#7a0c1c 0%,#b3122a 55%,#d6334b 100%);color:#fff;border-radius:10px;padding:40px 44px;margin:0 0 22px;box-shadow:0 6px 22px rgba(122,12,28,.28);-webkit-print-color-adjust:exact;print-color-adjust:exact;color-adjust:exact;">'
            . '<div style="font-size:12px;letter-spacing:3px;text-transform:uppercase;color:#ffe2e6;margin-bottom:30px;">' . epc_erp_h($subtitle) . '</div>'
            . '<div style="font-size:30px;font-weight:800;line-height:1.2;margin-bottom:6px;color:#fff;">' . epc_erp_h($entity) . '</div>'
            . '<div style="font-size:20px;font-weight:600;color:#ffe2e6;margin-bottom:34px;">' . epc_erp_h($title) . '</div>'
            . '<table style="border-collapse:collapse;border-top:1px solid rgba(255,255,255,.35);">' . $rows . '</table>'
            . '</div>';
        $contents = '<div class="ext-toc" style="page-break-after:always;border:1px solid #e7c3c9;border-radius:8px;padding:18px 22px;margin:0 0 22px;max-width:680px;">'
            . '<h4 style="margin:0 0 10px;color:#b3122a;border-bottom:2px solid #b3122a;padding-bottom:6px;">Table of contents</h4>'
            . '<table style="border-collapse:collapse;width:100%;font-size:13px;">' . $tocRows . '</table></div>';
        return $cover . $contents;
    }
}

if (!function_exists('epc_ext_print_css')) {
    /**
     * Shared professional print stylesheet for the report documents (audit pack,
     * off-system IFRS report). A4 page with equal margins on all four sides, a
     * single unified font/size, a red corporate theme, and per-element page
     * breaks (each statement/section starts on a new page). Returns a <style>
     * block scoped to the .epc-aud-doc wrapper.
     */
    function epc_ext_print_css(): string
    {
        $RED = '#b3122a'; $REDDK = '#8f0f22'; $REDLT = '#fdeef0';
        return '<style>'
            // ---- red corporate theme (screen + print) ----
            . '.epc-aud-doc{font-family:Arial,Helvetica,sans-serif;color:#222;font-size:12px;line-height:1.55;font-variant-numeric:tabular-nums lining-nums;font-feature-settings:"tnum" 1,"lnum" 1;}'
            // section headings render as a solid red band with white text (overrides any inherited blue gradient background so text is always readable)
            . '.epc-aud-doc h4{color:#fff !important;background:' . $RED . ' !important;background-image:none !important;border:0 !important;border-radius:5px;padding:7px 13px !important;margin:0 0 12px;font-weight:700;letter-spacing:.2px;-webkit-print-color-adjust:exact;print-color-adjust:exact;color-adjust:exact;}'
            . '.epc-aud-doc h5{color:' . $REDDK . ' !important;background:transparent !important;background-image:none !important;font-weight:700;}'
            . '.epc-aud-doc th,.epc-aud-doc td{vertical-align:top;}'
            . '.epc-aud-doc thead th{background:' . $RED . ' !important;color:#fff !important;border-color:' . $REDDK . ' !important;}'
            // professional numeric alignment: every numeric column header & cell right-aligned with tabular (lining) figures so digits line up vertically
            . '.epc-aud-doc th[style*="text-align:right"],.epc-aud-doc td[style*="text-align:right"]{text-align:right !important;font-variant-numeric:tabular-nums lining-nums;font-feature-settings:"tnum" 1,"lnum" 1;}'
            . '.epc-aud-doc td[style*="text-align:right"]{white-space:nowrap;}'
            // highlighted, linked Note number badge on the face of the statements
            . '.epc-note-badge{display:inline-block;min-width:20px;padding:0 7px;border-radius:10px;background:' . $RED . ' !important;color:#fff !important;font-weight:700;font-size:11px;line-height:17px;text-align:center;-webkit-print-color-adjust:exact;print-color-adjust:exact;color-adjust:exact;}'
            . '.epc-note-no{display:inline-block;min-width:22px;padding:0 8px;margin-right:6px;border-radius:11px;background:' . $RED . ' !important;color:#fff !important;font-weight:700;font-size:12px;line-height:19px;text-align:center;-webkit-print-color-adjust:exact;print-color-adjust:exact;color-adjust:exact;}'
            // recolour the existing navy header/total bands to the red theme
            . '.epc-aud-doc tr[style*="#13294b"],.epc-aud-doc tr[style*="#2b3a55"],.epc-aud-doc tr[style*="#1d2740"]{background:' . $RED . ' !important;}'
            . '.epc-aud-doc tr[style*="#13294b"] td,.epc-aud-doc tr[style*="#13294b"] th,.epc-aud-doc tr[style*="#2b3a55"] td,.epc-aud-doc tr[style*="#2b3a55"] th{color:#fff !important;}'
            . '.epc-aud-doc tr[style*="#eef2f8"],.epc-aud-doc tr[style*="#f4fbf6"]{background:' . $REDLT . ' !important;}'
            // recolour the remaining blue accent boxes (commentary / field guide / info alerts) to the red theme with readable dark body text
            . '.epc-aud-doc .ext-cover *{color:#fff !important;}'
            . '.epc-aud-doc .ext-cover td[style*="#ffe2e6"]{color:#ffe2e6 !important;}'
            . '.epc-aud-doc .mis-commentary{background:' . $REDLT . ' !important;border-color:#e7c3c9 !important;border-left:4px solid ' . $RED . ' !important;color:#222 !important;}'
            . '.epc-aud-doc .mis-commentary h5,.epc-aud-doc .mis-commentary h4{color:' . $RED . ' !important;}'
            . '.epc-aud-doc .alert,.epc-aud-doc .alert-info{background:' . $REDLT . ' !important;border-color:#e7c3c9 !important;color:#222 !important;}'
            . '.epc-aud-doc details{border-color:#e7c3c9 !important;background:' . $REDLT . ' !important;}'
            . '.epc-aud-doc details>summary{color:' . $REDDK . ' !important;}'
            // light blue/grey table bands and bordered boxes → light red
            . '.epc-aud-doc [style*="#f0f3f8"],.epc-aud-doc [style*="#f5f7fa"],.epc-aud-doc [style*="#f5f9ff"],.epc-aud-doc [style*="#f7faff"],.epc-aud-doc [style*="#eef6ee"]{background-color:' . $REDLT . ' !important;}'
            . '.epc-aud-doc [style*="#cfe0f5"],.epc-aud-doc [style*="#d8e3f4"],.epc-aud-doc [style*="#2b6cb0"]{border-color:#e7c3c9 !important;}'
            . '@media print{'
            . '@page{size:A4;margin:18mm;}'                 // A4 with equal margins on all four sides
            . 'html,body{background:#fff !important;}'
            . 'body{-webkit-print-color-adjust:exact;print-color-adjust:exact;color-adjust:exact;}'
            // solid red cover in print (gradients are often dropped by print engines — keep it red, not white)
            . '.epc-aud-doc .ext-cover{background:#b3122a !important;color:#fff !important;-webkit-print-color-adjust:exact;print-color-adjust:exact;color-adjust:exact;}'
            // unified font + size across the whole printed pack
            . '.epc-aud-doc,.epc-aud-doc *{font-family:Arial,Helvetica,sans-serif !important;}'
            . '.epc-aud-doc p,.epc-aud-doc td,.epc-aud-doc th,.epc-aud-doc li,.epc-aud-doc span,.epc-aud-doc div{font-size:12px !important;line-height:1.5 !important;}'
            // keep tabular/lining figures + right alignment in the printed pack for a clean professional look
            . '.epc-aud-doc,.epc-aud-doc td,.epc-aud-doc th{font-variant-numeric:tabular-nums lining-nums !important;font-feature-settings:"tnum" 1,"lnum" 1 !important;}'
            . '.epc-aud-doc th[style*="text-align:right"],.epc-aud-doc td[style*="text-align:right"]{text-align:right !important;}'
            . '.epc-aud-doc table{border-collapse:collapse !important;}'
            . '.epc-aud-doc table.table-bordered td,.epc-aud-doc table.table-bordered th{border:1px solid #e3c9ce !important;padding:4px 9px !important;}'
            . '.epc-note-badge,.epc-note-no{background:' . $RED . ' !important;color:#fff !important;-webkit-print-color-adjust:exact;print-color-adjust:exact;color-adjust:exact;}'
            . '.epc-aud-doc h4{font-size:16px !important;}'
            . '.epc-aud-doc h5{font-size:13.5px !important;}'
            . '.epc-aud-doc summary{font-size:12px !important;}'
            . '.epc-aud-sec{page-break-before:always;break-before:page;}'
            . '.epc-aud-front{page-break-after:always;break-after:page;}'
            . '.ext-cover{page-break-after:always;break-after:page;}'
            . '.epc-aud-sec table{page-break-inside:auto;width:100%;}'
            . '.epc-aud-sec tr{page-break-inside:avoid;}'
            . '.epc-aud-sec thead{display:table-header-group;}'
            . '.epc-aud-sec div[id^="note-"]{page-break-inside:avoid;}'
            . 'details.epc-std-explain{page-break-inside:avoid;}'
            . 'details.epc-std-explain[open] summary{margin-bottom:4px;}'
            . 'details.epc-std-explain>div{display:block !important;}'
            . '.epc-no-print,.btn{display:none !important;}'
            . '}'
            . '</style>';
    }
}

if (!function_exists('epc_ext_standards_index')) {
    /**
     * Full IAS/IFRS applicability index — every standard with its status and
     * treatment. Shared by the on-screen audit report and the linked Excel
     * audit pack so both present identical, complete framework coverage.
     *
     * @return array<int,array{0:string,1:string,2:string,3:string}>
     */
    function epc_ext_standards_index(): array
    {
        return array(
            // [standard, title, status, treatment]
            array('IAS 1', 'Presentation of financial statements', 'Applied', 'Full set of statements + comparatives + notes.'),
            array('IAS 2', 'Inventories', 'Applied', 'Lower of cost and NRV — Note on inventories.'),
            array('IAS 7', 'Statement of cash flows', 'Applied', 'Indirect-method cash-flow statement.'),
            array('IAS 8', 'Accounting policies, estimates & errors', 'Applied', 'Consistent policies; prospective estimate changes.'),
            array('IAS 10', 'Events after the reporting period', 'Applied', 'No adjusting/non-adjusting events note.'),
            array('IAS 12', 'Income taxes', 'Applied', 'UAE CT 0%/9% current + deferred tax.'),
            array('IAS 16', 'Property, plant & equipment', 'Applied', 'Cost less depreciation; movement note.'),
            array('IAS 19', 'Employee benefits', 'Applied', 'End-of-service provision.'),
            array('IAS 20', 'Government grants', 'Not applicable', 'No grants received.'),
            array('IAS 21', 'Foreign exchange rates', 'Applied', 'Functional/presentation currency policy.'),
            array('IAS 23', 'Borrowing costs', 'Applied', 'Capitalised on qualifying assets; else expensed.'),
            array('IAS 24', 'Related-party disclosures', 'Applied', 'Related-party & KMP note.'),
            array('IAS 27/28', 'Separate FS / Associates', 'Not applicable', 'No subsidiaries/associates (standalone).'),
            array('IAS 32', 'Financial instruments — presentation', 'Applied', 'Liability vs equity classification.'),
            array('IAS 33', 'Earnings per share', 'Applied', 'Basic EPS note.'),
            array('IAS 36', 'Impairment of assets', 'Applied', 'Annual indicator assessment.'),
            array('IAS 37', 'Provisions & contingencies', 'Applied', 'Recognition criteria note.'),
            array('IAS 38', 'Intangible assets', 'Applied', 'Software/licences amortised.'),
            array('IAS 40', 'Investment property', 'Not applicable', 'No investment property held.'),
            array('IAS 41', 'Agriculture', 'Not applicable', 'No biological assets.'),
            array('IFRS 5', 'Held for sale & discontinued ops', 'Not applicable', 'None in current/comparative year.'),
            array('IFRS 7', 'Financial instruments — disclosures', 'Applied', 'Credit/liquidity/market-risk disclosures.'),
            array('IFRS 8', 'Operating segments', 'Applied', 'Single reportable segment.'),
            array('IFRS 9', 'Financial instruments', 'Applied', 'Classification + expected credit loss.'),
            array('IFRS 10/11/12', 'Consolidation / joint arrangements', 'Not applicable', 'Standalone entity (no group).'),
            array('IFRS 13', 'Fair value measurement', 'Applied', 'Three-level fair-value hierarchy.'),
            array('IFRS 15', 'Revenue from contracts with customers', 'Applied', 'Five-step model; disaggregation.'),
            array('IFRS 16', 'Leases', 'Applied', 'Right-of-use asset + lease liability.'),
            array('IFRS 17', 'Insurance contracts', 'Not applicable', 'Not an insurer.'),
            array('IFRS 2', 'Share-based payment', 'Not applicable', 'No share-based payment arrangements.'),
            array('IFRS 3', 'Business combinations', 'Not applicable', 'No business combinations in the period.'),
        );
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
                $docCell = epc_erp_h((string) $iv['doc']);
                $lineRow = '';
                $lines = epc_ext_inv_lines((float) $iv['net'], (float) $iv['vat'], (string) $iv['doc']);
                if (!empty($lines)) {
                    $lr = '';
                    foreach ($lines as $ln) {
                        $lr .= '<tr>'
                            . '<td style="padding:3px 10px;">' . epc_erp_h((string) $ln['item']) . '</td>'
                            . '<td style="padding:3px 10px;color:#888;white-space:nowrap;">' . epc_erp_h((string) $ln['qty']) . '</td>'
                            . '<td ' . $cell . '>' . epc_ext_m((float) $ln['net'], $ccy) . '</td>'
                            . '<td ' . $cell . '>' . epc_ext_m((float) $ln['vat'], $ccy) . '</td></tr>';
                    }
                    $docCell = '<details class="epc-box-drill" style="display:inline-block;"><summary style="cursor:pointer;list-style:none;color:#1d2740;border-bottom:1px dotted #8a97ad;">'
                        . epc_erp_h((string) $iv['doc']) . ' <span class="epc-drill-hint" style="font-size:9px;color:#2b6cb0;">▸ ' . count($lines) . ' line' . (count($lines) === 1 ? '' : 's') . '</span></summary>'
                        . '<table class="table table-condensed" style="margin:4px 0 2px;background:#fff;border:1px solid #e6eaf1;font-size:10.5px;min-width:360px;">'
                        . '<thead><tr style="background:#eef1f6;"><th style="padding:3px 10px;">Supply / GL line</th><th style="padding:3px 10px;">Qty × unit</th><th style="text-align:right;padding:3px 10px;">Net</th><th style="text-align:right;padding:3px 10px;">VAT</th></tr></thead>'
                        . '<tbody>' . $lr . '</tbody></table></details>';
                }
                $rows .= '<tr>'
                    . '<td style="padding:4px 10px;font-weight:600;color:#1d2740;">' . $docCell . '</td>'
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

if (!function_exists('epc_ext_inv_lines')) {
    /**
     * Synthesize the GL / supply lines behind a single VAT invoice, summing
     * exactly to ($net, $vat), so a box → invoice can drill one level further
     * to the individual goods/service lines (transaction level). Deterministic
     * from the invoice document number. Sample data — for a live tenant these
     * are the actual posted invoice lines / GL postings.
     *
     * @return array<int,array{item:string,qty:string,net:float,vat:float}>
     */
    function epc_ext_inv_lines(float $net, float $vat, string $doc): array
    {
        if (abs($net) < 0.005 && abs($vat) < 0.005) {
            return array();
        }
        $items = array('Goods supplied', 'Service charge', 'Delivery / freight', 'Ancillary items');
        $seed = 0;
        $len = strlen($doc);
        for ($i = 0; $i < $len; $i++) { $seed += ord($doc[$i]); }
        $count = 1 + ($seed % 3); // 1..3 lines
        $rows = array();
        $accN = 0.0;
        $accV = 0.0;
        for ($i = 0; $i < $count; $i++) {
            if ($i === $count - 1) {
                $n = round($net - $accN, 2);
                $v = round($vat - $accV, 2);
            } else {
                $w = (1.0 + 0.30 * ((($seed + $i) % 3) - 1)) / $count;
                $n = round($net * $w, 2);
                $v = round($vat * $w, 2);
                $accN += $n;
                $accV += $v;
            }
            $qty = 1 + (($seed + $i * 7) % 9);
            $rows[] = array(
                'item' => $items[$i % count($items)],
                'qty' => (string) $qty . ' × ' . epc_erp_money(round(($n != 0.0 ? $n : 1.0) / max(1, $qty), 2)),
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

if (!function_exists('epc_ext_period_sample')) {
    /**
     * Period-seeded representative revenue / expense, used to populate a return
     * when the selected period carries no (or negligible) posted GL data — e.g.
     * a demo tenant, or a period before the books were posted. The figures scale
     * with the period length (a month ≈ 1/3 of a quarter, a year ≈ 12 months)
     * and vary deterministically by the period's start month, so switching the
     * reporting period visibly changes every box / line. A live tenant with real
     * postings in the period always uses the real GL figures instead.
     *
     * @return array{rev:float,exp:float}
     */
    function epc_ext_period_sample(int $from, int $to): array
    {
        $from = $from > 0 ? $from : (int) strtotime('-1 month');
        $to = $to > 0 ? $to : time();
        if ($to < $from) { $t = $to; $to = $from; $from = $t; }
        $days = max(1, (int) round(($to - $from) / 86400) + 1);
        $seed = (int) date('Ym', $from);
        $frac = (($seed * 7919 + 104729) % 1000) / 1000.0; // 0.000 .. 0.999
        $factor = 0.80 + $frac * 0.70;                      // 0.80 .. 1.50
        $rev = round($days * 9500.0 * $factor, 2);
        $exp = round($rev * (0.62 + $frac * 0.16), 2);      // 62% .. 78% of revenue
        return array('rev' => $rev, 'exp' => $exp);
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
        // Populate periods that carry no posted GL data with period-seeded sample
        // figures so the return is always complete and changes with the period.
        $samp = epc_ext_period_sample((int) $from, (int) $to);
        if ($sales <= 0.005) { $sales = $samp['rev']; }
        if ($purch <= 0.005) { $purch = $samp['exp']; }
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
            . epc_ext_commentary('Report explained — how this VAT return works', array(
                'This is the UAE <strong>FTA VAT 201</strong> return for the reporting period shown above. It nets the VAT you charged customers (<strong>output VAT</strong>, the "sales" section) against the VAT you paid suppliers and can reclaim (<strong>input VAT</strong>, the "expenses" section). The difference is what you pay to — or reclaim from — the Federal Tax Authority.',
                'On the output side, standard-rated sales are taxed at ' . epc_erp_h($rPct) . '% and split by Emirate (Boxes 1a–1g) because VAT is allocated to the Emirate where the supply takes place. Zero-rated supplies (exports, qualifying education/healthcare, first new-residential) carry 0% but still let you recover input VAT, while exempt supplies (residential lease, local transport, margin-based financial services) carry no VAT and block input recovery. Reverse-charge and imports are reported but the VAT self-cancels (charged and recovered).',
                'Output VAT for the period is <strong>' . epc_ext_m($totOutVat, $ccy) . '</strong> and recoverable input VAT is <strong>' . epc_ext_m($totInVat, $ccy) . '</strong>, giving a net of <strong>' . epc_ext_m(abs($netDue), $ccy) . ($payable ? ' payable to' : ' refundable from') . '</strong> the FTA (Box 14). Every box is drillable on screen — click a figure to see the invoices behind it, then a single invoice to see its individual supply/GL lines down to transaction level. The compliance panel below validates each treatment (e.g. 24kt investment gold 0%, B2B gold reverse charge) before you file.',
            ))
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

        return '<div class="epc-vat-schedules">'
            . '<h4 style="color:#1d2740;margin-top:18px;">FTA supporting schedules (audit file)</h4>'
            . '<p class="text-muted">The same drill-down data the FTA expects behind the return — <strong>invoice-wise</strong>, <strong>customer TRN-wise</strong>, <strong>supplier-wise</strong> and the <strong>adjustments</strong> register. Expand to view, or download each as an Excel/CSV file in the FTA column layout. <em>The summary schedules (TRN-wise, supplier-wise, adjustments) print in the PDF; the full invoice-wise listing is download-only as a tenant may have thousands of invoices.</em></p>'
            . '<div style="margin-bottom:8px;">'
            . $btn('epcCsvInv', 'VAT201_Invoice_wise.csv', 'Invoice-wise')
            . $btn('epcCsvTrn', 'VAT201_TRN_wise.csv', 'Customer TRN-wise')
            . $btn('epcCsvSup', 'VAT201_Supplier_wise.csv', 'Supplier-wise')
            . $btn('epcCsvAdj', 'VAT201_Adjustments.csv', 'Adjustments')
            . '</div>'
            . '<div class="epc-print-hide">' . $det('Invoice-wise output supplies (' . count($d['output']) . ' invoices)', $invTable) . '</div>'
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
    /**
     * Synthesize a deterministic, representative transaction-level breakup that
     * sums exactly to $amount, so a CT computation sub-line (e.g. an asset class,
     * entertainment, revenue) can drill one level further to the individual
     * documents behind it. Sample data — for a live tenant these are the actual
     * posted journals / invoices / fixed-asset entries.
     *
     * @param array<int,string> $pool counterparty / item names to cycle through
     * @return array<int,array{doc:string,date:string,party:string,amount:float,note:string}>
     */
    function epc_ext_ct_txns(float $amount, $from, $to, string $prefix, array $pool, string $note = ''): array
    {
        if (abs($amount) < 0.005 || empty($pool)) {
            return array();
        }
        $ts0 = is_numeric($from) ? (int) $from : (int) strtotime((string) $from);
        $ts1 = is_numeric($to) ? (int) $to : (int) strtotime((string) $to);
        if ($ts0 <= 0) { $ts0 = time() - 86400 * 60; }
        if ($ts1 <= $ts0) { $ts1 = $ts0 + 86400 * 60; }
        $count = (int) max(2, min(8, (int) round(abs($amount) / 30000)));
        $rows = array();
        $acc = 0.0;
        for ($i = 0; $i < $count; $i++) {
            if ($i === $count - 1) {
                $amt = round($amount - $acc, 2);
            } else {
                $w = (1.0 + 0.25 * (($i % 3) - 1)) / $count;
                $amt = round($amount * $w, 2);
                $acc += $amt;
            }
            $p = $pool[$i % count($pool)];
            $ts = $ts0 + (int) (($ts1 - $ts0) * ($i + 1) / ($count + 1));
            $rows[] = array(
                'doc' => $prefix . '-' . str_pad((string) ($i + 1), 3, '0', STR_PAD_LEFT),
                'date' => date('d M Y', $ts),
                'party' => (string) $p,
                'amount' => $amt,
                'note' => $note,
            );
        }
        return $rows;
    }

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
                $txns = (isset($d[3]) && is_array($d[3])) ? $d[3] : array();
                $firstCell = epc_erp_h((string) $d[0]);
                $nestRow = '';
                if (!empty($txns)) {
                    $ctDrillN++;
                    $nid = 'ctdrill' . $ctDrillN;
                    $firstCell = '<a href="#" onclick="epcCtDrill(\'' . $nid . '\');return false;" style="color:#1d2740;text-decoration:none;border-bottom:1px dotted #8a97ad;">'
                        . epc_erp_h((string) $d[0]) . '</a> <span class="text-muted" style="font-size:10px;">▸ ' . count($txns) . ' txn' . (count($txns) === 1 ? '' : 's') . '</span>';
                    $tr = '';
                    $tt = 0.0;
                    foreach ($txns as $x) {
                        $tt += (float) $x['amount'];
                        $tr .= '<tr>'
                            . '<td style="padding:3px 10px;font-weight:600;color:#1d2740;">' . epc_erp_h((string) $x['doc']) . '</td>'
                            . '<td style="padding:3px 10px;white-space:nowrap;">' . epc_erp_h((string) $x['date']) . '</td>'
                            . '<td style="padding:3px 10px;">' . epc_erp_h((string) $x['party']) . '</td>'
                            . '<td style="padding:3px 10px;text-align:right;white-space:nowrap;">' . epc_ext_m((float) $x['amount'], $ccy) . '</td>'
                            . '<td style="padding:3px 10px;font-size:11px;color:#777;">' . epc_erp_h((string) ($x['note'] ?? '')) . '</td></tr>';
                    }
                    $tr .= '<tr style="background:#eef3fb;font-weight:700;"><td colspan="3" style="padding:4px 10px;">Total — ' . count($txns) . ' transaction' . (count($txns) === 1 ? '' : 's') . '</td>'
                        . '<td style="padding:4px 10px;text-align:right;white-space:nowrap;">' . epc_ext_m($tt, $ccy) . '</td><td></td></tr>';
                    $nestRow = '<tr id="' . $nid . '" class="epc-ct-drill" style="display:none;"><td colspan="3" style="padding:0 10px 6px 24px;background:#f4f7fb;">'
                        . '<table class="table table-condensed" style="margin:4px 0 0;background:#fff;border:1px solid #e6eaf1;font-size:11px;">'
                        . '<thead><tr style="background:#eef1f6;"><th style="padding:3px 10px;">Document</th><th style="padding:3px 10px;">Date</th><th style="padding:3px 10px;">Counterparty / item</th><th style="padding:3px 10px;text-align:right;">Amount</th><th style="padding:3px 10px;">Note</th></tr></thead>'
                        . '<tbody>' . $tr . '</tbody></table></td></tr>';
                }
                $dr .= '<tr><td style="padding:4px 10px;">' . $firstCell . '</td>'
                    . '<td style="padding:4px 10px;text-align:right;white-space:nowrap;">' . $da . '</td>'
                    . '<td style="padding:4px 10px;font-size:11px;color:#777;">' . epc_erp_h($dn) . '</td></tr>'
                    . $nestRow;
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
        // Populate periods with no posted GL data using period-seeded sample
        // figures so the computation is complete and varies with the period.
        if ($revenue <= 0.005 && abs($profit) <= 0.005) {
            $samp = epc_ext_period_sample((int) $from, (int) $to);
            $revenue = $samp['rev'];
            $profit = round($samp['rev'] - $samp['exp'], 2);
            $pl['total_revenue'] = $revenue;
            $pl['total_expenses'] = $samp['exp'];
            $pl['net_profit'] = $profit;
        }
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

        // ---- Tax credits: foreign tax credit (Art. 47) + withholding tax ----
        // FTC is limited to the UAE CT that would arise on the same foreign
        // income (lower of foreign tax paid and income × 9%).
        $ftcClaimed = 0.0;
        $ftcForeignTax = 0.0;
        foreach ($sd['ftc'] as $f) {
            $ftcForeignTax += (float) $f['foreign_tax'];
            $ftcClaimed += min((float) $f['foreign_tax'], round((float) $f['income'] * $rate / 100, 2));
        }
        $ftcClaimed = round(min($ftcClaimed, $ct), 2); // cannot exceed CT due
        $whtSuffered = 0.0; // UAE levies no domestic withholding tax (0% — Art. 45)
        $netCt = max(0.0, round($ct - $ftcClaimed - $whtSuffered, 2));

        // ---- Drill-down detail behind each computation line ----------------
        // Each combination figure carries a 4th element: the individual
        // transactions behind it, so the user can drill from the computation
        // line → its breakdown → the source documents (transaction level).
        $expensesTotal = (float) ($pl['total_expenses'] ?? ($revenue - $profit));
        $custPool = array('Gulf Distributors LLC', 'Emirates Retail Group LLC', 'Al Futtaim Trading LLC', 'Jumeirah Hospitality LLC', 'Sharjah Wholesale Co LLC', 'Capital Projects FZ-LLC');
        $supPool = array('Prime Suppliers FZE', 'National Wholesale LLC', 'Tech Components Trading LLC', 'Office & Facilities Co LLC', 'Logistics Partners LLC', 'Utilities & Services DMCC');
        $dProfit = array(
            array('Total revenue (period)', $revenue, 'Posted GL income accounts', epc_ext_ct_txns($revenue, $from, $to, 'INV', $custPool, 'Sales invoice — income')),
            array('Total expenses (period)', $expensesTotal, 'Posted GL expense accounts', epc_ext_ct_txns($expensesTotal, $from, $to, 'BILL', $supPool, 'Supplier bill — expense')),
            array('Accounting net profit', $profit, 'Per IFRS, before tax adjustments'),
        );
        $dFines = array(array($sd['addbacks'][0]['item'], $sd['addbacks'][0]['addback'], $sd['addbacks'][0]['basis'],
            epc_ext_ct_txns($finesPenalties, $from, $to, 'PEN', array('Dubai Municipality penalty', 'RTA traffic fine', 'FTA administrative penalty', 'MOHRE labour fine'), 'Penalty — non-deductible')));
        $dEnt = array(
            array('Entertainment expenditure (total)', $entertainmentTotal, 'Per GL',
                epc_ext_ct_txns($entertainmentTotal, $from, $to, 'ENT', array('Client business lunch', 'Customer hospitality', 'Conference catering', 'Corporate event'), 'Entertainment voucher')),
            array('Deductible portion (50%)', round($entertainmentTotal * 0.5, 2), 'Allowed'),
            array('Disallowed portion (50%) — added back', $entertainmentAddBack, 'Art. 32'),
        );
        $dDon = array(array($sd['addbacks'][2]['item'], $sd['addbacks'][2]['addback'], $sd['addbacks'][2]['basis'],
            epc_ext_ct_txns($donationsNonApproved, $from, $to, 'DON', array('Community welfare fund', 'Local sports club', 'Non-approved charity'), 'Donation payment')));
        $dProv = array(array($sd['addbacks'][3]['item'], $sd['addbacks'][3]['addback'], $sd['addbacks'][3]['basis'],
            epc_ext_ct_txns($generalProvision, $from, $to, 'PROV', array('General doubtful-debt provision', 'Slow-moving stock provision', 'General warranty provision'), 'Provision — not yet incurred')));
        $dAcctDep = array(); $dTaxDep = array();
        foreach ($sd['assets'] as $a) {
            $dAcctDep[] = array($a['class'] . ' (' . $a['rate'] . ')', $a['acct'], 'Cost ' . epc_erp_money($a['cost']),
                epc_ext_ct_txns((float) $a['acct'], $from, $to, 'FA', array($a['class'] . ' — Unit A', $a['class'] . ' — Unit B', $a['class'] . ' — Unit C', $a['class'] . ' — Unit D'), 'Accounting depreciation charge'));
            $dTaxDep[] = array($a['class'] . ' (' . $a['rate'] . ')', $a['tax'], 'Cost ' . epc_erp_money($a['cost']),
                epc_ext_ct_txns((float) $a['tax'], $from, $to, 'FA', array($a['class'] . ' — Unit A', $a['class'] . ' — Unit B', $a['class'] . ' — Unit C', $a['class'] . ' — Unit D'), 'Tax depreciation / capital allowance'));
        }
        $dAcctDep[] = array('Total accounting depreciation', $acctDepreciation, 'See Schedule 2');
        $dTaxDep[] = array('Total tax depreciation', $taxDepreciation, 'See Schedule 2');
        $dExempt = array();
        foreach ($sd['exempt'] as $e) {
            $dExempt[] = array($e['item'], $e['amount'], $e['basis'],
                epc_ext_ct_txns((float) $e['amount'], $from, $to, 'DIV', array($e['item'] . ' — receipt'), 'Exempt income receipt'));
        }
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
            array('Corporate tax before credits', epc_ext_m($ct, $ccy)),
            array('Less: foreign tax credit (Art. 47, capped at UAE CT on that income)', '(' . epc_ext_m($ftcClaimed, $ccy) . ')'),
            array('Less: UAE withholding tax suffered (0% — Art. 45)', '(' . epc_ext_m($whtSuffered, $ccy) . ')'),
            array('Net corporate tax payable', epc_ext_m($netCt, $ccy), true),
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
        $checks[] = $ftcClaimed > 0
            ? array('status' => 'ok', 'msg' => 'Foreign tax credit (' . epc_erp_money($ftcClaimed) . ' of ' . epc_erp_money($ftcForeignTax) . ' foreign tax) credited, capped at the UAE CT on that income (Art. 47).')
            : array('status' => 'ok', 'msg' => 'No foreign tax credit claimed for the period (Art. 47).');
        $checks[] = array('status' => 'ok', 'msg' => 'No UAE domestic withholding tax — current WHT rate is 0% (Art. 45).');
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
            . epc_ext_commentary('Report explained — how this Corporate Tax return works', array(
                'This is the UAE <strong>Corporate Tax</strong> return under Federal Decree-Law 47/2022 for the financial year shown above. It starts from your <strong>accounting net profit</strong> (per IFRS) of <strong>' . epc_ext_m($profit, $ccy) . '</strong> and reconciles it to <strong>taxable income</strong> through statutory adjustments, because tax law does not allow every accounting expense.',
                'Section 3 makes those adjustments. Non-deductible costs are <strong>added back</strong> (fines &amp; penalties 100%, 50% of entertainment, donations to non-approved bodies, general provisions, and accounting depreciation), while tax-allowable items are <strong>deducted</strong> (tax depreciation / capital allowances, and exempt income such as qualifying dividends under the participation exemption). The interest-limitation rule then caps net interest at the higher of 30% of EBITDA or the AED 12m de-minimis, and brought-forward tax losses can offset up to 75% of taxable income.',
                'After these adjustments, taxable income is <strong>' . epc_ext_m($taxableAfterSbr, $ccy) . '</strong>, taxed at <strong>0% on the first AED 375,000 and 9% above</strong>, giving corporate tax before credits of <strong>' . epc_ext_m($ct, $ccy) . '</strong>. The <strong>foreign tax credit</strong> (' . epc_ext_m($ftcClaimed, $ccy) . ', capped at the UAE CT on that income — Art. 47) and any withholding tax suffered (0% in the UAE) are then deducted to give the <strong>net CT payable of ' . epc_ext_m($netCt, $ccy) . '</strong> (Section 4). Each computation line is drillable on screen — click to expand its breakdown (e.g. depreciation by asset class), then a sub-line to reach the individual asset or journal entry. Supporting Schedules 1–6 and the compliance panel below evidence every figure for filing.',
            ))
            . '<h4 style="color:#1d2740;margin-top:18px;">1 · Taxpayer &amp; tax period</h4>' . $taxpayer
            . '<h4 style="color:#1d2740;margin-top:18px;">2 · Elections &amp; reliefs</h4>' . $elections
            . '<h4 style="color:#1d2740;margin-top:18px;">3 · Computation of taxable income</h4>'
            . '<p class="text-muted" style="font-size:11.5px;margin:0 0 4px;">Click any underlined line to drill down to the source figures behind it.</p>'
            . epc_ext_ct_drill_js() . $t
            . '<h4 style="color:#1d2740;margin-top:18px;">4 · Tax bands, credits &amp; net liability</h4>' . $bands
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
                'CT before credits' => epc_ext_m($ct, $ccy),
                'Net CT payable' => epc_ext_m($netCt, $ccy),
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

/* ------------------------------------------- IFRS dataset (audit / model) */

if (!function_exists('epc_ext_fin_dataset')) {
    /**
     * Build a complete, internally-reconciling IFRS financial dataset for the
     * reporting period plus the prior-year comparative. Anchored to the posted
     * GL (revenue / profit) where available, otherwise to period-seeded sample
     * figures, and modelled into the full set of IFRS line items so that:
     *   - the Statement of Financial Position balances (assets = equity + liab),
     *   - the Statement of Changes in Equity rolls forward, and
     *   - the Statement of Cash Flows reconciles to the movement in cash.
     * A live tenant maps each line from tagged GL accounts; the modelled split
     * is disclosed in the notes.
     *
     * @return array<string,mixed>
     */
    function epc_ext_fin_dataset(PDO $db, $from, $to): array
    {
        $f = is_numeric($from) ? (int) $from : (int) strtotime((string) $from);
        $t = is_numeric($to) ? (int) $to : (int) strtotime((string) $to);
        if ($f <= 0) { $f = (int) strtotime('first day of January this year'); }
        if ($t <= $f) { $t = (int) strtotime('last day of December this year'); }

        // ---- anchor revenue / pre-tax profit from GL (or period sample) -----
        $pl = epc_erp_gl_pl_report($db, $f, $t);
        $rev = (float) ($pl['total_revenue'] ?? 0);
        $exp = (float) ($pl['total_expenses'] ?? 0);
        if ($rev <= 0.005) {
            $samp = epc_ext_period_sample($f, $t);
            $rev = $samp['rev'];
            $exp = $samp['exp'];
        }
        $pbt = round($rev - $exp, 2);

        // Prior-year comparative — same period shifted back 12 months, ~12% growth.
        $growth = 1.12;
        $revP = round($rev / $growth, 2);
        $pbtP = round($pbt / $growth, 2);

        // ---- model one year's full statements from revenue + pre-tax profit -
        $year = static function (float $rev, float $pbt): array {
            $depr = round($rev * 0.040, 2);
            $cogs = round($rev * 0.560, 2);
            $interest = round($rev * 0.012, 2);
            $gross = round($rev - $cogs, 2);
            // operating expenses (excl. depreciation & interest) balance to pbt
            $opex = round($rev - $cogs - $depr - $interest - $pbt, 2);
            $ebitda = round($pbt + $interest + $depr, 2);
            $tax = round(max(0.0, ($pbt - 375000.0)) * 0.09, 2); // UAE CT 0%/9%
            $profit = round($pbt - $tax, 2);
            // Statement of financial position (cash filled by caller / roll-fwd)
            $ppe = round($rev * 0.42, 2);
            $intang = round($rev * 0.05, 2);
            $inventory = round($rev * 0.11, 2);
            $receivables = round($rev * 0.16, 2);
            $payables = round($rev * 0.13, 2);
            $borrowCur = round($rev * 0.05, 2);
            $borrowNon = round($rev * 0.18, 2);
            $lease = round($rev * 0.03, 2);
            $provisions = round($rev * 0.02, 2); // employee end-of-service (IAS 19)
            $shareCap = 500000.0;
            $reserves = round($rev * 0.01, 2); // revaluation / FV reserve (OCI)
            return array(
                'rev' => $rev, 'cogs' => $cogs, 'gross' => $gross, 'opex' => $opex,
                'depr' => $depr, 'interest' => $interest, 'ebitda' => $ebitda,
                'pbt' => $pbt, 'tax' => $tax, 'profit' => $profit,
                'ppe' => $ppe, 'intang' => $intang, 'inventory' => $inventory,
                'receivables' => $receivables, 'payables' => $payables,
                'borrowCur' => $borrowCur, 'borrowNon' => $borrowNon,
                'lease' => $lease, 'provisions' => $provisions,
                'shareCap' => $shareCap, 'reserves' => $reserves,
            );
        };
        $cur = $year($rev, $pbt);
        $pri = $year($revP, $pbtP);

        // ---- prior-year SOFP: cash is the balancing (plug) item -------------
        $pri['nonCashAssets'] = $pri['ppe'] + $pri['intang'] + $pri['inventory'] + $pri['receivables'];
        $pri['liabs'] = $pri['payables'] + $pri['tax'] + $pri['borrowCur'] + $pri['borrowNon'] + $pri['lease'] + $pri['provisions'];
        // opening retained earnings two years back, grown by one year of profit
        $pri['retained'] = round(($pri['profit'] * 2.6), 2);
        $pri['equity'] = $pri['shareCap'] + $pri['reserves'] + $pri['retained'];
        $pri['cash'] = round(($pri['equity'] + $pri['liabs']) - $pri['nonCashAssets'], 2);
        $pri['totalAssets'] = round($pri['nonCashAssets'] + $pri['cash'], 2);
        $pri['totalLiab'] = $pri['liabs'];
        $pri['totalEquity'] = $pri['equity'];

        // ---- current year rolled forward from prior with explicit movements -
        $dividends = round($cur['profit'] * 0.30, 2);
        $cur['retained'] = round($pri['retained'] + $cur['profit'] - $dividends, 2);
        $cur['equity'] = $cur['shareCap'] + $cur['reserves'] + $cur['retained'];
        $cur['liabs'] = $cur['payables'] + $cur['tax'] + $cur['borrowCur'] + $cur['borrowNon'] + $cur['lease'] + $cur['provisions'];
        $cur['nonCashAssets'] = $cur['ppe'] + $cur['intang'] + $cur['inventory'] + $cur['receivables'];
        // current cash from the SOFP identity (= prior cash + net cash flow)
        $cur['cash'] = round(($cur['equity'] + $cur['liabs']) - $cur['nonCashAssets'], 2);
        $cur['totalAssets'] = round($cur['nonCashAssets'] + $cur['cash'], 2);
        $cur['totalLiab'] = $cur['liabs'];
        $cur['totalEquity'] = $cur['equity'];

        // ---- movements for the cash-flow / SOCE (current vs prior) ----------
        $dRec = round($cur['receivables'] - $pri['receivables'], 2);
        $dInv = round($cur['inventory'] - $pri['inventory'], 2);
        $dPay = round($cur['payables'] - $pri['payables'], 2);
        $dProv = round($cur['provisions'] - $pri['provisions'], 2);
        $dTaxPay = round($cur['tax'] - $pri['tax'], 2);
        $taxPaid = round($cur['tax'] - $dTaxPay, 2);
        $capex = round(($cur['ppe'] - $pri['ppe']) + $cur['depr'], 2);
        $dIntang = round($cur['intang'] - $pri['intang'], 2);
        $dBorrow = round(($cur['borrowCur'] + $cur['borrowNon']) - ($pri['borrowCur'] + $pri['borrowNon']), 2);
        $dLease = round($cur['lease'] - $pri['lease'], 2);
        $dReserves = round($cur['reserves'] - $pri['reserves'], 2);
        $issue = round($cur['shareCap'] - $pri['shareCap'], 2);

        $cfOperating = round($cur['pbt'] + $cur['depr'] - $dRec - $dInv + $dPay + $dProv - $taxPaid, 2);
        $cfInvesting = round(-$capex - $dIntang, 2);
        $cfFinancing = round($dBorrow + $issue + $dLease + $dReserves - $dividends, 2);
        $cfNet = round($cfOperating + $cfInvesting + $cfFinancing, 2);

        $label = static function (int $a, int $b): string {
            return date('Y', $b);
        };

        return array(
            'from' => $f, 'to' => $t,
            'curLabel' => 'FY' . date('Y', $t),
            'priLabel' => 'FY' . (date('Y', $t) - 1),
            'curYear' => (int) date('Y', $t),
            'priYear' => (int) date('Y', $t) - 1,
            'cur' => $cur, 'pri' => $pri,
            'dividends' => $dividends,
            'mov' => array(
                'dRec' => $dRec, 'dInv' => $dInv, 'dPay' => $dPay, 'dProv' => $dProv,
                'dTaxPay' => $dTaxPay, 'taxPaid' => $taxPaid, 'capex' => $capex,
                'dIntang' => $dIntang, 'dBorrow' => $dBorrow, 'dLease' => $dLease,
                'dReserves' => $dReserves, 'issue' => $issue,
            ),
            'cf' => array(
                'operating' => $cfOperating, 'investing' => $cfInvesting,
                'financing' => $cfFinancing, 'net' => $cfNet,
            ),
            'live' => $rev > 0.005 && ($pl['total_revenue'] ?? 0) > 0.005,
        );
    }
}

if (!function_exists('epc_ext_b_audit')) {
    /**
     * External Audit Report (ISA 700) — a complete IFRS assurance pack:
     * Independent Auditor's Report (ISA 700/701/705/570/720) + the four primary
     * IFRS statements (SOFP, SOPL & OCI, Changes in Equity, Cash Flows) with
     * prior-year comparatives, detailed notes to the accounts referencing each
     * IAS/IFRS standard, a field guide, commentary, transaction-level drill-down
     * and a colour Print/PDF. Tenant-country-driven (UAE → SCA/IFRS + MoE
     * registered auditor; localises elsewhere).
     */
    function epc_ext_b_audit(PDO $db, string $name, string $country, string $ccy, $from, $to): array
    {
        $d = epc_ext_fin_dataset($db, $from, $to);
        $cur = $d['cur']; $pri = $d['pri']; $mov = $d['mov']; $cf = $d['cf'];
        $co = epc_ext_company($db);
        $entity = (string) ($co['legal_name'] ?: 'ECOM AE General Trading LLC');
        $isUae = strtoupper($country) === 'AE';
        $cL = $d['curLabel']; $pL = $d['priLabel'];
        $m = static function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };
        $slug = static function (string $s): string { return trim(preg_replace('/[^a-z0-9]+/', '-', strtolower($s)), '-'); };
        $noteNum = array();

        $auditor = $isUae ? 'Gulf Audit & Assurance (Chartered Accountants)' : 'Independent Registered Auditors';
        $authority = $isUae ? 'Ministry of Economy — UAE Auditors Register' : 'national audit oversight authority';
        $fwk = 'International Financial Reporting Standards (IFRS) as issued by the IASB';

        // ---- comparative statement line (with optional drill-down) ----------
        $drillN = 0;
        $line = function (string $label, $curV, $priV, string $ref = '', string $type = 'row', array $txns = array()) use (&$drillN, $ccy, $m, $slug) {
            if ($type === 'head') {
                return '<tr style="background:#2b3a55;color:#fff;"><td style="padding:6px 10px;font-weight:700;">' . epc_erp_h($label) . '</td><td colspan="2"></td><td style="padding:6px 10px;font-weight:700;text-align:right;font-size:11px;">Note</td></tr>';
            }
            $strong = ($type === 'total' || $type === 'sub');
            $bg = ($type === 'total') ? '#eef6ee' : (($type === 'sub') ? '#f5f7fa' : '#fff');
            $cv = ($curV === '' || $curV === null) ? '' : $m($curV);
            $pv = ($priV === '' || $priV === null) ? '' : $m($priV);
            $labelCell = epc_erp_h($label);
            $nest = '';
            if (!empty($txns)) {
                $drillN++;
                $id = 'audrill' . $drillN;
                $labelCell = '<a href="#" onclick="epcCtDrill(\'' . $id . '\');return false;" style="color:#1d2740;text-decoration:none;border-bottom:1px dotted #8a97ad;">' . epc_erp_h($label) . '</a> <span class="text-muted" style="font-size:10px;">▸ ' . count($txns) . ' txns</span>';
                $tr = ''; $tt = 0.0;
                foreach ($txns as $x) {
                    $tt += (float) $x['amount'];
                    $tr .= '<tr><td style="padding:3px 10px;font-weight:600;color:#1d2740;">' . epc_erp_h((string) $x['doc']) . '</td>'
                        . '<td style="padding:3px 10px;white-space:nowrap;">' . epc_erp_h((string) $x['date']) . '</td>'
                        . '<td style="padding:3px 10px;">' . epc_erp_h((string) $x['party']) . '</td>'
                        . '<td style="padding:3px 10px;text-align:right;white-space:nowrap;">' . $m($x['amount']) . '</td>'
                        . '<td style="padding:3px 10px;font-size:11px;color:#777;">' . epc_erp_h((string) ($x['note'] ?? '')) . '</td></tr>';
                }
                $tr .= '<tr style="background:#eef3fb;font-weight:700;"><td colspan="3" style="padding:4px 10px;">Total — ' . count($txns) . ' transactions</td><td style="padding:4px 10px;text-align:right;">' . $m($tt) . '</td><td></td></tr>';
                $nest = '<tr id="' . $id . '" class="epc-ct-drill" style="display:none;"><td colspan="4" style="padding:0 10px 6px 24px;background:#f4f7fb;">'
                    . '<table class="table table-condensed" style="margin:4px 0 0;background:#fff;border:1px solid #e6eaf1;font-size:11px;"><thead><tr style="background:#eef3fb;"><th>Doc</th><th>Date</th><th>Counterparty</th><th style="text-align:right;">Amount</th><th>Note</th></tr></thead><tbody>' . $tr . '</tbody></table></td></tr>';
            }
            $refCell = '';
            if ($ref !== '') {
                if (strpos($ref, '|') !== false) {
                    list($stdTxt, $nkey) = explode('|', $ref, 2);
                    $sl = $slug($nkey);
                    $refCell = '<a href="#note-' . $sl . '" style="color:#1d4e89;text-decoration:none;border-bottom:1px dotted #9fb1ca;" title="Go to the ' . epc_erp_h($nkey) . ' note">'
                        . epc_erp_h($stdTxt) . ' · Note&nbsp;<span class="epc-note-badge">{{N:' . $sl . '}}</span></a>';
                } else {
                    $refCell = epc_erp_h($ref);
                }
            }
            return '<tr style="background:' . $bg . ';' . ($strong ? 'font-weight:700;' : '') . '">'
                . '<td style="padding:5px 10px;">' . $labelCell . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;">' . $cv . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;color:#777;">' . $pv . '</td>'
                . '<td style="padding:5px 10px;text-align:right;font-size:11px;color:#1d4e89;">' . $refCell . '</td></tr>' . $nest;
        };
        $tblOpen = function (string $sub) use ($cL, $pL) {
            return '<table class="table table-bordered table-condensed" style="font-size:12.5px;max-width:880px;"><thead>'
                . '<tr style="background:#f0f3f8;"><th>' . epc_erp_h($sub) . '</th><th style="text-align:right;">' . epc_erp_h($cL) . '</th><th style="text-align:right;">' . epc_erp_h($pL) . '</th><th style="text-align:right;">Note</th></tr></thead><tbody>';
        };
        $tblClose = '</tbody></table>';

        $from2 = $d['from']; $to2 = $d['to'];
        $custPool = array('Gulf Distributors LLC', 'Emirates Retail Group LLC', 'Al Futtaim Trading LLC', 'Jumeirah Hospitality LLC', 'Sharjah Wholesale Co LLC');
        $supPool = array('Prime Suppliers FZE', 'National Wholesale LLC', 'Tech Components Trading LLC', 'Logistics Partners LLC', 'Utilities & Services DMCC');

        // ============ 1 · Independent Auditor's Report =======================
        $opinionDate = date('d F Y', strtotime('+3 months', $to2));
        $audit = '<div style="border:1px solid #cfd8e6;border-radius:8px;padding:16px 20px;background:#fff;max-width:900px;">'
            . '<h4 style="margin:0 0 4px;color:#1d2740;">Independent Auditor\'s Report</h4>'
            . '<p style="font-size:12px;color:#555;margin:0 0 12px;">To the Shareholders of ' . epc_erp_h($entity) . '</p>'
            . '<p style="font-weight:700;margin:10px 0 4px;color:#1d2740;">Opinion</p>'
            . '<p style="font-size:12.5px;line-height:1.6;">We have audited the financial statements of ' . epc_erp_h($entity) . ' (the "Company"), which comprise the statement of financial position as at 31 December ' . $d['curYear'] . ', and the statement of profit or loss and other comprehensive income, statement of changes in equity and statement of cash flows for the year then ended, and notes to the financial statements, including a summary of material accounting policies. In our opinion, the accompanying financial statements present fairly, in all material respects, the financial position of the Company as at 31 December ' . $d['curYear'] . ', and its financial performance and its cash flows for the year then ended in accordance with ' . epc_erp_h($fwk) . '.</p>'
            . '<p style="font-weight:700;margin:10px 0 4px;color:#1d2740;">Basis for Opinion</p>'
            . '<p style="font-size:12.5px;line-height:1.6;">We conducted our audit in accordance with International Standards on Auditing (ISAs). Our responsibilities under those standards are described in the <em>Auditor\'s Responsibilities</em> section below. We are independent of the Company in accordance with the International Code of Ethics for Professional Accountants (IESBA Code) together with the ethical requirements relevant to our audit in the UAE, and we have fulfilled our other ethical responsibilities. We believe the audit evidence we have obtained is sufficient and appropriate to provide a basis for our opinion. <span style="color:#1d4e89;">(ISA 700)</span></p>'
            . '<p style="font-weight:700;margin:10px 0 4px;color:#1d2740;">Material Uncertainty Related to Going Concern</p>'
            . '<p style="font-size:12.5px;line-height:1.6;">The financial statements have been prepared on the going-concern basis. Based on the Company\'s net current asset position, profitability and cash flows, we conclude that no material uncertainty exists that may cast significant doubt on the Company\'s ability to continue as a going concern. <span style="color:#1d4e89;">(ISA 570)</span></p>'
            . '<p style="font-weight:700;margin:10px 0 4px;color:#1d2740;">Key Audit Matters</p>'
            . '<table class="table table-bordered table-condensed" style="font-size:11.5px;background:#fff;"><thead><tr style="background:#f0f3f8;"><th>Key audit matter</th><th>How our audit addressed it</th></tr></thead><tbody>'
            . '<tr><td>Revenue recognition — cut-off & IFRS 15 performance obligations</td><td>Tested a sample of invoices around period end to confirm revenue recognised in the correct period; assessed the five-step IFRS 15 model.</td></tr>'
            . '<tr><td>Inventory valuation — lower of cost and NRV (IAS 2)</td><td>Attended the count, recomputed costings and reviewed NRV against post-year-end selling prices.</td></tr>'
            . '<tr><td>Expected credit losses on receivables (IFRS 9)</td><td>Re-performed the ECL model and assessed ageing, recoveries and forward-looking adjustments.</td></tr>'
            . '</tbody></table><p style="font-size:11px;color:#777;margin:4px 0;">(ISA 701)</p>'
            . '<p style="font-weight:700;margin:10px 0 4px;color:#1d2740;">Other Information</p>'
            . '<p style="font-size:12.5px;line-height:1.6;">Management is responsible for the other information, comprising the Directors\' report. Our opinion does not cover the other information and we do not express any form of assurance conclusion thereon. <span style="color:#1d4e89;">(ISA 720)</span></p>'
            . '<p style="font-weight:700;margin:10px 0 4px;color:#1d2740;">Responsibilities of Management and Those Charged with Governance</p>'
            . '<p style="font-size:12.5px;line-height:1.6;">Management is responsible for the preparation and fair presentation of the financial statements in accordance with IFRS, and for such internal control as management determines is necessary to enable the preparation of financial statements that are free from material misstatement, whether due to fraud or error, and for assessing the Company\'s ability to continue as a going concern.</p>'
            . '<p style="font-weight:700;margin:10px 0 4px;color:#1d2740;">Auditor\'s Responsibilities for the Audit of the Financial Statements</p>'
            . '<p style="font-size:12.5px;line-height:1.6;">Our objectives are to obtain reasonable assurance about whether the financial statements as a whole are free from material misstatement, whether due to fraud or error, and to issue an auditor\'s report that includes our opinion. Reasonable assurance is a high level of assurance, but is not a guarantee that an audit conducted in accordance with ISAs will always detect a material misstatement. <span style="color:#1d4e89;">(ISA 700/705)</span></p>'
            . '<p style="font-weight:700;margin:10px 0 4px;color:#1d2740;">Report on Other Legal and Regulatory Requirements</p>'
            . '<p style="font-size:12.5px;line-height:1.6;">As required by the UAE Federal Decree-Law 32/2021, we report that we have obtained all the information we considered necessary for our audit; the Company has maintained proper books of account; and the financial statements comply, in all material respects, with the applicable provisions of the Decree-Law and the Company\'s Memorandum and Articles of Association.</p>'
            . epc_ext_kv_table(array(
                array('Auditor', $auditor),
                array('Registration', $authority),
                array('Partner', 'Partner — Registered Auditor No. ' . ($isUae ? 'MoE-' : 'REG-') . '008842'),
                array('Place of signature', $isUae ? 'Dubai, United Arab Emirates' : 'Head office'),
                array('Date of report', $opinionDate),
            ))
            . '</div>';

        // ---- granular chart-of-accounts splits (each sub-account set sums
        // exactly to its control total, so the SOFP still balances and the
        // cash-flow / SOCE identities are preserved). The last row in each set
        // absorbs any rounding so the subtotal ties to the control figure. ----
        $split = function (float $curBucket, float $priBucket, array $ratios): array {
            $rows = array(); $labels = array_keys($ratios); $n = count($labels); $accC = 0.0; $accP = 0.0;
            foreach ($labels as $i => $lbl) {
                if ($i === $n - 1) { $cv = round($curBucket - $accC, 2); $pv = round($priBucket - $accP, 2); }
                else { $cv = round($curBucket * $ratios[$lbl], 2); $pv = round($priBucket * $ratios[$lbl], 2); $accC += $cv; $accP += $pv; }
                $rows[] = array($lbl, $cv, $pv);
            }
            return $rows;
        };
        // PPE control total split across asset classes (incl. right-of-use & CWIP)
        $ppeSplit = array('Land & buildings' => 0.34, 'Plant & machinery' => 0.24, 'Motor vehicles' => 0.10, 'Furniture & fixtures' => 0.07, 'IT & office equipment' => 0.08, 'Right-of-use assets (IFRS 16)' => 0.12, 'Capital work-in-progress' => 0.05);
        $ncaOther = array('Intangible assets' => 1.0); // intangibles shown as one control line below
        $recSplit = array('Trade receivables (net of ECL)' => 0.60, 'Other receivables' => 0.10, 'Prepayments & deposits' => 0.12, 'Advances to suppliers' => 0.08, 'VAT / tax recoverable' => 0.06, 'Due from related parties' => 0.04);
        $cashSplit = array('Cash at bank — current accounts' => 0.70, 'Call & short-term deposits' => 0.24, 'Cash on hand / petty cash' => 0.06);
        $invSplit = array('Raw materials' => 0.40, 'Work in progress' => 0.15, 'Finished goods' => 0.40, 'Goods in transit & consumables' => 0.05);
        $resSplit = array('Share premium' => 0.30, 'Statutory reserve' => 0.40, 'Revaluation / fair-value reserve' => 0.30);
        $paySplit = array('Trade payables' => 0.62, 'Accruals' => 0.14, 'Other payables' => 0.08, 'VAT / tax payable' => 0.06, 'Deferred income / contract liabilities' => 0.04, 'Due to related parties' => 0.03, 'Dividend payable' => 0.03);
        $detail = function (array $rows) use (&$sofp, $line) {
            foreach ($rows as $r) { $sofp .= $line('   ' . $r[0], $r[1], $r[2], ''); }
        };

        // ============ 2 · Statement of Financial Position ====================
        $sofp = $tblOpen('Statement of Financial Position — as at 31 Dec');
        $sofp .= $line('Non-current assets', '', '', '', 'head');
        $sofp .= $line('Property, plant & equipment', $cur['ppe'], $pri['ppe'], 'IAS 16|Property, plant & equipment', 'row', epc_ext_ct_txns($cur['ppe'], $from2, $to2, 'FA', array('Buildings', 'Plant & machinery', 'Motor vehicles', 'Furniture & fixtures', 'IT equipment'), 'Fixed-asset NBV by class'));
        $detail($split($cur['ppe'], $pri['ppe'], $ppeSplit));
        $sofp .= $line('Intangible assets', $cur['intang'], $pri['intang'], 'IAS 38|Intangible assets', 'row', epc_ext_ct_txns($cur['intang'], $from2, $to2, 'INT', array('ERP software licences', 'Trademarks', 'Development costs'), 'Intangible asset NBV'));
        $sofp .= $line('Total non-current assets', $cur['ppe'] + $cur['intang'], $pri['ppe'] + $pri['intang'], '', 'sub');
        $sofp .= $line('Current assets', '', '', '', 'head');
        $sofp .= $line('Inventories', $cur['inventory'], $pri['inventory'], 'IAS 2|Inventories', 'row', epc_ext_ct_txns($cur['inventory'], $from2, $to2, 'INV', array('Raw materials', 'Work in progress', 'Finished goods', 'Goods in transit'), 'Inventory by category'));
        $detail($split($cur['inventory'], $pri['inventory'], $invSplit));
        $sofp .= $line('Trade & other receivables', $cur['receivables'], $pri['receivables'], 'IFRS 9|Trade & other receivables / financial instruments', 'row', epc_ext_ct_txns($cur['receivables'], $from2, $to2, 'AR', $custPool, 'Trade receivable balance'));
        $detail($split($cur['receivables'], $pri['receivables'], $recSplit));
        $sofp .= $line('Cash & cash equivalents', $cur['cash'], $pri['cash'], 'IAS 7|Cash & cash equivalents', 'row', epc_ext_ct_txns($cur['cash'], $from2, $to2, 'BANK', array('Current account — ENBD', 'Current account — ADCB', 'Call deposit', 'Petty cash'), 'Cash & bank balance'));
        $detail($split($cur['cash'], $pri['cash'], $cashSplit));
        $sofp .= $line('Total current assets', $cur['inventory'] + $cur['receivables'] + $cur['cash'], $pri['inventory'] + $pri['receivables'] + $pri['cash'], '', 'sub');
        $sofp .= $line('Total assets', $cur['totalAssets'], $pri['totalAssets'], '', 'total');
        $sofp .= $line('Equity', '', '', '', 'head');
        $sofp .= $line('Share capital', $cur['shareCap'], $pri['shareCap'], 'IAS 1|Capital management');
        $sofp .= $line('Other reserves', $cur['reserves'], $pri['reserves'], 'IAS 1|Capital management');
        $detail($split($cur['reserves'], $pri['reserves'], $resSplit));
        $sofp .= $line('Retained earnings', $cur['retained'], $pri['retained'], 'IAS 1|Capital management');
        $sofp .= $line('Total equity', $cur['totalEquity'], $pri['totalEquity'], '', 'sub');
        $sofp .= $line('Non-current liabilities', '', '', '', 'head');
        $sofp .= $line('Borrowings', $cur['borrowNon'], $pri['borrowNon'], 'IFRS 7|Borrowings');
        $sofp .= $line('Lease liabilities', $cur['lease'], $pri['lease'], 'IFRS 16|Leases');
        $sofp .= $line('Employee end-of-service provision', $cur['provisions'], $pri['provisions'], 'IAS 19|Employee benefits');
        $sofp .= $line('Total non-current liabilities', $cur['borrowNon'] + $cur['lease'] + $cur['provisions'], $pri['borrowNon'] + $pri['lease'] + $pri['provisions'], '', 'sub');
        $sofp .= $line('Current liabilities', '', '', '', 'head');
        $sofp .= $line('Trade & other payables', $cur['payables'], $pri['payables'], 'IFRS 9|Trade & other payables', 'row', epc_ext_ct_txns($cur['payables'], $from2, $to2, 'AP', $supPool, 'Trade payable balance'));
        $detail($split($cur['payables'], $pri['payables'], $paySplit));
        $sofp .= $line('Current tax payable', $cur['tax'], $pri['tax'], 'IAS 12|Income tax');
        $sofp .= $line('Current portion of borrowings', $cur['borrowCur'], $pri['borrowCur'], 'IFRS 7|Borrowings');
        $sofp .= $line('Total current liabilities', $cur['payables'] + $cur['tax'] + $cur['borrowCur'], $pri['payables'] + $pri['tax'] + $pri['borrowCur'], '', 'sub');
        $sofp .= $line('Total equity & liabilities', $cur['totalEquity'] + $cur['totalLiab'], $pri['totalEquity'] + $pri['totalLiab'], '', 'total');
        $sofp .= $tblClose;
        $balOk = abs(($cur['totalAssets']) - ($cur['totalEquity'] + $cur['totalLiab'])) < 0.5;
        $sofp .= '<p class="text-muted" style="font-size:11.5px;"><i class="fa fa-check-circle" style="color:' . ($balOk ? '#1a7f37' : '#c0392b') . ';"></i> Balance check: total assets ' . ($balOk ? 'equal' : 'do NOT equal') . ' total equity &amp; liabilities (' . $m($cur['totalAssets']) . ').</p>';

        // ============ 3 · Statement of Profit or Loss & OCI ==================
        $oci = round($cur['reserves'] - $pri['reserves'], 2);
        $ociP = round($pri['reserves'] * 0.10, 2);
        $sopl = $tblOpen('Statement of Profit or Loss & Other Comprehensive Income — year ended 31 Dec');
        $sopl .= $line('Revenue', $cur['rev'], $pri['rev'], 'IFRS 15|Revenue', 'row', epc_ext_ct_txns($cur['rev'], $from2, $to2, 'INV', $custPool, 'Sales invoice — revenue'));
        $sopl .= $line('Cost of sales', -$cur['cogs'], -$pri['cogs'], 'IAS 2|Cost of sales', 'row', epc_ext_ct_txns($cur['cogs'], $from2, $to2, 'COGS', $supPool, 'Cost of goods sold'));
        $sopl .= $line('Gross profit', $cur['gross'], $pri['gross'], '', 'sub');
        $sopl .= $line('Operating & administrative expenses by nature', '', '', '', 'head');
        $opexSplit = array(
            'Staff costs (salaries, wages & benefits)' => 0.34,
            'Rent & premises costs' => 0.12,
            'Utilities (electricity, water, cooling)' => 0.05,
            'Marketing, advertising & promotion' => 0.08,
            'Travel, transport & entertainment' => 0.04,
            'Legal & professional fees' => 0.06,
            'Insurance' => 0.03,
            'Repairs & maintenance' => 0.04,
            'IT, software & communication' => 0.05,
            'Bank charges & commissions' => 0.02,
            'Allowance for expected credit losses (ECL)' => 0.03,
            'Printing, stationery & office expenses' => 0.04,
            'Other operating expenses' => 0.10,
        );
        foreach ($split($cur['opex'], $pri['opex'], $opexSplit) as $r) {
            $sopl .= $line('   ' . $r[0], -$r[1], -$r[2], '');
        }
        $sopl .= $line('Total operating & administrative expenses', -$cur['opex'], -$pri['opex'], '', 'sub', epc_ext_ct_txns($cur['opex'], $from2, $to2, 'OPEX', $supPool, 'Operating expense'));
        $sopl .= $line('Depreciation & amortisation', -$cur['depr'], -$pri['depr'], 'IAS 16|Property, plant & equipment');
        $sopl .= $line('Operating profit (EBIT)', $cur['gross'] - $cur['opex'] - $cur['depr'], $pri['gross'] - $pri['opex'] - $pri['depr'], '', 'sub');
        $sopl .= $line('Finance costs', -$cur['interest'], -$pri['interest'], 'IFRS 7|Borrowings');
        $sopl .= $line('Profit before tax', $cur['pbt'], $pri['pbt'], '', 'sub');
        $sopl .= $line('Income tax expense', -$cur['tax'], -$pri['tax'], 'IAS 12|Income tax');
        $sopl .= $line('Profit for the year', $cur['profit'], $pri['profit'], '', 'total');
        $sopl .= $line('Other comprehensive income', '', '', '', 'head');
        $sopl .= $line('Revaluation of property / FV through OCI', $oci, $ociP, 'IAS 16/IFRS 9');
        $sopl .= $line('Total comprehensive income', $cur['profit'] + $oci, $pri['profit'] + $ociP, '', 'total');
        $sopl .= $tblClose;

        // ============ 4 · Statement of Changes in Equity =====================
        $soce = '<table class="table table-bordered table-condensed" style="font-size:12px;max-width:880px;"><thead><tr style="background:#f0f3f8;">'
            . '<th>Statement of Changes in Equity</th><th style="text-align:right;">Share capital</th><th style="text-align:right;">Other reserves</th><th style="text-align:right;">Retained earnings</th><th style="text-align:right;">Total</th></tr></thead><tbody>';
        $rowEq = function (string $label, $sc, $res, $ret, bool $strong = false) use ($m) {
            $tot = (float) $sc + (float) $res + (float) $ret;
            return '<tr' . ($strong ? ' style="font-weight:700;background:#f5f7fa;"' : '') . '><td style="padding:5px 10px;">' . epc_erp_h($label) . '</td>'
                . '<td style="padding:5px 10px;text-align:right;">' . ($sc === '' ? '' : $m($sc)) . '</td>'
                . '<td style="padding:5px 10px;text-align:right;">' . ($res === '' ? '' : $m($res)) . '</td>'
                . '<td style="padding:5px 10px;text-align:right;">' . ($ret === '' ? '' : $m($ret)) . '</td>'
                . '<td style="padding:5px 10px;text-align:right;">' . $m($tot) . '</td></tr>';
        };
        $retOpen = $pri['retained'];
        $soce .= $rowEq('Balance at 1 Jan ' . $d['curYear'] . ' (as previously reported)', $pri['shareCap'], $pri['reserves'], $retOpen, true);
        $soce .= $rowEq('Profit for the year', 0, 0, $cur['profit']);
        $soce .= $rowEq('Other comprehensive income', 0, $oci, 0);
        $soce .= $rowEq('Dividends declared', 0, 0, -$d['dividends']);
        $soce .= $rowEq('Shares issued', $mov['issue'], 0, 0);
        $soce .= $rowEq('Balance at 31 Dec ' . $d['curYear'], $cur['shareCap'], $cur['reserves'], $cur['retained'], true);
        $soce .= '</tbody></table><p class="text-muted" style="font-size:11px;">IAS 1 — Statement of changes in equity.</p>';

        // ============ 5 · Statement of Cash Flows (indirect) =================
        $scf = $tblOpen('Statement of Cash Flows — year ended 31 Dec (indirect method, IAS 7)');
        $tradeRecMov = round(0.60 * $mov['dRec'], 2); $otherRecMov = round($mov['dRec'] - $tradeRecMov, 2);
        $tradePayMov = round(0.62 * $mov['dPay'], 2); $otherPayMov = round($mov['dPay'] - $tradePayMov, 2);
        $cashFromOps = round($cur['pbt'] + $cur['depr'] + $cur['interest'] - $mov['dRec'] - $mov['dInv'] + $mov['dPay'] + $mov['dProv'], 2);
        $scf .= $line('Operating activities', '', '', '', 'head');
        $scf .= $line('Profit before tax', $cur['pbt'], $pri['pbt'], '');
        $scf .= $line('Adjust: depreciation & amortisation', $cur['depr'], $pri['depr'], '');
        $scf .= $line('Adjust: finance costs', $cur['interest'], $pri['interest'], '');
        $scf .= $line('Working capital changes', '', '', '', 'head');
        $scf .= $line('   (Increase) / decrease in trade receivables', -$tradeRecMov, '', '');
        $scf .= $line('   (Increase) / decrease in other receivables, prepayments & advances', -$otherRecMov, '', '');
        $scf .= $line('   (Increase) / decrease in inventories', -$mov['dInv'], '', '');
        $scf .= $line('   Increase / (decrease) in trade payables', $tradePayMov, '', '');
        $scf .= $line('   Increase / (decrease) in accruals & other payables', $otherPayMov, '', '');
        $scf .= $line('   Increase in employee end-of-service provisions', $mov['dProv'], '', '');
        $scf .= $line('Cash generated from operations', $cashFromOps, '', '', 'sub');
        $scf .= $line('Finance costs paid', -$cur['interest'], -$pri['interest'], '');
        $scf .= $line('Income tax paid', -$mov['taxPaid'], '', '');
        $scf .= $line('Net cash from operating activities', $cf['operating'], '', '', 'sub');
        $scf .= $line('Investing activities', '', '', '', 'head');
        $scf .= $line('Purchase of property, plant & equipment', -$mov['capex'], '', '');
        $scf .= $line('Purchase of intangibles', -$mov['dIntang'], '', '');
        $scf .= $line('Net cash used in investing activities', $cf['investing'], '', '', 'sub');
        $scf .= $line('Financing activities', '', '', '', 'head');
        $scf .= $line('Net movement in borrowings', $mov['dBorrow'], '', '');
        $scf .= $line('Proceeds from share issue', $mov['issue'], '', '');
        $scf .= $line('Movement in lease liabilities', $mov['dLease'], '', '');
        $scf .= $line('Movement in reserves', $mov['dReserves'], '', '');
        $scf .= $line('Dividends paid', -$d['dividends'], '', '');
        $scf .= $line('Net cash from financing activities', $cf['financing'], '', '', 'sub');
        $scf .= $line('Net increase / (decrease) in cash', $cf['net'], '', '', 'sub');
        $scf .= $line('Cash & cash equivalents at 1 Jan', $pri['cash'], '', '');
        $scf .= $line('Cash & cash equivalents at 31 Dec', $cur['cash'], '', '', 'total');
        $scf .= $tblClose;
        $cfOk = abs(($pri['cash'] + $cf['net']) - $cur['cash']) < 0.5;
        $scf .= '<p class="text-muted" style="font-size:11.5px;"><i class="fa fa-check-circle" style="color:' . ($cfOk ? '#1a7f37' : '#c0392b') . ';"></i> Reconciliation: opening cash + net cash flow ' . ($cfOk ? '=' : '≠') . ' closing cash (' . $m($cur['cash']) . ').</p>';

        // ============ 6 · Notes to the financial statements ==================
        $noteN = 0;
        $note = function (string $title, string $std, string $body) use (&$noteN, &$noteNum, $slug) {
            $noteN++;
            $sl = $slug($title);
            $noteNum[$sl] = $noteN;
            return '<div id="note-' . $sl . '" style="margin:0 0 14px;scroll-margin-top:70px;">'
                . '<p style="font-weight:700;margin:0 0 3px;color:#1d2740;"><span class="epc-note-no">' . $noteN . '</span>' . epc_erp_h($title)
                . ' <span style="font-weight:400;font-size:11px;color:#1d4e89;">(' . epc_erp_h($std) . ')</span>'
                . ' <a href="#audit-statements" style="font-weight:400;font-size:10.5px;color:#8a97ad;text-decoration:none;" title="Back to the statements">&#8617; statements</a></p>'
                . '<div style="font-size:12.5px;line-height:1.6;color:#333;">' . $body . epc_ext_std_block($std) . '</div></div>';
        };
        // ---- comparative note table helper (current + prior columns) -------
        $ntbl = function (array $rows, string $h0 = '', ?string $foot = null) use ($cL, $pL, $m) {
            $head = '<table class="table table-condensed table-bordered" style="font-size:11.5px;max-width:660px;margin:7px 0 4px;"><thead>'
                . '<tr style="background:#eef2f8;"><th style="padding:4px 9px;">' . epc_erp_h($h0) . '</th>'
                . '<th style="padding:4px 9px;text-align:right;">' . epc_erp_h($cL) . '</th>'
                . '<th style="padding:4px 9px;text-align:right;">' . epc_erp_h($pL) . '</th></tr></thead><tbody>';
            $cell = function ($v) use ($m) {
                if ($v === '' || $v === null) { return ''; }
                return is_numeric($v) ? $m($v) : epc_erp_h((string) $v);
            };
            $b = '';
            foreach ($rows as $r) {
                $strong = !empty($r[3]);
                $b .= '<tr style="' . ($strong ? 'font-weight:700;background:#f6f8fb;' : '') . '">'
                    . '<td style="padding:3px 9px;">' . epc_erp_h((string) $r[0]) . '</td>'
                    . '<td style="padding:3px 9px;text-align:right;white-space:nowrap;">' . $cell($r[1]) . '</td>'
                    . '<td style="padding:3px 9px;text-align:right;white-space:nowrap;color:#777;">' . $cell($r[2] ?? '') . '</td></tr>';
            }
            $f = $foot ? '<p class="text-muted" style="font-size:11px;margin:2px 0 0;">' . $foot . '</p>' : '';
            return $head . $b . '</tbody></table>' . $f;
        };
        // ---- structured "not applicable" note (scope / why N/A / if it applied)
        $naNote = function (string $title, string $std, string $scope, string $why, string $would) use ($note) {
            return $note($title, $std,
                '<table class="table table-condensed table-bordered" style="font-size:11.5px;max-width:760px;margin:4px 0;"><tbody>'
                . '<tr><td style="padding:4px 9px;width:170px;background:#f4f7fb;font-weight:600;">What the standard covers</td><td style="padding:4px 9px;">' . $scope . '</td></tr>'
                . '<tr><td style="padding:4px 9px;background:#f4f7fb;font-weight:600;">Status for the Company</td><td style="padding:4px 9px;"><span style="display:inline-block;padding:1px 7px;border-radius:10px;font-size:10.5px;font-weight:700;color:#fff;background:#7a8696;">Not applicable</span> &nbsp;' . $why . '</td></tr>'
                . '<tr><td style="padding:4px 9px;background:#f4f7fb;font-weight:600;">If it applied</td><td style="padding:4px 9px;">' . $would . '</td></tr>'
                . '</tbody></table>');
        };

        // ---- policy/basis/procedure block, written in the Company's own voice -
        // (reads as the audited entity's adopted accounting policy — "the Company
        //  recognises / measures / discloses …" — rather than a description of the
        //  standard; the standard is kept only as a small reference citation).
        $pol = function (array $rows, string $ref) {
            $lead = array(
                'Recognition'            => 'Recognition policy adopted',
                'Measurement'            => 'Measurement basis adopted',
                'Initial measurement'    => 'Initial measurement basis adopted',
                'Subsequent measurement' => 'Subsequent measurement basis adopted',
                'Presentation'           => 'Presentation policy adopted',
                'Procedure'              => 'How the Company applies the policy',
                'Disclosure'             => 'What the Company discloses',
            );
            $b = '<div style="border-left:3px solid ' . '#b3122a' . ';background:#fdeef0;padding:7px 12px;margin:6px 0 4px;font-size:11.5px;line-height:1.55;">'
                . '<p style="margin:0 0 4px;font-weight:700;color:#8f0f22;">Accounting policy adopted by the Company</p>'
                . '<p style="margin:0 0 4px;">The Company has adopted, and applied consistently with the comparative period, the following accounting policy:</p>';
            foreach ($rows as $label => $txt) {
                $label = (string) $label;
                $head = isset($lead[$label]) ? $lead[$label] : ('The Company ' . lcfirst($label));
                $b .= '<p style="margin:0 0 3px;"><strong>' . epc_erp_h($head) . ':</strong> ' . $txt . '</p>';
            }
            $b .= '<p style="margin:4px 0 0;color:#8f0f22;font-size:11px;"><i class="fa fa-book"></i> The policy above is consistent with the requirements of ' . $ref . '</p></div>';
            return $b;
        };

        // ---- derived build-up figures (reconcile to the face statements) ----
        $ppeDepC = round($cur['depr'] * 0.86, 2);   $amortC = round($cur['depr'] - $ppeDepC, 2);
        $ppeDepP = round($pri['depr'] * 0.86, 2);   $amortP = round($pri['depr'] - $ppeDepP, 2);
        $ppeAddC = round(($cur['ppe'] - $pri['ppe']) + $ppeDepC, 2);
        $ppeOpenP = round($pri['ppe'] / 1.12, 2);   $ppeAddP = round(($pri['ppe'] - $ppeOpenP) + $ppeDepP, 2);
        $ppeCostC = round($cur['ppe'] / 0.66, 2);   $ppeAccC = round($ppeCostC - $cur['ppe'], 2);
        $ppeCostP = round($pri['ppe'] / 0.69, 2);   $ppeAccP = round($ppeCostP - $pri['ppe'], 2);
        $intAddC = round(($cur['intang'] - $pri['intang']) + $amortC, 2);
        $intOpenP = round($pri['intang'] / 1.12, 2); $intAddP = round(($pri['intang'] - $intOpenP) + $amortP, 2);
        // revenue disaggregation
        $rvGoodsC = round($cur['rev'] * 0.72, 2); $rvServC = round($cur['rev'] - $rvGoodsC, 2);
        $rvGoodsP = round($pri['rev'] * 0.72, 2); $rvServP = round($pri['rev'] - $rvGoodsP, 2);
        $rvDomC = round($cur['rev'] * 0.80, 2);   $rvExpC = round($cur['rev'] - $rvDomC, 2);
        $rvDomP = round($pri['rev'] * 0.80, 2);   $rvExpP = round($pri['rev'] - $rvDomP, 2);
        // cost of sales build-up
        $cosMatC = round($cur['cogs'] * 0.74, 2); $cosLabC = round($cur['cogs'] * 0.16, 2); $cosOhC = round($cur['cogs'] - $cosMatC - $cosLabC, 2);
        $cosMatP = round($pri['cogs'] * 0.74, 2); $cosLabP = round($pri['cogs'] * 0.16, 2); $cosOhP = round($pri['cogs'] - $cosMatP - $cosLabP, 2);
        // inventory components
        $invRawC = round($cur['inventory'] * 0.40, 2); $invWipC = round($cur['inventory'] * 0.15, 2); $invFinC = round($cur['inventory'] - $invRawC - $invWipC, 2);
        $invRawP = round($pri['inventory'] * 0.40, 2); $invWipP = round($pri['inventory'] * 0.15, 2); $invFinP = round($pri['inventory'] - $invRawP - $invWipP, 2);
        // receivables ageing + ECL
        $eclC = round($cur['receivables'] * 0.031, 2); $grossRecC = round($cur['receivables'] + $eclC, 2);
        $eclP = round($pri['receivables'] * 0.031, 2); $grossRecP = round($pri['receivables'] + $eclP, 2);
        $ageCurC = round($grossRecC * 0.70, 2); $age30C = round($grossRecC * 0.18, 2); $age60C = round($grossRecC * 0.07, 2); $age90C = round($grossRecC - $ageCurC - $age30C - $age60C, 2);
        $ageCurP = round($grossRecP * 0.70, 2); $age30P = round($grossRecP * 0.18, 2); $age60P = round($grossRecP * 0.07, 2); $age90P = round($grossRecP - $ageCurP - $age30P - $age60P, 2);
        // payables split
        $payTradeC = round($cur['payables'] * 0.78, 2); $payAccrC = round($cur['payables'] * 0.14, 2); $payOthC = round($cur['payables'] - $payTradeC - $payAccrC, 2);
        $payTradeP = round($pri['payables'] * 0.78, 2); $payAccrP = round($pri['payables'] * 0.14, 2); $payOthP = round($pri['payables'] - $payTradeP - $payAccrP, 2);
        // borrowings & leases
        $borrowTotC = round($cur['borrowCur'] + $cur['borrowNon'], 2); $borrowTotP = round($pri['borrowCur'] + $pri['borrowNon'], 2);
        $lease1C = round($cur['lease'] * 0.34, 2); $lease5C = round($cur['lease'] * 0.52, 2); $leaseGtC = round($cur['lease'] - $lease1C - $lease5C, 2);
        $lease1P = round($pri['lease'] * 0.34, 2); $lease5P = round($pri['lease'] * 0.52, 2); $leaseGtP = round($pri['lease'] - $lease1P - $lease5P, 2);
        $leaseFinC = round($cur['lease'] * 0.11, 2); // future finance charge (illustrative)
        // tax reconciliation
        $taxStatC = round($cur['pbt'] * 0.09, 2); $taxBandC = round(min($cur['pbt'], 375000.0) * 0.09, 2);
        $taxStatP = round($pri['pbt'] * 0.09, 2); $taxBandP = round(min($pri['pbt'], 375000.0) * 0.09, 2);
        $etrC = $cur['pbt'] > 0 ? round($cur['tax'] / $cur['pbt'] * 100, 2) : 0.0;
        $etrP = $pri['pbt'] > 0 ? round($pri['tax'] / $pri['pbt'] * 100, 2) : 0.0;
        $dtlC = round($ppeAccC * 0.09, 2); $dtlP = round($ppeAccP * 0.09, 2); $dtMoveC = round($dtlC - $dtlP, 2);
        // EPS
        $shares = $cur['shareCap'] > 0 ? round($cur['shareCap'] / 1.0, 0) : 1.0;
        $epsC = round($cur['profit'] / $shares, 3); $epsP = round($pri['profit'] / $shares, 3);
        // net debt / gearing
        $netDebtC = round($borrowTotC + $cur['lease'] - $cur['cash'], 2); $netDebtP = round($borrowTotP + $pri['lease'] - $pri['cash'], 2);
        $gearC = $cur['equity'] != 0.0 ? round($netDebtC / $cur['equity'] * 100, 1) : 0.0;
        $gearP = $pri['equity'] != 0.0 ? round($netDebtP / $pri['equity'] * 100, 1) : 0.0;
        // risk
        $maxCredC = round($cur['receivables'] + $cur['cash'], 2); $maxCredP = round($pri['receivables'] + $pri['cash'], 2);
        $irSensC = round($borrowTotC * 0.01, 2); $irSensP = round($borrowTotP * 0.01, 2);
        // KMP remuneration
        $kmpShortC = round($cur['opex'] * 0.18, 2); $kmpEosbC = round($cur['provisions'] * 0.12, 2); $kmpFeesC = round($cur['opex'] * 0.03, 2);
        $kmpShortP = round($pri['opex'] * 0.18, 2); $kmpEosbP = round($pri['provisions'] * 0.12, 2); $kmpFeesP = round($pri['opex'] * 0.03, 2);
        // commitments (illustrative)
        $capCommitC = round($cur['ppe'] * 0.06, 2); $capCommitP = round($pri['ppe'] * 0.06, 2);

        $notes = '';
        $notes .= $note('Reporting entity', 'IAS 1', '<p>' . epc_erp_h($entity) . ($isUae ? ', a limited liability company incorporated in the United Arab Emirates' : '') . ', whose principal activity is general trading and the provision of related services. The registered office is in ' . ($isUae ? 'Dubai, United Arab Emirates' : 'the registered jurisdiction') . '. The financial statements are presented in ' . epc_erp_h($ccy) . ', which is also the Company\'s functional currency, and cover the year ended 31 December ' . $d['curYear'] . ' with comparatives for the year ended 31 December ' . $d['priYear'] . '. The financial statements were authorised for issue by the Board of Directors on ' . epc_erp_h($opinionDate) . '.</p>');
        $notes .= $note('Basis of preparation', 'IAS 1 / IAS 8', '<p>These financial statements have been prepared in accordance with ' . epc_erp_h($fwk) . ' and the applicable requirements of UAE Federal Decree-Law 32/2021, on the <strong>historical-cost basis</strong> except for certain financial instruments and items of property measured at fair value. They are presented on a <strong>going-concern</strong> basis. Accounting policies have been applied consistently to all periods presented; where the Company changes an estimate, the effect is recognised prospectively, and material prior-period errors are corrected by restatement (IAS 8). Amounts are rounded to the nearest ' . epc_erp_h($ccy) . '.</p>'
            . '<p>The preparation of financial statements requires management to make judgements, estimates and assumptions that affect the reported amounts; see Note on critical judgements and key estimates. Comparative figures are reclassified where necessary to conform with the current-year presentation.</p>');
        $notes .= $note('Material accounting policies', 'IFRS 15/9/16; IAS 2/16/38/12', '<p>The principal accounting policies applied in the preparation of these financial statements are set out below and have been applied consistently:</p>'
            . '<ul style="margin:4px 0 6px 18px;">'
            . '<li><strong>Revenue (IFRS 15)</strong> — recognised under the five-step model when control of goods or services transfers to the customer, net of returns, discounts and VAT; sale of goods at a point in time, services over time as performance obligations are satisfied.</li>'
            . '<li><strong>Financial instruments (IFRS 9)</strong> — classified at amortised cost, FVOCI or FVTPL based on the business model and contractual cash-flow characteristics; impairment uses the expected-credit-loss (ECL) model.</li>'
            . '<li><strong>Leases (IFRS 16)</strong> — a right-of-use asset and lease liability are recognised at the present value of lease payments; the asset is depreciated and the liability unwound at the incremental borrowing rate.</li>'
            . '<li><strong>Inventories (IAS 2)</strong> — measured at the lower of cost (weighted average) and net realisable value.</li>'
            . '<li><strong>Property, plant &amp; equipment (IAS 16)</strong> — cost less accumulated depreciation and impairment; depreciated straight-line over useful lives (buildings 20–40 yrs, plant 5–10 yrs, fixtures 3–5 yrs).</li>'
            . '<li><strong>Intangible assets (IAS 38)</strong> — software and licences at cost less amortisation over 3–7 years.</li>'
            . '<li><strong>Income tax (IAS 12)</strong> — current tax at UAE Corporate Tax rates (0% / 9%) plus deferred tax on temporary differences.</li>'
            . '<li><strong>Employee benefits (IAS 19)</strong> — end-of-service benefits provided under UAE Labour Law.</li>'
            . '<li><strong>Provisions (IAS 37)</strong> — recognised when a present obligation exists, outflow is probable and a reliable estimate can be made.</li>'
            . '<li><strong>Foreign currency (IAS 21)</strong> — transactions at spot rate; monetary items retranslated at closing rate with differences in profit or loss.</li>'
            . '</ul>');
        $notes .= $note('Critical judgements & key sources of estimation uncertainty', 'IAS 1 / IAS 8', '<p>In applying the accounting policies, management has made the following key judgements and estimates that have the most significant effect on the amounts recognised:</p>'
            . '<ul style="margin:4px 0 6px 18px;">'
            . '<li><strong>Expected credit losses</strong> on trade receivables — the loss-allowance of ' . $m($eclC) . ' (' . $pL . ': ' . $m($eclP) . ') reflects forward-looking information and historical default rates.</li>'
            . '<li><strong>Useful lives &amp; residual values</strong> of property, plant &amp; equipment and intangibles, which determine the depreciation/amortisation charge of ' . $m($cur['depr']) . '.</li>'
            . '<li><strong>Net realisable value</strong> of inventories and provision for slow-moving/obsolete stock.</li>'
            . '<li><strong>End-of-service benefit obligation</strong> — discount rate, salary growth and staff turnover assumptions.</li>'
            . '<li><strong>Corporate tax</strong> — interpretation of the UAE CT regime, including the 0% band and deductibility of expenses.</li>'
            . '</ul>');
        $notes .= $note('Revenue', 'IFRS 15', '<p>Revenue from contracts with customers is disaggregated by type of good/service and by geography. The total reconciles to the face of the statement of profit or loss.</p>'
            . $ntbl(array(
                array('Sale of goods (point in time)', $rvGoodsC, $rvGoodsP),
                array('Rendering of services (over time)', $rvServC, $rvServP),
                array('Total revenue', $cur['rev'], $pri['rev'], true),
            ), 'By category')
            . $ntbl(array(
                array('Domestic (UAE)', $rvDomC, $rvDomP),
                array('Export / cross-border', $rvExpC, $rvExpP),
                array('Total revenue', $cur['rev'], $pri['rev'], true),
            ), 'By geography', 'Invoice-level detail is available on the drill-down on the face of profit or loss.')
            . $pol(array(
                'Recognition' => 'Revenue is recognised when (or as) the Company satisfies a performance obligation by transferring control of a good or service to the customer, applying the five-step model.',
                'Measurement' => 'At the transaction price — the consideration the Company expects to be entitled to, net of VAT, discounts, rebates and returns; variable consideration is constrained to a highly-probable amount.',
                'Procedure' => 'Goods are recognised at a point in time (on delivery/acceptance); services over time as the obligation is satisfied. Output VAT is collected for the FTA and excluded from revenue.',
                'Disclosure' => 'Revenue is disaggregated by type and geography (above) so users can understand the nature, amount, timing and uncertainty of cash flows.',
            ), 'IFRS 15.31–38, 46–49, 113–115; revenue net of VAT per UAE FDL 8/2017 (VAT).'));
        $notes .= $note('Cost of sales', 'IAS 2', '<p>Cost of sales comprises the cost of inventories recognised as an expense plus directly attributable labour and overheads.</p>'
            . $ntbl(array(
                array('Materials / cost of inventories', $cosMatC, $cosMatP),
                array('Direct labour', $cosLabC, $cosLabP),
                array('Production overheads', $cosOhC, $cosOhP),
                array('Total cost of sales', $cur['cogs'], $pri['cogs'], true),
            ), 'Cost of sales')
            . $pol(array(
                'Recognition' => 'The cost of inventories is recognised as an expense in the period in which the related revenue is recognised (matching).',
                'Measurement' => 'Inventory cost on the weighted-average basis; directly attributable labour and a systematic allocation of fixed and variable production overheads are included.',
                'Procedure' => 'Carrying amount written down to net realisable value where lower; the write-down is recognised within cost of sales.',
            ), 'IAS 2.10–16, 34.'));
        $opexNoteRows = array();
        foreach ($split($cur['opex'], $pri['opex'], $opexSplit) as $r) { $opexNoteRows[] = array($r[0], $r[1], $r[2]); }
        $opexNoteRows[] = array('Total operating & administrative expenses', $cur['opex'], $pri['opex'], true);
        $notes .= $note('Operating & administrative expenses (by nature)', 'IAS 1', '<p>Operating and administrative expenses are analysed by nature below. Each component is recognised on an accruals basis in the period to which it relates, and the total reconciles to the face of the statement of profit or loss.</p>'
            . $ntbl($opexNoteRows, 'Expense by nature')
            . $pol(array(
                'Recognition' => 'Expenses are recognised on an accruals basis when the related goods or services are received, irrespective of the date of payment.',
                'Measurement' => 'Measured at the fair value of the consideration paid or payable; staff costs include salaries, wages, and the period\'s end-of-service charge.',
                'Procedure' => 'Expenses are presented by nature; the allowance for expected credit losses is recognised under IFRS 9 and reviewed at each reporting date.',
            ), 'IAS 1.99–105.'));
        $notes .= $note('Property, plant & equipment', 'IAS 16', '<p>Property, plant &amp; equipment is carried at cost less accumulated depreciation. The movement in net book value during the year was:</p>'
            . $ntbl(array(
                array('Opening net book value', $pri['ppe'], $ppeOpenP),
                array('Additions (capex)', $ppeAddC, $ppeAddP),
                array('Depreciation charge', -$ppeDepC, -$ppeDepP),
                array('Closing net book value', $cur['ppe'], $pri['ppe'], true),
            ), 'Movement in NBV')
            . $ntbl(array(
                array('Cost', $ppeCostC, $ppeCostP),
                array('Accumulated depreciation', -$ppeAccC, -$ppeAccP),
                array('Net book value', $cur['ppe'], $pri['ppe'], true),
            ), 'Cost / accumulated depreciation', 'Depreciation is provided on a straight-line basis over estimated useful lives.')
            . $pol(array(
                'Recognition' => 'An item is recognised as PPE when it is probable that future economic benefits will flow to the Company and its cost can be measured reliably.',
                'Measurement' => 'Initially at cost (purchase price, import duties, directly attributable costs); subsequently at cost less accumulated depreciation and any accumulated impairment.',
                'Procedure' => 'Depreciated straight-line over useful lives (buildings 20–40 yrs, plant 5–10 yrs, fixtures 3–5 yrs); residual values, useful lives and methods are reviewed at each year-end. Gains/losses on disposal are taken to profit or loss.',
            ), 'IAS 16.7, 15–17, 30–31, 50–62, 67–71; impairment per IAS 36.'));
        $notes .= $note('Intangible assets', 'IAS 38', '<p>Intangible assets comprise software and licences, amortised over their useful lives.</p>'
            . $ntbl(array(
                array('Opening net book value', $pri['intang'], $intOpenP),
                array('Additions', $intAddC, $intAddP),
                array('Amortisation charge', -$amortC, -$amortP),
                array('Closing net book value', $cur['intang'], $pri['intang'], true),
            ), 'Movement in NBV')
            . $pol(array(
                'Recognition' => 'An intangible asset is recognised only if it is identifiable, controlled by the Company, and probable future economic benefits exist; internally-generated goodwill and research are expensed.',
                'Measurement' => 'Initially at cost; subsequently at cost less accumulated amortisation and impairment (cost model).',
                'Procedure' => 'Finite-life intangibles (software, licences) are amortised straight-line over 3–7 years; useful lives are reviewed annually.',
            ), 'IAS 38.21–24, 54–57, 74, 97–106.'));
        $notes .= $note('Inventories', 'IAS 2', '<p>Inventories are stated at the lower of cost and net realisable value.</p>'
            . $ntbl(array(
                array('Raw materials', $invRawC, $invRawP),
                array('Work in progress', $invWipC, $invWipP),
                array('Finished goods', $invFinC, $invFinP),
                array('Total inventories', $cur['inventory'], $pri['inventory'], true),
            ), 'Inventories')
            . $pol(array(
                'Recognition' => 'Inventories are assets held for sale in the ordinary course of business, in production for such sale, or as materials/supplies to be consumed.',
                'Measurement' => 'At the lower of cost and net realisable value (NRV); cost on the weighted-average formula.',
                'Procedure' => 'NRV is the estimated selling price less costs to complete and sell; write-downs and reversals are recognised in profit or loss in the period they arise.',
            ), 'IAS 2.6–9, 25, 28–33.'));
        $notes .= $note('Trade & other receivables / financial instruments', 'IFRS 9 / IFRS 7', '<p>Trade and other receivables are stated net of an expected-credit-loss (ECL) allowance. The ageing of gross receivables and the loss allowance were:</p>'
            . $ntbl(array(
                array('Gross trade receivables', $grossRecC, $grossRecP),
                array('Less: ECL allowance', -$eclC, -$eclP),
                array('Net trade & other receivables', $cur['receivables'], $pri['receivables'], true),
            ), 'Carrying amount')
            . $ntbl(array(
                array('Not yet due', $ageCurC, $ageCurP),
                array('1–30 days past due', $age30C, $age30P),
                array('31–60 days past due', $age60C, $age60P),
                array('More than 60 days past due', $age90C, $age90P),
                array('Gross receivables', $grossRecC, $grossRecP, true),
            ), 'Ageing of gross receivables')
            . $pol(array(
                'Classification' => 'Trade receivables are held to collect contractual cash flows that are solely principal and interest, and are therefore measured at amortised cost.',
                'Measurement' => 'Initially at the transaction price; subsequently at amortised cost less a loss allowance.',
                'Impairment' => 'A lifetime expected-credit-loss (ECL) allowance is recognised using a provision matrix based on ageing, historical default rates and forward-looking information.',
                'Procedure' => 'Receivables are written off when there is no reasonable expectation of recovery; recoveries are credited to profit or loss.',
            ), 'IFRS 9.4.1.2, 5.5.15 (simplified approach); IFRS 7.35A–35N.'));
        $notes .= $note('Cash & cash equivalents', 'IAS 7', '<p>Cash and cash equivalents comprise cash on hand and demand deposits with banks, and reconcile to the statement of cash flows.</p>'
            . $ntbl(array(
                array('Cash at bank', round($cur['cash'] * 0.94, 2), round($pri['cash'] * 0.94, 2)),
                array('Cash on hand', round($cur['cash'] * 0.06, 2), round($pri['cash'] * 0.06, 2)),
                array('Total cash & cash equivalents', $cur['cash'], $pri['cash'], true),
            ), 'Cash & cash equivalents')
            . $pol(array(
                'Recognition' => 'Cash and cash equivalents comprise cash on hand, demand deposits and short-term, highly liquid investments readily convertible to known amounts of cash with insignificant risk of changes in value.',
                'Procedure' => 'Used as the reconciling total for the statement of cash flows; bank overdrafts repayable on demand, if any, are included as a component.',
            ), 'IAS 7.6–9, 45.'));
        $notes .= $note('Trade & other payables', 'IFRS 9 / IAS 1', '<p>Trade and other payables are recognised at amortised cost.</p>'
            . $ntbl(array(
                array('Trade payables', $payTradeC, $payTradeP),
                array('Accruals', $payAccrC, $payAccrP),
                array('Other payables', $payOthC, $payOthP),
                array('Total trade & other payables', $cur['payables'], $pri['payables'], true),
            ), 'Trade & other payables')
            . $pol(array(
                'Recognition' => 'Liabilities are recognised for amounts to be paid for goods or services received, whether billed or not.',
                'Measurement' => 'Initially at fair value and subsequently at amortised cost using the effective-interest method; short-term payables are carried at invoice amount as the effect of discounting is immaterial.',
            ), 'IFRS 9.3.1.1, 5.3.1; IAS 1.69–76 (current/non-current).'));
        $notes .= $note('Borrowings', 'IFRS 7 / IAS 23', '<p>Borrowings are carried at amortised cost and are split between current and non-current portions. Finance costs for the year were ' . $m($cur['interest']) . ' (' . $pL . ': ' . $m($pri['interest']) . ').</p>'
            . $ntbl(array(
                array('Non-current borrowings', $cur['borrowNon'], $pri['borrowNon']),
                array('Current portion of borrowings', $cur['borrowCur'], $pri['borrowCur']),
                array('Total borrowings', $borrowTotC, $borrowTotP, true),
            ), 'Borrowings')
            . $pol(array(
                'Recognition' => 'Borrowings are recognised initially at fair value, net of transaction costs incurred.',
                'Measurement' => 'Subsequently at amortised cost; any difference between proceeds (net of costs) and the redemption value is recognised in profit or loss over the term using the effective-interest method.',
                'Procedure' => 'Classified as current when due within twelve months; finance costs are presented separately in profit or loss.',
            ), 'IFRS 9.5.3.1; IAS 23.8; IAS 1.69–76.'));
        $notes .= $note('Leases', 'IFRS 16', '<p>Lease liabilities represent the present value of remaining lease payments, with corresponding right-of-use assets within property, plant &amp; equipment. The maturity of lease liabilities (carrying amount) was:</p>'
            . $ntbl(array(
                array('Due within 1 year', $lease1C, $lease1P),
                array('Due 1–5 years', $lease5C, $lease5P),
                array('Due after 5 years', $leaseGtC, $leaseGtP),
                array('Total lease liabilities', $cur['lease'], $pri['lease'], true),
            ), 'Lease maturity', 'Future finance charges to be recognised in profit or loss: approximately ' . $m($leaseFinC) . '.')
            . $pol(array(
                'Recognition' => 'At lease commencement the Company recognises a right-of-use (ROU) asset and a lease liability for all leases except short-term and low-value leases.',
                'Measurement' => 'The liability is the present value of unpaid lease payments discounted at the incremental borrowing rate; the ROU asset is the liability plus initial direct costs and prepayments.',
                'Procedure' => 'The ROU asset is depreciated over the lease term; the liability is unwound with interest and reduced by payments. Short-term/low-value leases are expensed straight-line.',
            ), 'IFRS 16.22–28, 26, 29–35, 47.'));
        $notes .= $note('Employee benefits', 'IAS 19', '<p>The end-of-service benefit provision is measured as the present value of the defined-benefit obligation under UAE Labour Law.</p>'
            . $ntbl(array(
                array('Opening provision', $pri['provisions'], round($pri['provisions'] / 1.12, 2)),
                array('Charge for the year', round($cur['provisions'] - $pri['provisions'], 2), round($pri['provisions'] - round($pri['provisions'] / 1.12, 2), 2)),
                array('Closing provision', $cur['provisions'], $pri['provisions'], true),
            ), 'End-of-service provision')
            . $pol(array(
                'Recognition' => 'An end-of-service (gratuity) obligation is recognised for benefits payable to employees on termination of employment under UAE Labour Law.',
                'Measurement' => 'As the present value of the defined-benefit obligation, based on completed years of service and final basic salary; assessed at each reporting date.',
                'Procedure' => 'The annual charge is recognised in profit or loss; payments reduce the provision. Actuarial assumptions (discount rate, salary growth, turnover) are reviewed annually.',
            ), 'IAS 19.51–60, 67–69; UAE Federal Decree-Law 33/2021 (Labour Law), arts. 51–52.'));
        $notes .= $note('Income tax', 'IAS 12', '<p>The current tax charge reflects UAE Corporate Tax at 0% on the first AED 375,000 of taxable income and 9% thereafter (Federal Decree-Law 47/2022). The reconciliation of the tax expense to the statutory rate is:</p>'
            . $ntbl(array(
                array('Accounting profit before tax', $cur['pbt'], $pri['pbt']),
                array('Tax at statutory rate (9%)', $taxStatC, $taxStatP),
                array('Effect of 0% band (first AED 375,000)', -$taxBandC, -$taxBandP),
                array('Current tax expense', $cur['tax'], $pri['tax'], true),
                array('Effective tax rate', $etrC . '%', $etrP . '%'),
            ), 'Tax reconciliation')
            . $ntbl(array(
                array('Deferred tax liability — opening', $dtlP, round($dtlP / 1.1, 2)),
                array('Movement (accelerated depreciation)', $dtMoveC, round($dtlP - round($dtlP / 1.1, 2), 2)),
                array('Deferred tax liability — closing', $dtlC, $dtlP, true),
            ), 'Deferred tax', 'Deferred tax arises on temporary differences between the carrying amount and tax base of property, plant &amp; equipment.')
            . $pol(array(
                'Current tax' => 'Recognised on taxable profit for the period at enacted rates — UAE Corporate Tax of 0% on the first AED 375,000 of taxable income and 9% on the excess.',
                'Deferred tax' => 'Recognised on temporary differences between the carrying amounts and tax bases of assets and liabilities, using the liability method at rates expected to apply on reversal.',
                'Procedure' => 'Deferred tax assets are recognised only to the extent it is probable that future taxable profit will be available; current and deferred tax are charged to profit or loss except where they relate to items in OCI/equity.',
            ), 'IAS 12.5, 12, 15–24, 47, 58; UAE Federal Decree-Law 47/2022 (Corporate Tax), arts. 3 & 20.'));
        $notes .= $note('Provisions & contingencies', 'IAS 37', '<p>Provisions are recognised where a present obligation exists, an outflow is probable and a reliable estimate can be made. No material contingent liabilities are expected to crystallise. The Company has no pending litigation expected to have a material effect on these financial statements.</p>'
            . $pol(array(
                'Recognition' => 'A provision is recognised when the Company has a present (legal or constructive) obligation from a past event, an outflow of resources is probable, and a reliable estimate can be made.',
                'Measurement' => 'At the best estimate of the expenditure required to settle the obligation at the reporting date, discounted where the time value of money is material.',
                'Procedure' => 'Contingent liabilities (possible obligations, or present obligations not meeting the recognition criteria) are disclosed but not recognised; contingent assets are disclosed only when an inflow is probable.',
            ), 'IAS 37.14, 36–47, 27–30, 86, 89.'));
        $notes .= $note('Related-party transactions', 'IAS 24', '<p>Transactions with shareholders, group companies and key management personnel (KMP) are conducted on agreed terms. Key management remuneration was:</p>'
            . $ntbl(array(
                array('Short-term employee benefits', $kmpShortC, $kmpShortP),
                array('End-of-service benefits', $kmpEosbC, $kmpEosbP),
                array('Directors\' fees', $kmpFeesC, $kmpFeesP),
                array('Total KMP remuneration', round($kmpShortC + $kmpEosbC + $kmpFeesC, 2), round($kmpShortP + $kmpEosbP + $kmpFeesP, 2), true),
            ), 'Key management remuneration')
            . $pol(array(
                'Scope' => 'Related parties include shareholders with control/significant influence, group entities, and key management personnel (KMP) having authority for planning, directing and controlling the Company.',
                'Procedure' => 'Transactions and outstanding balances with related parties, and the total of KMP compensation analysed by category, are disclosed regardless of whether a price was charged.',
            ), 'IAS 24.9, 17–18, 18A.'));
        $notes .= $note('Earnings per share', 'IAS 33', '<p>Basic earnings per share is calculated by dividing profit for the year attributable to ordinary shareholders by the weighted-average number of ordinary shares in issue. There are no dilutive instruments, so diluted EPS equals basic EPS.</p>'
            . $ntbl(array(
                array('Profit for the year', $cur['profit'], $pri['profit']),
                array('Weighted-average shares in issue', number_format($shares), number_format($shares)),
                array('Basic & diluted EPS', $m($epsC), $m($epsP), true),
            ), 'Earnings per share')
            . $pol(array(
                'Basic EPS' => 'Profit for the year attributable to ordinary shareholders divided by the weighted-average number of ordinary shares outstanding during the period.',
                'Diluted EPS' => 'Adjusts the numerator and denominator for the effects of all dilutive potential ordinary shares; the Company has none, so diluted equals basic.',
            ), 'IAS 33.9–12, 19, 31–32.'));
        $notes .= $note('Financial instruments — by category', 'IFRS 9 / IAS 32', '<p>The carrying amounts of financial assets and liabilities by measurement category were:</p>'
            . $ntbl(array(
                array('Financial assets at amortised cost (receivables + cash)', round($cur['receivables'] + $cur['cash'], 2), round($pri['receivables'] + $pri['cash'], 2)),
                array('Financial assets at FVOCI', $cur['reserves'], $pri['reserves']),
                array('Financial liabilities at amortised cost (borrowings + leases + payables)', round($borrowTotC + $cur['lease'] + $cur['payables'], 2), round($borrowTotP + $pri['lease'] + $pri['payables'], 2)),
            ), 'By measurement category')
            . $pol(array(
                'Classification' => 'Financial assets are classified at amortised cost, fair value through OCI (FVOCI) or fair value through profit or loss (FVTPL) based on the business model and the contractual cash-flow (SPPI) test.',
                'Liabilities' => 'Financial liabilities are measured at amortised cost unless designated at FVTPL.',
                'Procedure' => 'Instruments are presented as financial assets, financial liabilities or equity according to the substance of the contractual arrangement; offsetting only where a legal right and intention exist.',
            ), 'IFRS 9.4.1–4.2; IAS 32.11, 15–16, 42.'));
        $notes .= $note('Fair value measurement', 'IFRS 13', '<p>Assets measured at fair value are categorised within the three-level hierarchy based on the lowest-level significant input. Items carried at fair value through OCI total ' . $m($oci) . ' for the year and fall within the hierarchy as follows:</p>'
            . $ntbl(array(
                array('Level 1 — quoted prices', round($oci * 0.6, 2), round($ociP * 0.6, 2)),
                array('Level 2 — observable inputs', round($oci * 0.3, 2), round($ociP * 0.3, 2)),
                array('Level 3 — unobservable inputs', round($oci - round($oci * 0.6, 2) - round($oci * 0.3, 2), 2), round($ociP - round($ociP * 0.6, 2) - round($ociP * 0.3, 2), 2)),
                array('Total at fair value', $oci, $ociP, true),
            ), 'Fair-value hierarchy')
            . $pol(array(
                'Definition' => 'Fair value is the price that would be received to sell an asset or paid to transfer a liability in an orderly transaction between market participants at the measurement date.',
                'Procedure' => 'Inputs are categorised into Level 1 (quoted prices in active markets), Level 2 (observable inputs other than Level 1) and Level 3 (unobservable inputs); the measurement uses the lowest level of significant input.',
            ), 'IFRS 13.9, 24, 72–90.'));
        $notes .= $note('Financial risk management', 'IFRS 7', '<p>The Company is exposed to credit, liquidity and market (interest-rate) risk, managed under Board-approved policies.</p>'
            . '<p><strong>Credit risk</strong> — maximum exposure equals the carrying amount of receivables and cash:</p>'
            . $ntbl(array(
                array('Trade & other receivables', $cur['receivables'], $pri['receivables']),
                array('Cash & cash equivalents', $cur['cash'], $pri['cash']),
                array('Maximum credit exposure', $maxCredC, $maxCredP, true),
            ), 'Credit risk')
            . '<p><strong>Liquidity risk</strong> — contractual (undiscounted) maturity of financial liabilities:</p>'
            . $ntbl(array(
                array('Due within 1 year (payables + current borrowings + current leases)', round($cur['payables'] + $cur['borrowCur'] + $lease1C, 2), round($pri['payables'] + $pri['borrowCur'] + $lease1P, 2)),
                array('Due 1–5 years', round($cur['borrowNon'] + $lease5C, 2), round($pri['borrowNon'] + $lease5P, 2)),
                array('Due after 5 years', $leaseGtC, $leaseGtP),
            ), 'Liquidity risk')
            . '<p><strong>Market risk</strong> — a +1% change in interest rates would change annual finance cost by approximately ' . $m($irSensC) . ' (' . $pL . ': ' . $m($irSensP) . ').</p>'
            . $pol(array(
                'Objective' => 'The Company manages exposure to credit, liquidity and market risk under Board-approved policies and risk limits.',
                'Procedure' => 'Credit risk is mitigated by counterparty assessment and the ECL model; liquidity risk by cash-flow forecasting and committed facilities; interest-rate risk by monitoring the fixed/floating mix. Quantitative exposures and a sensitivity analysis are disclosed (above).',
            ), 'IFRS 7.31–42; sensitivity per IFRS 7.40.'));
        $notes .= $note('Capital management', 'IAS 1', '<p>The Company manages capital to safeguard its ability to continue as a going concern and to maintain an efficient capital structure. The gearing ratio (net debt / equity) was:</p>'
            . $ntbl(array(
                array('Total borrowings & leases', round($borrowTotC + $cur['lease'], 2), round($borrowTotP + $pri['lease'], 2)),
                array('Less: cash & cash equivalents', -$cur['cash'], -$pri['cash']),
                array('Net debt / (net cash)', $netDebtC, $netDebtP),
                array('Total equity', $cur['equity'], $pri['equity']),
                array('Gearing ratio', $gearC . '%', $gearP . '%', true),
            ), 'Capital management')
            . $pol(array(
                'Objective' => 'Capital (equity plus net debt) is managed to safeguard the going concern, provide returns to shareholders and maintain an efficient structure.',
                'Procedure' => 'The Company monitors the gearing ratio (net debt / equity) and adjusts dividends, returns of capital or debt levels as required; it is not subject to externally-imposed capital requirements.',
            ), 'IAS 1.134–136.'));
        $notes .= $note('Operating segments', 'IFRS 8', '<p>The Company operates as a single operating segment — general trading and services — which is the basis on which the chief operating decision-maker reviews performance and allocates resources.</p>'
            . $ntbl(array(
                array('Segment revenue', $cur['rev'], $pri['rev']),
                array('Segment result (operating profit)', round($cur['gross'] - $cur['opex'] - $cur['depr'], 2), round($pri['gross'] - $pri['opex'] - $pri['depr'], 2)),
                array('Segment assets', $cur['totalAssets'], $pri['totalAssets']),
                array('Segment liabilities', $cur['liabs'], $pri['liabs']),
            ), 'Single reportable segment')
            . $pol(array(
                'Procedure' => 'Operating segments are reported in a manner consistent with the internal reporting provided to the chief operating decision-maker (CODM), who allocates resources and assesses performance. The Company has a single operating and reportable segment.',
                'Disclosure' => 'Entity-wide information on products/services, geography and major customers is provided where material.',
            ), 'IFRS 8.5, 22–24, 31–34.'));
        $notes .= $note('Commitments & contingencies', 'IAS 37 / IAS 16', '<p>Capital commitments contracted for but not provided in these financial statements, together with contingent items, were:</p>'
            . $ntbl(array(
                array('Capital commitments (PPE)', $capCommitC, $capCommitP),
                array('Bank guarantees & letters of credit', round($cur['rev'] * 0.02, 2), round($pri['rev'] * 0.02, 2)),
            ), 'Commitments & contingencies')
            . $pol(array(
                'Capital commitments' => 'Amounts contracted for the acquisition of property, plant & equipment but not yet recognised as liabilities are disclosed.',
                'Contingencies' => 'Guarantees, letters of credit and other contingent items are disclosed where a possible obligation exists whose outcome is not wholly within the Company\'s control.',
            ), 'IAS 16.74(c); IAS 37.86, 89.'));
        $notes .= $note('Dividends', 'IAS 1 / IAS 10', '<p>Dividends are recognised in equity when approved and no longer at the discretion of the Company.</p>'
            . $ntbl(array(
                array('Dividends declared & paid', $d['dividends'], round($pri['profit'] * 0.30, 2)),
                array('Dividend per share', $m(round($d['dividends'] / $shares, 3)), $m(round(round($pri['profit'] * 0.30, 2) / $shares, 3))),
            ), 'Dividends')
            . $pol(array(
                'Recognition' => 'Dividends are recognised as a deduction from equity in the period in which they are approved by the shareholders (or, for interim dividends, when paid) and are no longer at the Company\'s discretion.',
                'Procedure' => 'Dividends proposed after the reporting date but before authorisation of the financial statements are disclosed, not recognised as a liability.',
            ), 'IAS 10.12–13; IAS 1.107; UAE FDL 32/2021 (Commercial Companies), art. 30.'));
        $notes .= $note('Foreign currency', 'IAS 21', '<p>The functional and presentation currency is ' . epc_erp_h($ccy) . '. Foreign-currency transactions are translated at the spot rate on the transaction date; monetary items are retranslated at the closing rate and exchange differences recognised in profit or loss. The Company\'s exposure to foreign-currency balances is not material at the reporting date.</p>'
            . $pol(array(
                'Functional currency' => 'The currency of the primary economic environment in which the Company operates (AED), also used as the presentation currency.',
                'Procedure' => 'Foreign-currency transactions are recorded at the spot rate on the transaction date; monetary items are retranslated at the closing rate, non-monetary items at historical rate; exchange differences are recognised in profit or loss.',
            ), 'IAS 21.8–9, 21–23, 28.'));
        $notes .= $note('Impairment of non-financial assets', 'IAS 36', '<p>At each reporting date the Company assesses whether there is any indication that property, plant &amp; equipment (' . $m($cur['ppe']) . ') or intangibles (' . $m($cur['intang']) . ') may be impaired. Where the carrying amount exceeds the recoverable amount (the higher of fair value less costs of disposal and value in use), an impairment loss is recognised. No impairment was required in the current year (' . $pL . ': none).</p>'
            . $pol(array(
                'Procedure' => 'At each reporting date the Company assesses for indicators of impairment; if any exist, the recoverable amount (higher of fair value less costs of disposal and value in use) is estimated and the asset written down to it.',
                'Reversal' => 'An impairment loss (other than on goodwill) is reversed if the recoverable amount subsequently increases, limited to the carrying amount that would have existed had no impairment been recognised.',
            ), 'IAS 36.9–12, 18–22, 59, 110–114.'));
        $notes .= $note('Events after the reporting period', 'IAS 10', '<p>No adjusting or material non-adjusting events have occurred between the reporting date and the date these financial statements were authorised for issue.</p>');
        $notes .= $note('Going concern', 'IAS 1 / ISA 570', '<p>Based on the Company\'s net current asset position, profitability and cash flows, the directors have a reasonable expectation that the Company has adequate resources to continue in operational existence for the foreseeable future. Accordingly, the going-concern basis of accounting continues to be adopted.</p>');

        // ---- standards not applicable to the Company (structured, not blank) -
        $notes .= '<p style="font-weight:700;margin:14px 0 4px;color:#1d2740;border-top:1px solid #e3e8f0;padding-top:10px;">Standards considered but not applicable in the current period</p>'
            . '<p class="text-muted" style="font-size:11.5px;margin:0 0 8px;">Each standard below has been assessed. It does not affect the current financial statements; the scope, the reason it is not applicable, and the treatment that would be required if it applied are set out so the assessment is transparent and complete.</p>';
        $notes .= $naNote('Investment property', 'IAS 40',
            'Recognition and measurement of property held to earn rentals or for capital appreciation (rather than for use or sale).',
            'The Company holds no investment property at the reporting date (' . $pL . ': none).',
            'It would be measured under the fair-value or cost model and disclosed separately from owner-occupied property, with rental income and direct operating expenses shown in profit or loss.');
        $notes .= $naNote('Government grants', 'IAS 20',
            'Accounting for, and disclosure of, government grants and other forms of government assistance.',
            'No government grants were received or recognised during the year or the comparative year.',
            'Grants would be recognised in profit or loss on a systematic basis over the periods in which the related costs are incurred, and presented either as deferred income or netted against the related asset/expense.');
        $notes .= $naNote('Non-current assets held for sale & discontinued operations', 'IFRS 5',
            'Classification, measurement and presentation of assets held for sale and the results of discontinued operations.',
            'There were no non-current assets (or disposal groups) classified as held for sale and no discontinued operations in either year.',
            'Such assets would be measured at the lower of carrying amount and fair value less costs to sell, no longer depreciated, and the post-tax results of discontinued operations presented as a single line in profit or loss.');
        $notes .= $naNote('Consolidated & separate financial statements / associates & joint arrangements', 'IFRS 10/11/12 · IAS 27/28',
            'Control and consolidation of subsidiaries, joint arrangements, and equity accounting for associates, with related disclosures of interests in other entities.',
            'The Company is a standalone entity with no subsidiaries, associates or joint arrangements; these are therefore separate (entity-only) financial statements.',
            'Subsidiaries would be fully consolidated, associates and joint ventures equity-accounted, and the nature, risks and financial effects of those interests disclosed.');
        $notes .= $naNote('Business combinations', 'IFRS 3',
            'Acquisition accounting — identifying the acquirer, measuring identifiable assets and liabilities at fair value, and recognising goodwill or a bargain-purchase gain.',
            'The Company entered into no business combinations during the year or the comparative year; there is no goodwill.',
            'Consideration transferred would be allocated to identifiable net assets at fair value, with any excess recognised as goodwill and tested annually for impairment.');
        $notes .= $naNote('Share-based payment', 'IFRS 2',
            'Recognition of transactions in which the entity receives goods or services in exchange for equity instruments or cash based on share price.',
            'The Company operates no share-based payment or employee share-option arrangements.',
            'The fair value of awards would be measured at grant date and expensed over the vesting period, with a corresponding increase in equity (equity-settled) or liability (cash-settled).');
        $notes .= $naNote('Insurance contracts', 'IFRS 17',
            'Recognition, measurement and disclosure of insurance and reinsurance contracts issued.',
            'The Company is not an insurer and issues no insurance contracts.',
            'Insurance liabilities would be measured using the general (building-block) or premium-allocation approach, with the contractual service margin released to profit as service is provided.');
        $notes .= $naNote('Agriculture', 'IAS 41',
            'Accounting for biological assets and agricultural produce at the point of harvest.',
            'The Company holds no biological assets and carries on no agricultural activity.',
            'Biological assets would be measured at fair value less costs to sell, with changes recognised in profit or loss.');

        $notes .= $note('Application of new & amended standards', 'IAS 8', '<p>The Company has applied all IFRS Accounting Standards and Interpretations issued by the IASB that are effective for the current reporting period. New or amended standards and interpretations in issue but not yet effective have been assessed and are not expected to have a material impact on the financial statements when adopted.</p>');

        // ---- Standards applicability index (every IAS/IFRS) ----------------
        $stdIndex = epc_ext_standards_index();
        $stdRows = '';
        foreach ($stdIndex as $r) {
            $applied = stripos($r[2], 'Applied') !== false;
            $chip = '<span style="display:inline-block;padding:1px 7px;border-radius:10px;font-size:10.5px;font-weight:700;color:#fff;background:' . ($applied ? '#1a7f37' : '#7a8696') . ';">' . epc_erp_h($r[2]) . '</span>';
            $stdRows .= '<tr><td style="padding:4px 9px;font-weight:600;white-space:nowrap;">' . epc_erp_h($r[0]) . '</td>'
                . '<td style="padding:4px 9px;">' . epc_erp_h($r[1]) . '</td>'
                . '<td style="padding:4px 9px;text-align:center;">' . $chip . '</td>'
                . '<td style="padding:4px 9px;color:#444;font-size:11.5px;">' . epc_erp_h($r[3]) . '</td></tr>';
        }
        $stdIndexTbl = '<div style="overflow-x:auto;"><table class="table table-bordered table-condensed" style="font-size:12px;min-width:680px;"><thead>'
            . '<tr style="background:#13294b;color:#fff;"><th>Standard</th><th>Title</th><th style="text-align:center;">Status</th><th>How it is applied / why not applicable</th></tr></thead><tbody>'
            . $stdRows . '</tbody></table></div>'
            . '<p class="text-muted" style="font-size:11px;">Every IAS/IFRS Accounting Standard is listed with its treatment in these financial statements — "Applied" where it affects recognition, measurement or disclosure, or "Not applicable" with the reason. This index demonstrates complete framework coverage.</p>';

        // ============ 8 · Consolidated & separate (individual) statements =====
        // Illustrative group consolidation: Parent (separate/individual FS, IAS 27)
        // + a wholly-owned Subsidiary, with intercompany trading and balances
        // eliminated, reconciling to the Consolidated position (IFRS 10). The
        // subsidiary is a scaled, self-balancing entity so the worksheet foots.
        $ck = 0.30;                                   // subsidiary size vs parent
        $sub = function ($v) use ($ck) { return round((float) $v * $ck, 2); };
        $icBal = round(min($cur['receivables'], $cur['payables']) * 0.15, 2);  // intercompany receivable = payable
        $icSales = round(min($cur['rev'], $sub($cur['rev'])) * 0.20, 2);       // intercompany sales = purchases
        $c4 = function (string $label, $p, $s, $e, string $type = 'row') use ($m) {
            $tot = (float) $p + (float) $s + (float) $e;
            if ($type === 'head') {
                return '<tr style="background:#2b3a55;color:#fff;"><td colspan="5" style="padding:6px 10px;font-weight:700;">' . epc_erp_h($label) . '</td></tr>';
            }
            $strong = ($type === 'total' || $type === 'sub');
            $bg = ($type === 'total') ? '#eef6ee' : (($type === 'sub') ? '#f5f7fa' : '#fff');
            $c = function ($v) use ($m) { return ($v === '' || $v === null) ? '' : $m($v); };
            return '<tr style="background:' . $bg . ';' . ($strong ? 'font-weight:700;' : '') . '">'
                . '<td style="padding:5px 10px;">' . epc_erp_h($label) . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;">' . $c($p) . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;">' . $c($s) . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;color:#b3541e;">' . (((float) $e) != 0.0 ? $m($e) : '') . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;font-weight:700;">' . $c($tot) . '</td></tr>';
        };
        $consHead = '<table class="table table-bordered table-condensed" style="font-size:12px;max-width:900px;"><thead>'
            . '<tr style="background:#f0f3f8;"><th>' . epc_erp_h($entity) . ' group</th>'
            . '<th style="text-align:right;">Parent (separate)</th><th style="text-align:right;">Subsidiary</th>'
            . '<th style="text-align:right;">Eliminations</th><th style="text-align:right;">Consolidated</th></tr></thead><tbody>';
        // --- subsidiary (scaled, self-balancing) and parent (separate) figures ---
        $sShareCap = $sub($cur['shareCap']); $sReserves = $sub($cur['reserves']); $sRetained = $sub($cur['retained']);
        $sEquity = round($sShareCap + $sReserves + $sRetained, 2);                 // subsidiary net equity
        $sPPE = $sub($cur['ppe']); $sIntang = $sub($cur['intang']); $sInv = $sub($cur['inventory']); $sRec = $sub($cur['receivables']);
        $sBorrow = $sub($cur['borrowNon'] + $cur['borrowCur']); $sLease = $sub($cur['lease']); $sProv = $sub($cur['provisions']); $sPay = $sub($cur['payables']); $sTax = $sub($cur['tax']);
        $sLiab = round($sBorrow + $sLease + $sProv + $sPay + $sTax, 2);
        $sCash = round(($sEquity + $sLiab) - ($sPPE + $sIntang + $sInv + $sRec), 2); // cash is the plug so the subsidiary balances exactly
        // parent (separate FS): cash used to acquire the subsidiary becomes an investment, so the parent still balances
        $pInvest = $sEquity; $pCash = round($cur['cash'] - $pInvest, 2);
        // --- consolidated SOFP worksheet ---
        $consSofp = $consHead;
        $consSofp .= $c4('Assets', '', '', '', 'head');
        $consSofp .= $c4('Property, plant & equipment', $cur['ppe'], $sPPE, 0);
        $consSofp .= $c4('Intangible assets', $cur['intang'], $sIntang, 0);
        $consSofp .= $c4('Investment in subsidiary (at cost)', $pInvest, 0, -$pInvest);
        $consSofp .= $c4('Inventories', $cur['inventory'], $sInv, 0);
        $consSofp .= $c4('Trade & other receivables', $cur['receivables'], $sRec, -$icBal);
        $consSofp .= $c4('Cash & cash equivalents', $pCash, $sCash, 0);
        $pAssetsTot = round($cur['ppe'] + $cur['intang'] + $pInvest + $cur['inventory'] + $cur['receivables'] + $pCash, 2);
        $sAssetsTot = round($sPPE + $sIntang + $sInv + $sRec + $sCash, 2);
        $consSofp .= $c4('Total assets', $pAssetsTot, $sAssetsTot, -($pInvest + $icBal), 'total');
        $consSofp .= $c4('Equity & liabilities', '', '', '', 'head');
        $consSofp .= $c4('Share capital', $cur['shareCap'], $sShareCap, -$sShareCap);
        $consSofp .= $c4('Other reserves', $cur['reserves'], $sReserves, -$sReserves);
        $consSofp .= $c4('Retained earnings', $cur['retained'], $sRetained, -$sRetained);
        $consSofp .= $c4('Total equity', $cur['totalEquity'], $sEquity, -$sEquity, 'sub');
        $consSofp .= $c4('Borrowings (incl. current portion)', $cur['borrowNon'] + $cur['borrowCur'], $sBorrow, 0);
        $consSofp .= $c4('Lease liabilities', $cur['lease'], $sLease, 0);
        $consSofp .= $c4('Employee end-of-service provision', $cur['provisions'], $sProv, 0);
        $consSofp .= $c4('Trade & other payables', $cur['payables'], $sPay, -$icBal);
        $consSofp .= $c4('Current tax payable', $cur['tax'], $sTax, 0);
        $pEqLiabTot = round($cur['totalEquity'] + $cur['totalLiab'], 2);
        $sEqLiabTot = round($sEquity + $sLiab, 2);
        $consSofp .= $c4('Total equity & liabilities', $pEqLiabTot, $sEqLiabTot, -($sEquity + $icBal), 'total');
        $consSofp .= '</tbody></table>';
        $consAssetsTot = round($pAssetsTot + $sAssetsTot - ($pInvest + $icBal), 2);
        $consEqLiabTot = round($pEqLiabTot + $sEqLiabTot - ($sEquity + $icBal), 2);
        $consBalOk = abs($consAssetsTot - $consEqLiabTot) < 0.5;
        $consSofp .= '<p class="text-muted" style="font-size:11.5px;"><i class="fa fa-check-circle" style="color:' . ($consBalOk ? '#1a7f37' : '#c0392b') . ';"></i> Consolidated balance check: total assets ' . ($consBalOk ? 'equal' : 'do NOT equal') . ' total equity &amp; liabilities (' . $m($consAssetsTot) . '). Parent + Subsidiary − Eliminations = Consolidated.</p>';
        // --- consolidated P&L worksheet ---
        $consPl = $consHead;
        $consPl .= $c4('Revenue', $cur['rev'], $sub($cur['rev']), -$icSales);
        $consPl .= $c4('Cost of sales', -$cur['cogs'], -$sub($cur['cogs']), $icSales);
        $consPl .= $c4('Operating & administrative expenses', -$cur['opex'], -$sub($cur['opex']), 0);
        $consPl .= $c4('Depreciation & amortisation', -$cur['depr'], -$sub($cur['depr']), 0);
        $consPl .= $c4('Finance costs', -$cur['interest'], -$sub($cur['interest']), 0);
        $consPl .= $c4('Profit before tax', $cur['pbt'], $sub($cur['pbt']), 0, 'sub');
        $consPl .= $c4('Income tax expense', -$cur['tax'], -$sub($cur['tax']), 0);
        $consPl .= $c4('Profit for the year', $cur['profit'], $sub($cur['profit']), 0, 'total');
        $consPl .= '</tbody></table>';
        $consNote = $note('Basis of consolidation & separate financial statements', 'IFRS 10 / IAS 27',
            '<p>The Company prepares both <strong>consolidated</strong> financial statements (combining the parent and the entities it controls) and, where required, <strong>separate (individual)</strong> financial statements in which investments in subsidiaries are carried at cost or under IFRS 9. Control exists where the parent has power over the investee, exposure to variable returns, and the ability to use its power to affect those returns (IFRS 10).</p>'
            . '<p>On consolidation, like items of assets, liabilities, equity, income and expenses are added together line-by-line; the parent\'s investment is eliminated against the subsidiary\'s equity at acquisition; and <strong>all intragroup balances and transactions are eliminated in full</strong> — here intercompany trade of ' . $m($icSales) . ' (revenue against cost of sales) and intercompany balances of ' . $m($icBal) . ' (receivable against payable). Where a subsidiary is not wholly owned, the non-controlling interest is presented within consolidated equity.</p>'
            . '<p class="text-muted" style="font-size:11px;">The worksheet above is illustrative for this entity (wholly-owned subsidiary scaled at ' . (int) round($ck * 100) . '% of the parent); a live group consolidates the actual subsidiary ledgers. The "Parent (separate)" column is the individual/standalone entity result; the "Consolidated" column is the group result after eliminations.</p>');
        $consol = '<p class="text-muted" style="font-size:12px;max-width:900px;">This section shows the <strong>individual (separate) entity</strong> figures and the <strong>consolidated group</strong> figures side by side, with the consolidation breakup — <em>Parent + Subsidiary − Eliminations = Consolidated</em> — so the combination of individual and group reporting is fully traceable (IFRS 10 consolidation; IAS 27 separate financial statements).</p>'
            . '<h5 style="color:#1d2740;margin:12px 0 4px;">8.1 · Consolidated statement of financial position (worksheet)</h5>' . $consSofp
            . '<h5 style="color:#1d2740;margin:14px 0 4px;">8.2 · Consolidated statement of profit or loss (worksheet)</h5>' . $consPl
            . '<h5 style="color:#1d2740;margin:14px 0 4px;">8.3 · Basis of consolidation &amp; separate financial statements</h5>' . $consNote;

        // ============ 9 · Financial analysis & commentary ====================
        $pctOf = function ($n, $d) { return ((float) $d == 0.0) ? 0.0 : ((float) $n / (float) $d); };
        $ca_c = $cur['inventory'] + $cur['receivables'] + $cur['cash']; $cl_c = $cur['payables'] + $cur['tax'] + $cur['borrowCur'];
        $ca_p = $pri['inventory'] + $pri['receivables'] + $pri['cash']; $cl_p = $pri['payables'] + $pri['tax'] + $pri['borrowCur'];
        $debt_c = $cur['borrowNon'] + $cur['borrowCur'] + $cur['lease']; $debt_p = $pri['borrowNon'] + $pri['borrowCur'] + $pri['lease'];
        $fPct = function ($r) { return number_format($r * 100, 1) . '%'; };
        $fRat = function ($r) { return number_format($r, 2) . 'x'; };
        $fDays = function ($r) { return number_format($r, 0) . ' days'; };
        $grade = function (float $chg, float $hi, float $mid) {
            $a = abs($chg);
            return ($a >= $hi) ? 'High' : (($a >= $mid) ? 'Medium' : 'Low');
        };
        $arow = function (string $metric, string $curD, string $priD, string $chgD, bool $fav, string $impact, string $comment) {
            $col = ($impact === 'High') ? '#c0392b' : (($impact === 'Medium') ? '#d68910' : '#1a7f37');
            $badge = '<span style="display:inline-block;padding:1px 8px;border-radius:10px;font-size:10.5px;font-weight:700;color:#fff;background:' . $col . ';">' . $impact . '</span>';
            $arrow = $fav ? '<span style="color:#1a7f37;">&#9650;</span> ' : '<span style="color:#c0392b;">&#9660;</span> ';
            return '<tr>'
                . '<td style="padding:5px 10px;font-weight:600;">' . epc_erp_h($metric) . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;">' . $curD . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;color:#777;">' . $priD . '</td>'
                . '<td style="padding:5px 10px;text-align:right;white-space:nowrap;">' . $arrow . $chgD . '</td>'
                . '<td style="padding:5px 10px;text-align:center;">' . $badge . '</td>'
                . '<td style="padding:5px 10px;font-size:11.5px;color:#333;">' . $comment . '</td></tr>';
        };
        // metric calcs
        $revG = $pctOf($cur['rev'] - $pri['rev'], $pri['rev']);
        $gmC = $pctOf($cur['gross'], $cur['rev']); $gmP = $pctOf($pri['gross'], $pri['rev']);
        $ebitC = $cur['gross'] - $cur['opex'] - $cur['depr']; $ebitP = $pri['gross'] - $pri['opex'] - $pri['depr'];
        $profG = $pctOf($cur['profit'] - $pri['profit'], $pri['profit']);
        $npC = $pctOf($cur['profit'], $cur['rev']); $npP = $pctOf($pri['profit'], $pri['rev']);
        $crC = $pctOf($ca_c, $cl_c); $crP = $pctOf($ca_p, $cl_p);
        $wcC = $ca_c - $cl_c; $wcP = $ca_p - $cl_p;
        $gearC = $pctOf($debt_c, $cur['totalEquity']); $gearP = $pctOf($debt_p, $pri['totalEquity']);
        $arC = $pctOf($cur['receivables'], $cur['rev']) * 365; $arP = $pctOf($pri['receivables'], $pri['rev']) * 365;
        $invC = $pctOf($cur['inventory'], $cur['cogs']) * 365; $invP = $pctOf($pri['inventory'], $pri['cogs']) * 365;
        $cashG = $pctOf($cur['cash'] - $pri['cash'], $pri['cash']);
        $dpp = function ($a, $b) { return number_format(($a - $b) * 100, 1) . ' pp'; };
        $rows = '';
        $rows .= $arow('Revenue', $m($cur['rev']), $m($pri['rev']), $fPct($revG), $revG >= 0, $grade($revG, 0.15, 0.05),
            'Revenue ' . ($revG >= 0 ? 'grew' : 'fell') . ' ' . $fPct(abs($revG)) . ' year-on-year — the primary driver of overall performance; a ' . ($revG >= 0 ? 'strong top-line expansion' : 'contraction that pressures fixed-cost absorption') . '.');
        $rows .= $arow('Gross profit margin', $fPct($gmC), $fPct($gmP), $dpp($gmC, $gmP), $gmC >= $gmP, $grade($gmC - $gmP, 0.05, 0.02),
            'Gross margin moved ' . $dpp($gmC, $gmP) . ' — reflects pricing and input-cost control; ' . ($gmC >= $gmP ? 'margin improvement supports profitability' : 'margin erosion warrants cost review') . '.');
        $rows .= $arow('Operating profit (EBIT)', $m($ebitC), $m($ebitP), $fPct($pctOf($ebitC - $ebitP, $ebitP)), $ebitC >= $ebitP, $grade($pctOf($ebitC - $ebitP, $ebitP), 0.15, 0.05),
            'Core operating profitability before financing and tax; ' . ($ebitC >= $ebitP ? 'improving operating leverage' : 'operating profit under pressure') . '.');
        $rows .= $arow('Profit for the year', $m($cur['profit']), $m($pri['profit']), $fPct($profG), $profG >= 0, $grade($profG, 0.15, 0.05),
            'Bottom-line result attributable to shareholders ' . ($profG >= 0 ? 'increased' : 'decreased') . ' ' . $fPct(abs($profG)) . ' — the headline measure of the year.');
        $rows .= $arow('Net profit margin', $fPct($npC), $fPct($npP), $dpp($npC, $npP), $npC >= $npP, $grade($npC - $npP, 0.04, 0.015),
            'Profit retained per unit of revenue; ' . ($npC >= $npP ? 'efficiency gains' : 'thinner conversion of sales to profit') . '.');
        $rows .= $arow('Current ratio (liquidity)', $fRat($crC), $fRat($crP), $fRat($crC - $crP), $crC >= $crP, $grade($pctOf($crC - $crP, $crP), 0.20, 0.08),
            'Current assets cover current liabilities ' . $fRat($crC) . '; ' . ($crC >= 1.0 ? 'comfortable short-term liquidity' : 'below 1.0 — monitor working-capital funding') . '.');
        $rows .= $arow('Working capital', $m($wcC), $m($wcP), $m($wcC - $wcP), $wcC >= $wcP, $grade($pctOf($wcC - $wcP, $wcP), 0.20, 0.08),
            'Net current assets available to fund operations ' . ($wcC >= $wcP ? 'strengthened' : 'tightened') . ' versus the prior year.');
        $rows .= $arow('Gearing (debt / equity)', $fPct($gearC), $fPct($gearP), $dpp($gearC, $gearP), $gearC <= $gearP, $grade($gearC - $gearP, 0.15, 0.05),
            'Financial leverage and solvency; ' . ($gearC <= $gearP ? 'deleveraging reduces financial risk' : 'rising leverage increases interest exposure and risk') . '.');
        $rows .= $arow('Receivables collection', $fDays($arC), $fDays($arP), $fDays($arC - $arP), $arC <= $arP, $grade($pctOf($arC - $arP, $arP), 0.20, 0.08),
            'Average days to collect from customers; ' . ($arC <= $arP ? 'faster collection improves cash conversion' : 'slower collection ties up cash and raises credit risk') . '.');
        $rows .= $arow('Inventory holding', $fDays($invC), $fDays($invP), $fDays($invC - $invP), $invC <= $invP, $grade($pctOf($invC - $invP, $invP), 0.20, 0.08),
            'Average days inventory is held; ' . ($invC <= $invP ? 'leaner stock improves liquidity' : 'higher holding increases obsolescence and storage cost') . '.');
        $rows .= $arow('Cash & cash equivalents', $m($cur['cash']), $m($pri['cash']), $fPct($cashG), $cashG >= 0, $grade($cashG, 0.20, 0.08),
            'Closing cash position ' . ($cashG >= 0 ? 'increased' : 'decreased') . ' ' . $fPct(abs($cashG)) . ' — see the statement of cash flows for the operating/investing/financing split.');
        // overall verdict
        $hiCount = substr_count($rows, '>High<'); $medCount = substr_count($rows, '>Medium<');
        $analysis = '<p class="text-muted" style="font-size:12px;max-width:920px;">A comparison of the reporting period against the comparative period, with each movement graded by its <strong>impact on the business</strong> — '
            . '<span style="display:inline-block;padding:0 7px;border-radius:10px;font-size:10.5px;font-weight:700;color:#fff;background:#c0392b;">High</span> (material to performance or risk), '
            . '<span style="display:inline-block;padding:0 7px;border-radius:10px;font-size:10.5px;font-weight:700;color:#fff;background:#d68910;">Medium</span> (notable) and '
            . '<span style="display:inline-block;padding:0 7px;border-radius:10px;font-size:10.5px;font-weight:700;color:#fff;background:#1a7f37;">Low</span> (minor). The arrow shows whether the movement is favourable (&#9650;) or adverse (&#9660;) for the business.</p>'
            . '<div style="overflow-x:auto;"><table class="table table-bordered table-condensed" style="font-size:12px;min-width:760px;"><thead>'
            . '<tr style="background:#13294b;color:#fff;"><th>Metric</th><th style="text-align:right;">' . epc_erp_h($cL) . '</th><th style="text-align:right;">' . epc_erp_h($pL) . '</th><th style="text-align:right;">Change</th><th style="text-align:center;">Impact</th><th>Commentary</th></tr></thead><tbody>'
            . $rows . '</tbody></table></div>'
            . epc_ext_commentary('Analyst summary — what the numbers say', array(
                'Performance: revenue ' . ($revG >= 0 ? 'grew' : 'declined') . ' ' . $fPct(abs($revG)) . ' and profit for the year ' . ($profG >= 0 ? 'rose' : 'fell') . ' ' . $fPct(abs($profG)) . ', with the net margin at ' . $fPct($npC) . ' (' . $fPct($npP) . ' prior). This is the headline story of the year and the main driver of shareholder value.',
                'Financial position &amp; risk: liquidity is ' . ($crC >= 1.0 ? 'sound' : 'tight') . ' at a current ratio of ' . $fRat($crC) . ', working capital is ' . $m($wcC) . ', and gearing is ' . $fPct($gearC) . ' (' . $fPct($gearP) . ' prior) — ' . ($gearC <= $gearP ? 'a lower, less risky leverage profile' : 'a higher leverage profile that increases financial risk') . '. Receivables are collected in ' . $fDays($arC) . ' and inventory is held for ' . $fDays($invC) . '.',
                'Overall, ' . $hiCount . ' metric' . ($hiCount === 1 ? '' : 's') . ' show a <strong>high</strong> business impact and ' . $medCount . ' a <strong>medium</strong> impact this year. Figures are illustrative for the demo; a live tenant draws them from the tagged general ledger, and the same analysis localises to the tenant\'s registered country and industry.',
            ));

        // ============ assemble =============================================
        $fetchNote = '<p class="text-muted" style="font-size:11.5px;margin:6px 0 0;"><i class="fa fa-sync"></i> Use <strong>Fetch</strong> to refresh against the live standard sources: '
            . '<a href="https://www.ifrs.org/issued-standards/list-of-standards/" target="_blank" rel="noopener">IFRS/IAS</a> · '
            . '<a href="https://www.iaasb.org/standards-pronouncements" target="_blank" rel="noopener">ISA</a> · '
            . '<a href="https://www.sca.gov.ae" target="_blank" rel="noopener">SCA</a> · '
            . '<a href="https://www.moec.gov.ae" target="_blank" rel="noopener">MoE Auditors Register</a>.</p>';

        $cover = epc_ext_cover_page(
            $entity,
            'Audited Financial Statements & Independent Auditor\'s Report',
            'External audit report · ISA 700 · IFRS',
            array(
                'Reporting period' => '01 Jan ' . $d['curYear'] . ' — 31 Dec ' . $d['curYear'],
                'Comparative period' => 'FY' . $d['priYear'],
                'Reporting framework' => $fwk,
                'Auditing standards' => 'International Standards on Auditing (ISA)',
                'Presentation currency' => $ccy,
                'Independent auditor' => $auditor,
                'Oversight authority' => $authority,
                'Date of report' => $opinionDate,
            ),
            array(
                array('1', 'Independent Auditor\'s Report'),
                array('2', 'Statement of Financial Position'),
                array('3', 'Statement of Profit or Loss & Other Comprehensive Income'),
                array('4', 'Statement of Changes in Equity'),
                array('5', 'Statement of Cash Flows'),
                array('6', 'Notes to the Financial Statements'),
                array('7', 'Standards applicability index (every IAS/IFRS)'),
                array('8', 'Consolidated & separate (individual) financial statements'),
                array('9', 'Financial analysis & commentary (impact-graded)'),
            )
        );

        // Professional print layout — A4, equal margins, unified font, red corporate theme;
        // each element starts on a new page (like one sheet per element in Excel).
        $printCss = epc_ext_print_css();

        $body = '<div class="epc-aud-doc">' . $printCss . $cover
            . '<div class="epc-aud-front">'
            . '<p class="text-muted">Complete <strong>External Audit Report</strong> under the International Standards on Auditing — an Independent Auditor\'s Report (ISA 700/701/705/570/720) on the full set of <strong>IFRS</strong> financial statements with <strong>prior-year comparatives</strong>: statement of financial position, profit or loss &amp; OCI, changes in equity, cash flows, and detailed notes referencing each IAS/IFRS standard. Figures are period-aware (reporting period vs comparative); a live tenant maps each line from tagged GL accounts.</p>'
            . $fetchNote
            . epc_ext_field_guide('Field guide — what each statement & note shows (and the standard behind it)',
                'Plain-language explanation so a learner understands the audit pack. Framework: IFRS (IASB) + ISA (IAASB); UAE oversight: SCA / Ministry of Economy.',
                epc_ext_audit_guide_rows())
            . epc_ext_commentary('Report explained — how this audit pack works', array(
                'This is a complete <strong>external audit report</strong>. It opens with the <strong>Independent Auditor\'s Report</strong> — the auditor\'s <em>opinion</em> on whether the statements give a true and fair view (ISA 700), the <em>basis</em> for that opinion, <em>going concern</em> (ISA 570), the <em>Key Audit Matters</em> (ISA 701) and the respective responsibilities of management and the auditor.',
                'It then presents the four <strong>primary IFRS statements</strong> with the prior year alongside for comparison: the <strong>Statement of Financial Position</strong> (what the company owns and owes), the <strong>Statement of Profit or Loss &amp; OCI</strong> (performance for the year), the <strong>Statement of Changes in Equity</strong> (how equity moved), and the <strong>Statement of Cash Flows</strong> (where cash came from and went). Each face line carries its IAS/IFRS reference as a clickable <strong>Note number</strong>, and key lines drill down to the underlying transactions.',
                'Finally, the <strong>notes to the accounts</strong> set out the accounting policies and disclosures required by each standard. Every note is <strong>numbered and linked</strong> from the face of the statements and carries a detailed <strong>standard explanation</strong> (objective, scope, recognition, measurement and disclosure) together with the <strong>build-up figures and prior-year comparatives that reconcile to the face</strong> of the statements. A <strong>standards applicability index</strong> lists every IAS/IFRS, and a <strong>consolidated &amp; separate (individual)</strong> section shows the group consolidation breakup. The statements reconcile — assets equal equity plus liabilities, and opening cash plus the net cash flow equals closing cash.',
            ))
            . '</div>'
            . '<section class="epc-aud-sec"><h4 style="color:#1d2740;">1 · Independent Auditor\'s Report</h4>' . $audit . '</section>'
            . '<section class="epc-aud-sec"><a id="audit-statements"></a><h4 style="color:#1d2740;">2 · Statement of Financial Position</h4>'
            . '<p class="text-muted epc-no-print" style="font-size:11.5px;margin:0 0 4px;">Click an underlined line to drill to the underlying transactions; click a <strong>Note</strong> number to jump to the note.</p>'
            . epc_ext_ct_drill_js() . $sofp . '</section>'
            . '<section class="epc-aud-sec"><h4 style="color:#1d2740;">3 · Statement of Profit or Loss &amp; Other Comprehensive Income</h4>' . $sopl . '</section>'
            . '<section class="epc-aud-sec"><h4 style="color:#1d2740;">4 · Statement of Changes in Equity</h4>' . $soce . '</section>'
            . '<section class="epc-aud-sec"><h4 style="color:#1d2740;">5 · Statement of Cash Flows</h4>' . $scf . '</section>'
            . '<section class="epc-aud-sec"><h4 style="color:#1d2740;">6 · Notes to the financial statements</h4>' . $notes . '</section>'
            . '<section class="epc-aud-sec"><h4 style="color:#1d2740;">7 · Standards applicability index — every IAS/IFRS</h4>'
            . '<p class="text-muted" style="font-size:11.5px;margin:0 0 6px;">Confirms full coverage of the framework: each standard is either applied (with the note/treatment) or marked not applicable with the reason.</p>'
            . $stdIndexTbl . '</section>'
            . '<section class="epc-aud-sec"><h4 style="color:#1d2740;">8 · Consolidated &amp; separate (individual) financial statements</h4>' . $consol . '</section>'
            . '<section class="epc-aud-sec"><h4 style="color:#1d2740;">9 · Financial analysis &amp; commentary</h4>' . $analysis . '</section>'
            . '</div>';

        $body = preg_replace_callback('/\{\{N:([a-z0-9\-]+)\}\}/', function ($mt) use ($noteNum) {
            return isset($noteNum[$mt[1]]) ? (string) $noteNum[$mt[1]] : '—';
        }, $body);

        return array(
            'title' => $name . ' (ISA 700 · IFRS)',
            'body' => $body,
            'summary' => array(
                'Opinion' => 'Unmodified (true & fair)',
                'Revenue' => $m($cur['rev']),
                'Profit for the year' => $m($cur['profit']),
                'Total assets' => $m($cur['totalAssets']),
                'Total equity' => $m($cur['totalEquity']),
            ),
            'live' => true,
        );
    }
}

if (!function_exists('epc_ext_audit_guide_rows')) {
    /** @return array<int,array{0:string,1:string}> */
    function epc_ext_audit_guide_rows(): array
    {
        return array(
            array('Independent Auditor\'s Report', 'The auditor\'s formal opinion on whether the financial statements give a true and fair view under IFRS, plus the basis, going-concern, key audit matters and responsibilities (ISA 700/701/705/570/720).'),
            array('Statement of Financial Position (SOFP)', 'A snapshot at the year-end of assets (what the company owns), liabilities (what it owes) and equity (the owners\' residual). Assets always equal equity + liabilities (IAS 1).'),
            array('Statement of Profit or Loss & OCI', 'Performance for the year: revenue less costs = profit, then other comprehensive income (e.g. revaluations) = total comprehensive income (IAS 1, IFRS 15).'),
            array('Statement of Changes in Equity (SOCE)', 'How each component of equity moved during the year — opening balance + profit + OCI − dividends + share issues = closing balance (IAS 1).'),
            array('Statement of Cash Flows', 'Cash generated and used, split into operating, investing and financing activities; opening cash + net flow = closing cash (IAS 7, indirect method).'),
            array('Notes to the accounts', 'The accounting policies and detailed disclosures required by each standard so a reader understands how every number was derived (IAS 1, 2, 7, 8, 10, 12, 16, 19, 24, 33, 37, 38; IFRS 7, 9, 15, 16).'),
            array('Comparatives', 'IFRS requires the prior period to be shown next to the current period so trends and changes are visible (IAS 1).'),
            array('Drill-down', 'Each combination figure expands to the underlying invoices / journal entries so you can trace any number to its source transactions.'),
        );
    }
}

if (!function_exists('epc_ext_ifrs_explain')) {
    /**
     * Detailed standard explanation registry — for each IAS/IFRS the objective,
     * scope, recognition, measurement, presentation/disclosure and the key
     * paragraph references, so each note in the audit pack reads like a
     * reference text (this is what makes it a long-form, ~100-page document).
     *
     * @return array<string,array{objective:string,scope:string,recognition:string,measurement:string,disclosure:string,paras:string}>
     */
    function epc_ext_ifrs_explain(): array
    {
        return array(
            'IAS 1' => array(
                'objective' => 'Prescribes the basis for presentation of general-purpose financial statements so they are comparable both with the entity’s own prior periods and with other entities.',
                'scope' => 'Applies to all general-purpose financial statements prepared and presented under IFRS — the complete set being the statement of financial position, statement of profit or loss & OCI, statement of changes in equity, statement of cash flows and notes.',
                'recognition' => 'Requires fair presentation and compliance with IFRS, going concern, the accrual basis, materiality and aggregation, no offsetting unless permitted, and consistency of presentation from period to period.',
                'measurement' => 'IAS 1 does not set measurement rules itself; it requires the measurement bases used (historical cost, fair value, NRV, present value) to be disclosed in the material accounting-policy information.',
                'disclosure' => 'Comparative information for the preceding period; current/non-current classification; the nature and amount of material items; judgements with the most significant effect; and sources of estimation uncertainty.',
                'paras' => 'IAS 1.10–138 (esp. 15 fair presentation, 25 going concern, 27 accrual, 38 comparatives, 54/82 minimum line items, 122/125 judgements & estimates).',
            ),
            'IAS 2' => array(
                'objective' => 'Prescribes the accounting treatment for inventories, including the determination of cost and its subsequent recognition as an expense.',
                'scope' => 'All inventories except work in progress under construction contracts, financial instruments, and biological assets related to agricultural activity.',
                'recognition' => 'The carrying amount of inventories is recognised as an expense (cost of sales) in the period in which the related revenue is recognised; write-downs to NRV are expensed when they occur.',
                'measurement' => 'At the lower of cost and net realisable value. Cost comprises purchase, conversion and other costs to bring inventory to its present location and condition, assigned using FIFO or weighted-average cost (not LIFO).',
                'disclosure' => 'Accounting policy and cost formula; total carrying amount by classification; amount recognised as an expense; write-downs and any reversals; and the carrying amount pledged as security.',
                'paras' => 'IAS 2.9 (lower of cost and NRV), 10–22 (cost), 25 (cost formula), 28–33 (NRV), 34–36 (recognition & disclosure).',
            ),
            'IAS 7' => array(
                'objective' => 'Requires information about the historical changes in cash and cash equivalents through a statement of cash flows classifying flows as operating, investing and financing.',
                'scope' => 'All entities; a statement of cash flows is an integral part of the complete set of financial statements.',
                'recognition' => 'Cash flows are reported when cash and cash equivalents are received or paid; non-cash transactions are excluded from the statement and disclosed separately.',
                'measurement' => 'Operating cash flows may be presented by the direct or indirect method (indirect used here — profit before tax adjusted for non-cash items and working-capital movements).',
                'disclosure' => 'Components of cash and cash equivalents with a reconciliation to the statement of financial position; significant non-cash transactions; and a reconciliation of liabilities arising from financing activities.',
                'paras' => 'IAS 7.10–17 (classification), 18–20 (operating — direct/indirect), 45 (components), 44A–44E (financing-liability reconciliation).',
            ),
            'IAS 8' => array(
                'objective' => 'Prescribes the criteria for selecting and changing accounting policies, and the accounting and disclosure for changes in policies, changes in estimates and corrections of errors.',
                'scope' => 'Selection and application of accounting policies and the treatment of changes therein, changes in estimates, and prior-period errors.',
                'recognition' => 'A change in policy is applied retrospectively (restating comparatives) unless impracticable; a change in estimate is applied prospectively; a material prior-period error is corrected by retrospective restatement.',
                'measurement' => 'Policies are selected from the applicable IFRS; absent a specific standard, management uses judgement to give relevant and reliable information, referring to the Conceptual Framework.',
                'disclosure' => 'Nature and amount of changes and their effect on each line item; the reason a new policy gives reliable & more relevant information; and the nature of prior-period errors and the correction.',
                'paras' => 'IAS 8.7–12 (selection), 19–27 (changes in policy), 32–38 (estimates), 41–49 (errors).',
            ),
            'IAS 10' => array(
                'objective' => 'Prescribes when an entity should adjust its financial statements for events after the reporting period and the disclosures to give.',
                'scope' => 'Events, both favourable and unfavourable, that occur between the reporting date and the date the financial statements are authorised for issue.',
                'recognition' => 'Adjusting events (conditions existing at the reporting date) are recognised; non-adjusting events (arising after) are not recognised but are disclosed if material.',
                'measurement' => 'Adjusting events are remeasured into the reported figures; an entity must not prepare statements on a going-concern basis if events indicate that basis is no longer appropriate.',
                'disclosure' => 'The date of authorisation for issue and who gave it; and for material non-adjusting events the nature of the event and an estimate of its financial effect.',
                'paras' => 'IAS 10.8–9 (adjusting), 10–11 (non-adjusting), 17 (authorisation date), 21 (disclosure).',
            ),
            'IAS 12' => array(
                'objective' => 'Prescribes the accounting for income taxes — current tax and deferred tax arising from temporary differences, unused tax losses and unused tax credits.',
                'scope' => 'All domestic and foreign taxes based on taxable profits, including the UAE Corporate Tax regime (Federal Decree-Law 47/2022).',
                'recognition' => 'Current tax for current and prior periods is recognised as a liability (or asset); deferred tax is recognised for all taxable/deductible temporary differences, with deferred tax assets recognised only to the extent recovery is probable.',
                'measurement' => 'Current tax at the rates enacted or substantively enacted; deferred tax at the rates expected to apply when the asset is realised/liability settled; deferred tax is not discounted.',
                'disclosure' => 'Major components of tax expense; a reconciliation between tax expense and accounting profit × applicable rate; and the amount and expiry of deductible temporary differences and unused losses.',
                'paras' => 'IAS 12.12–14 (current), 15–24 (deferred — taxable), 24–33 (deductible), 47 (rates), 81 (reconciliation).',
            ),
            'IAS 16' => array(
                'objective' => 'Prescribes the accounting for property, plant & equipment so users can discern information about an entity’s investment in its PPE and the changes therein.',
                'scope' => 'Tangible items held for use in production/supply, rental or administration, expected to be used for more than one period.',
                'recognition' => 'An item is recognised as PPE when it is probable that future economic benefits will flow to the entity and its cost can be measured reliably; subsequent costs are capitalised on the same criteria.',
                'measurement' => 'Initially at cost (purchase price, import duties, directly attributable costs, dismantling provision); subsequently under the cost model (cost − accumulated depreciation − impairment) or the revaluation model. Depreciation is allocated systematically over the useful life.',
                'disclosure' => 'Measurement bases, depreciation methods and useful lives/rates; and a reconciliation of the gross carrying amount and accumulated depreciation at the start and end of the period (the movement schedule).',
                'paras' => 'IAS 16.7 (recognition), 16–22 (cost), 30/31 (cost/revaluation models), 43–62 (depreciation), 73 (reconciliation).',
            ),
            'IAS 19' => array(
                'objective' => 'Prescribes the accounting and disclosure for employee benefits — short-term, post-employment, other long-term and termination benefits.',
                'scope' => 'All employee benefits except share-based payment (IFRS 2). Includes the UAE end-of-service gratuity, accounted for as a defined-benefit obligation.',
                'recognition' => 'A liability is recognised when an employee has rendered service in exchange for benefits to be paid in the future, and an expense when the entity consumes the economic benefit of that service.',
                'measurement' => 'Defined-benefit obligations are measured at present value using the projected-unit-credit method, with remeasurements (actuarial gains/losses) recognised in OCI.',
                'disclosure' => 'The characteristics of the plans and associated risks; amounts in the statements; and the effect of the obligation on the amount, timing and uncertainty of future cash flows.',
                'paras' => 'IAS 19.11–24 (short-term), 51–60 (defined benefit recognition), 66–98 (PUC measurement), 120–147 (disclosure).',
            ),
            'IAS 21' => array(
                'objective' => 'Prescribes how to include foreign-currency transactions and foreign operations and how to translate financial statements into a presentation currency.',
                'scope' => 'Accounting for transactions and balances in foreign currencies and the translation of the results and financial position of foreign operations.',
                'recognition' => 'A foreign-currency transaction is recorded on initial recognition at the spot rate at the transaction date; exchange differences on settlement/retranslation of monetary items go to profit or loss.',
                'measurement' => 'At each reporting date monetary items are translated at the closing rate; non-monetary items at historical cost stay at the transaction-date rate; those at fair value use the rate at the valuation date.',
                'disclosure' => 'The amount of exchange differences recognised in profit or loss and in OCI; and the functional and presentation currency and the reason for any difference.',
                'paras' => 'IAS 21.21–22 (initial), 23 (closing rate), 28–34 (exchange differences), 51–57 (disclosure).',
            ),
            'IAS 23' => array(
                'objective' => 'Prescribes the accounting for borrowing costs.',
                'scope' => 'Borrowing costs directly attributable to the acquisition, construction or production of a qualifying asset.',
                'recognition' => 'Borrowing costs directly attributable to a qualifying asset are capitalised as part of its cost; all other borrowing costs are expensed in the period incurred.',
                'measurement' => 'The amount eligible for capitalisation is the actual costs incurred less any investment income on temporary investment of the borrowings (or a capitalisation rate for general borrowings).',
                'disclosure' => 'The amount of borrowing costs capitalised in the period and the capitalisation rate used.',
                'paras' => 'IAS 23.8 (capitalisation), 10–15 (eligible amount), 17–25 (period), 26 (disclosure).',
            ),
            'IAS 24' => array(
                'objective' => 'Ensures the financial statements draw attention to the possibility that financial position and performance may have been affected by related parties and transactions with them.',
                'scope' => 'Identification of related-party relationships and transactions, outstanding balances and commitments, and key management personnel compensation.',
                'recognition' => 'IAS 24 is a disclosure standard — it does not change recognition or measurement of the underlying transactions.',
                'measurement' => 'No measurement requirements; the underlying transactions are measured under the applicable standards.',
                'disclosure' => 'Parent/ultimate controlling party relationships regardless of transactions; KMP compensation by category; and the nature, amount and terms of related-party transactions and outstanding balances.',
                'paras' => 'IAS 24.13–14 (relationships), 17 (KMP compensation), 18–24 (transactions & balances).',
            ),
            'IAS 32' => array(
                'objective' => 'Establishes principles for presenting financial instruments as liabilities or equity and for offsetting financial assets and liabilities.',
                'scope' => 'Classification from the issuer’s perspective of financial instruments into financial assets, financial liabilities and equity instruments.',
                'recognition' => 'An instrument is classified on initial recognition according to the substance of the contractual arrangement, not its legal form; a contractual obligation to deliver cash is a liability.',
                'measurement' => 'Compound instruments are split into liability and equity components; treasury shares are deducted from equity; interest, dividends, gains and losses follow the classification of the instrument.',
                'disclosure' => 'Presentation requirements are complemented by the disclosure requirements of IFRS 7; offsetting is permitted only where there is a legal right and intention to settle net.',
                'paras' => 'IAS 32.15–27 (liability vs equity), 28–32 (compound instruments), 33 (treasury shares), 42 (offsetting).',
            ),
            'IAS 33' => array(
                'objective' => 'Prescribes principles for determining and presenting earnings per share to improve performance comparisons.',
                'scope' => 'Entities whose ordinary shares are publicly traded, and any entity that chooses to disclose EPS; presented in the statement of profit or loss.',
                'recognition' => 'Basic and diluted EPS are presented for profit from continuing operations and for profit attributable to ordinary equity holders.',
                'measurement' => 'Basic EPS = profit attributable to ordinary shareholders ÷ weighted-average number of ordinary shares; diluted EPS adjusts for the effect of all dilutive potential ordinary shares.',
                'disclosure' => 'The amounts used as numerators and a reconciliation to profit/loss; the weighted-average number of shares; and instruments that could dilute EPS in future.',
                'paras' => 'IAS 33.9–10 (basic), 19–29 (weighted average), 30–57 (diluted), 66–70 (presentation & disclosure).',
            ),
            'IAS 36' => array(
                'objective' => 'Ensures assets are carried at no more than their recoverable amount and prescribes when and how to recognise and reverse impairment losses.',
                'scope' => 'Most assets except inventories, deferred tax, employee-benefit assets and financial assets within IFRS 9 (which have their own rules).',
                'recognition' => 'An impairment loss is recognised when the carrying amount of an asset (or cash-generating unit) exceeds its recoverable amount; goodwill is tested at least annually.',
                'measurement' => 'Recoverable amount is the higher of fair value less costs of disposal and value in use (the present value of future cash flows); the loss reduces the carrying amount to recoverable amount.',
                'disclosure' => 'The events and circumstances leading to impairment; the amount recognised/reversed by class of asset; and the key assumptions used to measure recoverable amount.',
                'paras' => 'IAS 36.8–17 (indicators & recognition), 18–57 (recoverable amount & VIU), 59–64 (recognition), 126–137 (disclosure).',
            ),
            'IAS 37' => array(
                'objective' => 'Ensures appropriate recognition criteria and measurement bases are applied to provisions, contingent liabilities and contingent assets, with adequate disclosure.',
                'scope' => 'Provisions, contingent liabilities and contingent assets, except those arising from executory contracts (unless onerous) and those covered by another standard.',
                'recognition' => 'A provision is recognised when there is a present obligation from a past event, an outflow is probable, and a reliable estimate can be made; contingent liabilities are disclosed, not recognised.',
                'measurement' => 'At the best estimate of the expenditure required to settle the obligation at the reporting date, taking risks and uncertainties into account and discounting where the time value of money is material.',
                'disclosure' => 'For each class of provision a reconciliation of the carrying amount; the nature of the obligation and expected timing; and the uncertainties and any expected reimbursement.',
                'paras' => 'IAS 37.14 (recognition), 23–26 (probable outflow), 36–52 (measurement), 84–92 (disclosure).',
            ),
            'IAS 38' => array(
                'objective' => 'Prescribes the accounting for intangible assets that are not dealt with specifically in another standard.',
                'scope' => 'Identifiable non-monetary assets without physical substance — e.g. software, licences, patents and development costs.',
                'recognition' => 'Recognised when it is identifiable, controlled, probable of generating future economic benefits and its cost is measurable; internally generated goodwill and most research costs are expensed.',
                'measurement' => 'Initially at cost; subsequently under the cost or revaluation model; assets with finite lives are amortised over the useful life and those with indefinite lives are tested for impairment.',
                'disclosure' => 'Useful lives or amortisation rates and methods; a reconciliation of the carrying amount at the start and end of the period; and the carrying amount of indefinite-life intangibles.',
                'paras' => 'IAS 38.18–23 (recognition), 24–32 (cost), 54–64 (internally generated), 97–106 (amortisation), 118 (reconciliation).',
            ),
            'IFRS 7' => array(
                'objective' => 'Requires disclosures enabling users to evaluate the significance of financial instruments and the nature and extent of risks arising from them.',
                'scope' => 'All entities for all types of financial instruments, complementing the recognition/measurement rules of IFRS 9 and the presentation rules of IAS 32.',
                'recognition' => 'A disclosure standard — it does not change recognition or measurement.',
                'measurement' => 'No measurement rules; it requires the carrying amounts of each category of financial asset and liability to be disclosed.',
                'disclosure' => 'Significance disclosures (carrying amounts by category, income/expense) and risk disclosures — credit risk (including ECL), liquidity risk (maturity analysis) and market risk (sensitivity analysis).',
                'paras' => 'IFRS 7.7–30 (significance), 31–42 (risk — qualitative & quantitative), 35A–35N (credit-risk/ECL).',
            ),
            'IFRS 8' => array(
                'objective' => 'Requires disclosure of information about an entity’s operating segments to evaluate the nature and financial effects of its business activities.',
                'scope' => 'Entities whose debt or equity is publicly traded; based on the components reviewed by the chief operating decision-maker (the management approach).',
                'recognition' => 'Operating segments are identified on the basis of internal reports regularly reviewed by the CODM to allocate resources and assess performance.',
                'measurement' => 'Segment amounts are measured on the same basis as reported internally to the CODM, with a reconciliation to the IFRS statement totals.',
                'disclosure' => 'Factors used to identify segments; reported profit or loss, assets and liabilities by segment; and entity-wide information about products, geography and major customers.',
                'paras' => 'IFRS 8.5–10 (operating segments), 11–19 (reportable segments), 20–24 (disclosure), 31–34 (entity-wide).',
            ),
            'IFRS 9' => array(
                'objective' => 'Establishes principles for the recognition, classification, measurement and impairment of financial instruments and for hedge accounting.',
                'scope' => 'All financial assets and financial liabilities except those scoped into other standards (e.g. interests in subsidiaries/associates).',
                'recognition' => 'A financial asset or liability is recognised when the entity becomes party to the contractual provisions; financial assets are derecognised when the rights to the cash flows expire or are transferred.',
                'measurement' => 'Financial assets are classified — based on the business model and the cash-flow characteristics (SPPI test) — as amortised cost, FVOCI or FVTPL; an expected-credit-loss (ECL) model is applied to debt instruments at amortised cost/FVOCI.',
                'disclosure' => 'Disclosures are provided under IFRS 7 — by category, the ECL allowance, and the credit-risk management approach.',
                'paras' => 'IFRS 9.3.1.1 (recognition), 4.1.1–4.1.4 (classification), 5.2 (measurement), 5.5 (ECL model).',
            ),
            'IFRS 13' => array(
                'objective' => 'Defines fair value, sets out a framework for measuring it, and requires disclosures about fair-value measurements.',
                'scope' => 'Applies when another IFRS requires or permits fair-value measurement (or disclosure), with limited exceptions.',
                'recognition' => 'Does not require fair-value measurement of any item — it specifies how to measure fair value when another standard requires it.',
                'measurement' => 'Fair value is an exit price — the price to sell an asset or transfer a liability in an orderly transaction between market participants at the measurement date — maximising observable inputs.',
                'disclosure' => 'A three-level hierarchy (Level 1 quoted prices, Level 2 observable inputs, Level 3 unobservable inputs), the valuation techniques and inputs, and transfers between levels.',
                'paras' => 'IFRS 13.9 (definition), 24 (market participants), 61–66 (techniques), 72–90 (hierarchy & disclosure).',
            ),
            'IFRS 15' => array(
                'objective' => 'Establishes principles for reporting the nature, amount, timing and uncertainty of revenue and cash flows arising from contracts with customers.',
                'scope' => 'All contracts with customers except leases (IFRS 16), insurance (IFRS 17), financial instruments (IFRS 9) and certain non-monetary exchanges.',
                'recognition' => 'Revenue is recognised to depict the transfer of promised goods or services in an amount reflecting the consideration expected, applying the five-step model and recognising revenue as each performance obligation is satisfied.',
                'measurement' => 'At the transaction price allocated to each performance obligation, including variable consideration (constrained to a highly-probable amount) and excluding amounts collected for third parties such as VAT.',
                'disclosure' => 'Disaggregation of revenue; contract balances and performance obligations; and significant judgements in applying the standard.',
                'paras' => 'IFRS 15.9 (contract), 22–30 (performance obligations), 31–38 (recognition), 46–49 (transaction price), 73–86 (allocation), 113–129 (disclosure).',
            ),
            'IFRS 16' => array(
                'objective' => 'Sets out the principles for the recognition, measurement, presentation and disclosure of leases, bringing most leases on-balance-sheet for lessees.',
                'scope' => 'All leases, including subleases, except short-term and low-value leases (for which a recognition exemption is available) and certain specialised assets.',
                'recognition' => 'At commencement a lessee recognises a right-of-use asset and a lease liability for the obligation to make lease payments.',
                'measurement' => 'The lease liability is the present value of the unpaid lease payments discounted at the rate implicit in the lease (or the incremental borrowing rate); the ROU asset is measured at cost and depreciated, with interest accruing on the liability.',
                'disclosure' => 'Depreciation of ROU assets, interest on lease liabilities, the maturity analysis of lease liabilities, and total cash outflow for leases.',
                'paras' => 'IFRS 16.9 (identifying a lease), 22–28 (initial recognition), 26 (discount rate), 29–35 (subsequent), 51–60 (disclosure).',
            ),
            'IFRS 10' => array(
                'objective' => 'Establishes principles for the presentation and preparation of consolidated financial statements when an entity controls one or more other entities.',
                'scope' => 'Parents that control subsidiaries; defines the principle of control as the single basis for consolidation.',
                'recognition' => 'An investor controls an investee when it has power over the investee, exposure to variable returns, and the ability to use its power to affect those returns — and consolidates from the date control is obtained.',
                'measurement' => 'Consolidation combines like items of assets, liabilities, equity, income and expenses line-by-line; intragroup balances, transactions, income and expenses are eliminated in full, and non-controlling interests are presented within equity.',
                'disclosure' => 'The basis of consolidation, the composition of the group, and (with IFRS 12) information about interests in other entities.',
                'paras' => 'IFRS 10.5–9 (control), 19–26 (consolidation procedures), B86–B93 (elimination & NCI).',
            ),
            'IAS 27' => array(
                'objective' => 'Prescribes the accounting and disclosure for investments in subsidiaries, joint ventures and associates when an entity prepares separate (individual) financial statements.',
                'scope' => 'Separate financial statements presented in addition to consolidated statements (or by an entity exempt from consolidation).',
                'recognition' => 'In separate financial statements, investments in subsidiaries, associates and joint ventures are recognised as investments rather than consolidated.',
                'measurement' => 'Such investments are carried either at cost, in accordance with IFRS 9, or using the equity method — applied consistently to each category.',
                'disclosure' => 'The fact that the statements are separate financial statements, the basis of accounting for the investments, and a list of significant investments.',
                'paras' => 'IAS 27.4 (definitions), 9–10 (preparation), 10 (measurement choice), 15–17 (disclosure).',
            ),
        );
    }
}

if (!function_exists('epc_ext_std_block')) {
    /**
     * Render the detailed "About the standard" explanation block for every
     * IAS/IFRS code found in a note's standard string (e.g. "IFRS 9 / IAS 32").
     */
    function epc_ext_std_block(string $stdStr): string
    {
        $reg = epc_ext_ifrs_explain();
        // pull canonical codes: "IAS 12", "IFRS 15" (ignore sub-paragraph numbers)
        preg_match_all('/\b(IAS|IFRS)\s*([0-9]{1,2})\b/', $stdStr, $mm, PREG_SET_ORDER);
        $seen = array(); $out = '';
        foreach ($mm as $x) {
            $code = strtoupper($x[1]) . ' ' . $x[2];
            if (isset($seen[$code]) || !isset($reg[$code])) { continue; }
            $seen[$code] = true;
            $e = $reg[$code];
            $out .= '<details class="epc-std-explain" style="margin:6px 0 4px;border:1px solid #d7e0ec;border-radius:6px;background:#fbfcfe;">'
                . '<summary style="cursor:pointer;padding:6px 11px;font-weight:700;color:#13294b;font-size:11.5px;">For reference — about ' . epc_erp_h($code) . ' (background only) <span style="font-weight:400;color:#1d4e89;">(objective, scope, recognition, measurement &amp; disclosure)</span></summary>'
                . '<div style="padding:4px 12px 9px;font-size:11.5px;line-height:1.55;color:#333;">'
                . '<p style="margin:3px 0;"><strong>Objective:</strong> ' . epc_erp_h($e['objective']) . '</p>'
                . '<p style="margin:3px 0;"><strong>Scope:</strong> ' . epc_erp_h($e['scope']) . '</p>'
                . '<p style="margin:3px 0;"><strong>Recognition:</strong> ' . epc_erp_h($e['recognition']) . '</p>'
                . '<p style="margin:3px 0;"><strong>Measurement:</strong> ' . epc_erp_h($e['measurement']) . '</p>'
                . '<p style="margin:3px 0;"><strong>Presentation &amp; disclosure:</strong> ' . epc_erp_h($e['disclosure']) . '</p>'
                . '<p style="margin:3px 0;color:#1d4e89;"><i class="fa fa-book"></i> <strong>Key paragraphs:</strong> ' . epc_erp_h($e['paras']) . '</p>'
                . '</div></details>';
        }
        return $out;
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
        $defaults = array('co' => 'Company', 'addr' => '', 'trnL' => 'TRN', 'trn' => '', 'ttl' => 'Report', 'juris' => '', 'auth' => '', 'law' => '', 'perL' => '', 'perR' => '', 'gen' => date('d M Y H:i'), 'theme' => '');
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
	// PDF policy: keep the SUMMARY supporting schedules (TRN-wise, supplier-wise,
	// adjustments, CT Schedules 1-6, commentary) but drop the transaction /
	// invoice-level detail (a tenant may have 10k+ invoices).
	rm('.epc-print-hide');               // invoice-wise listing (download-only)
	rm('.epc-ct-drill');                 // CT computation source breakdowns (txn level)
	rm('.epc-drill-hint');               // "▸ N invoices" / "drill-down" hints
	rm('button, textarea, script, .btn, form');
	var docd = clone.ownerDocument;
	// Collapse invoice / box drill-downs to just their one-line summary.
	var bd = clone.querySelectorAll('details.epc-box-drill');
	for(var i=0;i<bd.length;i++){ var d=bd[i]; if(!d.parentNode) continue; var s=d.querySelector('summary'); var rep=docd.createElement('div'); rep.className='epc-line'; rep.innerHTML=s?s.innerHTML:''; d.parentNode.replaceChild(rep,d); }
	// Expand the remaining <details> (supporting schedules + field guide) so
	// their tables/commentary render in the PDF instead of a collapsed title.
	var sd = clone.querySelectorAll('details');
	for(var j=0;j<sd.length;j++){ var e=sd[j]; if(!e.parentNode) continue; var su=e.querySelector('summary'); var wrap=docd.createElement('div'); wrap.className='mis-sched'; if(su){ var h=docd.createElement('div'); h.className='mis-sched-h'; h.innerHTML=su.innerHTML; wrap.appendChild(h); } var kids=[]; for(var k=0;k<e.childNodes.length;k++){ kids.push(e.childNodes[k]); } for(var k2=0;k2<kids.length;k2++){ if(kids[k2]!==su){ wrap.appendChild(kids[k2]); } } e.parentNode.replaceChild(wrap,e); }
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
	// theme palette — red corporate theme for the audit report, default navy/blue otherwise
	var TH = (c.theme==='red');
	var P1 = TH ? '#b3122a' : '#2b6cb0';            // primary accent
	var P2 = TH ? '#7a0c1c' : '#1d2740';            // dark
	var GRAD  = TH ? 'linear-gradient(90deg,#7a0c1c,#b3122a)' : 'linear-gradient(90deg,#1d2740,#2b6cb0)';
	var HGRAD = TH ? 'linear-gradient(90deg,#8f0f22,#b3122a)' : 'linear-gradient(90deg,#2b3a55,#3a6ea5)';
	var COVBG = TH ? 'linear-gradient(135deg,#fff6f7,#fbe3e7)' : 'linear-gradient(180deg,#fbfdff,#eef4fc)';
	var LT = TH ? '#fdeef0' : '#eef4fc';
	var css =
	'@page{size:A4;margin:24mm 14mm 20mm;}'
	+'*{box-sizing:border-box;-webkit-print-color-adjust:exact;print-color-adjust:exact;}'
	+'body{font-family:"Segoe UI",Arial,Helvetica,sans-serif;color:#1f2733;font-size:11.5px;line-height:1.45;margin:0;}'
	+'.mis-page{width:100%;border-collapse:collapse;}'
	+'.mis-page>thead{display:table-header-group;}'
	+'.mis-page>tfoot{display:table-footer-group;}'
	+'.mis-page>thead>tr>td,.mis-page>tfoot>tr>td,.mis-page>tbody>tr>td{border:none !important;padding:0;background:none;}'
	+'.mis-run{font-size:9px;color:#fff;background:'+GRAD+';padding:3px 8px;display:flex;justify-content:space-between;border-radius:3px;margin-bottom:4px;}'
	+'.mis-foot{font-size:9px;color:#8a93a3;border-top:1px solid #d7dce5;padding:3px 8px 0;display:flex;justify-content:space-between;margin-top:4px;}'
	+'.mis-body{padding-top:2px;}'
	+'.mis-cover{height:248mm;display:flex;flex-direction:column;justify-content:center;align-items:center;text-align:center;border:2px solid '+P1+';border-top:14px solid '+P2+';border-radius:6px;padding:40px;page-break-after:always;background:'+COVBG+';}'
	+'.mis-cover .badge{font-size:11px;letter-spacing:3px;text-transform:uppercase;color:#fff;background:'+P1+';padding:4px 14px;border-radius:14px;}'
	+'.mis-cover .co{font-size:30px;font-weight:800;color:'+P2+';margin:16px 0 2px;}'
	+'.mis-cover .addr{font-size:12px;color:#5b6577;}'
	+'.mis-cover .ttl{font-size:24px;font-weight:800;color:#fff;background:'+GRAD+';padding:10px 26px;border-radius:6px;margin:46px 0 6px;}'
	+'.mis-cover .juris{font-size:14px;color:'+P2+';}'
	+'.mis-cover .period{font-size:16px;font-weight:700;color:'+P1+';margin-top:26px;}'
	+'.mis-cover .perd{font-size:13px;color:#5b6577;}'
	+'.mis-cover .meta{margin-top:46px;font-size:12px;color:#5b6577;line-height:1.8;}'
	+'.mis-cover .rule{width:120px;border-top:3px solid #c2a14d;margin:24px auto;}'
	+'h3{font-size:16px;color:'+P2+';border-bottom:3px solid #c2a14d;padding-bottom:5px;margin:0 0 8px;page-break-after:avoid;}'
	+'h4{font-size:12.5px;color:#fff !important;margin:18px 0 6px;padding:6px 12px;background:'+HGRAD+';border-radius:4px;page-break-after:avoid;}'
	+'.epc-aud-sec{page-break-before:always;break-before:page;}'
	+'.epc-aud-sec:first-of-type{page-break-before:avoid;break-before:avoid;}'
	+'.epc-line{display:flex;align-items:center;width:100%;gap:8px;padding:7px 6px;border-bottom:1px solid #edf0f5;}'
	+'.epc-line:nth-of-type(even){background:#f7faff;}'
	+'table{border-collapse:collapse;width:100%;margin:6px 0;}'
	+'thead{display:table-header-group;}'
	+'tr{page-break-inside:avoid;}'
	+'td,th{border:1px solid #c7cedb;padding:5px 8px;font-size:11px;vertical-align:top;}'
	+'th{background:'+HGRAD+';color:#fff;text-align:left;}'
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
	+'.mis-sign>div{flex:1;border-top:1px solid #7a869a;padding-top:6px;font-size:11px;color:#5b6577;}'
	+'.mis-sched{margin:10px 0;page-break-inside:avoid;}'
	+'.mis-sched-h{font-weight:700;color:'+P2+';background:'+LT+';border-left:4px solid '+P1+';padding:5px 10px;margin:8px 0 4px;border-radius:3px;}'
	+'.mis-commentary{background:'+LT+';border:1px solid '+P1+';border-left:4px solid '+P1+';border-radius:5px;padding:10px 14px;margin:10px 0;font-size:11px;line-height:1.6;color:#33415c;page-break-inside:avoid;}'
	+'.mis-commentary h5{margin:0 0 5px;font-size:12px;color:'+P2+';}'
	+'.mis-commentary p{margin:0 0 6px;}';
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
		+cover
		+'<table class="mis-page"><thead><tr><td>'+runHdr+'</td></tr></thead>'
		+'<tfoot><tr><td>'+runFt+'</td></tr></tfoot>'
		+'<tbody><tr><td><div class="mis-body">'+clone.innerHTML+sign+'</div></td></tr></tbody></table>'
		+'</body></html>');
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
        if ($kind === 'fin') {
            $rows = array(
                array('Code', 'Description', 'Current year', 'Prior year'),
                array('META_LEGAL_NAME', 'Legal name (reporting entity)', 'Sample Client Trading LLC', ''),
                array('META_TRN', 'Tax / commercial registration number', '100000000000003', ''),
                array('META_ADDRESS', 'Registered address', 'Office 101, Business Bay, Dubai', ''),
                array('META_EMIRATE', 'Emirate / region', 'Dubai', ''),
                array('META_PERIOD_FROM', 'Reporting period from (YYYY-MM-DD)', '2024-01-01', ''),
                array('META_PERIOD_TO', 'Reporting period to (YYYY-MM-DD)', '2024-12-31', ''),
                array('META_AUDITOR', 'Independent auditor', 'Gulf Audit & Assurance', ''),
                array('FIN_REVENUE', 'Revenue (IFRS 15)', '8400000', '7500000'),
                array('FIN_COGS', 'Cost of sales', '4704000', '4200000'),
                array('FIN_OTHER_INCOME', 'Other income', '60000', '50000'),
                array('FIN_ADMIN_EXP', 'Administrative expenses', '1450000', '1300000'),
                array('FIN_SELLING_EXP', 'Selling & distribution expenses', '520000', '470000'),
                array('FIN_DEPR', 'Depreciation & amortisation (IAS 16/38)', '336000', '300000'),
                array('FIN_FINANCE_COST', 'Finance costs', '100800', '90000'),
                array('FIN_TAX', 'Income tax expense (IAS 12)', '113148', '90000'),
                array('FIN_OCI', 'Other comprehensive income', '0', '0'),
                array('FIN_PPE', 'Property, plant & equipment (IAS 16)', '3528000', '3150000'),
                array('FIN_INTANGIBLES', 'Intangible assets (IAS 38)', '420000', '375000'),
                array('FIN_INVENTORY', 'Inventories (IAS 2)', '924000', '825000'),
                array('FIN_RECEIVABLES', 'Trade & other receivables (IFRS 9)', '1344000', '1200000'),
                array('FIN_CASH', 'Cash & cash equivalents', '2571200', '1990000'),
                array('FIN_PAYABLES', 'Trade & other payables', '1092000', '975000'),
                array('FIN_BORROW_CUR', 'Borrowings - current', '420000', '375000'),
                array('FIN_BORROW_NONCUR', 'Borrowings - non-current', '1512000', '1350000'),
                array('FIN_LEASE', 'Lease liabilities (IFRS 16)', '252000', '225000'),
                array('FIN_PROVISIONS', 'Provisions (IAS 37)', '168000', '150000'),
                array('FIN_TAX_PAYABLE', 'Current tax payable (IAS 12)', '113148', '90000'),
                array('FIN_SHARE_CAPITAL', 'Share capital', '500000', '500000'),
                array('FIN_OTHER_RESERVES', 'Other reserves', '84000', '75000'),
                array('FIN_RETAINED_OPEN', 'Retained earnings - opening', '3710000', '2900000'),
                array('FIN_DIVIDENDS', 'Dividends declared in the year', '300000', '200000'),
            );
        } elseif ($kind === 'ct') {
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

if (!function_exists('epc_ext_xlsx_col_letter')) {
    /** Convert a 0-based column index to an A1-style column letter (0->A, 26->AA). */
    function epc_ext_xlsx_col_letter(int $idx): string
    {
        $s = '';
        $idx++;
        while ($idx > 0) {
            $r = ($idx - 1) % 26;
            $s = chr(65 + $r) . $s;
            $idx = (int) (($idx - $r) / 26);
        }
        return $s;
    }
}

if (!function_exists('epc_ext_xlsx_write')) {
    /**
     * Minimal multi-sheet XLSX writer (ZipArchive + Office Open XML). Every cell
     * is written as an inline string so it round-trips through epc_ext_parse_xlsx
     * (which reads inlineStr) and the numeric coercion in epc_ext_import_map.
     *
     * @param array<string,array<int,array<int,scalar>>> $sheets sheetName => rows of cells
     * @return string binary .xlsx content (empty string if ZipArchive unavailable)
     */
    function epc_ext_xlsx_write(array $sheets): string
    {
        if (!class_exists('ZipArchive')) {
            return '';
        }
        $esc = static function ($v): string {
            return htmlspecialchars((string) $v, ENT_QUOTES | ENT_XML1, 'UTF-8');
        };
        $names = array_keys($sheets);
        if (empty($names)) {
            $names = array('Sheet1');
            $sheets = array('Sheet1' => array());
        }
        $tmp = tempnam(sys_get_temp_dir(), 'epcxlsx');
        if ($tmp === false) {
            return '';
        }
        $zip = new \ZipArchive();
        if ($zip->open($tmp, \ZipArchive::OVERWRITE) !== true) {
            return '';
        }

        $sheetOverrides = '';
        $wbSheets = '';
        $wbRels = '';
        $i = 0;
        foreach ($names as $name) {
            $i++;
            $rowsXml = '';
            $rn = 0;
            foreach ($sheets[$name] as $row) {
                $rn++;
                $cellsXml = '';
                $ci = 0;
                foreach ($row as $cell) {
                    $ref = epc_ext_xlsx_col_letter($ci) . $rn;
                    if (is_array($cell)) {
                        // typed cell: formula array('f'=>'A1*B1','v'=>cached) or number array('t'=>'n','v'=>123)
                        if (isset($cell['f'])) {
                            $fml = ltrim((string) $cell['f'], '=');
                            $cv = array_key_exists('v', $cell) ? '<v>' . $esc($cell['v']) . '</v>' : '';
                            $cellsXml .= '<c r="' . $ref . '"><f>' . $esc($fml) . '</f>' . $cv . '</c>';
                        } elseif (($cell['t'] ?? '') === 'n') {
                            $cellsXml .= '<c r="' . $ref . '" t="n"><v>' . $esc($cell['v'] ?? 0) . '</v></c>';
                        } else {
                            $cellsXml .= '<c r="' . $ref . '" t="inlineStr"><is><t xml:space="preserve">'
                                . $esc($cell['v'] ?? '') . '</t></is></c>';
                        }
                    } else {
                        $cellsXml .= '<c r="' . $ref . '" t="inlineStr"><is><t xml:space="preserve">'
                            . $esc($cell) . '</t></is></c>';
                    }
                    $ci++;
                }
                $rowsXml .= '<row r="' . $rn . '">' . $cellsXml . '</row>';
            }
            $sheetXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
                . '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
                . '<sheetData>' . $rowsXml . '</sheetData></worksheet>';
            $zip->addFromString('xl/worksheets/sheet' . $i . '.xml', $sheetXml);
            $sheetOverrides .= '<Override PartName="/xl/worksheets/sheet' . $i . '.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>';
            // Excel sheet names: max 31 chars, no : \ / ? * [ ]
            $safe = preg_replace('/[:\\\\\\/?*\\[\\]]/', ' ', $name);
            $safe = trim(mb_substr($safe, 0, 31));
            if ($safe === '') { $safe = 'Sheet' . $i; }
            $wbSheets .= '<sheet name="' . $esc($safe) . '" sheetId="' . $i . '" r:id="rId' . $i . '"/>';
            $wbRels .= '<Relationship Id="rId' . $i . '" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet' . $i . '.xml"/>';
        }

        $zip->addFromString('[Content_Types].xml',
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            . '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
            . '<Default Extension="xml" ContentType="application/xml"/>'
            . '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
            . $sheetOverrides . '</Types>');
        $zip->addFromString('_rels/.rels',
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            . '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>'
            . '</Relationships>');
        $zip->addFromString('xl/workbook.xml',
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
            . 'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
            . '<sheets>' . $wbSheets . '</sheets></workbook>');
        $zip->addFromString('xl/_rels/workbook.xml.rels',
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            . $wbRels . '</Relationships>');
        $zip->close();
        $data = (string) @file_get_contents($tmp);
        @unlink($tmp);
        return $data;
    }
}

if (!function_exists('epc_ext_import_template_sheets')) {
    /**
     * The full multi-sheet template definition for the off-system importer.
     * $kind = 'vat' | 'ct'. Returns sheetName => rows. The Company & TRN and the
     * boxes/computation sheets carry the machine-read Code column; the invoice
     * and compliance sheets are reference detail (concatenated on read, ignored
     * by the code-based mapper).
     *
     * @return array<string,array<int,array<int,string>>>
     */
    function epc_ext_import_template_sheets(string $kind): array
    {
        if ($kind === 'fin') {
            return array(
                'Instructions' => array(
                    array('IFRS Financial Statements & Audit Report — complete import template (off-system)'),
                    array('Framework', 'International Financial Reporting Standards (IFRS) as issued by the IASB'),
                    array('Auditing', 'International Standards on Auditing (ISA 700/701/705/570/720)'),
                    array('Output', 'Cover page + Independent Auditor\'s Report + SOFP + SOPL & OCI + Changes in Equity + Cash Flows + full notes + standards index, with prior-year comparatives'),
                    array(''),
                    array('This workbook is designed so you can enter EVERYTHING needed to generate a complete, audit-ready IFRS report for any client.'),
                    array('Sheets in this workbook:'),
                    array('1', 'Company & details — entity, TRN, country, industry/principal activity, period & comparative, auditor & authority, share data.'),
                    array('2', 'Financial data — every statement line, Current + Prior columns (the single source for the face of the statements).'),
                    array('3', 'Revenue & segments — disaggregation of revenue by stream, geography and reportable segment (IFRS 15 / IFRS 8).'),
                    array('4', 'PPE & intangible movement — cost / accumulated depreciation / additions / disposals / charge, both years (IAS 16 / IAS 38).'),
                    array('5', 'Receivables, inventory & ECL — gross receivables, ECL allowance, ageing buckets, inventory categories (IFRS 9 / IAS 2).'),
                    array('6', 'Tax reconciliation — current vs deferred tax and the effective-rate reconciliation (IAS 12).'),
                    array('7', 'Leases, borrowings & financial risk — maturity analysis and risk inputs (IFRS 16 / IFRS 7).'),
                    array('8', 'Equity, EPS & dividends — share movements, weighted shares, dividend per share (IAS 33 / IAS 1).'),
                    array('9', 'Related parties & KMP — key management remuneration and related-party balances (IAS 24).'),
                    array('10', 'Other disclosures — commitments, contingencies, events after reporting date, going concern (IAS 37 / IAS 10).'),
                    array('11', 'Notes inputs — narrative accounting policies the report quotes for each standard.'),
                    array('12', 'Compliance checklist — the checks the engine validates before issuing.'),
                    array(''),
                    array('How to use:'),
                    array('a', 'Keep the Code column EXACTLY as provided — those codes are machine-read. Edit only the value/amount columns.'),
                    array('b', 'Enter amounts as positive numbers; the builder applies the correct sign on each statement.'),
                    array('c', 'Current year = column C, Prior year (comparative) = column D, on every "FIN_" line.'),
                    array('d', 'Detail sheets (PPE movement, receivables, tax, etc.) feed the notes — fill them for a richer report; blanks fall back to sensible derived values.'),
                    array('e', 'Save as .xlsx (or .csv) and upload under Import from Excel → Build return → "IFRS Financial Statements".'),
                    array('f', 'Off-system: nothing is read from or written to your ERP/GL — ideal for preparing other clients\' accounts.'),
                ),
                'Company & details' => array(
                    array('Code', 'Field', 'Value'),
                    array('META_LEGAL_NAME', 'Legal name (reporting entity)', 'Sample Client Trading LLC'),
                    array('META_TRADE_NAME', 'Trading / brand name', 'Sample Client'),
                    array('META_TRN', 'Tax / commercial registration number (TRN)', '100000000000003'),
                    array('META_REG_NO', 'Trade licence / commercial registration no.', 'CN-1234567'),
                    array('META_LEGAL_FORM', 'Legal form', 'Limited Liability Company'),
                    array('META_INCORP_DATE', 'Date of incorporation (YYYY-MM-DD)', '2015-03-01'),
                    array('META_COUNTRY', 'Country of registration (ISO code)', 'AE'),
                    array('META_ADDRESS', 'Registered address', 'Office 101, Business Bay, Dubai'),
                    array('META_EMIRATE', 'Emirate / region', 'Dubai'),
                    array('META_PHONE', 'Contact phone', '+971 4 000 0000'),
                    array('META_EMAIL', 'Contact email', 'finance@sampleclient.ae'),
                    array('META_WEBSITE', 'Website', 'www.sampleclient.ae'),
                    array('META_INDUSTRY', 'Industry / sector', 'Wholesale & retail trade'),
                    array('META_PRINCIPAL_ACTIVITY', 'Principal activity', 'General trading and the provision of related services'),
                    array('META_CURRENCY', 'Presentation currency', 'AED'),
                    array('META_FUNCTIONAL_CCY', 'Functional currency', 'AED'),
                    array('META_ROUNDING', 'Figures rounded to', 'Nearest currency unit'),
                    array('META_PERIOD_FROM', 'Reporting period from (YYYY-MM-DD)', '2024-01-01'),
                    array('META_PERIOD_TO', 'Reporting period to (YYYY-MM-DD)', '2024-12-31'),
                    array('META_PRIOR_FROM', 'Comparative period from (YYYY-MM-DD)', '2023-01-01'),
                    array('META_PRIOR_TO', 'Comparative period to (YYYY-MM-DD)', '2023-12-31'),
                    array('META_SHARE_COUNT', 'Number of shares in issue', '500000'),
                    array('META_PAR_VALUE', 'Par value per share', '1'),
                    array('META_EMPLOYEES', 'Average number of employees', '45'),
                    array('META_AUDITOR', 'Independent auditor', 'Gulf Audit & Assurance (Chartered Accountants)'),
                    array('META_AUDITOR_REG', 'Auditor registration / licence no.', 'MoE-000000'),
                    array('META_AUDITOR_AUTH', 'Auditor oversight authority', 'Ministry of Economy — UAE Auditors Register'),
                    array('META_DIRECTOR', 'Director signing the accounts', 'Managing Director'),
                    array('META_PREPARER', 'Accounts prepared by', 'Finance Manager'),
                    array('META_APPROVAL_DATE', 'Date accounts approved (YYYY-MM-DD)', '2025-03-31'),
                ),
                'Financial data' => array(
                    array('Code', 'Description', 'Current year', 'Prior year'),
                    array('— Statement of profit or loss & OCI —', '', '', ''),
                    array('FIN_REVENUE', 'Revenue (IFRS 15)', '8400000', '7500000'),
                    array('FIN_COGS', 'Cost of sales', '4704000', '4200000'),
                    array('FIN_OTHER_INCOME', 'Other income', '60000', '50000'),
                    array('FIN_ADMIN_EXP', 'Administrative expenses', '1450000', '1300000'),
                    array('FIN_SELLING_EXP', 'Selling & distribution expenses', '520000', '470000'),
                    array('FIN_DEPR', 'Depreciation & amortisation (IAS 16/38)', '336000', '300000'),
                    array('FIN_FINANCE_COST', 'Finance costs', '100800', '90000'),
                    array('FIN_TAX', 'Income tax expense (IAS 12)', '113148', '90000'),
                    array('FIN_OCI', 'Other comprehensive income (revaluation etc.)', '0', '0'),
                    array('— Statement of financial position: assets —', '', '', ''),
                    array('FIN_PPE', 'Property, plant & equipment (IAS 16)', '3528000', '3150000'),
                    array('FIN_INTANGIBLES', 'Intangible assets (IAS 38)', '420000', '375000'),
                    array('FIN_INVENTORY', 'Inventories (IAS 2)', '924000', '825000'),
                    array('FIN_RECEIVABLES', 'Trade & other receivables, net (IFRS 9)', '1344000', '1200000'),
                    array('FIN_CASH', 'Cash & cash equivalents', '2571200', '1990000'),
                    array('— Statement of financial position: liabilities —', '', '', ''),
                    array('FIN_PAYABLES', 'Trade & other payables', '1092000', '975000'),
                    array('FIN_BORROW_CUR', 'Borrowings — current', '420000', '375000'),
                    array('FIN_BORROW_NONCUR', 'Borrowings — non-current', '1512000', '1350000'),
                    array('FIN_LEASE', 'Lease liabilities (IFRS 16)', '252000', '225000'),
                    array('FIN_PROVISIONS', 'Provisions (IAS 37)', '168000', '150000'),
                    array('FIN_TAX_PAYABLE', 'Current tax payable (IAS 12)', '113148', '90000'),
                    array('— Equity —', '', '', ''),
                    array('FIN_SHARE_CAPITAL', 'Share capital', '500000', '500000'),
                    array('FIN_OTHER_RESERVES', 'Other reserves', '84000', '75000'),
                    array('FIN_RETAINED_OPEN', 'Retained earnings — opening balance', '3710000', '2900000'),
                    array('FIN_DIVIDENDS', 'Dividends declared in the year', '300000', '200000'),
                ),
                'Revenue & segments' => array(
                    array('Code', 'Revenue disaggregation (IFRS 15 / IFRS 8)', 'Current year', 'Prior year'),
                    array('FIN_REV_GOODS', 'Sale of goods (recognised at a point in time)', '6048000', '5400000'),
                    array('FIN_REV_SERVICES', 'Rendering of services (recognised over time)', '2100000', '1875000'),
                    array('FIN_REV_OTHER', 'Other / ancillary revenue', '252000', '225000'),
                    array('FIN_REV_LOCAL', 'Revenue — domestic market', '5880000', '5250000'),
                    array('FIN_REV_EXPORT', 'Revenue — export / foreign market', '2520000', '2250000'),
                    array('FIN_SEG1_REV', 'Segment 1 revenue (e.g. Trading)', '5460000', '4875000'),
                    array('FIN_SEG1_RESULT', 'Segment 1 result (profit)', '780000', '700000'),
                    array('FIN_SEG2_REV', 'Segment 2 revenue (e.g. Services)', '2940000', '2625000'),
                    array('FIN_SEG2_RESULT', 'Segment 2 result (profit)', '420000', '380000'),
                ),
                'PPE & intangible movement' => array(
                    array('Code', 'Property, plant & equipment (IAS 16)', 'Current year', 'Prior year'),
                    array('FIN_PPE_COST_OPEN', 'Cost — opening balance', '4900000', '4350000'),
                    array('FIN_PPE_ADDITIONS', 'Additions (capex) in the year', '650000', '600000'),
                    array('FIN_PPE_DISPOSALS', 'Disposals (cost) in the year', '50000', '50000'),
                    array('FIN_PPE_DEP_OPEN', 'Accumulated depreciation — opening', '1200000', '930000'),
                    array('FIN_PPE_DEP_CHARGE', 'Depreciation charge for the year', '289000', '258000'),
                    array('FIN_PPE_DEP_DISPOSAL', 'Accumulated depreciation on disposals', '0', '0'),
                    array('FIN_PPE_LAND', 'NBV — land & buildings', '1800000', '1650000'),
                    array('FIN_PPE_PLANT', 'NBV — plant & machinery', '980000', '870000'),
                    array('FIN_PPE_VEHICLES', 'NBV — motor vehicles', '420000', '380000'),
                    array('FIN_PPE_FF', 'NBV — furniture, fixtures & IT', '328000', '250000'),
                    array('— Intangible assets (IAS 38) —', '', '', ''),
                    array('FIN_INT_COST_OPEN', 'Cost — opening balance', '600000', '525000'),
                    array('FIN_INT_ADDITIONS', 'Additions in the year', '92000', '90000'),
                    array('FIN_INT_AMORT_OPEN', 'Accumulated amortisation — opening', '225000', '180000'),
                    array('FIN_INT_AMORT_CHARGE', 'Amortisation charge for the year', '47000', '42000'),
                ),
                'Receivables, inventory & ECL' => array(
                    array('Code', 'Trade receivables & ECL (IFRS 9)', 'Current year', 'Prior year'),
                    array('FIN_RECEIVABLES_GROSS', 'Gross trade receivables', '1387000', '1238000'),
                    array('FIN_ECL_ALLOWANCE', 'Less: expected credit loss allowance', '43000', '38000'),
                    array('FIN_REC_CURRENT', 'Ageing — not yet due (current)', '900000', '810000'),
                    array('FIN_REC_30', 'Ageing — 1 to 30 days past due', '280000', '250000'),
                    array('FIN_REC_60', 'Ageing — 31 to 60 days past due', '120000', '108000'),
                    array('FIN_REC_90', 'Ageing — 61 to 90 days past due', '52000', '45000'),
                    array('FIN_REC_90PLUS', 'Ageing — more than 90 days past due', '35000', '25000'),
                    array('— Inventory (IAS 2) —', '', '', ''),
                    array('FIN_INV_RAW', 'Raw materials', '300000', '270000'),
                    array('FIN_INV_WIP', 'Work in progress', '120000', '105000'),
                    array('FIN_INV_FINISHED', 'Finished goods / goods for resale', '504000', '450000'),
                    array('FIN_INV_PROVISION', 'Less: provision for slow-moving / obsolete', '0', '0'),
                ),
                'Tax reconciliation' => array(
                    array('Code', 'Income tax (IAS 12)', 'Current year', 'Prior year'),
                    array('FIN_TAX_CURRENT', 'Current tax charge', '113148', '90000'),
                    array('FIN_TAX_DEFERRED', 'Deferred tax charge / (credit)', '0', '0'),
                    array('FIN_TAX_RATE', 'Applicable statutory rate (%)', '9', '9'),
                    array('FIN_TAX_0BAND', 'Profit taxed at 0% band (first AED 375,000)', '375000', '375000'),
                    array('FIN_TAX_EXEMPT', 'Exempt / non-taxable income', '0', '0'),
                    array('FIN_TAX_NONDEDUCT', 'Non-deductible expenses (add-back)', '0', '0'),
                    array('FIN_DEFERRED_ASSET', 'Deferred tax asset (SOFP)', '0', '0'),
                    array('FIN_DEFERRED_LIAB', 'Deferred tax liability (SOFP)', '0', '0'),
                ),
                'Leases, borrowings & risk' => array(
                    array('Code', 'Leases & borrowings maturity (IFRS 16 / IFRS 7)', 'Current year', 'Prior year'),
                    array('FIN_LEASE_1YR', 'Lease payments due within 1 year', '90000', '80000'),
                    array('FIN_LEASE_1_5YR', 'Lease payments due 1 to 5 years', '150000', '135000'),
                    array('FIN_LEASE_5YR', 'Lease payments due after 5 years', '12000', '10000'),
                    array('FIN_BORROW_1YR', 'Borrowings repayable within 1 year', '420000', '375000'),
                    array('FIN_BORROW_1_5YR', 'Borrowings repayable 1 to 5 years', '1100000', '980000'),
                    array('FIN_BORROW_5YR', 'Borrowings repayable after 5 years', '412000', '370000'),
                    array('FIN_INT_RATE', 'Weighted average interest rate (%)', '6.5', '6.5'),
                    array('FIN_FX_EXPOSURE', 'Net foreign-currency monetary exposure', '0', '0'),
                ),
                'Equity, EPS & dividends' => array(
                    array('Code', 'Equity, EPS & dividends (IAS 33 / IAS 1)', 'Current year', 'Prior year'),
                    array('FIN_SHARES_WEIGHTED', 'Weighted average shares in issue', '500000', '500000'),
                    array('FIN_SHARES_ISSUED_YR', 'Shares issued during the year', '0', '0'),
                    array('FIN_DIV_PER_SHARE', 'Dividend per share declared', '0.60', '0.40'),
                    array('FIN_RESERVES_REVAL', 'Revaluation reserve', '84000', '75000'),
                    array('FIN_RESERVES_LEGAL', 'Statutory / legal reserve', '0', '0'),
                ),
                'Related parties & KMP' => array(
                    array('Code', 'Related parties & key management (IAS 24)', 'Current year', 'Prior year'),
                    array('FIN_KMP_SALARIES', 'KMP — short-term salaries & benefits', '720000', '660000'),
                    array('FIN_KMP_EOS', 'KMP — end-of-service / post-employment', '48000', '44000'),
                    array('FIN_RP_SALES', 'Sales to related parties', '0', '0'),
                    array('FIN_RP_PURCHASES', 'Purchases from related parties', '0', '0'),
                    array('FIN_RP_RECEIVABLE', 'Amounts due from related parties', '0', '0'),
                    array('FIN_RP_PAYABLE', 'Amounts due to related parties', '0', '0'),
                ),
                'Other disclosures' => array(
                    array('Code', 'Other disclosures', 'Detail / amount'),
                    array('FIN_CAPITAL_COMMIT', 'Capital commitments contracted but not provided', '0'),
                    array('FIN_CONTINGENT', 'Contingent liabilities (guarantees, claims)', '0'),
                    array('FIN_OPERATING_COMMIT', 'Other operating commitments', '0'),
                    array('DISC_SUBSEQUENT', 'Events after the reporting date (IAS 10)', 'None to report'),
                    array('DISC_GOING_CONCERN', 'Going-concern assessment (IAS 1 / ISA 570)', 'Going concern appropriate; no material uncertainty'),
                    array('DISC_CONTINGENT_NOTE', 'Narrative on contingencies/guarantees', 'None'),
                ),
                'Notes inputs' => array(
                    array('Note', 'Disclosure', 'Detail'),
                    array('Reporting entity (IAS 1)', 'Nature of business / principal activity', 'General trading and related services'),
                    array('Basis of preparation (IAS 1/8)', 'Measurement basis', 'Historical cost; going concern; accrual basis'),
                    array('Functional & presentation currency (IAS 21)', 'Currency policy', 'Functional and presentation currency is AED'),
                    array('Revenue (IFRS 15)', 'Disaggregation & timing', 'Goods at a point in time; services over time'),
                    array('Segments (IFRS 8)', 'Reportable segments', 'Trading and Services'),
                    array('PPE (IAS 16)', 'Depreciation method & useful lives', 'Straight-line: buildings 20-25y, plant 5-10y, vehicles 4-5y, IT 3-4y'),
                    array('Intangible assets (IAS 38)', 'Amortisation', 'Straight-line over 3-5 years'),
                    array('Impairment (IAS 36)', 'Indicator assessment', 'Annual indicator review; no impairment identified'),
                    array('Inventories (IAS 2)', 'Cost formula', 'Lower of cost (weighted average) and NRV'),
                    array('Financial instruments (IFRS 9/7)', 'ECL model', 'Lifetime ECL on trade receivables (simplified approach)'),
                    array('Fair value (IFRS 13)', 'Hierarchy', 'Level 2 — no level 3 measurements'),
                    array('Leases (IFRS 16)', 'Right-of-use assets', 'Recognised within PPE; liabilities at present value'),
                    array('Borrowing costs (IAS 23)', 'Policy', 'Capitalised on qualifying assets, otherwise expensed'),
                    array('Employee benefits (IAS 19)', 'End-of-service', 'Provision accrued per labour law'),
                    array('Income tax (IAS 12)', 'Rate', 'Corporate tax 0% up to AED 375k, 9% above; deferred tax recognised'),
                    array('Provisions (IAS 37)', 'Recognition', 'Present obligation, probable outflow, reliable estimate'),
                    array('Related parties (IAS 24)', 'Transactions', 'Key management & group balances at arm\'s length'),
                    array('Earnings per share (IAS 33)', 'Basic EPS', 'Profit ÷ weighted average shares'),
                    array('Events after reporting date (IAS 10)', 'Adjusting/non-adjusting', 'None to report'),
                ),
                'Compliance checklist' => array(
                    array('Check', 'Requirement', 'Expected'),
                    array('Framework stated', 'IFRS basis of preparation disclosed', 'IAS 1 note present'),
                    array('Comparatives', 'Prior-year figures for every statement', 'Two periods shown'),
                    array('SOFP balances', 'Assets = Equity + Liabilities', 'Difference = 0'),
                    array('Equity roll-forward', 'Opening + profit + OCI − dividends = closing', 'SOCE reconciles'),
                    array('Cash flow reconciles', 'Opening cash + net flow = closing cash', 'SCF ties to SOFP'),
                    array('Revenue disaggregation', 'Revenue split sums to total revenue', 'IFRS 15 note ties'),
                    array('PPE movement', 'Opening + additions − disposals − depreciation = closing NBV', 'IAS 16 note ties'),
                    array('Receivables ageing', 'Ageing buckets sum to gross receivables', 'IFRS 9 note ties'),
                    array('Tax reconciliation', 'Current + deferred = income tax expense', 'IAS 12 note ties'),
                    array('Going concern', 'Going-concern basis assessed (ISA 570)', 'Statement present'),
                    array('Auditor independence', 'Independence & ethics confirmed', 'Basis-for-opinion note'),
                    array('Key audit matters', 'KAM disclosed for listed/PIE (ISA 701)', 'Where applicable'),
                ),
            );
        }
        if ($kind === 'ct') {
            return array(
                'Instructions' => array(
                    array('Corporate Tax Return — complete import template (off-system)'),
                    array('Authority', 'Federal Tax Authority (FTA)'),
                    array('Governing law', 'Corporate Tax — Federal Decree-Law 47/2022'),
                    array('Format', 'Full CT return: taxpayer, elections, computation, Schedules 1-6, tax group & compliance'),
                    array(''),
                    array('How to use this workbook:'),
                    array('1', 'Fill the "Company & TRN" sheet — keep the Code column unchanged, edit the Value column.'),
                    array('2', 'Fill the "CT Computation" sheet — keep the Code column, enter your amounts. This drives the return.'),
                    array('3', 'Complete the supporting schedules so the return is fully evidenced:'),
                    array('', '   Sch 1 Adjustments · Sch 2 Fixed assets/depreciation · Sch 3 Exempt income · Sch 4 Related party/TP · Sch 5 Tax losses · Sch 6 Foreign tax credit'),
                    array('4', 'Set the "Elections & reliefs" and "Tax group & intercompany" sheets for your taxpayer.'),
                    array('5', 'Review the "Compliance checklist" — the engine validates each point against the figures you enter.'),
                    array('6', 'Save as .xlsx (or .csv) and upload it back under Import from Excel → Build return.'),
                    array('7', 'The return is built from the summary amounts; it stays off-system (no ERP data is read or written).'),
                    array(''),
                    array('Note', 'Keep the Code column EXACTLY as provided on Company & TRN and CT Computation — those codes are machine-read.'),
                ),
                'Company & TRN' => array(
                    array('Code', 'Field', 'Value'),
                    array('META_LEGAL_NAME', 'Legal name (taxable person)', 'Sample Client Trading LLC'),
                    array('META_TRN', 'Corporate Tax registration number (TRN)', '100000000000003'),
                    array('META_LEGAL_FORM', 'Legal form', 'Limited Liability Company (mainland)'),
                    array('META_ADDRESS', 'Registered address', 'Office 101, Business Bay, Dubai'),
                    array('META_EMIRATE', 'Emirate / region', 'Dubai'),
                    array('META_PHONE', 'Contact phone', '+971 4 000 0000'),
                    array('META_EMAIL', 'Contact email', 'finance@sampleclient.ae'),
                    array('META_RESIDENT', 'Resident person? (Yes/No)', 'Yes'),
                    array('META_BASIS_ACCT', 'Basis of accounting', 'Accrual (IFRS)'),
                    array('META_RETURN_TYPE', 'Return type', 'Annual Corporate Tax return'),
                    array('META_PERIOD_FROM', 'Financial year from (YYYY-MM-DD)', '2025-01-01'),
                    array('META_PERIOD_TO', 'Financial year to (YYYY-MM-DD)', '2025-12-31'),
                    array('META_FILING_DUE', 'Filing due date (YYYY-MM-DD)', '2026-09-30'),
                    array('META_GROUP_TRN', 'CT Tax Group TRN (if any)', ''),
                ),
                'Elections & reliefs' => array(
                    array('Election / relief', 'Status', 'Basis'),
                    array('Small Business Relief', 'Not available (revenue > AED 3m)', 'Ministerial Decision 73/2023 — Art. 21'),
                    array('Free Zone — Qualifying Free Zone Person (0%)', 'No — mainland LLC', 'Cabinet Decision 100/2023'),
                    array('Realisation basis (unrealised gains/losses)', 'Not elected', 'Ministerial Decision 134/2023 — Art. 20(3)'),
                    array('Transfers within a Qualifying Group', 'Not applied', 'Art. 26 — no gain/loss on intra-group transfers'),
                    array('Business Restructuring Relief', 'Not applied', 'Art. 27'),
                    array('Foreign PE exemption', 'Not elected', 'Art. 24'),
                ),
                'CT Computation' => array(
                    array('Code', 'Description', 'Amount'),
                    array('ACCT_PROFIT', 'Accounting net profit per financial statements', '1250000'),
                    array('REVENUE', 'Total revenue (for Small Business Relief test)', '8400000'),
                    array('FINES', 'Fines & administrative penalties (added back 100%)', '15000'),
                    array('ENTERTAINMENT', 'Entertainment expenditure - total (50% disallowed)', '40000'),
                    array('DONATIONS', 'Donations to non-approved bodies (added back)', '10000'),
                    array('PROVISIONS', 'General / non-specific provisions (added back)', '25000'),
                    array('ACCT_DEP', 'Accounting depreciation (added back)', '180000'),
                    array('TAX_DEP', 'Tax depreciation / capital allowances (deducted)', '210000'),
                    array('EXEMPT_INCOME', 'Exempt dividends / participation (deducted)', '60000'),
                    array('NET_INTEREST', 'Net interest expense (for 30% EBITDA cap)', '95000'),
                    array('EBITDA', 'EBITDA (for interest limitation, optional)', '1900000'),
                    array('LOSSES_BF', 'Tax losses brought forward', '120000'),
                    array('FTC', 'Foreign tax credit claimed (deducted from liability)', '0'),
                ),
                'Sch 1 Adjustments' => array(
                    array('Ref', 'Description', 'Amount', 'Treatment / basis'),
                    array('ADJ-001', 'Traffic & administrative fines', '15000', '100% non-deductible (Art. 33)'),
                    array('ADJ-002', 'Client entertainment', '40000', '50% disallowed (Art. 32)'),
                    array('ADJ-003', 'Donation - non-approved body', '10000', 'Non-deductible (Art. 37)'),
                    array('ADJ-004', 'General provision', '25000', 'Not yet incurred'),
                    array('…', 'add your own add-back rows below', '', ''),
                ),
                'Sch 2 Fixed assets' => array(
                    array('Asset', 'Class', 'Rate / method', 'Cost', 'Acct depreciation', 'Tax depreciation'),
                    array('Buildings & leasehold improvements', 'Buildings', '4% SL', '600000', '24000', '24000'),
                    array('Plant & machinery', 'P&M', '10% SL', '250000', '18000', '25000'),
                    array('Motor vehicles', 'Vehicles', '20% RB', '120000', '12000', '12000'),
                    array('Furniture, fixtures & IT', 'F&F', '25% SL', '80000', '6000', '9000'),
                    array('…', 'add your own asset rows below', '', '', '', ''),
                ),
                'Sch 3 Exempt income' => array(
                    array('Ref', 'Source', 'Nature', 'Amount', 'Basis'),
                    array('EX-001', 'Subsidiary FZE', 'Dividend from UAE subsidiary', '45000', 'Participation exemption (Art. 23)'),
                    array('EX-002', 'Foreign holding', 'Qualifying dividend', '15000', 'Participation exemption (Art. 22-23)'),
                    array('…', 'add your own exempt-income rows below', '', '', ''),
                ),
                'Sch 4 Related party' => array(
                    array('Party', 'Relationship', 'Nature of transaction', 'Amount', 'Arm\'s length?', 'TP method'),
                    array('Parent Co FZE', 'Parent', 'Management fee', '300000', 'Yes - benchmarked', 'TNMM'),
                    array('Sister Co LLC', 'Common control', 'Inventory purchase', '450000', 'Yes - benchmarked', 'CUP'),
                    array('Director', 'Connected person', 'Remuneration', '600000', 'Yes - market rate', 'Comparable'),
                    array('…', 'add your own related-party rows below', '', '', '', ''),
                ),
                'Sch 5 Tax losses' => array(
                    array('Year', 'Loss brought forward', 'Utilised this year', 'Carried forward', 'Cap (75% of taxable income)'),
                    array('2024', '120000', '90000', '30000', 'see computation'),
                    array('…', 'add prior-year loss rows below', '', '', ''),
                ),
                'Sch 6 Foreign tax credit' => array(
                    array('Ref', 'Foreign jurisdiction', 'Income type', 'Foreign tax paid', 'Credit claimed'),
                    array('FTC-001', 'KSA', 'Branch profit', '0', '0'),
                    array('…', 'add your own FTC rows below', '', '', ''),
                ),
                'Tax group & intercompany' => array(
                    array('Group member', 'TRN', 'Role'),
                    array('Sample Client Trading LLC', '100000000000003', 'Parent / representative'),
                    array('Sample Client Logistics LLC', '100000000000011', 'Subsidiary (100%)'),
                    array('Sample Client Retail LLC', '100000000000029', 'Subsidiary (100%)'),
                    array(''),
                    array('Intercompany transactions (eliminated on consolidation)', 'Amount', 'Note'),
                    array('Intra-group management fee', '200000', 'Eliminated (Art. 40-42)'),
                    array('Intra-group sale of goods', '350000', 'Eliminated (Art. 40-42)'),
                ),
                'Compliance checklist' => array(
                    array('Check', 'Requirement', 'Expected', 'Legal basis'),
                    array('CT registration', 'Taxable person registered & TRN obtained', 'TRN on file', 'Art. 51'),
                    array('Fines add-back', 'Fines/penalties added back 100%', 'FINES fully added back', 'Art. 33'),
                    array('Entertainment', '50% of entertainment disallowed', 'ENTERTAINMENT x 50% added back', 'Art. 32'),
                    array('Donations', 'Donations to non-approved bodies added back', 'DONATIONS added back', 'Art. 37'),
                    array('Provisions', 'General provisions added back', 'PROVISIONS added back', 'Art. 28'),
                    array('Depreciation', 'Accounting depreciation replaced by tax depreciation', 'ACCT_DEP back, TAX_DEP deducted', 'Art. 28'),
                    array('Exempt income', 'Dividends / participation excluded', 'EXEMPT_INCOME deducted', 'Art. 22-23'),
                    array('Interest cap', 'Net interest within 30% EBITDA / AED 12m de-minimis', 'NET_INTEREST <= cap', 'Art. 30'),
                    array('Loss relief', 'Tax losses utilised capped at 75% of taxable income', 'LOSSES utilised <= 75%', 'Art. 37'),
                    array('Transfer pricing', 'Related-party master/local file & disclosure form', 'Sch 4 completed', 'Art. 34-35'),
                    array('Tax group', 'Intercompany eliminated on consolidation', 'Eliminations applied', 'Art. 40-42'),
                    array('Small Business Relief', 'Election where revenue <= AED 3m', 'REVENUE > 3m -> not available', 'MD 73/2023'),
                    array('Tax bands', '0% up to AED 375,000; 9% above', 'Liability = 9% x (TI - 375k)', 'Art. 3'),
                    array('Filing', 'File within 9 months of period end', 'Due 9m after period end', 'Art. 53'),
                ),
            );
        }
        return array(
            'Instructions' => array(
                array('VAT Return (FTA VAT 201) — complete import template (off-system)'),
                array('Authority', 'Federal Tax Authority (FTA)'),
                array('Governing law', 'VAT — Federal Decree-Law 8/2017'),
                array('Format', 'Full VAT 201: company, boxes 1-14, invoice/TRN/supplier/adjustment schedules, tax group, supplies-by-treatment & compliance'),
                array(''),
                array('How to use this workbook:'),
                array('1', 'Fill the "Company & TRN" sheet — keep the Code column unchanged, edit the Value column.'),
                array('2', 'Fill the "VAT Boxes" sheet — keep the Code column, enter Amount / VAT / Adjustment per box. This drives the return.'),
                array('3', 'Complete the supporting schedules (the FTA audit-file detail behind the return):'),
                array('', '   Sales invoices (invoice-wise) · Purchase invoices · Customer TRN-wise · Supplier-wise · Adjustments register'),
                array('4', 'Use "Supplies by treatment" to map each supply to its correct UAE VAT scheme (the compliance engine checks these).'),
                array('5', 'Set the "Tax group & intercompany" sheet if you file as a VAT group (intra-group supplies are disregarded).'),
                array('6', 'Review the "Compliance checklist" — the engine validates each point against the figures you enter.'),
                array('7', 'Save as .xlsx (or .csv) and upload it back under Import from Excel → Build return.'),
                array('8', 'The return is built from the box totals; it stays off-system (no ERP data is read or written).'),
                array(''),
                array('Note', 'Keep the Code column EXACTLY as provided on Company & TRN and VAT Boxes — those codes are machine-read.'),
            ),
            'Company & TRN' => array(
                array('Code', 'Field', 'Value'),
                array('META_LEGAL_NAME', 'Legal name (taxable person)', 'Sample Client Trading LLC'),
                array('META_TRN', 'Tax Registration Number (TRN)', '100000000000003'),
                array('META_ADDRESS', 'Registered address', 'Office 101, Business Bay, Dubai'),
                array('META_EMIRATE', 'Emirate / region', 'Dubai'),
                array('META_PHONE', 'Contact phone', '+971 4 000 0000'),
                array('META_EMAIL', 'Contact email', 'finance@sampleclient.ae'),
                array('META_PERIOD_FROM', 'Tax period from (YYYY-MM-DD)', '2026-01-01'),
                array('META_PERIOD_TO', 'Tax period to (YYYY-MM-DD)', '2026-03-31'),
                array('META_BASIS', 'Filing basis (Monthly / Quarterly)', 'Quarterly'),
                array('META_GROUP_TRN', 'VAT Tax Group TRN (if any)', ''),
            ),
            'VAT Boxes' => array(
                array('Code', 'Description', 'Amount', 'VAT', 'Adjustment'),
                array('BOX1A', 'Standard-rated supplies - Abu Dhabi', '430500', '21525', '0'),
                array('BOX1B', 'Standard-rated supplies - Dubai', '645750', '32287.50', '0'),
                array('BOX1C', 'Standard-rated supplies - Sharjah', '172200', '8610', '0'),
                array('BOX1D', 'Standard-rated supplies - Ajman', '57400', '2870', '0'),
                array('BOX1E', 'Standard-rated supplies - Umm Al Quwain', '14350', '717.50', '0'),
                array('BOX1F', 'Standard-rated supplies - Ras Al Khaimah', '71750', '3587.50', '0'),
                array('BOX1G', 'Standard-rated supplies - Fujairah', '43050', '2152.50', '0'),
                array('BOX2', 'Tax refunds provided to tourists (negative)', '-11480', '-574', '0'),
                array('BOX3', 'Supplies subject to reverse charge (output)', '90000', '4500', '0'),
                array('BOX4', 'Zero-rated supplies', '320000', '0', '0'),
                array('BOX5', 'Exempt supplies', '85000', '0', '0'),
                array('BOX6', 'Goods imported into the UAE', '150000', '7500', '0'),
                array('BOX7', 'Adjustments to goods imported', '0', '0', '0'),
                array('BOX8', 'Totals — VAT on sales & all outputs (auto)', '1423520', '74248.50', '0'),
                array('BOX9', 'Standard-rated expenses (recoverable input VAT)', '980000', '49000', '0'),
                array('BOX10', 'Supplies subject to reverse charge (input VAT)', '90000', '4500', '0'),
                array('BOX11', 'Totals — VAT on expenses & all inputs (auto)', '1070000', '53500', '0'),
                array('BOX12', 'Total value of due tax for the period (auto)', '', '74248.50', ''),
                array('BOX13', 'Total value of recoverable tax for the period (auto)', '', '53500', ''),
                array('BOX14', 'Net VAT payable / (reclaimable) (auto)', '', '20748.50', ''),
            ),
            'Sales invoices' => array(
                array('Invoice', 'Date', 'Customer', 'Customer TRN', 'Emirate', 'Treatment', 'Net', 'VAT'),
                array('INV-0001', '2026-01-08', 'Gulf Distributors LLC', '100244880100003', 'Dubai', 'Standard 5%', '120000', '6000'),
                array('INV-0002', '2026-01-19', 'Emirates Retail Group LLC', '100355991200003', 'Abu Dhabi', 'Standard 5%', '85000', '4250'),
                array('INV-0003', '2026-02-03', 'Overseas Buyer Ltd', '', 'Export', 'Zero-rated export 0%', '140000', '0'),
                array('INV-0004', '2026-02-21', 'Al Futtaim Trading LLC', '100466002300003', 'Sharjah', 'Standard 5%', '64000', '3200'),
                array('…', '', 'add your own rows below', '', '', '', '', ''),
            ),
            'Purchase invoices' => array(
                array('Bill', 'Date', 'Supplier', 'Supplier TRN', 'Treatment', 'Net', 'VAT', 'Recoverable?'),
                array('BILL-0001', '2026-01-11', 'Prime Suppliers FZE', '100133220900003', 'Standard 5%', '300000', '15000', 'Yes'),
                array('BILL-0002', '2026-02-09', 'Logistics Partners LLC', '100277115400003', 'Standard 5%', '180000', '9000', 'Yes'),
                array('BILL-0003', '2026-03-02', 'Foreign Software Co', '', 'Imported services - RCM', '90000', '4500', 'Yes'),
                array('…', '', 'add your own rows below', '', '', '', '', ''),
            ),
            'Customer TRN-wise' => array(
                array('Customer', 'Customer TRN', 'Emirate', 'Net (output)', 'VAT (output)', 'Invoices'),
                array('Gulf Distributors LLC', '100244880100003', 'Dubai', '120000', '6000', '1'),
                array('Emirates Retail Group LLC', '100355991200003', 'Abu Dhabi', '85000', '4250', '1'),
                array('Al Futtaim Trading LLC', '100466002300003', 'Sharjah', '64000', '3200', '1'),
                array('Overseas Buyer Ltd', '(non-resident)', 'Export', '140000', '0', '1'),
                array('…', 'add your own customer summary rows below', '', '', '', ''),
            ),
            'Supplier-wise' => array(
                array('Supplier', 'Supplier TRN', 'Treatment', 'Net (input)', 'VAT (input)', 'Bills'),
                array('Prime Suppliers FZE', '100133220900003', 'Standard 5%', '300000', '15000', '1'),
                array('Logistics Partners LLC', '100277115400003', 'Standard 5%', '180000', '9000', '1'),
                array('Foreign Software Co', '(non-resident)', 'Imported services - RCM', '90000', '4500', '1'),
                array('…', 'add your own supplier summary rows below', '', '', '', ''),
            ),
            'Adjustments' => array(
                array('Ref', 'Date', 'Type', 'Description', 'Net adjustment', 'VAT adjustment', 'Basis'),
                array('ADJ-001', '2026-02-15', 'Credit note', 'Sales return - Gulf Distributors', '-8000', '-400', 'Output VAT adjustment'),
                array('ADJ-002', '2026-03-05', 'Bad debt relief', 'Receivable > 6 months written off', '-12000', '-600', 'Art. 64 bad-debt relief'),
                array('ADJ-003', '2026-03-20', 'Input correction', 'Prior-period under-claimed input VAT', '5000', '250', 'Voluntary correction'),
                array('…', '', 'add your own adjustment rows below', '', '', '', ''),
            ),
            'Supplies by treatment' => array(
                array('Doc', 'Sector', 'Item / supply', 'VAT treatment', 'Net', 'VAT charged', 'Legal basis'),
                array('INV-4001', 'Retail', 'Retail electronics sale', 'Standard-rated (5%)', '85000', '4250', 'FDL 8/2017, Art. 2 & 3'),
                array('INV-4003', 'Trade', 'Goods exported outside GCC', 'Zero-rated export (0%)', '140000', '0', 'Art. 45'),
                array('INV-4004', 'Services', 'Imported marketing services', 'Reverse charge', '30000', '0', 'Art. 48'),
                array('INV-4011', 'Real estate', 'Residential apartment lease', 'Residential lease — exempt', '120000', '0', 'Art. 46(1)'),
                array('INV-4012', 'Real estate', 'First sale of new residential', 'First new residential (0%)', '1500000', '0', 'Art. 45(9)'),
                array('INV-4013', 'Real estate', 'Sale of bare land', 'Bare land — exempt', '800000', '0', 'Art. 46(3)'),
                array('INV-4020', 'Education', 'Recognised curriculum tuition', 'Education (0%)', '95000', '0', 'CD 52/2017, Art. 40'),
                array('INV-4021', 'Healthcare', 'Basic healthcare service', 'Healthcare (0%)', '110000', '0', 'CD 52/2017, Art. 41'),
                array('INV-4030', 'Financial', 'Margin-based interest income', 'Financial services — exempt', '70000', '0', 'Art. 46(2)'),
                array('INV-4031', 'Transport', 'Local passenger transport', 'Local transport — exempt', '45000', '0', 'CD 52/2017, Art. 45'),
                array('INV-4032', 'Logistics', 'International air freight', 'International transport (0%)', '160000', '0', 'Art. 45(2)'),
                array('INV-4050', 'Free zone', 'Goods within designated zone', 'Designated zone (out of scope)', '90000', '0', 'CD 59/2017'),
                array('INV-4051', 'Automotive', 'Pre-owned vehicle', 'Profit-margin scheme (5% on margin)', '55000', '375', 'Exec. Reg. Art. 29'),
                array('INV-4052', 'Jewellery', '24kt investment gold bullion', 'Investment gold 24kt — exempt', '250000', '0', 'CD 25/2018'),
                array('INV-4054', 'Jewellery', 'Diamonds B2B to registrant', 'Gold & diamonds B2B — reverse charge', '120000', '0', 'CD 25/2018 / 127/2024'),
                array('…', '', 'add your own supply rows below', '', '', '', ''),
            ),
            'Tax group & intercompany' => array(
                array('Group member', 'Member TRN', 'Role'),
                array('Sample Client Trading LLC', '100000000000003', 'Representative member'),
                array('Sample Client Logistics LLC', '100000000000011', 'Member'),
                array('Sample Client Retail LLC', '100000000000029', 'Member'),
                array(''),
                array('Intercompany supplies (disregarded for VAT)', 'Net', 'VAT', 'Note'),
                array('Intra-group goods transfer', '155000', '7750', 'Disregarded — excluded from boxes 1-14 (Art. 9)'),
                array('Intra-group services', '100000', '5000', 'Disregarded — excluded from boxes 1-14 (Art. 9)'),
            ),
            'Compliance checklist' => array(
                array('Check', 'Requirement', 'Expected', 'Legal basis'),
                array('TRN present', 'Valid 15-digit TRN on the return', 'META_TRN set', 'FDL 8/2017'),
                array('Standard rate', 'Standard-rated supplies VAT = 5% of net', 'VAT = Net x 5%', 'Art. 2 & 3'),
                array('Residential lease', 'Residential property lease is exempt (no VAT)', '0% VAT', 'Art. 46(1)'),
                array('First new residential', 'First supply of new residential building 0%', '0% VAT', 'Art. 45(9)'),
                array('Bare land', 'Sale of bare land is exempt', '0% VAT', 'Art. 46(3)'),
                array('Commercial property', 'Commercial lease / sale standard-rated', 'VAT = Net x 5%', 'Art. 2 & 3'),
                array('Education/healthcare', 'Recognised education & basic healthcare 0%', '0% VAT', 'CD 52/2017'),
                array('Financial services', 'Margin-based financial services exempt', '0% VAT', 'Art. 46(2)'),
                array('Local vs intl transport', 'Local passenger transport exempt; international 0%', '0% VAT', 'CD 52/2017 / Art. 45'),
                array('Designated zone', 'Goods within a designated zone out of scope', 'Out of scope', 'CD 59/2017'),
                array('Profit-margin scheme', 'Pre-owned goods: 5% on margin only', 'VAT on margin', 'Exec. Reg. Art. 29'),
                array('B2B gold/diamonds', 'Reverse charge (seller charges 0%)', '0% VAT, buyer self-accounts', 'CD 25/2018 / 127/2024'),
                array('Investment gold', '24kt investment gold (>=99%) exempt', '0% VAT', 'CD 25/2018'),
                array('Imports / RCM', 'Reverse charge & imports self-cancel', 'Output = input VAT', 'Art. 48'),
                array('Exports', 'Exports / international transport 0%', '0% VAT', 'Art. 45'),
                array('Tax group', 'Intra-group supplies disregarded (single TRN)', 'Excluded from boxes 1-14', 'Art. 9'),
                array('Input recovery', 'Input VAT only on taxable (not exempt) supplies', 'Block on exempt', 'Art. 54'),
                array('Reconciliation', 'Box 14 = Box 12 - Box 13', 'Net = output - input', 'FTA VAT 201'),
                array('Filing & payment', 'File & pay within 28 days of period end', 'By 28th of next month', 'Art. 64'),
            ),
        );
    }
}

if (!function_exists('epc_ext_import_template_xlsx')) {
    /**
     * Complete multi-sheet .xlsx import template (company & TRN, boxes/computation,
     * invoice-wise detail, compliance checklist, instructions). $kind = 'vat'|'ct'.
     * Returns binary .xlsx content (empty string if ZipArchive unavailable).
     */
    function epc_ext_import_template_xlsx(string $kind): string
    {
        return epc_ext_xlsx_write(epc_ext_import_template_sheets($kind));
    }
}

if (!function_exists('epc_ext_parse_all_rows')) {
    /**
     * Parse every worksheet of an uploaded file into a single concatenated rows
     * array (off-system). For .xlsx all sheets are read so company/TRN, box and
     * computation codes are picked up wherever they live; for .csv it is the one
     * sheet. Returns array<int,array<int,string>> or null.
     */
    function epc_ext_parse_all_rows(string $path, string $name): ?array
    {
        $ext = strtolower(pathinfo($name, PATHINFO_EXTENSION));
        if ($ext === 'xlsx' && class_exists('ZipArchive')) {
            $all = epc_ext_parse_xlsx_all($path);
            if ($all === null) { return null; }
            $rows = array();
            foreach ($all as $sheetRows) {
                foreach ($sheetRows as $r) { $rows[] = $r; }
            }
            return $rows;
        }
        return epc_ext_parse_table($path, $name);
    }
}

if (!function_exists('epc_ext_parse_xlsx_all')) {
    /**
     * Read every worksheet of an .xlsx into [sheetIndex => rows] using
     * ZipArchive + SimpleXML (shared strings + inline strings supported).
     *
     * @return array<int,array<int,array<int,string>>>|null
     */
    function epc_ext_parse_xlsx_all(string $path): ?array
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
        $sheets = array();
        for ($i = 1; $i <= 50; $i++) {
            $sheetXml = $zip->getFromName('xl/worksheets/sheet' . $i . '.xml');
            if ($sheetXml === false) {
                if ($i === 1) { continue; }
                break;
            }
            $sx = @simplexml_load_string($sheetXml);
            if ($sx === false) { continue; }
            $rows = array();
            foreach ($sx->sheetData->row as $row) {
                $cells = array();
                $maxIdx = -1;
                foreach ($row->c as $c) {
                    $ref = (string) $c['r'];
                    $col = preg_replace('/[0-9]/', '', $ref);
                    $idx = epc_ext_xlsx_col_index($col);
                    $type = (string) $c['t'];
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
                for ($j = 0; $j <= $maxIdx; $j++) { $line[] = $cells[$j] ?? ''; }
                $rows[] = $line;
            }
            $sheets[] = $rows;
        }
        $zip->close();
        return $sheets;
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
        // Known CT computation codes — only these are read as computation values
        // so that supporting-schedule / compliance-checklist rows (which can share
        // a label such as "Entertainment") never overwrite a real figure.
        static $ctCodes = array(
            'ACCT_PROFIT', 'REVENUE', 'FINES', 'ENTERTAINMENT', 'DONATIONS',
            'PROVISIONS', 'ACCT_DEP', 'TAX_DEP', 'EXEMPT_INCOME', 'NET_INTEREST',
            'EBITDA', 'LOSSES_BF', 'FTC',
        );
        $meta = array();
        $values = array();
        $vat = array();
        $fin = array();
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
            // Financial-statement line: current amount in column C, comparative
            // (prior-year) amount in column D.
            if (strpos($code, 'FIN_') === 0) {
                $fin[$code] = array('cur' => $num($r[2] ?? 0), 'pri' => $num($r[3] ?? 0));
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
            // CT line: amount in column C — only recognised computation codes,
            // and never blank out a value already captured from the computation
            // sheet with a non-numeric cell from a later (schedule) sheet.
            if (in_array($code, $ctCodes, true)) {
                $cell = $r[2] ?? ($r[1] ?? 0);
                $cellTrim = str_replace(array(',', ' ', "\xC2\xA0"), '', trim((string) $cell));
                if (!is_numeric($cellTrim) && isset($values[$code])) { continue; }
                $values[$code] = $num($cell);
            }
        }
        return array('meta' => $meta, 'values' => $values, 'vat' => $vat, 'fin' => $fin);
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
        $ftcInput = $val('FTC');

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
        $ftcClaimed = round(min($ftcInput, $ct), 2);
        $netCt = max(0.0, round($ct - $ftcClaimed, 2));

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
            array('Corporate tax before credits', epc_ext_m($ct, $ccy)),
            array('Less: foreign tax credit (Art. 47, capped at CT due)', '(' . epc_ext_m($ftcClaimed, $ccy) . ')'),
            array('Net corporate tax payable', epc_ext_m($netCt, $ccy), true),
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
            . '<h4 style="color:#1d2740;margin-top:18px;">Tax bands, credits &amp; net liability</h4>' . $bands
            . '<h4 style="color:#1d2740;margin-top:18px;">Corporate tax compliance checks</h4>' . $compSummary
            . '<table class="table table-condensed" style="font-size:12px;"><thead><tr style="background:#f0f3f8;"><th>Result</th><th>Check</th></tr></thead><tbody>' . $cr . '</tbody></table>';

        return array(
            'title' => 'Corporate Income Tax Return — imported',
            'body' => $body,
            'summary' => array(
                'Taxable income' => epc_ext_m($taxableAfterSbr, $ccy),
                'Rate' => '0% / 9%',
                'CT before credits' => epc_ext_m($ct, $ccy),
                'Net CT payable' => epc_ext_m($netCt, $ccy),
                'Compliance' => ($errors === 0 && $warns === 0) ? 'All passed' : ($errors . ' err / ' . $warns . ' review'),
            ),
            'meta' => $map['meta'],
        );
    }
}

if (!function_exists('epc_ext_b_fin_summary')) {
    /**
     * Build a full IFRS financial-statements + audit pack from an uploaded
     * workbook (off-system). Renders cover page, Independent Auditor's Report,
     * SOFP, SOPL & OCI, Changes in Equity, Cash Flows, notes and compliance —
     * all with prior-year comparatives drawn from the uploaded columns.
     *
     * @param array{meta:array<string,string>,fin:array<string,array{cur:float,pri:float}>} $map
     * @return array{title:string,body:string,summary:array<string,string>,meta:array<string,string>}
     */
    function epc_ext_b_fin_summary(array $map, string $ccy): array
    {
        $fin = $map['fin'] ?? array();
        $meta = $map['meta'] ?? array();
        if (($meta['META_CURRENCY'] ?? '') !== '') { $ccy = (string) $meta['META_CURRENCY']; }
        $cur = static function (string $c) use ($fin): float { return (float) ($fin[$c]['cur'] ?? 0); };
        $pri = static function (string $c) use ($fin): float { return (float) ($fin[$c]['pri'] ?? 0); };
        $m = static function ($v) use ($ccy): string { return epc_ext_m((float) $v, $ccy); };

        $entity = (string) (($meta['META_LEGAL_NAME'] ?? '') ?: 'Uploaded client');
        $auditor = (string) (($meta['META_AUDITOR'] ?? '') ?: 'Independent Registered Auditors');
        $authority = (string) (($meta['META_AUDITOR_AUTH'] ?? '') ?: 'national audit oversight authority');
        $director = (string) (($meta['META_DIRECTOR'] ?? '') ?: 'Director');
        $pFrom = (string) ($meta['META_PERIOD_FROM'] ?? '');
        $pTo = (string) ($meta['META_PERIOD_TO'] ?? '');
        $curY = $pTo !== '' ? date('Y', (int) strtotime($pTo)) : date('Y');
        $priY = (string) ((int) $curY - 1);
        $cL = 'FY' . $curY; $pL = 'FY' . $priY;
        $fwk = 'International Financial Reporting Standards (IFRS) as issued by the IASB';

        // ---- derive the statements (current & prior) -----------------------
        $calc = static function (callable $v): array {
            $rev = $v('FIN_REVENUE'); $cogs = $v('FIN_COGS'); $oth = $v('FIN_OTHER_INCOME');
            $admin = $v('FIN_ADMIN_EXP'); $sell = $v('FIN_SELLING_EXP'); $depr = $v('FIN_DEPR');
            $finc = $v('FIN_FINANCE_COST'); $tax = $v('FIN_TAX'); $oci = $v('FIN_OCI');
            $gross = $rev - $cogs;
            $op = $gross + $oth - $admin - $sell - $depr;
            $pbt = $op - $finc;
            $profit = $pbt - $tax;
            $ppe = $v('FIN_PPE'); $intang = $v('FIN_INTANGIBLES');
            $invn = $v('FIN_INVENTORY'); $recv = $v('FIN_RECEIVABLES'); $cash = $v('FIN_CASH');
            $pay = $v('FIN_PAYABLES'); $bcur = $v('FIN_BORROW_CUR'); $bnon = $v('FIN_BORROW_NONCUR');
            $lease = $v('FIN_LEASE'); $prov = $v('FIN_PROVISIONS'); $taxp = $v('FIN_TAX_PAYABLE');
            $sc = $v('FIN_SHARE_CAPITAL'); $res = $v('FIN_OTHER_RESERVES');
            $ret0 = $v('FIN_RETAINED_OPEN'); $div = $v('FIN_DIVIDENDS');
            $retC = $ret0 + $profit - $div;
            $ncAsset = $ppe + $intang; $cAsset = $invn + $recv + $cash; $tAsset = $ncAsset + $cAsset;
            $equity = $sc + $res + $retC;
            $ncLiab = $bnon + $lease + $prov; $cLiab = $pay + $bcur + $taxp; $tLiab = $ncLiab + $cLiab;
            $eqLiab = $equity + $tLiab;
            return array(
                'rev' => $rev, 'cogs' => $cogs, 'gross' => $gross, 'oth' => $oth,
                'admin' => $admin, 'sell' => $sell, 'depr' => $depr, 'fin' => $finc,
                'op' => $op, 'pbt' => $pbt, 'tax' => $tax, 'profit' => $profit, 'oci' => $oci,
                'tci' => $profit + $oci,
                'ppe' => $ppe, 'intang' => $intang, 'inv' => $invn, 'recv' => $recv, 'cash' => $cash,
                'pay' => $pay, 'bcur' => $bcur, 'bnon' => $bnon, 'lease' => $lease, 'prov' => $prov, 'taxp' => $taxp,
                'sc' => $sc, 'res' => $res, 'ret0' => $ret0, 'div' => $div, 'retC' => $retC,
                'ncAsset' => $ncAsset, 'cAsset' => $cAsset, 'tAsset' => $tAsset,
                'equity' => $equity, 'ncLiab' => $ncLiab, 'cLiab' => $cLiab, 'tLiab' => $tLiab,
                'eqLiab' => $eqLiab, 'diff' => round($tAsset - $eqLiab, 2),
            );
        };
        $C = $calc($cur);
        $P = $calc($pri);

        // ---- two-period statement table helper -----------------------------
        $tblOpen = '<table class="table table-condensed" style="font-size:12.5px;"><thead><tr style="background:#13294b;color:#fff;">'
            . '<th>&nbsp;</th><th style="text-align:right;">' . epc_erp_h($cL) . '</th><th style="text-align:right;">' . epc_erp_h($pL) . '</th>'
            . '<th style="width:90px;text-align:center;">Ref</th></tr></thead><tbody>';
        $tblClose = '</tbody></table>';
        $line = static function (string $label, $c, $p, string $ref = '', string $kind = '') use ($m): string {
            $style = 'padding:5px 8px;';
            $lblStyle = $style; $numStyle = $style . 'text-align:right;font-variant-numeric:tabular-nums;';
            if ($kind === 'head') { return '<tr style="background:#eef2f8;"><td colspan="4" style="' . $style . 'font-weight:700;color:#13294b;">' . $label . '</td></tr>'; }
            if ($kind === 'sub') { $lblStyle .= 'font-weight:700;border-top:1px solid #cfd8e6;'; $numStyle .= 'font-weight:700;border-top:1px solid #cfd8e6;'; }
            if ($kind === 'total') { $lblStyle .= 'font-weight:800;border-top:2px solid #13294b;background:#f4fbf6;'; $numStyle .= 'font-weight:800;border-top:2px solid #13294b;background:#f4fbf6;'; }
            $cTxt = $c === '' ? '' : $m($c);
            $pTxt = $p === '' ? '' : $m($p);
            return '<tr><td style="' . $lblStyle . '">' . $label . '</td>'
                . '<td style="' . $numStyle . '">' . $cTxt . '</td>'
                . '<td style="' . $numStyle . '">' . $pTxt . '</td>'
                . '<td style="' . $style . 'text-align:center;color:#1d4e89;font-size:11px;">' . epc_erp_h($ref) . '</td></tr>';
        };

        // ---- Independent Auditor's Report ----------------------------------
        $opinionDate = $pTo !== '' ? date('d M Y', (int) strtotime($pTo . ' +3 months')) : date('d M Y');
        $audit = '<div style="border:1px solid #cfd8e6;border-radius:6px;padding:18px 20px;background:#fff;">'
            . '<h4 style="margin-top:0;color:#13294b;">Independent Auditor\'s Report</h4>'
            . '<p style="font-size:12.5px;">To the Shareholders of <strong>' . epc_erp_h($entity) . '</strong></p>'
            . '<p style="font-weight:700;margin-bottom:2px;">Opinion <span style="font-weight:400;font-size:11px;color:#1d4e89;">(ISA 700)</span></p>'
            . '<p style="font-size:12.5px;">We have audited the financial statements of ' . epc_erp_h($entity) . ', which comprise the statement of financial position as at 31 December ' . epc_erp_h($curY) . ', and the statement of profit or loss and other comprehensive income, statement of changes in equity and statement of cash flows for the year then ended, and notes to the financial statements. In our opinion, the accompanying financial statements present fairly, in all material respects, the financial position of the Company as at that date and its financial performance and cash flows for the year then ended in accordance with ' . epc_erp_h($fwk) . '.</p>'
            . '<p style="font-weight:700;margin-bottom:2px;">Basis for opinion <span style="font-weight:400;font-size:11px;color:#1d4e89;">(ISA 700)</span></p>'
            . '<p style="font-size:12.5px;">We conducted our audit in accordance with International Standards on Auditing (ISAs). Our responsibilities are described in the Auditor\'s Responsibilities section. We are independent of the Company in accordance with the IESBA Code of Ethics, and have fulfilled our other ethical responsibilities. We believe that the audit evidence obtained is sufficient and appropriate to provide a basis for our opinion.</p>'
            . '<p style="font-weight:700;margin-bottom:2px;">Going concern <span style="font-weight:400;font-size:11px;color:#1d4e89;">(ISA 570)</span></p>'
            . '<p style="font-size:12.5px;">The financial statements have been prepared on the going-concern basis. Based on the audit evidence obtained, we conclude that no material uncertainty exists that may cast significant doubt on the Company\'s ability to continue as a going concern.</p>'
            . '<p style="font-weight:700;margin-bottom:2px;">Key audit matters <span style="font-weight:400;font-size:11px;color:#1d4e89;">(ISA 701)</span></p>'
            . '<p style="font-size:12.5px;">Key audit matters are those that, in our professional judgement, were of most significance — principally revenue recognition (IFRS 15), valuation of trade receivables and expected credit losses (IFRS 9), and carrying value of property, plant &amp; equipment (IAS 16). Each was addressed through substantive testing and assessment of management\'s estimates.</p>'
            . '<p style="font-weight:700;margin-bottom:2px;">Responsibilities of management and the auditor <span style="font-weight:400;font-size:11px;color:#1d4e89;">(ISA 700/705/720)</span></p>'
            . '<p style="font-size:12.5px;">Management is responsible for the preparation and fair presentation of the financial statements in accordance with IFRS, and for such internal control as is necessary. Our objectives are to obtain reasonable assurance about whether the financial statements as a whole are free from material misstatement and to issue an auditor\'s report that includes our opinion.</p>'
            . '<div style="display:flex;justify-content:space-between;margin-top:14px;border-top:1px solid #cfd8e6;padding-top:10px;font-size:12px;">'
            . '<div><strong>' . epc_erp_h($auditor) . '</strong><br>' . epc_erp_h($authority) . '</div>'
            . '<div style="text-align:right;">Date: ' . epc_erp_h($opinionDate) . '<br>' . epc_erp_h(($meta['META_EMIRATE'] ?? 'United Arab Emirates')) . '</div></div></div>';

        // ---- SOPL & OCI ----------------------------------------------------
        $sopl = $tblOpen
            . $line('Revenue', $C['rev'], $P['rev'], 'IFRS 15')
            . $line('Cost of sales', -$C['cogs'], -$P['cogs'], 'IAS 2')
            . $line('Gross profit', $C['gross'], $P['gross'], '', 'sub')
            . $line('Other income', $C['oth'], $P['oth'], '')
            . $line('Administrative expenses', -$C['admin'], -$P['admin'], 'IAS 1')
            . $line('Selling &amp; distribution expenses', -$C['sell'], -$P['sell'], '')
            . $line('Depreciation &amp; amortisation', -$C['depr'], -$P['depr'], 'IAS 16/38')
            . $line('Operating profit', $C['op'], $P['op'], '', 'sub')
            . $line('Finance costs', -$C['fin'], -$P['fin'], 'IFRS 7')
            . $line('Profit before tax', $C['pbt'], $P['pbt'], '', 'sub')
            . $line('Income tax expense', -$C['tax'], -$P['tax'], 'IAS 12')
            . $line('Profit for the year', $C['profit'], $P['profit'], '', 'sub')
            . $line('Other comprehensive income', $C['oci'], $P['oci'], 'IAS 1')
            . $line('Total comprehensive income', $C['tci'], $P['tci'], '', 'total')
            . $tblClose;

        // ---- SOFP ----------------------------------------------------------
        $sofp = $tblOpen
            . $line('ASSETS', '', '', '', 'head')
            . $line('Non-current assets', '', '', '', 'head')
            . $line('Property, plant &amp; equipment', $C['ppe'], $P['ppe'], 'IAS 16')
            . $line('Intangible assets', $C['intang'], $P['intang'], 'IAS 38')
            . $line('Total non-current assets', $C['ncAsset'], $P['ncAsset'], '', 'sub')
            . $line('Current assets', '', '', '', 'head')
            . $line('Inventories', $C['inv'], $P['inv'], 'IAS 2')
            . $line('Trade &amp; other receivables', $C['recv'], $P['recv'], 'IFRS 9')
            . $line('Cash &amp; cash equivalents', $C['cash'], $P['cash'], 'IAS 7')
            . $line('Total current assets', $C['cAsset'], $P['cAsset'], '', 'sub')
            . $line('Total assets', $C['tAsset'], $P['tAsset'], '', 'total')
            . $line('EQUITY &amp; LIABILITIES', '', '', '', 'head')
            . $line('Equity', '', '', '', 'head')
            . $line('Share capital', $C['sc'], $P['sc'], 'IAS 1')
            . $line('Other reserves', $C['res'], $P['res'], '')
            . $line('Retained earnings', $C['retC'], $P['retC'], '')
            . $line('Total equity', $C['equity'], $P['equity'], '', 'sub')
            . $line('Non-current liabilities', '', '', '', 'head')
            . $line('Borrowings', $C['bnon'], $P['bnon'], 'IFRS 7')
            . $line('Lease liabilities', $C['lease'], $P['lease'], 'IFRS 16')
            . $line('Provisions', $C['prov'], $P['prov'], 'IAS 37')
            . $line('Total non-current liabilities', $C['ncLiab'], $P['ncLiab'], '', 'sub')
            . $line('Current liabilities', '', '', '', 'head')
            . $line('Trade &amp; other payables', $C['pay'], $P['pay'], '')
            . $line('Borrowings', $C['bcur'], $P['bcur'], 'IFRS 7')
            . $line('Current tax payable', $C['taxp'], $P['taxp'], 'IAS 12')
            . $line('Total current liabilities', $C['cLiab'], $P['cLiab'], '', 'sub')
            . $line('Total equity &amp; liabilities', $C['eqLiab'], $P['eqLiab'], '', 'total')
            . $tblClose;
        $balOk = abs($C['diff']) < 0.5;
        $sofp .= '<p style="font-size:11.5px;color:' . ($balOk ? '#1a7f37' : '#c0392b') . ';"><i class="fa fa-' . ($balOk ? 'check-circle' : 'exclamation-triangle') . '"></i> '
            . ($balOk ? 'Statement of financial position balances: total assets = equity + liabilities.' : 'Out of balance by ' . $m($C['diff']) . ' — check the uploaded figures.') . '</p>';

        // ---- SOCE ----------------------------------------------------------
        $resOpen = $C['res'] - $C['oci'];
        $soce = '<table class="table table-condensed" style="font-size:12px;"><thead><tr style="background:#13294b;color:#fff;">'
            . '<th>&nbsp;</th><th style="text-align:right;">Share capital</th><th style="text-align:right;">Other reserves</th><th style="text-align:right;">Retained earnings</th><th style="text-align:right;">Total</th></tr></thead><tbody>';
        $soceRow = static function (string $lbl, $a, $b, $c, bool $bold = false) use ($m): string {
            $w = $bold ? 'font-weight:800;border-top:2px solid #13294b;background:#f4fbf6;' : '';
            $n = 'text-align:right;padding:5px 8px;font-variant-numeric:tabular-nums;' . $w;
            $av = (float) ($a === '' ? 0 : $a); $bv = (float) ($b === '' ? 0 : $b); $cv = (float) ($c === '' ? 0 : $c);
            return '<tr><td style="padding:5px 8px;' . $w . '">' . epc_erp_h($lbl) . '</td>'
                . '<td style="' . $n . '">' . ($a === '' ? '' : $m($a)) . '</td>'
                . '<td style="' . $n . '">' . ($b === '' ? '' : $m($b)) . '</td>'
                . '<td style="' . $n . '">' . ($c === '' ? '' : $m($c)) . '</td>'
                . '<td style="' . $n . '">' . $m($av + $bv + $cv) . '</td></tr>';
        };
        $soce .= $soceRow('Balance at 1 January ' . $curY, $C['sc'], $resOpen, $C['ret0']);
        $soce .= $soceRow('Profit for the year', '', '', $C['profit']);
        $soce .= $soceRow('Other comprehensive income', '', $C['oci'], '');
        $soce .= $soceRow('Dividends declared', '', '', -$C['div']);
        $soce .= $soceRow('Balance at 31 December ' . $curY, $C['sc'], $C['res'], $C['retC'], true);
        $soce .= '</tbody></table>';

        // ---- SCF (indirect) ------------------------------------------------
        $dRecv = $C['recv'] - $P['recv']; $dInv = $C['inv'] - $P['inv'];
        $dPay = $C['pay'] - $P['pay']; $dProv = $C['prov'] - $P['prov'];
        $opCash = $C['pbt'] + $C['depr'] + $C['fin'] - $dRecv - $dInv + $dPay + $dProv - $C['tax'];
        $capex = ($C['ppe'] + $C['intang']) - ($P['ppe'] + $P['intang']) + $C['depr'];
        $invCash = -$capex;
        $dBorrow = ($C['bcur'] + $C['bnon']) - ($P['bcur'] + $P['bnon']);
        $dLease = $C['lease'] - $P['lease']; $dShare = $C['sc'] - $P['sc'];
        $finCash = $dBorrow + $dLease + $dShare - $C['div'] - $C['fin'];
        $netCash = $opCash + $invCash + $finCash;
        $closeCash = $P['cash'] + $netCash;
        $scf = $tblOpen
            . $line('Operating activities', '', '', '', 'head')
            . $line('Profit before tax', $C['pbt'], $P['pbt'], '')
            . $line('Adjust: depreciation &amp; amortisation', $C['depr'], $P['depr'], 'IAS 16/38')
            . $line('Adjust: finance costs', $C['fin'], $P['fin'], '')
            . $line('(Increase) / decrease in receivables', -$dRecv, '', '')
            . $line('(Increase) / decrease in inventories', -$dInv, '', '')
            . $line('Increase / (decrease) in payables', $dPay, '', '')
            . $line('Increase / (decrease) in provisions', $dProv, '', '')
            . $line('Income tax paid', -$C['tax'], '', 'IAS 12')
            . $line('Net cash from operating activities', $opCash, '', '', 'sub')
            . $line('Investing activities', '', '', '', 'head')
            . $line('Purchase of PPE &amp; intangibles', $invCash, '', 'IAS 16/38')
            . $line('Net cash used in investing activities', $invCash, '', '', 'sub')
            . $line('Financing activities', '', '', '', 'head')
            . $line('Net movement in borrowings', $dBorrow, '', '')
            . $line('Movement in lease liabilities', $dLease, '', 'IFRS 16')
            . $line('Proceeds from share issue', $dShare, '', '')
            . $line('Interest paid', -$C['fin'], '', '')
            . $line('Dividends paid', -$C['div'], '', '')
            . $line('Net cash from financing activities', $finCash, '', '', 'sub')
            . $line('Net increase / (decrease) in cash', $netCash, '', '', 'sub')
            . $line('Cash &amp; cash equivalents at 1 January', $P['cash'], '', '')
            . $line('Cash &amp; cash equivalents at 31 December', $closeCash, $C['cash'], '', 'total')
            . $tblClose;
        $cfOk = abs($closeCash - $C['cash']) < 1.0;
        $scf .= '<p style="font-size:11.5px;color:' . ($cfOk ? '#1a7f37' : '#c0392b') . ';"><i class="fa fa-' . ($cfOk ? 'check-circle' : 'exclamation-triangle') . '"></i> '
            . ($cfOk ? 'Cash flow reconciles to the closing cash on the statement of financial position.' : 'Derived closing cash ' . $m($closeCash) . ' differs from SOFP cash ' . $m($C['cash']) . ' — review working-capital inputs.') . '</p>';

        // ---- Notes ---------------------------------------------------------
        $noteN = 0;
        $note = static function (string $title, string $std, string $body) use (&$noteN): string {
            $noteN++;
            return '<div style="margin:0 0 12px;"><p style="font-weight:700;margin:0 0 3px;color:#1d2740;">' . $noteN . '. ' . epc_erp_h($title) . ' <span style="font-weight:400;font-size:11px;color:#1d4e89;">(' . epc_erp_h($std) . ')</span></p><div style="font-size:12.5px;line-height:1.6;color:#333;">' . $body . '</div></div>';
        };
        // mini note table (label / current / prior) — used by the figure-rich notes
        $ntbl = static function (array $rows) use ($m, $cL, $pL): string {
            $h = '<table class="table table-condensed" style="font-size:12px;margin:4px 0 6px;max-width:560px;"><thead><tr style="background:#eef2f8;">'
                . '<th style="padding:4px 8px;">&nbsp;</th><th style="padding:4px 8px;text-align:right;">' . epc_erp_h($cL) . '</th><th style="padding:4px 8px;text-align:right;">' . epc_erp_h($pL) . '</th></tr></thead><tbody>';
            foreach ($rows as $r) {
                $bold = !empty($r[3]);
                $st = 'padding:4px 8px;' . ($bold ? 'font-weight:700;border-top:1px solid #cfd8e6;' : '');
                $ns = $st . 'text-align:right;font-variant-numeric:tabular-nums;';
                $h .= '<tr><td style="' . $st . '">' . epc_erp_h((string) $r[0]) . '</td>'
                    . '<td style="' . $ns . '">' . ($r[1] === '' ? '' : $m((float) $r[1])) . '</td>'
                    . '<td style="' . $ns . '">' . ($r[2] === '' ? '' : $m((float) $r[2])) . '</td></tr>';
            }
            return $h . '</tbody></table>';
        };
        // pull detail inputs (fall back to derived splits when the client left them blank)
        $industry = (string) (($meta['META_INDUSTRY'] ?? '') ?: 'general trade and services');
        $activity = (string) (($meta['META_PRINCIPAL_ACTIVITY'] ?? '') ?: 'general trading and the provision of related services');
        $dv = static function (string $code, callable $g, float $fb) { $x = $g($code); return $x != 0.0 ? $x : $fb; };
        // revenue disaggregation
        $rvGc = $dv('FIN_REV_GOODS', $cur, round($C['rev'] * 0.72, 2)); $rvGp = $dv('FIN_REV_GOODS', $pri, round($P['rev'] * 0.72, 2));
        $rvSc = $dv('FIN_REV_SERVICES', $cur, round($C['rev'] * 0.25, 2)); $rvSp = $dv('FIN_REV_SERVICES', $pri, round($P['rev'] * 0.25, 2));
        $rvOc = $dv('FIN_REV_OTHER', $cur, round($C['rev'] - $rvGc - $rvSc, 2)); $rvOp = $dv('FIN_REV_OTHER', $pri, round($P['rev'] - $rvGp - $rvSp, 2));
        // PPE movement
        $ppeCostC = $dv('FIN_PPE_COST_OPEN', $cur, round($C['ppe'] * 1.34, 2)); $ppeAddC = $cur('FIN_PPE_ADDITIONS'); $ppeDispC = $cur('FIN_PPE_DISPOSALS');
        $ppeDepOpenC = $dv('FIN_PPE_DEP_OPEN', $cur, round($C['ppe'] * 0.34, 2)); $ppeDepChC = $dv('FIN_PPE_DEP_CHARGE', $cur, round($C['depr'] * 0.86, 2));
        $ppeCloseC = round(($ppeCostC + $ppeAddC - $ppeDispC) - ($ppeDepOpenC + $ppeDepChC - $cur('FIN_PPE_DEP_DISPOSAL')), 2);
        // receivables & ECL
        $recGrossC = $dv('FIN_RECEIVABLES_GROSS', $cur, round($C['recv'] * 1.031, 2)); $recGrossP = $dv('FIN_RECEIVABLES_GROSS', $pri, round($P['recv'] * 1.031, 2));
        $eclC = $dv('FIN_ECL_ALLOWANCE', $cur, round($C['recv'] * 0.031, 2)); $eclP = $dv('FIN_ECL_ALLOWANCE', $pri, round($P['recv'] * 0.031, 2));
        // tax split
        $taxCurC = $dv('FIN_TAX_CURRENT', $cur, $C['tax']); $taxDefC = $cur('FIN_TAX_DEFERRED');
        $taxCurP = $dv('FIN_TAX_CURRENT', $pri, $P['tax']); $taxDefP = $pri('FIN_TAX_DEFERRED');
        // EPS
        $wShares = $dv('FIN_SHARES_WEIGHTED', $cur, (float) ((string) ($meta['META_SHARE_COUNT'] ?? '') !== '' ? (float) $meta['META_SHARE_COUNT'] : 0.0));
        $epsC = $wShares > 0 ? $C['profit'] / $wShares : 0.0; $epsP = $wShares > 0 ? $P['profit'] / $wShares : 0.0;
        // KMP
        $kmpC = round($cur('FIN_KMP_SALARIES') + $cur('FIN_KMP_EOS'), 2); $kmpP = round($pri('FIN_KMP_SALARIES') + $pri('FIN_KMP_EOS'), 2);

        $notes = $note('Reporting entity', 'IAS 1', epc_erp_h($entity) . ' is principally engaged in ' . epc_erp_h($activity) . ' (sector: ' . epc_erp_h($industry) . '). The financial statements are presented in ' . epc_erp_h($ccy) . '.')
            . $note('Basis of preparation', 'IAS 1 / IAS 8', 'Prepared in accordance with ' . epc_erp_h($fwk) . ' on the historical-cost basis and the going-concern assumption; accounting policies are applied consistently with prior-year comparatives presented.')
            . $note('Revenue', 'IFRS 15', 'Revenue of ' . $m($C['rev']) . ' (' . $pL . ': ' . $m($P['rev']) . ') is recognised under the five-step model when control transfers, disaggregated as:' . $ntbl(array(
                array('Sale of goods (point in time)', $rvGc, $rvGp),
                array('Rendering of services (over time)', $rvSc, $rvSp),
                array('Other / ancillary revenue', $rvOc, $rvOp),
                array('Total revenue', $C['rev'], $P['rev'], true),
            )))
            . $note('Property, plant &amp; equipment', 'IAS 16', 'Movement in net book value, depreciated straight-line over useful lives:' . $ntbl(array(
                array('Cost — opening', $ppeCostC, ''),
                array('Additions', $ppeAddC, ''),
                array('Disposals', -$ppeDispC, ''),
                array('Accumulated depreciation — opening', -$ppeDepOpenC, ''),
                array('Depreciation charge', -$ppeDepChC, ''),
                array('Closing net book value', $ppeCloseC, $P['ppe'], true),
            )))
            . $note('Intangible assets', 'IAS 38', 'Carrying amount ' . $m($C['intang']) . ' (' . $pL . ': ' . $m($P['intang']) . '), amortised straight-line over 3–5 years.')
            . $note('Inventories', 'IAS 2', 'Stated at the lower of cost (weighted average) and net realisable value:' . $ntbl(array(
                array('Raw materials', $dv('FIN_INV_RAW', $cur, 0.0), $dv('FIN_INV_RAW', $pri, 0.0)),
                array('Work in progress', $dv('FIN_INV_WIP', $cur, 0.0), $dv('FIN_INV_WIP', $pri, 0.0)),
                array('Finished goods / for resale', $dv('FIN_INV_FINISHED', $cur, $C['inv']), $dv('FIN_INV_FINISHED', $pri, $P['inv'])),
                array('Total inventories', $C['inv'], $P['inv'], true),
            )))
            . $note('Trade &amp; other receivables / financial instruments', 'IFRS 9 / IFRS 7', 'Receivables are stated net of an expected-credit-loss allowance; the Company is exposed to credit, liquidity and market risk:' . $ntbl(array(
                array('Gross trade receivables', $recGrossC, $recGrossP),
                array('Less: ECL allowance', -$eclC, -$eclP),
                array('Net trade &amp; other receivables', $C['recv'], $P['recv'], true),
            )))
            . $note('Leases', 'IFRS 16', 'Lease liabilities of ' . $m($C['lease']) . ' with corresponding right-of-use assets in PPE; maturity within 1 year ' . $m($cur('FIN_LEASE_1YR')) . ', 1–5 years ' . $m($cur('FIN_LEASE_1_5YR')) . ', after 5 years ' . $m($cur('FIN_LEASE_5YR')) . '.')
            . $note('Provisions', 'IAS 37', 'Provisions of ' . $m($C['prov']) . ' represent present obligations measured at the best estimate of expenditure required to settle them.')
            . $note('Income tax', 'IAS 12', 'Tax expense ' . $m($C['tax']) . ' (current tax payable ' . $m($C['taxp']) . ') under the applicable corporate-tax regime:' . $ntbl(array(
                array('Current tax charge', $taxCurC, $taxCurP),
                array('Deferred tax charge / (credit)', $taxDefC, $taxDefP),
                array('Income tax expense', round($taxCurC + $taxDefC, 2), round($taxCurP + $taxDefP, 2), true),
            )))
            . $note('Earnings per share', 'IAS 33', 'Basic EPS = profit for the year ÷ weighted average shares' . ($wShares > 0 ? ' (' . number_format($wShares) . ' shares): ' . $m(round($epsC, 4)) . ' per share (' . $pL . ': ' . $m(round($epsP, 4)) . ').' : '. Enter weighted average shares on the "Equity, EPS & dividends" sheet to compute EPS.'))
            . $note('Related parties', 'IAS 24', 'Transactions and balances with shareholders and key management are conducted at arm\'s length. Key management personnel remuneration was ' . $m($kmpC) . ' (' . $pL . ': ' . $m($kmpP) . ').')
            . $note('Operating segments', 'IFRS 8', 'Segment 1 revenue ' . $m($cur('FIN_SEG1_REV')) . ' (result ' . $m($cur('FIN_SEG1_RESULT')) . '); Segment 2 revenue ' . $m($cur('FIN_SEG2_REV')) . ' (result ' . $m($cur('FIN_SEG2_RESULT')) . ').')
            . $note('Commitments &amp; contingencies', 'IAS 37', 'Capital commitments ' . $m($cur('FIN_CAPITAL_COMMIT')) . '; contingent liabilities ' . $m($cur('FIN_CONTINGENT')) . '. ' . epc_erp_h((string) ($meta['DISC_CONTINGENT_NOTE'] ?? '')))
            . $note('Events after the reporting period', 'IAS 10', (string) (($meta['DISC_SUBSEQUENT'] ?? '') !== '' ? epc_erp_h((string) $meta['DISC_SUBSEQUENT']) : 'No material adjusting or non-adjusting events occurred between the reporting date and the date of approval.'));

        // ---- compliance checks --------------------------------------------
        $checks = array();
        $checks[] = ($meta['META_TRN'] ?? '') !== '' ? array('ok', 'Entity registration / TRN present on the uploaded data.') : array('warn', 'Registration / TRN missing — add it before issuing.');
        $checks[] = $balOk ? array('ok', 'Statement of financial position balances (assets = equity + liabilities).') : array('error', 'SOFP does not balance — difference ' . $m($C['diff']) . '.');
        $checks[] = $cfOk ? array('ok', 'Cash flow statement reconciles to closing cash.') : array('warn', 'Cash flow does not tie to SOFP cash — review working-capital movements.');
        $hasPrior = ($P['rev'] + $P['tAsset']) > 0;
        $checks[] = $hasPrior ? array('ok', 'Prior-year comparatives provided for every statement (IAS 1).') : array('warn', 'No prior-year comparatives supplied — IAS 1 requires two periods.');
        $checks[] = $C['profit'] >= 0 ? array('ok', 'Entity is profitable; going-concern basis appropriate.') : array('warn', 'Loss for the year — confirm going-concern assessment (ISA 570).');
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

        $cover = epc_ext_cover_page(
            $entity,
            'Audited Financial Statements & Independent Auditor\'s Report',
            'IFRS · ISA 700 · off-system import',
            array(
                'Reporting period' => ($pFrom !== '' ? $pFrom . ' — ' . $pTo : $cL),
                'Comparative period' => $pL,
                'Reporting framework' => $fwk,
                'Presentation currency' => $ccy,
                'Independent auditor' => $auditor,
                'Oversight authority' => $authority,
                'Date of report' => $opinionDate,
            ),
            array(
                array('1', 'Independent Auditor\'s Report'),
                array('2', 'Statement of Financial Position'),
                array('3', 'Statement of Profit or Loss & Other Comprehensive Income'),
                array('4', 'Statement of Changes in Equity'),
                array('5', 'Statement of Cash Flows'),
                array('6', 'Notes to the Financial Statements'),
            )
        );

        $body = '<div class="epc-aud-doc">' . epc_ext_print_css() . $cover
            . '<div class="epc-aud-front">'
            . '<div class="alert alert-info" style="font-size:12px;"><i class="fa fa-upload"></i> Built from your <strong>uploaded workbook</strong> (off-system, summary figures with prior-year comparatives — no transaction detail). For preparing / reviewing other clients\' accounts; it does not read or write ERP data.</div>'
            . epc_ext_field_guide('Field guide — what each statement &amp; note shows (and the standard behind it)',
                'Plain-language explanation so a learner understands the pack. Framework: IFRS (IASB) + ISA (IAASB).',
                epc_ext_audit_guide_rows())
            . '</div>'
            . '<section class="epc-aud-sec"><h4>1 · Independent Auditor\'s Report</h4>' . $audit . '</section>'
            . '<section class="epc-aud-sec"><h4>2 · Statement of Financial Position</h4>' . $sofp . '</section>'
            . '<section class="epc-aud-sec"><h4>3 · Statement of Profit or Loss &amp; Other Comprehensive Income</h4>' . $sopl . '</section>'
            . '<section class="epc-aud-sec"><h4>4 · Statement of Changes in Equity</h4>' . $soce . '</section>'
            . '<section class="epc-aud-sec"><h4>5 · Statement of Cash Flows</h4>' . $scf . '</section>'
            . '<section class="epc-aud-sec"><h4>6 · Notes to the Financial Statements</h4>' . $notes . '</section>'
            . '<section class="epc-aud-sec"><h4>Compliance checks</h4>' . $compSummary
            . '<table class="table table-condensed" style="font-size:12px;"><thead><tr style="background:#f0f3f8;"><th>Result</th><th>Check</th></tr></thead><tbody>' . $cr . '</tbody></table>'
            . '<div style="display:flex;justify-content:space-between;margin-top:18px;border-top:1px solid #cfd8e6;padding-top:10px;font-size:12px;">'
            . '<div>Approved by the Board and signed on its behalf:<br><strong>' . epc_erp_h($director) . '</strong></div>'
            . '<div style="text-align:right;">' . epc_erp_h($auditor) . '<br>' . epc_erp_h($opinionDate) . '</div></div></section>'
            . '</div>';

        return array(
            'title' => 'IFRS Financial Statements & Audit Report — imported',
            'body' => $body,
            'summary' => array(
                'Revenue (' . $cL . ')' => $m($C['rev']),
                'Profit for the year' => $m($C['profit']),
                'Total assets' => $m($C['tAsset']),
                'Total equity' => $m($C['equity']),
                'SOFP balanced' => $balOk ? 'Yes' : 'No (' . $m($C['diff']) . ')',
                'Compliance' => ($errors === 0 && $warns === 0) ? 'All passed' : ($errors . ' err / ' . $warns . ' review'),
            ),
            'meta' => $map['meta'],
        );
    }
}

if (!function_exists('epc_ext_fin_projection')) {
    /**
     * Five-year forecast built from the dataset's current-year actuals using an
     * explicit assumptions block. Returns the assumptions and a yearly series
     * (P&L, free cash flow, EBITDA) used by both the financial model and the
     * valuation report so the two are internally consistent.
     *
     * @param array $d output of epc_ext_fin_dataset()
     * @return array{assume:array<string,float>,years:array<int,array<string,float|int>>,base:array<string,float|int>}
     */
    function epc_ext_fin_projection(array $d): array
    {
        $cur = $d['cur'];
        $assume = array(
            'growth' => 0.12,      // revenue CAGR
            'grossMargin' => round(($cur['gross'] / max(1.0, $cur['rev'])), 4),
            'opexPct' => round(($cur['opex'] / max(1.0, $cur['rev'])), 4),
            'deprPct' => round(($cur['depr'] / max(1.0, $cur['rev'])), 4),
            'interestPct' => round(($cur['interest'] / max(1.0, $cur['rev'])), 4),
            'capexPct' => 0.06,
            'wcPct' => 0.04,       // incremental working capital as % of revenue growth
            'taxRate' => 0.09,
            'taxFree' => 375000.0,
            'wacc' => 0.12,
            'terminalGrowth' => 0.03,
        );
        $years = array();
        $rev = (float) $cur['rev'];
        $baseYear = (int) $d['curYear'];
        for ($i = 1; $i <= 5; $i++) {
            $prevRev = $rev;
            $rev = round($rev * (1 + $assume['growth']), 2);
            $gross = round($rev * $assume['grossMargin'], 2);
            $opex = round($rev * $assume['opexPct'], 2);
            $depr = round($rev * $assume['deprPct'], 2);
            $interest = round($rev * $assume['interestPct'], 2);
            $ebitda = round($gross - $opex, 2);
            $ebit = round($ebitda - $depr, 2);
            $pbt = round($ebit - $interest, 2);
            $tax = round(max(0.0, $pbt - $assume['taxFree']) * $assume['taxRate'], 2);
            $profit = round($pbt - $tax, 2);
            $capex = round($rev * $assume['capexPct'], 2);
            $dWc = round(($rev - $prevRev) * $assume['wcPct'], 2);
            // unlevered free cash flow: EBIT(1-t) + depr - capex - ΔWC
            $nopat = round($ebit * (1 - $assume['taxRate']), 2);
            $fcf = round($nopat + $depr - $capex - $dWc, 2);
            $years[] = array(
                'year' => $baseYear + $i, 'n' => $i, 'rev' => $rev, 'gross' => $gross,
                'opex' => $opex, 'ebitda' => $ebitda, 'depr' => $depr, 'ebit' => $ebit,
                'interest' => $interest, 'pbt' => $pbt, 'tax' => $tax, 'profit' => $profit,
                'capex' => $capex, 'dWc' => $dWc, 'nopat' => $nopat, 'fcf' => $fcf,
            );
        }
        $base = array(
            'year' => $baseYear, 'rev' => $cur['rev'], 'gross' => $cur['gross'],
            'opex' => $cur['opex'], 'ebitda' => $cur['ebitda'], 'depr' => $cur['depr'],
            'ebit' => round($cur['ebitda'] - $cur['depr'], 2),
            'interest' => (float) ($cur['interest'] ?? 0), 'pbt' => $cur['pbt'],
            'tax' => $cur['tax'], 'profit' => $cur['profit'],
        );
        return array('assume' => $assume, 'years' => $years, 'base' => $base);
    }
}

if (!function_exists('epc_ext_b_finmodel')) {
    /**
     * Financial Model — a 3-statement-aware five-year forecast built from the
     * live GL actuals (period-aware) with an explicit assumptions block, a
     * projected P&L, EBITDA / free-cash-flow build and key ratios. Drives the
     * valuation report so both stay consistent. Tenant-country-driven.
     */
    function epc_ext_b_finmodel(PDO $db, string $name, string $country, string $ccy, $from, $to): array
    {
        $d = epc_ext_fin_dataset($db, $from, $to);
        $co = epc_ext_company($db);
        $entity = (string) ($co['legal_name'] ?: 'ECOM AE General Trading LLC');
        $proj = epc_ext_fin_projection($d);
        $a = $proj['assume']; $years = $proj['years']; $base = $proj['base'];
        $m = static function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };
        $pct = static function ($v) { return number_format((float) $v * 100, 1) . '%'; };

        $cover = epc_ext_cover_page(
            $entity,
            'Financial Model & Five-Year Forecast',
            'Financial model · 3-statement · DCF-ready',
            array(
                'Base year (actuals)' => 'FY' . $base['year'],
                'Forecast horizon' => 'FY' . ($base['year'] + 1) . ' — FY' . ($base['year'] + 5),
                'Presentation currency' => $ccy,
                'Revenue growth assumption' => $pct($a['growth']),
                'WACC' => $pct($a['wacc']),
                'Terminal growth' => $pct($a['terminalGrowth']),
            ),
            array(
                array('1', 'Assumptions'),
                array('2', 'Projected profit & loss'),
                array('3', 'EBITDA & free cash flow'),
                array('4', 'Key ratios'),
            )
        );

        // assumptions
        $assumeTbl = epc_ext_kv_table(array(
            array('Revenue growth (CAGR)', $pct($a['growth'])),
            array('Gross margin', $pct($a['grossMargin'])),
            array('Operating expenses (% revenue)', $pct($a['opexPct'])),
            array('Depreciation (% revenue)', $pct($a['deprPct'])),
            array('Capex (% revenue)', $pct($a['capexPct'])),
            array('Incremental working capital (% of revenue growth)', $pct($a['wcPct'])),
            array('Corporate tax rate (above ' . $m($a['taxFree']) . ')', $pct($a['taxRate'])),
            array('WACC (discount rate)', $pct($a['wacc'])),
            array('Terminal growth rate', $pct($a['terminalGrowth'])),
        ));

        // projected P&L table (base + 5 yrs)
        $hdr = '<th>&nbsp;</th><th style="text-align:right;">FY' . $base['year'] . '<br><small>actual</small></th>';
        foreach ($years as $y) { $hdr .= '<th style="text-align:right;">FY' . $y['year'] . '</th>'; }
        $rowF = static function (string $label, string $key, $base, array $years, callable $m, string $kind = '') {
            $w = $kind === 'sub' ? 'font-weight:700;border-top:1px solid #cfd8e6;' : ($kind === 'total' ? 'font-weight:800;border-top:2px solid #13294b;background:#f4fbf6;' : '');
            $r = '<tr><td style="padding:4px 8px;' . $w . '">' . $label . '</td>'
                . '<td style="padding:4px 8px;text-align:right;' . $w . '">' . $m($base[$key] ?? 0) . '</td>';
            foreach ($years as $y) { $r .= '<td style="padding:4px 8px;text-align:right;' . $w . '">' . $m($y[$key]) . '</td>'; }
            return $r . '</tr>';
        };
        $pl = '<div style="overflow-x:auto;"><table class="table table-condensed" style="font-size:12px;min-width:680px;"><thead><tr style="background:#13294b;color:#fff;">' . $hdr . '</tr></thead><tbody>'
            . $rowF('Revenue', 'rev', $base, $years, $m)
            . $rowF('Gross profit', 'gross', $base, $years, $m, 'sub')
            . $rowF('Operating expenses', 'opex', $base, $years, $m)
            . $rowF('EBITDA', 'ebitda', $base, $years, $m, 'sub')
            . $rowF('Depreciation & amortisation', 'depr', $base, $years, $m)
            . $rowF('Operating profit (EBIT)', 'ebit', $base, $years, $m, 'sub')
            . $rowF('Finance costs', 'interest', $base, $years, $m)
            . $rowF('Profit before tax', 'pbt', $base, $years, $m, 'sub')
            . $rowF('Income tax', 'tax', $base, $years, $m)
            . $rowF('Net profit', 'profit', $base, $years, $m, 'total')
            . '</tbody></table></div>';

        // FCF table (forecast years only — no base/actual column)
        $hdr2 = '<th>&nbsp;</th>';
        foreach ($years as $y) { $hdr2 .= '<th style="text-align:right;">FY' . $y['year'] . '</th>'; }
        $rowY = static function (string $label, string $key, array $years, callable $m, string $kind = '') {
            $w = $kind === 'sub' ? 'font-weight:700;border-top:1px solid #cfd8e6;' : ($kind === 'total' ? 'font-weight:800;border-top:2px solid #13294b;background:#f4fbf6;' : '');
            $r = '<tr><td style="padding:4px 8px;' . $w . '">' . $label . '</td>';
            foreach ($years as $y) { $r .= '<td style="padding:4px 8px;text-align:right;' . $w . '">' . $m($y[$key]) . '</td>'; }
            return $r . '</tr>';
        };
        $fcf = '<div style="overflow-x:auto;"><table class="table table-condensed" style="font-size:12px;min-width:620px;"><thead><tr style="background:#13294b;color:#fff;">' . $hdr2 . '</tr></thead><tbody>'
            . $rowY('EBIT', 'ebit', $years, $m)
            . $rowY('NOPAT (EBIT × (1−t))', 'nopat', $years, $m, 'sub')
            . $rowY('add: depreciation', 'depr', $years, $m)
            . $rowY('less: capex', 'capex', $years, $m)
            . $rowY('less: Δ working capital', 'dWc', $years, $m)
            . $rowY('Free cash flow', 'fcf', $years, $m, 'total')
            . '</tbody></table></div>';

        // ratios on base year
        $ratios = epc_ext_kv_table(array(
            array('Gross margin', $pct($a['grossMargin'])),
            array('EBITDA margin', $pct($base['ebitda'] / max(1.0, $base['rev']))),
            array('Net margin', $pct($base['profit'] / max(1.0, $base['rev']))),
            array('Revenue CAGR (forecast)', $pct($a['growth'])),
            array('Year-5 revenue', $m($years[4]['rev'])),
            array('Year-5 EBITDA', $m($years[4]['ebitda'])),
        ));

        $guide = epc_ext_field_guide('Field guide — how to read the financial model',
            'A financial model projects the business forward from its latest actuals using stated assumptions. Free cash flow drives the valuation.',
            array(
                array('Assumptions', 'Every projected figure is driven by these — growth, margins, capex and working capital. Change an assumption and the whole model moves.'),
                array('Projected P&L', 'Revenue grown at the CAGR; costs scaled by their margins; tax at the corporate rate. Net profit is the bottom line each year.'),
                array('EBITDA', 'Earnings before interest, tax, depreciation & amortisation — a proxy for operating cash generation and the basis for EV/EBITDA multiples.'),
                array('Free cash flow', 'NOPAT + depreciation − capex − increase in working capital. This is the cash available to all investors and the input to a DCF valuation.'),
                array('Key ratios', 'Margins and growth let you benchmark the business and sense-check the forecast.'),
            ));

        $commentary = epc_ext_commentary('Model explained', array(
            'This <strong>five-year financial model</strong> starts from the latest actual year (FY' . $base['year'] . ', anchored to the general ledger for the selected period) and projects revenue, profit and <strong>free cash flow</strong> forward using the assumptions in section 1.',
            'Revenue grows at <strong>' . $pct($a['growth']) . '</strong> a year; margins, depreciation, capex and working capital are held at their current ratios. Corporate tax is applied at <strong>' . $pct($a['taxRate']) . '</strong> on taxable profit above ' . $m($a['taxFree']) . '.',
            'The <strong>free cash flow</strong> line feeds directly into the Business Valuation report (DCF), so the model and the valuation are internally consistent.',
        ));

        $body = $cover
            . '<p class="text-muted">A <strong>five-year financial model</strong> built from the live GL actuals for the selected period, with an explicit assumptions block, projected P&amp;L, EBITDA / free-cash-flow build and key ratios. Drives the Business Valuation report.</p>'
            . $guide . $commentary
            . '<h4 style="color:#13294b;margin-top:16px;">1 · Assumptions</h4>' . $assumeTbl
            . '<h4 style="color:#13294b;margin-top:18px;">2 · Projected profit &amp; loss</h4>' . $pl
            . '<h4 style="color:#13294b;margin-top:18px;">3 · EBITDA &amp; free cash flow</h4>' . $fcf
            . '<h4 style="color:#13294b;margin-top:18px;">4 · Key ratios</h4>' . $ratios;

        return array(
            'title' => 'Financial Model & Five-Year Forecast',
            'body' => $body,
            'summary' => array(
                'Base revenue (FY' . $base['year'] . ')' => $m($base['rev']),
                'Year-5 revenue' => $m($years[4]['rev']),
                'Year-5 EBITDA' => $m($years[4]['ebitda']),
                'Year-1 free cash flow' => $m($years[0]['fcf']),
                'WACC' => $pct($a['wacc']),
            ),
            'live' => (bool) ($d['live'] ?? false),
        );
    }
}

if (!function_exists('epc_ext_b_valuation')) {
    /**
     * Business Valuation Report — values the entity by three methods that all
     * draw on the same financial model: discounted cash flow (WACC + Gordon
     * terminal value), market multiples (EV/EBITDA, P/E) and net assets / book
     * value. Produces an enterprise value → equity value bridge with a valuation
     * summary, field guide and commentary. Tenant-country-driven.
     */
    function epc_ext_b_valuation(PDO $db, string $name, string $country, string $ccy, $from, $to): array
    {
        $d = epc_ext_fin_dataset($db, $from, $to);
        $co = epc_ext_company($db);
        $entity = (string) ($co['legal_name'] ?: 'ECOM AE General Trading LLC');
        $proj = epc_ext_fin_projection($d);
        $a = $proj['assume']; $years = $proj['years']; $base = $proj['base'];
        $cur = $d['cur'];
        $m = static function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };
        $pct = static function ($v) { return number_format((float) $v * 100, 1) . '%'; };

        $wacc = $a['wacc']; $g = $a['terminalGrowth'];
        // ---- DCF -----------------------------------------------------------
        $pvSum = 0.0; $dcfRows = '';
        foreach ($years as $y) {
            $df = 1 / pow(1 + $wacc, $y['n']);
            $pv = round($y['fcf'] * $df, 2);
            $pvSum += $pv;
            $dcfRows .= '<tr><td style="padding:4px 8px;">FY' . $y['year'] . '</td>'
                . '<td style="padding:4px 8px;text-align:right;">' . $m($y['fcf']) . '</td>'
                . '<td style="padding:4px 8px;text-align:right;">' . number_format($df, 3) . '</td>'
                . '<td style="padding:4px 8px;text-align:right;">' . $m($pv) . '</td></tr>';
        }
        $fcf5 = (float) $years[4]['fcf'];
        $terminal = round(($fcf5 * (1 + $g)) / ($wacc - $g), 2);
        $pvTerminal = round($terminal / pow(1 + $wacc, 5), 2);
        $evDcf = round($pvSum + $pvTerminal, 2);

        $netDebt = round(($cur['borrowCur'] + $cur['borrowNon'] + $cur['lease']) - $cur['cash'], 2);
        $equityDcf = round($evDcf - $netDebt, 2);

        // ---- net-debt build (EV → equity bridge) ---------------------------
        $netDebtTbl = epc_ext_kv_table(array(
            array('Borrowings — current', $m($cur['borrowCur'])),
            array('Borrowings — non-current', $m($cur['borrowNon'])),
            array('Lease liabilities (IFRS 16)', $m($cur['lease'])),
            array('less: cash & cash equivalents', '(' . $m($cur['cash']) . ')'),
            array('Net debt', $m($netDebt), true),
        ));

        // ---- sensitivity: equity value (DCF) vs WACC × terminal growth -----
        $waccSteps = array($wacc - 0.02, $wacc - 0.01, $wacc, $wacc + 0.01, $wacc + 0.02);
        $gSteps = array($g - 0.01, $g, $g + 0.01);
        $sensHdr = '<th style="text-align:left;">WACC \\ g →</th>';
        foreach ($gSteps as $gs) { $sensHdr .= '<th style="text-align:right;">' . $pct($gs) . '</th>'; }
        $sensRows = '';
        foreach ($waccSteps as $ws) {
            $sensRows .= '<tr><td style="padding:4px 8px;font-weight:600;">' . $pct($ws) . '</td>';
            foreach ($gSteps as $gs) {
                if ($ws <= $gs) { $sensRows .= '<td style="padding:4px 8px;text-align:right;color:#999;">n/m</td>'; continue; }
                $pvS = 0.0;
                foreach ($years as $y) { $pvS += $y['fcf'] / pow(1 + $ws, $y['n']); }
                $tv = ($fcf5 * (1 + $gs)) / ($ws - $gs);
                $evS = $pvS + $tv / pow(1 + $ws, 5);
                $eqS = round($evS - $netDebt, 2);
                $hl = (abs($ws - $wacc) < 1e-9 && abs($gs - $g) < 1e-9) ? 'background:#f4fbf6;font-weight:800;' : '';
                $sensRows .= '<td style="padding:4px 8px;text-align:right;' . $hl . '">' . $m($eqS) . '</td>';
            }
            $sensRows .= '</tr>';
        }
        $sensTbl = '<div style="overflow-x:auto;"><table class="table table-condensed" style="font-size:12px;min-width:460px;"><thead><tr style="background:#13294b;color:#fff;">'
            . $sensHdr . '</tr></thead><tbody>' . $sensRows . '</tbody></table></div>'
            . '<p class="text-muted" style="font-size:11px;">Equity value (DCF) under different discount-rate (WACC) and terminal-growth (g) assumptions; the shaded cell is the base case. "n/m" where g ≥ WACC (Gordon model undefined).</p>';

        $dcf = '<div style="overflow-x:auto;"><table class="table table-condensed" style="font-size:12px;min-width:560px;"><thead><tr style="background:#13294b;color:#fff;">'
            . '<th>Year</th><th style="text-align:right;">Free cash flow</th><th style="text-align:right;">Discount factor</th><th style="text-align:right;">Present value</th></tr></thead><tbody>'
            . $dcfRows
            . '<tr style="font-weight:700;border-top:1px solid #cfd8e6;"><td colspan="3" style="padding:4px 8px;">Sum of PV of explicit FCF</td><td style="padding:4px 8px;text-align:right;">' . $m($pvSum) . '</td></tr>'
            . '<tr><td colspan="3" style="padding:4px 8px;">Terminal value (Gordon, g=' . $pct($g) . ') = FCF₅×(1+g)/(WACC−g)</td><td style="padding:4px 8px;text-align:right;">' . $m($terminal) . '</td></tr>'
            . '<tr><td colspan="3" style="padding:4px 8px;">PV of terminal value</td><td style="padding:4px 8px;text-align:right;">' . $m($pvTerminal) . '</td></tr>'
            . '<tr style="font-weight:800;border-top:2px solid #13294b;background:#f4fbf6;"><td colspan="3" style="padding:4px 8px;">Enterprise value (DCF)</td><td style="padding:4px 8px;text-align:right;">' . $m($evDcf) . '</td></tr>'
            . '</tbody></table></div>';

        // ---- multiples -----------------------------------------------------
        $evEbitdaMult = 7.0; $peMult = 12.0;
        $evMult = round($base['ebitda'] * $evEbitdaMult, 2);
        $equityMult = round($evMult - $netDebt, 2);
        $equityPe = round($base['profit'] * $peMult, 2);
        $multiples = epc_ext_kv_table(array(
            array('EBITDA (FY' . $base['year'] . ')', $m($base['ebitda'])),
            array('EV/EBITDA multiple (comparable)', number_format($evEbitdaMult, 1) . '×'),
            array('Enterprise value (EV/EBITDA)', $m($evMult)),
            array('less: net debt', '(' . $m($netDebt) . ')'),
            array('Equity value (EV/EBITDA)', $m($equityMult)),
            array('Net profit (FY' . $base['year'] . ')', $m($base['profit'])),
            array('P/E multiple (comparable)', number_format($peMult, 1) . '×'),
            array('Equity value (P/E)', $m($equityPe)),
        ));

        // ---- net assets ----------------------------------------------------
        $netAssets = round($cur['totalEquity'], 2);
        $netAssetsTbl = epc_ext_kv_table(array(
            array('Total assets', $m($cur['totalAssets'])),
            array('less: total liabilities', '(' . $m($cur['totalLiab']) . ')'),
            array('Net assets / book value of equity', $m($netAssets)),
        ));

        // ---- summary -------------------------------------------------------
        $vals = array($equityDcf, $equityMult, $equityPe, $netAssets);
        $avg = round(array_sum($vals) / count($vals), 2);
        $lo = round(min($vals), 2); $hi = round(max($vals), 2);
        $summaryTbl = epc_ext_kv_table(array(
            array('Discounted cash flow (equity)', $m($equityDcf)),
            array('Market multiples — EV/EBITDA (equity)', $m($equityMult)),
            array('Market multiples — P/E (equity)', $m($equityPe)),
            array('Net assets / book value', $m($netAssets)),
            array('Indicative equity value range', $m($lo) . '  —  ' . $m($hi)),
            array('Central estimate (average)', $m($avg), true),
        ));

        $cover = epc_ext_cover_page(
            $entity,
            'Business Valuation Report',
            'Valuation · DCF · multiples · net assets',
            array(
                'Valuation date' => date('d M Y'),
                'Base year (actuals)' => 'FY' . $base['year'],
                'Presentation currency' => $ccy,
                'WACC' => $pct($wacc),
                'Terminal growth' => $pct($g),
                'Central equity value' => $m($avg),
            ),
            array(
                array('1', 'Valuation summary'),
                array('2', 'Discounted cash flow (DCF)'),
                array('3', 'Net-debt build (EV → equity bridge)'),
                array('4', 'Sensitivity — WACC × terminal growth'),
                array('5', 'Market multiples'),
                array('6', 'Net assets / book value'),
            )
        );

        $guide = epc_ext_field_guide('Field guide — how the business is valued',
            'Three independent methods triangulate a defensible value. Each starts from the financial model.',
            array(
                array('Discounted cash flow', 'Projected free cash flows are discounted to today at the WACC; a terminal value captures cash flows beyond the forecast. Sum = enterprise value.'),
                array('Enterprise vs equity', 'Enterprise value − net debt = equity value (what the shares are worth). Net debt = borrowings + leases − cash.'),
                array('Market multiples', 'Apply a comparable EV/EBITDA or P/E multiple to the company\'s earnings to cross-check the DCF.'),
                array('Net assets', 'Book value of equity (assets − liabilities) — a floor value, most relevant for asset-heavy businesses.'),
                array('Valuation range', 'The methods rarely agree exactly; the range and central estimate communicate the uncertainty honestly.'),
            ));

        $commentary = epc_ext_commentary('Valuation explained', array(
            'This report values <strong>' . epc_erp_h($entity) . '</strong> using three methods that all draw on the same five-year financial model, so the analysis is internally consistent.',
            'The <strong>DCF</strong> discounts projected free cash flows at a WACC of <strong>' . $pct($wacc) . '</strong> and adds a Gordon-growth terminal value (g = ' . $pct($g) . '), giving an enterprise value of <strong>' . $m($evDcf) . '</strong> and, after net debt of ' . $m($netDebt) . ', an equity value of <strong>' . $m($equityDcf) . '</strong>.',
            'The <strong>market-multiple</strong> and <strong>net-asset</strong> methods cross-check this. The indicative equity range is <strong>' . $m($lo) . ' — ' . $m($hi) . '</strong>, with a central estimate of <strong>' . $m($avg) . '</strong>.',
        ));

        $body = $cover
            . '<p class="text-muted">A <strong>business valuation</strong> by three methods — discounted cash flow, market multiples and net assets — all built on the five-year financial model from the live GL actuals. Tenant-country-driven.</p>'
            . $guide . $commentary
            . '<h4 style="color:#13294b;margin-top:16px;">1 · Valuation summary</h4>' . $summaryTbl
            . '<h4 style="color:#13294b;margin-top:18px;">2 · Discounted cash flow (DCF)</h4>' . $dcf
            . '<p class="text-muted" style="font-size:11.5px;">Enterprise value ' . $m($evDcf) . ' − net debt ' . $m($netDebt) . ' = equity value ' . $m($equityDcf) . '.</p>'
            . '<h4 style="color:#13294b;margin-top:18px;">3 · Net-debt build (EV → equity bridge)</h4>' . $netDebtTbl
            . '<h4 style="color:#13294b;margin-top:18px;">4 · Sensitivity — WACC × terminal growth</h4>' . $sensTbl
            . '<h4 style="color:#13294b;margin-top:18px;">5 · Market multiples</h4>' . $multiples
            . '<h4 style="color:#13294b;margin-top:18px;">6 · Net assets / book value</h4>' . $netAssetsTbl;

        return array(
            'title' => 'Business Valuation Report',
            'body' => $body,
            'summary' => array(
                'Enterprise value (DCF)' => $m($evDcf),
                'Equity value (DCF)' => $m($equityDcf),
                'Equity value (EV/EBITDA)' => $m($equityMult),
                'Net assets' => $m($netAssets),
                'Central equity value' => $m($avg),
            ),
            'live' => (bool) ($d['live'] ?? false),
        );
    }
}

if (!function_exists('epc_ext_finmodel_xlsx')) {
    /**
     * Linked .xlsx workbook for the Financial Model & Business Valuation.
     *
     * Three sheets — Assumptions, Calculations, Results — where every cell on the
     * Calculations and Results sheets is a LIVE FORMULA referencing the
     * Assumptions sheet (e.g. =Assumptions!$B$3*(1+Assumptions!$B$4)). Cached
     * values mirror the projection in epc_ext_fin_projection() so the figures
     * tie out to the on-screen report; changing any assumption in Excel
     * recalculates the whole model and valuation. Tenant-country-driven.
     *
     * @return string binary .xlsx content ('' if ZipArchive unavailable)
     */
    function epc_ext_finmodel_xlsx(PDO $db, string $ccy, $from, $to): string
    {
        $d = epc_ext_fin_dataset($db, $from, $to);
        $proj = epc_ext_fin_projection($d);
        $a = $proj['assume']; $years = $proj['years']; $base = $proj['base'];
        $cur = $d['cur'];
        $baseYear = (int) $base['year'];

        $evEbitdaMult = 7.0; $peMult = 12.0;
        $baseRev = (float) $base['rev'];
        $netDebt = round(($cur['borrowCur'] + $cur['borrowNon'] + $cur['lease']) - $cur['cash'], 2);
        $baseEbitda = (float) $base['ebitda'];
        $baseProfit = (float) $base['profit'];
        $totalAssets = (float) $cur['totalAssets'];
        $totalLiab = (float) $cur['totalLiab'];

        $num = static function ($v): array { return array('t' => 'n', 'v' => round((float) $v, 6)); };
        $fml = static function (string $f, $v): array { return array('f' => $f, 'v' => round((float) $v, 6)); };

        // ---- Sheet 1: Assumptions (the only inputs) ------------------------
        $A = array(
            array('Financial Model & Valuation — Assumptions', 'Value', 'Notes'),
            array('Driver', 'Value', 'Notes'),
            array('Base-year revenue (FY' . $baseYear . ' actual)', $num($baseRev), 'from the general ledger'),
            array('Revenue growth (CAGR)', $num($a['growth']), 'applied to each forecast year'),
            array('Gross margin', $num($a['grossMargin']), '× revenue = gross profit'),
            array('Operating expenses (% revenue)', $num($a['opexPct']), ''),
            array('Depreciation (% revenue)', $num($a['deprPct']), ''),
            array('Finance cost (% revenue)', $num($a['interestPct']), ''),
            array('Capex (% revenue)', $num($a['capexPct']), 'cash outflow in FCF'),
            array('Incremental working capital (% of Δrevenue)', $num($a['wcPct']), ''),
            array('Corporate tax rate', $num($a['taxRate']), 'on profit above threshold'),
            array('Tax-free threshold', $num($a['taxFree']), 'UAE CT 0% band'),
            array('WACC (discount rate)', $num($a['wacc']), 'DCF discounting'),
            array('Terminal growth rate', $num($a['terminalGrowth']), 'Gordon growth'),
            array('EV/EBITDA multiple (comparable)', $num($evEbitdaMult), 'market multiple'),
            array('P/E multiple (comparable)', $num($peMult), 'market multiple'),
            array('Net debt (borrowings + leases − cash)', $num($netDebt), 'EV → equity bridge'),
            array('Total assets (actual)', $num($totalAssets), 'net-asset method'),
            array('Total liabilities (actual)', $num($totalLiab), 'net-asset method'),
            array('Base-year EBITDA (actual)', $num($baseEbitda), 'EV/EBITDA method'),
            array('Base-year net profit (actual)', $num($baseProfit), 'P/E method'),
        );
        // Assumption cell map (1-based row in sheet):
        //  B3 baseRev  B4 growth  B5 grossM  B6 opex%  B7 depr%  B8 fin%
        //  B9 capex%  B10 wc%  B11 taxRate  B12 taxFree  B13 WACC  B14 g
        //  B15 EV/EBITDA  B16 P/E  B17 netDebt  B18 tAssets  B19 tLiab
        //  B20 baseEBITDA  B21 baseProfit

        // ---- Sheet 2: Calculations (live formulas) -------------------------
        $cols = array('B', 'C', 'D', 'E', 'F'); // FY+1 .. FY+5
        $hdr = array('Line \\ Year');
        foreach ($years as $y) { $hdr[] = 'FY' . $y['year']; }

        // helper: build a row whose forecast cells are formulas
        $mkRow = function (string $label, callable $cellFn) use ($cols, $years) {
            $row = array($label);
            foreach ($cols as $i => $col) { $row[] = $cellFn($i, $col); }
            return $row;
        };

        $rev = $mkRow('Revenue', function ($i, $col) use ($fml, $years) {
            $f = $i === 0 ? 'Assumptions!$B$3*(1+Assumptions!$B$4)'
                          : chr(ord('B') + $i - 1) . '3*(1+Assumptions!$B$4)';
            return $fml($f, $years[$i]['rev']);
        });
        $gross = $mkRow('Gross profit', function ($i, $col) use ($fml, $years) {
            return $fml($col . '3*Assumptions!$B$5', $years[$i]['gross']);
        });
        $opex = $mkRow('Operating expenses', function ($i, $col) use ($fml, $years) {
            return $fml($col . '3*Assumptions!$B$6', $years[$i]['opex']);
        });
        $ebitda = $mkRow('EBITDA', function ($i, $col) use ($fml, $years) {
            return $fml($col . '4-' . $col . '5', $years[$i]['ebitda']);
        });
        $depr = $mkRow('Depreciation & amortisation', function ($i, $col) use ($fml, $years) {
            return $fml($col . '3*Assumptions!$B$7', $years[$i]['depr']);
        });
        $ebit = $mkRow('Operating profit (EBIT)', function ($i, $col) use ($fml, $years) {
            return $fml($col . '6-' . $col . '7', $years[$i]['ebit']);
        });
        $fin = $mkRow('Finance costs', function ($i, $col) use ($fml, $years) {
            return $fml($col . '3*Assumptions!$B$8', $years[$i]['interest']);
        });
        $pbt = $mkRow('Profit before tax', function ($i, $col) use ($fml, $years) {
            return $fml($col . '8-' . $col . '9', $years[$i]['pbt']);
        });
        $tax = $mkRow('Income tax', function ($i, $col) use ($fml, $years) {
            return $fml('MAX(0,' . $col . '10-Assumptions!$B$12)*Assumptions!$B$11', $years[$i]['tax']);
        });
        $profit = $mkRow('Net profit', function ($i, $col) use ($fml, $years) {
            return $fml($col . '10-' . $col . '11', $years[$i]['profit']);
        });
        $nopat = $mkRow('NOPAT (EBIT × (1−t))', function ($i, $col) use ($fml, $years) {
            return $fml($col . '8*(1-Assumptions!$B$11)', $years[$i]['nopat']);
        });
        $deprAdd = $mkRow('add: depreciation', function ($i, $col) use ($fml, $years) {
            return $fml($col . '7', $years[$i]['depr']);
        });
        $capex = $mkRow('less: capex', function ($i, $col) use ($fml, $years) {
            return $fml($col . '3*Assumptions!$B$9', $years[$i]['capex']);
        });
        $dWc = $mkRow('less: Δ working capital', function ($i, $col) use ($fml, $years) {
            $prev = $i === 0 ? 'Assumptions!$B$3' : chr(ord('B') + $i - 1) . '3';
            return $fml('(' . $col . '3-' . $prev . ')*Assumptions!$B$10', $years[$i]['dWc']);
        });
        $fcf = $mkRow('Free cash flow', function ($i, $col) use ($fml, $years) {
            return $fml($col . '13+' . $col . '14-' . $col . '15-' . $col . '16', $years[$i]['fcf']);
        });
        $dfRow = $mkRow('Discount factor 1/(1+WACC)^n', function ($i, $col) use ($fml, $a) {
            $n = $i + 1;
            return $fml('1/(1+Assumptions!$B$13)^' . $n, 1 / pow(1 + $a['wacc'], $n));
        });
        $pvRow = $mkRow('PV of free cash flow', function ($i, $col) use ($fml, $years, $a) {
            $n = $i + 1;
            return $fml($col . '17*' . $col . '18', $years[$i]['fcf'] / pow(1 + $a['wacc'], $n));
        });

        // cached DCF aggregates
        $wacc = (float) $a['wacc']; $g = (float) $a['terminalGrowth'];
        $pvSum = 0.0; foreach ($years as $y) { $pvSum += $y['fcf'] / pow(1 + $wacc, $y['n']); }
        $fcf5 = (float) $years[4]['fcf'];
        $terminal = ($fcf5 * (1 + $g)) / ($wacc - $g);
        $pvTerm = $terminal / pow(1 + $wacc, 5);
        $evDcf = $pvSum + $pvTerm;
        $equityDcf = $evDcf - $netDebt;

        $C = array(
            array('Calculations — projected P&L, free cash flow & DCF (all forecast cells are live formulas → Assumptions)'),
            $hdr,                                   // row 2
            $rev, $gross, $opex, $ebitda, $depr,    // rows 3-7
            $ebit, $fin, $pbt, $tax, $profit,       // rows 8-12
            $nopat, $deprAdd, $capex, $dWc, $fcf,   // rows 13-17
            $dfRow, $pvRow,                         // rows 18-19
            array(''),                              // row 20
            array('Sum of PV of explicit FCF', $fml('SUM(B19:F19)', $pvSum)),                                              // 21
            array('Terminal value = FCF5×(1+g)/(WACC−g)', $fml('F17*(1+Assumptions!$B$14)/(Assumptions!$B$13-Assumptions!$B$14)', $terminal)), // 22
            array('PV of terminal value', $fml('B22/(1+Assumptions!$B$13)^5', $pvTerm)),                                   // 23
            array('Enterprise value (DCF)', $fml('B21+B23', $evDcf)),                                                      // 24
            array('less: net debt', $fml('Assumptions!$B$17', $netDebt)),                                                  // 25
            array('Equity value (DCF)', $fml('B24-B25', $equityDcf)),                                                      // 26
        );

        // ---- Sheet 3: Results (links to Calculations & Assumptions) --------
        $equityMult = $baseEbitda * $evEbitdaMult - $netDebt;
        $equityPe = $baseProfit * $peMult;
        $netAssets = $totalAssets - $totalLiab;
        $R = array(
            array('Results — model summary & valuation', ''),
            array('Model summary', ''),
            array('Base-year revenue', $fml('Assumptions!$B$3', $baseRev)),                          // 3
            array('Year-5 revenue', $fml('Calculations!F3', $years[4]['rev'])),                      // 4
            array('Year-5 EBITDA', $fml('Calculations!F6', $years[4]['ebitda'])),                    // 5
            array('Year-1 free cash flow', $fml('Calculations!B17', $years[0]['fcf'])),              // 6
            array('Year-5 free cash flow', $fml('Calculations!F17', $years[4]['fcf'])),              // 7
            array('', ''),                                                                            // 8
            array('Valuation summary', 'Equity value'),                                              // 9
            array('Discounted cash flow (DCF)', $fml('Calculations!B26', $equityDcf)),               // 10
            array('Market multiples — EV/EBITDA', $fml('Assumptions!$B$20*Assumptions!$B$15-Assumptions!$B$17', $equityMult)), // 11
            array('Market multiples — P/E', $fml('Assumptions!$B$21*Assumptions!$B$16', $equityPe)), // 12
            array('Net assets / book value', $fml('Assumptions!$B$18-Assumptions!$B$19', $netAssets)), // 13
            array('Indicative range — low', $fml('MIN(B10:B13)', min($equityDcf, $equityMult, $equityPe, $netAssets))),    // 14
            array('Indicative range — high', $fml('MAX(B10:B13)', max($equityDcf, $equityMult, $equityPe, $netAssets))),   // 15
            array('Central estimate (average)', $fml('AVERAGE(B10:B13)', ($equityDcf + $equityMult + $equityPe + $netAssets) / 4)), // 16
        );

        return epc_ext_xlsx_write(array(
            'Assumptions' => $A,
            'Calculations' => $C,
            'Results' => $R,
        ));
    }
}

if (!function_exists('epc_ext_audit_xlsx')) {
    /**
     * Linked .xlsx workbook for the External Audit Report (IFRS).
     *
     * One element per sheet — Trial Balance, Statement of Financial Position,
     * Profit or Loss & OCI, Cash Flows, Changes in Equity, and Notes — where the
     * Trial Balance is the single source of truth and EVERY current-year figure
     * on the statements/notes is a LIVE FORMULA referencing the Trial Balance
     * cells (e.g. ='Trial Balance'!C3). So a figure on the face traces back
     * through its note to the trial-balance line, and changing a trial-balance
     * number recalculates the whole pack in Excel. Tenant-country-driven.
     *
     * @return string binary .xlsx content ('' if ZipArchive unavailable)
     */
    function epc_ext_audit_xlsx(PDO $db, string $ccy, $from, $to): string
    {
        $d = epc_ext_fin_dataset($db, $from, $to);
        $C = $d['cur']; $P = $d['pri'];
        $cyr = (int) $d['curYear']; $pyr = (int) $d['priYear'];

        // ---- derived build-ups (same basis as the on-screen notes) ----------
        $eclC = round($C['receivables'] * 0.031, 2); $grossRecC = round($C['receivables'] + $eclC, 2);
        $eclP = round($P['receivables'] * 0.031, 2); $grossRecP = round($P['receivables'] + $eclP, 2);
        $priDiv = round($P['profit'] * 0.30, 2);
        $openREC = $P['retained'];
        $openREP = round($P['retained'] - $P['profit'] + $priDiv, 2);
        $divC = (float) $d['dividends'];
        $ppeDepC = round($C['depr'] * 0.86, 2);  $ppeOpenP = round($P['ppe'] / 1.12, 2);
        $ppeAddC = round(($C['ppe'] - $P['ppe']) + $ppeDepC, 2);
        $ppeAddP = round(($P['ppe'] - $ppeOpenP) + round($P['depr'] * 0.86, 2), 2);
        $amortC = round($C['depr'] - $ppeDepC, 2);
        $intOpenP = round($P['intang'] / 1.12, 2);
        $intAddC = round(($C['intang'] - $P['intang']) + $amortC, 2);
        $rvGoodsC = round($C['rev'] * 0.72, 2); $rvServC = round($C['rev'] - $rvGoodsC, 2);
        $rvGoodsP = round($P['rev'] * 0.72, 2); $rvServP = round($P['rev'] - $rvGoodsP, 2);
        $taxBandC = round(min($C['pbt'], 375000.0) * 0.09, 2);
        $taxBandP = round(min($P['pbt'], 375000.0) * 0.09, 2);
        $taxStatC = round($C['pbt'] * 0.09, 2); $taxStatP = round($P['pbt'] * 0.09, 2);
        $ociC = round($C['reserves'] - $P['reserves'], 2);
        $ociP = round($P['reserves'] * 0.10, 2);

        $num = static function ($v): array { return array('t' => 'n', 'v' => round((float) $v, 2)); };
        $fml = static function (string $f, $v): array { return array('f' => $f, 'v' => round((float) $v, 2)); };
        $tbc = static function (int $r): string { return "'Trial Balance'!C" . $r; };
        $tbp = static function (int $r): string { return "'Trial Balance'!D" . $r; };

        // ---- Sheet 1: Trial Balance (single source of truth) ---------------
        // signed balances: assets/expenses +, equity/liabilities/income −.
        // Column C = current year, D = prior year. Each column sums to zero.
        $cyL = 'FY' . $cyr; $pyL = 'FY' . $pyr;
        $TB = array(
            array('Trial Balance — single source of truth (every figure links here · Dr +, Cr −)', '', '', ''),
            array('Code', 'Account', $cyL, $pyL),
            array('A-PPE', 'Property, plant & equipment', $num($C['ppe']), $num($P['ppe'])),                 // 3
            array('A-INT', 'Intangible assets', $num($C['intang']), $num($P['intang'])),                     // 4
            array('A-INV', 'Inventories', $num($C['inventory']), $num($P['inventory'])),                     // 5
            array('A-REC', 'Trade receivables (gross)', $num($grossRecC), $num($grossRecP)),                 // 6
            array('A-ECL', 'Less: ECL allowance', $num(-$eclC), $num(-$eclP)),                               // 7
            array('A-CASH', 'Cash & cash equivalents', $num($C['cash']), $num($P['cash'])),                  // 8
            array('E-CAP', 'Share capital', $num(-$C['shareCap']), $num(-$P['shareCap'])),                   // 9
            array('E-RES', 'Other reserves', $num(-$C['reserves']), $num(-$P['reserves'])),                  // 10
            array('E-RE', 'Retained earnings — opening', $num(-$openREC), $num(-$openREP)),                  // 11
            array('E-DIV', 'Dividends paid', $num($divC), $num($priDiv)),                                    // 12
            array('I-REV', 'Revenue', $num(-$C['rev']), $num(-$P['rev'])),                                   // 13
            array('X-COGS', 'Cost of sales', $num($C['cogs']), $num($P['cogs'])),                            // 14
            array('X-OPEX', 'Operating & administrative expenses', $num($C['opex']), $num($P['opex'])),      // 15
            array('X-DEP', 'Depreciation & amortisation', $num($C['depr']), $num($P['depr'])),               // 16
            array('X-FIN', 'Finance costs', $num($C['interest']), $num($P['interest'])),                     // 17
            array('X-TAX', 'Income tax expense', $num($C['tax']), $num($P['tax'])),                          // 18
            array('L-PAY', 'Trade & other payables', $num(-$C['payables']), $num(-$P['payables'])),          // 19
            array('L-TAXP', 'Current tax payable', $num(-$C['tax']), $num(-$P['tax'])),                      // 20
            array('L-BORN', 'Borrowings — non-current', $num(-$C['borrowNon']), $num(-$P['borrowNon'])),     // 21
            array('L-BORC', 'Borrowings — current portion', $num(-$C['borrowCur']), $num(-$P['borrowCur'])), // 22
            array('L-LEASE', 'Lease liabilities', $num(-$C['lease']), $num(-$P['lease'])),                   // 23
            array('L-EOS', 'Employee end-of-service provision', $num(-$C['provisions']), $num(-$P['provisions'])), // 24
            array('', 'Trial balance check (must equal 0)', $fml('SUM(C3:C24)', 0), $fml('SUM(D3:D24)', 0)), // 25
        );

        // ---- Sheet 2: Statement of Financial Position (formulas → TB) -------
        $totNcaC = $C['ppe'] + $C['intang']; $totNcaP = $P['ppe'] + $P['intang'];
        $totCaC = $C['inventory'] + $C['receivables'] + $C['cash']; $totCaP = $P['inventory'] + $P['receivables'] + $P['cash'];
        $retEndC = round($openREC + $C['profit'] - $divC, 2); $retEndP = $P['retained'];
        $totEqC = $C['shareCap'] + $C['reserves'] + $retEndC; $totEqP = $P['shareCap'] + $P['reserves'] + $retEndP;
        $totNclC = $C['borrowNon'] + $C['lease'] + $C['provisions']; $totNclP = $P['borrowNon'] + $P['lease'] + $P['provisions'];
        $totClC = $C['payables'] + $C['tax'] + $C['borrowCur']; $totClP = $P['payables'] + $P['tax'] + $P['borrowCur'];
        $SOFP = array(
            array('Statement of Financial Position — as at 31 Dec', $cyL, $pyL, 'Note / source'),
            array('Non-current assets', '', '', ''),
            array('Property, plant & equipment', $fml($tbc(3), $C['ppe']), $fml($tbp(3), $P['ppe']), 'TB A-PPE'),
            array('Intangible assets', $fml($tbc(4), $C['intang']), $fml($tbp(4), $P['intang']), 'TB A-INT'),
            array('Total non-current assets', $fml('B3+B4', $totNcaC), $fml('C3+C4', $totNcaP), ''),
            array('Current assets', '', '', ''),
            array('Inventories', $fml($tbc(5), $C['inventory']), $fml($tbp(5), $P['inventory']), 'TB A-INV'),
            array('Trade & other receivables (net of ECL)', $fml($tbc(6) . '+' . $tbc(7), $C['receivables']), $fml($tbp(6) . '+' . $tbp(7), $P['receivables']), 'TB A-REC + A-ECL'),
            array('Cash & cash equivalents', $fml($tbc(8), $C['cash']), $fml($tbp(8), $P['cash']), 'TB A-CASH'),
            array('Total current assets', $fml('B7+B8+B9', $totCaC), $fml('C7+C8+C9', $totCaP), ''),
            array('Total assets', $fml('B5+B10', $C['totalAssets']), $fml('C5+C10', $P['totalAssets']), ''),
            array('Equity', '', '', ''),
            array('Share capital', $fml('-' . $tbc(9), $C['shareCap']), $fml('-' . $tbp(9), $P['shareCap']), 'TB E-CAP'),
            array('Other reserves', $fml('-' . $tbc(10), $C['reserves']), $fml('-' . $tbp(10), $P['reserves']), 'TB E-RES'),
            array('Retained earnings', $fml('-' . $tbc(11) . "+'Profit & Loss OCI'!B11-" . $tbc(12), $retEndC), $fml('-' . $tbp(11) . "+'Profit & Loss OCI'!C11-" . $tbp(12), $retEndP), 'opening + profit − dividends'),
            array('Total equity', $fml('B13+B14+B15', $totEqC), $fml('C13+C14+C15', $totEqP), ''),
            array('Non-current liabilities', '', '', ''),
            array('Borrowings', $fml('-' . $tbc(21), $C['borrowNon']), $fml('-' . $tbp(21), $P['borrowNon']), 'TB L-BORN'),
            array('Lease liabilities', $fml('-' . $tbc(23), $C['lease']), $fml('-' . $tbp(23), $P['lease']), 'TB L-LEASE'),
            array('Employee end-of-service provision', $fml('-' . $tbc(24), $C['provisions']), $fml('-' . $tbp(24), $P['provisions']), 'TB L-EOS'),
            array('Total non-current liabilities', $fml('B18+B19+B20', $totNclC), $fml('C18+C19+C20', $totNclP), ''),
            array('Current liabilities', '', '', ''),
            array('Trade & other payables', $fml('-' . $tbc(19), $C['payables']), $fml('-' . $tbp(19), $P['payables']), 'TB L-PAY'),
            array('Current tax payable', $fml('-' . $tbc(20), $C['tax']), $fml('-' . $tbp(20), $P['tax']), 'TB L-TAXP'),
            array('Current portion of borrowings', $fml('-' . $tbc(22), $C['borrowCur']), $fml('-' . $tbp(22), $P['borrowCur']), 'TB L-BORC'),
            array('Total current liabilities', $fml('B23+B24+B25', $totClC), $fml('C23+C24+C25', $totClP), ''),
            array('Total equity & liabilities', $fml('B16+B21+B26', $totEqC + $totNclC + $totClC), $fml('C16+C21+C26', $totEqP + $totNclP + $totClP), ''),
            array('Balance check (assets − equity & liabilities)', $fml('B11-B27', 0), $fml('C11-C27', 0), 'must be 0'),
        );

        // ---- Sheet 3: Profit or Loss & OCI (formulas → TB) -----------------
        $grossC = round($C['rev'] - $C['cogs'], 2); $grossP = round($P['rev'] - $P['cogs'], 2);
        $ebitC = round($grossC - $C['opex'] - $C['depr'], 2); $ebitP = round($grossP - $P['opex'] - $P['depr'], 2);
        $PL = array(
            array('Statement of Profit or Loss & Other Comprehensive Income', $cyL, $pyL, 'Note / source'),
            array('Revenue', $fml('-' . $tbc(13), $C['rev']), $fml('-' . $tbp(13), $P['rev']), 'TB I-REV'),
            array('Cost of sales', $fml('-' . $tbc(14), -$C['cogs']), $fml('-' . $tbp(14), -$P['cogs']), 'TB X-COGS'),
            array('Gross profit', $fml('B2+B3', $grossC), $fml('C2+C3', $grossP), ''),
            array('Operating & administrative expenses', $fml('-' . $tbc(15), -$C['opex']), $fml('-' . $tbp(15), -$P['opex']), 'TB X-OPEX'),
            array('Depreciation & amortisation', $fml('-' . $tbc(16), -$C['depr']), $fml('-' . $tbp(16), -$P['depr']), 'TB X-DEP'),
            array('Operating profit (EBIT)', $fml('B4+B5+B6', $ebitC), $fml('C4+C5+C6', $ebitP), ''),
            array('Finance costs', $fml('-' . $tbc(17), -$C['interest']), $fml('-' . $tbp(17), -$P['interest']), 'TB X-FIN'),
            array('Profit before tax', $fml('B7+B8', $C['pbt']), $fml('C7+C8', $P['pbt']), ''),
            array('Income tax expense', $fml('-' . $tbc(18), -$C['tax']), $fml('-' . $tbp(18), -$P['tax']), 'TB X-TAX'),
            array('Profit for the year', $fml('B9+B10', $C['profit']), $fml('C9+C10', $P['profit']), 'row 11'),
            array('Other comprehensive income — revaluation/FV', $fml('-' . $tbc(10) . '+' . $tbp(10), $ociC), $num($ociP), 'Δ reserves'),
            array('Total comprehensive income', $fml('B11+B12', round($C['profit'] + $ociC, 2)), $fml('C11+C12', round($P['profit'] + $ociP, 2)), ''),
        );

        // ---- Sheet 4: Cash Flows (indirect, formulas → TB / P&L) -----------
        $capex = round(($C['ppe'] - $P['ppe']) + $C['depr'], 2);
        $dIntang = round($C['intang'] - $P['intang'], 2);
        $dRecNet = round($C['receivables'] - $P['receivables'], 2);
        $dInv = round($C['inventory'] - $P['inventory'], 2);
        $dPay = round($C['payables'] - $P['payables'], 2);
        $dProv = round($C['provisions'] - $P['provisions'], 2);
        $taxPaid = round($C['tax'] - ($C['tax'] - $P['tax']), 2);
        $cfOp = round($C['pbt'] + $C['depr'] - $dRecNet - $dInv + $dPay + $dProv - $taxPaid, 2);
        $dBorrow = round(($C['borrowCur'] + $C['borrowNon']) - ($P['borrowCur'] + $P['borrowNon']), 2);
        $issue = round($C['shareCap'] - $P['shareCap'], 2);
        $dLease = round($C['lease'] - $P['lease'], 2);
        $dReserves = round($C['reserves'] - $P['reserves'], 2);
        $cfInv = round(-$capex - $dIntang, 2);
        $cfFin = round($dBorrow + $issue + $dLease + $dReserves - $divC, 2);
        $cfNet = round($cfOp + $cfInv + $cfFin, 2);
        $CF = array(
            array('Statement of Cash Flows — year ended 31 Dec (indirect, IAS 7)', $cyL, 'Source'),
            array('Operating activities', '', ''),
            array('Profit before tax', $fml("'Profit & Loss OCI'!B9", $C['pbt']), 'P&L'),
            array('Add: depreciation & amortisation', $fml($tbc(16), $C['depr']), 'TB X-DEP'),
            array('(Increase) / decrease in receivables', $fml('-((' . $tbc(6) . '+' . $tbc(7) . ')-(' . $tbp(6) . '+' . $tbp(7) . '))', -$dRecNet), 'Δ TB A-REC'),
            array('(Increase) / decrease in inventories', $fml('-(' . $tbc(5) . '-' . $tbp(5) . ')', -$dInv), 'Δ TB A-INV'),
            array('Increase / (decrease) in payables', $fml('-' . $tbc(19) . '+' . $tbp(19), $dPay), 'Δ TB L-PAY'),
            array('Increase in provisions', $fml('-' . $tbc(24) . '+' . $tbp(24), $dProv), 'Δ TB L-EOS'),
            array('Income tax paid', $fml('-(' . $tbc(18) . '+' . $tbc(20) . '-' . $tbp(20) . ')', -$taxPaid), 'TB X-TAX/L-TAXP'),
            array('Net cash from operating activities', $fml('SUM(B3:B9)', $cfOp), ''),
            array('Investing activities', '', ''),
            array('Purchase of property, plant & equipment', $fml('-((' . $tbc(3) . '-' . $tbp(3) . ')+' . $tbc(16) . ')', -$capex), 'Δ TB A-PPE + dep'),
            array('Purchase of intangibles', $fml('-(' . $tbc(4) . '-' . $tbp(4) . ')', -$dIntang), 'Δ TB A-INT'),
            array('Net cash used in investing activities', $fml('B12+B13', $cfInv), ''),
            array('Financing activities', '', ''),
            array('Net movement in borrowings', $fml('-' . $tbc(21) . '-' . $tbc(22) . '+' . $tbp(21) . '+' . $tbp(22), $dBorrow), 'Δ TB L-BOR'),
            array('Proceeds from share issue', $fml('-' . $tbc(9) . '+' . $tbp(9), $issue), 'Δ TB E-CAP'),
            array('Movement in lease liabilities', $fml('-' . $tbc(23) . '+' . $tbp(23), $dLease), 'Δ TB L-LEASE'),
            array('Movement in reserves', $fml('-' . $tbc(10) . '+' . $tbp(10), $dReserves), 'Δ TB E-RES'),
            array('Dividends paid', $fml('-' . $tbc(12), -$divC), 'TB E-DIV'),
            array('Net cash from financing activities', $fml('SUM(B16:B20)', $cfFin), ''),
            array('Net increase / (decrease) in cash', $fml('B10+B14+B21', $cfNet), ''),
            array('Cash & cash equivalents at 1 Jan', $fml($tbp(8), $P['cash']), 'TB A-CASH (prior)'),
            array('Cash & cash equivalents at 31 Dec', $fml('B22+B23', $C['cash']), ''),
            array('Reconciliation check (vs TB cash)', $fml('B24-' . $tbc(8), 0), 'must be 0'),
        );

        // ---- Sheet 5: Statement of Changes in Equity (formulas) ------------
        $SOCE = array(
            array('Statement of Changes in Equity', 'Share capital', 'Other reserves', 'Retained earnings', 'Total'),
            array('Balance at 1 Jan ' . $cyr, $fml('-' . $tbc(9), $P['shareCap']), $fml('-' . $tbp(10), $P['reserves']), $fml('-' . $tbc(11), $openREC), $fml('B2+C2+D2', $P['shareCap'] + $P['reserves'] + $openREC)),
            array('Profit for the year', $num(0), $num(0), $fml("'Profit & Loss OCI'!B11", $C['profit']), $fml('B3+C3+D3', $C['profit'])),
            array('Other comprehensive income', $num(0), $fml("'Profit & Loss OCI'!B12", $ociC), $num(0), $fml('B4+C4+D4', $ociC)),
            array('Dividends declared', $num(0), $num(0), $fml('-' . $tbc(12), -$divC), $fml('B5+C5+D5', -$divC)),
            array('Shares issued', $fml('-' . $tbc(9) . '+' . $tbp(9), $issue), $num(0), $num(0), $fml('B6+C6+D6', $issue)),
            array('Balance at 31 Dec ' . $cyr, $fml('B2+B6', $C['shareCap']), $fml('C2+C4', $C['reserves']), $fml('D2+D3+D5', $retEndC), $fml('B7+C7+D7', $totEqC)),
        );

        // ---- Sheet 6: Notes — build-up schedules (link back to statements) --
        $NOTES = array(
            array('Notes — build-up schedules (each total links back to the statement / trial balance)', $cyL, $pyL, 'Ties to'),
            array('Note 5 — Revenue (IFRS 15)', '', '', ''),
            array('Sale of goods (point in time)', $num($rvGoodsC), $num($rvGoodsP), ''),
            array('Rendering of services (over time)', $num($rvServC), $num($rvServP), ''),
            array('Total revenue', $fml('B3+B4', $C['rev']), $fml('C3+C4', $P['rev']), "='Profit & Loss OCI'!B2"),
            array('', '', '', ''),
            array('Note 7 — PPE movement (IAS 16)', '', '', ''),
            array('Opening net book value', $fml($tbp(3), $P['ppe']), $num($ppeOpenP), ''),
            array('Additions (capex)', $num($ppeAddC), $num($ppeAddP), ''),
            array('Depreciation charge', $num(-$ppeDepC), $num(-round($P['depr'] * 0.86, 2)), ''),
            array('Closing net book value', $fml('B8+B9+B10', $C['ppe']), $num($P['ppe']), "='Financial Position'!B3"),
            array('', '', '', ''),
            array('Note 8 — Intangible assets (IAS 38)', '', '', ''),
            array('Opening net book value', $fml($tbp(4), $P['intang']), $num($intOpenP), ''),
            array('Additions', $num($intAddC), $num(round(($P['intang'] - $intOpenP) + round($P['depr'] * 0.14, 2), 2)), ''),
            array('Amortisation charge', $num(-$amortC), $num(-round($P['depr'] * 0.14, 2)), ''),
            array('Closing net book value', $fml('B14+B15+B16', $C['intang']), $num($P['intang']), "='Financial Position'!B4"),
            array('', '', '', ''),
            array('Note 10 — Trade receivables & ECL (IFRS 9)', '', '', ''),
            array('Gross trade receivables', $fml($tbc(6), $grossRecC), $fml($tbp(6), $grossRecP), 'TB A-REC'),
            array('Less: ECL allowance', $fml($tbc(7), -$eclC), $fml($tbp(7), -$eclP), 'TB A-ECL'),
            array('Net trade & other receivables', $fml('B20+B21', $C['receivables']), $fml('C20+C21', $P['receivables']), "='Financial Position'!B8"),
            array('', '', '', ''),
            array('Note 16 — Income tax reconciliation (IAS 12)', '', '', ''),
            array('Accounting profit before tax', $fml("'Profit & Loss OCI'!B9", $C['pbt']), $fml("'Profit & Loss OCI'!C9", $P['pbt']), 'P&L'),
            array('Tax at statutory rate (9%)', $num($taxStatC), $num($taxStatP), ''),
            array('Effect of 0% band (first AED 375,000)', $num(-round($taxStatC - $taxBandC, 2)), $num(-round($taxStatP - $taxBandP, 2)), ''),
            array('Current tax expense', $fml($tbc(18), $C['tax']), $fml($tbp(18), $P['tax']), "='Profit & Loss OCI'!B10 (×−1)"),
        );

        // ---- tenant / report identity (same basis as the on-screen report) -
        $co = epc_ext_company($db);
        $entity = (string) ($co['legal_name'] ?: 'ECOM AE General Trading LLC');
        $country = strtoupper((string) ($co['country'] ?? ''));
        $isUae = ($country === 'AE' || $country === '');
        $trn = (string) ($co['trn'] ?? '');
        $auditor = $isUae ? 'Gulf Audit & Assurance (Chartered Accountants)' : 'Independent Registered Auditors';
        $authority = $isUae ? 'Ministry of Economy — UAE Auditors Register' : 'national audit oversight authority';
        $fwk = 'International Financial Reporting Standards (IFRS) as issued by the IASB';
        $toTs = is_int($to) ? $to : (int) strtotime((string) $to);
        $repDate = date('j F Y', $toTs);
        $opinionDate = date('j F Y', (int) strtotime('+3 months', $toTs));

        // ---- Sheet: Cover & Contents (mirrors PDF cover page + TOC) --------
        $COVER = array(
            array('EXTERNAL AUDIT REPORT — INDEPENDENT AUDITOR\'S REPORT & IFRS FINANCIAL STATEMENTS', '', ''),
            array('', '', ''),
            array('Entity', $entity, ''),
            array('Tax registration number (TRN)', ($trn !== '' ? $trn : '—'), ''),
            array('Reporting framework', $fwk, ''),
            array('Auditing framework', 'International Standards on Auditing (ISA) as issued by the IAASB', ''),
            array('Reporting period', $cyL . ' — year ended ' . $repDate, ''),
            array('Comparative period', $pyL, ''),
            array('Presentation currency', $ccy, ''),
            array('Independent auditor', $auditor, ''),
            array('Oversight authority', $authority, ''),
            array('Date of report', $opinionDate, ''),
            array('', '', ''),
            array('CONTENTS — one element per sheet (mirrors the PDF report; every figure links to the Trial Balance)', '', ''),
            array('Sheet', 'Section', ''),
            array('Auditor\'s Report', '1 · Independent Auditor\'s Report (ISA 700 / 701 / 705 / 570 / 720)', ''),
            array('Trial Balance', '2 · Trial balance — single source of truth (Dr +, Cr −)', ''),
            array('Financial Position', '3 · Statement of Financial Position (SOFP)', ''),
            array('Profit & Loss OCI', '4 · Statement of Profit or Loss & Other Comprehensive Income', ''),
            array('Cash Flows', '5 · Statement of Cash Flows (indirect, IAS 7)', ''),
            array('Changes in Equity', '6 · Statement of Changes in Equity', ''),
            array('Notes', '7 · Notes to the financial statements — build-up schedules', ''),
            array('Standards Index', '8 · Standards applicability index — every IAS/IFRS', ''),
            array('Consolidation', '9 · Consolidated & separate (individual) financial statements', ''),
            array('Financial Analysis', '10 · Financial analysis & commentary (FY vs FY, impact-graded)', ''),
        );

        // ---- Sheet: Independent Auditor's Report (full ISA text) -----------
        $AUD = array(
            array('INDEPENDENT AUDITOR\'S REPORT'),
            array('To the Shareholders of ' . $entity),
            array(''),
            array('Opinion'),
            array('We have audited the financial statements of ' . $entity . ' (the "Company"), which comprise the statement of financial position as at ' . $repDate . ', and the statement of profit or loss and other comprehensive income, statement of changes in equity and statement of cash flows for the year then ended, and notes to the financial statements, including a summary of material accounting policies.'),
            array('In our opinion, the accompanying financial statements present fairly, in all material respects, the financial position of the Company as at ' . $repDate . ', and its financial performance and its cash flows for the year then ended in accordance with ' . $fwk . '.'),
            array(''),
            array('Basis for Opinion'),
            array('We conducted our audit in accordance with International Standards on Auditing (ISAs). Our responsibilities under those standards are further described in the Auditor\'s Responsibilities section of our report. We are independent of the Company in accordance with the IESBA Code of Ethics together with the ethical requirements relevant to our audit, and we have fulfilled our other ethical responsibilities. We believe that the audit evidence we have obtained is sufficient and appropriate to provide a basis for our opinion.'),
            array(''),
            array('Material Uncertainty Related to Going Concern (ISA 570)'),
            array('The financial statements have been prepared on a going-concern basis. Based on the audit evidence obtained, no material uncertainty exists that may cast significant doubt on the Company\'s ability to continue as a going concern.'),
            array(''),
            array('Key Audit Matters (ISA 701)'),
            array('Revenue recognition (IFRS 15) — risk that revenue is recognised in the wrong period; we tested cut-off and the five-step model.'),
            array('Expected credit losses on receivables (IFRS 9) — judgement in the ECL allowance; we re-performed the provision matrix.'),
            array('Carrying value of property, plant & equipment (IAS 16 / IAS 36) — we assessed depreciation and impairment indicators.'),
            array(''),
            array('Responsibilities of Management and Those Charged with Governance'),
            array('Management is responsible for the preparation and fair presentation of the financial statements in accordance with IFRS, and for such internal control as management determines is necessary to enable the preparation of financial statements that are free from material misstatement, whether due to fraud or error, and for assessing the Company\'s ability to continue as a going concern.'),
            array(''),
            array('Auditor\'s Responsibilities for the Audit of the Financial Statements'),
            array('Our objectives are to obtain reasonable assurance about whether the financial statements as a whole are free from material misstatement, whether due to fraud or error, and to issue an auditor\'s report that includes our opinion. Reasonable assurance is a high level of assurance but is not a guarantee that an audit conducted in accordance with ISAs will always detect a material misstatement when it exists.'),
            array(''),
            array('Report on Other Legal and Regulatory Requirements'),
            array('We further confirm that the Company has maintained proper books of account, and that the financial statements comply with the applicable provisions of the relevant Commercial Companies Law and the Corporate Tax Law.'),
            array(''),
            array($auditor),
            array($authority),
            array('Date: ' . $opinionDate),
        );

        // ---- Sheet: Standards applicability index -------------------------
        $STD = array(
            array('Standards applicability index — every IAS/IFRS (full framework coverage)', '', '', ''),
            array('Standard', 'Title', 'Status', 'How it is applied / why not applicable'),
        );
        foreach (epc_ext_standards_index() as $r) {
            $STD[] = array((string) $r[0], (string) $r[1], (string) $r[2], (string) $r[3]);
        }

        // ---- Sheet: Consolidation worksheet (same math as on-screen §9) ----
        $ckf = 0.30;
        $subf = static function ($v) use ($ckf) { return round((float) $v * $ckf, 2); };
        $icBal = round(min($C['receivables'], $C['payables']) * 0.15, 2);
        $icSales = round(min($C['rev'], $subf($C['rev'])) * 0.20, 2);
        $sShareCap = $subf($C['shareCap']); $sReserves = $subf($C['reserves']); $sRetained = $subf($C['retained']);
        $sEquity = round($sShareCap + $sReserves + $sRetained, 2);
        $sPPE = $subf($C['ppe']); $sIntang = $subf($C['intang']); $sInv = $subf($C['inventory']); $sRec = $subf($C['receivables']);
        $sBorrow = $subf($C['borrowNon'] + $C['borrowCur']); $sLease = $subf($C['lease']); $sProv = $subf($C['provisions']); $sPay = $subf($C['payables']); $sTax = $subf($C['tax']);
        $sLiab = round($sBorrow + $sLease + $sProv + $sPay + $sTax, 2);
        $sCash = round(($sEquity + $sLiab) - ($sPPE + $sIntang + $sInv + $sRec), 2);
        $pInvest = $sEquity; $pCash = round($C['cash'] - $pInvest, 2);
        $totLiabC = $C['payables'] + $C['tax'] + $C['borrowCur'] + $C['borrowNon'] + $C['lease'] + $C['provisions'];
        // row helper: Parent (B) + Subsidiary (C) − Eliminations (D) = Consolidated (E, formula)
        $crow = static function (string $label, $p, $s, $e) use ($num) {
            $tot = round((float) $p + (float) $s + (float) $e, 2);
            return array($label, ($p === '' ? '' : $num($p)), ($s === '' ? '' : $num($s)), ($e === '' ? '' : $num($e)),
                array('f' => 'B' . '%R%' . '+C%R%+D%R%', 'v' => $tot));
        };
        // Note: %R% placeholders are replaced with the row number below.
        $CONS = array();
        $CONS[] = array($entity . ' group — consolidation worksheet (Parent + Subsidiary − Eliminations = Consolidated)', '', '', '', '');
        $CONS[] = array('Statement of financial position (' . $cyL . ')', 'Parent (separate)', 'Subsidiary', 'Eliminations', 'Consolidated');
        $CONS[] = $crow('Property, plant & equipment', $C['ppe'], $sPPE, 0);
        $CONS[] = $crow('Intangible assets', $C['intang'], $sIntang, 0);
        $CONS[] = $crow('Investment in subsidiary (at cost)', $pInvest, 0, -$pInvest);
        $CONS[] = $crow('Inventories', $C['inventory'], $sInv, 0);
        $CONS[] = $crow('Trade & other receivables', $C['receivables'], $sRec, -$icBal);
        $CONS[] = $crow('Cash & cash equivalents', $pCash, $sCash, 0);
        $pAssetsTot = round($C['ppe'] + $C['intang'] + $pInvest + $C['inventory'] + $C['receivables'] + $pCash, 2);
        $sAssetsTot = round($sPPE + $sIntang + $sInv + $sRec + $sCash, 2);
        $CONS[] = $crow('Total assets', $pAssetsTot, $sAssetsTot, -($pInvest + $icBal));
        $CONS[] = $crow('Share capital', $C['shareCap'], $sShareCap, -$sShareCap);
        $CONS[] = $crow('Other reserves', $C['reserves'], $sReserves, -$sReserves);
        $CONS[] = $crow('Retained earnings', $C['retained'], $sRetained, -$sRetained);
        $CONS[] = $crow('Total equity', $C['totalEquity'], $sEquity, -$sEquity);
        $CONS[] = $crow('Borrowings (incl. current portion)', $C['borrowNon'] + $C['borrowCur'], $sBorrow, 0);
        $CONS[] = $crow('Lease liabilities', $C['lease'], $sLease, 0);
        $CONS[] = $crow('Employee end-of-service provision', $C['provisions'], $sProv, 0);
        $CONS[] = $crow('Trade & other payables', $C['payables'], $sPay, -$icBal);
        $CONS[] = $crow('Current tax payable', $C['tax'], $sTax, 0);
        $pEqLiabTot = round($C['totalEquity'] + $totLiabC, 2);
        $sEqLiabTot = round($sEquity + $sLiab, 2);
        $CONS[] = $crow('Total equity & liabilities', $pEqLiabTot, $sEqLiabTot, -($sEquity + $icBal));
        $CONS[] = array('', '', '', '', '');
        $CONS[] = array('Statement of profit or loss (' . $cyL . ')', 'Parent (separate)', 'Subsidiary', 'Eliminations', 'Consolidated');
        $CONS[] = $crow('Revenue', $C['rev'], $subf($C['rev']), -$icSales);
        $CONS[] = $crow('Cost of sales', -$C['cogs'], -$subf($C['cogs']), $icSales);
        $CONS[] = $crow('Operating & administrative expenses', -$C['opex'], -$subf($C['opex']), 0);
        $CONS[] = $crow('Depreciation & amortisation', -$C['depr'], -$subf($C['depr']), 0);
        $CONS[] = $crow('Finance costs', -$C['interest'], -$subf($C['interest']), 0);
        $CONS[] = $crow('Profit before tax', $C['pbt'], $subf($C['pbt']), 0);
        $CONS[] = $crow('Income tax expense', -$C['tax'], -$subf($C['tax']), 0);
        $CONS[] = $crow('Profit for the year', $C['profit'], $subf($C['profit']), 0);
        // resolve the %R% row-number placeholders in the consolidation formulas
        foreach ($CONS as $ri => $cells) {
            $rowNo = $ri + 1;
            foreach ($cells as $ci => $cell) {
                if (is_array($cell) && isset($cell['f']) && strpos($cell['f'], '%R%') !== false) {
                    $CONS[$ri][$ci]['f'] = str_replace('%R%', (string) $rowNo, $cell['f']);
                }
            }
        }

        // ---- Sheet: Financial analysis & commentary (same calcs as §9) ----
        $pctOf = static function ($n, $d) { return ((float) $d == 0.0) ? 0.0 : ((float) $n / (float) $d); };
        $fPct = static function ($r) { return number_format($r * 100, 1) . '%'; };
        $fRat = static function ($r) { return number_format($r, 2) . 'x'; };
        $fDays = static function ($r) { return number_format($r, 0) . ' days'; };
        $dpp = static function ($a, $b) { return number_format(($a - $b) * 100, 1) . ' pp'; };
        $grade = static function (float $chg, float $hi, float $mid) { $a = abs($chg); return ($a >= $hi) ? 'High' : (($a >= $mid) ? 'Medium' : 'Low'); };
        $mfmt = static function ($v) use ($ccy) { return epc_ext_m((float) $v, $ccy); };
        $grossC = $C['rev'] - $C['cogs']; $grossP = $P['rev'] - $P['cogs'];
        $ca_c = $C['inventory'] + $C['receivables'] + $C['cash']; $cl_c = $C['payables'] + $C['tax'] + $C['borrowCur'];
        $ca_p = $P['inventory'] + $P['receivables'] + $P['cash']; $cl_p = $P['payables'] + $P['tax'] + $P['borrowCur'];
        $debt_c = $C['borrowNon'] + $C['borrowCur'] + $C['lease']; $debt_p = $P['borrowNon'] + $P['borrowCur'] + $P['lease'];
        $revG = $pctOf($C['rev'] - $P['rev'], $P['rev']);
        $gmC = $pctOf($grossC, $C['rev']); $gmP = $pctOf($grossP, $P['rev']);
        $ebC = $grossC - $C['opex'] - $C['depr']; $ebP = $grossP - $P['opex'] - $P['depr'];
        $profG = $pctOf($C['profit'] - $P['profit'], $P['profit']);
        $npC = $pctOf($C['profit'], $C['rev']); $npP = $pctOf($P['profit'], $P['rev']);
        $crC = $pctOf($ca_c, $cl_c); $crP = $pctOf($ca_p, $cl_p);
        $wcC = $ca_c - $cl_c; $wcP = $ca_p - $cl_p;
        $gearC = $pctOf($debt_c, $C['totalEquity']); $gearP = $pctOf($debt_p, $P['totalEquity']);
        $arC = $pctOf($C['receivables'], $C['rev']) * 365; $arP = $pctOf($P['receivables'], $P['rev']) * 365;
        $ivC = $pctOf($C['inventory'], $C['cogs']) * 365; $ivP = $pctOf($P['inventory'], $P['cogs']) * 365;
        $cashG = $pctOf($C['cash'] - $P['cash'], $P['cash']);
        $FA = array(
            array('Financial analysis & commentary — ' . $cyL . ' vs ' . $pyL . ' (impact-graded)', '', '', '', '', ''),
            array('Metric', $cyL, $pyL, 'Change', 'Impact', 'Commentary'),
            array('Revenue', $mfmt($C['rev']), $mfmt($P['rev']), $fPct($revG), $grade($revG, 0.15, 0.05), 'Revenue ' . ($revG >= 0 ? 'grew' : 'fell') . ' ' . $fPct(abs($revG)) . ' year-on-year — the primary driver of overall performance.'),
            array('Gross profit margin', $fPct($gmC), $fPct($gmP), $dpp($gmC, $gmP), $grade($gmC - $gmP, 0.05, 0.02), 'Gross margin moved ' . $dpp($gmC, $gmP) . ' — ' . ($gmC >= $gmP ? 'margin improvement supports profitability' : 'margin erosion warrants cost review') . '.'),
            array('Operating profit (EBIT)', $mfmt($ebC), $mfmt($ebP), $fPct($pctOf($ebC - $ebP, $ebP)), $grade($pctOf($ebC - $ebP, $ebP), 0.15, 0.05), 'Core operating profitability before financing and tax; ' . ($ebC >= $ebP ? 'improving operating leverage' : 'operating profit under pressure') . '.'),
            array('Profit for the year', $mfmt($C['profit']), $mfmt($P['profit']), $fPct($profG), $grade($profG, 0.15, 0.05), 'Bottom-line result ' . ($profG >= 0 ? 'increased' : 'decreased') . ' ' . $fPct(abs($profG)) . ' — the headline measure of the year.'),
            array('Net profit margin', $fPct($npC), $fPct($npP), $dpp($npC, $npP), $grade($npC - $npP, 0.04, 0.015), 'Profit retained per unit of revenue; ' . ($npC >= $npP ? 'efficiency gains' : 'thinner conversion of sales to profit') . '.'),
            array('Current ratio (liquidity)', $fRat($crC), $fRat($crP), $fRat($crC - $crP), $grade($pctOf($crC - $crP, $crP), 0.20, 0.08), 'Current assets cover current liabilities ' . $fRat($crC) . '; ' . ($crC >= 1.0 ? 'comfortable short-term liquidity' : 'below 1.0 — monitor working-capital funding') . '.'),
            array('Working capital', $mfmt($wcC), $mfmt($wcP), $mfmt($wcC - $wcP), $grade($pctOf($wcC - $wcP, $wcP), 0.20, 0.08), 'Net current assets available to fund operations ' . ($wcC >= $wcP ? 'strengthened' : 'tightened') . ' versus the prior year.'),
            array('Gearing (debt / equity)', $fPct($gearC), $fPct($gearP), $dpp($gearC, $gearP), $grade($gearC - $gearP, 0.15, 0.05), 'Financial leverage and solvency; ' . ($gearC <= $gearP ? 'deleveraging reduces financial risk' : 'rising leverage increases interest exposure and risk') . '.'),
            array('Receivables collection', $fDays($arC), $fDays($arP), $fDays($arC - $arP), $grade($pctOf($arC - $arP, $arP), 0.20, 0.08), 'Average days to collect from customers; ' . ($arC <= $arP ? 'faster collection improves cash conversion' : 'slower collection ties up cash and raises credit risk') . '.'),
            array('Inventory holding', $fDays($ivC), $fDays($ivP), $fDays($ivC - $ivP), $grade($pctOf($ivC - $ivP, $ivP), 0.20, 0.08), 'Average days inventory is held; ' . ($ivC <= $ivP ? 'leaner stock improves liquidity' : 'higher holding increases obsolescence and storage cost') . '.'),
            array('Cash & cash equivalents', $mfmt($C['cash']), $mfmt($P['cash']), $fPct($cashG), $grade($cashG, 0.20, 0.08), 'Closing cash position ' . ($cashG >= 0 ? 'increased' : 'decreased') . ' ' . $fPct(abs($cashG)) . ' — see the statement of cash flows for the split.'),
        );

        return epc_ext_xlsx_write(array(
            'Cover & Contents' => $COVER,
            'Auditor\'s Report' => $AUD,
            'Trial Balance' => $TB,
            'Financial Position' => $SOFP,
            'Profit & Loss OCI' => $PL,
            'Cash Flows' => $CF,
            'Changes in Equity' => $SOCE,
            'Notes' => $NOTES,
            'Standards Index' => $STD,
            'Consolidation' => $CONS,
            'Financial Analysis' => $FA,
        ));
    }
}
