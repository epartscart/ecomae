<?php
/**
 * Advanced ERP — Data import / export.
 *
 * Spreadsheet (CSV / Excel-compatible) import & export usable across every
 * module. Provides:
 *   - Column specifications per dataset (items, suppliers, customers, opening
 *     balances, etc.) with required/optional flags and types.
 *   - Template generation (header row + sample) so users always upload the
 *     right columns.
 *   - A validating parser that maps rows to associative records and reports
 *     per-row errors (type/required checks) without throwing.
 *
 * Pure functions over strings/arrays — no DB writes here; callers persist the
 * validated rows with their own module functions. Nothing existing is modified.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_dataio_specs')) {
    /**
     * Built-in dataset column specifications.
     *
     * @return array<string,array<string,mixed>>
     */
    function epc_dataio_specs(): array
    {
        return array(
            'items' => array(
                'label' => 'Products / Items',
                'columns' => array(
                    array('key' => 'sku', 'label' => 'SKU', 'type' => 'string', 'required' => true, 'sample' => 'BRK-1001'),
                    array('key' => 'name', 'label' => 'Name', 'type' => 'string', 'required' => true, 'sample' => 'Brake Pad Set'),
                    array('key' => 'unit', 'label' => 'Unit', 'type' => 'string', 'required' => false, 'sample' => 'pc'),
                    array('key' => 'cost_price', 'label' => 'Cost Price', 'type' => 'number', 'required' => false, 'sample' => '45.00'),
                    array('key' => 'sale_price', 'label' => 'Sale Price', 'type' => 'number', 'required' => false, 'sample' => '69.00'),
                    array('key' => 'reorder_level', 'label' => 'Reorder Level', 'type' => 'int', 'required' => false, 'sample' => '10'),
                ),
            ),
            'suppliers' => array(
                'label' => 'Suppliers',
                'columns' => array(
                    array('key' => 'name', 'label' => 'Name', 'type' => 'string', 'required' => true, 'sample' => 'Acme Parts Ltd'),
                    array('key' => 'email', 'label' => 'Email', 'type' => 'string', 'required' => false, 'sample' => 'sales@acme.com'),
                    array('key' => 'phone', 'label' => 'Phone', 'type' => 'string', 'required' => false, 'sample' => '+971500000000'),
                    array('key' => 'country_code', 'label' => 'Country', 'type' => 'string', 'required' => false, 'sample' => 'AE'),
                    array('key' => 'vat_number', 'label' => 'VAT/TRN', 'type' => 'string', 'required' => false, 'sample' => '100123456700003'),
                ),
            ),
            'customers' => array(
                'label' => 'Customers',
                'columns' => array(
                    array('key' => 'name', 'label' => 'Name', 'type' => 'string', 'required' => true, 'sample' => 'Globex LLC'),
                    array('key' => 'email', 'label' => 'Email', 'type' => 'string', 'required' => false, 'sample' => 'ap@globex.com'),
                    array('key' => 'country_code', 'label' => 'Country', 'type' => 'string', 'required' => false, 'sample' => 'AE'),
                    array('key' => 'credit_limit', 'label' => 'Credit Limit', 'type' => 'number', 'required' => false, 'sample' => '50000'),
                    array('key' => 'terms_days', 'label' => 'Payment Terms (days)', 'type' => 'int', 'required' => false, 'sample' => '30'),
                ),
            ),
            'opening_stock' => array(
                'label' => 'Opening Stock',
                'columns' => array(
                    array('key' => 'sku', 'label' => 'SKU', 'type' => 'string', 'required' => true, 'sample' => 'BRK-1001'),
                    array('key' => 'warehouse_code', 'label' => 'Warehouse', 'type' => 'string', 'required' => true, 'sample' => 'MAIN'),
                    array('key' => 'qty', 'label' => 'Quantity', 'type' => 'number', 'required' => true, 'sample' => '100'),
                    array('key' => 'unit_cost', 'label' => 'Unit Cost', 'type' => 'number', 'required' => true, 'sample' => '45.00'),
                ),
            ),
        );
    }
}

if (!function_exists('epc_dataio_spec')) {
    /**
     * @return array<string,mixed>|null
     */
    function epc_dataio_spec(string $dataset): ?array
    {
        $all = epc_dataio_specs();
        return $all[$dataset] ?? null;
    }
}

if (!function_exists('epc_dataio_csv_cell')) {
    function epc_dataio_csv_cell(string $v): string
    {
        if (preg_match('/[",\r\n]/', $v)) {
            return '"' . str_replace('"', '""', $v) . '"';
        }
        return $v;
    }
}

if (!function_exists('epc_dataio_template')) {
    /**
     * Build a CSV template (header + one sample row) for a dataset.
     */
    function epc_dataio_template(string $dataset): string
    {
        $spec = epc_dataio_spec($dataset);
        if (!$spec) {
            throw new Exception('Unknown dataset: ' . $dataset);
        }
        $headers = array();
        $sample = array();
        foreach ($spec['columns'] as $c) {
            $headers[] = epc_dataio_csv_cell((string) $c['label']);
            $sample[] = epc_dataio_csv_cell((string) ($c['sample'] ?? ''));
        }
        return implode(',', $headers) . "\r\n" . implode(',', $sample) . "\r\n";
    }
}

