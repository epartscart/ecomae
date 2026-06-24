<?php
/**
 * Advanced ERP — E-invoicing.
 *
 * Builds a structured, country-aware electronic-invoice payload (UBL-inspired
 * JSON) from an ERP sales document. Designed to be the canonical intermediate
 * representation that country adapters (UAE FTA, EU EN16931, India IRN/GST,
 * Saudi ZATCA, etc.) can serialise to their required format.
 *
 * Pure transformation — reads document data passed in and emits a normalised
 * structure. No tables are created or modified.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_einv_country_profile')) {
    /**
     * Per-country e-invoicing profile: scheme name + which extra fields the
     * format mandates. Falls back to a generic profile.
     *
     * @return array<string,mixed>
     */
    function epc_einv_country_profile(string $country): array
    {
        $country = strtoupper(trim($country));
        $map = array(
            'AE' => array('scheme' => 'UAE-FTA', 'tax_label' => 'VAT', 'requires' => array('seller_trn', 'buyer_trn'), 'clearance' => false),
            'SA' => array('scheme' => 'ZATCA', 'tax_label' => 'VAT', 'requires' => array('seller_trn', 'qr'), 'clearance' => true),
            'IN' => array('scheme' => 'GST-IRN', 'tax_label' => 'GST', 'requires' => array('seller_gstin', 'buyer_gstin', 'hsn'), 'clearance' => true),
            'GB' => array('scheme' => 'EN16931', 'tax_label' => 'VAT', 'requires' => array('seller_vat'), 'clearance' => false),
            'DE' => array('scheme' => 'EN16931', 'tax_label' => 'VAT', 'requires' => array('seller_vat', 'buyer_vat'), 'clearance' => false),
            'FR' => array('scheme' => 'EN16931', 'tax_label' => 'VAT', 'requires' => array('seller_vat', 'buyer_vat'), 'clearance' => true),
            'IT' => array('scheme' => 'FatturaPA', 'tax_label' => 'IVA', 'requires' => array('seller_vat', 'sdi_code'), 'clearance' => true),
            'AU' => array('scheme' => 'PEPPOL', 'tax_label' => 'GST', 'requires' => array('seller_abn'), 'clearance' => false),
        );
        if (isset($map[$country])) {
            $p = $map[$country];
            $p['country'] = $country;
            return $p;
        }
        return array('country' => $country, 'scheme' => 'GENERIC', 'tax_label' => 'TAX', 'requires' => array(), 'clearance' => false);
    }
}

