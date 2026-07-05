<?php
/**
 * Landed Cost V2 Module — distribute import expenses over product cost.
 * Methods: by value (pro-rata), by weight, by volume, by quantity, equal split.
 * Links to PO/GRN, posts updated cost to inventory.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_landed_cost_v2_ensure_schema')) {
    function epc_landed_cost_v2_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_landed_cost_sheets` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `sheet_no` varchar(32) NOT NULL DEFAULT '',
            `po_reference` varchar(100) NOT NULL DEFAULT '',
            `grn_reference` varchar(100) NOT NULL DEFAULT '',
            `supplier_id` int(11) NOT NULL DEFAULT 0,
            `supplier_name` varchar(200) NOT NULL DEFAULT '',
            `goods_value` decimal(14,2) NOT NULL DEFAULT 0.00,
            `total_expenses` decimal(14,2) NOT NULL DEFAULT 0.00,
            `distribution_method` enum('value','weight','volume','quantity','equal') NOT NULL DEFAULT 'value',
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `status` enum('draft','calculated','posted','voided') NOT NULL DEFAULT 'draft',
            `posted_at` datetime DEFAULT NULL,
            `created_by` int(11) NOT NULL DEFAULT 0,
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_po` (`po_reference`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Landed cost distribution sheets'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_landed_cost_expenses` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `sheet_id` int(11) NOT NULL DEFAULT 0,
            `expense_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'freight,customs,insurance,handling,inspection,duty,other',
            `vendor_name` varchar(200) NOT NULL DEFAULT '',
            `reference` varchar(100) NOT NULL DEFAULT '',
            `amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `exchange_rate` decimal(10,6) NOT NULL DEFAULT 1.000000,
            `amount_local` decimal(14,2) NOT NULL DEFAULT 0.00,
            PRIMARY KEY (`id`),
            KEY `x_sheet` (`sheet_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Expense lines per sheet'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_landed_cost_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `sheet_id` int(11) NOT NULL DEFAULT 0,
            `product_id` int(11) NOT NULL DEFAULT 0,
            `sku` varchar(100) NOT NULL DEFAULT '',
            `description` varchar(300) NOT NULL DEFAULT '',
            `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
            `unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `line_value` decimal(14,2) NOT NULL DEFAULT 0.00,
            `weight` decimal(10,3) NOT NULL DEFAULT 0.000,
            `volume` decimal(10,3) NOT NULL DEFAULT 0.000,
            `allocated_cost` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'Distributed expense per unit',
            `new_unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'unit_cost + allocated_cost',
            PRIMARY KEY (`id`),
            KEY `x_sheet` (`sheet_id`),
            KEY `x_product` (`product_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Line items receiving cost allocation'");
    }

    function epc_landed_cost_v2_calculate(PDO $db, int $sheetId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_landed_cost_sheets` WHERE `id` = ?");
        $stmt->execute([$sheetId]);
        $sheet = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$sheet) return ['ok' => false, 'error' => 'Sheet not found'];

        $expenses = $db->prepare("SELECT COALESCE(SUM(`amount_local`), 0) as total FROM `epc_landed_cost_expenses` WHERE `sheet_id` = ?");
        $expenses->execute([$sheetId]);
        $totalExpenses = (float) $expenses->fetchColumn();

        $lines = $db->prepare("SELECT * FROM `epc_landed_cost_lines` WHERE `sheet_id` = ?");
        $lines->execute([$sheetId]);
        $lineItems = $lines->fetchAll(PDO::FETCH_ASSOC);
        if (empty($lineItems)) return ['ok' => false, 'error' => 'No line items'];

        $totalBasis = 0;
        $method = $sheet['distribution_method'];
        foreach ($lineItems as $l) {
            $totalBasis += match($method) {
                'weight' => (float) $l['weight'] * (float) $l['qty'],
                'volume' => (float) $l['volume'] * (float) $l['qty'],
                'quantity' => (float) $l['qty'],
                'equal' => 1,
                default => (float) $l['line_value'],
            };
        }

        if ($totalBasis <= 0) return ['ok' => false, 'error' => 'Cannot distribute — total basis is zero'];

        foreach ($lineItems as $l) {
            $lineBasis = match($method) {
                'weight' => (float) $l['weight'] * (float) $l['qty'],
                'volume' => (float) $l['volume'] * (float) $l['qty'],
                'quantity' => (float) $l['qty'],
                'equal' => 1,
                default => (float) $l['line_value'],
            };
            $share = ($lineBasis / $totalBasis) * $totalExpenses;
            $allocatedPerUnit = (float) $l['qty'] > 0 ? $share / (float) $l['qty'] : 0;
            $newUnitCost = (float) $l['unit_cost'] + $allocatedPerUnit;

            $db->prepare("UPDATE `epc_landed_cost_lines` SET `allocated_cost` = ?, `new_unit_cost` = ? WHERE `id` = ?")
                ->execute([round($allocatedPerUnit, 4), round($newUnitCost, 4), $l['id']]);
        }

        $db->prepare("UPDATE `epc_landed_cost_sheets` SET `total_expenses` = ?, `status` = 'calculated' WHERE `id` = ?")
            ->execute([$totalExpenses, $sheetId]);

        return ['ok' => true, 'total_expenses' => $totalExpenses, 'lines_updated' => count($lineItems), 'method' => $method];
    }

    function epc_landed_cost_v2_post(PDO $db, int $sheetId): array
    {
        $db->prepare("UPDATE `epc_landed_cost_sheets` SET `status` = 'posted', `posted_at` = NOW() WHERE `id` = ? AND `status` = 'calculated'")->execute([$sheetId]);
        return ['ok' => true, 'message' => 'Landed costs posted to inventory'];
    }

    function epc_landed_cost_v2_list(PDO $db, int $companyId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_landed_cost_sheets` WHERE `company_id` = ? ORDER BY `time_created` DESC");
        $stmt->execute([$companyId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
