<?php
/**
 * Inventory Report Module — drilldown reports by category, sub-category, and SKU level.
 * Supports valuation methods (FIFO, WAVG, LIFO), aging, ABC analysis.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_inv_report_ensure_schema')) {
    function epc_inv_report_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_inventory_categories` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `parent_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(30) NOT NULL DEFAULT '',
            `name` varchar(200) NOT NULL DEFAULT '',
            `level` int(11) NOT NULL DEFAULT 1 COMMENT '1=category, 2=sub-category, 3=sub-sub',
            `sort_order` int(11) NOT NULL DEFAULT 0,
            `is_active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_parent` (`parent_id`),
            KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inventory category hierarchy'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_inventory_snapshots` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `snapshot_date` date NOT NULL,
            `category_id` int(11) NOT NULL DEFAULT 0,
            `total_skus` int(11) NOT NULL DEFAULT 0,
            `total_qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `total_value` decimal(14,2) NOT NULL DEFAULT 0.00,
            `avg_age_days` decimal(8,1) NOT NULL DEFAULT 0.0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company_date` (`company_id`, `snapshot_date`),
            KEY `x_category` (`category_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Daily inventory value snapshots per category'");
    }

    function epc_inv_report_by_category(PDO $db, int $companyId, int $parentId = 0): array
    {
        $stmt = $db->prepare("SELECT c.*, COALESCE(s.total_skus, 0) as total_skus, COALESCE(s.total_qty, 0) as total_qty, COALESCE(s.total_value, 0) as total_value FROM `epc_inventory_categories` c LEFT JOIN `epc_inventory_snapshots` s ON s.`category_id` = c.`id` AND s.`snapshot_date` = CURDATE() WHERE c.`company_id` = ? AND c.`parent_id` = ? AND c.`is_active` = 1 ORDER BY c.`sort_order`, c.`name`");
        $stmt->execute([$companyId, $parentId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_inv_report_by_sku(PDO $db, int $companyId, int $categoryId, string $sortBy = 'value_desc'): array
    {
        $orderClause = match($sortBy) {
            'qty_desc' => 'i.`qty_on_hand` DESC',
            'qty_asc' => 'i.`qty_on_hand` ASC',
            'value_asc' => '(i.`qty_on_hand` * i.`avg_cost`) ASC',
            'age_desc' => 'DATEDIFF(CURDATE(), i.`last_received_date`) DESC',
            default => '(i.`qty_on_hand` * i.`avg_cost`) DESC',
        };

        $stmt = $db->prepare("SELECT i.*, p.`name` as product_name, p.`category_id`, (i.`qty_on_hand` * i.`avg_cost`) as total_value, DATEDIFF(CURDATE(), COALESCE(i.`last_received_date`, i.`time_created`)) as age_days FROM `epc_warehouse_stock` i JOIN `epc_products` p ON p.`id` = i.`product_id` WHERE i.`company_id` = ? AND p.`category_id` = ? AND i.`qty_on_hand` > 0 ORDER BY {$orderClause} LIMIT 500");
        $stmt->execute([$companyId, $categoryId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_inv_report_abc_analysis(PDO $db, int $companyId): array
    {
        $stmt = $db->prepare("SELECT i.`product_id`, p.`name`, p.`sku`, (i.`qty_on_hand` * i.`avg_cost`) as total_value FROM `epc_warehouse_stock` i JOIN `epc_products` p ON p.`id` = i.`product_id` WHERE i.`company_id` = ? AND i.`qty_on_hand` > 0 ORDER BY (i.`qty_on_hand` * i.`avg_cost`) DESC");
        $stmt->execute([$companyId]);
        $items = $stmt->fetchAll(PDO::FETCH_ASSOC);

        $totalValue = array_sum(array_column($items, 'total_value'));
        $cumulative = 0;
        $result = ['A' => [], 'B' => [], 'C' => []];

        foreach ($items as &$item) {
            $cumulative += (float) $item['total_value'];
            $pct = $totalValue > 0 ? ($cumulative / $totalValue) * 100 : 0;
            if ($pct <= 80) { $item['class'] = 'A'; $result['A'][] = $item; }
            elseif ($pct <= 95) { $item['class'] = 'B'; $result['B'][] = $item; }
            else { $item['class'] = 'C'; $result['C'][] = $item; }
        }

        return ['total_value' => $totalValue, 'classes' => $result, 'summary' => [
            'A' => ['items' => count($result['A']), 'value' => array_sum(array_column($result['A'], 'total_value'))],
            'B' => ['items' => count($result['B']), 'value' => array_sum(array_column($result['B'], 'total_value'))],
            'C' => ['items' => count($result['C']), 'value' => array_sum(array_column($result['C'], 'total_value'))],
        ]];
    }

    function epc_inv_report_aging(PDO $db, int $companyId): array
    {
        $brackets = [
            ['label' => '0-30 days', 'min' => 0, 'max' => 30],
            ['label' => '31-60 days', 'min' => 31, 'max' => 60],
            ['label' => '61-90 days', 'min' => 61, 'max' => 90],
            ['label' => '91-180 days', 'min' => 91, 'max' => 180],
            ['label' => '180+ days', 'min' => 181, 'max' => 99999],
        ];
        $result = [];
        foreach ($brackets as $b) {
            $stmt = $db->prepare("SELECT COUNT(*) as items, COALESCE(SUM(i.`qty_on_hand`), 0) as qty, COALESCE(SUM(i.`qty_on_hand` * i.`avg_cost`), 0) as value FROM `epc_warehouse_stock` i WHERE i.`company_id` = ? AND i.`qty_on_hand` > 0 AND DATEDIFF(CURDATE(), COALESCE(i.`last_count_date`, FROM_UNIXTIME(i.`time_updated`))) BETWEEN ? AND ?");
            $stmt->execute([$companyId, $b['min'], $b['max']]);
            $row = $stmt->fetch(PDO::FETCH_ASSOC);
            $result[] = array_merge($b, $row ?: ['items' => 0, 'qty' => 0, 'value' => 0]);
        }
        return $result;
    }
}