if (!function_exists('epc_dataio_export_csv')) {
    /**
     * Export records to CSV using a dataset spec (column order + labels).
     *
     * @param array<int,array<string,mixed>> $records
     */
    function epc_dataio_export_csv(string $dataset, array $records): string
    {
        $spec = epc_dataio_spec($dataset);
        if (!$spec) {
            throw new Exception('Unknown dataset: ' . $dataset);
        }
        $headers = array();
        foreach ($spec['columns'] as $c) {
            $headers[] = epc_dataio_csv_cell((string) $c['label']);
        }
        $out = implode(',', $headers) . "\r\n";
        foreach ($records as $rec) {
            $row = array();
            foreach ($spec['columns'] as $c) {
                $row[] = epc_dataio_csv_cell((string) ($rec[$c['key']] ?? ''));
            }
            $out .= implode(',', $row) . "\r\n";
        }
        return $out;
    }
}

if (!function_exists('epc_dataio_parse_csv')) {
    /**
     * Parse raw CSV text into rows of arrays (RFC-4180-ish: quotes, embedded
     * commas/newlines, doubled quotes).
     *
     * @return array<int,array<int,string>>
     */
    function epc_dataio_parse_csv(string $raw): array
    {
        $raw = str_replace("\r\n", "\n", $raw);
        $raw = str_replace("\r", "\n", $raw);
        $rows = array();
        $field = '';
        $row = array();
        $inQuotes = false;
        $len = strlen($raw);
        for ($i = 0; $i < $len; $i++) {
            $ch = $raw[$i];
            if ($inQuotes) {
                if ($ch === '"') {
                    if ($i + 1 < $len && $raw[$i + 1] === '"') {
                        $field .= '"';
                        $i++;
                    } else {
                        $inQuotes = false;
                    }
                } else {
                    $field .= $ch;
                }
            } else {
                if ($ch === '"') {
                    $inQuotes = true;
                } elseif ($ch === ',') {
                    $row[] = $field;
                    $field = '';
                } elseif ($ch === "\n") {
                    $row[] = $field;
                    $rows[] = $row;
                    $row = array();
                    $field = '';
                } else {
                    $field .= $ch;
                }
            }
        }
        // last field/row (if file didn't end with newline)
        if ($field !== '' || count($row) > 0) {
            $row[] = $field;
            $rows[] = $row;
        }
        return $rows;
    }
}

if (!function_exists('epc_dataio_validate_value')) {
    /**
     * Coerce/validate a single value against a column type.
     *
     * @return array{ok:bool,value:mixed,error:string}
     */
    function epc_dataio_validate_value(string $raw, array $col): array
    {
        $raw = trim($raw);
        $type = (string) ($col['type'] ?? 'string');
        $required = !empty($col['required']);
        if ($raw === '') {
            if ($required) {
                return array('ok' => false, 'value' => null, 'error' => $col['label'] . ' is required');
            }
            return array('ok' => true, 'value' => null, 'error' => '');
        }
        if ($type === 'number') {
            if (!is_numeric($raw)) {
                return array('ok' => false, 'value' => null, 'error' => $col['label'] . ' must be a number');
            }
            return array('ok' => true, 'value' => (float) $raw, 'error' => '');
        }
        if ($type === 'int') {
            if (!preg_match('/^-?\d+$/', $raw)) {
                return array('ok' => false, 'value' => null, 'error' => $col['label'] . ' must be a whole number');
            }
            return array('ok' => true, 'value' => (int) $raw, 'error' => '');
        }
        return array('ok' => true, 'value' => $raw, 'error' => '');
    }
}

if (!function_exists('epc_dataio_import')) {
    /**
     * Parse + validate an upload against a dataset spec. Matches header labels
     * (case-insensitive) to spec columns. Returns valid records + per-row
     * errors. Does NOT write to the DB.
     *
     * @return array<string,mixed>
     */
    function epc_dataio_import(string $dataset, string $rawCsv): array
    {
        $spec = epc_dataio_spec($dataset);
        if (!$spec) {
            throw new Exception('Unknown dataset: ' . $dataset);
        }
        $rows = epc_dataio_parse_csv($rawCsv);
        if (!$rows) {
            return array('dataset' => $dataset, 'records' => array(), 'errors' => array(), 'row_count' => 0, 'valid_count' => 0, 'error_count' => 0);
        }

        // Header mapping: label (or key) -> column index.
        $header = array_shift($rows);
        $byLabel = array();
        foreach ($spec['columns'] as $c) {
            $byLabel[strtolower((string) $c['label'])] = $c;
            $byLabel[strtolower((string) $c['key'])] = $c;
        }
        $colIndex = array(); // key -> index
        foreach ($header as $idx => $h) {
            $h = strtolower(trim($h));
            if (isset($byLabel[$h])) {
                $colIndex[$byLabel[$h]['key']] = $idx;
            }
        }

        $records = array();
        $errors = array();
        $rowNo = 1; // data rows (excludes header)
        foreach ($rows as $r) {
            // skip fully blank lines
            $joined = trim(implode('', $r));
            if ($joined === '') {
                $rowNo++;
                continue;
            }
            $rec = array();
            $rowErrors = array();
            foreach ($spec['columns'] as $c) {
                $idx = $colIndex[$c['key']] ?? null;
                $raw = $idx !== null && isset($r[$idx]) ? (string) $r[$idx] : '';
                $v = epc_dataio_validate_value($raw, $c);
                if (!$v['ok']) {
                    $rowErrors[] = $v['error'];
                } else {
                    $rec[$c['key']] = $v['value'];
                }
            }
            if ($rowErrors) {
                $errors[] = array('row' => $rowNo, 'errors' => $rowErrors);
            } else {
                $records[] = $rec;
            }
            $rowNo++;
        }

        return array(
            'dataset' => $dataset,
            'records' => $records,
            'errors' => $errors,
            'row_count' => $rowNo - 1,
            'valid_count' => count($records),
            'error_count' => count($errors),
        );
    }
}
