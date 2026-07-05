<?php
/**
 * RFID Provision Module — register, scan, and track RFID-tagged inventory.
 * Bulk scan for stock-take, real-time location, anti-theft, audit trail.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_rfid_ensure_schema')) {
    function epc_rfid_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rfid_tags` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `rfid_epc` varchar(100) NOT NULL DEFAULT '' COMMENT 'Electronic Product Code (96-bit hex)',
            `rfid_tid` varchar(100) NOT NULL DEFAULT '' COMMENT 'Tag Identifier (unique per chip)',
            `product_id` int(11) NOT NULL DEFAULT 0,
            `barcode` varchar(100) NOT NULL DEFAULT '',
            `sku` varchar(100) NOT NULL DEFAULT '',
            `item_description` varchar(300) NOT NULL DEFAULT '',
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `location_zone` varchar(50) NOT NULL DEFAULT '',
            `status` enum('active','sold','deactivated','lost','damaged') NOT NULL DEFAULT 'active',
            `last_scanned_at` datetime DEFAULT NULL,
            `last_scanned_location` varchar(100) NOT NULL DEFAULT '',
            `registered_by` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_epc` (`company_id`, `rfid_epc`),
            KEY `x_product` (`product_id`),
            KEY `x_barcode` (`barcode`),
            KEY `x_warehouse` (`warehouse_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RFID tag registry'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rfid_scan_sessions` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `session_type` varchar(30) NOT NULL DEFAULT 'stocktake' COMMENT 'stocktake,audit,gate_check,search',
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `zone` varchar(50) NOT NULL DEFAULT '',
            `total_scanned` int(11) NOT NULL DEFAULT 0,
            `total_expected` int(11) NOT NULL DEFAULT 0,
            `total_found` int(11) NOT NULL DEFAULT 0,
            `total_missing` int(11) NOT NULL DEFAULT 0,
            `total_unexpected` int(11) NOT NULL DEFAULT 0,
            `scanned_by` int(11) NOT NULL DEFAULT 0,
            `scanned_by_name` varchar(120) NOT NULL DEFAULT '',
            `status` enum('in_progress','completed','cancelled') NOT NULL DEFAULT 'in_progress',
            `time_started` int(11) NOT NULL DEFAULT 0,
            `time_completed` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_warehouse` (`warehouse_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RFID scan sessions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rfid_scan_results` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `session_id` int(11) NOT NULL DEFAULT 0,
            `rfid_epc` varchar(100) NOT NULL DEFAULT '',
            `tag_id` int(11) NOT NULL DEFAULT 0,
            `scan_result` enum('found','missing','unexpected') NOT NULL DEFAULT 'found',
            `signal_strength` int(11) NOT NULL DEFAULT 0 COMMENT 'RSSI dBm',
            `time_scanned` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_session` (`session_id`),
            KEY `x_epc` (`rfid_epc`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Individual scan results'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rfid_readers` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `reader_name` varchar(100) NOT NULL DEFAULT '',
            `reader_model` varchar(100) NOT NULL DEFAULT '' COMMENT 'Zebra FX7500, Impinj R420, etc.',
            `ip_address` varchar(45) NOT NULL DEFAULT '',
            `port` int(11) NOT NULL DEFAULT 5084,
            `location` varchar(200) NOT NULL DEFAULT '',
            `warehouse_id` int(11) NOT NULL DEFAULT 0,
            `is_active` tinyint(1) NOT NULL DEFAULT 1,
            `last_ping_at` datetime DEFAULT NULL,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RFID reader hardware registry'");
    }

    function epc_rfid_register_tag(PDO $db, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_rfid_tags` (`company_id`,`rfid_epc`,`rfid_tid`,`product_id`,`barcode`,`sku`,`item_description`,`warehouse_id`,`location_zone`,`status`,`registered_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['rfid_epc'] ?? '', $data['rfid_tid'] ?? '', $data['product_id'] ?? 0, $data['barcode'] ?? '', $data['sku'] ?? '', $data['item_description'] ?? '', $data['warehouse_id'] ?? 0, $data['location_zone'] ?? '', 'active', $data['registered_by'] ?? 0, time()]);
        return (int) $db->lastInsertId();
    }

    function epc_rfid_start_scan_session(PDO $db, array $data): int
    {
        $expected = 0;
        if (($data['warehouse_id'] ?? 0) > 0) {
            $stmt = $db->prepare("SELECT COUNT(*) FROM `epc_rfid_tags` WHERE `company_id` = ? AND `warehouse_id` = ? AND `status` = 'active'");
            $stmt->execute([$data['company_id'] ?? 0, $data['warehouse_id'] ?? 0]);
            $expected = (int) $stmt->fetchColumn();
        }

        $stmt = $db->prepare("INSERT INTO `epc_rfid_scan_sessions` (`company_id`,`session_type`,`warehouse_id`,`zone`,`total_expected`,`scanned_by`,`scanned_by_name`,`status`,`time_started`) VALUES (?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['session_type'] ?? 'stocktake', $data['warehouse_id'] ?? 0, $data['zone'] ?? '', $expected, $data['scanned_by'] ?? 0, $data['scanned_by_name'] ?? '', 'in_progress', time()]);
        return (int) $db->lastInsertId();
    }

    function epc_rfid_process_scan(PDO $db, int $sessionId, string $rfidEpc, int $companyId, int $rssi = 0): array
    {
        $tag = $db->prepare("SELECT * FROM `epc_rfid_tags` WHERE `company_id` = ? AND `rfid_epc` = ?");
        $tag->execute([$companyId, $rfidEpc]);
        $tagRow = $tag->fetch(PDO::FETCH_ASSOC);

        $result = $tagRow ? 'found' : 'unexpected';
        $tagId = $tagRow ? (int) $tagRow['id'] : 0;

        $db->prepare("INSERT INTO `epc_rfid_scan_results` (`session_id`,`rfid_epc`,`tag_id`,`scan_result`,`signal_strength`,`time_scanned`) VALUES (?,?,?,?,?,?)")
            ->execute([$sessionId, $rfidEpc, $tagId, $result, $rssi, time()]);

        if ($tagRow) {
            $db->prepare("UPDATE `epc_rfid_tags` SET `last_scanned_at` = NOW(), `time_updated` = ? WHERE `id` = ?")->execute([time(), $tagId]);
        }

        $db->prepare("UPDATE `epc_rfid_scan_sessions` SET `total_scanned` = `total_scanned` + 1, `total_found` = `total_found` + ? WHERE `id` = ?")
            ->execute([$result === 'found' ? 1 : 0, $sessionId]);

        return ['result' => $result, 'tag' => $tagRow];
    }

    function epc_rfid_list_tags(PDO $db, int $companyId, int $warehouseId = 0): array
    {
        $sql = "SELECT * FROM `epc_rfid_tags` WHERE `company_id` = ? AND `status` = 'active'";
        $params = [$companyId];
        if ($warehouseId > 0) { $sql .= " AND `warehouse_id` = ?"; $params[] = $warehouseId; }
        $sql .= " ORDER BY `time_created` DESC LIMIT 500";
        $stmt = $db->prepare($sql);
        $stmt->execute($params);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
