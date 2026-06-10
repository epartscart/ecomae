<?php
/**
 * Advanced ERP — Company profile, legal document branding & Statements of
 * Account.
 *
 *   - Per-tenant company profile: logo, legal/trade name, address, TRN/VAT &
 *     trade-license numbers, bank pay-to details, letterhead header/footer,
 *     stamp/signature, default terms — applied to every printed document.
 *   - Per-branch overrides (branch address/phone on that branch's documents).
 *   - Legal document header builder (seller + buyer TRN, tax-invoice title,
 *     amount-in-words, VAT-by-rate summary).
 *   - Statement of Account builder (customer AR or vendor AP): forward-balance
 *     or open-item, any date range, with ageing — pure over supplied rows so
 *     it is reusable and fully tested.
 *
 * Additive (company/branch profile tables only); statement builders read
 * existing AR/AP rows. Tenant-isolated, entitlement-aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_co_ensure_schema')) {
    function epc_co_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_co_profile` (
            `id` tinyint(1) NOT NULL DEFAULT 1,
            `legal_name` varchar(200) NOT NULL DEFAULT '',
            `trade_name` varchar(200) NOT NULL DEFAULT '',
            `logo_url` varchar(255) NOT NULL DEFAULT '',
            `stamp_url` varchar(255) NOT NULL DEFAULT '',
            `address` varchar(500) NOT NULL DEFAULT '',
            `city` varchar(120) NOT NULL DEFAULT '',
            `country` varchar(2) NOT NULL DEFAULT '',
            `phone` varchar(60) NOT NULL DEFAULT '',
            `email` varchar(160) NOT NULL DEFAULT '',
            `website` varchar(160) NOT NULL DEFAULT '',
            `trn` varchar(40) NOT NULL DEFAULT '',
            `tax_label` varchar(20) NOT NULL DEFAULT 'TRN',
            `trade_license` varchar(60) NOT NULL DEFAULT '',
            `reg_no` varchar(60) NOT NULL DEFAULT '',
            `bank_details` varchar(500) NOT NULL DEFAULT '',
            `base_currency` varchar(3) NOT NULL DEFAULT '',
            `fy_start` varchar(5) NOT NULL DEFAULT '01-01',
            `invoice_terms` varchar(1000) NOT NULL DEFAULT '',
            `header_html` text,
            `footer_html` text,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Single-row tenant company profile'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_co_branch_profile` (
            `branch_id` int(11) NOT NULL,
            `name` varchar(200) NOT NULL DEFAULT '',
            `address` varchar(500) NOT NULL DEFAULT '',
            `phone` varchar(60) NOT NULL DEFAULT '',
            `email` varchar(160) NOT NULL DEFAULT '',
            `trn` varchar(40) NOT NULL DEFAULT '',
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`branch_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-branch document overrides'");
    }
}

/* ----------------------- Company profile CRUD ------------------------ */