if (!function_exists('epc_einv_build')) {
    /**
     * Build a normalised e-invoice document.
     *
     * @param array<string,mixed> $invoice Header: invoice_no, issue_date (ts),
     *        currency, seller{name,tax_id,country,address}, buyer{...}.
     * @param array<int,array<string,mixed>> $lines Each: description, qty,
     *        unit_price, tax_percent (optional), tax_amount (optional).
     * @return array<string,mixed>
     */
    function epc_einv_build(array $invoice, array $lines): array
    {
        $seller = $invoice['seller'] ?? array();
        $buyer = $invoice['buyer'] ?? array();
        $country = (string) ($seller['country'] ?? $invoice['country'] ?? '');
        $profile = epc_einv_country_profile($country);

        $netTotal = 0.0;
        $taxTotal = 0.0;
        $outLines = array();
        $i = 0;
        foreach ($lines as $ln) {
            $i++;
            $qty = (float) ($ln['qty'] ?? 0);
            $price = (float) ($ln['unit_price'] ?? 0);
            $lineNet = round($qty * $price, 2);
            $taxPct = isset($ln['tax_percent']) ? (float) $ln['tax_percent'] : null;
            if (isset($ln['tax_amount'])) {
                $lineTax = round((float) $ln['tax_amount'], 2);
            } elseif ($taxPct !== null) {
                $lineTax = round($lineNet * $taxPct / 100, 2);
            } else {
                $lineTax = 0.0;
            }
            $netTotal += $lineNet;
            $taxTotal += $lineTax;
            $outLines[] = array(
                'id' => $i,
                'description' => (string) ($ln['description'] ?? ''),
                'quantity' => $qty,
                'unit_price' => $price,
                'line_net' => $lineNet,
                'tax_percent' => $taxPct,
                'tax_amount' => $lineTax,
                'line_total' => round($lineNet + $lineTax, 2),
                'hsn_sac' => (string) ($ln['hsn_sac'] ?? ''),
            );
        }

        $netTotal = round($netTotal, 2);
        $taxTotal = round($taxTotal, 2);
        $grand = round($netTotal + $taxTotal, 2);

        $doc = array(
            'profile' => $profile['scheme'],
            'tax_label' => $profile['tax_label'],
            'clearance_required' => (bool) $profile['clearance'],
            'invoice_number' => (string) ($invoice['invoice_no'] ?? ''),
            'issue_date' => date('Y-m-d', (int) ($invoice['issue_date'] ?? time())),
            'currency' => (string) ($invoice['currency'] ?? 'AED'),
            'seller' => array(
                'name' => (string) ($seller['name'] ?? ''),
                'tax_id' => (string) ($seller['tax_id'] ?? ''),
                'country' => $country,
                'address' => (string) ($seller['address'] ?? ''),
            ),
            'buyer' => array(
                'name' => (string) ($buyer['name'] ?? ''),
                'tax_id' => (string) ($buyer['tax_id'] ?? ''),
                'country' => (string) ($buyer['country'] ?? ''),
                'address' => (string) ($buyer['address'] ?? ''),
            ),
            'lines' => $outLines,
            'totals' => array(
                'net' => $netTotal,
                'tax' => $taxTotal,
                'grand_total' => $grand,
            ),
        );

        $doc['validation'] = epc_einv_validate($doc, $profile);
        $doc['hash'] = hash('sha256', json_encode(array($doc['invoice_number'], $doc['issue_date'], $grand, $doc['seller']['tax_id'])) ?: '');
        return $doc;
    }
}

if (!function_exists('epc_einv_validate')) {
    /**
     * Validate a built document against its country profile's required fields.
     *
     * @param array<string,mixed> $doc
     * @param array<string,mixed> $profile
     * @return array<string,mixed>
     */
    function epc_einv_validate(array $doc, array $profile): array
    {
        $errors = array();
        if (($doc['invoice_number'] ?? '') === '') {
            $errors[] = 'invoice_number is required';
        }
        if (empty($doc['lines'])) {
            $errors[] = 'at least one line is required';
        }
        foreach ((array) ($profile['requires'] ?? array()) as $req) {
            switch ($req) {
                case 'seller_trn':
                case 'seller_vat':
                case 'seller_gstin':
                case 'seller_abn':
                    if (($doc['seller']['tax_id'] ?? '') === '') {
                        $errors[] = "seller tax id required for {$profile['scheme']}";
                    }
                    break;
                case 'buyer_trn':
                case 'buyer_vat':
                case 'buyer_gstin':
                    if (($doc['buyer']['tax_id'] ?? '') === '') {
                        $errors[] = "buyer tax id required for {$profile['scheme']}";
                    }
                    break;
                case 'hsn':
                    foreach ((array) $doc['lines'] as $l) {
                        if (($l['hsn_sac'] ?? '') === '') {
                            $errors[] = 'HSN/SAC code required on all lines for GST';
                            break;
                        }
                    }
                    break;
            }
        }
        return array('valid' => count($errors) === 0, 'errors' => $errors);
    }
}

