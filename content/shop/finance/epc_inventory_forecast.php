<?php
/**
 * P1 #22 — Inventory Forecasting
 *
 * Demand planning, reorder point calculation, safety stock,
 * lead time tracking, and automated purchase suggestions.
 * Schema: epc_inventory_forecast, epc_demand_history
 */

if (!defined('EPC_INVENTORY_FORECAST_VERSION')) {
    define('EPC_INVENTORY_FORECAST_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_forecast_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_demand_history` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `sku`             VARCHAR(64)    NOT NULL,
            `period`          DATE           NOT NULL,
            `qty_sold`        INT            NOT NULL DEFAULT 0,
            `qty_returned`    INT            NOT NULL DEFAULT 0,
            `revenue`         DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            UNIQUE KEY `uk_demand` (`site_key`, `sku`, `period`),
            INDEX `idx_sku` (`sku`),
            INDEX `idx_period` (`period`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_inventory_forecast` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `sku`             VARCHAR(64)    NOT NULL,
            `product_name`    VARCHAR(255)   NOT NULL DEFAULT '',
            `current_stock`   INT            NOT NULL DEFAULT 0,
            `avg_daily_demand`DECIMAL(8,2)   NOT NULL DEFAULT 0.00,
            `lead_time_days`  INT UNSIGNED   NOT NULL DEFAULT 7,
            `safety_stock`    INT            NOT NULL DEFAULT 0,
            `reorder_point`   INT            NOT NULL DEFAULT 0,
            `eoq`             INT            NOT NULL DEFAULT 0,
            `days_of_stock`   INT            NOT NULL DEFAULT 0,
            `stockout_date`   DATE           NULL,
            `forecast_status` ENUM('healthy','low','critical','stockout') NOT NULL DEFAULT 'healthy',
            `last_computed`   DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_forecast` (`site_key`, `sku`),
            INDEX `idx_status` (`forecast_status`),
            INDEX `idx_stockout` (`stockout_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── record demand ─── */

function epc_forecast_record_demand(PDO $pdo, string $siteKey, string $sku, string $date, int $qtySold, float $revenue = 0, int $qtyReturned = 0): bool
{
    epc_forecast_ensure_schema($pdo);

    $st = $pdo->prepare("
        INSERT INTO `epc_demand_history` (`site_key`, `sku`, `period`, `qty_sold`, `qty_returned`, `revenue`)
        VALUES (?, ?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE
            `qty_sold` = `qty_sold` + VALUES(`qty_sold`),
            `qty_returned` = `qty_returned` + VALUES(`qty_returned`),
            `revenue` = `revenue` + VALUES(`revenue`)
    ");
    return $st->execute(array($siteKey, $sku, $date, $qtySold, $qtyReturned, $revenue));
}

/* ─── compute average daily demand ─── */

function epc_forecast_avg_demand(PDO $pdo, string $siteKey, string $sku, int $days = 90): float
{
    $st = $pdo->prepare("
        SELECT COALESCE(SUM(`qty_sold` - `qty_returned`), 0) / ?
        FROM `epc_demand_history`
        WHERE `site_key` = ? AND `sku` = ? AND `period` >= DATE_SUB(CURDATE(), INTERVAL ? DAY)
    ");
    $st->execute(array($days, $siteKey, $sku, $days));
    return round((float) $st->fetchColumn(), 2);
}

/* ─── compute safety stock ─── */

function epc_forecast_safety_stock(float $avgDailyDemand, int $leadTimeDays, float $serviceFactor = 1.65): int
{
    return (int) ceil($serviceFactor * sqrt((float) $leadTimeDays) * $avgDailyDemand);
}

/* ─── compute reorder point ─── */

function epc_forecast_reorder_point(float $avgDailyDemand, int $leadTimeDays, int $safetyStock): int
{
    return (int) ceil(($avgDailyDemand * $leadTimeDays) + $safetyStock);
}

/* ─── economic order quantity (EOQ) ─── */

function epc_forecast_eoq(float $annualDemand, float $orderCost = 50, float $holdingCost = 5): int
{
    if ($annualDemand <= 0 || $holdingCost <= 0) {
        return 0;
    }
    return (int) ceil(sqrt((2 * $annualDemand * $orderCost) / $holdingCost));
}

/* ─── compute forecast for a SKU ─── */

function epc_forecast_compute(PDO $pdo, string $siteKey, string $sku, int $currentStock, string $productName = '', int $leadTimeDays = 7): array
{
    epc_forecast_ensure_schema($pdo);

    $avgDaily = epc_forecast_avg_demand($pdo, $siteKey, $sku, 90);
    $safetyStock = epc_forecast_safety_stock($avgDaily, $leadTimeDays);
    $reorderPoint = epc_forecast_reorder_point($avgDaily, $leadTimeDays, $safetyStock);
    $annualDemand = $avgDaily * 365;
    $eoq = epc_forecast_eoq($annualDemand);
    $daysOfStock = $avgDaily > 0 ? (int) floor($currentStock / $avgDaily) : 999;
    $stockoutDate = $avgDaily > 0 ? date('Y-m-d', strtotime('+' . $daysOfStock . ' days')) : null;

    $status = 'healthy';
    if ($currentStock <= 0) {
        $status = 'stockout';
    } elseif ($currentStock <= $safetyStock) {
        $status = 'critical';
    } elseif ($currentStock <= $reorderPoint) {
        $status = 'low';
    }

    $st = $pdo->prepare("
        INSERT INTO `epc_inventory_forecast`
            (`site_key`, `sku`, `product_name`, `current_stock`, `avg_daily_demand`,
             `lead_time_days`, `safety_stock`, `reorder_point`, `eoq`, `days_of_stock`,
             `stockout_date`, `forecast_status`, `last_computed`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, NOW())
        ON DUPLICATE KEY UPDATE
            `product_name` = VALUES(`product_name`),
            `current_stock` = VALUES(`current_stock`),
            `avg_daily_demand` = VALUES(`avg_daily_demand`),
            `lead_time_days` = VALUES(`lead_time_days`),
            `safety_stock` = VALUES(`safety_stock`),
            `reorder_point` = VALUES(`reorder_point`),
            `eoq` = VALUES(`eoq`),
            `days_of_stock` = VALUES(`days_of_stock`),
            `stockout_date` = VALUES(`stockout_date`),
            `forecast_status` = VALUES(`forecast_status`),
            `last_computed` = NOW()
    ");
    $st->execute(array($siteKey, $sku, $productName, $currentStock, $avgDaily, $leadTimeDays, $safetyStock, $reorderPoint, $eoq, $daysOfStock, $stockoutDate, $status));

    return array(
        'ok'               => true,
        'sku'              => $sku,
        'current_stock'    => $currentStock,
        'avg_daily_demand' => $avgDaily,
        'safety_stock'     => $safetyStock,
        'reorder_point'    => $reorderPoint,
        'eoq'              => $eoq,
        'days_of_stock'    => $daysOfStock,
        'stockout_date'    => $stockoutDate,
        'status'           => $status,
    );
}

/* ─── get purchase suggestions ─── */

function epc_forecast_purchase_suggestions(PDO $pdo, string $siteKey, int $limit = 50): array
{
    epc_forecast_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT * FROM `epc_inventory_forecast`
        WHERE `site_key` = ? AND `forecast_status` IN ('low', 'critical', 'stockout')
        ORDER BY FIELD(`forecast_status`, 'stockout', 'critical', 'low'), `days_of_stock` ASC
        LIMIT ?
    ");
    $st->execute(array($siteKey, $limit));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    foreach ($rows as &$row) {
        $row['suggested_qty'] = max((int) $row['eoq'], (int) $row['reorder_point'] - (int) $row['current_stock']);
    }
    return $rows;
}

/* ─── demand trend for a SKU ─── */

function epc_forecast_demand_trend(PDO $pdo, string $siteKey, string $sku, int $months = 6): array
{
    $st = $pdo->prepare("
        SELECT DATE_FORMAT(`period`, '%Y-%m') AS `month`,
               SUM(`qty_sold`) AS `sold`,
               SUM(`qty_returned`) AS `returned`,
               SUM(`revenue`) AS `revenue`
        FROM `epc_demand_history`
        WHERE `site_key` = ? AND `sku` = ? AND `period` >= DATE_SUB(CURDATE(), INTERVAL ? MONTH)
        GROUP BY DATE_FORMAT(`period`, '%Y-%m')
        ORDER BY `month` ASC
    ");
    $st->execute(array($siteKey, $sku, $months));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── fleet stats (BOS) ─── */

function epc_forecast_fleet_stats(PDO $pdo): array
{
    epc_forecast_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(*) AS `total_skus`,
               SUM(CASE WHEN `forecast_status` = 'stockout' THEN 1 ELSE 0 END) AS `stockout`,
               SUM(CASE WHEN `forecast_status` = 'critical' THEN 1 ELSE 0 END) AS `critical`,
               SUM(CASE WHEN `forecast_status` = 'low' THEN 1 ELSE 0 END) AS `low`,
               SUM(CASE WHEN `forecast_status` = 'healthy' THEN 1 ELSE 0 END) AS `healthy`,
               AVG(`days_of_stock`) AS `avg_days_stock`
        FROM `epc_inventory_forecast`
        GROUP BY `site_key`
        ORDER BY `stockout` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── upcoming stockouts across fleet ─── */

function epc_forecast_upcoming_stockouts(PDO $pdo, int $withinDays = 14): array
{
    epc_forecast_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT `site_key`, `sku`, `product_name`, `current_stock`, `avg_daily_demand`,
               `days_of_stock`, `stockout_date`, `reorder_point`, `eoq`
        FROM `epc_inventory_forecast`
        WHERE `stockout_date` IS NOT NULL AND `stockout_date` <= DATE_ADD(CURDATE(), INTERVAL ? DAY)
          AND `forecast_status` != 'stockout'
        ORDER BY `stockout_date` ASC
        LIMIT 100
    ");
    $st->execute(array($withinDays));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── ABC analysis ─── */

function epc_forecast_abc_analysis(PDO $pdo, string $siteKey): array
{
    $st = $pdo->prepare("
        SELECT `sku`, SUM(`revenue`) AS `total_revenue`, SUM(`qty_sold`) AS `total_qty`
        FROM `epc_demand_history`
        WHERE `site_key` = ? AND `period` >= DATE_SUB(CURDATE(), INTERVAL 12 MONTH)
        GROUP BY `sku`
        ORDER BY `total_revenue` DESC
    ");
    $st->execute(array($siteKey));
    $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    $totalRevenue = array_sum(array_column($rows, 'total_revenue'));
    $cumulative = 0;
    $result = array('A' => array(), 'B' => array(), 'C' => array());

    foreach ($rows as $row) {
        $cumulative += (float) $row['total_revenue'];
        $pct = $totalRevenue > 0 ? ($cumulative / $totalRevenue) * 100 : 0;

        if ($pct <= 80) {
            $row['class'] = 'A';
            $result['A'][] = $row;
        } elseif ($pct <= 95) {
            $row['class'] = 'B';
            $result['B'][] = $row;
        } else {
            $row['class'] = 'C';
            $result['C'][] = $row;
        }
    }

    return array(
        'total_revenue' => $totalRevenue,
        'A' => array('count' => count($result['A']), 'items' => array_slice($result['A'], 0, 20)),
        'B' => array('count' => count($result['B']), 'items' => array_slice($result['B'], 0, 20)),
        'C' => array('count' => count($result['C']), 'items' => array_slice($result['C'], 0, 20)),
    );
}