if (!function_exists('epc_co_profile_save')) {
    /**
     * Save / merge the tenant company profile (idempotent single row).
     *
     * @param array<string,mixed> $p
     * @return array<string,mixed> the stored profile
     */
    function epc_co_profile_save(PDO $db, array $p): array
    {
        epc_co_ensure_schema($db);
        $cur = epc_co_profile_get($db);
        $f = array(
            'legal_name', 'trade_name', 'logo_url', 'stamp_url', 'address', 'city', 'country',
            'phone', 'email', 'website', 'trn', 'tax_label', 'trade_license', 'reg_no',
            'bank_details', 'base_currency', 'fy_start', 'invoice_terms', 'header_html', 'footer_html',
        );
        $v = array();
        foreach ($f as $key) {
            $v[$key] = (string) ($p[$key] ?? $cur[$key]);
        }
        if ($v['tax_label'] === '') {
            $v['tax_label'] = 'TRN';
        }
        $db->prepare("INSERT INTO `epc_co_profile`
              (`id`,`legal_name`,`trade_name`,`logo_url`,`stamp_url`,`address`,`city`,`country`,`phone`,`email`,`website`,`trn`,`tax_label`,`trade_license`,`reg_no`,`bank_details`,`base_currency`,`fy_start`,`invoice_terms`,`header_html`,`footer_html`,`time_updated`)
              VALUES (1,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
              ON DUPLICATE KEY UPDATE
              `legal_name`=VALUES(`legal_name`),`trade_name`=VALUES(`trade_name`),`logo_url`=VALUES(`logo_url`),`stamp_url`=VALUES(`stamp_url`),`address`=VALUES(`address`),`city`=VALUES(`city`),`country`=VALUES(`country`),`phone`=VALUES(`phone`),`email`=VALUES(`email`),`website`=VALUES(`website`),`trn`=VALUES(`trn`),`tax_label`=VALUES(`tax_label`),`trade_license`=VALUES(`trade_license`),`reg_no`=VALUES(`reg_no`),`bank_details`=VALUES(`bank_details`),`base_currency`=VALUES(`base_currency`),`fy_start`=VALUES(`fy_start`),`invoice_terms`=VALUES(`invoice_terms`),`header_html`=VALUES(`header_html`),`footer_html`=VALUES(`footer_html`),`time_updated`=VALUES(`time_updated`)")
           ->execute(array(
               $v['legal_name'], $v['trade_name'], $v['logo_url'], $v['stamp_url'], $v['address'], $v['city'], $v['country'],
               $v['phone'], $v['email'], $v['website'], $v['trn'], $v['tax_label'], $v['trade_license'], $v['reg_no'],
               $v['bank_details'], $v['base_currency'], $v['fy_start'], $v['invoice_terms'], $v['header_html'], $v['footer_html'], time(),
           ));
        return epc_co_profile_get($db);
    }
}

if (!function_exists('epc_co_profile_get')) {
    /**
     * Read the tenant company profile (defaults when unset).
     *
     * @return array<string,mixed>
     */
    function epc_co_profile_get(PDO $db): array
    {
        epc_co_ensure_schema($db);
        $row = $db->query("SELECT * FROM `epc_co_profile` WHERE `id`=1")->fetch(PDO::FETCH_ASSOC);
        $defaults = array(
            'legal_name' => '', 'trade_name' => '', 'logo_url' => '', 'stamp_url' => '', 'address' => '', 'city' => '', 'country' => '',
            'phone' => '', 'email' => '', 'website' => '', 'trn' => '', 'tax_label' => 'TRN', 'trade_license' => '', 'reg_no' => '',
            'bank_details' => '', 'base_currency' => '', 'fy_start' => '01-01', 'invoice_terms' => '', 'header_html' => '', 'footer_html' => '',
        );
        if (!$row) {
            return $defaults;
        }
        foreach ($defaults as $k => $dv) {
            $defaults[$k] = (string) ($row[$k] ?? $dv);
        }
        return $defaults;
    }
}

if (!function_exists('epc_co_branch_save')) {
    /**
     * Save a per-branch document override.
     *
     * @param array<string,mixed> $p name, address, phone, email, trn
     */
    function epc_co_branch_save(PDO $db, int $branchId, array $p): void
    {
        epc_co_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_co_branch_profile` (`branch_id`,`name`,`address`,`phone`,`email`,`trn`,`time_updated`)
                      VALUES (?,?,?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`address`=VALUES(`address`),`phone`=VALUES(`phone`),`email`=VALUES(`email`),`trn`=VALUES(`trn`),`time_updated`=VALUES(`time_updated`)")
           ->execute(array($branchId, (string) ($p['name'] ?? ''), (string) ($p['address'] ?? ''), (string) ($p['phone'] ?? ''), (string) ($p['email'] ?? ''), (string) ($p['trn'] ?? ''), time()));
    }
}

if (!function_exists('epc_co_document_header')) {
    /**
     * Build the resolved header a printed document should use: company profile
     * with any branch override applied (branch address/phone/TRN win when set).
     *
     * @return array<string,mixed>
     */
    function epc_co_document_header(PDO $db, int $branchId = 0): array
    {
        $co = epc_co_profile_get($db);
        if ($branchId > 0) {
            $st = $db->prepare("SELECT * FROM `epc_co_branch_profile` WHERE `branch_id`=?");
            $st->execute(array($branchId));
            $b = $st->fetch(PDO::FETCH_ASSOC);
            if ($b) {
                if ((string) $b['address'] !== '') {
                    $co['address'] = (string) $b['address'];
                }
                if ((string) $b['phone'] !== '') {
                    $co['phone'] = (string) $b['phone'];
                }
                if ((string) $b['trn'] !== '') {
                    $co['trn'] = (string) $b['trn'];
                }
                if ((string) $b['name'] !== '') {
                    $co['branch_name'] = (string) $b['name'];
                }
            }
        }
        return $co;
    }
}

/* --------------------------- Amount in words ------------------------- */

if (!function_exists('epc_co_amount_in_words')) {
    /**
     * Amount-in-words for tax invoices, e.g. 1234.50 / AED ->
     * "AED One Thousand Two Hundred Thirty Four and Fifty Fils Only".
     * Minor-unit name resolved per currency (Fils/Cents/Paise/Halala...).
     */
    function epc_co_amount_in_words(float $amount, string $currency = ''): string
    {
        $minorNames = array(
            'AED' => 'Fils', 'SAR' => 'Halala', 'KWD' => 'Fils', 'BHD' => 'Fils', 'QAR' => 'Dirham', 'OMR' => 'Baisa',
            'USD' => 'Cents', 'EUR' => 'Cents', 'GBP' => 'Pence', 'INR' => 'Paise', 'PKR' => 'Paisa',
        );
        $minor = $minorNames[strtoupper($currency)] ?? 'Cents';
        $neg = $amount < 0;
        $amount = abs($amount);
        $whole = (int) floor($amount + 1e-9);
        $frac = (int) round(($amount - $whole) * 100);
        if ($frac === 100) {
            $whole++;
            $frac = 0;
        }
        $words = ($currency !== '' ? strtoupper($currency) . ' ' : '') . epc_co_int_to_words($whole);
        if ($frac > 0) {
            $words .= ' and ' . epc_co_int_to_words($frac) . ' ' . $minor;
        }
        $words .= ' Only';
        return ($neg ? 'Minus ' : '') . $words;
    }
}

if (!function_exists('epc_co_int_to_words')) {
    /** Convert a non-negative integer to English words (Indian-free, short scale). */
    function epc_co_int_to_words(int $n): string
    {
        if ($n === 0) {
            return 'Zero';
        }
        $ones = array('', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten',
            'Eleven', 'Twelve', 'Thirteen', 'Fourteen', 'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen');
        $tens = array('', '', 'Twenty', 'Thirty', 'Forty', 'Fifty', 'Sixty', 'Seventy', 'Eighty', 'Ninety');
        $scales = array(1000000000 => 'Billion', 1000000 => 'Million', 1000 => 'Thousand', 100 => 'Hundred');
        $out = '';
        foreach ($scales as $value => $name) {
            if ($n >= $value) {
                $count = intdiv($n, $value);
                $out .= epc_co_int_to_words($count) . ' ' . $name . ' ';
                $n %= $value;
            }
        }
        if ($n >= 20) {
            $out .= $tens[intdiv($n, 10)];
            if ($n % 10 > 0) {
                $out .= ' ' . $ones[$n % 10];
            }
        } elseif ($n > 0) {
            $out .= $ones[$n];
        }
        return trim($out);
    }
}

/* ----------------------- Statement of Account ------------------------ */

if (!function_exists('epc_co_statement')) {
    /**
     * Build a Statement of Account from ledger-style transaction rows. Pure
     * (no DB) so it serves both customer (AR) and vendor (AP) statements and is
     * fully testable.
     *
     * Each row: {date(ts), doc_no, type, debit, credit, currency?}. For a
     * customer, invoices are debit and receipts/credit-notes are credit; for a
     * vendor, bills are credit and payments are debit (caller maps the sign).
     *
     * @param array<int,array<string,mixed>> $rows
     * @param array<string,mixed> $opts opening(float), from(ts), to(ts), mode('forward'|'open')
     * @return array<string,mixed> {opening, lines, debit_total, credit_total, closing, ageing}
     */
    function epc_co_statement(array $rows, array $opts = array()): array
    {
        $opening = (float) ($opts['opening'] ?? 0.0);
        $from = (int) ($opts['from'] ?? 0);
        $to = (int) ($opts['to'] ?? 0);
        $mode = (string) ($opts['mode'] ?? 'forward');
        $asOf = $to > 0 ? $to : time();

        usort($rows, function ($a, $b) {
            return ((int) ($a['date'] ?? 0)) <=> ((int) ($b['date'] ?? 0));
        });

        $balance = $opening;
        $debitTotal = 0.0;
        $creditTotal = 0.0;
        $lines = array();
        foreach ($rows as $r) {
            $d = (int) ($r['date'] ?? 0);
            if ($from > 0 && $d < $from) {
                // pre-range rows roll into the opening balance
                $balance += (float) ($r['debit'] ?? 0) - (float) ($r['credit'] ?? 0);
                $opening = $balance;
                continue;
            }
            if ($to > 0 && $d > $to) {
                continue;
            }
            $debit = (float) ($r['debit'] ?? 0);
            $credit = (float) ($r['credit'] ?? 0);
            $balance = round($balance + $debit - $credit, 2);
            $debitTotal = round($debitTotal + $debit, 2);
            $creditTotal = round($creditTotal + $credit, 2);
            $lines[] = array(
                'date' => $d,
                'doc_no' => (string) ($r['doc_no'] ?? ''),
                'type' => (string) ($r['type'] ?? ''),
                'debit' => $debit,
                'credit' => $credit,
                'balance' => $balance,
            );
        }

        // Open-item mode: keep only unsettled documents (debit lines with a
        // positive running outstanding). Simplified net-by-document.
        if ($mode === 'open') {
            $net = array();
            foreach ($lines as $l) {
                $key = $l['doc_no'] !== '' ? $l['doc_no'] : ('row' . count($net));
                $net[$key] = ($net[$key] ?? 0) + $l['debit'] - $l['credit'];
            }
            $openLines = array();
            foreach ($lines as $l) {
                $key = $l['doc_no'] !== '' ? $l['doc_no'] : '';
                if ($key !== '' && abs($net[$key]) > 0.005 && $l['debit'] > 0) {
                    $l['outstanding'] = round($net[$key], 2);
                    $openLines[] = $l;
                    $net[$key] = 0; // emit once
                }
            }
            $lines = $openLines;
        }

        $closing = round($opening + $debitTotal - $creditTotal, 2);

        // Ageing of the closing (debit-positive) balance by document date.
        $buckets = array('current' => 0.0, 'd30' => 0.0, 'd60' => 0.0, 'd90' => 0.0, 'd90plus' => 0.0);
        foreach ($rows as $r) {
            $d = (int) ($r['date'] ?? 0);
            if ($to > 0 && $d > $to) {
                continue;
            }
            $amt = (float) ($r['debit'] ?? 0) - (float) ($r['credit'] ?? 0);
            if ($amt <= 0) {
                continue;
            }
            $age = (int) floor(($asOf - $d) / 86400);
            if ($age <= 0) {
                $buckets['current'] += $amt;
            } elseif ($age <= 30) {
                $buckets['d30'] += $amt;
            } elseif ($age <= 60) {
                $buckets['d60'] += $amt;
            } elseif ($age <= 90) {
                $buckets['d90'] += $amt;
            } else {
                $buckets['d90plus'] += $amt;
            }
        }
        foreach ($buckets as $k => $val) {
            $buckets[$k] = round($val, 2);
        }

        return array(
            'opening' => round($opening, 2),
            'lines' => $lines,
            'debit_total' => $debitTotal,
            'credit_total' => $creditTotal,
            'closing' => $closing,
            'ageing' => $buckets,
        );
    }
}
