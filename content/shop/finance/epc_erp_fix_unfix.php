<?php
/**
 * Fix/Unfix Purchase Module — jewellery purchase structure tracking.
 * "Fix" = locked gold rate at purchase time. "Unfix" = rate to be determined later.
 * Tracks margins on both fix and unfix structures with reporting.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_fix_unfix_ensure_schema')) {
    function epc_fix_unfix_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fix_unfix_purchases` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `purchase_id` int(11) NOT NULL DEFAULT 0,
            `supplier_id` int(11) NOT NULL DEFAULT 0,
            `supplier_name` varchar(200) NOT NULL DEFAULT '',
            `purchase_date` date DEFAULT NULL,
            `structure_type` enum('fix','unfix') NOT NULL DEFAULT 'fix',
            `metal_type` varchar(30) NOT NULL DEFAULT 'gold',
            `karat` varchar(10) NOT NULL DEFAULT '24K',
            `weight_grams` decimal(10,3) NOT NULL DEFAULT 0.000,
            `fix_rate` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'Locked rate (fix only)',
            `fix_date` date DEFAULT NULL COMMENT 'Date rate was fixed',
            `fix_reference` varchar(100) NOT NULL DEFAULT '' COMMENT 'Hedge/contract ref',
            `unfix_estimated_rate` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'Estimated at purchase (unfix)',
            `unfix_settle_rate` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'Actual settlement rate (unfix)',
            `unfix_settle_date` date DEFAULT NULL,
            `margin_on_fix` decimal(14,2) NOT NULL DEFAULT 0.00,
            `margin_on_unfix` decimal(14,2) NOT NULL DEFAULT 0.00,
            `making_charges` decimal(14,2) NOT NULL DEFAULT 0.00,
            `total_value` decimal(14,2) NOT NULL DEFAULT 0.00,
            `status` enum('open','fixed','settled','cancelled') NOT NULL DEFAULT 'open',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_supplier` (`supplier_id`),
            KEY `x_structure` (`structure_type`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Fix/unfix purchase structure tracking'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_fix_unfix_settlements` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `purchase_fix_id` int(11) NOT NULL DEFAULT 0,
            `settle_date` date DEFAULT NULL,
            `settle_rate` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `weight_settled` decimal(10,3) NOT NULL DEFAULT 0.000,
            `gain_loss` decimal(14,2) NOT NULL DEFAULT 0.00 COMMENT 'Positive = gain, negative = loss',
            `reference` varchar(100) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_purchase` (`purchase_fix_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Unfix settlement records'");
    }

    function epc_fix_unfix_create(PDO $db, array $data): int
    {
        $weight = (float) ($data['weight_grams'] ?? 0);
        $totalValue = 0;
        if (($data['structure_type'] ?? 'fix') === 'fix') {
            $totalValue = $weight * (float) ($data['fix_rate'] ?? 0) + (float) ($data['making_charges'] ?? 0);
        } else {
            $totalValue = $weight * (float) ($data['unfix_estimated_rate'] ?? 0) + (float) ($data['making_charges'] ?? 0);
        }

        $stmt = $db->prepare("INSERT INTO `epc_fix_unfix_purchases` (`company_id`,`purchase_id`,`supplier_id`,`supplier_name`,`purchase_date`,`structure_type`,`metal_type`,`karat`,`weight_grams`,`fix_rate`,`fix_date`,`fix_reference`,`unfix_estimated_rate`,`margin_on_fix`,`margin_on_unfix`,`making_charges`,`total_value`,`status`,`notes`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([
            $data['company_id'] ?? 0, $data['purchase_id'] ?? 0, $data['supplier_id'] ?? 0,
            $data['supplier_name'] ?? '', $data['purchase_date'] ?? date('Y-m-d'),
            $data['structure_type'] ?? 'fix', $data['metal_type'] ?? 'gold', $data['karat'] ?? '24K',
            $weight, $data['fix_rate'] ?? 0, $data['fix_date'] ?? null, $data['fix_reference'] ?? '',
            $data['unfix_estimated_rate'] ?? 0, $data['margin_on_fix'] ?? 0, $data['margin_on_unfix'] ?? 0,
            $data['making_charges'] ?? 0, $totalValue, 'open', $data['notes'] ?? '', time()
        ]);
        return (int) $db->lastInsertId();
    }

    function epc_fix_unfix_settle(PDO $db, int $id, float $settleRate): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_fix_unfix_purchases` WHERE `id` = ?");
        $stmt->execute([$id]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$row || $row['structure_type'] !== 'unfix') return ['ok' => false, 'error' => 'Not an unfix purchase'];

        $gainLoss = ($settleRate - (float) $row['unfix_estimated_rate']) * (float) $row['weight_grams'];

        $db->prepare("UPDATE `epc_fix_unfix_purchases` SET `unfix_settle_rate` = ?, `unfix_settle_date` = CURDATE(), `status` = 'settled', `time_updated` = ? WHERE `id` = ?")->execute([$settleRate, time(), $id]);
        $db->prepare("INSERT INTO `epc_fix_unfix_settlements` (`purchase_fix_id`,`settle_date`,`settle_rate`,`weight_settled`,`gain_loss`,`time_created`) VALUES (?,CURDATE(),?,?,?,?)")
            ->execute([$id, $settleRate, $row['weight_grams'], $gainLoss, time()]);

        return ['ok' => true, 'gain_loss' => $gainLoss];
    }

    function epc_fix_unfix_report(PDO $db, int $companyId, string $dateFrom = '', string $dateTo = ''): array
    {
        $sql = "SELECT `structure_type`, COUNT(*) as count, SUM(`weight_grams`) as total_weight, SUM(`total_value`) as total_value, SUM(`margin_on_fix`) as margin_fix, SUM(`margin_on_unfix`) as margin_unfix FROM `epc_fix_unfix_purchases` WHERE `company_id` = ?";
        $params = [$companyId];
        if ($dateFrom) { $sql .= " AND `purchase_date` >= ?"; $params[] = $dateFrom; }
        if ($dateTo) { $sql .= " AND `purchase_date` <= ?"; $params[] = $dateTo; }
        $sql .= " GROUP BY `structure_type`";
        $stmt = $db->prepare($sql);
        $stmt->execute($params);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