if (!function_exists('epc_einv_to_json')) {
    /**
     * Serialise a built e-invoice document to pretty JSON.
     *
     * @param array<string,mixed> $doc
     */
    function epc_einv_to_json(array $doc): string
    {
        return (string) json_encode($doc, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    }
}

/* ─────────────────── ASP API Live Submit (PINT-AE) ─────────────────── */

if (!function_exists('epc_einv_asp_config')) {
    function epc_einv_asp_config(): array
    {
        return array(
            'api_url' => 'https://asp.ecomae.com/api/v1/invoices',
            'sandbox_url' => 'https://sandbox-asp.ecomae.com/api/v1/invoices',
            'mode' => 'live',
            'timeout' => 30,
            'retry_max' => 3,
            'pint_ae_profile' => 'urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0',
        );
    }
}

if (!function_exists('epc_einv_asp_submit')) {
    /**
     * Submit an e-invoice to ASP (Accredited Service Provider) for UAE FTA.
     *
     * @param array $doc Built e-invoice document from epc_einv_build()
     * @param string $apiKey Tenant's ASP API key
     * @param string $mode 'live' or 'sandbox'
     * @return array{ok:bool, submission_id:string, status:string, errors:array}
     */
    function epc_einv_asp_submit(array $doc, string $apiKey, string $mode = 'live'): array
    {
        $config = epc_einv_asp_config();
        $url = ($mode === 'sandbox') ? $config['sandbox_url'] : $config['api_url'];

        $payload = array(
            'profile_id' => $config['pint_ae_profile'],
            'document_type' => ($doc['totals']['grand_total'] ?? 0) < 0 ? '381' : '380',
            'invoice' => $doc,
            'submitted_at' => date('c'),
        );

        $body = json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
        $headers = array(
            'Content-Type: application/json',
            'Authorization: Bearer ' . $apiKey,
            'X-PINT-AE-Version: 1.0',
            'X-Document-Hash: ' . ($doc['hash'] ?? ''),
        );

        $lastError = '';
        for ($attempt = 1; $attempt <= $config['retry_max']; $attempt++) {
            $ch = curl_init();
            curl_setopt_array($ch, array(
                CURLOPT_URL            => $url,
                CURLOPT_POST           => true,
                CURLOPT_POSTFIELDS     => $body,
                CURLOPT_HTTPHEADER     => $headers,
                CURLOPT_RETURNTRANSFER => true,
                CURLOPT_TIMEOUT        => $config['timeout'],
                CURLOPT_CONNECTTIMEOUT => 10,
                CURLOPT_SSL_VERIFYPEER => true,
            ));
            $response = curl_exec($ch);
            $httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
            $lastError = curl_error($ch);
            curl_close($ch);

            if ($httpCode >= 200 && $httpCode < 300) {
                $data = json_decode((string) $response, true) ?: array();
                return array(
                    'ok' => true,
                    'submission_id' => (string) ($data['submission_id'] ?? $data['id'] ?? ''),
                    'status' => (string) ($data['status'] ?? 'submitted'),
                    'asp_response' => $data,
                    'errors' => array(),
                );
            }

            if ($httpCode >= 400 && $httpCode < 500) {
                $data = json_decode((string) $response, true) ?: array();
                return array(
                    'ok' => false,
                    'submission_id' => '',
                    'status' => 'rejected',
                    'errors' => (array) ($data['errors'] ?? array($data['message'] ?? 'HTTP ' . $httpCode)),
                    'http_code' => $httpCode,
                );
            }

            if ($attempt < $config['retry_max']) {
                usleep($attempt * 500000);
            }
        }

        return array(
            'ok' => false,
            'submission_id' => '',
            'status' => 'error',
            'errors' => array($lastError ?: 'ASP API unreachable after ' . $config['retry_max'] . ' attempts'),
        );
    }
}

if (!function_exists('epc_einv_asp_poll_status')) {
    /**
     * Poll ASP for submission status (clearance/approval).
     */
    function epc_einv_asp_poll_status(string $submissionId, string $apiKey, string $mode = 'live'): array
    {
        $config = epc_einv_asp_config();
        $url = (($mode === 'sandbox') ? $config['sandbox_url'] : $config['api_url']) . '/' . $submissionId . '/status';

        $ch = curl_init();
        curl_setopt_array($ch, array(
            CURLOPT_URL            => $url,
            CURLOPT_HTTPHEADER     => array('Authorization: Bearer ' . $apiKey),
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT        => 15,
            CURLOPT_SSL_VERIFYPEER => true,
        ));
        $response = curl_exec($ch);
        $httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($httpCode >= 200 && $httpCode < 300) {
            $data = json_decode((string) $response, true) ?: array();
            return array(
                'ok' => true,
                'submission_id' => $submissionId,
                'status' => (string) ($data['status'] ?? 'pending'),
                'clearance_status' => (string) ($data['clearance_status'] ?? ''),
                'fta_reference' => (string) ($data['fta_reference'] ?? ''),
                'errors' => (array) ($data['errors'] ?? array()),
            );
        }
        return array('ok' => false, 'status' => 'poll_error', 'http_code' => $httpCode);
    }
}

if (!function_exists('epc_einv_credit_note_381')) {
    /**
     * Build a credit note (type 381) for a previously issued invoice.
     */
    function epc_einv_credit_note_381(array $originalInvoice, array $creditLines, string $reason = ''): array
    {
        $creditInvoice = $originalInvoice;
        $creditInvoice['invoice_no'] = 'CN-' . ($originalInvoice['invoice_no'] ?? '');
        $creditInvoice['issue_date'] = time();

        $doc = epc_einv_build($creditInvoice, $creditLines);
        $doc['document_type'] = '381';
        $doc['billing_reference'] = array(
            'original_invoice' => (string) ($originalInvoice['invoice_no'] ?? ''),
            'original_issue_date' => date('Y-m-d', (int) ($originalInvoice['issue_date'] ?? time())),
        );
        $doc['credit_note_reason'] = $reason;

        foreach ($doc['lines'] as &$line) {
            $line['line_net'] = -abs($line['line_net']);
            $line['tax_amount'] = -abs($line['tax_amount']);
            $line['line_total'] = -abs($line['line_total']);
        }
        unset($line);

        $doc['totals']['net'] = -abs($doc['totals']['net']);
        $doc['totals']['tax'] = -abs($doc['totals']['tax']);
        $doc['totals']['grand_total'] = -abs($doc['totals']['grand_total']);

        return $doc;
    }
}

/* ─────────────────── E-invoice DB tracking ─────────────────── */

if (!function_exists('epc_einv_ensure_schema')) {
    function epc_einv_ensure_schema(PDO $db): void
    {
        static $done = false;
        if ($done) return;
        $done = true;

        $db->exec(
            'CREATE TABLE IF NOT EXISTS `epc_einvoice_submissions` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                `invoice_no` VARCHAR(64) NOT NULL,
                `document_type` CHAR(3) NOT NULL DEFAULT \'380\',
                `submission_id` VARCHAR(128) NOT NULL DEFAULT \'\',
                `status` VARCHAR(32) NOT NULL DEFAULT \'draft\',
                `asp_response_json` TEXT,
                `fta_reference` VARCHAR(128) NOT NULL DEFAULT \'\',
                `submitted_at` DATETIME NULL,
                `cleared_at` DATETIME NULL,
                `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
                INDEX `idx_invoice` (`invoice_no`),
                INDEX `idx_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
        );
    }
}

if (!function_exists('epc_einv_track_submission')) {
    function epc_einv_track_submission(PDO $db, string $invoiceNo, array $aspResult): int
    {
        epc_einv_ensure_schema($db);
        $db->prepare(
            'INSERT INTO `epc_einvoice_submissions`
             (`invoice_no`, `document_type`, `submission_id`, `status`, `asp_response_json`, `submitted_at`)
             VALUES (?, ?, ?, ?, ?, NOW())'
        )->execute(array(
            $invoiceNo,
            $aspResult['document_type'] ?? '380',
            $aspResult['submission_id'] ?? '',
            $aspResult['status'] ?? 'submitted',
            json_encode($aspResult),
        ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_einv_submission_history')) {
    function epc_einv_submission_history(PDO $db, string $invoiceNo = '', int $limit = 50): array
    {
        epc_einv_ensure_schema($db);
        if ($invoiceNo !== '') {
            $st = $db->prepare('SELECT * FROM `epc_einvoice_submissions` WHERE `invoice_no` = ? ORDER BY `id` DESC LIMIT ?');
            $st->execute(array($invoiceNo, $limit));
        } else {
            $st = $db->prepare('SELECT * FROM `epc_einvoice_submissions` ORDER BY `id` DESC LIMIT ?');
            $st->execute(array($limit));
        }
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}
