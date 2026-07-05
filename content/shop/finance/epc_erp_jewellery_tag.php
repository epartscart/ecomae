<?php
/**
 * Jewellery TAG System — barcode-level tagging for items.
 * Each physical piece gets a unique tag at barcode/purchase level.
 * Tags flow through: purchase → stock → display → invoice.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_jw_tag_ensure_schema')) {
    function epc_jw_tag_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_jw_tags` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `tag_no` varchar(50) NOT NULL DEFAULT '' COMMENT 'Unique physical tag/barcode',
            `barcode` varchar(100) NOT NULL DEFAULT '',
            `item_type` varchar(30) NOT NULL DEFAULT 'gold' COMMENT 'gold,diamond,silver,platinum,gemstone,mixed',
            `karat` varchar(10) NOT NULL DEFAULT '22K',
            `gross_weight` decimal(10,3) NOT NULL DEFAULT 0.000 COMMENT 'grams',
            `net_weight` decimal(10,3) NOT NULL DEFAULT 0.000 COMMENT 'pure metal weight',
            `stone_weight` decimal(10,3) NOT NULL DEFAULT 0.000,
            `stone_count` int(11) NOT NULL DEFAULT 0,
            `making_charges` decimal(14,2) NOT NULL DEFAULT 0.00,
            `making_type` enum('per_gram','lumpsum','percentage') NOT NULL DEFAULT 'per_gram',
            `cost_price` decimal(14,2) NOT NULL DEFAULT 0.00,
            `sell_price` decimal(14,2) NOT NULL DEFAULT 0.00,
            `margin_pct` decimal(5,2) NOT NULL DEFAULT 0.00,
            `design_no` varchar(50) NOT NULL DEFAULT '',
            `category` varchar(100) NOT NULL DEFAULT '',
            `subcategory` varchar(100) NOT NULL DEFAULT '',
            `supplier_id` int(11) NOT NULL DEFAULT 0,
            `purchase_id` int(11) NOT NULL DEFAULT 0,
            `purchase_date` date DEFAULT NULL,
            `location` varchar(100) NOT NULL DEFAULT '' COMMENT 'showroom, vault, exhibition, etc.',
            `status` enum('in_stock','displayed','reserved','sold','returned','transferred','melted') NOT NULL DEFAULT 'in_stock',
            `sold_invoice_id` int(11) NOT NULL DEFAULT 0,
            `sold_date` date DEFAULT NULL,
            `salesman_id` int(11) NOT NULL DEFAULT 0,
            `rfid_tag` varchar(100) NOT NULL DEFAULT '',
            `description` varchar(500) NOT NULL DEFAULT '',
            `image_path` varchar(500) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_tag` (`company_id`, `tag_no`),
            KEY `x_barcode` (`barcode`),
            KEY `x_status` (`status`),
            KEY `x_category` (`category`),
            KEY `x_location` (`location`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Jewellery item tags (one per physical piece)'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_jw_tag_history` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `tag_id` int(11) NOT NULL DEFAULT 0,
            `action` varchar(30) NOT NULL DEFAULT '' COMMENT 'created,displayed,reserved,sold,returned,transferred,melted',
            `from_location` varchar(100) NOT NULL DEFAULT '',
            `to_location` varchar(100) NOT NULL DEFAULT '',
            `reference` varchar(100) NOT NULL DEFAULT '',
            `actor_id` int(11) NOT NULL DEFAULT 0,
            `actor_name` varchar(120) NOT NULL DEFAULT '',
            `notes` varchar(300) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_tag` (`tag_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tag movement history'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_jw_tag_sequences` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `prefix` varchar(10) NOT NULL DEFAULT 'T',
            `last_number` int(11) NOT NULL DEFAULT 0,
            `format` varchar(50) NOT NULL DEFAULT '{PREFIX}{YYYY}{SEQ:6}',
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tag number sequences'");
    }

    function epc_jw_tag_generate_no(PDO $db, int $companyId): string
    {
        $stmt = $db->prepare("SELECT * FROM `epc_jw_tag_sequences` WHERE `company_id` = ? LIMIT 1 FOR UPDATE");
        $stmt->execute([$companyId]);
        $seq = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$seq) {
            $db->prepare("INSERT INTO `epc_jw_tag_sequences` (`company_id`,`prefix`,`last_number`) VALUES (?,?,?)")->execute([$companyId, 'T', 0]);
            $seq = ['prefix' => 'T', 'last_number' => 0, 'format' => '{PREFIX}{YYYY}{SEQ:6}'];
        }
        $next = $seq['last_number'] + 1;
        $db->prepare("UPDATE `epc_jw_tag_sequences` SET `last_number` = ? WHERE `company_id` = ?")->execute([$next, $companyId]);
        return $seq['prefix'] . date('Y') . str_pad((string) $next, 6, '0', STR_PAD_LEFT);
    }

    function epc_jw_tag_create(PDO $db, array $data): int
    {
        $tagNo = $data['tag_no'] ?? epc_jw_tag_generate_no($db, (int) ($data['company_id'] ?? 0));
        $stmt = $db->prepare("INSERT INTO `epc_jw_tags` (`company_id`,`tag_no`,`barcode`,`item_type`,`karat`,`gross_weight`,`net_weight`,`stone_weight`,`stone_count`,`making_charges`,`making_type`,`cost_price`,`sell_price`,`margin_pct`,`design_no`,`category`,`subcategory`,`supplier_id`,`purchase_id`,`purchase_date`,`location`,`status`,`description`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $tagNo, $data['barcode'] ?? $tagNo, $data['item_type'] ?? 'gold', $data['karat'] ?? '22K', $data['gross_weight'] ?? 0, $data['net_weight'] ?? 0, $data['stone_weight'] ?? 0, $data['stone_count'] ?? 0, $data['making_charges'] ?? 0, $data['making_type'] ?? 'per_gram', $data['cost_price'] ?? 0, $data['sell_price'] ?? 0, $data['margin_pct'] ?? 0, $data['design_no'] ?? '', $data['category'] ?? '', $data['subcategory'] ?? '', $data['supplier_id'] ?? 0, $data['purchase_id'] ?? 0, $data['purchase_date'] ?? date('Y-m-d'), $data['location'] ?? 'showroom', 'in_stock', $data['description'] ?? '', time()]);
        $tagId = (int) $db->lastInsertId();

        $db->prepare("INSERT INTO `epc_jw_tag_history` (`tag_id`,`action`,`to_location`,`reference`,`time_created`) VALUES (?,?,?,?,?)")
            ->execute([$tagId, 'created', $data['location'] ?? 'showroom', 'Purchase: ' . ($data['purchase_id'] ?? ''), time()]);

        return $tagId;
    }

    function epc_jw_tag_sell(PDO $db, int $tagId, int $invoiceId, int $salesmanId): bool
    {
        $db->prepare("UPDATE `epc_jw_tags` SET `status` = 'sold', `sold_invoice_id` = ?, `sold_date` = CURDATE(), `salesman_id` = ?, `time_updated` = ? WHERE `id` = ?")->execute([$invoiceId, $salesmanId, time(), $tagId]);
        $db->prepare("INSERT INTO `epc_jw_tag_history` (`tag_id`,`action`,`reference`,`actor_id`,`time_created`) VALUES (?,?,?,?,?)")
            ->execute([$tagId, 'sold', 'Invoice #' . $invoiceId, $salesmanId, time()]);
        return true;
    }

    function epc_jw_tag_search(PDO $db, int $companyId, array $filters): array
    {
        $sql = "SELECT * FROM `epc_jw_tags` WHERE `company_id` = ?";
        $params = [$companyId];
        if (!empty($filters['status'])) { $sql .= " AND `status` = ?"; $params[] = $filters['status']; }
        if (!empty($filters['category'])) { $sql .= " AND `category` = ?"; $params[] = $filters['category']; }
        if (!empty($filters['location'])) { $sql .= " AND `location` = ?"; $params[] = $filters['location']; }
        if (!empty($filters['karat'])) { $sql .= " AND `karat` = ?"; $params[] = $filters['karat']; }
        if (!empty($filters['barcode'])) { $sql .= " AND (`barcode` LIKE ? OR `tag_no` LIKE ?)"; $params[] = '%' . $filters['barcode'] . '%'; $params[] = '%' . $filters['barcode'] . '%'; }
        $sql .= " ORDER BY `time_created` DESC LIMIT 200";
        $stmt = $db->prepare($sql);
        $stmt->execute($params);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
