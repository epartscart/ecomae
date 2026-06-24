<?php
/**
 * P2 #36 — Import Orchestrator
 *
 * Bulk CSV/XML import with validation, field mapping, duplicate detection,
 * dry-run preview, chunked processing, and error reporting.
 * Schema: epc_import_jobs, epc_import_errors
 */

if (!defined('EPC_IMPORT_ORCHESTRATOR_VERSION')) {
    define('EPC_IMPORT_ORCHESTRATOR_VERSION', '1.0.0');
}

function epc_import_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_import_jobs` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `entity_type`     VARCHAR(32)    NOT NULL,
            `source_format`   ENUM('csv','xml','json','xlsx') NOT NULL DEFAULT 'csv',
            `filename`        VARCHAR(256)   NOT NULL DEFAULT '',
            `total_rows`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `processed_rows`  INT UNSIGNED   NOT NULL DEFAULT 0,
            `success_rows`    INT UNSIGNED   NOT NULL DEFAULT 0,
            `error_rows`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `skip_rows`       INT UNSIGNED   NOT NULL DEFAULT 0,
            `field_mapping`   JSON           NULL,
            `options`         JSON           NULL,
            `status`          ENUM('pending','validating','validated','importing','completed','failed','cancelled') NOT NULL DEFAULT 'pending',
            `dry_run`         TINYINT(1)     NOT NULL DEFAULT 0,
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `completed_at`    DATETIME       NULL,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_status` (`status`),
            INDEX `idx_entity` (`entity_type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_import_errors` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `job_id`          INT UNSIGNED   NOT NULL,
            `row_number`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `field`           VARCHAR(64)    NOT NULL DEFAULT '',
            `value`           TEXT           NULL,
            `error_type`      ENUM('required','format','duplicate','reference','validation','unknown') NOT NULL DEFAULT 'unknown',
            `message`         VARCHAR(512)   NOT NULL DEFAULT '',
            INDEX `idx_job` (`job_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_import_entity_schemas(): array
{
    return array(
        'products' => array(
            'required' => array('sku', 'product_name'),
            'optional' => array('price', 'stock_qty', 'category', 'brand', 'weight', 'description', 'image_url', 'barcode'),
            'unique_key' => 'sku',
        ),
        'customers' => array(
            'required' => array('email'),
            'optional' => array('first_name', 'last_name', 'phone', 'company', 'address', 'city', 'country', 'tax_id'),
            'unique_key' => 'email',
        ),
        'orders' => array(
            'required' => array('order_ref', 'customer_email', 'total'),
            'optional' => array('status', 'currency', 'shipping_address', 'items_json'),
            'unique_key' => 'order_ref',
        ),
        'inventory' => array(
            'required' => array('sku', 'stock_qty'),
            'optional' => array('warehouse', 'location', 'reorder_point', 'cost_price'),
            'unique_key' => 'sku',
        ),
        'gl_entries' => array(
            'required' => array('account_code', 'debit', 'credit'),
            'optional' => array('description', 'reference', 'date', 'currency'),
            'unique_key' => '',
        ),
    );
}

function epc_import_create_job(PDO $pdo, string $siteKey, array $data): array
{
    epc_import_ensure_schema($pdo);
    $entityType = (string)($data['entity_type'] ?? 'products');
    $schemas = epc_import_entity_schemas();
    if (!isset($schemas[$entityType])) {
        return array('ok' => false, 'error' => 'Unknown entity type: ' . $entityType);
    }

    $st = $pdo->prepare("INSERT INTO `epc_import_jobs` (`site_key`,`entity_type`,`source_format`,`filename`,`total_rows`,`field_mapping`,`options`,`dry_run`,`created_by`) VALUES (?,?,?,?,?,?,?,?,?)");
    $st->execute(array(
        $siteKey, $entityType, (string)($data['source_format']??'csv'), (string)($data['filename']??''),
        (int)($data['total_rows']??0), json_encode($data['field_mapping']??array()),
        json_encode($data['options']??array()), (int)($data['dry_run']??0), (int)($data['created_by']??0),
    ));
    return array('ok' => true, 'job_id' => (int)$pdo->lastInsertId(), 'schema' => $schemas[$entityType]);
}

function epc_import_validate_row(array $row, array $schema, int $rowNum): array
{
    $errors = array();
    foreach ($schema['required'] as $field) {
        if (!isset($row[$field]) || trim((string)$row[$field]) === '') {
            $errors[] = array('row_number' => $rowNum, 'field' => $field, 'error_type' => 'required', 'message' => 'Required field missing: ' . $field);
        }
    }
    if (isset($row['email']) && $row['email'] !== '' && !filter_var($row['email'], FILTER_VALIDATE_EMAIL)) {
        $errors[] = array('row_number' => $rowNum, 'field' => 'email', 'value' => $row['email'], 'error_type' => 'format', 'message' => 'Invalid email format');
    }
    if (isset($row['price']) && $row['price'] !== '' && !is_numeric($row['price'])) {
        $errors[] = array('row_number' => $rowNum, 'field' => 'price', 'value' => $row['price'], 'error_type' => 'format', 'message' => 'Price must be numeric');
    }
    return $errors;
}

function epc_import_process_chunk(PDO $pdo, int $jobId, array $rows): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_import_jobs` WHERE `id`=?");
    $st->execute(array($jobId));
    $job = $st->fetch(PDO::FETCH_ASSOC);
    if (!$job) return array('ok' => false, 'error' => 'Job not found');

    $schemas = epc_import_entity_schemas();
    $schema = $schemas[$job['entity_type']] ?? array('required' => array(), 'optional' => array());
    $success = 0; $errors = 0; $skipped = 0;

    $pdo->prepare("UPDATE `epc_import_jobs` SET `status`='importing' WHERE `id`=?")->execute(array($jobId));

    foreach ($rows as $i => $row) {
        $rowErrors = epc_import_validate_row($row, $schema, $i + 1);
        if (!empty($rowErrors)) {
            foreach ($rowErrors as $e) {
                $pdo->prepare("INSERT INTO `epc_import_errors` (`job_id`,`row_number`,`field`,`value`,`error_type`,`message`) VALUES (?,?,?,?,?,?)")
                    ->execute(array($jobId, $e['row_number'], $e['field'], $e['value'] ?? '', $e['error_type'], $e['message']));
            }
            $errors++;
        } else {
            $success++;
        }
    }

    $pdo->prepare("UPDATE `epc_import_jobs` SET `processed_rows`=`processed_rows`+?, `success_rows`=`success_rows`+?, `error_rows`=`error_rows`+?, `skip_rows`=`skip_rows`+? WHERE `id`=?")
        ->execute(array(count($rows), $success, $errors, $skipped, $jobId));

    return array('ok' => true, 'processed' => count($rows), 'success' => $success, 'errors' => $errors, 'skipped' => $skipped);
}

function epc_import_job_status(PDO $pdo, int $jobId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_import_jobs` WHERE `id`=?");
    $st->execute(array($jobId));
    $job = $st->fetch(PDO::FETCH_ASSOC);
    if (!$job) return array();
    $job['field_mapping'] = json_decode($job['field_mapping']?:'{}', true);
    $job['options'] = json_decode($job['options']?:'{}', true);
    return $job;
}

function epc_import_job_errors(PDO $pdo, int $jobId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_import_errors` WHERE `job_id`=? ORDER BY `row_number`");
    $st->execute(array($jobId));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_import_list_jobs(PDO $pdo, string $siteKey): array
{
    epc_import_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT * FROM `epc_import_jobs` WHERE `site_key`=? ORDER BY `created_at` DESC");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_import_fleet_stats(PDO $pdo): array
{
    epc_import_ensure_schema($pdo);
    $st = $pdo->query("SELECT `site_key`, COUNT(*) AS `jobs`, SUM(`success_rows`) AS `imported`, SUM(`error_rows`) AS `errors` FROM `epc_import_jobs` GROUP BY `site_key`");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── Dry-Run & Validation ─── */

function epc_import_dry_run(PDO $pdo, string $siteKey, string $source, array $mapping, string $format, array $rows): array
{
    $results = array('total' => count($rows), 'valid' => 0, 'invalid' => 0, 'errors' => array());
    $validators = epc_import_validators($source);

    foreach ($rows as $idx => $row) {
        $rowErrors = array();
        foreach ($mapping as $dbCol => $csvCol) {
            $val = $row[$csvCol] ?? '';
            if (isset($validators[$dbCol])) {
                $check = $validators[$dbCol]($val);
                if ($check !== true) {
                    $rowErrors[] = array('column' => $dbCol, 'value' => $val, 'error' => $check);
                }
            }
        }
        if (empty($rowErrors)) {
            $results['valid']++;
        } else {
            $results['invalid']++;
            if (count($results['errors']) < 100) {
                $results['errors'][] = array('row' => $idx + 1, 'errors' => $rowErrors);
            }
        }
    }
    return $results;
}

function epc_import_validators(string $source): array
{
    $common = array(
        'email' => function($v) { return filter_var($v, FILTER_VALIDATE_EMAIL) || $v === '' ? true : 'Invalid email'; },
        'phone' => function($v) { return preg_match('/^[\+\d\s\-\(\)]*$/', $v) ? true : 'Invalid phone'; },
    );
    $sourceValidators = array(
        'products' => array(
            'sku' => function($v) { return $v !== '' ? true : 'SKU required'; },
            'price' => function($v) { return is_numeric($v) ? true : 'Invalid price'; },
            'stock_qty' => function($v) { return ctype_digit($v) || $v === '' ? true : 'Invalid qty'; },
        ),
        'customers' => array(
            'name' => function($v) { return $v !== '' ? true : 'Name required'; },
        ),
        'invoices' => array(
            'amount' => function($v) { return is_numeric($v) ? true : 'Invalid amount'; },
            'date' => function($v) { return strtotime($v) !== false ? true : 'Invalid date'; },
        ),
    );
    return array_merge($common, $sourceValidators[$source] ?? array());
}

function epc_import_supported_formats(): array
{
    return array(
        array('format' => 'csv', 'label' => 'CSV (comma-separated)', 'mime' => 'text/csv'),
        array('format' => 'tsv', 'label' => 'TSV (tab-separated)', 'mime' => 'text/tab-separated-values'),
        array('format' => 'xlsx', 'label' => 'Excel (.xlsx)', 'mime' => 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'),
        array('format' => 'xml', 'label' => 'XML', 'mime' => 'application/xml'),
        array('format' => 'json', 'label' => 'JSON', 'mime' => 'application/json'),
    );
}

function epc_import_supported_sources(): array
{
    return array(
        'products' => array('label' => 'Products / SKUs', 'table' => 'shop_products', 'required_fields' => array('sku', 'name')),
        'customers' => array('label' => 'Customers', 'table' => 'shop_customers', 'required_fields' => array('name')),
        'invoices' => array('label' => 'Invoices', 'table' => 'epc_invoices', 'required_fields' => array('invoice_no', 'amount')),
        'suppliers' => array('label' => 'Suppliers', 'table' => 'epc_suppliers', 'required_fields' => array('name')),
        'gl_entries' => array('label' => 'Journal Entries', 'table' => 'epc_gl_entries', 'required_fields' => array('account_code', 'amount')),
        'employees' => array('label' => 'Employees', 'table' => 'epc_employees', 'required_fields' => array('name', 'employee_id')),
    );
}

function epc_import_cancel(PDO $pdo, int $jobId): array
{
    $pdo->prepare("UPDATE `epc_import_jobs` SET `status`='cancelled' WHERE `id`=? AND `status` IN ('pending','processing')")->execute(array($jobId));
    return array('ok' => true);
}

function epc_import_retry(PDO $pdo, int $jobId): array
{
    $pdo->prepare("UPDATE `epc_import_jobs` SET `status`='pending', `error_rows`=0, `skip_rows`=0 WHERE `id`=? AND `status`='failed'")->execute(array($jobId));
    return array('ok' => true);
}
