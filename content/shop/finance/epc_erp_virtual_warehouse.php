<?php
/**
 * Virtual Warehouse / Exhibition Stock Module — manage multiple stock locations.
 * Physical warehouses, virtual locations, exhibition displays, consignment.
 * Inter-location transfers with full audit trail.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_vwh_ensure_schema')) {
    function epc_vwh_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_warehouses` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(20) NOT NULL DEFAULT '',
            `name` varchar(200) NOT NULL DEFAULT '',
            `type` enum('physical','virtual','exhibition','consignment','transit') NOT NULL DEFAULT 'physical',
            `address` varchar(500) NOT NULL DEFAULT '',
            `manager_id` int(11) NOT NULL DEFAULT 0,
            `manager_name` varchar(120) NOT NULL DEFAULT '',
            `is_active` tinyint(1) NOT NULL DEFAULT 1,
            `is_sellable` tinyint(1) NOT NULL DEFAULT 1 COMMENT 'Can sell from this location',
            `event_name` varchar(200) NOT NULL DEFAULT '' COMMENT 'For exhibition type',
            `event_start` date DEFAULT NULL,
            `event_end` date DEFAULT NULL,
            `return_warehouse_id` int(11) NOT NULL DEFAULT 0 COMMENT 'Where stock returns after event',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_type` (`type`),
            KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Warehouse / location master'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_warehouse_stock` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `product_id` int(11) NOT NULL DEFAULT 0,
            `sku` varchar(100) NOT NULL DEFAULT '',
            `barcode` varchar(100) NOT NULL DEFAULT '',
            `qty_on_hand` decimal(14,3) NOT NULL DEFAULT 0.000,
            `qty_reserved` decimal(14,3) NOT NULL DEFAULT 0.000,
            `qty_available` decimal(14,3) NOT NULL DEFAULT 0.000,
            `avg_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `last_count_date` date DEFAULT NULL,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_wh_prod` (`warehouse_id`, `product_id`),
            KEY `x_company` (`company_id`),
            KEY `x_sku` (`sku`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Stock per warehouse'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_warehouse_transfers` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `transfer_no` varchar(32) NOT NULL DEFAULT '',
            `from_warehouse_id` int(11) NOT NULL DEFAULT 0,
            `to_warehouse_id` int(11) NOT NULL DEFAULT 0,
            `reason` varchar(200) NOT NULL DEFAULT '' COMMENT 'exhibition,rebalance,return,repair',
            `status` enum('draft','in_transit','received','cancelled') NOT NULL DEFAULT 'draft',
            `total_items` int(11) NOT NULL DEFAULT 0,
            `total_qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `shipped_at` datetime DEFAULT NULL,
            `received_at` datetime DEFAULT NULL,
            `created_by` int(11) NOT NULL DEFAULT 0,
            `received_by` int(11) NOT NULL DEFAULT 0,
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_from` (`from_warehouse_id`),
            KEY `x_to` (`to_warehouse_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inter-warehouse transfers'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_warehouse_transfer_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `transfer_id` int(11) NOT NULL DEFAULT 0,
            `product_id` int(11) NOT NULL DEFAULT 0,
            `sku` varchar(100) NOT NULL DEFAULT '',
            `barcode` varchar(100) NOT NULL DEFAULT '',
            `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `received_qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `notes` varchar(300) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            KEY `x_transfer` (`transfer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Transfer line items'");
    }

    function epc_vwh_create_warehouse(PDO $db, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_warehouses` (`company_id`,`code`,`name`,`type`,`address`,`manager_id`,`manager_name`,`is_sellable`,`event_name`,`event_start`,`event_end`,`return_warehouse_id`,`notes`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['code'] ?? '', $data['name'] ?? '', $data['type'] ?? 'physical', $data['address'] ?? '', $data['manager_id'] ?? 0, $data['manager_name'] ?? '', $data['is_sellable'] ?? 1, $data['event_name'] ?? '', $data['event_start'] ?? null, $data['event_end'] ?? null, $data['return_warehouse_id'] ?? 0, $data['notes'] ?? '', time()]);
        return (int) $db->lastInsertId();
    }

    function epc_vwh_create_transfer(PDO $db, array $data, array $lines): int
    {
        $transferNo = 'TRF-' . date('Ymd') . '-' . str_pad((string) rand(1, 9999), 4, '0', STR_PAD_LEFT);
        $totalQty = 0;
        foreach ($lines as $l) $totalQty += (float) ($l['qty'] ?? 0);

        $stmt = $db->prepare("INSERT INTO `epc_warehouse_transfers` (`company_id`,`transfer_no`,`from_warehouse_id`,`to_warehouse_id`,`reason`,`status`,`total_items`,`total_qty`,`created_by`,`notes`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $transferNo, $data['from_warehouse_id'] ?? 0, $data['to_warehouse_id'] ?? 0, $data['reason'] ?? '', 'draft', count($lines), $totalQty, $data['created_by'] ?? 0, $data['notes'] ?? '', time()]);
        $transferId = (int) $db->lastInsertId();

        foreach ($lines as $l) {
            $db->prepare("INSERT INTO `epc_warehouse_transfer_lines` (`transfer_id`,`product_id`,`sku`,`barcode`,`qty`) VALUES (?,?,?,?,?)")
                ->execute([$transferId, $l['product_id'] ?? 0, $l['sku'] ?? '', $l['barcode'] ?? '', $l['qty'] ?? 0]);
        }

        return $transferId;
    }

    function epc_vwh_ship_transfer(PDO $db, int $transferId): bool
    {
        $db->prepare("UPDATE `epc_warehouse_transfers` SET `status` = 'in_transit', `shipped_at` = NOW() WHERE `id` = ?")->execute([$transferId]);
        return true;
    }

    function epc_vwh_receive_transfer(PDO $db, int $transferId, int $receivedBy): bool
    {
        $db->prepare("UPDATE `epc_warehouse_transfers` SET `status` = 'received', `received_at` = NOW(), `received_by` = ? WHERE `id` = ?")->execute([$receivedBy, $transferId]);
        return true;
    }

    function epc_vwh_list_warehouses(PDO $db, int $companyId, string $type = ''): array
    {
        $sql = "SELECT w.*, (SELECT COUNT(*) FROM `epc_warehouse_stock` s WHERE s.`warehouse_id` = w.`id` AND s.`qty_on_hand` > 0) as item_count FROM `epc_warehouses` w WHERE w.`company_id` = ? AND w.`is_active` = 1";
        $params = [$companyId];
        if ($type) { $sql .= " AND w.`type` = ?"; $params[] = $type; }
        $sql .= " ORDER BY w.`name`";
        $stmt = $db->prepare($sql);
        $stmt->execute($params);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
