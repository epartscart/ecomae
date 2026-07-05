<?php
/**
 * Jewellery Barcode Purchase Module — barcode = purchase record.
 * Each product barcode carries full purchase info, margins, and salesman allocation.
 * The barcode window shows all information for the piece.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_barcode_purchase_ensure_schema')) {
    function epc_barcode_purchase_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_barcode_purchases` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `barcode` varchar(100) NOT NULL DEFAULT '',
            `item_description` varchar(300) NOT NULL DEFAULT '',
            `supplier_id` int(11) NOT NULL DEFAULT 0,
            `supplier_name` varchar(200) NOT NULL DEFAULT '',
            `purchase_date` date DEFAULT NULL,
            `purchase_invoice_no` varchar(64) NOT NULL DEFAULT '',
            `metal_type` varchar(30) NOT NULL DEFAULT 'gold',
            `karat` varchar(10) NOT NULL DEFAULT '22K',
            `gross_weight` decimal(10,3) NOT NULL DEFAULT 0.000,
            `net_weight` decimal(10,3) NOT NULL DEFAULT 0.000,
            `stone_weight` decimal(10,3) NOT NULL DEFAULT 0.000,
            `gold_rate_at_purchase` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `metal_value` decimal(14,2) NOT NULL DEFAULT 0.00,
            `making_charges` decimal(14,2) NOT NULL DEFAULT 0.00,
            `stone_value` decimal(14,2) NOT NULL DEFAULT 0.00,
            `other_charges` decimal(14,2) NOT NULL DEFAULT 0.00,
            `total_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
            `margin_pct` decimal(5,2) NOT NULL DEFAULT 15.00,
            `margin_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `selling_price` decimal(14,2) NOT NULL DEFAULT 0.00,
            `salesman_id` int(11) NOT NULL DEFAULT 0,
            `salesman_name` varchar(120) NOT NULL DEFAULT '',
            `salesman_commission_pct` decimal(5,2) NOT NULL DEFAULT 2.00,
            `category` varchar(100) NOT NULL DEFAULT '',
            `design_no` varchar(50) NOT NULL DEFAULT '',
            `hallmark_no` varchar(50) NOT NULL DEFAULT '',
            `certificate_no` varchar(50) NOT NULL DEFAULT '',
            `status` enum('available','reserved','sold','returned','melted') NOT NULL DEFAULT 'available',
            `sold_to_customer_id` int(11) NOT NULL DEFAULT 0,
            `sold_invoice_id` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_barcode` (`company_id`, `barcode`),
            KEY `x_status` (`status`),
            KEY `x_salesman` (`salesman_id`),
            KEY `x_supplier` (`supplier_id`),
            KEY `x_category` (`category`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Barcode-level purchase records with margins'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_barcode_margin_rules` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `category` varchar(100) NOT NULL DEFAULT '',
            `min_weight` decimal(10,3) NOT NULL DEFAULT 0.000,
            `max_weight` decimal(10,3) NOT NULL DEFAULT 999.000,
            `margin_pct` decimal(5,2) NOT NULL DEFAULT 15.00,
            `salesman_commission_pct` decimal(5,2) NOT NULL DEFAULT 2.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_category` (`category`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Automatic margin rules by category/weight'");
    }

    function epc_barcode_purchase_create(PDO $db, array $data): int
    {
        $metalValue = (float) ($data['net_weight'] ?? 0) * (float) ($data['gold_rate_at_purchase'] ?? 0);
        $totalCost = $metalValue + (float) ($data['making_charges'] ?? 0) + (float) ($data['stone_value'] ?? 0) + (float) ($data['other_charges'] ?? 0);
        $marginPct = (float) ($data['margin_pct'] ?? 15);
        $marginAmount = $totalCost * ($marginPct / 100);
        $sellingPrice = $totalCost + $marginAmount;

        $stmt = $db->prepare("INSERT INTO `epc_barcode_purchases` (`company_id`,`barcode`,`item_description`,`supplier_id`,`supplier_name`,`purchase_date`,`purchase_invoice_no`,`metal_type`,`karat`,`gross_weight`,`net_weight`,`stone_weight`,`gold_rate_at_purchase`,`metal_value`,`making_charges`,`stone_value`,`other_charges`,`total_cost`,`margin_pct`,`margin_amount`,`selling_price`,`salesman_id`,`salesman_name`,`salesman_commission_pct`,`category`,`design_no`,`hallmark_no`,`certificate_no`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([
            $data['company_id'] ?? 0, $data['barcode'] ?? '', $data['item_description'] ?? '',
            $data['supplier_id'] ?? 0, $data['supplier_name'] ?? '', $data['purchase_date'] ?? date('Y-m-d'),
            $data['purchase_invoice_no'] ?? '', $data['metal_type'] ?? 'gold', $data['karat'] ?? '22K',
            $data['gross_weight'] ?? 0, $data['net_weight'] ?? 0, $data['stone_weight'] ?? 0,
            $data['gold_rate_at_purchase'] ?? 0, $metalValue, $data['making_charges'] ?? 0,
            $data['stone_value'] ?? 0, $data['other_charges'] ?? 0, $totalCost,
            $marginPct, $marginAmount, $sellingPrice,
            $data['salesman_id'] ?? 0, $data['salesman_name'] ?? '', $data['salesman_commission_pct'] ?? 2,
            $data['category'] ?? '', $data['design_no'] ?? '', $data['hallmark_no'] ?? '',
            $data['certificate_no'] ?? '', 'available', time()
        ]);
        return (int) $db->lastInsertId();
    }

    function epc_barcode_purchase_lookup(PDO $db, int $companyId, string $barcode): ?array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_barcode_purchases` WHERE `company_id` = ? AND `barcode` = ?");
        $stmt->execute([$companyId, $barcode]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }

    function epc_barcode_purchase_sell(PDO $db, int $id, int $customerId, int $invoiceId): bool
    {
        return $db->prepare("UPDATE `epc_barcode_purchases` SET `status` = 'sold', `sold_to_customer_id` = ?, `sold_invoice_id` = ?, `time_updated` = ? WHERE `id` = ?")->execute([$customerId, $invoiceId, time(), $id]);
    }
}
