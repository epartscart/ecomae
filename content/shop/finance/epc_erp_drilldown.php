<?php
/**
 * Report Drill-Down Module — click any report figure to see underlying transactions.
 * Applies to GL, AR, AP, inventory, sales reports. One-click to transaction detail.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_drilldown_ensure_schema')) {
    function epc_drilldown_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_drilldown_config` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `report_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'gl_trial,gl_ledger,ar_aging,ap_aging,sales_summary,inventory_valuation',
            `drill_field` varchar(100) NOT NULL DEFAULT '' COMMENT 'account_code,customer_id,supplier_id,product_id',
            `target_view` varchar(100) NOT NULL DEFAULT '' COMMENT 'journal_entries,invoices,payments,stock_movements',
            `is_enabled` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_report` (`report_type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Report drill-down configuration'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_drilldown_audit` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `user_id` int(11) NOT NULL DEFAULT 0,
            `report_type` varchar(50) NOT NULL DEFAULT '',
            `drill_field` varchar(100) NOT NULL DEFAULT '',
            `drill_value` varchar(200) NOT NULL DEFAULT '',
            `target_view` varchar(100) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Drill-down usage audit'");
    }

    function epc_drilldown_get_journal_entries(PDO $db, int $companyId, string $accountCode, string $dateFrom, string $dateTo): array
    {
        $stmt = $db->prepare("SELECT j.*, a.`account_name` FROM `epc_gl_journals` j LEFT JOIN `epc_gl_accounts` a ON a.`account_code` = j.`account_code` AND a.`company_id` = j.`company_id` WHERE j.`company_id` = ? AND j.`account_code` = ? AND j.`journal_date` BETWEEN ? AND ? ORDER BY j.`journal_date` DESC, j.`id` DESC LIMIT 500");
        $stmt->execute([$companyId, $accountCode, $dateFrom, $dateTo]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_drilldown_get_invoices(PDO $db, int $companyId, int $customerId, string $dateFrom, string $dateTo): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_ar_invoices` WHERE `company_id` = ? AND `customer_id` = ? AND `invoice_date` BETWEEN ? AND ? ORDER BY `invoice_date` DESC LIMIT 500");
        $stmt->execute([$companyId, $customerId, $dateFrom, $dateTo]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_drilldown_render_link(string $reportType, string $fieldName, string $fieldValue, string $displayText, string $dateFrom = '', string $dateTo = ''): string
    {
        $params = http_build_query(['report' => $reportType, 'field' => $fieldName, 'value' => $fieldValue, 'from' => $dateFrom, 'to' => $dateTo]);
        return '<a href="#" class="epc-drilldown-link" data-params="' . htmlspecialchars($params, ENT_QUOTES, 'UTF-8') . '" title="Click to drill down">' . htmlspecialchars($displayText, ENT_QUOTES, 'UTF-8') . '</a>';
    }

    function epc_drilldown_default_config(int $companyId): array
    {
        return [
            ['report_type' => 'gl_trial', 'drill_field' => 'account_code', 'target_view' => 'journal_entries'],
            ['report_type' => 'gl_ledger', 'drill_field' => 'account_code', 'target_view' => 'journal_entries'],
            ['report_type' => 'ar_aging', 'drill_field' => 'customer_id', 'target_view' => 'invoices'],
            ['report_type' => 'ap_aging', 'drill_field' => 'supplier_id', 'target_view' => 'bills'],
            ['report_type' => 'sales_summary', 'drill_field' => 'product_id', 'target_view' => 'sales_lines'],
            ['report_type' => 'inventory_valuation', 'drill_field' => 'product_id', 'target_view' => 'stock_movements'],
        ];
    }
}
