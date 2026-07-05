<?php
/**
 * Data Migration Tool — import data from existing systems into ERP.
 * Supports open balance transfer and full transaction transfer.
 * Excel/CSV upload with validation, mapping, and dry-run.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_data_mig_ensure_schema')) {
    function epc_data_mig_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_data_migrations` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `migration_type` varchar(50) NOT NULL DEFAULT 'open_balance' COMMENT 'open_balance,full_transaction,master_data',
            `entity_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'customers,suppliers,products,coa,invoices,journals,inventory',
            `file_name` varchar(300) NOT NULL DEFAULT '',
            `file_path` varchar(500) NOT NULL DEFAULT '',
            `total_rows` int(11) NOT NULL DEFAULT 0,
            `valid_rows` int(11) NOT NULL DEFAULT 0,
            `error_rows` int(11) NOT NULL DEFAULT 0,
            `imported_rows` int(11) NOT NULL DEFAULT 0,
            `status` enum('uploaded','validated','dry_run','importing','completed','failed','rolled_back') NOT NULL DEFAULT 'uploaded',
            `column_mapping` text COMMENT 'JSON: source_col => target_field',
            `validation_errors` text COMMENT 'JSON array of row-level errors',
            `options` text COMMENT 'JSON: date_format, currency, decimal_separator, etc.',
            `imported_by` int(11) NOT NULL DEFAULT 0,
            `imported_by_name` varchar(120) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_completed` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Data migration jobs'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_data_migration_rows` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `migration_id` int(11) NOT NULL DEFAULT 0,
            `row_number` int(11) NOT NULL DEFAULT 0,
            `raw_data` text COMMENT 'JSON of original row',
            `mapped_data` text COMMENT 'JSON of mapped fields',
            `status` enum('pending','valid','error','imported','skipped') NOT NULL DEFAULT 'pending',
            `error_message` varchar(500) NOT NULL DEFAULT '',
            `created_entity_id` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_migration` (`migration_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Individual migration rows'");
    }

    function epc_data_mig_upload(PDO $db, array $data, array $file): array
    {
        $allowed = ['text/csv', 'application/vnd.ms-excel', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'text/plain'];
        $maxSize = 50 * 1024 * 1024;

        if (!isset($file['tmp_name']) || !is_uploaded_file($file['tmp_name'])) {
            return ['ok' => false, 'error' => 'No file uploaded'];
        }
        if ($file['size'] > $maxSize) {
            return ['ok' => false, 'error' => 'File too large (max 50MB)'];
        }

        $uploadDir = $_SERVER['DOCUMENT_ROOT'] . '/uploads/data_migrations/' . ($data['company_id'] ?? 0) . '/';
        if (!is_dir($uploadDir)) @mkdir($uploadDir, 0755, true);

        $safeName = preg_replace('/[^a-zA-Z0-9._-]/', '_', basename($file['name']));
        $uniqueName = time() . '_' . $safeName;
        $destPath = $uploadDir . $uniqueName;
        if (!move_uploaded_file($file['tmp_name'], $destPath)) {
            return ['ok' => false, 'error' => 'Failed to save file'];
        }

        $stmt = $db->prepare("INSERT INTO `epc_data_migrations` (`company_id`,`migration_type`,`entity_type`,`file_name`,`file_path`,`status`,`imported_by`,`imported_by_name`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['migration_type'] ?? 'open_balance', $data['entity_type'] ?? '', $safeName, $destPath, 'uploaded', $data['user_id'] ?? 0, $data['user_name'] ?? '', time()]);

        return ['ok' => true, 'migration_id' => (int) $db->lastInsertId()];
    }

    function epc_data_mig_parse_csv(string $filePath, string $delimiter = ','): array
    {
        $rows = [];
        $headers = [];
        if (($fh = fopen($filePath, 'r')) !== false) {
            $lineNum = 0;
            while (($row = fgetcsv($fh, 0, $delimiter)) !== false) {
                $lineNum++;
                if ($lineNum === 1) {
                    $headers = array_map('trim', $row);
                    continue;
                }
                $mapped = [];
                foreach ($headers as $i => $h) {
                    $mapped[$h] = $row[$i] ?? '';
                }
                $rows[] = $mapped;
            }
            fclose($fh);
        }
        return ['headers' => $headers, 'rows' => $rows, 'total' => count($rows)];
    }

    function epc_data_mig_validate(PDO $db, int $migrationId, array $columnMapping): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_data_migrations` WHERE `id` = ?");
        $stmt->execute([$migrationId]);
        $mig = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$mig) return ['ok' => false, 'error' => 'Migration not found'];

        $parsed = epc_data_mig_parse_csv($mig['file_path']);
        $valid = 0; $errors = [];

        foreach ($parsed['rows'] as $i => $row) {
            $rowErrors = [];
            $mapped = [];
            foreach ($columnMapping as $srcCol => $targetField) {
                $mapped[$targetField] = $row[$srcCol] ?? '';
            }

            if ($mig['entity_type'] === 'customers' && empty($mapped['name'])) {
                $rowErrors[] = 'Customer name is required';
            }
            if ($mig['entity_type'] === 'invoices' && empty($mapped['amount'])) {
                $rowErrors[] = 'Amount is required';
            }

            $status = empty($rowErrors) ? 'valid' : 'error';
            if ($status === 'valid') $valid++;
            else $errors[] = ['row' => $i + 2, 'errors' => $rowErrors];

            $db->prepare("INSERT INTO `epc_data_migration_rows` (`migration_id`,`row_number`,`raw_data`,`mapped_data`,`status`,`error_message`) VALUES (?,?,?,?,?,?)")
                ->execute([$migrationId, $i + 2, json_encode($row), json_encode($mapped), $status, implode('; ', $rowErrors)]);
        }

        $db->prepare("UPDATE `epc_data_migrations` SET `total_rows` = ?, `valid_rows` = ?, `error_rows` = ?, `status` = 'validated', `column_mapping` = ? WHERE `id` = ?")
            ->execute([count($parsed['rows']), $valid, count($errors), json_encode($columnMapping), $migrationId]);

        return ['ok' => true, 'total' => count($parsed['rows']), 'valid' => $valid, 'errors' => count($errors), 'error_details' => $errors];
    }

    function epc_data_mig_get_templates(): array
    {
        return [
            'customers' => ['name', 'email', 'phone', 'address', 'city', 'country', 'tax_id', 'group', 'credit_limit', 'payment_terms'],
            'suppliers' => ['name', 'email', 'phone', 'address', 'city', 'country', 'tax_id', 'payment_terms', 'bank_account'],
            'products' => ['sku', 'name', 'category', 'unit', 'cost_price', 'sell_price', 'tax_rate', 'barcode', 'weight', 'stock_qty'],
            'coa' => ['account_code', 'account_name', 'account_type', 'parent_code', 'currency', 'opening_balance'],
            'open_balances' => ['account_code', 'customer_or_supplier', 'debit', 'credit', 'currency', 'reference', 'date'],
            'invoices' => ['invoice_no', 'customer', 'date', 'due_date', 'amount', 'tax', 'total', 'status', 'reference'],
            'inventory' => ['sku', 'warehouse', 'location', 'qty_on_hand', 'cost_price', 'batch_no', 'expiry_date'],
        ];
    }
}
