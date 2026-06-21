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
