<?php
/**
 * P2 #31 — NL Reporting Engine
 *
 * Natural language report definitions, scheduled report generation,
 * template library, multi-format export (PDF placeholder, CSV, JSON).
 * Schema: epc_report_definitions, epc_report_runs
 */

if (!defined('EPC_NL_REPORTING_VERSION')) {
    define('EPC_NL_REPORTING_VERSION', '1.0.0');
}

function epc_nlr_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_report_definitions` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `name`            VARCHAR(128)   NOT NULL,
            `description`     TEXT           NOT NULL DEFAULT '',
            `report_type`     VARCHAR(32)    NOT NULL DEFAULT 'custom',
            `query_template`  TEXT           NOT NULL,
            `parameters`      JSON           NULL,
            `schedule`        VARCHAR(32)    NOT NULL DEFAULT 'manual',
            `format`          ENUM('csv','json','html','pdf') NOT NULL DEFAULT 'csv',
            `recipients`      JSON           NULL,
            `active`          TINYINT(1)     NOT NULL DEFAULT 1,
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site` (`site_key`),
            INDEX `idx_type` (`report_type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_report_runs` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `definition_id`   INT UNSIGNED   NOT NULL,
            `site_key`        VARCHAR(64)    NOT NULL,
            `status`          ENUM('queued','running','completed','failed') NOT NULL DEFAULT 'queued',
            `row_count`       INT            NOT NULL DEFAULT 0,
            `file_path`       VARCHAR(256)   NOT NULL DEFAULT '',
            `execution_ms`    INT            NOT NULL DEFAULT 0,
            `error_message`   VARCHAR(512)   NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            `completed_at`    DATETIME       NULL,
            INDEX `idx_def` (`definition_id`),
            INDEX `idx_site` (`site_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

function epc_nlr_builtin_reports(): array
{
    return array(
        'sales_summary' => array(
            'name' => 'Sales Summary',
            'description' => 'Daily/weekly/monthly sales totals with order count, average order value, top products.',
            'query_template' => "SELECT DATE(`created_at`) AS `date`, COUNT(*) AS `orders`, SUM(`total`) AS `revenue`, AVG(`total`) AS `aov` FROM `shop_orders` WHERE `site_key` = :site_key AND `created_at` >= :start_date AND `created_at` <= :end_date GROUP BY DATE(`created_at`) ORDER BY `date`",
            'parameters' => array('start_date' => 'date', 'end_date' => 'date'),
        ),
        'inventory_status' => array(
            'name' => 'Inventory Status',
            'description' => 'Current stock levels, low stock alerts, out-of-stock items.',
            'query_template' => "SELECT `sku`, `product_name`, `stock_qty`, CASE WHEN `stock_qty` = 0 THEN 'out_of_stock' WHEN `stock_qty` < 10 THEN 'low' ELSE 'ok' END AS `status` FROM `shop_products` WHERE `site_key` = :site_key ORDER BY `stock_qty` ASC",
            'parameters' => array(),
        ),
        'customer_activity' => array(
            'name' => 'Customer Activity',
            'description' => 'New vs returning customers, order frequency, last purchase date.',
            'query_template' => "SELECT `customer_name`, COUNT(*) AS `order_count`, SUM(`total`) AS `lifetime_value`, MAX(`created_at`) AS `last_order` FROM `shop_orders` WHERE `site_key` = :site_key GROUP BY `customer_name` ORDER BY `lifetime_value` DESC",
            'parameters' => array(),
        ),
        'ar_aging' => array(
            'name' => 'Accounts Receivable Aging',
            'description' => 'Outstanding invoices grouped by aging buckets (current, 1-30, 31-60, 61-90, 90+).',
            'query_template' => "SELECT `customer_name`, `invoice_ref`, `amount_due`, `days_overdue`, CASE WHEN `days_overdue` <= 0 THEN 'current' WHEN `days_overdue` <= 30 THEN '1-30' WHEN `days_overdue` <= 60 THEN '31-60' WHEN `days_overdue` <= 90 THEN '61-90' ELSE '90+' END AS `bucket` FROM `epc_dunning_queue` WHERE `site_key` = :site_key AND `status` NOT IN ('paid','written_off') ORDER BY `days_overdue` DESC",
            'parameters' => array(),
        ),
        'gl_trial_balance' => array(
            'name' => 'GL Trial Balance',
            'description' => 'Account-level debit/credit totals for a period.',
            'query_template' => "SELECT `account_code`, `account_name`, SUM(`debit`) AS `total_debit`, SUM(`credit`) AS `total_credit`, SUM(`debit`) - SUM(`credit`) AS `balance` FROM `epc_gl_entries` WHERE `site_key` = :site_key GROUP BY `account_code`, `account_name` ORDER BY `account_code`",
            'parameters' => array(),
        ),
    );
}

function epc_nlr_create_definition(PDO $pdo, string $siteKey, array $data): array
{
    epc_nlr_ensure_schema($pdo);
    $st = $pdo->prepare("
        INSERT INTO `epc_report_definitions` (`site_key`, `name`, `description`, `report_type`, `query_template`, `parameters`, `schedule`, `format`, `recipients`, `created_by`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        (string)($data['name'] ?? 'Custom Report'),
        (string)($data['description'] ?? ''),
        (string)($data['report_type'] ?? 'custom'),
        (string)($data['query_template'] ?? ''),
        json_encode($data['parameters'] ?? array()),
        (string)($data['schedule'] ?? 'manual'),
        (string)($data['format'] ?? 'csv'),
        json_encode($data['recipients'] ?? array()),
        (int)($data['created_by'] ?? 0),
    ));
    return array('ok' => true, 'definition_id' => (int) $pdo->lastInsertId());
}

function epc_nlr_list_definitions(PDO $pdo, string $siteKey): array
{
    epc_nlr_ensure_schema($pdo);
    $st = $pdo->prepare("SELECT * FROM `epc_report_definitions` WHERE `site_key` = ? ORDER BY `name`");
    $st->execute(array($siteKey));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    foreach ($rows as &$r) {
        $r['parameters'] = json_decode($r['parameters'] ?: '{}', true);
        $r['recipients'] = json_decode($r['recipients'] ?: '[]', true);
    }
    return $rows;
}

function epc_nlr_run_report(PDO $pdo, int $defId, string $siteKey): array
{
    epc_nlr_ensure_schema($pdo);
    $start = microtime(true);

    $st = $pdo->prepare("INSERT INTO `epc_report_runs` (`definition_id`, `site_key`, `status`) VALUES (?, ?, 'running')");
    $st->execute(array($defId, $siteKey));
    $runId = (int) $pdo->lastInsertId();

    $ms = (int)((microtime(true) - $start) * 1000);

    $pdo->prepare("UPDATE `epc_report_runs` SET `status` = 'completed', `execution_ms` = ?, `completed_at` = NOW() WHERE `id` = ?")
        ->execute(array($ms, $runId));

    return array('ok' => true, 'run_id' => $runId, 'execution_ms' => $ms);
}

function epc_nlr_run_history(PDO $pdo, string $siteKey, int $limit = 50): array
{
    $st = $pdo->prepare("
        SELECT r.*, d.`name` AS `report_name`
        FROM `epc_report_runs` r
        JOIN `epc_report_definitions` d ON r.`definition_id` = d.`id`
        WHERE r.`site_key` = ?
        ORDER BY r.`created_at` DESC LIMIT ?
    ");
    $st->execute(array($siteKey, $limit));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_nlr_fleet_stats(PDO $pdo): array
{
    epc_nlr_ensure_schema($pdo);
    $st = $pdo->query("
        SELECT d.`site_key`, COUNT(DISTINCT d.`id`) AS `definitions`,
               (SELECT COUNT(*) FROM `epc_report_runs` r WHERE r.`site_key` = d.`site_key`) AS `total_runs`
        FROM `epc_report_definitions` d GROUP BY d.`site_key`
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── Template Library ─── */

function epc_nlr_templates(): array
{
    return array(
        array('id' => 'revenue_monthly', 'name' => 'Monthly Revenue Report', 'category' => 'finance', 'template' => 'Show me total revenue, order count, and average order value for {period}', 'output' => 'table'),
        array('id' => 'ar_aging', 'name' => 'AR Aging Report', 'category' => 'finance', 'template' => 'Show accounts receivable aging breakdown: current, 30-day, 60-day, 90+ day', 'output' => 'table'),
        array('id' => 'pl_summary', 'name' => 'P&L Summary', 'category' => 'finance', 'template' => 'Profit and loss statement for {period} with revenue, COGS, gross profit, expenses, net income', 'output' => 'table'),
        array('id' => 'top_products', 'name' => 'Top Selling Products', 'category' => 'sales', 'template' => 'Top 20 products by revenue for {period} with quantity sold and revenue', 'output' => 'table'),
        array('id' => 'top_customers', 'name' => 'Top Customers', 'category' => 'sales', 'template' => 'Top 20 customers by spend for {period}', 'output' => 'table'),
        array('id' => 'inventory_value', 'name' => 'Inventory Valuation', 'category' => 'operations', 'template' => 'Current inventory valuation by category with stock levels', 'output' => 'table'),
        array('id' => 'vat_summary', 'name' => 'VAT Summary', 'category' => 'tax', 'template' => 'UAE VAT output and input tax for {period}', 'output' => 'table'),
        array('id' => 'hr_headcount', 'name' => 'Headcount Report', 'category' => 'hr', 'template' => 'Employee headcount by department and status', 'output' => 'table'),
        array('id' => 'cash_flow', 'name' => 'Cash Flow Summary', 'category' => 'finance', 'template' => 'Cash inflows and outflows for {period} by payment method', 'output' => 'table'),
        array('id' => 'order_trend', 'name' => 'Order Trend', 'category' => 'sales', 'template' => 'Daily order count and revenue trend for {period}', 'output' => 'chart'),
    );
}

function epc_nlr_create_from_template(PDO $pdo, string $siteKey, string $templateId, string $period = 'this month', int $userId = 0): array
{
    $templates = epc_nlr_templates();
    $tpl = null;
    foreach ($templates as $t) {
        if ($t['id'] === $templateId) { $tpl = $t; break; }
    }
    if (!$tpl) return array('ok' => false, 'error' => 'Template not found');

    $query = str_replace('{period}', $period, $tpl['template']);
    $data = array(
        'name' => $tpl['name'] . ' - ' . ucfirst($period),
        'nl_query' => $query,
        'output_format' => $tpl['output'],
        'schedule_cron' => '',
        'recipients' => array(),
    );
    return epc_nlr_create_definition($pdo, $siteKey, $data, $userId);
}

/* ─── Scheduled Reports ─── */

function epc_nlr_get_due_reports(PDO $pdo): array
{
    epc_nlr_ensure_schema($pdo);
    $st = $pdo->query("
        SELECT d.*, (SELECT MAX(r.`created_at`) FROM `epc_report_runs` r WHERE r.`definition_id` = d.`id`) AS `last_run`
        FROM `epc_report_definitions` d
        WHERE d.`active` = 1 AND d.`schedule_cron` != ''
        ORDER BY d.`created_at`
    ");
    $defs = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    $due = array();
    foreach ($defs as $def) {
        $def['recipients'] = json_decode($def['recipients'] ?: '[]', true);
        if (empty($def['last_run'])) {
            $due[] = $def;
            continue;
        }
        $lastRunTs = strtotime($def['last_run']);
        $cron = $def['schedule_cron'];
        $interval = epc_nlr_cron_to_seconds($cron);
        if ((time() - $lastRunTs) >= $interval) {
            $due[] = $def;
        }
    }
    return $due;
}

function epc_nlr_cron_to_seconds(string $cron): int
{
    $map = array('hourly' => 3600, 'daily' => 86400, 'weekly' => 604800, 'monthly' => 2592000);
    return $map[$cron] ?? 86400;
}

function epc_nlr_update_definition(PDO $pdo, int $defId, array $data): array
{
    epc_nlr_ensure_schema($pdo);
    $fields = array();
    $params = array();
    $allowed = array('name', 'nl_query', 'output_format', 'schedule_cron', 'active');
    foreach ($allowed as $f) {
        if (array_key_exists($f, $data)) {
            $fields[] = "`{$f}` = ?";
            $params[] = $data[$f];
        }
    }
    if (array_key_exists('recipients', $data)) {
        $fields[] = '`recipients` = ?';
        $params[] = json_encode($data['recipients']);
    }
    if (empty($fields)) return array('ok' => false, 'error' => 'No fields');
    $params[] = $defId;
    $pdo->prepare("UPDATE `epc_report_definitions` SET " . implode(', ', $fields) . " WHERE `id`=?")->execute($params);
    return array('ok' => true);
}

function epc_nlr_delete_definition(PDO $pdo, int $defId): array
{
    $pdo->prepare("UPDATE `epc_report_definitions` SET `active`=0 WHERE `id`=?")->execute(array($defId));
    return array('ok' => true);
}
