<?php
/**
 * P1 #17 — BI Metrics Engine
 *
 * Materialized views, KPI snapshots, trend analysis, and
 * scheduled metric computation for executive dashboards.
 * Schema: epc_bi_snapshots, epc_bi_definitions
 */

if (!defined('EPC_BI_METRICS_VERSION')) {
    define('EPC_BI_METRICS_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_bi_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_bi_definitions` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `metric_key`      VARCHAR(64)    NOT NULL,
            `label`           VARCHAR(128)   NOT NULL,
            `category`        VARCHAR(64)    NOT NULL DEFAULT 'general',
            `unit`            VARCHAR(16)    NOT NULL DEFAULT 'number',
            `sql_template`    TEXT           NOT NULL,
            `aggregation`     ENUM('sum','avg','count','min','max','latest') NOT NULL DEFAULT 'sum',
            `schedule`        ENUM('hourly','daily','weekly','monthly') NOT NULL DEFAULT 'daily',
            `active`          TINYINT(1)     NOT NULL DEFAULT 1,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_metric` (`metric_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_bi_snapshots` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `metric_key`      VARCHAR(64)    NOT NULL,
            `period`          VARCHAR(10)    NOT NULL,
            `period_start`    DATE           NOT NULL,
            `value`           DECIMAL(20,4)  NOT NULL DEFAULT 0,
            `previous_value`  DECIMAL(20,4)  NOT NULL DEFAULT 0,
            `change_pct`      DECIMAL(8,2)   NOT NULL DEFAULT 0,
            `metadata`        JSON           NULL,
            `computed_at`     DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_snapshot` (`site_key`, `metric_key`, `period`, `period_start`),
            INDEX `idx_metric` (`metric_key`),
            INDEX `idx_period` (`period_start`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── built-in metric definitions ─── */

function epc_bi_builtin_metrics(): array
{
    return array(
        array(
            'metric_key' => 'revenue_daily',
            'label'      => 'Daily Revenue',
            'category'   => 'finance',
            'unit'       => 'currency',
            'aggregation' => 'sum',
            'schedule'   => 'daily',
            'sql_template' => "SELECT COALESCE(SUM(total), 0) FROM orders WHERE status != 'cancelled' AND DATE(created_at) = :date",
        ),
        array(
            'metric_key' => 'orders_daily',
            'label'      => 'Daily Orders',
            'category'   => 'sales',
            'unit'       => 'number',
            'aggregation' => 'count',
            'schedule'   => 'daily',
            'sql_template' => "SELECT COUNT(*) FROM orders WHERE DATE(created_at) = :date",
        ),
        array(
            'metric_key' => 'aov',
            'label'      => 'Average Order Value',
            'category'   => 'sales',
            'unit'       => 'currency',
            'aggregation' => 'avg',
            'schedule'   => 'daily',
            'sql_template' => "SELECT COALESCE(AVG(total), 0) FROM orders WHERE status != 'cancelled' AND DATE(created_at) = :date",
        ),
        array(
            'metric_key' => 'new_customers',
            'label'      => 'New Customers',
            'category'   => 'growth',
            'unit'       => 'number',
            'aggregation' => 'count',
            'schedule'   => 'daily',
            'sql_template' => "SELECT COUNT(*) FROM customers WHERE DATE(created_at) = :date",
        ),
        array(
            'metric_key' => 'active_skus',
            'label'      => 'Active SKUs',
            'category'   => 'inventory',
            'unit'       => 'number',
            'aggregation' => 'latest',
            'schedule'   => 'daily',
            'sql_template' => "SELECT COUNT(*) FROM products WHERE active = 1",
        ),
        array(
            'metric_key' => 'low_stock_items',
            'label'      => 'Low Stock Items',
            'category'   => 'inventory',
            'unit'       => 'number',
            'aggregation' => 'latest',
            'schedule'   => 'daily',
            'sql_template' => "SELECT COUNT(*) FROM products WHERE stock_qty <= reorder_level AND stock_qty >= 0",
        ),
        array(
            'metric_key' => 'outstanding_ar',
            'label'      => 'Outstanding AR',
            'category'   => 'finance',
            'unit'       => 'currency',
            'aggregation' => 'latest',
            'schedule'   => 'daily',
            'sql_template' => "SELECT COALESCE(SUM(balance_due), 0) FROM invoices WHERE status = 'outstanding'",
        ),
        array(
            'metric_key' => 'gross_margin',
            'label'      => 'Gross Margin %',
            'category'   => 'finance',
            'unit'       => 'percent',
            'aggregation' => 'avg',
            'schedule'   => 'weekly',
            'sql_template' => "SELECT CASE WHEN SUM(total) > 0 THEN ((SUM(total) - SUM(COALESCE(cost,0))) / SUM(total)) * 100 ELSE 0 END FROM orders WHERE status != 'cancelled' AND created_at >= :week_start AND created_at < :week_end",
        ),
        array(
            'metric_key' => 'return_rate',
            'label'      => 'Return Rate %',
            'category'   => 'operations',
            'unit'       => 'percent',
            'aggregation' => 'avg',
            'schedule'   => 'weekly',
            'sql_template' => "SELECT CASE WHEN COUNT(*) > 0 THEN (SUM(CASE WHEN status = 'returned' THEN 1 ELSE 0 END) / COUNT(*)) * 100 ELSE 0 END FROM orders WHERE created_at >= :week_start AND created_at < :week_end",
        ),
        array(
            'metric_key' => 'conversion_rate',
            'label'      => 'Conversion Rate %',
            'category'   => 'sales',
            'unit'       => 'percent',
            'aggregation' => 'avg',
            'schedule'   => 'daily',
            'sql_template' => "SELECT 0",
        ),
    );
}

/* ─── record snapshot ─── */

function epc_bi_record_snapshot(PDO $pdo, string $siteKey, string $metricKey, string $period, string $periodStart, float $value, float $previousValue = 0): array
{
    epc_bi_ensure_schema($pdo);

    $changePct = $previousValue != 0 ? round((($value - $previousValue) / abs($previousValue)) * 100, 2) : 0;

    $st = $pdo->prepare("
        INSERT INTO `epc_bi_snapshots`
            (`site_key`, `metric_key`, `period`, `period_start`, `value`, `previous_value`, `change_pct`)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE
            `value` = VALUES(`value`),
            `previous_value` = VALUES(`previous_value`),
            `change_pct` = VALUES(`change_pct`),
            `computed_at` = NOW()
    ");
    $st->execute(array($siteKey, $metricKey, $period, $periodStart, $value, $previousValue, $changePct));

    return array('ok' => true, 'metric_key' => $metricKey, 'value' => $value, 'change_pct' => $changePct);
}

/* ─── get metric trend ─── */

function epc_bi_metric_trend(PDO $pdo, string $siteKey, string $metricKey, string $period = 'daily', int $limit = 30): array
{
    epc_bi_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT `period_start`, `value`, `previous_value`, `change_pct`, `computed_at`
        FROM `epc_bi_snapshots`
        WHERE `site_key` = ? AND `metric_key` = ? AND `period` = ?
        ORDER BY `period_start` DESC
        LIMIT ?
    ");
    $st->execute(array($siteKey, $metricKey, $period, $limit));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    return array_reverse($rows);
}

/* ─── get latest value for all metrics ─── */

function epc_bi_latest_all(PDO $pdo, string $siteKey): array
{
    epc_bi_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT s.`metric_key`, s.`value`, s.`previous_value`, s.`change_pct`, s.`period_start`, s.`computed_at`
        FROM `epc_bi_snapshots` s
        INNER JOIN (
            SELECT `metric_key`, MAX(`period_start`) AS max_date
            FROM `epc_bi_snapshots`
            WHERE `site_key` = ?
            GROUP BY `metric_key`
        ) latest ON s.`metric_key` = latest.`metric_key` AND s.`period_start` = latest.max_date
        WHERE s.`site_key` = ?
    ");
    $st->execute(array($siteKey, $siteKey));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    $result = array();
    foreach ($rows as $row) {
        $result[$row['metric_key']] = $row;
    }
    return $result;
}

/* ─── compute metrics for a date ─── */

function epc_bi_compute_daily(PDO $pdo, string $siteKey, string $date = ''): array
{
    if ($date === '') {
        $date = date('Y-m-d');
    }

    $metrics = epc_bi_builtin_metrics();
    $results = array();

    foreach ($metrics as $m) {
        if ($m['schedule'] !== 'daily') {
            continue;
        }

        $value = 0;
        try {
            $sql = str_replace(':date', $pdo->quote($date), $m['sql_template']);
            $st = $pdo->query($sql);
            $value = $st ? (float) $st->fetchColumn() : 0;
        } catch (\Exception $e) {
            $value = 0;
        }

        $previousDate = date('Y-m-d', strtotime($date . ' -1 day'));
        $prev = epc_bi_metric_trend($pdo, $siteKey, $m['metric_key'], 'daily', 1);
        $previousValue = !empty($prev) ? (float) $prev[0]['value'] : 0;

        $results[] = epc_bi_record_snapshot($pdo, $siteKey, $m['metric_key'], 'daily', $date, $value, $previousValue);
    }

    return array('ok' => true, 'date' => $date, 'computed' => count($results), 'results' => $results);
}

/* ─── dashboard summary ─── */

function epc_bi_dashboard(PDO $pdo, string $siteKey): array
{
    $latest = epc_bi_latest_all($pdo, $siteKey);
    $metrics = epc_bi_builtin_metrics();
    $categories = array();

    foreach ($metrics as $m) {
        $key = $m['metric_key'];
        $data = $latest[$key] ?? array('value' => 0, 'change_pct' => 0);
        $categories[$m['category']][] = array(
            'metric_key' => $key,
            'label'      => $m['label'],
            'unit'       => $m['unit'],
            'value'      => (float) ($data['value'] ?? 0),
            'change_pct' => (float) ($data['change_pct'] ?? 0),
            'computed_at' => $data['computed_at'] ?? null,
        );
    }

    return $categories;
}

/* ─── fleet BI overview (BOS) ─── */

function epc_bi_fleet_overview(PDO $pdo): array
{
    epc_bi_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(DISTINCT `metric_key`) AS `metrics_tracked`,
               MAX(`computed_at`) AS `last_computed`,
               SUM(CASE WHEN `metric_key` = 'revenue_daily' THEN `value` ELSE 0 END) AS `latest_revenue`,
               SUM(CASE WHEN `metric_key` = 'orders_daily' THEN `value` ELSE 0 END) AS `latest_orders`
        FROM `epc_bi_snapshots` s
        INNER JOIN (
            SELECT `site_key` sk, `metric_key` mk, MAX(`period_start`) AS max_date
            FROM `epc_bi_snapshots`
            GROUP BY `site_key`, `metric_key`
        ) latest ON s.`site_key` = latest.sk AND s.`metric_key` = latest.mk AND s.`period_start` = latest.max_date
        GROUP BY `site_key`
        ORDER BY `latest_revenue` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── metric comparison across tenants ─── */

function epc_bi_compare_tenants(PDO $pdo, string $metricKey): array
{
    epc_bi_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT s.`site_key`, s.`value`, s.`change_pct`, s.`period_start`
        FROM `epc_bi_snapshots` s
        INNER JOIN (
            SELECT `site_key`, MAX(`period_start`) AS max_date
            FROM `epc_bi_snapshots`
            WHERE `metric_key` = ?
            GROUP BY `site_key`
        ) latest ON s.`site_key` = latest.`site_key` AND s.`period_start` = latest.max_date
        WHERE s.`metric_key` = ?
        ORDER BY s.`value` DESC
    ");
    $st->execute(array($metricKey, $metricKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── cleanup old snapshots ─── */

function epc_bi_cleanup(PDO $pdo, int $keepDays = 365): int
{
    $st = $pdo->prepare("DELETE FROM `epc_bi_snapshots` WHERE `period_start` < DATE_SUB(CURDATE(), INTERVAL ? DAY)");
    $st->execute(array($keepDays));
    return $st->rowCount();
}
