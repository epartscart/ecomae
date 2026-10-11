using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Verbatim-DDL schema ensures for ERP tables that PHP creates lazily via
/// <c>*_ensure_schema</c> functions (CREATE TABLE IF NOT EXISTS at the top of
/// each entry point). Without these, a tenant where the table has never been
/// used gets HTTP 500 instead of the feature working — same class as the
/// contracts/subscriptions/hr-expense fixes.
/// </summary>
internal static class ErpLazySchema
{
    /// <summary>epc_bos_vat_refunds (epc_bos_vat_refund.php)</summary>
    public static Task EnsureVatRefundsAsync(DbConnection c, CancellationToken ct) =>
        ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_bos_vat_refunds` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`tag_ref` varchar(64) DEFAULT NULL," +
            "`country` varchar(4) NOT NULL DEFAULT ''," +
            "`scheme` varchar(80) DEFAULT NULL," +
            "`operator` varchar(80) DEFAULT NULL," +
            "`invoice_ref` varchar(120) DEFAULT NULL," +
            "`customer_name` varchar(160) DEFAULT NULL," +
            "`passport_no` varchar(64) DEFAULT NULL," +
            "`nationality` varchar(80) DEFAULT NULL," +
            "`sale_amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`vat_amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`refund_amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`fee_amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`retained_amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`status` enum('recorded','validated','exported','refunded','void') NOT NULL DEFAULT 'recorded'," +
            "`sale_date` int(11) NOT NULL DEFAULT 0," +
            "`notes` text," +
            "`admin_id` int(11) NOT NULL DEFAULT 0," +
            "`time` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_country` (`country`)," +
            "KEY `x_status` (`status`)," +
            "KEY `x_saledate` (`sale_date`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS tourist/VAT refund register'",
            ct);

    /// <summary>epc_cons_entities (epc_erp_consolidation.php)</summary>
    public static Task EnsureConsEntitiesAsync(DbConnection c, CancellationToken ct) =>
        ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_cons_entities` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`currency_code` varchar(8) NOT NULL DEFAULT 'AED'," +
            "`ownership_pct` decimal(7,3) NOT NULL DEFAULT 100.000," +
            "`is_home` tinyint(1) NOT NULL DEFAULT 0," +
            "`parent_code` varchar(40) NOT NULL DEFAULT ''," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `u_code` (`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Group member companies'",
            ct);

    /// <summary>epc_hr_leave (epc_erp_hr.php)</summary>
    public static Task EnsureHrLeaveAsync(DbConnection c, CancellationToken ct) =>
        ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_hr_leave` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`employee_id` int(11) NOT NULL," +
            "`type` varchar(24) NOT NULL DEFAULT 'annual'," +
            "`days` decimal(6,2) NOT NULL DEFAULT 0.00," +
            "`date_from` int(11) NOT NULL DEFAULT 0," +
            "`date_to` int(11) NOT NULL DEFAULT 0," +
            "`status` varchar(12) NOT NULL DEFAULT 'pending'," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_emp` (`employee_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Leave requests'",
            ct);

    /// <summary>epc_erp_ins_policies + epc_erp_ins_claims + epc_erp_ins_documents (epc_erp_insurance.php)</summary>
    public static async Task EnsureInsuranceAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_ins_policies` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`policy_no` varchar(120) NOT NULL DEFAULT ''," +
            "`class` varchar(40) NOT NULL DEFAULT 'other'," +
            "`title` varchar(200) NOT NULL DEFAULT ''," +
            "`insurer` varchar(200) NOT NULL DEFAULT ''," +
            "`broker` varchar(200) NOT NULL DEFAULT ''," +
            "`insured_name` varchar(200) NOT NULL DEFAULT ''," +
            "`sum_insured` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`premium` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`deductible` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`currency` varchar(3) NOT NULL DEFAULT 'AED'," +
            "`start_date` int(11) NOT NULL DEFAULT 0," +
            "`expiry_date` int(11) NOT NULL DEFAULT 0," +
            "`reminder_days` varchar(120) NOT NULL DEFAULT '90,60,30,7'," +
            "`contact_email` varchar(200) NOT NULL DEFAULT ''," +
            "`status` varchar(16) NOT NULL DEFAULT 'active'," +
            "`note` text," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_class` (`class`)," +
            "KEY `x_expiry` (`expiry_date`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Insurance policy register'",
            ct).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_ins_claims` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`policy_id` int(11) NOT NULL," +
            "`claim_no` varchar(120) NOT NULL DEFAULT ''," +
            "`loss_date` int(11) NOT NULL DEFAULT 0," +
            "`notified_date` int(11) NOT NULL DEFAULT 0," +
            "`description` text," +
            "`claim_amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`settled_amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`surveyor` varchar(200) NOT NULL DEFAULT ''," +
            "`deadline_date` int(11) NOT NULL DEFAULT 0," +
            "`status` varchar(20) NOT NULL DEFAULT 'notified'," +
            "`note` text," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_policy` (`policy_id`)," +
            "KEY `x_status` (`status`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Insurance claims tracking'",
            ct).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_ins_documents` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`policy_id` int(11) NOT NULL," +
            "`doc_type` varchar(60) NOT NULL DEFAULT 'policy'," +
            "`title` varchar(200) NOT NULL DEFAULT ''," +
            "`file_path` varchar(255) NOT NULL DEFAULT ''," +
            "`note` varchar(255) NOT NULL DEFAULT ''," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_policy` (`policy_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Insurance document store'",
            ct).ConfigureAwait(false);
    }

    /// <summary>epc_demand_history + epc_inventory_forecast (epc_inventory_forecast.php)</summary>
    public static async Task EnsureInventoryForecastAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_demand_history` (" +
            "`id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY," +
            "`site_key` VARCHAR(64) NOT NULL," +
            "`sku` VARCHAR(64) NOT NULL," +
            "`period` DATE NOT NULL," +
            "`qty_sold` INT NOT NULL DEFAULT 0," +
            "`qty_returned` INT NOT NULL DEFAULT 0," +
            "`revenue` DECIMAL(12,2) NOT NULL DEFAULT 0.00," +
            "UNIQUE KEY `uk_demand` (`site_key`, `sku`, `period`)," +
            "INDEX `idx_sku` (`sku`)," +
            "INDEX `idx_period` (`period`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            ct).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_inventory_forecast` (" +
            "`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY," +
            "`site_key` VARCHAR(64) NOT NULL," +
            "`sku` VARCHAR(64) NOT NULL," +
            "`product_name` VARCHAR(255) NOT NULL DEFAULT ''," +
            "`current_stock` INT NOT NULL DEFAULT 0," +
            "`avg_daily_demand` DECIMAL(8,2) NOT NULL DEFAULT 0.00," +
            "`lead_time_days` INT UNSIGNED NOT NULL DEFAULT 7," +
            "`safety_stock` INT NOT NULL DEFAULT 0," +
            "`reorder_point` INT NOT NULL DEFAULT 0," +
            "`eoq` INT NOT NULL DEFAULT 0," +
            "`days_of_stock` INT NOT NULL DEFAULT 0," +
            "`stockout_date` DATE NULL," +
            "`forecast_status` ENUM('healthy','low','critical','stockout') NOT NULL DEFAULT 'healthy'," +
            "`last_computed` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP," +
            "UNIQUE KEY `uk_forecast` (`site_key`, `sku`)," +
            "INDEX `idx_status` (`forecast_status`)," +
            "INDEX `idx_stockout` (`stockout_date`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            ct).ConfigureAwait(false);
    }

    /// <summary>epc_landed_cost_sheets/lines/expenses (epc_erp_landed_cost_v2.php)</summary>
    public static async Task EnsureLandedCostAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_landed_cost_sheets` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`sheet_no` varchar(32) NOT NULL DEFAULT ''," +
            "`po_reference` varchar(100) NOT NULL DEFAULT ''," +
            "`grn_reference` varchar(100) NOT NULL DEFAULT ''," +
            "`supplier_id` int(11) NOT NULL DEFAULT 0," +
            "`supplier_name` varchar(200) NOT NULL DEFAULT ''," +
            "`goods_value` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`total_expenses` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`distribution_method` enum('value','weight','volume','quantity','equal') NOT NULL DEFAULT 'value'," +
            "`currency` varchar(3) NOT NULL DEFAULT 'AED'," +
            "`status` enum('draft','calculated','posted','voided') NOT NULL DEFAULT 'draft'," +
            "`posted_at` datetime DEFAULT NULL," +
            "`created_by` int(11) NOT NULL DEFAULT 0," +
            "`notes` text," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_po` (`po_reference`)," +
            "KEY `x_status` (`status`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Landed cost distribution sheets'",
            ct).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_landed_cost_lines` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`sheet_id` int(11) NOT NULL DEFAULT 0," +
            "`product_id` int(11) NOT NULL DEFAULT 0," +
            "`sku` varchar(100) NOT NULL DEFAULT ''," +
            "`description` varchar(300) NOT NULL DEFAULT ''," +
            "`qty` decimal(14,3) NOT NULL DEFAULT 0.000," +
            "`unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000," +
            "`line_value` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`weight` decimal(10,3) NOT NULL DEFAULT 0.000," +
            "`volume` decimal(10,3) NOT NULL DEFAULT 0.000," +
            "`allocated_cost` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'Distributed expense per unit'," +
            "`new_unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'unit_cost + allocated_cost'," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_sheet` (`sheet_id`)," +
            "KEY `x_product` (`product_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Line items receiving cost allocation'",
            ct).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_landed_cost_expenses` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`sheet_id` int(11) NOT NULL DEFAULT 0," +
            "`expense_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'freight,customs,insurance,handling,inspection,duty,other'," +
            "`vendor_name` varchar(200) NOT NULL DEFAULT ''," +
            "`reference` varchar(100) NOT NULL DEFAULT ''," +
            "`amount` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`currency` varchar(3) NOT NULL DEFAULT 'AED'," +
            "`exchange_rate` decimal(10,6) NOT NULL DEFAULT 1.000000," +
            "`amount_local` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_sheet` (`sheet_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Expense lines per sheet'",
            ct).ConfigureAwait(false);
    }

    /// <summary>epc_fx_rates (epc_multi_currency_gl.php)</summary>
    public static Task EnsureFxRatesAsync(DbConnection c, CancellationToken ct) =>
        ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_fx_rates` (" +
            "`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY," +
            "`base_currency` CHAR(3) NOT NULL DEFAULT 'AED'," +
            "`target_currency` CHAR(3) NOT NULL," +
            "`rate` DECIMAL(16,8) NOT NULL," +
            "`inverse_rate` DECIMAL(16,8) NOT NULL," +
            "`source` VARCHAR(64) NOT NULL DEFAULT 'manual'," +
            "`effective_date` DATE NOT NULL," +
            "`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP," +
            "UNIQUE KEY `uk_rate` (`base_currency`, `target_currency`, `effective_date`)," +
            "INDEX `idx_date` (`effective_date`)," +
            "INDEX `idx_target` (`target_currency`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            ct);

    /// <summary>epc_prja_budget/recognition/txn (epc_erp_project_accounting.php)</summary>
    public static async Task EnsureProjectAccountingAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_prja_budget` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`project_id` int(11) NOT NULL DEFAULT 0," +
            "`category` varchar(60) NOT NULL DEFAULT 'general'," +
            "`cost_budget` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`revenue_budget` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_project` (`project_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Project budgets by category'",
            ct).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_prja_recognition` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`project_id` int(11) NOT NULL DEFAULT 0," +
            "`method` varchar(16) NOT NULL DEFAULT 'poc'," +
            "`as_of` int(11) NOT NULL DEFAULT 0," +
            "`pct_complete` decimal(7,4) NOT NULL DEFAULT 0.0000," +
            "`recognized_revenue` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`recognized_cost` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`wip` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`detail_json` text," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_project` (`project_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Project revenue recognition runs'",
            ct).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_prja_txn` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`project_id` int(11) NOT NULL DEFAULT 0," +
            "`txn_type` varchar(12) NOT NULL DEFAULT 'cost'," +
            "`category` varchar(60) NOT NULL DEFAULT 'general'," +
            "`description` varchar(200) NOT NULL DEFAULT ''," +
            "`amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`txn_date` int(11) NOT NULL DEFAULT 0," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_project` (`project_id`)," +
            "KEY `x_type` (`txn_type`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Project cost/revenue/billing transactions'",
            ct).ConfigureAwait(false);
    }

    /// <summary>epc_tickets (epc_erp_tickets.php)</summary>
    public static Task EnsureTicketsAsync(DbConnection c, CancellationToken ct) =>
        ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_tickets` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`ticket_no` varchar(32) NOT NULL DEFAULT ''," +
            "`subject` varchar(300) NOT NULL DEFAULT ''," +
            "`description` text," +
            "`category` varchar(100) NOT NULL DEFAULT 'general'," +
            "`priority` enum('low','medium','high','critical') NOT NULL DEFAULT 'medium'," +
            "`status` enum('open','in_progress','waiting','resolved','closed') NOT NULL DEFAULT 'open'," +
            "`client_id` int(11) NOT NULL DEFAULT 0," +
            "`client_name` varchar(200) NOT NULL DEFAULT ''," +
            "`assigned_to` int(11) NOT NULL DEFAULT 0," +
            "`assigned_name` varchar(120) NOT NULL DEFAULT ''," +
            "`sla_id` int(11) NOT NULL DEFAULT 0," +
            "`response_deadline` datetime DEFAULT NULL," +
            "`resolution_deadline` datetime DEFAULT NULL," +
            "`first_response_at` datetime DEFAULT NULL," +
            "`resolved_at` datetime DEFAULT NULL," +
            "`escalation_level` int(11) NOT NULL DEFAULT 0," +
            "`tags` varchar(500) NOT NULL DEFAULT ''," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `ux_ticket_no` (`ticket_no`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_status` (`status`)," +
            "KEY `x_priority` (`priority`)," +
            "KEY `x_assigned` (`assigned_to`)," +
            "KEY `x_client` (`client_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Support tickets'",
            ct);

    /// <summary>epc_user_shortcuts (epc_erp_shortcut_icons.php)</summary>
    public static Task EnsureUserShortcutsAsync(DbConnection c, CancellationToken ct) =>
        ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_user_shortcuts` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`user_id` int(11) NOT NULL DEFAULT 0," +
            "`surface` varchar(16) NOT NULL DEFAULT 'both'," +
            "`shortcut_key` varchar(64) NOT NULL DEFAULT ''," +
            "`label` varchar(100) NOT NULL DEFAULT ''," +
            "`icon_class` varchar(100) NOT NULL DEFAULT 'fa fa-star'," +
            "`icon_color` varchar(20) NOT NULL DEFAULT '#3498db'," +
            "`target_url` varchar(500) NOT NULL DEFAULT ''," +
            "`target_tab` varchar(50) NOT NULL DEFAULT ''," +
            "`sort_order` int(11) NOT NULL DEFAULT 0," +
            "`is_pinned` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_user` (`user_id`)," +
            "KEY `x_user_surface` (`user_id`, `surface`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_sort` (`sort_order`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User dashboard shortcuts'",
            ct);

    /// <summary>epc_as_jobs + epc_as_job_lines + epc_as_rma + epc_as_rma_lines + epc_as_warranty (epc_erp_aftersales.php ensure_schema)</summary>
    public static async Task EnsureAftersalesAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_as_jobs` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`job_no` varchar(40) NOT NULL DEFAULT ''," +
            "`customer_id` int(11) NOT NULL DEFAULT 0," +
            "`asset_ref` varchar(120) DEFAULT NULL," +
            "`complaint` text," +
            "`status` varchar(16) NOT NULL DEFAULT 'open'," +
            "`under_warranty` tinyint(1) NOT NULL DEFAULT 0," +
            "`parts_total` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`labour_total` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`tax_total` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`grand_total` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_status` (`status`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Service / repair jobs'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_as_job_lines` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`job_id` int(11) NOT NULL," +
            "`line_type` varchar(10) NOT NULL DEFAULT 'part'," +
            "`description` varchar(190) DEFAULT NULL," +
            "`item_id` int(11) NOT NULL DEFAULT 0," +
            "`qty` decimal(14,4) NOT NULL DEFAULT 0.0000," +
            "`unit_price` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`tax_percent` decimal(7,3) NOT NULL DEFAULT 0.000," +
            "`chargeable` tinyint(1) NOT NULL DEFAULT 1," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_job` (`job_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Service job parts/labour lines'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_as_rma` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`rma_no` varchar(40) NOT NULL DEFAULT ''," +
            "`customer_id` int(11) NOT NULL DEFAULT 0," +
            "`source_type` varchar(40) NOT NULL DEFAULT 'sales_order'," +
            "`source_id` int(11) NOT NULL DEFAULT 0," +
            "`reason` varchar(190) DEFAULT NULL," +
            "`disposition` varchar(20) NOT NULL DEFAULT 'pending'," +
            "`status` varchar(16) NOT NULL DEFAULT 'open'," +
            "`refund_amount` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`restock` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_customer` (`customer_id`)," +
            "KEY `x_status` (`status`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Return merchandise authorisations'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_as_rma_lines` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`rma_id` int(11) NOT NULL," +
            "`item_id` int(11) NOT NULL," +
            "`qty` decimal(14,4) NOT NULL DEFAULT 0.0000," +
            "`unit_price` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`condition_note` varchar(190) DEFAULT NULL," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_rma` (`rma_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RMA return lines'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_as_warranty` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`item_id` int(11) NOT NULL DEFAULT 0," +
            "`serial_no` varchar(80) DEFAULT NULL," +
            "`customer_id` int(11) NOT NULL DEFAULT 0," +
            "`source_type` varchar(40) NOT NULL DEFAULT 'sales_order'," +
            "`source_id` int(11) NOT NULL DEFAULT 0," +
            "`start_date` int(11) NOT NULL DEFAULT 0," +
            "`months` int(11) NOT NULL DEFAULT 0," +
            "`expires_at` int(11) NOT NULL DEFAULT 0," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_serial` (`serial_no`)," +
            "KEY `x_item` (`item_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Warranty register'",
            ct);
    }

    /// <summary>epc_oa_party + epc_oa_address + epc_oa_contact + epc_oa_calendar + epc_oa_holiday (epc_erp_orgadmin.php ensure_schema)</summary>
    public static async Task EnsureOrgAdminAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_oa_party` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`party_type` varchar(16) NOT NULL DEFAULT 'organization'," +
            "`name` varchar(190) NOT NULL DEFAULT ''," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Global address book parties'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_oa_address` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`party_id` int(11) NOT NULL DEFAULT 0," +
            "`purpose` varchar(16) NOT NULL DEFAULT 'business'," +
            "`line1` varchar(255) NOT NULL DEFAULT ''," +
            "`city` varchar(120) NOT NULL DEFAULT ''," +
            "`state` varchar(120) NOT NULL DEFAULT ''," +
            "`postcode` varchar(40) NOT NULL DEFAULT ''," +
            "`country` varchar(60) NOT NULL DEFAULT ''," +
            "`is_primary` tinyint(1) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_party` (`party_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Party postal addresses'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_oa_contact` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`party_id` int(11) NOT NULL DEFAULT 0," +
            "`contact_type` varchar(12) NOT NULL DEFAULT 'email'," +
            "`value` varchar(190) NOT NULL DEFAULT ''," +
            "`is_primary` tinyint(1) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_party` (`party_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Party electronic contacts'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_oa_calendar` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`working_days` varchar(20) NOT NULL DEFAULT '1,2,3,4,5'," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_company_code` (`company_id`,`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Working calendars'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_oa_holiday` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`calendar_id` int(11) NOT NULL DEFAULT 0," +
            "`holiday_date` varchar(10) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_cal_date` (`calendar_id`,`holiday_date`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Calendar holidays'",
            ct);
    }

    /// <summary>epc_rbac_role + epc_rbac_duty + epc_rbac_privilege + links (epc_erp_rbac.php ensure_schema)</summary>
    public static async Task EnsureRbacAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_rbac_role` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`code` varchar(60) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_company_code` (`company_id`,`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Security roles'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_rbac_duty` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`code` varchar(60) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_company_code` (`company_id`,`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Security duties'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_rbac_privilege` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`code` varchar(60) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`access_level` varchar(8) NOT NULL DEFAULT 'read'," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_company_code` (`company_id`,`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Security privileges'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_rbac_role_duty` (" +
            "`role_id` int(11) NOT NULL DEFAULT 0," +
            "`duty_id` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`role_id`,`duty_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Role -> duty'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_rbac_duty_priv` (" +
            "`duty_id` int(11) NOT NULL DEFAULT 0," +
            "`privilege_id` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`duty_id`,`privilege_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Duty -> privilege'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_rbac_user_role` (" +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`user_id` int(11) NOT NULL DEFAULT 0," +
            "`role_id` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`company_id`,`user_id`,`role_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User -> role'",
            ct);
    }

    /// <summary>epc_cft_forecast + epc_cft_line + epc_cft_instrument + epc_cft_instr_event (epc_erp_cash_treasury.php ensure_schema)</summary>
    public static async Task EnsureCashTreasuryAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_cft_forecast` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`opening_balance` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`currency` varchar(8) NOT NULL DEFAULT ''," +
            "`notes` text," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Cash flow forecasts'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_cft_line` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`forecast_id` int(11) NOT NULL DEFAULT 0," +
            "`due_date` varchar(16) NOT NULL DEFAULT ''," +
            "`direction` varchar(4) NOT NULL DEFAULT 'in'," +
            "`amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`category` varchar(80) NOT NULL DEFAULT ''," +
            "`source` varchar(120) NOT NULL DEFAULT ''," +
            "`notes` varchar(255) NOT NULL DEFAULT ''," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_forecast` (`forecast_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Cash flow forecast lines'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_cft_instrument` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`ref` varchar(60) NOT NULL DEFAULT ''," +
            "`type` varchar(12) NOT NULL DEFAULT 'lc'," +
            "`beneficiary` varchar(180) NOT NULL DEFAULT ''," +
            "`applicant` varchar(180) NOT NULL DEFAULT ''," +
            "`bank` varchar(180) NOT NULL DEFAULT ''," +
            "`amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`currency` varchar(8) NOT NULL DEFAULT ''," +
            "`issue_date` varchar(16) NOT NULL DEFAULT ''," +
            "`expiry_date` varchar(16) NOT NULL DEFAULT ''," +
            "`status` varchar(16) NOT NULL DEFAULT 'draft'," +
            "`notes` text," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_status` (`status`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Bank instruments (LC/BG/SBLC)'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_cft_instr_event` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`instrument_id` int(11) NOT NULL DEFAULT 0," +
            "`event_type` varchar(24) NOT NULL DEFAULT ''," +
            "`detail` varchar(255) NOT NULL DEFAULT ''," +
            "`amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_instrument` (`instrument_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Bank instrument events'",
            ct);
    }

    /// <summary>epc_wht_code + epc_wht_txn (epc_erp_withholding.php ensure_schema)</summary>
    public static async Task EnsureWithholdingAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_wht_code` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`rate` decimal(8,4) NOT NULL DEFAULT 0.0000," +
            "`account` varchar(60) NOT NULL DEFAULT ''," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Withholding tax codes'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_wht_txn` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`code_id` int(11) NOT NULL DEFAULT 0," +
            "`vendor` varchar(180) NOT NULL DEFAULT ''," +
            "`doc_ref` varchar(80) NOT NULL DEFAULT ''," +
            "`txn_date` varchar(16) NOT NULL DEFAULT ''," +
            "`base_amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`wht_amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`rate` decimal(8,4) NOT NULL DEFAULT 0.0000," +
            "`certificate_no` varchar(60) NOT NULL DEFAULT ''," +
            "`status` varchar(16) NOT NULL DEFAULT 'accrued'," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_code` (`code_id`)," +
            "KEY `x_status` (`status`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Withholding tax transactions'",
            ct);
    }

    /// <summary>epc_cc_centers + epc_cc_allocations (epc_erp_costing.php ensure)</summary>
    public static async Task EnsureCostCentersAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_cc_centers` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`code` varchar(30) NOT NULL," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`branch_id` int(11) NOT NULL DEFAULT 0," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_code` (`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Cost centers'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_cc_allocations` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`run_label` varchar(80) NOT NULL DEFAULT ''," +
            "`source_cost` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`cost_center_id` int(11) NOT NULL DEFAULT 0," +
            "`amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_run` (`run_label`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Cost allocation results'",
            ct);
    }

    /// <summary>epc_costm_item + epc_costm_txn (epc_erp_cost_models.php ensure)</summary>
    public static async Task EnsureCostModelsAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_costm_item` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`item_id` int(11) NOT NULL DEFAULT 0," +
            "`model` varchar(16) NOT NULL DEFAULT 'moving_avg'," +
            "`std_cost` decimal(18,4) NOT NULL DEFAULT 0.0000," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_company_item` (`company_id`,`item_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-item costing model assignment'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_costm_txn` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`item_id` int(11) NOT NULL DEFAULT 0," +
            "`txn_type` varchar(8) NOT NULL DEFAULT 'receipt'," +
            "`qty` decimal(18,4) NOT NULL DEFAULT 0.0000," +
            "`unit_cost` decimal(18,4) NOT NULL DEFAULT 0.0000," +
            "`txn_date` int(11) NOT NULL DEFAULT 0," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_item` (`item_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Costing model transactions'",
            ct);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_costm_close` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`item_id` int(11) NOT NULL DEFAULT 0," +
            "`model` varchar(16) NOT NULL DEFAULT 'moving_avg'," +
            "`label` varchar(40) NOT NULL DEFAULT ''," +
            "`cogs` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`closing_qty` decimal(18,4) NOT NULL DEFAULT 0.0000," +
            "`closing_value` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`variance` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`detail_json` mediumtext," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_item` (`item_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inventory closing/recalculation runs'",
            ct);
    }

    /// <summary>
    /// epc_erp_company_context_ensure (epc_erp_company_context.php): the legal-entity
    /// master must exist AND a fresh tenant auto-seeds one default company from the
    /// single-row tenant profile (epc_co_profile id=1) so GL/company resolution has a
    /// real id — without this a new tenant silently posts company_id=0.
    /// </summary>
    public static async Task EnsureCompanyContextAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_pm_legal_entities` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`name` varchar(190) NOT NULL DEFAULT ''," +
            "`country_code` varchar(4) NOT NULL DEFAULT ''," +
            "`currency_code` varchar(8) NOT NULL DEFAULT ''," +
            "`trn` varchar(40) NOT NULL DEFAULT ''," +
            "`note` varchar(255) NOT NULL DEFAULT ''," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            ct).ConfigureAwait(false);

        try
        {
            var count = await ErpDb.LongAsync(c, null,
                "SELECT COUNT(*) FROM `epc_erp_pm_legal_entities` WHERE `active` = 1",
                ct).ConfigureAwait(false);
            if (count > 0)
            {
                return;
            }
        }
        catch (DbException)
        {
            return; // table could not be created/read on this tenant — leave unscoped
        }

        var legalName = string.Empty;
        var tradeName = string.Empty;
        var baseCurrency = string.Empty;
        var country = string.Empty;
        var trn = string.Empty;
        try
        {
            await using var command = c.CreateCommand();
            command.CommandText =
                "SELECT COALESCE(`legal_name`,''), COALESCE(`trade_name`,'')," +
                " COALESCE(`base_currency`,''), COALESCE(`country`,''), COALESCE(`trn`,'')" +
                " FROM `epc_co_profile` WHERE `id` = 1 LIMIT 1";
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                legalName = Convert.ToString(reader.GetValue(0)) ?? string.Empty;
                tradeName = Convert.ToString(reader.GetValue(1)) ?? string.Empty;
                baseCurrency = Convert.ToString(reader.GetValue(2)) ?? string.Empty;
                country = Convert.ToString(reader.GetValue(3)) ?? string.Empty;
                trn = Convert.ToString(reader.GetValue(4)) ?? string.Empty;
            }
        }
        catch (DbException)
        {
            // epc_co_profile may be absent on lean tenants — seed defaults like PHP.
        }

        var name = (legalName.Length > 0 ? legalName : tradeName).Trim();
        if (name.Length == 0)
        {
            name = "Main Company";
        }

        var currency = baseCurrency.Trim().ToUpperInvariant();
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await ErpDb.ExecuteAsync(c, null,
                "INSERT INTO `epc_erp_pm_legal_entities`" +
                " (`code`,`name`,`country_code`,`currency_code`,`trn`,`note`,`active`,`time_created`,`time_updated`)" +
                " VALUES ('MAIN', @p0, @p1, @p2, @p3, 'Auto-created default company', 1, @p4, @p4)",
                ct,
                name,
                country.Trim().ToUpperInvariant()[..Math.Min(4, country.Trim().ToUpperInvariant().Length)],
                currency,
                trn.Trim(),
                now).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // Insert raced another request seeding the same row — PHP treats ensure as best-effort too.
        }
    }
}
