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
}
