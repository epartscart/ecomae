<?php
/**
 * Tourist Refund Module — VAT refund scheme for tourist purchases.
 * Generates barcoded invoices when category = tourist refund.
 * Compliant with UAE Tax-Free Shopping (Planet/Global Blue integration).
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_tourist_refund_ensure_schema')) {
    function epc_tourist_refund_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_tourist_refund_invoices` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `invoice_id` int(11) NOT NULL DEFAULT 0,
            `invoice_no` varchar(64) NOT NULL DEFAULT '',
            `tourist_name` varchar(200) NOT NULL DEFAULT '',
            `passport_no` varchar(50) NOT NULL DEFAULT '',
            `nationality` varchar(50) NOT NULL DEFAULT '',
            `departure_date` date DEFAULT NULL,
            `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `refund_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `refund_pct` decimal(5,2) NOT NULL DEFAULT 85.00 COMMENT 'Typically 85% of VAT',
            `barcode` varchar(100) NOT NULL DEFAULT '' COMMENT 'Unique refund barcode',
            `barcode_image_path` varchar(500) NOT NULL DEFAULT '',
            `refund_status` enum('pending','validated','refunded','expired','rejected') NOT NULL DEFAULT 'pending',
            `refund_provider` varchar(50) NOT NULL DEFAULT '' COMMENT 'planet,global_blue,manual',
            `provider_reference` varchar(100) NOT NULL DEFAULT '',
            `validated_at` datetime DEFAULT NULL,
            `refunded_at` datetime DEFAULT NULL,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_invoice` (`invoice_id`),
            KEY `x_barcode` (`barcode`),
            KEY `x_passport` (`passport_no`),
            KEY `x_status` (`refund_status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tourist VAT refund invoices'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_tourist_refund_config` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `min_purchase_amount` decimal(14,2) NOT NULL DEFAULT 250.00,
            `vat_rate` decimal(5,2) NOT NULL DEFAULT 5.00,
            `refund_percentage` decimal(5,2) NOT NULL DEFAULT 85.00,
            `provider` varchar(50) NOT NULL DEFAULT 'manual',
            `provider_merchant_id` varchar(100) NOT NULL DEFAULT '',
            `provider_api_key` varchar(300) NOT NULL DEFAULT '',
            `barcode_prefix` varchar(10) NOT NULL DEFAULT 'TR',
            `auto_print` tinyint(1) NOT NULL DEFAULT 1,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tourist refund configuration'");
    }

    function epc_tourist_refund_generate_barcode(PDO $db, int $companyId): string
    {
        $prefix = 'TR';
        $cfgStmt = $db->prepare("SELECT `barcode_prefix` FROM `epc_tourist_refund_config` WHERE `company_id` = ? LIMIT 1");
        $cfgStmt->execute([$companyId]);
        $cfg = $cfgStmt->fetch(PDO::FETCH_ASSOC);
        if ($cfg) $prefix = $cfg['barcode_prefix'];
        return $prefix . date('Ymd') . str_pad((string) rand(1, 999999), 6, '0', STR_PAD_LEFT);
    }

    function epc_tourist_refund_create(PDO $db, array $data): array
    {
        $barcode = epc_tourist_refund_generate_barcode($db, (int) ($data['company_id'] ?? 0));
        $vatAmount = (float) ($data['vat_amount'] ?? 0);
        $refundPct = (float) ($data['refund_pct'] ?? 85);
        $refundAmount = round($vatAmount * ($refundPct / 100), 2);

        $stmt = $db->prepare("INSERT INTO `epc_tourist_refund_invoices` (`company_id`,`invoice_id`,`invoice_no`,`tourist_name`,`passport_no`,`nationality`,`departure_date`,`total_amount`,`vat_amount`,`refund_amount`,`refund_pct`,`barcode`,`refund_provider`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['invoice_id'] ?? 0, $data['invoice_no'] ?? '', $data['tourist_name'] ?? '', $data['passport_no'] ?? '', $data['nationality'] ?? '', $data['departure_date'] ?? null, $data['total_amount'] ?? 0, $vatAmount, $refundAmount, $refundPct, $barcode, $data['refund_provider'] ?? 'manual', time()]);

        return ['id' => (int) $db->lastInsertId(), 'barcode' => $barcode, 'refund_amount' => $refundAmount];
    }

    function epc_tourist_refund_validate(PDO $db, string $barcode): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_tourist_refund_invoices` WHERE `barcode` = ?");
        $stmt->execute([$barcode]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$row) return ['ok' => false, 'error' => 'Barcode not found'];
        if ($row['refund_status'] !== 'pending') return ['ok' => false, 'error' => 'Already processed: ' . $row['refund_status']];

        $db->prepare("UPDATE `epc_tourist_refund_invoices` SET `refund_status` = 'validated', `validated_at` = NOW() WHERE `id` = ?")->execute([$row['id']]);
        return ['ok' => true, 'refund' => $row];
    }
}
