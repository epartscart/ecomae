namespace EcomAE.Platform.Migration;

public static class LegacySurfaceDashboardSql
{
    public const string CountUsers = "SELECT COUNT(*) FROM `users`";

    public const string CountAdminSessions = """
        SELECT COUNT(*) FROM `sessions` WHERE `type` = 1
        """;

    public const string CountPortalTenants = "SELECT COUNT(*) FROM `epc_portal_tenants`";

    public const string CountActivePortalTenants = """
        SELECT COUNT(*) FROM `epc_portal_tenants` WHERE `is_active` = 1
        """;

    /// <summary>Mirrors PHP <c>epc_bos_tenant_list</c> select list (read-only).</summary>
    public const string SelectPortalTenants = """
        SELECT `site_key`, `hostname`, `industry_code`, `status`, `trade_name`, `hub_name`,
               `hosted_on`, `erp_only_shared`, `is_active`, `db_name`
        FROM `epc_portal_tenants`
        WHERE `site_key` != ''
        ORDER BY `status` ASC, `trade_name` ASC
        LIMIT @limit
        """;

    /// <summary>Mirrors PHP <c>epc_erp_cash_bank_total</c> batched balance query.</summary>
    public const string SumCashBankTotal = """
        SELECT IFNULL(SUM(a.`opening_balance`
                + IFNULL(x.in_amt, 0) - IFNULL(x.out_amt, 0)), 0) AS total
        FROM `epc_erp_cash_bank_accounts` a
        LEFT JOIN (
            SELECT `account_id`,
                SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE 0 END) AS in_amt,
                SUM(CASE WHEN `direction` = 0 THEN `amount` ELSE 0 END) AS out_amt
            FROM `epc_erp_cash_bank_entries`
            WHERE `active` = 1
            GROUP BY `account_id`
        ) x ON x.`account_id` = a.`id`
        WHERE a.`active` = 1
        """;

    public const string CountCashAccounts = """
        SELECT COUNT(*) FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1
        """;

    public const string CountActiveSuppliers = """
        SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1
        """;

    public const string CountActivePurchases = """
        SELECT COUNT(*) FROM `epc_erp_purchases` WHERE `active` = 1
        """;

    public const string SumSupplierCredit = """
        SELECT IFNULL(SUM(`amount`), 0) FROM `epc_erp_supplier_accounting`
        WHERE `active` = 1 AND `is_credit` = 1
        """;

    public const string SumSupplierDebit = """
        SELECT IFNULL(SUM(`amount`), 0) FROM `epc_erp_supplier_accounting`
        WHERE `active` = 1 AND `is_credit` = 0
        """;

    /// <summary>PHP <c>erp_dashboard.php</c> receivables tile (<c>epc_invoices</c>).</summary>
    public const string SumErpDashboardReceivables = """
        SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_invoices`
        WHERE `status` <> 'paid'
        """;

    /// <summary>PHP <c>erp_dashboard.php</c> payables tile (<c>epc_bills</c>).</summary>
    public const string SumErpDashboardPayables = """
        SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_bills`
        WHERE `status` <> 'paid'
        """;

    /// <summary>
    /// PHP <c>erp_dashboard.php</c> stock value tile — prefer modern <c>epc_erp_inv_stock</c>,
    /// fall back to legacy <c>epc_inventory_stock</c> when modern table is empty/missing.
    /// </summary>
    public const string SumErpDashboardStockValue = """
        SELECT IF(
            EXISTS(SELECT 1 FROM `epc_erp_inv_stock` LIMIT 1),
            (SELECT COALESCE(SUM(`qty_on_hand` * `avg_unit_cost`), 0) FROM `epc_erp_inv_stock`),
            (SELECT COALESCE(SUM(`quantity` * `avg_cost`), 0) FROM `epc_inventory_stock`)
        )
        """;

    /// <summary>PHP <c>epc_erp_cc_kpi_tiles</c> revenue (ex. VAT) for current month.</summary>
    public const string SumErpCcRevenueExVat = """
        SELECT IFNULL(SUM(CASE WHEN `successfully_created` = 1 THEN `price_total_wt` - `price_total_wt_vat` ELSE 0 END), 0)
        FROM `shop_orders`
        WHERE `time` >= @dateFrom AND `time` <= @dateTo
        """;

    /// <summary>PHP <c>epc_erp_cc_kpi_tiles</c> orders count for current month.</summary>
    public const string CountErpCcOrders = """
        SELECT COUNT(*) FROM `shop_orders`
        WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo
        """;

    /// <summary>PHP command-center AR balance from shop user accounting.</summary>
    public const string SumErpCcArBalance = """
        SELECT IFNULL(SUM(CASE WHEN `income` = 1 THEN `amount` ELSE -`amount` END), 0)
        FROM `shop_users_accounting` WHERE `active` = 1
        """;

    /// <summary>PHP command-center AP balance from supplier balances.</summary>
    public const string SumErpCcApBalance = """
        SELECT IFNULL(SUM(`balance`), 0) FROM `epc_erp_suppliers` WHERE `active` = 1
        """;

    public const string SumErpCcVatOut = """
        SELECT IFNULL(SUM(`price_total_wt_vat`), 0) FROM `shop_orders`
        WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo
        """;

    public const string SumErpCcVatIn = """
        SELECT IFNULL(SUM(`vat_amount`), 0) FROM `epc_erp_purchases`
        WHERE `active` = 1 AND `purchase_date` >= @dateFrom AND `purchase_date` <= @dateTo
        """;

    public const string CountErpCcInventoryItems = """
        SELECT COUNT(*) FROM `epc_erp_inv_items` WHERE `active` = 1
        """;

    public const string SelectErpCcPeriodStatus = """
        SELECT `status` FROM `epc_erp_periods`
        WHERE `period_key` = @periodKey
        LIMIT 1
        """;

    public const string CountErpCcDraftSalesOrders = """
        SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'draft'
        """;

    public const string CountErpCcPendingPurchaseOrders = """
        SELECT COUNT(*) FROM `epc_erp_purchase_orders`
        WHERE `status` IN ('draft', 'pending')
        """;

    public const string CountErpCcUnpostedGlJournals = """
        SELECT COUNT(*) FROM `epc_erp_gl_journals`
        WHERE `status` = 'draft' AND `active` = 1
        """;

    public const string CountErpCcOverdueInvoices = """
        SELECT COUNT(*) FROM `epc_erp_sales_invoices`
        WHERE `status` = 'unpaid' AND `due_date` < @overdueBefore
        """;

    public const string CountErpCcLowStockItems = """
        SELECT COUNT(*) FROM `epc_erp_inv_stock` s
        INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1
        WHERE i.`reorder_level` > 0 AND s.`qty_on_hand` > 0 AND s.`qty_on_hand` <= i.`reorder_level`
        """;

    public const string CountErpCcPendingEinvoices = """
        SELECT COUNT(*) FROM `epc_einvoice_documents`
        WHERE `status` IN ('draft', 'queued')
        """;

    /// <summary>PHP process-flow cases live in <c>epc_pf_cases</c> (not a tasks alias table).</summary>
    public const string CountErpCcProcessOpen = """
        SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'open'
        """;

    public const string CountErpCcProcessDone = """
        SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'done'
        """;

    public const string CountErpCcProcessOverdue = """
        SELECT COUNT(*) FROM `epc_pf_cases`
        WHERE `status` = 'open' AND `due_at` > 0 AND `due_at` < UNIX_TIMESTAMP()
        """;

    /// <summary>
    /// Single-round-trip ERP dashboard KPI batch (PHP erp_dashboard + command-center tiles).
    /// Parameters: @dateFrom, @dateTo, @periodKey, @overdueBefore.
    /// </summary>
    public const string SelectErpDashboardSummaryBatch = """
        SELECT
            (
                SELECT IFNULL(SUM(a.`opening_balance`
                        + IFNULL(x.in_amt, 0) - IFNULL(x.out_amt, 0)), 0)
                FROM `epc_erp_cash_bank_accounts` a
                LEFT JOIN (
                    SELECT `account_id`,
                        SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE 0 END) AS in_amt,
                        SUM(CASE WHEN `direction` = 0 THEN `amount` ELSE 0 END) AS out_amt
                    FROM `epc_erp_cash_bank_entries`
                    WHERE `active` = 1
                    GROUP BY `account_id`
                ) x ON x.`account_id` = a.`id`
                WHERE a.`active` = 1
            ) AS cash_position,
            (SELECT IFNULL(SUM(`amount`), 0) FROM `epc_erp_supplier_accounting` WHERE `active` = 1 AND `is_credit` = 1) AS supplier_credit,
            (SELECT IFNULL(SUM(`amount`), 0) FROM `epc_erp_supplier_accounting` WHERE `active` = 1 AND `is_credit` = 0) AS supplier_debit,
            (SELECT COUNT(*) FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1) AS cash_accounts,
            (SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1) AS active_suppliers,
            (SELECT COUNT(*) FROM `epc_erp_purchases` WHERE `active` = 1) AS active_purchases,
            (SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_invoices` WHERE `status` <> 'paid') AS receivables,
            (SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_bills` WHERE `status` <> 'paid') AS payables,
            (
                SELECT IF(
                    EXISTS(SELECT 1 FROM `epc_erp_inv_stock` LIMIT 1),
                    (SELECT COALESCE(SUM(`qty_on_hand` * `avg_unit_cost`), 0) FROM `epc_erp_inv_stock`),
                    (SELECT COALESCE(SUM(`quantity` * `avg_cost`), 0) FROM `epc_inventory_stock`)
                )
            ) AS stock_value,
            (
                SELECT IFNULL(SUM(CASE WHEN `successfully_created` = 1 THEN `price_total_wt` - `price_total_wt_vat` ELSE 0 END), 0)
                FROM `shop_orders`
                WHERE `time` >= @dateFrom AND `time` <= @dateTo
            ) AS revenue_ex_vat,
            (
                SELECT COUNT(*) FROM `shop_orders`
                WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo
            ) AS orders_count,
            (
                SELECT IFNULL(SUM(CASE WHEN `income` = 1 THEN `amount` ELSE -`amount` END), 0)
                FROM `shop_users_accounting` WHERE `active` = 1
            ) AS ar_balance,
            (SELECT IFNULL(SUM(`balance`), 0) FROM `epc_erp_suppliers` WHERE `active` = 1) AS ap_balance,
            (
                SELECT IFNULL(SUM(`price_total_wt_vat`), 0) FROM `shop_orders`
                WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo
            ) AS vat_out,
            (
                SELECT IFNULL(SUM(`vat_amount`), 0) FROM `epc_erp_purchases`
                WHERE `active` = 1 AND `purchase_date` >= @dateFrom AND `purchase_date` <= @dateTo
            ) AS vat_in,
            (SELECT COUNT(*) FROM `epc_erp_inv_items` WHERE `active` = 1) AS inventory_items,
            IFNULL((SELECT `status` FROM `epc_erp_periods` WHERE `period_key` = @periodKey LIMIT 1), 'open') AS period_status,
            (SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'draft') AS draft_sales_orders,
            (SELECT COUNT(*) FROM `epc_erp_purchase_orders` WHERE `status` IN ('draft', 'pending')) AS pending_purchase_orders,
            (SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `status` = 'draft' AND `active` = 1) AS unposted_gl_journals,
            (
                SELECT COUNT(*) FROM `epc_erp_sales_invoices`
                WHERE `status` = 'unpaid' AND `due_date` < @overdueBefore
            ) AS overdue_invoices,
            (
                SELECT COUNT(*) FROM `epc_erp_inv_stock` s
                INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1
                WHERE i.`reorder_level` > 0 AND s.`qty_on_hand` > 0 AND s.`qty_on_hand` <= i.`reorder_level`
            ) AS low_stock_items,
            (SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `status` IN ('draft', 'queued')) AS pending_einvoices,
            (SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'open') AS process_open,
            (SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'done') AS process_done,
            (
                SELECT COUNT(*) FROM `epc_pf_cases`
                WHERE `status` = 'open' AND `due_at` > 0 AND `due_at` < UNIX_TIMESTAMP()
            ) AS process_overdue
        """;

    /// <summary>Process-flow case KPIs aligned to PHP <c>epc_erp_processflow.php</c> / <c>epc_pf_cases</c>.</summary>
    public const string SelectErpProcessFlowTaskStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_pf_cases`) AS task_count,
            (SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'open') AS open_count,
            (SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'done') AS done_count,
            (SELECT COUNT(*) FROM `epc_pf_cases`
             WHERE `status` = 'open' AND `due_at` > 0 AND `due_at` < UNIX_TIMESTAMP()) AS overdue_count,
            (SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` IN ('cancelled','rejected')) AS cancelled_count
        """;

    /// <summary>
    /// Safe generic report-center table peek (PHP <c>epc_rc_table_rows</c> subset).
    /// Table name is substituted only from an allowlisted registry entry — never from request input.
    /// </summary>
    public const string SelectErpReportCenterTableRowsTemplate = """
        SELECT * FROM `{0}`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Company-scoped variant when the table has <c>company_id</c> (PHP <c>epc_rc_table_rows</c>).</summary>
    public const string SelectErpReportCenterTableRowsByCompanyTemplate = """
        SELECT * FROM `{0}`
        WHERE `company_id` = @companyId
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Detect company_id column before scoped peeks (PHP SHOW COLUMNS LIKE 'company_id').</summary>
    public const string SelectErpReportCenterHasCompanyIdTemplate = """
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND COLUMN_NAME = 'company_id'
        """;

    /// <summary>Process-flow cases — omits comments/step detail; PHP processflow UI remains authoritative.</summary>
    public const string SelectErpProcessFlowTasks = """
        SELECT `id`, IFNULL(`process_id`,0) AS process_id, IFNULL(`title`,'') AS title,
               IFNULL(`reference`,'') AS reference, IFNULL(`priority`,'') AS priority,
               IFNULL(`status`,'') AS status, IFNULL(`current_step_no`,0) AS current_step_no,
               IFNULL(`current_assignee_id`,0) AS current_assignee_id,
               IFNULL(`current_department`,'') AS current_department,
               IFNULL(`initiator_id`,0) AS initiator_id,
               IFNULL(`subject_type`,'') AS subject_type, IFNULL(`subject_id`,0) AS subject_id,
               IFNULL(`started_at`,0) AS started_at, IFNULL(`due_at`,0) AS due_at,
               IFNULL(`completed_at`,0) AS completed_at,
               IFNULL(`time_created`,0) AS time_created, IFNULL(`time_updated`,0) AS time_updated
        FROM `epc_pf_cases`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string CountCustomerOrders = """
        SELECT COUNT(*) FROM `shop_orders` WHERE `user_id` = @userId
        """;

    public const string CountCustomerSessionsForUser = """
        SELECT COUNT(*) FROM `sessions` WHERE `user_id` = @userId
        """;

    public const string SelectCustomerOrders = """
        SELECT `id`, `time`, `paid`, `successfully_created`, `status`
        FROM `shop_orders`
        WHERE `user_id` = @userId
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>
    /// Guest checkout lookup (PHP <c>ajax_check_order_not_authorized.php</c>).
    /// Never omit <c>user_id = 0</c> — registered orders must not leak here.
    /// Email/phone are matched in C# after fetch.
    /// </summary>
    public const string SelectGuestOrder = """
        SELECT o.`id`,
               IFNULL(o.`time`, 0) AS time,
               IFNULL(o.`paid`, 0) AS paid,
               IFNULL(o.`successfully_created`, 0) AS successfully_created,
               IFNULL(o.`status`, 0) AS status,
               IFNULL(o.`office_id`, 0) AS office_id,
               IFNULL(o.`email_not_auth`, '') AS email_not_auth,
               IFNULL(o.`phone_not_auth`, '') AS phone_not_auth,
               IFNULL((
                   SELECT SUM(i.`price` * i.`count_need`)
                   FROM `shop_orders_items` i
                   WHERE i.`order_id` = o.`id`
               ), 0) AS sum,
               IFNULL((
                   SELECT m.`caption`
                   FROM `shop_obtaining_modes` m
                   WHERE m.`id` = o.`how_get`
                   LIMIT 1
               ), '') AS obtain_caption
        FROM `shop_orders` o
        WHERE o.`id` = @orderId
          AND o.`user_id` = 0
        LIMIT 1
        """;

    /// <summary>Customer-scoped order lines (PHP <c>my_orders_items.php</c>). Never omit the user join.</summary>
    public const string SelectCustomerOrderItems = """
        SELECT i.`id`, i.`order_id`,
               IFNULL(i.`t2_manufacturer`, '') AS brand,
               IFNULL(i.`t2_article`, '') AS article,
               IFNULL(i.`t2_name`, '') AS name,
               IFNULL(i.`price`, 0) AS price,
               IFNULL(i.`count_need`, 0) AS count_need,
               IFNULL(i.`status`, 0) AS status
        FROM `shop_orders_items` i
        INNER JOIN `shop_orders` o ON o.`id` = i.`order_id`
        WHERE o.`user_id` = @userId
          AND (@orderId = 0 OR i.`order_id` = @orderId)
        ORDER BY i.`order_id` DESC, i.`id` ASC
        LIMIT @limit
        """;

    /// <summary>Customer-scoped order thread (PHP <c>my_order.php</c> messages). Never omit the user join.</summary>
    public const string SelectCustomerOrderMessages = """
        SELECT m.`id`, IFNULL(m.`time`, 0) AS time, IFNULL(m.`text`, '') AS text, IFNULL(m.`is_customer`, 0) AS is_customer
        FROM `shop_orders_messages` m
        INNER JOIN `shop_orders` o ON o.`id` = m.`order_id`
        WHERE o.`user_id` = @userId
          AND m.`order_id` = @orderId
          AND IFNULL(m.`return_id`, 0) = 0
        ORDER BY m.`id` ASC
        LIMIT @limit
        """;

    /// <summary>Customer-scoped returns list (PHP <c>shop/returns/returns.php</c>). Never omit user_id.</summary>
    public const string SelectCustomerReturns = """
        SELECT r.`id`,
               IFNULL(r.`status_id`, 0) AS status_id,
               IFNULL(s.`caption`, '') AS status,
               0 AS time_unix,
               IFNULL((
                   SELECT i.`order_id`
                   FROM `shop_orders_returns_items` ri
                   INNER JOIN `shop_orders_items` i ON i.`id` = ri.`item_id`
                   WHERE ri.`return_id` = r.`id`
                   LIMIT 1
               ), 0) AS order_id
        FROM `shop_orders_returns` r
        LEFT JOIN `shop_orders_returns_statuses` s ON s.`id` = r.`status_id`
        WHERE r.`user_id` = @userId
        ORDER BY r.`id` DESC
        LIMIT @limit
        """;

    /// <summary>Customer-scoped return lines (PHP <c>shop/returns/return.php</c>).</summary>
    public const string SelectCustomerReturnItems = """
        SELECT ri.`id`, ri.`return_id`, ri.`item_id`,
               IFNULL(ri.`reason_id`, 0) AS reason_id,
               IFNULL(rs.`caption`, '') AS reason,
               IFNULL(oi.`order_id`, 0) AS order_id,
               IFNULL(oi.`t2_manufacturer`, '') AS brand,
               IFNULL(oi.`t2_article`, '') AS article,
               IFNULL(oi.`t2_name`, '') AS name,
               IFNULL(oi.`price`, 0) AS price,
               IFNULL(oi.`count_need`, 0) AS count_need
        FROM `shop_orders_returns_items` ri
        INNER JOIN `shop_orders_returns` r ON r.`id` = ri.`return_id`
        LEFT JOIN `shop_orders_returns_reasons` rs ON rs.`id` = ri.`reason_id`
        LEFT JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
        WHERE r.`user_id` = @userId
          AND ri.`return_id` = @returnId
        ORDER BY ri.`id` ASC
        LIMIT @limit
        """;

    /// <summary>Customer-scoped return thread. Never omit the user join. Does not mark messages read.</summary>
    public const string SelectCustomerReturnMessages = """
        SELECT m.`id`, IFNULL(m.`time`, 0) AS time, IFNULL(m.`text`, '') AS text, IFNULL(m.`is_customer`, 0) AS is_customer
        FROM `shop_orders_messages` m
        INNER JOIN `shop_orders_returns` r ON r.`id` = m.`return_id`
        WHERE r.`user_id` = @userId
          AND m.`return_id` = @returnId
        ORDER BY m.`id` ASC
        LIMIT @limit
        """;

    /// <summary>Customer VIN / seller-request inbox (PHP <c>content/requests/requests.php</c>).</summary>
    public const string SelectCustomerVinRequests = """
        SELECT `id`, IFNULL(`time`, 0) AS time_unix, IFNULL(`viewed_customer`, 0) AS viewed_customer
        FROM `users_vin`
        WHERE `user_id` = @userId
        ORDER BY `viewed_customer` ASC, `id` DESC
        LIMIT @limit
        """;

    /// <summary>One VIN request header. Text is HTML from PHP send_vin_email — shown as source only.</summary>
    public const string SelectCustomerVinRequestById = """
        SELECT `id`, IFNULL(`time`, 0) AS time_unix, IFNULL(`viewed_customer`, 0) AS viewed_customer,
               IFNULL(`text`, '') AS text
        FROM `users_vin`
        WHERE `user_id` = @userId
          AND `id` = @requestId
        LIMIT 1
        """;

    /// <summary>Customer VIN request thread (PHP <c>ajax_get_message.php</c>). Ownership via users_vin join.</summary>
    public const string SelectCustomerVinRequestMessages = """
        SELECT m.`id`, IFNULL(m.`time`, 0) AS time, IFNULL(m.`text`, '') AS text, IFNULL(m.`is_customer`, 0) AS is_customer
        FROM `users_vin_messages` m
        INNER JOIN `users_vin` v ON v.`id` = m.`vin_id`
        WHERE v.`user_id` = @userId
          AND m.`vin_id` = @requestId
        ORDER BY m.`id` ASC
        LIMIT @limit
        """;

    /// <summary>Garage notepad lines (PHP <c>notepad.php</c>). Ownership via garage user_id join.</summary>
    public const string SelectCustomerGarageNotepad = """
        SELECT n.`id`, n.`garage_id`,
               IFNULL(n.`brend`, '') AS brend,
               IFNULL(n.`article`, '') AS article,
               IFNULL(n.`name`, '') AS name,
               IFNULL(n.`exist`, 0) AS exist,
               IFNULL(n.`price`, 0) AS price,
               IFNULL(n.`comment`, '') AS comment
        FROM `shop_docpart_garage_notepad` n
        INNER JOIN `shop_docpart_garage` g ON g.`id` = n.`garage_id` AND g.`user_id` = n.`user_id`
        WHERE n.`user_id` = @userId
          AND n.`garage_id` = @garageId
        ORDER BY n.`id` DESC
        LIMIT @limit
        """;

    public const string SelectCustomerPriceGroup = """
        SELECT IFNULL(`group_id`, 0) AS group_id
        FROM `users_groups_bind`
        WHERE `user_id` = @userId
        ORDER BY `group_id` ASC
        LIMIT 1
        """;

    /// <summary>
    /// CP OMS recent orders (platform-wide read digest).
    /// Office-manager ACL filtering remains PHP-authoritative.
    /// </summary>
    /// <summary>Core columns only — fallback when OMS enrichment joins are missing on a tenant.</summary>
    public const string SelectCpShopOrdersCore = """
        SELECT o.`id`, o.`time`, o.`user_id`, o.`status`, o.`paid`,
               IFNULL(o.`paid_type`, 0) AS paid_type,
               IFNULL(o.`office_id`, 0) AS office_id,
               IFNULL(o.`successfully_created`, 0) AS successfully_created,
               IFNULL((SELECT COUNT(*) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0) AS count_items,
               IFNULL((SELECT SUM(i.`price` * i.`count_need`) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0) AS order_sum
        FROM `shop_orders` o
        ORDER BY o.`id` DESC
        LIMIT @limit
        """;

    public const string SelectCpShopOrders = """
        SELECT o.`id`, o.`time`, o.`user_id`, o.`status`, o.`paid`,
               IFNULL(o.`paid_type`, 0) AS paid_type,
               IFNULL(o.`office_id`, 0) AS office_id,
               IFNULL(o.`successfully_created`, 0) AS successfully_created,
               IFNULL((SELECT COUNT(*) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0) AS count_items,
               IFNULL((SELECT SUM(i.`price` * i.`count_need`) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0) AS order_sum,
               IFNULL((SELECT SUM(i.`t2_price_purchase` * i.`count_need`) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0) AS purchase_sum,
               GREATEST(
                   IFNULL((SELECT MAX(l.`time`) FROM `shop_orders_logs` l WHERE l.`order_id` = o.`id`), 0),
                   IFNULL((SELECT MAX(m.`time`) FROM `shop_orders_messages` m WHERE m.`order_id` = o.`id`), 0),
                   IFNULL(o.`time`, 0)
               ) AS last_modified,
               IFNULL((SELECT v.`viewed_flag` FROM `shop_orders_viewed` v WHERE v.`order_id` = o.`id` LIMIT 1), 1) AS viewed_flag,
               IFNULL((SELECT u.`email` FROM `users` u WHERE u.`user_id` = o.`user_id` LIMIT 1), '') AS customer_label,
               IFNULL((SELECT s.`name` FROM `shop_orders_statuses_ref` s WHERE s.`id` = o.`status` LIMIT 1), '') AS status_name,
               IFNULL((SELECT s.`for_finish` FROM `shop_orders_statuses_ref` s WHERE s.`id` = o.`status` LIMIT 1), 0) AS status_for_finish,
               IFNULL((SELECT s.`for_inverse` FROM `shop_orders_statuses_ref` s WHERE s.`id` = o.`status` LIMIT 1), 0) AS status_for_inverse,
               IFNULL((SELECT s.`for_created` FROM `shop_orders_statuses_ref` s WHERE s.`id` = o.`status` LIMIT 1), 0) AS status_for_created,
               IFNULL((SELECT om.`caption` FROM `shop_obtaining_modes` om WHERE om.`id` = o.`how_get` LIMIT 1), '') AS obtain_caption
        FROM `shop_orders` o
        ORDER BY o.`id` DESC
        LIMIT @limit
        """;

    /// <summary>Open = statuses that are not finished and not canceled (PHP epc_orders_ws_open_status_ids).</summary>
    public const string CountCpOrdersOpen = """
        SELECT COUNT(*) FROM `shop_orders`
        WHERE `status` IN (
            SELECT `id` FROM `shop_orders_statuses_ref`
            WHERE `for_inverse` != 1 AND `for_finish` != 1
        )
        """;

    public const string CountCpOrdersToday = """
        SELECT COUNT(*) FROM `shop_orders` WHERE `time` >= @todayStart
        """;

    /// <summary>Pending ship ≈ open-ish statuses with paid IN (1,2) — PHP epc_orders_ws_kpi pending_ship (all offices).</summary>
    public const string CountCpOrdersPendingShip = """
        SELECT COUNT(*) FROM `shop_orders`
        WHERE `paid` IN (1, 2)
          AND `status` IN (
              SELECT `id` FROM `shop_orders_statuses_ref`
              WHERE `for_finish` != 1 AND `for_inverse` != 1
          )
        """;

    public const string CountCpOrdersCompleted = """
        SELECT COUNT(*) FROM `shop_orders`
        WHERE `status` IN (
            SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1
        )
        """;

    public const string SelectCpShopOrderById = """
        SELECT o.`id`, o.`time`, o.`user_id`, o.`status`, o.`paid`,
               IFNULL(o.`paid_type`, 0) AS paid_type,
               IFNULL(o.`office_id`, 0) AS office_id,
               IFNULL(o.`successfully_created`, 0) AS successfully_created,
               IFNULL((SELECT COUNT(*) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0) AS count_items,
               IFNULL((SELECT SUM(i.`price` * i.`count_need`) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0) AS order_sum,
               IFNULL((SELECT SUM(i.`t2_price_purchase` * i.`count_need`) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0) AS purchase_sum,
               GREATEST(
                   IFNULL((SELECT MAX(l.`time`) FROM `shop_orders_logs` l WHERE l.`order_id` = o.`id`), 0),
                   IFNULL((SELECT MAX(m.`time`) FROM `shop_orders_messages` m WHERE m.`order_id` = o.`id`), 0),
                   IFNULL(o.`time`, 0)
               ) AS last_modified,
               1 AS viewed_flag,
               IFNULL((SELECT u.`email` FROM `users` u WHERE u.`user_id` = o.`user_id` LIMIT 1), '') AS customer_label,
               IFNULL((SELECT s.`name` FROM `shop_orders_statuses_ref` s WHERE s.`id` = o.`status` LIMIT 1), '') AS status_name,
               IFNULL((SELECT s.`for_finish` FROM `shop_orders_statuses_ref` s WHERE s.`id` = o.`status` LIMIT 1), 0) AS status_for_finish,
               IFNULL((SELECT s.`for_inverse` FROM `shop_orders_statuses_ref` s WHERE s.`id` = o.`status` LIMIT 1), 0) AS status_for_inverse,
               IFNULL((SELECT s.`for_created` FROM `shop_orders_statuses_ref` s WHERE s.`id` = o.`status` LIMIT 1), 0) AS status_for_created,
               IFNULL((SELECT om.`caption` FROM `shop_obtaining_modes` om WHERE om.`id` = o.`how_get` LIMIT 1), '') AS obtain_caption,
               IFNULL((SELECT u.`phone` FROM `users` u WHERE u.`user_id` = o.`user_id` LIMIT 1), '') AS customer_phone,
               IFNULL((
                   SELECT SUM(`amount`) FROM `shop_users_accounting`
                   WHERE `active` = 1 AND `income` = 0 AND `order_id` = o.`id`
               ), 0) -
               IFNULL((
                   SELECT SUM(`amount`) FROM `shop_users_accounting`
                   WHERE `active` = 1 AND `income` = 1 AND `order_id` = o.`id`
               ), 0) AS paid_sum
        FROM `shop_orders` o
        WHERE o.`id` = @orderId
        LIMIT 1
        """;

    public const string SelectCpShopOrderItems = """
        SELECT i.`id`, i.`order_id`,
               IFNULL(i.`t2_manufacturer`, '') AS brand,
               IFNULL(i.`t2_article`, '') AS article,
               IFNULL(i.`t2_name`, '') AS name,
               IFNULL(i.`price`, 0) AS price,
               IFNULL(i.`count_need`, 0) AS count_need,
               IFNULL(i.`t2_price_purchase`, 0) AS purchase,
               IFNULL(i.`status`, 0) AS status,
               '' AS status_name,
               '' AS storage_label
        FROM `shop_orders_items` i
        WHERE i.`order_id` = @orderId
        ORDER BY i.`id` ASC
        """;

    public const string SelectCpShopOrderLogs = """
        SELECT IFNULL(`time`, 0) AS time, IFNULL(`text`, '') AS text,
               IFNULL(`is_manager`, 0) AS is_manager, IFNULL(`is_robot`, 0) AS is_robot
        FROM `shop_orders_logs`
        WHERE `order_id` = @orderId
        ORDER BY `id` DESC
        LIMIT 40
        """;

    public const string SelectCpShopOrderMessages = """
        SELECT `id`, IFNULL(`time`, 0) AS time, IFNULL(`text`, '') AS text, IFNULL(`is_customer`, 0) AS is_customer
        FROM `shop_orders_messages`
        WHERE `order_id` = @orderId AND IFNULL(`return_id`, 0) = 0
        ORDER BY `id` ASC
        LIMIT 80
        """;

    public const string SelectCpUsers = """
        SELECT `user_id`, `email`, `phone`, `unlocked`, `time_registered`, `time_last_visit`
        FROM `users`
        ORDER BY `user_id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP <c>users/usermanager/user</c> core row.</summary>
    public const string SelectCpUserById = """
        SELECT `user_id`, `email`, `email_confirmed`, `phone`, `phone_confirmed`,
               `unlocked`, `reg_variant`, `time_registered`, `time_last_visit`
        FROM `users`
        WHERE `user_id` = @userId
        LIMIT 1
        """;

    /// <summary>PHP user.php groups tree bind.</summary>
    public const string SelectCpUserGroups = """
        SELECT g.`id`, g.`value`, g.`for_backend`, g.`unblocked`
        FROM `users_groups_bind` b
        INNER JOIN `groups` g ON g.`id` = b.`group_id`
        WHERE b.`user_id` = @userId
        ORDER BY g.`level` ASC, g.`id` ASC
        LIMIT 100
        """;

    /// <summary>PHP user.php / user_manager balance (income − issue).</summary>
    public const string SelectCpUserBalance = """
        SELECT IFNULL((
                   SELECT SUM(`amount`) FROM `shop_users_accounting`
                   WHERE `user_id` = @userId AND `income` = 1 AND `active` = 1
               ), 0)
             - IFNULL((
                   SELECT SUM(`amount`) FROM `shop_users_accounting`
                   WHERE `user_id` = @userId AND `income` = 0 AND `active` = 1
               ), 0) AS balance
        """;

    public const string SelectCpGroups = """
        SELECT `id`, `value`, `for_backend`, `for_guests`, `for_registrated`, `unblocked`, `parent`, `level`
        FROM `groups`
        ORDER BY `level` ASC, `id` ASC
        LIMIT @limit
        """;

    public const string SelectErpSuppliers = """
        SELECT s.`id`, s.`name`, s.`storage_id`,
            IFNULL((SELECT SUM(`amount`) FROM `epc_erp_supplier_accounting`
                    WHERE `supplier_id` = s.`id` AND `active` = 1 AND `is_credit` = 1), 0)
            - IFNULL((SELECT SUM(`amount`) FROM `epc_erp_supplier_accounting`
                    WHERE `supplier_id` = s.`id` AND `active` = 1 AND `is_credit` = 0), 0) AS balance
        FROM `epc_erp_suppliers` s
        WHERE s.`active` = 1
        ORDER BY s.`name` ASC
        LIMIT @limit
        """;

    public const string SelectErpPurchases = """
        SELECT p.`id`, p.`supplier_id`, s.`name` AS supplier_name, p.`purchase_date`,
               p.`invoice_number`, p.`total_amount`, p.`status`, p.`order_id`
        FROM `epc_erp_purchases` p
        INNER JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id`
        WHERE p.`active` = 1
        ORDER BY p.`purchase_date` DESC, p.`id` DESC
        LIMIT @limit
        """;

    public const string CountCustomerGarage = """
        SELECT COUNT(*) FROM `shop_docpart_garage` WHERE `user_id` = @userId
        """;

    public const string SelectCustomerGarage = """
        SELECT `id`, `caption`, `marka`, `model`, `year`, `vin`, `active`
        FROM `shop_docpart_garage`
        WHERE `user_id` = @userId
        ORDER BY `active` DESC, `caption` ASC
        LIMIT @limit
        """;

    /// <summary>Mirrors PHP <c>epc_erp_list_cash_accounts</c> with balance calculation.</summary>
    public const string SelectErpCashAccounts = """
        SELECT a.`id`, a.`name`, a.`account_type`, IFNULL(a.`currency_code`, '') AS currency_code,
               a.`opening_balance`,
               (a.`opening_balance` + IFNULL(x.in_amt, 0) - IFNULL(x.out_amt, 0)) AS balance
        FROM `epc_erp_cash_bank_accounts` a
        LEFT JOIN (
            SELECT `account_id`,
                SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE 0 END) AS in_amt,
                SUM(CASE WHEN `direction` = 0 THEN `amount` ELSE 0 END) AS out_amt
            FROM `epc_erp_cash_bank_entries`
            WHERE `active` = 1
            GROUP BY `account_id`
        ) x ON x.`account_id` = a.`id`
        WHERE a.`active` = 1
        ORDER BY a.`account_type` ASC, a.`name` ASC
        LIMIT @limit
        """;

    public const string SelectStorefrontUserCore = """
        SELECT `user_id`, `email`, `email_confirmed`, `phone`, `phone_confirmed`, `reg_variant`
        FROM `users`
        WHERE `user_id` = @userId
        LIMIT 1
        """;

    public const string SelectStorefrontUserProfiles = """
        SELECT `data_key`, `data_value`
        FROM `users_profiles`
        WHERE `user_id` = @userId
        ORDER BY `data_key` ASC
        LIMIT 200
        """;

    public const string SelectErpCashEntries = """
        SELECT e.`id`, e.`account_id`, a.`name` AS account_name, a.`account_type`,
               e.`time`, e.`direction`, e.`amount`,
               IFNULL(e.`reference`, '') AS reference, IFNULL(e.`note`, '') AS note
        FROM `epc_erp_cash_bank_entries` e
        INNER JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = e.`account_id`
        WHERE e.`active` = 1
        ORDER BY e.`time` DESC, e.`id` DESC
        LIMIT @limit
        """;

    public const string SelectErpCashEntriesForAccount = """
        SELECT e.`id`, e.`account_id`, a.`name` AS account_name, a.`account_type`,
               e.`time`, e.`direction`, e.`amount`,
               IFNULL(e.`reference`, '') AS reference, IFNULL(e.`note`, '') AS note
        FROM `epc_erp_cash_bank_entries` e
        INNER JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = e.`account_id`
        WHERE e.`active` = 1 AND e.`account_id` = @accountId
        ORDER BY e.`time` DESC, e.`id` DESC
        LIMIT @limit
        """;

    public const string SelectErpInvoices = """
        SELECT d.`id`, IFNULL(d.`invoice_number`, '') AS invoice_number, d.`order_id`, d.`user_id`,
               IFNULL(u.`email`, '') AS customer_email, d.`issue_date`,
               IFNULL(d.`status`, '') AS status, IFNULL(d.`total_incl_vat`, 0) AS total_incl_vat
        FROM `epc_einvoice_documents` d
        LEFT JOIN `users` u ON u.`user_id` = d.`user_id`
        WHERE d.`active` = 1
        ORDER BY d.`issue_date` DESC, d.`id` DESC
        LIMIT @limit
        """;

    public const string SelectErpGlJournals = """
        SELECT j.`id`, IFNULL(j.`journal_no`, '') AS journal_no, j.`journal_date`,
               IFNULL(j.`source_type`, '') AS source_type, IFNULL(j.`source_id`, 0) AS source_id,
               IF(j.`active` = 1, 'posted', 'void') AS status,
               (SELECT IFNULL(SUM(`debit`), 0) FROM `epc_erp_gl_lines` WHERE `journal_id` = j.`id`) AS total_debit
        FROM `epc_erp_gl_journals` j
        WHERE j.`active` = 1
        ORDER BY j.`journal_date` DESC, j.`id` DESC
        LIMIT @limit
        """;

    public const string SelectCpModules = """
        SELECT `id`, IFNULL(`caption`, '') AS caption, `activated`, `is_frontend`,
               `is_prototype`, `control_available`
        FROM `modules`
        WHERE `is_prototype` = 0
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    public const string SelectCpConfigItemsMeta = """
        SELECT `name`, IFNULL(`caption`, '') AS caption, IFNULL(`type`, '') AS type,
               IFNULL(`config_group`, '') AS config_group, `visible`, `order`
        FROM `config_items`
        ORDER BY `order` ASC, `name` ASC
        LIMIT @limit
        """;

    /// <summary>COA with current balance (PHP <c>epc_erp_gl_list_coa</c> signed balance).</summary>
    public const string SelectErpCoaAccounts = """
        SELECT a.`id`, IFNULL(a.`code`, '') AS code, IFNULL(a.`name`, '') AS name,
               IFNULL(a.`account_type`, '') AS account_type, IFNULL(a.`normal_side`, '') AS normal_side,
               IFNULL(a.`parent_id`, 0) AS parent_id, IFNULL(a.`opening_balance`, 0) AS opening_balance,
               CASE
                 WHEN IFNULL(a.`normal_side`, 'debit') = 'credit'
                 THEN IFNULL(a.`opening_balance`, 0) + IFNULL(x.credits, 0) - IFNULL(x.debits, 0)
                 ELSE IFNULL(a.`opening_balance`, 0) + IFNULL(x.debits, 0) - IFNULL(x.credits, 0)
               END AS balance,
               a.`active`
        FROM `epc_erp_coa_accounts` a
        LEFT JOIN (
            SELECT l.`coa_id`,
                   IFNULL(SUM(l.`debit`), 0) AS debits,
                   IFNULL(SUM(l.`credit`), 0) AS credits
            FROM `epc_erp_gl_lines` l
            INNER JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1
            GROUP BY l.`coa_id`
        ) x ON x.`coa_id` = a.`id`
        WHERE a.`active` = 1
        ORDER BY a.`code` ASC
        LIMIT @limit
        """;

    public const string SelectErpWarehouses = """
        SELECT `id`, IFNULL(`storage_id`, 0) AS storage_id, IFNULL(`code`, '') AS code,
               IFNULL(`name`, '') AS name, `active`, IFNULL(`time_created`, 0) AS time_created
        FROM `epc_erp_inv_warehouses`
        WHERE `active` = 1
        ORDER BY `name` ASC
        LIMIT @limit
        """;

    public const string SelectErpSalesOrders = """
        SELECT `id`, IFNULL(`so_no`, '') AS so_no, IFNULL(`customer_user_id`, 0) AS customer_user_id,
               IFNULL(`total_amount`, 0) AS total_amount, IFNULL(`status`, '') AS status,
               IFNULL(`time_created`, 0) AS time_created
        FROM `epc_erp_sales_orders`
        ORDER BY `time_created` DESC, `id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP <c>erp_tabs_sales_orders.php</c> line picker source.</summary>
    public const string SelectErpInventoryItemsForPicker = """
        SELECT `id`, IFNULL(`sku`, '') AS sku, IFNULL(`name`, '') AS name,
               IFNULL(`sales_price`, 0) AS sales_price, IFNULL(`purchase_price`, 0) AS purchase_price
        FROM `epc_erp_inv_items`
        WHERE `active` = 1
        ORDER BY `sku` ASC
        LIMIT @limit
        """;

    public const string SelectErpSalesOrderLines = """
        SELECT `l`.`id` AS line_id, `l`.`sales_order_id` AS document_id, IFNULL(`l`.`item_code`, '') AS item_code,
               IFNULL(`l`.`description`, '') AS description, IFNULL(`l`.`qty`, 0) AS qty,
               IFNULL(`l`.`unit_price_ex_vat`, 0) AS unit_price_ex_vat, IFNULL(`l`.`line_ex_vat`, 0) AS line_ex_vat
        FROM `epc_erp_sales_order_lines` `l`
        WHERE `l`.`sales_order_id` IN (
            SELECT `id` FROM (
                SELECT `id` FROM `epc_erp_sales_orders` ORDER BY `time_created` DESC, `id` DESC LIMIT @limit
            ) `h`)
        ORDER BY `l`.`sales_order_id` DESC, `l`.`line_no` ASC, `l`.`id` ASC
        """;

    public const string SelectCpMenus = """
        SELECT `id`, IFNULL(`caption`, '') AS caption, `is_frontend`,
               IFNULL(`menu_ul_class`, '') AS menu_ul_class, IFNULL(`menu_ul_id`, '') AS menu_ul_id,
               IFNULL(`structure`, '') AS structure
        FROM `menu`
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    public const string SelectCpPages = """
        SELECT `id`, IFNULL(`value`, '') AS caption, IFNULL(`url`, '') AS url,
               IFNULL(`alias`, '') AS alias, `is_frontend`, IFNULL(`published_flag`, 0) AS published_flag,
               IFNULL(`level`, 0) AS level, IFNULL(`order`, 0) AS sort_order
        FROM `content`
        ORDER BY `level` ASC, `order` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>Admin session metadata only — never selects the raw session token column.</summary>
    public const string SelectCpAdminSessions = """
        SELECT s.`user_id`, IFNULL(u.`email`, '') AS email, s.`type`, COUNT(*) AS session_count
        FROM `sessions` s
        LEFT JOIN `users` u ON u.`user_id` = s.`user_id`
        WHERE s.`type` = 1
        GROUP BY s.`user_id`, u.`email`, s.`type`
        ORDER BY session_count DESC, s.`user_id` ASC
        LIMIT @limit
        """;

    public const string SelectCpStorages = """
        SELECT `id`, IFNULL(`name`, '') AS name, IFNULL(`short_name`, '') AS short_name,
               IFNULL(`hidden`, 0) AS hidden
        FROM `shop_storages`
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    public const string SelectBosAuditLog = """
        SELECT `id`, `ts`, `user_id`, IFNULL(`actor`, '') AS actor, IFNULL(`area`, '') AS area,
               IFNULL(`action`, '') AS action, IFNULL(`target`, '') AS target, IFNULL(`ip`, '') AS ip
        FROM `epc_boc_audit`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectBosAuditLogForArea = """
        SELECT `id`, `ts`, `user_id`, IFNULL(`actor`, '') AS actor, IFNULL(`area`, '') AS area,
               IFNULL(`action`, '') AS action, IFNULL(`target`, '') AS target, IFNULL(`ip`, '') AS ip
        FROM `epc_boc_audit`
        WHERE `area` = @area
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpPurchaseOrders = """
        SELECT `id`, IFNULL(`po_no`, '') AS po_no, IFNULL(`supplier_id`, 0) AS supplier_id,
               IFNULL(`title`, '') AS title, IFNULL(`total_amount`, 0) AS total_amount,
               IFNULL(`status`, '') AS status, IFNULL(`time_created`, 0) AS time_created
        FROM `epc_erp_purchase_orders`
        ORDER BY `time_created` DESC, `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpPurchaseOrderLines = """
        SELECT `l`.`id` AS line_id, `l`.`po_id` AS document_id, IFNULL(`l`.`item_code`, '') AS item_code,
               IFNULL(`l`.`description`, '') AS description, IFNULL(`l`.`qty`, 0) AS qty,
               IFNULL(`l`.`unit_cost_ex_vat`, 0) AS unit_price_ex_vat, IFNULL(`l`.`line_ex_vat`, 0) AS line_ex_vat,
               IFNULL(`l`.`qty_received`, 0) AS qty_received
        FROM `epc_erp_po_lines` `l`
        WHERE `l`.`po_id` IN (
            SELECT `id` FROM (
                SELECT `id` FROM `epc_erp_purchase_orders` ORDER BY `time_created` DESC, `id` DESC LIMIT @limit
            ) `h`)
        ORDER BY `l`.`po_id` DESC, `l`.`line_no` ASC, `l`.`id` ASC
        """;

    /// <summary>PHP <c>epc_erp_purchase_inv_lines</c> receipt lines resolved to the inventory item SKU/name.</summary>
    public const string SelectErpPurchaseInvoiceLines = """
        SELECT `l`.`id` AS line_id, `l`.`purchase_id` AS document_id, IFNULL(`i`.`sku`, '') AS item_code,
               IFNULL(`i`.`name`, '') AS description, IFNULL(`l`.`qty`, 0) AS qty,
               IFNULL(`l`.`unit_cost`, 0) AS unit_price_ex_vat,
               ROUND(IFNULL(`l`.`qty`, 0) * IFNULL(`l`.`unit_cost`, 0), 2) AS line_ex_vat
        FROM `epc_erp_purchase_inv_lines` `l`
        LEFT JOIN `epc_erp_inv_items` `i` ON `i`.`id` = `l`.`item_id`
        WHERE `l`.`purchase_id` IN (
            SELECT `id` FROM (
                SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1
                ORDER BY `purchase_date` DESC, `id` DESC LIMIT @limit
            ) `h`)
        ORDER BY `l`.`purchase_id` DESC, `l`.`id` ASC
        """;

    public const string SelectErpInventoryStockSummary = """
        SELECT COUNT(*) AS row_count,
               IFNULL(SUM(`qty_on_hand`), 0) AS qty_on_hand,
               IFNULL(SUM(`qty_on_hand` * `avg_unit_cost`), 0) AS stock_value,
               COUNT(DISTINCT `warehouse_id`) AS warehouse_count,
               COUNT(DISTINCT `item_id`) AS item_count
        FROM `epc_erp_inv_stock`
        """;

    /// <summary>
    /// PHP <c>epc_erp_inventory_stock_report</c> join — active items only; optional warehouse filter.
    /// </summary>
    public const string SelectErpInventoryStockRows = """
        SELECT s.`id`, s.`warehouse_id`, s.`item_id`,
               IFNULL(i.`sku`, '') AS sku, IFNULL(i.`name`, '') AS name,
               IFNULL(i.`item_type`, '') AS item_type, IFNULL(i.`unit`, '') AS unit,
               IFNULL(w.`name`, '') AS warehouse_name,
               IFNULL(s.`qty_on_hand`, 0) AS qty_on_hand,
               IFNULL(s.`avg_unit_cost`, 0) AS avg_unit_cost,
               IFNULL(s.`batch_no`, '') AS batch_no,
               IFNULL(s.`variant_label`, '') AS variant_label,
               IFNULL(s.`expiry_date`, '') AS expiry_date,
               IFNULL(s.`time_updated`, 0) AS time_updated
        FROM `epc_erp_inv_stock` s
        INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1
        INNER JOIN `epc_erp_inv_warehouses` w ON w.`id` = s.`warehouse_id`
        WHERE (@warehouseId = 0 OR s.`warehouse_id` = @warehouseId)
        ORDER BY w.`name`, i.`sku`, s.`batch_no`
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_erp_inventory_low_stock_lines</c>.</summary>
    public const string SelectErpInventoryLowStockRows = """
        SELECT s.`id`, s.`warehouse_id`, s.`item_id`,
               IFNULL(i.`sku`, '') AS sku, IFNULL(i.`name`, '') AS name,
               IFNULL(w.`name`, '') AS warehouse_name,
               IFNULL(s.`qty_on_hand`, 0) AS qty_on_hand,
               IFNULL(i.`reorder_level`, 0) AS reorder_level,
               IFNULL(s.`avg_unit_cost`, 0) AS avg_unit_cost
        FROM `epc_erp_inv_stock` s
        INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1
        INNER JOIN `epc_erp_inv_warehouses` w ON w.`id` = s.`warehouse_id`
        WHERE i.`reorder_level` > 0 AND s.`qty_on_hand` <= i.`reorder_level`
          AND (@warehouseId = 0 OR s.`warehouse_id` = @warehouseId)
        ORDER BY (s.`qty_on_hand` / NULLIF(i.`reorder_level`, 0)) ASC, i.`sku` ASC
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_erp_inventory_ledger</c> movement rows (running balance computed in reporter).</summary>
    public const string SelectErpInventoryMovements = """
        SELECT m.`id`, IFNULL(m.`movement_type`, '') AS movement_type,
               IFNULL(m.`warehouse_id`, 0) AS warehouse_id, IFNULL(m.`item_id`, 0) AS item_id,
               IFNULL(m.`qty`, 0) AS qty, IFNULL(m.`unit_cost`, 0) AS unit_cost,
               IFNULL(m.`total_cost`, 0) AS total_cost, IFNULL(m.`batch_no`, '') AS batch_no,
               IFNULL(m.`reference`, '') AS reference, IFNULL(m.`movement_date`, 0) AS movement_date,
               IFNULL(i.`sku`, '') AS sku, IFNULL(i.`name`, '') AS item_name,
               IFNULL(w.`name`, '') AS warehouse_name
        FROM `epc_erp_inv_movements` m
        LEFT JOIN `epc_erp_inv_items` i ON i.`id` = m.`item_id`
        LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = m.`warehouse_id`
        WHERE m.`active` = 1
          AND (@itemId = 0 OR m.`item_id` = @itemId)
          AND (@warehouseId = 0 OR m.`warehouse_id` = @warehouseId)
        ORDER BY m.`movement_date` ASC, m.`id` ASC
        LIMIT @limit
        """;

    public const string CountErpInventoryMovements = """
        SELECT COUNT(*) FROM `epc_erp_inv_movements` WHERE `active` = 1
        """;

    /// <summary>Report-center / aging: AR outstanding from einvoice documents (bucketed in reporter).</summary>
    public const string SelectErpAgingArDocuments = """
        SELECT d.`user_id`, IFNULL(u.`email`, '') AS email,
               IFNULL(d.`issue_date`, 0) AS issue_date,
               IFNULL(d.`payment_due_date`, 0) AS payment_due_date,
               IFNULL(d.`total_incl_vat`, 0) AS total_incl_vat,
               IFNULL(d.`paid_amount`, 0) AS paid_amount
        FROM `epc_einvoice_documents` d
        LEFT JOIN `users` u ON u.`user_id` = d.`user_id`
        WHERE d.`active` = 1 AND d.`status` <> 'cancelled'
          AND d.`doc_category` IN ('tax_invoice','commercial_invoice')
        LIMIT 2000
        """;

    /// <summary>Report-center / aging: AP outstanding from purchases.</summary>
    public const string SelectErpAgingApDocuments = """
        SELECT p.`id`, p.`supplier_id`, IFNULL(s.`name`, '') AS name,
               IFNULL(p.`purchase_date`, 0) AS purchase_date,
               IFNULL(p.`total_amount`, 0) AS total_amount,
               IFNULL((SELECT SUM(a.`amount`) FROM `epc_erp_supplier_accounting` a
                       WHERE a.`purchase_id` = p.`id` AND a.`active` = 1 AND a.`is_credit` = 0), 0) AS paid
        FROM `epc_erp_purchases` p
        LEFT JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id`
        WHERE p.`active` = 1 AND p.`status` <> 'draft'
        LIMIT 2000
        """;

    /// <summary>Inventory aging value by item (age from last inbound movement).</summary>
    public const string SelectErpAgingInventoryRows = """
        SELECT st.`item_id`, IFNULL(it.`sku`, '') AS sku, IFNULL(it.`name`, '') AS name,
               IFNULL(st.`qty_on_hand`, 0) AS qty_on_hand,
               IFNULL(st.`avg_unit_cost`, 0) AS avg_unit_cost,
               IFNULL(st.`time_updated`, 0) AS time_updated,
               IFNULL((SELECT MAX(m.`movement_date`) FROM `epc_erp_inv_movements` m
                       WHERE m.`item_id` = st.`item_id` AND m.`active` = 1
                         AND m.`movement_type` IN ('opening','purchase_in','transfer_in','return_in')), 0) AS last_in
        FROM `epc_erp_inv_stock` st
        INNER JOIN `epc_erp_inv_items` it ON it.`id` = st.`item_id` AND it.`active` = 1
        WHERE st.`qty_on_hand` > 0
        LIMIT 2000
        """;

    /// <summary>PHP <c>epc_erp_receivables</c> — customer AR (email is the PHP AR identifier).</summary>
    public const string SelectErpReceivables = """
        SELECT
            `users`.`user_id`,
            IFNULL(`users`.`email`, '') AS email,
            IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = `users`.`user_id` AND `active` = 1 AND `income` = 1), 0)
            - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = `users`.`user_id` AND `active` = 1 AND `income` = 0), 0) AS balance,
            IFNULL((
                SELECT SUM(GREATEST(
                    IFNULL((SELECT SUM(i.`price` * i.`count_need`) FROM `shop_orders_items` i WHERE i.`order_id` = o.`id`), 0)
                    - (
                        IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = o.`id`), 0)
                        - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = o.`id`), 0)
                    )
                , 0))
                FROM `shop_orders` o
                WHERE o.`user_id` = `users`.`user_id` AND o.`successfully_created` = 1
            ), 0) AS order_receivable_due,
            (SELECT COUNT(*) FROM `shop_orders` WHERE `user_id` = `users`.`user_id` AND `successfully_created` = 1) AS order_count,
            IFNULL((
                SELECT COUNT(*) FROM `shop_orders` o
                WHERE o.`user_id` = `users`.`user_id` AND o.`successfully_created` = 1
                  AND o.`status` IN (SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1)
            ), 0) AS complete_order_count
        FROM `users`
        HAVING balance != 0 OR order_count > 0 OR order_receivable_due != 0
        ORDER BY order_receivable_due DESC, balance DESC
        LIMIT @limit
        """;

    public const string SelectErpCreditProfiles = """
        SELECT `customer_id`, IFNULL(`customer_account`, '') AS customer_account,
               IFNULL(`customer_name`, '') AS customer_name,
               IFNULL(`customer_group`, '') AS customer_group,
               IFNULL(`currency_code`, '') AS currency_code,
               IFNULL(`credit_limit`, 0) AS credit_limit,
               IFNULL(`terms_days`, 0) AS terms_days,
               IFNULL(`risk_band`, '') AS risk_band,
               IFNULL(`on_hold`, 0) AS on_hold
        FROM `epc_credit_profiles`
        ORDER BY `customer_id` DESC
        LIMIT @limit
        """;

    public const string SelectErpCreditHolds = """
        SELECT `customer_id`, IFNULL(`customer_name`, '') AS customer_name,
               IFNULL(`credit_limit`, 0) AS credit_limit,
               IFNULL(`terms_days`, 0) AS terms_days,
               IFNULL(`risk_band`, '') AS risk_band
        FROM `epc_credit_profiles`
        WHERE `on_hold` = 1
        ORDER BY `customer_id` DESC
        LIMIT @limit
        """;

    public const string SelectErpReportCenterWorkingCapital = """
        SELECT
          (SELECT COALESCE(SUM(`total_amount`), 0) FROM `epc_erp_sales_orders`
           WHERE `status` IN ('confirmed','partial') AND `active` = 1) AS ar,
          (SELECT COALESCE(SUM(`total_amount`), 0) FROM `epc_erp_purchases`
           WHERE `status` IN ('confirmed','partial') AND `active` = 1) AS ap,
          (SELECT COALESCE(SUM(`qty_on_hand` * `avg_unit_cost`), 0) FROM `epc_erp_inv_stock`) AS inventory,
          (
            (SELECT COALESCE(SUM(`opening_balance`), 0) FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1)
            + (SELECT COALESCE(SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE -`amount` END), 0)
               FROM `epc_erp_cash_bank_entries` WHERE `active` = 1)
          ) AS cash
        """;

    public const string SelectErpReportCenterArAgingExec = """
        SELECT `total_amount`, `order_date`
        FROM `epc_erp_sales_orders`
        WHERE `status` IN ('confirmed','partial') AND `active` = 1
        """;

    public const string SelectErpReportCenterCashHistory = """
        SELECT
          COALESCE(SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE 0 END), 0) AS inflow,
          COALESCE(SUM(CASE WHEN `direction` = 0 THEN `amount` ELSE 0 END), 0) AS outflow
        FROM `epc_erp_cash_bank_entries`
        WHERE `active` = 1 AND `time` BETWEEN @from AND @to
        """;

    public const string SelectErpTrialBalanceRows = """
        SELECT IFNULL(a.`code`, '') AS code, IFNULL(a.`name`, '') AS name,
               IFNULL(a.`account_type`, '') AS account_type, IFNULL(a.`normal_side`, '') AS normal_side,
               CASE
                 WHEN IFNULL(a.`normal_side`, 'debit') = 'credit'
                 THEN IFNULL(a.`opening_balance`, 0) + IFNULL(x.credits, 0) - IFNULL(x.debits, 0)
                 ELSE IFNULL(a.`opening_balance`, 0) + IFNULL(x.debits, 0) - IFNULL(x.credits, 0)
               END AS balance
        FROM `epc_erp_coa_accounts` a
        LEFT JOIN (
            SELECT l.`coa_id`,
                   IFNULL(SUM(l.`debit`), 0) AS debits,
                   IFNULL(SUM(l.`credit`), 0) AS credits
            FROM `epc_erp_gl_lines` l
            INNER JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id` AND j.`active` = 1
            GROUP BY l.`coa_id`
        ) x ON x.`coa_id` = a.`id`
        WHERE a.`active` = 1
        ORDER BY a.`code` ASC
        LIMIT @limit
        """;

    public const string SelectCpCurrencies = """
        SELECT `id`, IFNULL(`iso_code`, '') AS iso_code, IFNULL(`iso_name`, '') AS iso_name,
               IFNULL(`caption_short`, '') AS caption_short, IFNULL(`rate`, 0) AS rate,
               IFNULL(`available`, 0) AS available, IFNULL(`order`, 0) AS sort_order
        FROM `shop_currencies`
        ORDER BY `order` ASC, `iso_name` ASC
        LIMIT @limit
        """;

    /// <summary>API client metadata only — never selects client_key_hash.</summary>
    public const string SelectCpApiClientsMeta = """
        SELECT `id`, IFNULL(`client_key_prefix`, '') AS client_key_prefix,
               IFNULL(`product`, '') AS product, IFNULL(`label`, '') AS label,
               IFNULL(`contact_email`, '') AS contact_email, `active`,
               IFNULL(`daily_limit`, 0) AS daily_limit, IFNULL(`calls_today`, 0) AS calls_today,
               IFNULL(`time_created`, 0) AS time_created
        FROM `epc_api_clients`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Power BI workspace/report metadata — no Azure client secrets.</summary>
    public const string SelectCpPowerBiConfig = """
        SELECT IFNULL(`site_key`, '') AS site_key,
               IFNULL(`workspace_id`, '') AS workspace_id,
               IFNULL(`azure_tenant_id`, '') AS azure_tenant_id,
               IFNULL(`default_report_id`, '') AS default_report_id,
               IFNULL(`default_dataset_id`, '') AS default_dataset_id,
               IFNULL(`embed_url`, '') AS embed_url,
               IFNULL(`embed_mode`, '') AS embed_mode,
               IFNULL(`notes`, '') AS notes,
               `active`
        FROM `epc_power_bi_config`
        ORDER BY CASE WHEN `site_key` = '__platform__' THEN 0 ELSE 1 END, `id` ASC
        LIMIT 1
        """;

    /// <summary>Power BI registered reports metadata (read-only).</summary>
    public const string SelectCpPowerBiReports = """
        SELECT `id`, IFNULL(`site_key`, '') AS site_key,
               IFNULL(`report_id`, '') AS report_id,
               IFNULL(`report_name`, '') AS report_name,
               IFNULL(`dataset_id`, '') AS dataset_id,
               IFNULL(`category`, '') AS category,
               IFNULL(`embed_url`, '') AS embed_url,
               `active`
        FROM `epc_power_bi_reports`
        ORDER BY `category` ASC, `report_name` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>Metabase URL/active only — never selects secret_key.</summary>
    public const string SelectCpMetabaseConfig = """
        SELECT IFNULL(`site_key`, '') AS site_key,
               IFNULL(`metabase_url`, '') AS metabase_url,
               `active`
        FROM `epc_metabase_config`
        ORDER BY CASE WHEN `site_key` = '__platform__' THEN 0 ELSE 1 END, `id` ASC
        LIMIT 1
        """;

    public const string SelectCpMetabaseDashboards = """
        SELECT `id`, IFNULL(`site_key`, '') AS site_key,
               IFNULL(`dashboard_id`, 0) AS dashboard_id,
               IFNULL(`dashboard_name`, '') AS dashboard_name,
               IFNULL(`category`, '') AS category,
               `active`
        FROM `epc_metabase_dashboards`
        ORDER BY `category` ASC, `dashboard_name` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>NL report definitions metadata — omits query_template / recipients JSON.</summary>
    public const string SelectCpNlReportDefinitions = """
        SELECT `id`, IFNULL(`site_key`, '') AS site_key,
               IFNULL(`name`, '') AS name,
               IFNULL(`description`, '') AS description,
               IFNULL(`report_type`, '') AS report_type,
               IFNULL(`schedule`, '') AS schedule,
               IFNULL(`format`, '') AS format,
               `active`,
               IFNULL(`created_by`, 0) AS created_by
        FROM `epc_report_definitions`
        ORDER BY `name` ASC, `id` ASC
        LIMIT @limit
        """;

    public const string SelectCpMarketingBroadcastStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_marketing_broadcast_campaigns`) AS campaigns,
            (SELECT IFNULL(SUM(`sent_ok`),0) FROM `epc_marketing_broadcast_campaigns` WHERE `channel` = 'email') AS emails_sent,
            (SELECT IFNULL(SUM(`sent_ok`),0) FROM `epc_marketing_broadcast_campaigns` WHERE `channel` = 'whatsapp') AS whatsapp_sent
        """;

    /// <summary>Campaign metadata — omits body_html / body_text.</summary>
    public const string SelectCpMarketingBroadcastCampaigns = """
        SELECT `id`, IFNULL(`created_at`, 0) AS created_at,
               IFNULL(`channel`, '') AS channel,
               IFNULL(`template_key`, '') AS template_key,
               IFNULL(`subject`, '') AS subject,
               IFNULL(`preview`, '') AS preview,
               IFNULL(`audience_mode`, '') AS audience_mode,
               IFNULL(`audience_meta`, '') AS audience_meta,
               IFNULL(`total_targets`, 0) AS total_targets,
               IFNULL(`sent_ok`, 0) AS sent_ok,
               IFNULL(`sent_fail`, 0) AS sent_fail,
               IFNULL(`status`, '') AS status,
               IFNULL(`operator_id`, 0) AS operator_id
        FROM `epc_marketing_broadcast_campaigns`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Demo tenant registry — no passwords / temp credentials.</summary>
    public const string SelectCpDemoTenants = """
        SELECT IFNULL(`site_key`, '') AS site_key,
               IFNULL(`hostname`, '') AS hostname,
               IFNULL(`industry_code`, '') AS industry_code,
               IFNULL(`status`, '') AS status,
               IFNULL(`trade_name`, '') AS trade_name,
               IFNULL(`hub_name`, '') AS hub_name,
               IFNULL(`hosted_on`, '') AS hosted_on,
               IFNULL(`erp_only_shared`, 0) AS erp_only_shared,
               IFNULL(`is_active`, 0) AS is_active,
               IFNULL(`demo_expires_at`, 0) AS demo_expires_at,
               IFNULL(`demo_contact_email`, '') AS demo_contact_email
        FROM `epc_portal_tenants`
        WHERE `is_demo` = 1 AND IFNULL(`site_key`, '') != ''
        ORDER BY `demo_expires_at` ASC, `trade_name` ASC
        LIMIT @limit
        """;

    /// <summary>Mobile apps config blob from portal site settings (JSON; secrets stripped in reporter).</summary>
    public const string SelectCpMobileAppsIntegrationsJson = """
        SELECT IFNULL(`integrations_json`, '') AS integrations_json
        FROM `epc_portal_site_settings`
        ORDER BY `id` ASC
        LIMIT 1
        """;

    /// <summary>Parts agent config — omits system_prompt / greeting.</summary>
    public const string SelectCpPartsAgentConfig = """
        SELECT IFNULL(`enabled`, 0) AS enabled,
               IFNULL(`agent_name`, '') AS agent_name,
               IFNULL(`domain`, '') AS domain
        FROM `epc_parts_agent_config`
        ORDER BY `id` ASC
        LIMIT 1
        """;

    public const string SelectCpPartsAgentStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_parts_agent_session`) AS total_sessions,
            (SELECT COUNT(*) FROM `epc_parts_agent_session` WHERE `updated_at` >= UNIX_TIMESTAMP(CURDATE())) AS sessions_today,
            (SELECT COUNT(*) FROM `epc_parts_agent_message` WHERE `created_at` >= UNIX_TIMESTAMP(CURDATE())) AS messages_today,
            (SELECT COUNT(*) FROM `epc_parts_agent_session` WHERE `user_id` > 0) AS logged_in_sessions
        """;

    /// <summary>Parts agent sessions — omits client_ip / user_agent; truncates last texts.</summary>
    public const string SelectCpPartsAgentSessions = """
        SELECT IFNULL(`session_id`, '') AS session_id,
               IFNULL(`updated_at`, 0) AS updated_at,
               IFNULL(`message_count`, 0) AS message_count,
               IFNULL(`country_code`, '') AS country_code,
               IFNULL(`country_name`, '') AS country_name,
               IFNULL(`user_id`, 0) AS user_id,
               IFNULL(`ip_hash`, '') AS ip_hash,
               LEFT(IFNULL(`last_user_text`, ''), 240) AS last_user_text,
               LEFT(IFNULL(`last_agent_text`, ''), 240) AS last_agent_text
        FROM `epc_parts_agent_session`
        ORDER BY `updated_at` DESC
        LIMIT @limit
        """;

    public const string SelectCpPosSettings = """
        SELECT IFNULL(`pos_enabled`, 0) AS pos_enabled,
               IFNULL(`register_name`, '') AS register_name
        FROM `epc_pos_settings`
        ORDER BY `id` ASC
        LIMIT 1
        """;

    public const string SelectCpPosStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_pos_sessions` WHERE `status` = 'open') AS open_sessions,
            (SELECT COUNT(*) FROM `epc_pos_sales` WHERE `status` = 'completed' AND `time_created` >= UNIX_TIMESTAMP(CURDATE())) AS sales_today,
            (SELECT IFNULL(SUM(`total_amount`), 0) FROM `epc_pos_sales` WHERE `status` = 'completed' AND `time_created` >= UNIX_TIMESTAMP(CURDATE())) AS sales_total_today
        """;

    public const string SelectCpPosSales = """
        SELECT `id`, IFNULL(`sale_no`, '') AS sale_no,
               IFNULL(`session_id`, 0) AS session_id,
               IFNULL(`customer_label`, '') AS customer_label,
               IFNULL(`subtotal_ex`, 0) AS subtotal_ex,
               IFNULL(`vat_amount`, 0) AS vat_amount,
               IFNULL(`total_amount`, 0) AS total_amount,
               IFNULL(`payment_method`, '') AS payment_method,
               IFNULL(`tax_kit_code`, '') AS tax_kit_code,
               IFNULL(`status`, '') AS status,
               IFNULL(`time_created`, 0) AS time_created
        FROM `epc_pos_sales`
        ORDER BY `time_created` DESC, `id` DESC
        LIMIT @limit
        """;

    /// <summary>Tax toolkit catalog — omits rules_json.</summary>
    public const string SelectCpTaxToolkits = """
        SELECT `id`, IFNULL(`kit_code`, '') AS kit_code,
               IFNULL(`name`, '') AS name,
               IFNULL(`jurisdiction`, '') AS jurisdiction,
               IFNULL(`tax_type`, '') AS tax_type,
               IFNULL(`is_system`, 0) AS is_system,
               IFNULL(`active`, 0) AS active
        FROM `epc_tax_toolkits`
        WHERE IFNULL(`active`, 0) = 1
        ORDER BY `kit_code` ASC, `id` ASC
        LIMIT @limit
        """;

    public const string SelectCpTaxToolkitStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_tax_toolkits` WHERE IFNULL(`active`, 0) = 1) AS toolkit_count,
            (SELECT COUNT(*) FROM `epc_tax_toolkit_installs`) AS install_count
        """;

    /// <summary>Tenant tax profile — omits reg_number.</summary>
    public const string SelectCpTaxTenantProfile = """
        SELECT IFNULL(`country_code`, '') AS country_code,
               IFNULL(`kit_code`, '') AS kit_code
        FROM `epc_tax_toolkit_tenant_profile`
        ORDER BY `id` ASC
        LIMIT 1
        """;

    /// <summary>SMS operators — omits parameters / parameters_values.</summary>
    public const string SelectCpSmsOperators = """
        SELECT `id`, IFNULL(`name`, '') AS name,
               IFNULL(`handler`, '') AS handler,
               IFNULL(`description`, '') AS description,
               IFNULL(`active`, 0) AS active,
               IFNULL(`control_available`, 0) AS control_available
        FROM `sms_api`
        WHERE IFNULL(`control_available`, 0) = 1
        ORDER BY CASE WHEN `handler` LIKE 'epc_%' THEN 0 ELSE 1 END, `id` ASC
        LIMIT @limit
        """;

    public const string SelectCpWhatsappLogStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_whatsapp_notify_log` WHERE IFNULL(`status`, 0) = 1) AS whatsapp_sent,
            (SELECT COUNT(*) FROM `epc_whatsapp_notify_log` WHERE IFNULL(`status`, 0) != 1) AS whatsapp_failed
        """;

    /// <summary>WhatsApp notify log — masks phone; omits raw response JSON.</summary>
    public const string SelectCpWhatsappNotifyLog = """
        SELECT `id`, IFNULL(`created_at`, 0) AS created_at,
               IFNULL(`notify_name`, '') AS notify_name,
               CASE
                   WHEN CHAR_LENGTH(IFNULL(`phone`, '')) <= 4 THEN '****'
                   ELSE CONCAT(REPEAT('*', GREATEST(CHAR_LENGTH(`phone`) - 4, 0)), RIGHT(`phone`, 4))
               END AS phone_masked,
               IFNULL(`status`, 0) AS status,
               LEFT(IFNULL(`message_preview`, ''), 240) AS message_preview
        FROM `epc_whatsapp_notify_log`
        ORDER BY `id` DESC
        LIMIT @limit
        """;


    public const string SelectCpCrmStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_crm_leads` WHERE IFNULL(`active`,0)=1) AS leads,
            (SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE IFNULL(`active`,0)=1) AS opportunities,
            (SELECT COUNT(*) FROM `epc_crm_activities` WHERE IFNULL(`active`,0)=1) AS activities,
            (SELECT COUNT(*) FROM `epc_crm_tickets` WHERE IFNULL(`active`,0)=1 AND `status` IN ('open','pending')) AS tickets_open
        """;

    /// <summary>CRM leads — omits email/phone/notes.</summary>
    public const string SelectCpCrmLeads = """
        SELECT `id`,
               IFNULL(`company`, '') AS title,
               IFNULL(`status`, '') AS status,
               IFNULL(`source`, '') AS source,
               IFNULL(`owner_user_id`, 0) AS owner_id,
               IFNULL(`expected_value`, 0) AS amount,
               IFNULL(`time_updated`, 0) AS updated_at
        FROM `epc_crm_leads`
        WHERE IFNULL(`active`, 0) = 1
        ORDER BY `time_updated` DESC, `id` DESC
        LIMIT @limit
        """;

    public const string SelectCpDocumentCompanyName = """
        SELECT IFNULL(`trade_name`, IFNULL(`legal_name`, '')) AS company_name
        FROM `epc_document_company`
        WHERE `id` = 1
        LIMIT 1
        """;

    public const string SelectCpDocumentStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_document_templates`) AS template_count,
            (SELECT COUNT(*) FROM `epc_document_attachments`) AS attachment_count
        """;

    /// <summary>Document templates — omits HTML bodies / CSS. Id assigned in reporter.</summary>
    public const string SelectCpDocumentTemplates = """
        SELECT IFNULL(`code`, '') AS code,
               IFNULL(`title`, '') AS title,
               IFNULL(`category`, '') AS category,
               IFNULL(`active`, 0) AS active,
               IFNULL(`sort_order`, 0) AS sort_order
        FROM `epc_document_templates`
        ORDER BY `sort_order` ASC, `code` ASC
        LIMIT @limit
        """;

    public const string SelectCpDeliveryStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_obtaining_modes` WHERE IFNULL(`control_available`,0)=1) AS methods,
            (SELECT COUNT(*) FROM `shop_obtaining_modes` WHERE IFNULL(`control_available`,0)=1 AND IFNULL(`available`,0)=1) AS available
        """;

    /// <summary>Delivery modes — omits parameters_values.</summary>
    public const string SelectCpDeliveryModes = """
        SELECT `id`, IFNULL(`caption`, '') AS caption,
               IFNULL(`handler`, '') AS handler,
               IFNULL(`available`, 0) AS available,
               IFNULL(`control_available`, 0) AS control_available,
               IFNULL(`order`, 0) AS sort_order
        FROM `shop_obtaining_modes`
        WHERE IFNULL(`control_available`, 0) = 1
        ORDER BY `order` ASC, `id` ASC
        LIMIT @limit
        """;

    public const string SelectCpCrossesStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list`) AS total_pairs,
            (SELECT COUNT(DISTINCT `manufacturer_article`) FROM `shop_docpart_articles_analogs_list`) AS brands
        """;

    public const string SelectCpCrossPairs = """
        SELECT `id`,
               IFNULL(`manufacturer_article`, '') AS manufacturer,
               IFNULL(`article`, '') AS article,
               IFNULL(`manufacturer_analog`, '') AS cross_manufacturer,
               IFNULL(`analog`, '') AS cross_article
        FROM `shop_docpart_articles_analogs_list`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>HR overview KPIs — omits salary/allowances/currency/payslip detail.</summary>
    public const string SelectCpHrStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_hr_employees` WHERE IFNULL(`status`,'')='active') AS active_employees,
            (SELECT COUNT(*) FROM `epc_hr_leave` WHERE IFNULL(`status`,'')='pending') AS pending_leave,
            (SELECT COUNT(*) FROM `epc_hr_payroll_runs`) AS payroll_runs,
            (SELECT COUNT(*) FROM `epc_hr_attendance`) AS attendance_rows
        """;

    /// <summary>HR employees — omits salary/allowances/currency/payslip detail.</summary>
    public const string SelectCpHrEmployees = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`department`,'') AS department, IFNULL(`status`,'') AS status,
               IFNULL(`join_date`,0) AS join_date
        FROM `epc_hr_employees`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Production overview KPIs — omits cost columns.</summary>
    public const string SelectCpProductionStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_mfg_bom` WHERE IFNULL(`active`,0)=1) AS bom_count,
            (SELECT COUNT(*) FROM `epc_mfg_work_orders` WHERE `status` IN ('planned','in_progress')) AS open_work_orders,
            (SELECT COUNT(*) FROM `epc_mfg_work_orders` WHERE `status`='completed') AS completed_work_orders
        """;

    /// <summary>Production work orders — omits cost columns.</summary>
    public const string SelectCpProductionWorkOrders = """
        SELECT `id`, IFNULL(`wo_no`,'') AS wo_no, IFNULL(`status`,'') AS status,
               IFNULL(`qty_planned`,0) AS qty_planned, IFNULL(`qty_produced`,0) AS qty_produced,
               IFNULL(`time_updated`,0) AS updated_at
        FROM `epc_mfg_work_orders`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Projects overview KPIs — omits timesheet rates; includes contract count.</summary>
    public const string SelectCpProjectsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_prj_projects` WHERE IFNULL(`status`,'')='open') AS open_projects,
            (SELECT COUNT(*) FROM `epc_prj_tasks`) AS task_count,
            (SELECT COUNT(*) FROM `epc_con_contracts`) AS contract_count
        """;

    /// <summary>Projects list — omits timesheet rates.</summary>
    public const string SelectCpProjects = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`status`,'') AS status, IFNULL(`billing_type`,'') AS billing_type,
               IFNULL(`contract_value`,0) AS contract_value
        FROM `epc_prj_projects`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Industry pack KPIs — omits JSON blobs.</summary>
    public const string SelectCpIndustryPackStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_industry_packs`) AS pack_count,
            (SELECT COUNT(*) FROM `epc_industry_packs` WHERE IFNULL(`active`,0)=1) AS active_packs,
            (SELECT COUNT(*) FROM `epc_tenant_pack_assignments`) AS assignments
        """;

    /// <summary>Industry packs — omits modules/gl_template/tax_rules/theme/product_attrs JSON.</summary>
    public const string SelectCpIndustryPacks = """
        SELECT `id`, IFNULL(`pack_key`,'') AS pack_key, IFNULL(`name`,'') AS name,
               IFNULL(`description`,'') AS description, IFNULL(`icon`,'') AS icon,
               IFNULL(`active`,0) AS active
        FROM `epc_industry_packs`
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    /// <summary>ERP multi-company legal entities (PHP <c>epc_erp_companies_list</c>).</summary>
    public const string SelectErpCompanies = """
        SELECT `id`,
               IFNULL(`code`,'') AS code,
               IFNULL(`name`,'') AS name,
               IFNULL(`currency_code`,'') AS currency_code,
               IFNULL(`country_code`,'') AS country_code,
               IFNULL(`active`,1) AS active
        FROM `epc_erp_pm_legal_entities`
        WHERE IFNULL(`active`,1) = 1
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    /// <summary>Per-company industry_pack overrides (PHP <c>epc_org_company_settings</c>).</summary>
    public const string SelectErpCompanyIndustryPacks = """
        SELECT `company_id`, IFNULL(`setting_value`,'') AS industry_pack
        FROM `epc_org_company_settings`
        WHERE `setting_key` = 'industry_pack'
        """;

    /// <summary>Jewellery retail KPIs — omits mobile/email/tel/passport/remarks/narration/customer PII/cost.</summary>
    public const string SelectCpJewelleryStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_jewel_voucher`) AS voucher_count,
            (SELECT COUNT(*) FROM `epc_jewel_voucher` WHERE IFNULL(`status`,'') IN ('draft','posted','authorized')) AS open_vouchers,
            (SELECT COUNT(*) FROM `epc_jw_tags`) AS tag_count,
            (SELECT COUNT(*) FROM `epc_jewel_metal_stock`) AS metal_stock_rows
        """;

    /// <summary>Jewellery vouchers — omits mobile/email/tel/passport/remarks/narration/customer PII/cost.</summary>
    public const string SelectCpJewelleryVouchers = """
        SELECT `id`, IFNULL(`voc_type`,'') AS voc_type, IFNULL(`voc_date`,'') AS voc_date,
               IFNULL(`voc_no`,0) AS voc_no, IFNULL(`party_name`,'') AS party_name,
               IFNULL(`status`,'') AS status,
               IFNULL(`net_amount`,0) AS net_amount, IFNULL(`vat_amount`,0) AS vat_amount,
               IFNULL(`total_with_vat`,0) AS total_with_vat
        FROM `epc_jewel_voucher`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Price list KPIs — omits stats_json/error_text/stored_relpath.</summary>
    public const string SelectCpPriceListStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_pl_lists` WHERE IFNULL(`active`,0)=1) AS active_lists,
            (SELECT COUNT(*) FROM `epc_pl_prices`) AS price_rows,
            (SELECT COUNT(*) FROM `epc_price_upload_history`) AS upload_count
        """;

    /// <summary>Price lists — omits stats_json/error_text/stored_relpath.</summary>
    public const string SelectCpPriceLists = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`currency`,'') AS currency, IFNULL(`customer_id`,0) AS customer_id,
               IFNULL(`priority`,0) AS priority, IFNULL(`active`,0) AS active
        FROM `epc_pl_lists`
        ORDER BY `priority` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>Auto-price KPIs — omits config_json/notes/meta.</summary>
    public const string SelectCpAutoPriceStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_auto_price_rules` WHERE IFNULL(`active`,0)=1) AS active_rules,
            (SELECT COUNT(*) FROM `epc_price_sources` WHERE IFNULL(`active`,0)=1) AS active_sources,
            (SELECT COUNT(*) FROM `epc_price_compare_runs`) AS compare_runs
        """;

    /// <summary>Auto-price rules — omits config_json/notes/meta.</summary>
    public const string SelectCpAutoPriceRules = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`rule_key`,'') AS rule_key,
               IFNULL(`min_margin_percent`,0) AS min_margin_percent,
               IFNULL(`auto_update_prices`,0) AS auto_update_prices,
               IFNULL(`schedule_hours`,0) AS schedule_hours,
               IFNULL(`active`,0) AS active, IFNULL(`updated_at`,0) AS updated_at
        FROM `epc_auto_price_rules`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>UAE tax KPIs — omits erp_summary/compliance_actions_json/pdf_url/passport.</summary>
    public const string SelectCpUaeTaxStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_uae_tax_legislation_items`) AS legislation_count,
            (SELECT COUNT(*) FROM `epc_uae_vat_advance`) AS vat_advance_rows,
            (SELECT COUNT(*) FROM `epc_bos_vat_refunds`) AS vat_refund_rows
        """;

    /// <summary>UAE tax legislation items — omits erp_summary/compliance_actions_json/pdf_url/passport.</summary>
    public const string SelectCpUaeTaxItems = """
        SELECT `id`, IFNULL(`slug`,'') AS slug, IFNULL(`title`,'') AS title,
               IFNULL(`issue_date`,'') AS issue_date, IFNULL(`category`,'') AS category,
               IFNULL(`tax_category`,'') AS tax_category,
               IFNULL(`is_new`,0) AS is_new, IFNULL(`is_updated`,0) AS is_updated,
               IFNULL(`time_synced`,0) AS time_synced
        FROM `epc_uae_tax_legislation_items`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Budget KPIs — omits note text.</summary>
    public const string SelectCpBudgetStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_erp_pm_budgets`) AS budget_count,
            (SELECT COUNT(*) FROM `epc_erp_pm_budgets` WHERE IFNULL(`active`,0)=1) AS active_budgets,
            (SELECT COUNT(*) FROM `epc_erp_pm_budget_lines`) AS budget_line_count,
            (SELECT COUNT(*) FROM `epc_erp_pm_dimensions`) AS dimension_count
        """;

    /// <summary>Budgets — omits note.</summary>
    public const string SelectCpBudgets = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`fiscal_year`,'') AS fiscal_year,
               IFNULL(`business_unit_id`,0) AS business_unit_id,
               IFNULL(`is_master`,0) AS is_master, IFNULL(`active`,0) AS active
        FROM `epc_erp_pm_budgets`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Courier KPIs from logistics hub tables — not ERP TMS. Omits config_json.</summary>
    public const string SelectCpCarrierStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_carrier_accounts`) AS carrier_count,
            (SELECT COUNT(*) FROM `epc_carrier_accounts` WHERE IFNULL(`active`,0)=1) AS active_carriers,
            (SELECT COUNT(*) FROM `epc_carrier_shipments`) AS shipment_count,
            (SELECT COUNT(*) FROM `epc_carrier_shipments` WHERE IFNULL(`status`,'') NOT IN ('delivered','cancelled','canceled','void')) AS open_shipments
        """;

    /// <summary>Courier accounts — omits config_json. Catalog region/blurb enriched in reporter.</summary>
    public const string SelectCpCarriers = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`active`,0) AS active, IFNULL(`demo_mode`,0) AS demo_mode,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_carrier_accounts`
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    /// <summary>Payment gateway KPIs — omits parameters/credentials. anable=Enabled; active=Default.</summary>
    public const string SelectCpPaymentGatewayStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_payment_systems`) AS gateway_count,
            (SELECT COUNT(*) FROM `shop_payment_systems` WHERE IFNULL(`anable`,0)=1) AS enabled_gateways,
            (SELECT COUNT(*) FROM `shop_payment_systems` WHERE IFNULL(`active`,0)=1) AS active_gateways,
            (SELECT COUNT(*) FROM `shop_payment_systems` WHERE IFNULL(`is_selectable`,0)=1) AS selectable_gateways,
            (SELECT COUNT(*) FROM `epc_payment_accounts`) AS account_count
        """;

    /// <summary>Payment gateways — omits parameters/parameters_values/description/credentials.</summary>
    public const string SelectCpPaymentGateways = """
        SELECT `id`, IFNULL(`name`,'') AS name, IFNULL(`handler`,'') AS handler,
               IFNULL(`anable`,0) AS anable, IFNULL(`active`,0) AS active,
               IFNULL(`is_selectable`,0) AS is_selectable
        FROM `shop_payment_systems`
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    /// <summary>Workflow KPIs — omits trigger_config/step JSON.</summary>
    public const string SelectCpWorkflowStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_workflows`) AS workflow_count,
            (SELECT COUNT(*) FROM `epc_workflows` WHERE IFNULL(`active`,0)=1) AS active_workflows,
            (SELECT COUNT(*) FROM `epc_workflow_runs`) AS run_count,
            (SELECT COUNT(*) FROM `epc_workflow_runs` WHERE IFNULL(`status`,'')='failed') AS failed_runs
        """;

    /// <summary>Workflows — omits trigger_config/description JSON blobs.</summary>
    public const string SelectCpWorkflows = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`name`,'') AS name,
               IFNULL(`trigger_type`,'') AS trigger_type, IFNULL(`active`,0) AS active,
               IFNULL(`version`,0) AS version, IFNULL(`run_count`,0) AS run_count,
               IFNULL(`last_run_status`,'') AS last_run_status
        FROM `epc_workflows`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Purchase requisition KPIs — omits justification/decision notes.</summary>
    public const string SelectCpPurchaseRequestStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_proc_req`) AS req_count,
            (SELECT COUNT(*) FROM `epc_proc_req` WHERE IFNULL(`status`,'')='draft') AS draft_count,
            (SELECT COUNT(*) FROM `epc_proc_req` WHERE IFNULL(`requires_approval`,0)=1 AND IFNULL(`status`,'') NOT IN ('approved','rejected','converted')) AS pending_approval,
            (SELECT COUNT(*) FROM `epc_proc_req_line`) AS line_count,
            (SELECT COUNT(*) FROM `epc_proc_category` WHERE IFNULL(`active`,0)=1) AS category_count
        """;

    /// <summary>Purchase requisitions — omits justification/decision_note.</summary>
    public const string SelectCpPurchaseRequests = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`req_number`,'') AS req_number,
               IFNULL(`requester`,'') AS requester, IFNULL(`business_unit_id`,0) AS business_unit_id,
               IFNULL(`status`,'') AS status, IFNULL(`total`,0) AS total,
               IFNULL(`requires_approval`,0) AS requires_approval, IFNULL(`po_ref`,'') AS po_ref,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_proc_req`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Promotion KPIs.</summary>
    public const string SelectCpPromotionStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_promo_promotions`) AS promotion_count,
            (SELECT COUNT(*) FROM `epc_promo_promotions` WHERE IFNULL(`active`,0)=1) AS active_promotions,
            (SELECT COUNT(*) FROM `epc_promo_promotions` WHERE IFNULL(`type`,'')='percent') AS percent_promotions,
            (SELECT COUNT(*) FROM `epc_loy_accounts`) AS loyalty_accounts
        """;

    /// <summary>Promotions from epc_promo_promotions.</summary>
    public const string SelectCpPromotions = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`type`,'') AS type, IFNULL(`value`,0) AS value,
               IFNULL(`min_spend`,0) AS min_spend, IFNULL(`valid_from`,0) AS valid_from,
               IFNULL(`valid_to`,0) AS valid_to, IFNULL(`active`,0) AS active
        FROM `epc_promo_promotions`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>CRM opportunity KPIs — omits notes.</summary>
    public const string SelectCpCrmOpportunityStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE IFNULL(`active`,0)=1) AS opportunity_count,
            (SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE IFNULL(`active`,0)=1 AND IFNULL(`stage`,'') NOT IN ('won','lost')) AS open_opportunities,
            (SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE IFNULL(`active`,0)=1 AND IFNULL(`stage`,'')='won') AS won_opportunities,
            (SELECT IFNULL(SUM(`amount`),0) FROM `epc_crm_opportunities` WHERE IFNULL(`active`,0)=1 AND IFNULL(`stage`,'') NOT IN ('won','lost')) AS pipeline_amount
        """;

    /// <summary>CRM opportunities — omits notes.</summary>
    public const string SelectCpCrmOpportunities = """
        SELECT `id`, IFNULL(`title`,'') AS title, IFNULL(`stage`,'') AS stage,
               IFNULL(`amount`,0) AS amount, IFNULL(`probability`,0) AS probability,
               IFNULL(`close_date`,0) AS close_date, IFNULL(`owner_user_id`,0) AS owner_user_id,
               IFNULL(`lead_id`,0) AS lead_id, IFNULL(`active`,0) AS active
        FROM `epc_crm_opportunities`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Optional tenant feature flags overlay for Integrations Hub (secrets/config_json omitted).</summary>
    public const string SelectCpIntegrationFeatureFlags = """
        SELECT IFNULL(`feature_key`,'') AS feature_key, IFNULL(`enabled`,0) AS enabled
        FROM `epc_tenant_feature_flags`
        LIMIT 2000
        """;


    /// <summary>Next-wave: commerce statistics KPIs.</summary>
    public const string SelectCpStatisticsStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_orders`) AS order_count,
            (SELECT COUNT(*) FROM `shop_stat_article_queries`) AS query_count,
            (SELECT COUNT(DISTINCT `article`) FROM `shop_stat_article_queries`) AS unique_articles,
            (SELECT COUNT(DISTINCT FROM_UNIXTIME(IFNULL(`time`,0), '%Y-%m-%d')) FROM `shop_stat_article_queries` WHERE IFNULL(`time`,0) > 0) AS active_days
        """;

    /// <summary>Next-wave: top article query rows (ip omitted).</summary>
    public const string SelectCpStatisticsRows = """
        SELECT IFNULL(`article`,'') AS article,
               IFNULL(`manufacturer`,'') AS brand,
               COUNT(*) AS hits,
               MAX(IFNULL(`time`,0)) AS last_seen
        FROM `shop_stat_article_queries`
        GROUP BY IFNULL(`article`,''), IFNULL(`manufacturer`,'')
        ORDER BY hits DESC, last_seen DESC
        LIMIT @limit
        """;

    public const string SelectCpAccessoriesStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_acc_listings`) AS listing_count,
            (SELECT COUNT(*) FROM `epc_acc_listings` WHERE IFNULL(`status`,'')='published') AS published_count,
            (SELECT COUNT(*) FROM `epc_acc_categories`) AS category_count,
            (SELECT COUNT(*) FROM `epc_acc_photos`) AS photo_count
        """;

    public const string SelectCpAccessoriesRows = """
        SELECT `id`, IFNULL(`title`,'') AS title, IFNULL(`make`,'') AS make, IFNULL(`model`,'') AS model,
               IFNULL(`price`,0) AS price, IFNULL(`status`,'') AS status
        FROM `epc_acc_listings`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectCpSynonymsStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_docpart_manufacturers`) AS manufacturer_count,
            (SELECT COUNT(*) FROM `shop_docpart_manufacturers_synonyms`) AS synonym_count,
            (SELECT COUNT(*) FROM `shop_docpart_manufacturers_synonyms` s
             LEFT JOIN `shop_docpart_manufacturers` m ON m.`id` = s.`manufacturer_id`
             WHERE m.`id` IS NULL) AS orphan_count,
            (SELECT COUNT(DISTINCT `manufacturer_id`) FROM `shop_docpart_manufacturers_synonyms`) AS mapped_count
        """;

    public const string SelectCpSynonymsRows = """
        SELECT IFNULL(m.`name`,'') AS manufacturer,
               IFNULL(s.`synonym`,'') AS synonym,
               IFNULL(s.`manufacturer_id`,0) AS manufacturer_id
        FROM `shop_docpart_manufacturers_synonyms` s
        LEFT JOIN `shop_docpart_manufacturers` m ON m.`id` = s.`manufacturer_id`
        ORDER BY manufacturer ASC, synonym ASC
        LIMIT @limit
        """;

    public const string SelectCpSeoStats = """
        SELECT
            (SELECT COUNT(*) FROM `content` WHERE IFNULL(`is_frontend`,0)=1) AS url_count,
            (SELECT COUNT(*) FROM `content` WHERE IFNULL(`is_frontend`,0)=1 AND IFNULL(`published_flag`,0)=1) AS indexed_ready,
            (SELECT COUNT(*) FROM `content` WHERE IFNULL(`is_frontend`,0)=1 AND IFNULL(`published_flag`,0)=1
                AND IFNULL(`robots_tag`,'') NOT LIKE '%noindex%') AS robots_indexable,
            (SELECT COUNT(*) FROM `content` WHERE IFNULL(`is_frontend`,0)=1 AND IFNULL(`description_tag`,'') != ''
                AND IFNULL(`description_tag`,'') != '0') AS with_description,
            (SELECT IFNULL(`title_tag`,'') FROM `content` WHERE IFNULL(`main_flag`,0)=1 ORDER BY `id` ASC LIMIT 1) AS home_title_tag,
            (SELECT IFNULL(`description_tag`,'') FROM `content` WHERE IFNULL(`main_flag`,0)=1 ORDER BY `id` ASC LIMIT 1) AS home_description_tag,
            0 AS ping_jobs,
            0 AS warm_jobs
        """;

    public const string SelectCpTenantFeaturesStats = """
        SELECT
            (SELECT COUNT(DISTINCT `site_key`) FROM `epc_tenant_feature_flags`) AS site_count,
            (SELECT COUNT(*) FROM `epc_tenant_feature_flags`) AS flag_count,
            (SELECT COUNT(*) FROM `epc_tenant_feature_flags` WHERE IFNULL(`enabled`,0)=1) AS enabled_count,
            (SELECT COUNT(*) FROM `epc_tenant_feature_flags` WHERE IFNULL(`enabled`,0)=0) AS disabled_count
        """;

    public const string SelectCpTenantFeaturesRows = """
        SELECT IFNULL(`site_key`,'') AS site_key, IFNULL(`feature_key`,'') AS feature_key,
               IFNULL(`enabled`,0) AS enabled, IFNULL(`updated_at`,0) AS updated_at
        FROM `epc_tenant_feature_flags`
        ORDER BY `site_key` ASC, `feature_key` ASC
        LIMIT @limit
        """;

    public const string SelectCpSocialHubStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_social_accounts`) AS account_count,
            (SELECT COUNT(*) FROM `epc_social_post_drafts`) AS draft_count,
            (SELECT COUNT(*) FROM `epc_social_post_drafts` WHERE IFNULL(`status`,'')='published') AS published_count,
            (SELECT COUNT(*) FROM `epc_social_post_drafts` WHERE IFNULL(`last_error`,'') != '') AS error_count
        """;

    public const string SelectCpSocialHubRows = """
        SELECT IFNULL(a.`platform`,'') AS platform,
               IFNULL(a.`username`,'') AS username,
               IFNULL(a.`status`,'') AS status,
               IFNULL(d.`title`,'') AS title,
               IFNULL(d.`status`,'') AS draft_status
        FROM `epc_social_accounts` a
        LEFT JOIN `epc_social_post_drafts` d ON d.`site_key` = a.`site_key` AND d.`platform` = a.`platform`
        ORDER BY a.`id` DESC, d.`id` DESC
        LIMIT @limit
        """;

    public const string SelectCpCustomerBoardStats = """
        SELECT
            (SELECT COUNT(*) FROM `users`) AS user_count,
            (SELECT COUNT(*) FROM `users` WHERE IFNULL(`email`,'') != '') AS with_email,
            (SELECT COUNT(*) FROM `users` WHERE IFNULL(`phone`,'') != '') AS with_phone,
            (SELECT COUNT(*) FROM `users` WHERE IFNULL(`time_last_visit`,0) > UNIX_TIMESTAMP() - 86400*30) AS recent_logins
        """;

    public const string SelectCpCustomerBoardRows = """
        SELECT u.`user_id` AS id, IFNULL(u.`email`,'') AS email,
               IFNULL((SELECT `data_value` FROM `users_profiles` p WHERE p.`user_id` = u.`user_id` AND p.`data_key` IN ('name','fio','full_name') LIMIT 1), '') AS name,
               IFNULL(u.`phone`,'') AS phone,
               IFNULL(u.`time_registered`,0) AS reg_time
        FROM `users` u
        ORDER BY u.`user_id` DESC
        LIMIT @limit
        """;
    public const string SelectCpFulfillmentQueueStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_fulfillment_orders` WHERE IFNULL(`status`,'')='queued') AS queued,
            (SELECT COUNT(*) FROM `epc_fulfillment_orders` WHERE IFNULL(`status`,'') IN ('picking','picked','packing','packed')) AS picking,
            (SELECT COUNT(*) FROM `epc_fulfillment_orders` WHERE IFNULL(`status`,'') IN ('shipping','shipped')) AS shipping,
            (SELECT COUNT(*) FROM `epc_fulfillment_orders` WHERE IFNULL(`status`,'')='delivered') AS delivered
        """;

    public const string SelectCpFulfillmentQueueRows = """
        SELECT `id`, IFNULL(`order_number`,'') AS order_number, IFNULL(`customer_name`,'') AS customer_name,
               IFNULL(`status`,'') AS status, IFNULL(`priority`,'') AS priority,
               IFNULL(`warehouse`,'') AS warehouse, IFNULL(`carrier`,'') AS carrier
        FROM `epc_fulfillment_orders`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectCpSsoSamlStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_sso_providers`) AS provider_count,
            (SELECT COUNT(*) FROM `epc_sso_providers` WHERE IFNULL(`active`,0)=1) AS active_providers,
            (SELECT COUNT(*) FROM `epc_sso_sessions`) AS session_count,
            (SELECT COUNT(*) FROM `epc_sso_sessions` WHERE IFNULL(`status`,'')='active') AS active_sessions
        """;

    public const string SelectCpSsoSamlRows = """
        SELECT IFNULL(p.`provider_name`,'') AS provider_name,
               IFNULL(p.`provider_type`,'') AS provider_type,
               IFNULL(p.`active`,0) AS active,
               IFNULL(s.`email`,'') AS email,
               IFNULL(s.`status`,'') AS status
        FROM `epc_sso_providers` p
        LEFT JOIN `epc_sso_sessions` s ON s.`provider_id` = p.`id`
        ORDER BY p.`id` DESC, s.`id` DESC
        LIMIT @limit
        """;

    public const string SelectCpEventBusStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_events`) AS event_count,
            (SELECT COUNT(DISTINCT `event_type`) FROM `epc_events`) AS type_count,
            (SELECT COUNT(DISTINCT `tenant_key`) FROM `epc_events`) AS tenant_count,
            (SELECT COUNT(*) FROM `epc_events` WHERE `created_at` >= (NOW() - INTERVAL 1 DAY)) AS last_24h
        """;

    public const string SelectCpEventBusRows = """
        SELECT `id`, IFNULL(`event_type`,'') AS event_type, IFNULL(`tenant_key`,'') AS tenant_key,
               IFNULL(`actor_type`,'') AS actor_type,
               DATE_FORMAT(`created_at`, '%Y-%m-%d %H:%i:%s') AS created_at
        FROM `epc_events`
        ORDER BY `id` DESC
        LIMIT @limit
        """;


    /// <summary>
    /// PHP <c>docpart_sql_article_normalized_expr()</c> — exactly 15 REPLACE layers + UPPER.
    /// <paramref name="columnSql"/> must be a trusted column identifier (never user input).
    /// </summary>
    public static string DocpartNormalizeArticleExpr(string columnSql)
    {
        // Order matches content/shop/docpart/docpart_article_match.php
        return "UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE("
             + "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE("
             + columnSql
             + ", ' ', ''), '-', ''), '_', ''), '`', ''), '/', ''), '''', ''), '\"', ''), '.', ''), ',', ''), '#', ''), "
             + "CHAR(92), ''), CHAR(13,10), ''), CHAR(13), ''), CHAR(10), ''), CHAR(9), ''))";
    }

    /// <summary>
    /// PHP two-step article match: indexed <c>article_search</c> primary, REPLACE normalize fallback.
    /// When <paramref name="hasArticleSearchColumn"/> is true and <paramref name="useReplaceFallback"/> is false,
    /// returns equality on <c>article_search</c> only (no OR REPLACE — avoids full scans).
    /// </summary>
    public static string StorefrontPriceArticleMatchSql(bool hasArticleSearchColumn, bool useReplaceFallback = false)
    {
        var art = DocpartNormalizeArticleExpr("IFNULL(d.`article`, '')");
        var show = DocpartNormalizeArticleExpr("IFNULL(d.`article_show`, '')");
        if (hasArticleSearchColumn && !useReplaceFallback)
        {
            return "(d.`article_search` = @article)";
        }

        return $"({art} = @article OR {show} = @article)";
    }

    /// <summary>PHP <c>article_search IN (...)</c> for expanded cross-candidate lists.</summary>
    public static string StorefrontPriceArticleSearchInSql(int count)
    {
        if (count <= 0)
        {
            return "0";
        }

        var placeholders = BuildIndexedParams("a", count);
        return $"(d.`article_search` IN ({placeholders}))";
    }

    /// <summary>
    /// Single-article equality (no IN list) — PHP CHPU first-hit path for codes like A2N233V / DA320.
    /// Binds <c>@article</c> only (avoids multi-IN parameter reuse quirks).
    /// </summary>
    public const string StorefrontPriceArticleSimpleEqualitySql = """
        (
          UPPER(TRIM(IFNULL(d.`article`, ''))) = @article
          OR UPPER(TRIM(IFNULL(d.`article_show`, ''))) = @article
          OR UPPER(REPLACE(REPLACE(REPLACE(IFNULL(d.`article`, ''), '-', ''), ' ', ''), '.', '')) = @article
          OR UPPER(REPLACE(REPLACE(REPLACE(IFNULL(d.`article_show`, ''), '-', ''), ' ', ''), '.', '')) = @article
        )
        """;

    /// <summary>
    /// Fast exact/trim match (no nested REPLACE) — enough for already-normalized OE codes like DA320.
    /// Used before the heavy PHP REPLACE fallback.
    /// Each IN clause uses distinct parameter prefixes so drivers that do not reuse names still bind.
    /// </summary>
    public static string StorefrontPriceArticleExactInSql(int count)
    {
        if (count <= 0)
        {
            return "0";
        }

        var a = BuildIndexedParams("a", count);
        var b = BuildIndexedParams("b", count);
        var c = BuildIndexedParams("c", count);
        return $"(UPPER(TRIM(IFNULL(d.`article`, ''))) IN ({a})"
             + $" OR UPPER(TRIM(IFNULL(d.`article_show`, ''))) IN ({b})"
             + $" OR UPPER(REPLACE(REPLACE(REPLACE(IFNULL(d.`article`, ''), '-', ''), ' ', ''), '.', '')) IN ({c}))";
    }

    /// <summary>REPLACE-normalize IN match on article/article_show (PHP CHPU fallback when article_search misses).</summary>
    public static string StorefrontPriceArticleReplaceInSql(int count)
    {
        if (count <= 0)
        {
            return "0";
        }

        var art = DocpartNormalizeArticleExpr("IFNULL(d.`article`, '')");
        var show = DocpartNormalizeArticleExpr("IFNULL(d.`article_show`, '')");
        var a = BuildIndexedParams("a", count);
        var b = BuildIndexedParams("b", count);
        return $"({art} IN ({a}) OR {show} IN ({b}))";
    }

    private static string BuildIndexedParams(string prefix, int count)
    {
        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            parts[i] = "@" + prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return string.Join(",", parts);
    }

    /// <summary>
    /// Cross-table match: prefer indexed <c>article_search</c>/<c>analog_search</c> when probed.
    /// </summary>
    public static string StorefrontCrossArticleMatchSql(bool hasAnalogsSearchColumns = false)
    {
        if (hasAnalogsSearchColumns)
        {
            return "(`article_search` = @article OR `analog_search` = @article)";
        }

        var art = DocpartNormalizeArticleExpr("IFNULL(`article`, '')");
        var analog = DocpartNormalizeArticleExpr("IFNULL(`analog`, '')");
        return $"({art} = @article OR {analog} = @article)";
    }

    /// <summary>
    /// Batch 4 storefront part search (PHP warehouse offers after brand pick).
    /// Placeholder <c>{ARTICLE_MATCH}</c> is replaced at runtime via match helpers.
    /// </summary>
    public const string SelectStorefrontPartSearch = """
        SELECT d.`price_id`, IFNULL(p.`name`, '') AS price_list,
               IFNULL(d.`manufacturer`, '') AS manufacturer,
               IFNULL(d.`article`, '') AS article,
               IFNULL(d.`article_show`, '') AS article_show,
               IFNULL(d.`name`, '') AS name,
               IFNULL(d.`price`, 0) AS price,
               IFNULL(d.`exist`, 0) AS exist,
               IFNULL(d.`storage`, '') AS storage,
               IFNULL(d.`time_to_exe`, '') AS time_to_exe
        FROM `shop_docpart_prices_data` d
        LEFT JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
        WHERE {ARTICLE_MATCH}
          AND (@brand = '' OR UPPER(TRIM(d.`manufacturer`)) = @brand
               OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(d.`manufacturer`)), ' ', ''), '-', ''), '.', '') = @brandCompact)
        ORDER BY (IFNULL(d.`exist`, 0) > 0) DESC, d.`price` ASC
        LIMIT @limit
        """;

    /// <summary>
    /// Article-only warehouse manufacturers with PHP SSR brand-picker aggregates
    /// (<c>epc_chpu_ssr_brand_picker_table_html</c>: name / exist sum / min price / warehouse).
    /// No price&gt;0 / storefront_temp_disabled filters — CHPU brand query is article-table only.
    /// Rows with zero total stock are omitted (PHP SSR skips <c>exist &lt;= 0</c>).
    /// </summary>
    public const string SelectStorefrontArticleWarehouseBrands = """
        SELECT MIN(TRIM(d.`manufacturer`)) AS brand_name,
               MAX(NULLIF(TRIM(IFNULL(d.`name`, '')), '')) AS part_name,
               SUM(IFNULL(d.`exist`, 0)) AS exist_sum,
               MIN(CASE WHEN IFNULL(d.`price`, 0) > 0 THEN d.`price` ELSE NULL END) AS min_price,
               MAX(NULLIF(TRIM(IFNULL(d.`storage`, '')), '')) AS warehouse
        FROM `shop_docpart_prices_data` d
        WHERE {ARTICLE_MATCH}
          AND TRIM(IFNULL(d.`manufacturer`, '')) != ''
        GROUP BY UPPER(TRIM(d.`manufacturer`))
        HAVING SUM(IFNULL(d.`exist`, 0)) > 0
        ORDER BY UPPER(TRIM(d.`manufacturer`)) ASC
        LIMIT @limit
        """;

    /// <summary>Cross-reference partners for normalized article (PHP <c>docpart_load_interchange_partners</c>).</summary>
    public const string SelectStorefrontArticleCrossPairs = """
        SELECT IFNULL(`manufacturer_article`, '') AS source_brand,
               IFNULL(`article`, '') AS source_article,
               IFNULL(`manufacturer_analog`, '') AS cross_brand,
               IFNULL(`analog`, '') AS cross_article
        FROM `shop_docpart_articles_analogs_list`
        WHERE {CROSS_MATCH}
        LIMIT @limit
        """;

    /// <summary>
    /// Batch 4 authenticated customer cart KPI (mirrors PHP ajax_get_cart_info).
    /// Guest carts (<c>session_id</c>) stay PHP-only for this slice.
    /// </summary>
    public const string SelectStorefrontCartSummary = """
        SELECT COUNT(`id`) AS `count`,
               IFNULL(SUM(`price` * `count_need`), 0) AS `sum`
        FROM `shop_carts`
        WHERE `user_id` = @userId
          AND `session_id` = 0
        """;

    /// <summary>
    /// Batch 4 authenticated customer cart lines (read-only subset of cart.php).
    /// Qty/check/delete/add and checkout remain PHP.
    /// </summary>
    public const string SelectStorefrontCartLines = """
        SELECT `id`, IFNULL(`price`, 0) AS price, IFNULL(`count_need`, 0) AS count_need,
               IFNULL(`checked_for_order`, 0) AS checked_for_order, IFNULL(`product_type`, 0) AS product_type,
               IFNULL(`t2_manufacturer`, '') AS manufacturer, IFNULL(`t2_article`, '') AS article,
               IFNULL(`t2_name`, '') AS name, IFNULL(`t2_time_to_exe`, '') AS time_to_exe,
               IFNULL(`t2_time_to_exe_guaranteed`, '') AS time_to_exe_guaranteed,
               IFNULL(`t2_min_order`, 0) AS min_order, IFNULL(`t2_exist`, 0) AS t2_exist
        FROM `shop_carts`
        WHERE `user_id` = @userId
          AND `session_id` = 0
        ORDER BY `id` DESC
        LIMIT @limit
        """;


    /// <summary>Bank reconciliation KPIs from statement lines.</summary>
    public const string SelectErpBankReconciliationStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_erp_bank_statement_lines`) AS line_count,
            (SELECT COUNT(*) FROM `epc_erp_bank_statement_lines` WHERE IFNULL(`matched_entry_id`,0)=0) AS unmatched_count,
            (SELECT COUNT(*) FROM `epc_erp_bank_statement_lines` WHERE IFNULL(`matched_entry_id`,0)>0) AS matched_count,
            (SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_bank_statement_lines` WHERE IFNULL(`direction`,0)=1) AS credit_total,
            (SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_bank_statement_lines` WHERE IFNULL(`direction`,0)=0) AS debit_total
        """;

    /// <summary>Bank statement lines for reconciliation.</summary>
    public const string SelectErpBankReconciliationLines = """
        SELECT `id`, IFNULL(`account_id`,0) AS account_id, IFNULL(`line_date`,0) AS line_date,
               IFNULL(`description`,'') AS description, IFNULL(`reference`,'') AS reference,
               IFNULL(`amount`,0) AS amount, IFNULL(`direction`,0) AS direction,
               IFNULL(`matched_entry_id`,0) AS matched_entry_id, IFNULL(`import_batch`,'') AS import_batch,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_bank_statement_lines`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Stock transfer KPIs — omits notes.</summary>
    public const string SelectErpStockTransferStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_warehouse_transfers`) AS transfer_count,
            (SELECT COUNT(*) FROM `epc_warehouse_transfers` WHERE IFNULL(`status`,'')='draft') AS draft_count,
            (SELECT COUNT(*) FROM `epc_warehouse_transfers` WHERE IFNULL(`status`,'')='in_transit') AS in_transit_count,
            (SELECT COUNT(*) FROM `epc_warehouse_transfers` WHERE IFNULL(`status`,'')='received') AS received_count,
            (SELECT IFNULL(SUM(`total_qty`),0) FROM `epc_warehouse_transfers`) AS total_qty
        """;

    /// <summary>Warehouse stock transfers — omits notes.</summary>
    public const string SelectErpStockTransfers = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`transfer_no`,'') AS transfer_no,
               IFNULL(`from_warehouse_id`,0) AS from_warehouse_id, IFNULL(`to_warehouse_id`,0) AS to_warehouse_id,
               IFNULL(`reason`,'') AS reason, IFNULL(`status`,'') AS status,
               IFNULL(`total_items`,0) AS total_items, IFNULL(`total_qty`,0) AS total_qty,
               IFNULL(`shipped_at`,'') AS shipped_at, IFNULL(`received_at`,'') AS received_at,
               IFNULL(`created_by`,0) AS created_by, IFNULL(`time_created`,0) AS time_created
        FROM `epc_warehouse_transfers`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Sales quotation KPIs — omits notes.</summary>
    public const string SelectErpSalesQuotationStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_crm_quotes` WHERE IFNULL(`active`,0)=1) AS quote_count,
            (SELECT COUNT(*) FROM `epc_crm_quotes` WHERE IFNULL(`active`,0)=1 AND IFNULL(`status`,'')='draft') AS draft_count,
            (SELECT COUNT(*) FROM `epc_crm_quotes` WHERE IFNULL(`active`,0)=1 AND IFNULL(`status`,'')='sent') AS sent_count,
            (SELECT COUNT(*) FROM `epc_crm_quotes` WHERE IFNULL(`active`,0)=1 AND IFNULL(`status`,'')='accepted') AS accepted_count,
            (SELECT IFNULL(SUM(`subtotal`),0) FROM `epc_crm_quotes` WHERE IFNULL(`active`,0)=1) AS subtotal_sum
        """;

    /// <summary>Sales quotations — omits notes.</summary>
    public const string SelectErpSalesQuotations = """
        SELECT `id`, IFNULL(`opportunity_id`,0) AS opportunity_id, IFNULL(`lead_id`,0) AS lead_id,
               IFNULL(`customer_user_id`,0) AS customer_user_id, IFNULL(`quote_number`,'') AS quote_number,
               IFNULL(`status`,'') AS status, IFNULL(`currency_code`,'') AS currency_code,
               IFNULL(`subtotal`,0) AS subtotal, IFNULL(`shop_order_id`,0) AS shop_order_id,
               IFNULL(`time_created`,0) AS time_created, IFNULL(`active`,0) AS active
        FROM `epc_crm_quotes`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Workspace favorites / shortcut KPIs.</summary>
    public const string SelectErpWorkspaceFavoriteStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_user_shortcuts`) AS shortcut_count,
            (SELECT COUNT(*) FROM `epc_user_shortcuts` WHERE IFNULL(`is_pinned`,0)=1) AS pinned_count,
            (SELECT COUNT(DISTINCT `user_id`) FROM `epc_user_shortcuts`) AS user_count,
            (SELECT COUNT(*) FROM `epc_user_shortcuts` WHERE IFNULL(`surface`,'') IN ('erp','both','')) AS erp_surface_count
        """;

    /// <summary>Workspace favorites / shortcuts.</summary>
    public const string SelectErpWorkspaceFavorites = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`user_id`,0) AS user_id,
               IFNULL(`surface`,'') AS surface, IFNULL(`shortcut_key`,'') AS shortcut_key,
               IFNULL(`label`,'') AS label, IFNULL(`icon_class`,'') AS icon_class,
               IFNULL(`target_url`,'') AS target_url, IFNULL(`target_tab`,'') AS target_tab,
               IFNULL(`sort_order`,0) AS sort_order, IFNULL(`is_pinned`,0) AS is_pinned,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_user_shortcuts`
        ORDER BY `sort_order` ASC, `id` DESC
        LIMIT @limit
        """;

    /// <summary>Fixed asset KPIs — omits note.</summary>
    public const string SelectErpFixedAssetStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_erp_fa_assets`) AS asset_count,
            (SELECT COUNT(*) FROM `epc_erp_fa_assets` WHERE IFNULL(`status`,'')='active') AS active_count,
            (SELECT COUNT(*) FROM `epc_erp_fa_assets` WHERE IFNULL(`status`,'')='disposed') AS disposed_count,
            (SELECT IFNULL(SUM(`cost`),0) FROM `epc_erp_fa_assets`) AS cost_total,
            (SELECT IFNULL(SUM(`book_value`),0) FROM `epc_erp_fa_assets`) AS book_value_total
        """;

    /// <summary>Fixed assets register — omits note.</summary>
    public const string SelectErpFixedAssets = """
        SELECT `id`, IFNULL(`asset_code`,'') AS asset_code, IFNULL(`name`,'') AS name,
               IFNULL(`category_id`,0) AS category_id, IFNULL(`acquisition_date`,'') AS acquisition_date,
               IFNULL(`cost`,0) AS cost, IFNULL(`salvage_value`,0) AS salvage_value,
               IFNULL(`useful_life_months`,0) AS useful_life_months,
               IFNULL(`depreciation_method`,'') AS depreciation_method,
               IFNULL(`accumulated_depreciation`,0) AS accumulated_depreciation,
               IFNULL(`book_value`,0) AS book_value, IFNULL(`location`,'') AS location,
               IFNULL(`status`,'') AS status, IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_fa_assets`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Page builder layout KPIs — omits layout_json/brand_json.</summary>
    public const string SelectCpPageBuilderStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_page_builder_layouts`) AS layout_count,
            (SELECT COUNT(*) FROM `epc_page_builder_layouts` WHERE IFNULL(`is_published`,0)=1) AS published_count,
            (SELECT COUNT(*) FROM `epc_page_builder_layouts` WHERE IFNULL(`is_published`,0)=0) AS draft_count,
            (SELECT COUNT(DISTINCT `site_key`) FROM `epc_page_builder_layouts`) AS site_count
        """;

    /// <summary>Page builder layouts — omits layout_json/brand_json.</summary>
    public const string SelectCpPageBuilderLayouts = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`page_key`,'') AS page_key,
               IFNULL(`is_published`,0) AS is_published, IFNULL(`updated_at`,0) AS updated_at,
               IFNULL(`published_at`,0) AS published_at
        FROM `epc_page_builder_layouts`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Product catalogue KPIs from shop_catalogue_products.</summary>
    public const string SelectCpProductCatalogueStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_catalogue_products`) AS product_count,
            (SELECT COUNT(*) FROM `shop_catalogue_products` WHERE IFNULL(`published_flag`,0)=1) AS published_count,
            (SELECT COUNT(*) FROM `shop_catalogue_products` WHERE IFNULL(`published_flag`,0)=0) AS unpublished_count,
            (SELECT COUNT(DISTINCT `category_id`) FROM `shop_catalogue_products`) AS category_count
        """;

    /// <summary>Product catalogue rows — safe columns only.</summary>
    public const string SelectCpProductCatalogue = """
        SELECT `id`, IFNULL(`category_id`,0) AS category_id, IFNULL(`caption`,'') AS caption,
               IFNULL(`alias`,'') AS alias, IFNULL(`published_flag`,0) AS published_flag
        FROM `shop_catalogue_products`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Platform governance KPIs — omits description/config_json.</summary>
    public const string SelectCpPlatformGovernanceStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_platform_governance_rules`) AS rule_count,
            (SELECT COUNT(*) FROM `epc_platform_governance_rules` WHERE IFNULL(`active`,0)=1) AS active_count,
            (SELECT COUNT(*) FROM `epc_platform_governance_rules` WHERE IFNULL(`enforcement`,'')='required') AS required_count,
            (SELECT COUNT(DISTINCT `category`) FROM `epc_platform_governance_rules`) AS category_count
        """;

    /// <summary>Platform governance rules — omits description/config_json.</summary>
    public const string SelectCpPlatformGovernanceRules = """
        SELECT `id`, IFNULL(`rule_key`,'') AS rule_key, IFNULL(`category`,'') AS category,
               IFNULL(`title`,'') AS title, IFNULL(`enforcement`,'') AS enforcement,
               IFNULL(`scope`,'') AS scope, IFNULL(`module_link`,'') AS module_link,
               IFNULL(`active`,0) AS active, IFNULL(`time_updated`,0) AS time_updated
        FROM `epc_platform_governance_rules`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>E-invoice document KPIs from epc_einvoice_documents (CREATE TABLE in epc_einvoice_schema.php).</summary>
    public const string SelectCpEinvoiceDocumentStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE IFNULL(`active`,0)=1) AS document_count,
            (SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE IFNULL(`active`,0)=1 AND IFNULL(`status`,'') IN ('draft','validated','queued')) AS open_count,
            (SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE IFNULL(`active`,0)=1 AND IFNULL(`status`,'') IN ('submitted','accepted')) AS submitted_count,
            (SELECT IFNULL(SUM(`total_incl_vat`),0) FROM `epc_einvoice_documents` WHERE IFNULL(`active`,0)=1) AS total_incl_vat
        """;

    /// <summary>E-invoice documents — omits seller_json/buyer_json/xml/validation/tax_breakdown payloads.</summary>
    public const string SelectCpEinvoiceDocuments = """
        SELECT `id`, IFNULL(`uuid`,'') AS uuid, IFNULL(`invoice_number`,'') AS invoice_number,
               IFNULL(`order_id`,0) AS order_id, IFNULL(`user_id`,0) AS user_id,
               IFNULL(`doc_category`,'') AS doc_category, IFNULL(`issue_date`,0) AS issue_date,
               IFNULL(`currency_code`,'') AS currency_code, IFNULL(`status`,'') AS status,
               IFNULL(`total_incl_vat`,0) AS total_incl_vat, IFNULL(`validation_ok`,0) AS validation_ok,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_einvoice_documents`
        WHERE IFNULL(`active`,0)=1
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Jewellery repair KPIs from epc_jewel_repair (CREATE TABLE in epc_erp_jewellery.php).</summary>
    public const string SelectCpJewelleryRepairStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_jewel_repair`) AS repair_count,
            (SELECT COUNT(*) FROM `epc_jewel_repair` WHERE IFNULL(`status`,'') IN ('received','in_progress','workshop')) AS open_count,
            (SELECT COUNT(*) FROM `epc_jewel_repair` WHERE IFNULL(`authorized`,0)=1) AS authorized_count,
            (SELECT COUNT(*) FROM `epc_jewel_repair_items`) AS item_count
        """;

    /// <summary>Jewellery repairs — omits mobile/email/tel/remarks/narration/customer PII.</summary>
    public const string SelectCpJewelleryRepairs = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`branch`,'') AS branch,
               IFNULL(`voc_type`,'') AS voc_type, IFNULL(`voc_date`,'') AS voc_date,
               IFNULL(`voc_no`,0) AS voc_no, IFNULL(`customer_name`,'') AS customer_name,
               IFNULL(`status`,'') AS status, IFNULL(`currency`,'') AS currency,
               IFNULL(`delivery_date`,'') AS delivery_date, IFNULL(`authorized`,0) AS authorized,
               IFNULL(`created_at`,'') AS created_at
        FROM `epc_jewel_repair`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>CRM ticket KPIs from epc_crm_tickets (CREATE TABLE in epc_crm_schema.php).</summary>
    public const string SelectCpCrmTicketStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_crm_tickets` WHERE IFNULL(`active`,0)=1) AS ticket_count,
            (SELECT COUNT(*) FROM `epc_crm_tickets` WHERE IFNULL(`active`,0)=1 AND IFNULL(`status`,'') IN ('open','pending')) AS open_count,
            (SELECT COUNT(*) FROM `epc_crm_tickets` WHERE IFNULL(`active`,0)=1 AND IFNULL(`priority`,'') IN ('high','urgent')) AS high_priority_count,
            (SELECT COUNT(*) FROM `epc_crm_ticket_messages`) AS message_count
        """;

    /// <summary>CRM tickets — subject/status only (message bodies omitted).</summary>
    public const string SelectCpCrmTickets = """
        SELECT `id`, IFNULL(`customer_user_id`,0) AS customer_user_id, IFNULL(`order_id`,0) AS order_id,
               IFNULL(`subject`,'') AS subject, IFNULL(`status`,'') AS status,
               IFNULL(`priority`,'') AS priority, IFNULL(`assigned_user_id`,0) AS assigned_user_id,
               IFNULL(`time_created`,0) AS time_created, IFNULL(`time_updated`,0) AS time_updated,
               IFNULL(`active`,0) AS active
        FROM `epc_crm_tickets`
        WHERE IFNULL(`active`,0)=1
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Marketing growth KPIs from epc_marketing_* (CREATE TABLE in epc_marketing_schema.php).</summary>
    public const string SelectCpMarketingGrowthStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_marketing_task_progress`) AS task_count,
            (SELECT COUNT(*) FROM `epc_marketing_task_progress` WHERE IFNULL(`is_done`,0)=1) AS tasks_done,
            (SELECT COUNT(*) FROM `epc_marketing_kpi_log`) AS kpi_log_count,
            (SELECT COUNT(*) FROM `epc_marketing_reviews`) AS review_count
        """;

    /// <summary>Marketing growth reviews — omits notes.</summary>
    public const string SelectCpMarketingGrowthReviews = """
        SELECT `id`, IFNULL(`strategy_key`,'') AS strategy_key, IFNULL(`review_type`,'') AS review_type,
               IFNULL(`score`,0) AS score, IFNULL(`created_at`,0) AS created_at,
               IFNULL(`created_by`,0) AS created_by
        FROM `epc_marketing_reviews`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>SOC 2 KPIs from epc_soc2_* (CREATE TABLE in epc_soc2_compliance.php).</summary>
    public const string SelectCpSoc2ComplianceStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_soc2_controls`) AS control_count,
            (SELECT COUNT(*) FROM `epc_soc2_controls` WHERE IFNULL(`status`,'') IN ('implemented','tested','effective')) AS implemented_count,
            (SELECT COUNT(*) FROM `epc_soc2_evidence`) AS evidence_count,
            (SELECT COUNT(*) FROM `epc_soc2_policies`) AS policy_count
        """;

    /// <summary>SOC 2 controls — omits description/implementation.</summary>
    public const string SelectCpSoc2Controls = """
        SELECT `id`, IFNULL(`control_id`,'') AS control_id, IFNULL(`category`,'') AS category,
               IFNULL(`title`,'') AS title, IFNULL(`status`,'') AS status,
               IFNULL(`owner`,'') AS owner, IFNULL(`frequency`,'') AS frequency,
               IFNULL(`risk_level`,'') AS risk_level
        FROM `epc_soc2_controls`
        ORDER BY `control_id` ASC
        LIMIT @limit
        """;

    /// <summary>Cost model KPIs from epc_costm_* (CREATE TABLE in epc_erp_cost_models.php).</summary>
    public const string SelectCpCostModelsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_costm_item`) AS item_count,
            (SELECT COUNT(*) FROM `epc_costm_txn`) AS txn_count,
            (SELECT COUNT(*) FROM `epc_costm_close`) AS close_count,
            (SELECT COUNT(DISTINCT `model`) FROM `epc_costm_item`) AS model_count
        """;

    /// <summary>Cost model item assignments.</summary>
    public const string SelectCpCostModelItems = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`item_id`,0) AS item_id,
               IFNULL(`model`,'') AS model, IFNULL(`std_cost`,0) AS std_cost,
               IFNULL(`time_updated`,0) AS time_updated
        FROM `epc_costm_item`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Financial depth KPIs from epc_fin_* (CREATE TABLE in epc_erp_fin_advanced.php).</summary>
    public const string SelectCpFinAdvancedStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_fin_periods`) AS period_count,
            (SELECT COUNT(*) FROM `epc_fin_periods` WHERE IFNULL(`status`,'')='open') AS open_period_count,
            (SELECT COUNT(*) FROM `epc_fin_alloc_rule` WHERE IFNULL(`active`,0)=1) AS alloc_rule_count,
            (SELECT COUNT(*) FROM `epc_fin_accrual`) AS accrual_count
        """;

    /// <summary>Fiscal periods — omits allocation/accrual/FX JSON payloads.</summary>
    public const string SelectCpFinPeriods = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`fy`,0) AS fy,
               IFNULL(`period_no`,0) AS period_no, IFNULL(`start_date`,0) AS start_date,
               IFNULL(`end_date`,0) AS end_date, IFNULL(`status`,'') AS status,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_fin_periods`
        ORDER BY `fy` DESC, `period_no` DESC, `id` DESC
        LIMIT @limit
        """;

    /// <summary>Blockchain proof KPIs from epc_bc_* (CREATE TABLE in epc_blockchain_bos.php).</summary>
    public const string SelectCpBlockchainProofStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_bc_proofs`) AS proof_count,
            (SELECT COUNT(*) FROM `epc_bc_proofs` WHERE IFNULL(`status`,'')='pending') AS pending_count,
            (SELECT COUNT(*) FROM `epc_bc_proofs` WHERE IFNULL(`status`,'') IN ('anchored','confirmed')) AS anchored_count,
            (SELECT COUNT(*) FROM `epc_bc_anchor_batches`) AS batch_count
        """;

    /// <summary>Blockchain proofs — omits payload_json/merkle_proof_json.</summary>
    public const string SelectCpBlockchainProofs = """
        SELECT `id`, IFNULL(`proof_uid`,'') AS proof_uid, IFNULL(`tenant_key`,'') AS tenant_key,
               IFNULL(`record_type`,'') AS record_type, IFNULL(`record_id`,'') AS record_id,
               IFNULL(`payload_hash`,'') AS payload_hash, IFNULL(`status`,'') AS status,
               `batch_id`, IFNULL(`anchor_ref`,'') AS anchor_ref,
               IFNULL(`created_at`,'') AS created_at
        FROM `epc_bc_proofs`
        ORDER BY `id` DESC
        LIMIT @limit
        """;


    /// <summary>Landed-cost KPIs from epc_landed_cost_* (CREATE TABLE in epc_erp_landed_cost_v2.php).</summary>
    public const string SelectCpLandedCostStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_landed_cost_sheets`) AS sheet_count,
            (SELECT COUNT(*) FROM `epc_landed_cost_sheets` WHERE IFNULL(`status`,'') IN ('calculated','posted')) AS posted_count,
            (SELECT COUNT(*) FROM `epc_landed_cost_expenses`) AS expense_count,
            (SELECT COUNT(*) FROM `epc_landed_cost_lines`) AS line_count
        """;

    /// <summary>Landed cost sheets — omits notes.</summary>
    public const string SelectCpLandedCostSheets = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`sheet_no`,'') AS sheet_no,
               IFNULL(`po_reference`,'') AS po_reference, IFNULL(`grn_reference`,'') AS grn_reference,
               IFNULL(`supplier_id`,0) AS supplier_id, IFNULL(`supplier_name`,'') AS supplier_name,
               IFNULL(`goods_value`,0) AS goods_value, IFNULL(`total_expenses`,0) AS total_expenses,
               IFNULL(`distribution_method`,'') AS distribution_method, IFNULL(`currency`,'') AS currency,
               IFNULL(`status`,'') AS status, IFNULL(`time_created`,0) AS time_created
        FROM `epc_landed_cost_sheets`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>WMS KPIs from epc_erp_wms_* (CREATE TABLE in epc_erp_wms.php).</summary>
    public const string SelectCpWarehouseWmsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_erp_wms_locations` WHERE IFNULL(`active`,0)=1) AS location_count,
            (SELECT COUNT(*) FROM `epc_erp_wms_lp` WHERE IFNULL(`status`,'')='active') AS lp_count,
            (SELECT COUNT(*) FROM `epc_erp_wms_waves`) AS wave_count,
            (SELECT COUNT(*) FROM `epc_erp_wms_work` WHERE IFNULL(`status`,'') IN ('open','assigned')) AS open_work_count
        """;

    /// <summary>WMS work pool — status/type overview.</summary>
    public const string SelectCpWarehouseWmsWork = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`work_type`,'') AS work_type,
               IFNULL(`reference`,'') AS reference, IFNULL(`wave_id`,0) AS wave_id,
               IFNULL(`item`,'') AS item, IFNULL(`qty`,0) AS qty,
               IFNULL(`status`,'') AS status, IFNULL(`assigned_to`,'') AS assigned_to,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_wms_work`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>AI service KPIs from epc_ai_* (CREATE TABLE in epc_ai_service.php).</summary>
    public const string SelectCpAiServiceStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_ai_queries`) AS query_count,
            (SELECT COUNT(*) FROM `epc_ai_queries` WHERE IFNULL(`status`,'')='success') AS success_count,
            (SELECT COUNT(*) FROM `epc_ai_queries` WHERE IFNULL(`status`,'') IN ('refused','pii_blocked','error')) AS blocked_count,
            (SELECT COUNT(*) FROM `epc_ai_providers` WHERE IFNULL(`active`,0)=1) AS provider_count
        """;

    /// <summary>AI queries — omits input_text/output_text (PII).</summary>
    public const string SelectCpAiServiceQueries = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`user_id`,0) AS user_id,
               IFNULL(`service`,'') AS service, IFNULL(`intent`,'') AS intent,
               IFNULL(`tokens_used`,0) AS tokens_used, IFNULL(`execution_ms`,0) AS execution_ms,
               IFNULL(`pii_stripped`,0) AS pii_stripped, IFNULL(`status`,'') AS status,
               IFNULL(`created_at`,'') AS created_at
        FROM `epc_ai_queries`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Returns/RMA KPIs from epc_warranties/epc_rma_* (CREATE TABLE in epc_warranty_rma.php).</summary>
    public const string SelectCpReturnsRmaStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_rma_requests`) AS rma_count,
            (SELECT COUNT(*) FROM `epc_rma_requests` WHERE IFNULL(`status`,'') IN ('pending','approved','received','inspecting','repair','replacement','refund')) AS open_count,
            (SELECT COUNT(*) FROM `epc_warranties` WHERE IFNULL(`status`,'')='active') AS active_warranty_count,
            (SELECT COUNT(*) FROM `epc_rma_items`) AS item_count
        """;

    /// <summary>RMA requests — omits description/resolution_notes.</summary>
    public const string SelectCpReturnsRmaRequests = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`rma_number`,'') AS rma_number,
               `warranty_id`, IFNULL(`customer_id`,0) AS customer_id,
               IFNULL(`customer_name`,'') AS customer_name, IFNULL(`reason`,'') AS reason,
               IFNULL(`status`,'') AS status, IFNULL(`resolution_type`,'') AS resolution_type,
               IFNULL(`created_at`,'') AS created_at
        FROM `epc_rma_requests`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Isolation audit KPIs from epc_ci_* (CREATE TABLE in epc_commerce_isolation.php).</summary>
    public const string SelectCpIsolationAuditStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_ci_audit_runs`) AS run_count,
            (SELECT COUNT(*) FROM `epc_ci_audit_runs` WHERE IFNULL(`failed`,0)>0) AS failed_run_count,
            (SELECT COUNT(*) FROM `epc_ci_violations`) AS violation_count,
            (SELECT COUNT(DISTINCT `site_key`) FROM `epc_ci_violations`) AS site_count
        """;

    /// <summary>Isolation audit runs — omits report_json.</summary>
    public const string SelectCpIsolationAuditRuns = """
        SELECT `id`, IFNULL(`run_at`,'') AS run_at,
               IFNULL(`total_tenants`,0) AS total_tenants,
               IFNULL(`passed`,0) AS passed, IFNULL(`failed`,0) AS failed,
               IFNULL(`warnings`,0) AS warnings,
               IFNULL(`triggered_by`,'') AS triggered_by
        FROM `epc_ci_audit_runs`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>AML KPIs from epc_aml_* (CREATE TABLE in epc_erp_aml_compliance.php).</summary>
    public const string SelectCpAmlComplianceStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_aml_kyc`) AS kyc_count,
            (SELECT COUNT(*) FROM `epc_aml_kyc` WHERE IFNULL(`verification_status`,'')='pending') AS pending_kyc_count,
            (SELECT COUNT(*) FROM `epc_aml_transactions` WHERE IFNULL(`flagged`,0)=1) AS flagged_txn_count,
            (SELECT COUNT(*) FROM `epc_aml_rules` WHERE IFNULL(`is_active`,0)=1) AS active_rule_count
        """;

    /// <summary>AML KYC rows — omits notes/id_document_path.</summary>
    public const string SelectCpAmlComplianceKyc = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`customer_id`,0) AS customer_id,
               IFNULL(`customer_name`,'') AS customer_name, IFNULL(`id_type`,'') AS id_type,
               IFNULL(`risk_level`,'') AS risk_level, IFNULL(`pep_status`,0) AS pep_status,
               IFNULL(`verification_status`,'') AS verification_status,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_aml_kyc`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Jewellery master KPIs from epc_jewel_* masters (CREATE TABLE in epc_erp_jewellery.php).</summary>
    public const string SelectCpJewelleryMastersStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_jewel_karat_master`) AS karat_count,
            (SELECT COUNT(*) FROM `epc_jewel_rate_type`) AS rate_type_count,
            (SELECT COUNT(*) FROM `epc_jewel_barcode`) AS barcode_count,
            (SELECT COUNT(*) FROM `epc_jewel_diamond_master`) AS diamond_count
        """;

    /// <summary>Jewellery karat masters — omits description.</summary>
    public const string SelectCpJewelleryMastersKarats = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`karat_code`,'') AS karat_code,
               IFNULL(`std_purity`,0) AS std_purity, IFNULL(`range_from`,0) AS range_from,
               IFNULL(`range_to`,0) AS range_to, IFNULL(`sp_gravity`,0) AS sp_gravity,
               IFNULL(`division`,'') AS division, IFNULL(`created_at`,'') AS created_at
        FROM `epc_jewel_karat_master`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Consolidation KPIs from epc_cons_* (CREATE TABLE in epc_erp_consolidation.php).</summary>
    public const string SelectCpConsolidationsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_cons_entities` WHERE IFNULL(`active`,0)=1) AS entity_count,
            (SELECT COUNT(*) FROM `epc_cons_figures`) AS figure_count,
            (SELECT COUNT(*) FROM `epc_cons_ic`) AS ic_count,
            (SELECT COUNT(*) FROM `epc_cons_ic` WHERE IFNULL(`reconciled`,0)=0) AS open_ic_count
        """;

    /// <summary>Consolidation entities — group members.</summary>
    public const string SelectCpConsolidationsEntities = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`currency_code`,'') AS currency_code,
               IFNULL(`ownership_pct`,0) AS ownership_pct,
               IFNULL(`is_home`,0) AS is_home, IFNULL(`parent_code`,'') AS parent_code,
               IFNULL(`active`,0) AS active, IFNULL(`time_created`,0) AS time_created
        FROM `epc_cons_entities`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>CRM activity KPIs from epc_crm_activities (CREATE TABLE in epc_crm_schema.php).</summary>
    public const string SelectCpCrmActivitiesStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_crm_activities` WHERE IFNULL(`active`,1)=1) AS activity_count,
            (SELECT COUNT(*) FROM `epc_crm_activities` WHERE IFNULL(`active`,1)=1 AND IFNULL(`done`,0)=0) AS open_count,
            (SELECT COUNT(*) FROM `epc_crm_activities` WHERE IFNULL(`active`,1)=1 AND IFNULL(`done`,0)=0 AND IFNULL(`due_date`,0)>0 AND `due_date` <= UNIX_TIMESTAMP()) AS overdue_count,
            (SELECT COUNT(*) FROM `epc_crm_activities` WHERE IFNULL(`active`,1)=1 AND IFNULL(`done`,0)=1) AS done_count
        """;

    /// <summary>CRM activities — omits notes.</summary>
    public const string SelectCpCrmActivities = """
        SELECT `id`, IFNULL(`activity_type`,'') AS activity_type,
               IFNULL(`related_type`,'') AS related_type, IFNULL(`related_id`,0) AS related_id,
               IFNULL(`due_date`,0) AS due_date, IFNULL(`done`,0) AS done,
               IFNULL(`owner_user_id`,0) AS owner_user_id,
               IFNULL(`time_created`,0) AS time_created, IFNULL(`active`,1) AS active
        FROM `epc_crm_activities`
        WHERE IFNULL(`active`,1)=1
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Auth MFA KPIs from epc_mfa_* (CREATE TABLE in epc_auth_mfa.php).</summary>
    public const string SelectCpAuthMfaStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_mfa_secrets`) AS secret_count,
            (SELECT COUNT(*) FROM `epc_mfa_secrets` WHERE IFNULL(`confirmed`,0)=1) AS confirmed_count,
            (SELECT COUNT(*) FROM `epc_mfa_backup_codes` WHERE IFNULL(`used`,0)=0) AS backup_unused_count,
            (SELECT COUNT(*) FROM `epc_mfa_policy`) AS policy_count
        """;

    /// <summary>MFA secrets — omits secret/webauthn credential material.</summary>
    public const string SelectCpAuthMfaSecrets = """
        SELECT `id`, IFNULL(`user_id`,0) AS user_id, IFNULL(`method`,'') AS method,
               IFNULL(`confirmed`,0) AS confirmed, IFNULL(`label`,'') AS label,
               IFNULL(`created_at`,'') AS created_at, IFNULL(`last_used_at`,'') AS last_used_at
        FROM `epc_mfa_secrets`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Electronic reporting KPIs from epc_er_* (CREATE TABLE in epc_erp_elec_reporting.php).</summary>
    public const string SelectCpElectronicReportingStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_er_format` WHERE IFNULL(`active`,1)=1) AS format_count,
            (SELECT COUNT(*) FROM `epc_er_field`) AS field_count,
            (SELECT COUNT(*) FROM `epc_er_run`) AS run_count,
            (SELECT COUNT(DISTINCT `output_type`) FROM `epc_er_format`) AS output_type_count
        """;

    /// <summary>Electronic reporting formats — preview lives on runs, omitted.</summary>
    public const string SelectCpElectronicReportingFormats = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`code`,'') AS code,
               IFNULL(`name`,'') AS name, IFNULL(`output_type`,'') AS output_type,
               IFNULL(`root_element`,'') AS root_element, IFNULL(`row_element`,'') AS row_element,
               IFNULL(`active`,1) AS active, IFNULL(`time_created`,0) AS time_created
        FROM `epc_er_format`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Collections/dunning KPIs from epc_dunning_* (CREATE TABLE in epc_collections_dunning.php).</summary>
    public const string SelectCpCollectionsDunningStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_dunning_queue`) AS queue_count,
            (SELECT COUNT(*) FROM `epc_dunning_queue` WHERE IFNULL(`status`,'') IN ('open','in_progress','promised','partial','disputed')) AS open_count,
            (SELECT COUNT(*) FROM `epc_dunning_profiles` WHERE IFNULL(`active`,0)=1) AS profile_count,
            (SELECT COUNT(*) FROM `epc_dunning_log`) AS log_count
        """;

    /// <summary>Dunning queue — omits notes and customer_name (PII).</summary>
    public const string SelectCpCollectionsDunningQueue = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`customer_id`,0) AS customer_id,
               IFNULL(`invoice_ref`,'') AS invoice_ref,
               IFNULL(`invoice_amount`,0) AS invoice_amount, IFNULL(`amount_due`,0) AS amount_due,
               IFNULL(`due_date`,'') AS due_date, IFNULL(`days_overdue`,0) AS days_overdue,
               IFNULL(`dunning_step`,0) AS dunning_step, IFNULL(`status`,'') AS status,
               IFNULL(`updated_at`,'') AS updated_at
        FROM `epc_dunning_queue`
        ORDER BY `id` DESC
        LIMIT @limit
        """;


    /// <summary>Marketplace channel KPIs from epc_marketplace_* (CREATE TABLE in epc_channel_schema.php).</summary>
    public const string SelectCpMarketplaceChannelsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_marketplace_channels`) AS channel_count,
            (SELECT COUNT(*) FROM `epc_marketplace_channels` WHERE IFNULL(`active`,0)=1) AS active_count,
            (SELECT COUNT(*) FROM `epc_marketplace_sku_map` WHERE IFNULL(`active`,0)=1) AS sku_map_count,
            (SELECT COUNT(*) FROM `epc_marketplace_orders`) AS order_count
        """;

    /// <summary>Marketplace channels — omits config_json.</summary>
    public const string SelectCpMarketplaceChannels = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`marketplace_id`,'') AS marketplace_id, IFNULL(`active`,0) AS active,
               IFNULL(`demo_mode`,0) AS demo_mode, IFNULL(`last_sync_at`,0) AS last_sync_at,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_marketplace_channels`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Demand intelligence KPIs from epc_demand_* (CREATE TABLE in epc_demand_intelligence.php).</summary>
    public const string SelectCpDemandIntelligenceStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_demand_country`) AS country_count,
            (SELECT COUNT(*) FROM `epc_article_demand`) AS article_demand_count,
            (SELECT COUNT(*) FROM `epc_price_list_demand`) AS price_list_demand_count,
            (SELECT COUNT(*) FROM `epc_user_demand_country`) AS user_demand_count
        """;

    /// <summary>Demand country rows.</summary>
    public const string SelectCpDemandIntelligenceCountries = """
        SELECT IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`sort_order`,0) AS sort_order
        FROM `epc_demand_country`
        ORDER BY `sort_order` ASC, `code` ASC
        LIMIT @limit
        """;

    /// <summary>Credit limit KPIs from epc_credit_* (CREATE TABLE in epc_credit_limit.php).</summary>
    public const string SelectCpCreditLimitsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_credit_limits`) AS limit_count,
            (SELECT COUNT(*) FROM `epc_credit_limits` WHERE IFNULL(`status`,'')='active') AS active_count,
            (SELECT COUNT(*) FROM `epc_credit_limits` WHERE IFNULL(`status`,'') IN ('on_hold','suspended','review')) AS held_count,
            (SELECT COUNT(*) FROM `epc_credit_transactions`) AS txn_count
        """;

    /// <summary>Credit limits — omits notes/hold_reason detail beyond status.</summary>
    public const string SelectCpCreditLimits = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`customer_id`,0) AS customer_id,
               IFNULL(`credit_limit`,0) AS credit_limit, IFNULL(`balance_used`,0) AS balance_used,
               IFNULL(`currency`,'') AS currency, IFNULL(`status`,'') AS status,
               IFNULL(`risk_score`,0) AS risk_score, IFNULL(`payment_terms`,'') AS payment_terms,
               IFNULL(`updated_at`,'') AS updated_at
        FROM `epc_credit_limits`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Insurance KPIs from epc_erp_ins_* (CREATE TABLE in epc_erp_insurance.php).</summary>
    public const string SelectCpInsuranceComplianceStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_erp_ins_policies`) AS policy_count,
            (SELECT COUNT(*) FROM `epc_erp_ins_policies` WHERE IFNULL(`status`,'')='active') AS active_count,
            (SELECT COUNT(*) FROM `epc_erp_ins_claims`) AS claim_count,
            (SELECT COUNT(*) FROM `epc_erp_ins_documents`) AS document_count
        """;

    /// <summary>Insurance policies — omits note/contact_email.</summary>
    public const string SelectCpInsuranceCompliancePolicies = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`policy_no`,'') AS policy_no,
               IFNULL(`class`,'') AS policy_class, IFNULL(`title`,'') AS title,
               IFNULL(`insurer`,'') AS insurer, IFNULL(`sum_insured`,0) AS sum_insured,
               IFNULL(`premium`,0) AS premium, IFNULL(`currency`,'') AS currency,
               IFNULL(`expiry_date`,0) AS expiry_date, IFNULL(`status`,'') AS status,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_ins_policies`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>ERP audit trail KPIs from epc_erp_audit_log (CREATE TABLE in epc_erp_audit.php).</summary>
    public const string SelectCpAuditTrailStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_erp_audit_log`) AS entry_count,
            (SELECT COUNT(DISTINCT `action`) FROM `epc_erp_audit_log`) AS action_count,
            (SELECT COUNT(DISTINCT `admin_id`) FROM `epc_erp_audit_log`) AS admin_count,
            (SELECT COUNT(DISTINCT `entity_type`) FROM `epc_erp_audit_log` WHERE IFNULL(`entity_type`,'')<>'') AS entity_type_count
        """;

    /// <summary>ERP audit trail rows — omits detail_json/old_json/new_json/user_agent/ip_address.</summary>
    public const string SelectCpAuditTrailEntries = """
        SELECT `id`, IFNULL(`time`,0) AS time_unix, IFNULL(`admin_id`,0) AS admin_id,
               IFNULL(`action`,'') AS action, IFNULL(`entity_type`,'') AS entity_type,
               IFNULL(`entity_id`,0) AS entity_id, IFNULL(`summary`,'') AS summary
        FROM `epc_erp_audit_log`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Document expiry KPIs from epc_erp_doc_expiry* (CREATE TABLE in epc_erp_doc_expiry.php).</summary>
    public const string SelectCpDocExpiryStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_erp_doc_expiry`) AS document_count,
            (SELECT COUNT(*) FROM `epc_erp_doc_expiry` WHERE IFNULL(`active`,0)=1) AS active_count,
            (SELECT COUNT(*) FROM `epc_erp_doc_expiry` WHERE IFNULL(`active`,0)=1 AND IFNULL(`expiry_date`,0)>0 AND `expiry_date` < UNIX_TIMESTAMP()) AS expired_count,
            (SELECT COUNT(*) FROM `epc_erp_doc_expiry_reminders`) AS reminder_count
        """;

    /// <summary>Document expiry rows — omits note/owner_email/attachment_path.</summary>
    public const string SelectCpDocExpiryDocuments = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`category`,'') AS category,
               IFNULL(`doc_type`,'') AS doc_type, IFNULL(`title`,'') AS title,
               IFNULL(`ref_no`,'') AS ref_no, IFNULL(`owner`,'') AS owner,
               IFNULL(`issuer`,'') AS issuer, IFNULL(`expiry_date`,0) AS expiry_date,
               IFNULL(`source_module`,'') AS source_module, IFNULL(`active`,0) AS active,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_doc_expiry`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Tenant config KPIs from epc_tenant_config* (CREATE TABLE in epc_tenant_config.php).</summary>
    public const string SelectCpTenantConfigStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_tenant_config`) AS config_count,
            (SELECT COUNT(DISTINCT `config_group`) FROM `epc_tenant_config`) AS group_count,
            (SELECT COUNT(*) FROM `epc_tenant_config` WHERE IFNULL(`editable`,0)=1) AS editable_count,
            (SELECT COUNT(*) FROM `epc_tenant_config_history`) AS history_count
        """;

    /// <summary>Tenant config rows — omits config_value.</summary>
    public const string SelectCpTenantConfigEntries = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`config_group`,'') AS config_group,
               IFNULL(`config_key`,'') AS config_key, IFNULL(`value_type`,'') AS value_type,
               IFNULL(`label`,'') AS label, IFNULL(`editable`,0) AS editable,
               IFNULL(`updated_by`,0) AS updated_by, IFNULL(`updated_at`,'') AS updated_at
        FROM `epc_tenant_config`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Jewellery stock verification KPIs. Open = in_progress/Draft (PHP schema default + save path); complete = remaining_pcs=0 (PHP INSERT/schema status vocabulary is inconsistent).</summary>
    public const string SelectCpJewelleryStockVerificationStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_jewel_stock_verification`) AS verification_count,
            (SELECT COUNT(*) FROM `epc_jewel_stock_verification` WHERE IFNULL(`status`,'') IN ('in_progress','Draft','draft')) AS in_progress_count,
            (SELECT COUNT(*) FROM `epc_jewel_stock_verification` WHERE IFNULL(`remaining_pcs`,0)=0 AND IFNULL(`total_pcs`,0)>0) AS complete_count,
            (SELECT COUNT(*) FROM `epc_jewel_stock_verification_lines`) AS line_count
        """;

    /// <summary>Jewellery stock verification rows — omits remarks.</summary>
    public const string SelectCpJewelleryStockVerificationRows = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`branch`,'') AS branch,
               IFNULL(`voc_type`,'') AS voc_type, IFNULL(`voc_date`,'') AS voc_date,
               IFNULL(`voc_no`,0) AS voc_no, IFNULL(`location`,'') AS location,
               IFNULL(`total_pcs`,0) AS total_pcs, IFNULL(`scanned_pcs`,0) AS scanned_pcs,
               IFNULL(`remaining_pcs`,0) AS remaining_pcs, IFNULL(`status`,'') AS status,
               IFNULL(`created_by`,'') AS created_by
        FROM `epc_jewel_stock_verification`
        ORDER BY `id` DESC
        LIMIT @limit
        """;



    /// <summary>Tax external reporting KPIs from epc_cmp_rules + staging/audit (CREATE TABLE unused cluster).</summary>
    public const string SelectCpTaxExternalReportingStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_cmp_rules`) AS rule_count,
            (SELECT COUNT(*) FROM `epc_cmp_rules` WHERE IFNULL(`status`,'')='active') AS active_count,
            (SELECT COUNT(*) FROM `epc_cmp_staging`) AS staging_count,
            (SELECT COUNT(*) FROM `epc_cmp_audit`) AS audit_count
        """;

    /// <summary>Tax external reporting rows — value_json/notes omitted.</summary>
    public const string SelectCpTaxExternalReportingRows = """
        SELECT `id`, IFNULL(`country`,'') AS country, IFNULL(`rule_key`,'') AS rule_key,
               IFNULL(`version`,0) AS version, IFNULL(`status`,'') AS status,
               IFNULL(`source`,'') AS rule_source, IFNULL(`valid_from`,0) AS valid_from,
               IFNULL(`valid_to`,0) AS valid_to
        FROM `epc_cmp_rules`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>PO approvals KPIs from epc_po_requests + approval_steps (CREATE TABLE unused cluster).</summary>
    public const string SelectCpPoApprovalsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_po_requests`) AS request_count,
            (SELECT COUNT(*) FROM `epc_po_requests` WHERE IFNULL(`status`,'')='pending') AS pending_count,
            (SELECT COUNT(*) FROM `epc_po_requests` WHERE IFNULL(`status`,'')='approved') AS approved_count,
            (SELECT COUNT(*) FROM `epc_po_approval_steps`) AS step_count
        """;

    /// <summary>PO approvals rows — description/notes/attachments/items JSON omitted.</summary>
    public const string SelectCpPoApprovalsRows = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`po_number`,'') AS po_number,
               IFNULL(`requester_id`,0) AS requester_id, IFNULL(`vendor_name`,'') AS vendor_name,
               IFNULL(`currency`,'') AS currency, IFNULL(`total`,0) AS total,
               IFNULL(`status`,'') AS status, IFNULL(`current_tier`,0) AS current_tier,
               IFNULL(`priority`,'') AS priority, IFNULL(`created_at`,'') AS created_at
        FROM `epc_po_requests`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Finance close KPIs from epc_erp_opening_batches/lines + epc_erp_periods/close_log (CREATE TABLE unused cluster).</summary>
    public const string SelectCpFinanceCloseStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_erp_opening_batches`) AS batch_count,
            (SELECT COUNT(*) FROM `epc_erp_opening_batches` WHERE IFNULL(`status`,'')='posted') AS posted_batch_count,
            (SELECT COUNT(*) FROM `epc_erp_opening_lines`) AS opening_line_count,
            (SELECT COUNT(*) FROM `epc_erp_periods`) AS period_count,
            (SELECT COUNT(*) FROM `epc_erp_periods` WHERE IFNULL(`status`,'') IN ('soft_close','locked')) AS closed_period_count,
            (SELECT COUNT(*) FROM `epc_erp_period_close_log`) AS close_log_count
        """;

    /// <summary>Finance close rows — batch notes/meta_json/checklist omitted.</summary>
    public const string SelectCpFinanceCloseRows = """
        SELECT `id`, IFNULL(`module`,'') AS module, IFNULL(`as_of_date`,'') AS as_of_date,
               IFNULL(`reference`,'') AS reference, IFNULL(`status`,'') AS status,
               IFNULL(`admin_id`,0) AS admin_id, IFNULL(`time_created`,0) AS time_created,
               IFNULL(`time_posted`,0) AS time_posted
        FROM `epc_erp_opening_batches`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Jewellery fixing KPIs. Petty-cash count uses epc_jewel_voucher PCV (PHP save path); epc_jewel_petty_cash is a stale/empty helper table.</summary>
    public const string SelectCpJewelleryFixingStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_jewel_fixing`) AS fixing_count,
            (SELECT COUNT(*) FROM `epc_jewel_fixing` WHERE IFNULL(`status`,'')='open') AS open_fixing_count,
            (SELECT COUNT(*) FROM `epc_fix_unfix_purchases`) AS purchase_fix_count,
            (SELECT COUNT(*) FROM `epc_fix_unfix_settlements`) AS settlement_count,
            (SELECT COUNT(*) FROM `epc_jewel_voucher` WHERE IFNULL(`voc_type`,'')='PCV') AS petty_cash_count
        """;

    /// <summary>Jewellery fixing rows — remarks/notes omitted.</summary>
    public const string SelectCpJewelleryFixingRows = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`branch`,'') AS branch,
               IFNULL(`fix_type`,'') AS fix_type, IFNULL(`fix_date`,'') AS fix_date,
               IFNULL(`fix_no`,0) AS fix_no, IFNULL(`party_code`,'') AS party_code,
               IFNULL(`party_name`,'') AS party_name, IFNULL(`metal`,'') AS metal,
               IFNULL(`karat`,'') AS karat, IFNULL(`fix_qty_gms`,0) AS fix_qty_gms,
               IFNULL(`fix_amount`,0) AS fix_amount, IFNULL(`status`,'') AS status,
               IFNULL(`created_by`,'') AS created_by
        FROM `epc_jewel_fixing`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Web tracker KPIs from epc_web_tracker_* (CREATE TABLE in epc_web_tracker.php).</summary>
    public const string SelectCpWebTrackerStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_web_tracker_sessions`) AS session_count,
            (SELECT COUNT(*) FROM `epc_web_tracker_pageviews`) AS pageview_count,
            (SELECT COUNT(*) FROM `epc_web_tracker_events`) AS event_count,
            (SELECT COUNT(DISTINCT `country_code`) FROM `epc_web_tracker_sessions` WHERE IFNULL(`country_code`,'')<>'') AS country_count
        """;

    /// <summary>Web tracker session rows — ip/ua/meta_json omitted.</summary>
    public const string SelectCpWebTrackerRows = """
        SELECT `id`, IFNULL(`session_uid`,'') AS session_uid, IFNULL(`site_key`,'') AS site_key,
               IFNULL(`pageview_count`,0) AS pageview_count, IFNULL(`event_count`,0) AS event_count,
               IFNULL(`country_code`,'') AS country_code, IFNULL(`device_type`,'') AS device_type,
               IFNULL(`browser`,'') AS browser, IFNULL(`first_seen_at`,0) AS first_seen_at,
               IFNULL(`last_seen_at`,0) AS last_seen_at
        FROM `epc_web_tracker_sessions`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>
    /// Abandoned carts KPIs from shop_carts (PHP /CP/shop/orders/carts lists all cart lines).
    /// Guest/session carts use session_id != 0; authenticated lines use session_id = 0.
    /// </summary>
    public const string SelectCpAbandonedCartsStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_carts`) AS line_count,
            (SELECT COUNT(*) FROM `shop_carts` WHERE IFNULL(`session_id`,0) != 0) AS guest_line_count,
            (SELECT COUNT(*) FROM `shop_carts` WHERE IFNULL(`session_id`,0) = 0 AND IFNULL(`user_id`,0) > 0) AS user_line_count,
            (SELECT COUNT(DISTINCT `session_id`) FROM `shop_carts` WHERE IFNULL(`session_id`,0) != 0) AS guest_session_count,
            (SELECT COUNT(DISTINCT `user_id`) FROM `shop_carts` WHERE IFNULL(`session_id`,0) = 0 AND IFNULL(`user_id`,0) > 0) AS user_cart_count,
            (SELECT IFNULL(SUM(`price` * `count_need`), 0) FROM `shop_carts`) AS cart_sum
        """;

    /// <summary>
    /// Abandoned cart lines (read-only subset of carts.php). Deletes/filters remain PHP.
    /// Prefer guest/session rows first, then authenticated user carts.
    /// </summary>
    public const string SelectCpAbandonedCartsRows = """
        SELECT `id`, IFNULL(`user_id`,0) AS user_id, IFNULL(`session_id`,0) AS session_id,
               IFNULL(`price`,0) AS price, IFNULL(`count_need`,0) AS count_need,
               IFNULL(`checked_for_order`,0) AS checked_for_order, IFNULL(`product_type`,0) AS product_type,
               IFNULL(`t2_manufacturer`,'') AS manufacturer, IFNULL(`t2_article`,'') AS article,
               IFNULL(`t2_name`,'') AS name, IFNULL(`time`,0) AS time,
               CAST(IFNULL(`price`,0) * IFNULL(`count_need`,0) AS DECIMAL(20,2)) AS price_sum
        FROM `shop_carts`
        ORDER BY CASE WHEN IFNULL(`session_id`,0) != 0 THEN 0 ELSE 1 END ASC, `id` DESC
        LIMIT @limit
        """;

    /// <summary>Customer quote list (PHP <c>my_quotes.php</c>).</summary>
    public const string SelectStorefrontCustomerQuotes = """
        SELECT q.`id`, IFNULL(q.`status`,'') AS status,
               IFNULL(q.`time_created`,0) AS time_created,
               IFNULL(q.`time_updated`,0) AS time_updated,
               (SELECT COUNT(*) FROM `shop_quote_items` i WHERE i.`quote_id` = q.`id`) AS item_count
        FROM `shop_quote_requests` q
        WHERE q.`user_id` = @userId
        ORDER BY q.`id` DESC
        LIMIT @limit
        """;

    /// <summary>Customer quote header by id.</summary>
    public const string SelectStorefrontCustomerQuoteHeader = """
        SELECT `id`, IFNULL(`status`,'') AS status
        FROM `shop_quote_requests`
        WHERE `id` = @quoteId AND `user_id` = @userId
        LIMIT 1
        """;

    /// <summary>Quote lines for a customer quote (product fields live in product_object_json).</summary>
    public const string SelectStorefrontCustomerQuoteItems = """
        SELECT `id`,
               IFNULL(`product_object_json`,'') AS product_object_json,
               IFNULL(`count_need`, 0) AS count_need,
               IFNULL(`quoted_price`, 0) AS quoted_price,
               IFNULL(`offer_alternative`, 0) AS offer_alternative,
               IFNULL(`alt_manufacturer`,'') AS alt_manufacturer,
               IFNULL(`alt_article`,'') AS alt_article,
               IFNULL(`alt_name`,'') AS alt_name,
               IFNULL(`alt_count_need`, 0) AS alt_count_need,
               IFNULL(`alt_quoted_price`, 0) AS alt_quoted_price
        FROM `shop_quote_items`
        WHERE `quote_id` = @quoteId
        ORDER BY `id` ASC
        LIMIT 500
        """;

    /// <summary>
    /// Published own-catalogue categories for storefront mega menu / own-catalog-app
    /// (PHP <c>get_catalogue_tree.php</c> + <c>dp_menu.php</c>).
    /// </summary>
    /// <summary>Own-catalogue tree (raw lang id in <c>value</c>). Labels via LabelFor / optional translated query.</summary>
    public const string SelectStorefrontCatalogueCategories = """
        SELECT `id`, IFNULL(`alias`,'') AS alias, IFNULL(`url`,'') AS url,
               IFNULL(`parent`,0) AS parent, IFNULL(`level`,0) AS level,
               IFNULL(`count`,0) AS child_count, IFNULL(`order`,0) AS sort_order,
               IFNULL(`image`,'') AS image, IFNULL(`published_flag`,0) AS published_flag,
               IFNULL(`value`,0) AS value_lang_id,
               '' AS value_translated
        FROM `shop_catalogue_categories`
        WHERE IFNULL(`published_flag`,0) = 1
        ORDER BY `level` ASC, `order` ASC, `id` ASC
        LIMIT 5000
        """;

    /// <summary>Same tree with PHP <c>translate_str_by_id</c> caption from lang_text_strings_translation.</summary>
    public const string SelectStorefrontCatalogueCategoriesTranslated = """
        SELECT c.`id`, IFNULL(c.`alias`,'') AS alias, IFNULL(c.`url`,'') AS url,
               IFNULL(c.`parent`,0) AS parent, IFNULL(c.`level`,0) AS level,
               IFNULL(c.`count`,0) AS child_count, IFNULL(c.`order`,0) AS sort_order,
               IFNULL(c.`image`,'') AS image, IFNULL(c.`published_flag`,0) AS published_flag,
               IFNULL(c.`value`,0) AS value_lang_id,
               IFNULL((
                   SELECT NULLIF(TRIM(t.`value`), '')
                   FROM `lang_text_strings_translation` t
                   WHERE t.`str_key` = CAST(c.`value` AS CHAR)
                   ORDER BY CASE WHEN t.`lang_code` = 'en' THEN 0 ELSE 1 END, t.`lang_code`
                   LIMIT 1
               ), '') AS value_translated
        FROM `shop_catalogue_categories` c
        WHERE IFNULL(c.`published_flag`,0) = 1
        ORDER BY c.`level` ASC, c.`order` ASC, c.`id` ASC
        LIMIT 5000
        """;

    /// <summary>Own-catalogue products in a category (PHP <c>catalogue_for_customer</c> / name search).</summary>
    public const string SelectStorefrontCatalogueProductsByCategory = """
        SELECT p.`id`, IFNULL(p.`caption`,'') AS caption, IFNULL(p.`alias`,'') AS alias,
               IFNULL(p.`category_id`,0) AS category_id, IFNULL(p.`published`,0) AS published,
               IFNULL(sku.`brand`,'') AS manufacturer, IFNULL(sku.`article`,'') AS article,
               IFNULL(sku.`title`,'') AS description, IFNULL(sku.`id`,0) AS sku_profile_id
        FROM `shop_catalogue_products` p
        LEFT JOIN `epc_sku_profiles` sku
          ON sku.`id` = (
              SELECT s2.`id` FROM `epc_sku_profiles` s2
              WHERE s2.`product_id` = p.`id`
                AND IFNULL(s2.`status`,'') NOT IN ('hidden','draft')
              ORDER BY s2.`id` DESC LIMIT 1
          )
        WHERE p.`category_id` = @categoryId
          AND IFNULL(p.`published`, 1) = 1
          AND (@search = '' OR LOWER(IFNULL(p.`caption`,'')) LIKE CONCAT('%', @search, '%')
               OR LOWER(IFNULL(p.`alias`,'')) LIKE CONCAT('%', @search, '%')
               OR LOWER(IFNULL(sku.`article`,'')) LIKE CONCAT('%', @search, '%')
               OR LOWER(IFNULL(sku.`brand`,'')) LIKE CONCAT('%', @search, '%'))
        ORDER BY p.`id` DESC
        LIMIT @limit
        """;

    /// <summary>Own-catalogue name search across published products (PHP <c>/en/shop/search</c>).</summary>
    public const string SelectStorefrontCatalogueProductsByName = """
        SELECT p.`id`, IFNULL(p.`caption`,'') AS caption, IFNULL(p.`alias`,'') AS alias,
               IFNULL(p.`category_id`,0) AS category_id, IFNULL(p.`published`,0) AS published,
               IFNULL(sku.`brand`,'') AS manufacturer, IFNULL(sku.`article`,'') AS article,
               IFNULL(sku.`title`,'') AS description, IFNULL(sku.`id`,0) AS sku_profile_id
        FROM `shop_catalogue_products` p
        LEFT JOIN `epc_sku_profiles` sku
          ON sku.`id` = (
              SELECT s2.`id` FROM `epc_sku_profiles` s2
              WHERE s2.`product_id` = p.`id`
                AND IFNULL(s2.`status`,'') NOT IN ('hidden','draft')
              ORDER BY s2.`id` DESC LIMIT 1
          )
        WHERE IFNULL(p.`published`, 1) = 1
          AND @search != ''
          AND (LOWER(IFNULL(p.`caption`,'')) LIKE CONCAT('%', @search, '%')
               OR LOWER(IFNULL(p.`alias`,'')) LIKE CONCAT('%', @search, '%')
               OR LOWER(IFNULL(sku.`article`,'')) LIKE CONCAT('%', @search, '%')
               OR LOWER(IFNULL(sku.`brand`,'')) LIKE CONCAT('%', @search, '%'))
        ORDER BY p.`id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_price_attr_search</c> against <c>epc_price_attr_index</c>.</summary>
    public const string SelectStorefrontWarehouseAttrIndex = """
        SELECT `price_data_id`, `price_id`, `field_key`, `value_raw`, `value_norm`,
               `manufacturer`, `article`, `article_show`, `name`
        FROM `epc_price_attr_index`
        WHERE `value_norm` LIKE @like
          AND (@field = '' OR @field = 'all' OR `field_key` = @field)
        ORDER BY
          CASE WHEN `value_norm` = @norm THEN 0 WHEN `value_norm` LIKE @like THEN 1 ELSE 2 END,
          `manufacturer` ASC, `article_show` ASC
        LIMIT @limit
        """;

    /// <summary>Catalogue product card fields for wishlist/compare/product-app digests.</summary>
    public const string SelectStorefrontProductById = """
        SELECT p.`id`, IFNULL(p.`caption`,'') AS caption, IFNULL(p.`alias`,'') AS alias,
               IFNULL(p.`category_id`,0) AS category_id, IFNULL(p.`published`,0) AS published,
               IFNULL(sku.`brand`,'') AS manufacturer, IFNULL(sku.`article`,'') AS article,
               IFNULL(sku.`title`,'') AS description, IFNULL(sku.`id`,0) AS sku_profile_id
        FROM `shop_catalogue_products` p
        LEFT JOIN `epc_sku_profiles` sku
          ON sku.`product_id` = p.`id`
         AND IFNULL(sku.`status`,'') NOT IN ('hidden','draft')
        WHERE p.`id` = @productId
        ORDER BY sku.`id` DESC
        LIMIT 1
        """;

    /// <summary>Catalogue products by id list (wishlist/compare cookies).</summary>
    public const string SelectStorefrontProductsByIds = """
        SELECT p.`id`, IFNULL(p.`caption`,'') AS caption, IFNULL(p.`alias`,'') AS alias,
               IFNULL(p.`category_id`,0) AS category_id, IFNULL(p.`published`,0) AS published,
               IFNULL(sku.`brand`,'') AS manufacturer, IFNULL(sku.`article`,'') AS article,
               IFNULL(sku.`title`,'') AS description, IFNULL(sku.`id`,0) AS sku_profile_id
        FROM `shop_catalogue_products` p
        LEFT JOIN `epc_sku_profiles` sku
          ON sku.`id` = (
              SELECT s2.`id` FROM `epc_sku_profiles` s2
              WHERE s2.`product_id` = p.`id`
                AND IFNULL(s2.`status`,'') NOT IN ('hidden','draft')
              ORDER BY s2.`id` DESC LIMIT 1
          )
        WHERE p.`id` IN ({IDS})
        ORDER BY FIELD(p.`id`, {IDS})
        """;

    /// <summary>Legacy catalogue gallery (PHP printProduct_Info shop_products_images).</summary>
    public const string SelectStorefrontProductImages = """
        SELECT `id`, IFNULL(`file_name`,'') AS file_name
        FROM `shop_products_images`
        WHERE `product_id` = @productId
        ORDER BY `id` ASC
        LIMIT 40
        """;

    /// <summary>PHP <c>epc_sku_media_find_profile</c> by brand + article_key (active only).</summary>
    public const string SelectStorefrontSkuProfileByBrandArticle = """
        SELECT `id`
        FROM `epc_sku_profiles`
        WHERE UPPER(`brand`) = @brand
          AND `article_key` = @articleKey
          AND IFNULL(`status`,'active') NOT IN ('hidden','draft')
        ORDER BY `id` DESC
        LIMIT 1
        """;

    /// <summary>SKU media gallery (PHP epc_sku_photos).</summary>
    public const string SelectStorefrontSkuPhotos = """
        SELECT `id`, IFNULL(`file_name`,'') AS file_name, IFNULL(`alt`,'') AS alt,
               IFNULL(`caption`,'') AS caption, IFNULL(`is_primary`,0) AS is_primary
        FROM `epc_sku_photos`
        WHERE `profile_id` = @profileId
        ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC
        LIMIT 40
        """;

    /// <summary>SKU specification rows (PHP epc_sku_spec_rows + groups).</summary>
    public const string SelectStorefrontSkuSpecs = """
        SELECT IFNULL(g.`name`,'Specifications') AS group_name,
               IFNULL(r.`label`,'') AS label,
               IFNULL(r.`value`,'') AS value,
               IFNULL(r.`unit`,'') AS unit,
               IFNULL(r.`value_type`,'text') AS value_type
        FROM `epc_sku_spec_rows` r
        LEFT JOIN `epc_sku_spec_groups` g ON g.`id` = r.`group_id`
        WHERE r.`profile_id` = @profileId
        ORDER BY IFNULL(g.`sort_order`,0) ASC, r.`sort_order` ASC, r.`id` ASC
        LIMIT 200
        """;

    /// <summary>Genuine OE manufacturer keys from UMAPI passenger/commercial/motorbike sections.</summary>
    public const string SelectStorefrontGenuineManufacturerNames = """
        SELECT DISTINCT IFNULL(`manufacturer`,'') AS name
        FROM `epc_umapi_manufacturers`
        WHERE `section` IN ('passenger','commercial','motorbike')
          AND IFNULL(`manufacturer`,'') <> ''
        LIMIT 5000
        """;

    /// <summary>Manufacturer synonym expansions for genuine brand matching.</summary>
    public const string SelectStorefrontManufacturerSynonyms = """
        SELECT IFNULL(m.`name`,'') AS name, IFNULL(s.`synonym`,'') AS synonym
        FROM `shop_docpart_manufacturers` m
        INNER JOIN `shop_docpart_manufacturers_synonyms` s ON s.`manufacturer_id` = m.`id`
        WHERE IFNULL(m.`name`,'') <> '' AND IFNULL(s.`synonym`,'') <> ''
        LIMIT 20000
        """;

    /// <summary>Office↔storage bunches for progressive supplier poll (PHP office_storage_bunches).</summary>
    public const string SelectStorefrontOfficeStorageBunches = """
        SELECT DISTINCT m.`office_id`, m.`storage_id`,
               IFNULL(t.`handler_folder`,'') AS handler_folder
        FROM `shop_offices_storages_map` m
        INNER JOIN `shop_storages` s ON s.`id` = m.`storage_id`
        LEFT JOIN `shop_storages_interfaces_types` t ON t.`id` = s.`interface_type`
        WHERE IFNULL(s.`hidden`,0) = 0
          AND IFNULL(s.`interface_type`,0) > 1
        ORDER BY m.`office_id` ASC, m.`storage_id` ASC
        LIMIT 500
        """;

    /// <summary>
    /// PHP part_search fallback: all active price-list storages when office maps omit prices.
    /// </summary>
    public const string SelectStorefrontPriceStorageFallback = """
        SELECT 1 AS office_id, s.`id` AS storage_id, 'prices' AS handler_folder
        FROM `shop_storages` s
        INNER JOIN `shop_storages_interfaces_types` t ON t.`id` = s.`interface_type`
        WHERE IFNULL(s.`hidden`,0) = 0
          AND IFNULL(t.`handler_folder`,'') = 'prices'
        ORDER BY s.`id` ASC
        LIMIT 500
        """;

    /// <summary>Customer bulk-upload history (PHP epc_bulk_upload_history).</summary>
    public const string SelectStorefrontBulkUploadHistory = """
        SELECT `id`, IFNULL(`file_name`,'') AS file_name, IFNULL(`priority`,'') AS priority,
               IFNULL(`uploaded_count`,0) AS uploaded_count, IFNULL(`available_count`,0) AS available_count,
               IFNULL(`cross_count`,0) AS cross_count, IFNULL(`short_count`,0) AS short_count,
               IFNULL(`notfound_count`,0) AS notfound_count,
               IFNULL(`created_at`,'') AS created_at, IFNULL(`updated_at`,'') AS updated_at
        FROM `epc_bulk_upload_history`
        WHERE `user_id` = @userId
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Quote request KPIs — status stages are distinct in PHP (draft→submitted→quoted→accepted).</summary>
    public const string SelectCpQuoteRequestsStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_quote_requests`) AS quote_count,
            (SELECT COUNT(*) FROM `shop_quote_requests` WHERE IFNULL(`status`,'')='draft') AS draft_count,
            (SELECT COUNT(*) FROM `shop_quote_requests` WHERE IFNULL(`status`,'')='submitted') AS submitted_count,
            (SELECT COUNT(*) FROM `shop_quote_requests` WHERE IFNULL(`status`,'')='quoted') AS quoted_count,
            (SELECT COUNT(*) FROM `shop_quote_requests` WHERE IFNULL(`status`,'')='accepted') AS accepted_count,
            (SELECT COUNT(*) FROM `shop_quote_items`) AS item_count
        """;

    /// <summary>Quote request rows — admin_note/customer_note omitted.</summary>
    public const string SelectCpQuoteRequestsRows = """
        SELECT `id`, IFNULL(`user_id`,0) AS user_id, IFNULL(`session_id`,0) AS session_id,
               IFNULL(`status`,'') AS status, IFNULL(`time_created`,0) AS time_created,
               IFNULL(`time_updated`,0) AS time_updated, IFNULL(`time_submitted`,0) AS time_submitted,
               IFNULL(`accepted_order_id`,0) AS accepted_order_id
        FROM `shop_quote_requests`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Platform communication KPIs from epc_platform_comm_settings + internal_tasks (CREATE TABLE in epc_super_cp_platform.php).</summary>
    public const string SelectCpPlatformCommunicationStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_platform_comm_settings`) AS setting_count,
            (SELECT COUNT(*) FROM `epc_platform_internal_tasks`) AS task_count,
            (SELECT COUNT(*) FROM `epc_platform_internal_tasks` WHERE IFNULL(`status`,'')='open') AS open_task_count,
            (SELECT COUNT(*) FROM `epc_platform_internal_tasks` WHERE IFNULL(`priority`,'') IN ('high','urgent')) AS high_priority_count
        """;

    /// <summary>Platform communication task rows — description omitted.</summary>
    public const string SelectCpPlatformCommunicationRows = """
        SELECT `id`, IFNULL(`title`,'') AS title, IFNULL(`assigned_to`,0) AS assigned_to,
               IFNULL(`site_key`,'') AS site_key, IFNULL(`category`,'') AS category,
               IFNULL(`status`,'') AS status, IFNULL(`priority`,'') AS priority,
               IFNULL(`due_at`,0) AS due_at, IFNULL(`created_at`,0) AS created_at
        FROM `epc_platform_internal_tasks`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Info blocks KPIs from epc_platform_info_blocks (CREATE TABLE in epc_super_cp_platform.php).</summary>
    public const string SelectCpInfoBlocksStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_platform_info_blocks`) AS block_count,
            (SELECT COUNT(*) FROM `epc_platform_info_blocks` WHERE IFNULL(`active`,0)=1) AS active_count,
            (SELECT COUNT(DISTINCT `placement`) FROM `epc_platform_info_blocks`) AS placement_count,
            (SELECT COUNT(DISTINCT `locale`) FROM `epc_platform_info_blocks`) AS locale_count
        """;

    /// <summary>Info block rows — content_html omitted.</summary>
    public const string SelectCpInfoBlocksRows = """
        SELECT `id`, IFNULL(`block_key`,'') AS block_key, IFNULL(`title`,'') AS title,
               IFNULL(`scope`,'') AS scope, IFNULL(`site_key`,'') AS site_key,
               IFNULL(`placement`,'') AS placement, IFNULL(`locale`,'') AS locale,
               IFNULL(`active`,0) AS active, IFNULL(`sort_order`,0) AS sort_order,
               IFNULL(`updated_at`,0) AS updated_at
        FROM `epc_platform_info_blocks`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Free tools KPIs — active = seen in last 30 days (matches PHP epc_ecomae_free_tools.php admin KPI).</summary>
    public const string SelectCpFreeToolsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_free_tool_accounts`) AS account_count,
            (SELECT COUNT(*) FROM `epc_free_tool_saves`) AS save_count,
            (SELECT COUNT(*) FROM `epc_free_tool_settings`) AS setting_count,
            (SELECT COUNT(*) FROM `epc_free_tool_accounts` WHERE IFNULL(`time_last_seen`,0) >= UNIX_TIMESTAMP() - 30*86400) AS active_account_count
        """;

    /// <summary>Free tool account rows — token/pass_hash/del_code_hash/payload omitted.</summary>
    public const string SelectCpFreeToolsRows = """
        SELECT `id`, IFNULL(`email`,'') AS email, IFNULL(`company`,'') AS company,
               IFNULL(`country`,'') AS country, IFNULL(`use_count`,0) AS use_count,
               IFNULL(`login_count`,0) AS login_count, IFNULL(`time_created`,0) AS time_created,
               IFNULL(`time_last_seen`,0) AS time_last_seen
        FROM `epc_free_tool_accounts`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Config sandbox KPIs from epc_config_snapshots/changes (CREATE TABLE in epc_config_sandbox.php).</summary>
    public const string SelectCpConfigSandboxStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_config_snapshots`) AS snapshot_count,
            (SELECT COUNT(*) FROM `epc_config_snapshots` WHERE IFNULL(`status`,'')='active') AS active_snapshot_count,
            (SELECT COUNT(*) FROM `epc_config_snapshots` WHERE IFNULL(`status`,'')='promoted') AS promoted_snapshot_count,
            (SELECT COUNT(*) FROM `epc_sandbox_changes`) AS change_count
        """;

    /// <summary>Config sandbox snapshot rows — config_data omitted.</summary>
    public const string SelectCpConfigSandboxRows = """
        SELECT `id`, IFNULL(`site_key`,'') AS site_key, IFNULL(`snapshot_name`,'') AS snapshot_name,
               IFNULL(`status`,'') AS status, IFNULL(`created_by`,0) AS created_by,
               IFNULL(CAST(`created_at` AS CHAR),'') AS created_at,
               IFNULL(CAST(`promoted_at` AS CHAR),'') AS promoted_at
        FROM `epc_config_snapshots`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Marketplace portal KPIs from epc_marketplace_* (CREATE TABLE in epc_marketplace.php).</summary>
    public const string SelectCpMarketplaceAppsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_marketplace_apps`) AS app_count,
            (SELECT COUNT(*) FROM `epc_marketplace_apps` WHERE IFNULL(`status`,'')='published') AS published_count,
            (SELECT COUNT(*) FROM `epc_marketplace_installs`) AS install_count,
            (SELECT COUNT(*) FROM `epc_marketplace_installs` WHERE IFNULL(`status`,'')='active') AS active_install_count,
            (SELECT COUNT(*) FROM `epc_marketplace_reviews`) AS review_count
        """;

    /// <summary>Marketplace app rows — description/features/requirements/screenshots/config/review_text omitted.</summary>
    public const string SelectCpMarketplaceAppsRows = """
        SELECT `id`, IFNULL(`app_key`,'') AS app_key, IFNULL(`name`,'') AS name,
               IFNULL(`short_desc`,'') AS short_desc, IFNULL(`category`,'') AS category,
               IFNULL(`developer`,'') AS developer, IFNULL(`version`,'') AS version,
               IFNULL(`pricing`,'') AS pricing, IFNULL(`price_monthly`,0) AS price_monthly,
               IFNULL(`downloads`,0) AS downloads, IFNULL(`avg_rating`,0) AS avg_rating,
               IFNULL(`review_count`,0) AS review_count, IFNULL(`status`,'') AS status,
               IFNULL(CAST(`published_at` AS CHAR),'') AS published_at
        FROM `epc_marketplace_apps`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Notifications KPIs from epc_notifications/prefs (CREATE TABLE in epc_notifications.php).</summary>
    public const string SelectCpNotificationsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_notifications`) AS notification_count,
            (SELECT COUNT(*) FROM `epc_notifications` WHERE IFNULL(`is_read`,0)=0) AS unread_count,
            (SELECT COUNT(*) FROM `epc_notification_prefs`) AS pref_count,
            (SELECT COUNT(DISTINCT `channel`) FROM `epc_notifications`) AS channel_count
        """;

    /// <summary>Notification rows — body/metadata/action_url omitted.</summary>
    public const string SelectCpNotificationsRows = """
        SELECT `id`, IFNULL(`tenant_key`,'') AS tenant_key, IFNULL(`user_id`,0) AS user_id,
               IFNULL(`channel`,'') AS channel, IFNULL(`category`,'') AS category,
               IFNULL(`severity`,'') AS severity, IFNULL(`title`,'') AS title,
               IFNULL(`is_read`,0) AS is_read, IFNULL(CAST(`created_at` AS CHAR),'') AS created_at
        FROM `epc_notifications`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Portal site settings KPIs (CREATE in content/general_pages/epc_portal_db.php).</summary>
    public const string SelectCpPortalSettingsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_portal_site_settings`) AS site_count,
            (SELECT COUNT(DISTINCT IFNULL(`industry_code`,'')) FROM `epc_portal_site_settings`) AS industry_count,
            (SELECT COUNT(DISTINCT IFNULL(`access_mode`,'')) FROM `epc_portal_site_settings`) AS access_mode_count,
            (SELECT COUNT(*) FROM `epc_portal_deploy_targets` WHERE IFNULL(`active`,0)=1) AS deploy_target_count
        """;

    /// <summary>Portal site settings rows — contact_json/enabled_packs_json/theme_json/cp_menu_json/erp_modules_json omitted.</summary>
    public const string SelectCpPortalSettingsRows = """
        SELECT IFNULL(`host`,'') AS host, IFNULL(`industry_code`,'') AS industry_code,
               IFNULL(`system_name`,'') AS system_name, IFNULL(`hub_name`,'') AS hub_name,
               IFNULL(`tagline`,'') AS tagline, IFNULL(`domain_path`,'') AS domain_path,
               IFNULL(`theme_template`,'') AS theme_template, IFNULL(`access_mode`,'') AS access_mode,
               IFNULL(`cp_default_lang`,'') AS cp_default_lang, IFNULL(`country_code`,'') AS country_code,
               IFNULL(`updated_at`,0) AS updated_at
        FROM `epc_portal_site_settings`
        ORDER BY `updated_at` DESC, `host` ASC
        LIMIT @limit
        """;

    /// <summary>Data migration KPIs from epc_data_migrations + epc_data_migration_rows (CREATE in epc_erp_data_migration.php).</summary>
    public const string SelectCpDataMigrationsStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_data_migrations`) AS migration_count,
            (SELECT COUNT(*) FROM `epc_data_migrations` WHERE IFNULL(`status`,'')='completed') AS completed_count,
            (SELECT COUNT(*) FROM `epc_data_migrations` WHERE IFNULL(`status`,'') IN ('failed','rolled_back')) AS failed_count,
            (SELECT COUNT(*) FROM `epc_data_migration_rows`) AS row_count
        """;

    /// <summary>Data migration rows — file_path/column_mapping/validation_errors/options/raw_data/mapped_data omitted.</summary>
    public const string SelectCpDataMigrationsRows = """
        SELECT `id`, IFNULL(`company_id`,0) AS company_id, IFNULL(`migration_type`,'') AS migration_type,
               IFNULL(`entity_type`,'') AS entity_type, IFNULL(`file_name`,'') AS file_name,
               IFNULL(`total_rows`,0) AS total_rows, IFNULL(`valid_rows`,0) AS valid_rows,
               IFNULL(`error_rows`,0) AS error_rows, IFNULL(`imported_rows`,0) AS imported_rows,
               IFNULL(`status`,'') AS status, IFNULL(`imported_by_name`,'') AS imported_by_name,
               IFNULL(`time_created`,0) AS time_created, IFNULL(`time_completed`,0) AS time_completed
        FROM `epc_data_migrations`
        ORDER BY `id` DESC
        LIMIT @limit
        """;


    // ---- Wave 22 CMS/platform leftover digests ----
    public const string CountCpGeoRegionsNodeCount = "SELECT COUNT(*) FROM `shop_geo`";
    public const string CountCpGeoRegionsLevel1Count = "SELECT COUNT(*) FROM `shop_geo` WHERE IFNULL(`level`,0)=1";
    public const string CountCpGeoRegionsLevel2Count = "SELECT COUNT(*) FROM `shop_geo` WHERE IFNULL(`level`,0)=2";
    public const string CountCpGeoRegionsMappedOfficeCount = "SELECT COUNT(DISTINCT `office_id`) FROM `shop_offices_geo_map`";

    /// <summary>Wave 22 geo-regions rows — raw lang string bodies; value stored as lang id.</summary>
    public const string SelectCpGeoRegionsRows = """
        SELECT `id`, IFNULL(`level`,0) AS level, IFNULL(`parent`,0) AS parent,
        IFNULL(`order`,0) AS sort_order, IFNULL(`count`,0) AS child_count,
        IFNULL(`value`,0) AS value_lang_id
        FROM `shop_geo`
        ORDER BY `level` ASC, `order` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>Wave 22 product-filters KPIs (shop_docpart_filter).</summary>
    public const string SelectCpProductFiltersStats = """
        SELECT
        COUNT(*) AS filter_count,
        SUM(CASE WHEN IFNULL(`list_storages`,'') NOT IN ('','[]','null') THEN 1 ELSE 0 END) AS with_storage_scope,
        SUM(CASE WHEN IFNULL(`min_price`,0)>0 OR IFNULL(`max_price`,0)>0 THEN 1 ELSE 0 END) AS with_price_band,
        SUM(CASE WHEN IFNULL(`min_time`,0)>0 OR IFNULL(`max_time`,0)>0 THEN 1 ELSE 0 END) AS with_time_band
        FROM `shop_docpart_filter`
        """;

    /// <summary>Wave 22 product-filters rows — list_storages JSON.</summary>
    public const string SelectCpProductFiltersRows = """
        SELECT `id`, IFNULL(`manufacturer`,'') AS manufacturer, IFNULL(`article`,'') AS article,
        IFNULL(`name`,'') AS name,
        IFNULL(`min_price`,0) AS min_price, IFNULL(`max_price`,0) AS max_price,
        IFNULL(`min_time`,0) AS min_time, IFNULL(`max_time`,0) AS max_time
        FROM `shop_docpart_filter`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Wave 22 search-tabs KPIs (shop_docpart_search_tabs).</summary>
    public const string SelectCpSearchTabsStats = """
        SELECT
        COUNT(*) AS tab_count,
        SUM(CASE WHEN IFNULL(`enabled`,0)=1 THEN 1 ELSE 0 END) AS enabled_count,
        SUM(CASE WHEN IFNULL(`enabled`,0)=0 THEN 1 ELSE 0 END) AS disabled_count,
        IFNULL(MAX(`order`),0) AS max_order
        FROM `shop_docpart_search_tabs`
        """;

    /// <summary>Wave 22 search-tabs rows — parameters_values JSON.</summary>
    public const string SelectCpSearchTabsRows = """
        SELECT `id`, IFNULL(`caption`,'') AS caption, IFNULL(`order`,0) AS sort_order,
        IFNULL(`enabled`,0) AS enabled
        FROM `shop_docpart_search_tabs`
        ORDER BY `order` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>Wave 22 system-requests KPIs (users_vin).</summary>
    public const string SelectCpSystemRequestsStats = """
        SELECT
        COUNT(*) AS request_count,
        SUM(CASE WHEN IFNULL(`viewed`,0)=0 THEN 1 ELSE 0 END) AS unviewed_count,
        SUM(CASE WHEN IFNULL(`viewed`,0)=1 THEN 1 ELSE 0 END) AS viewed_count,
        SUM(CASE WHEN IFNULL(`user_id`,0)>0 THEN 1 ELSE 0 END) AS with_user_count
        FROM `users_vin`
        """;

    /// <summary>Wave 22 system-requests rows — VIN request text body (injection-prone PHP cookie filters not ported).</summary>
    public const string SelectCpSystemRequestsRows = """
        SELECT `id`, IFNULL(`time`,0) AS time_unix, IFNULL(`user_id`,0) AS user_id,
        IFNULL(`viewed`,0) AS viewed
        FROM `users_vin`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>Wave 22 additional-texts KPIs (text_for_url).</summary>
    public const string SelectCpAdditionalTextsStats = """
        SELECT
        COUNT(*) AS text_count,
        SUM(CASE WHEN IFNULL(`before_main`,0)=1 THEN 1 ELSE 0 END) AS before_main_count,
        SUM(CASE WHEN IFNULL(`title_tag`,'')!='' THEN 1 ELSE 0 END) AS with_title_count,
        SUM(CASE WHEN IFNULL(`description_tag`,'')!='' THEN 1 ELSE 0 END) AS with_description_count
        FROM `text_for_url`
        """;

    /// <summary>Wave 22 additional-texts rows — content HTML + description_tag bodies in rows (title/keywords only).</summary>
    public const string SelectCpAdditionalTextsRows = """
        SELECT `id`, IFNULL(`url`,'') AS url, IFNULL(`before_main`,0) AS before_main,
        IFNULL(`title_tag`,'') AS title_tag, IFNULL(`keywords_tag`,'') AS keywords_tag
        FROM `text_for_url`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string CountCpSliderBannersImageCount = "SELECT COUNT(*) FROM `slider_images`";
    public const string CountCpSliderBannersConnected = "SELECT IFNULL(`connected`,0) FROM `slider_setings` LIMIT 1";
    public const string CountCpSliderBannersCntImg = "SELECT IFNULL(`cnt_img`,0) FROM `slider_setings` LIMIT 1";
    public const string CountCpSliderBannersCntImgNext = "SELECT IFNULL(`cnt_img_next`,0) FROM `slider_setings` LIMIT 1";

    /// <summary>Wave 22 slider-banners rows — none critical (paths only).</summary>
    public const string SelectCpSliderBannersRows = """
        SELECT `id`, IFNULL(`orders`,0) AS sort_order, IFNULL(`link`,'') AS link,
        IFNULL(`href`,'') AS href
        FROM `slider_images`
        ORDER BY `orders` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>Wave 22 structure-dumps KPIs (content_structure_dumps).</summary>
    public const string SelectCpStructureDumpsStats = """
        SELECT
        COUNT(*) AS dump_count,
        IFNULL(SUM(`records_count`),0) AS total_records,
        IFNULL(MAX(`time_created`),0) AS latest_time_created,
        SUM(CASE WHEN IFNULL(`file_name`,'')!='' THEN 1 ELSE 0 END) AS with_file_count
        FROM `content_structure_dumps`
        """;

    /// <summary>Wave 22 structure-dumps rows — dump file bodies.</summary>
    public const string SelectCpStructureDumpsRows = """
        SELECT `id`, IFNULL(`time_created`,0) AS time_created, IFNULL(`fields_in_dump`,'') AS fields_in_dump,
        IFNULL(`file_name`,'') AS file_name, IFNULL(`records_count`,0) AS records_count
        FROM `content_structure_dumps`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string CountCpCommunicationsTestSmsActiveCount = "SELECT COUNT(*) FROM `sms_api` WHERE IFNULL(`active`,0)=1";
    public const string CountCpCommunicationsTestSmsTotalCount = "SELECT COUNT(*) FROM `sms_api`";

    public const string SelectCpCommunicationsTestEmailLastStatus = "SELECT IFNULL(`status`,'') FROM `debug_results` WHERE `name`='email' ORDER BY `time` DESC LIMIT 1";
    public const string SelectCpCommunicationsTestSmsLastStatus = "SELECT IFNULL(`status`,'') FROM `debug_results` WHERE `name`='sms' ORDER BY `time` DESC LIMIT 1";

    /// <summary>Wave 22 communications-test rows — debug_result blobs + sms parameters_values secrets.</summary>
    public const string SelectCpCommunicationsTestRows = """
        SELECT IFNULL(`name`,'') AS name, IFNULL(`active`,0) AS active,
        IFNULL(`is_selectable`,0) AS is_selectable, IFNULL(`handler`,'') AS handler
        FROM `sms_api`
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    /// <summary>Wave 22 languages KPIs (lang_languages).</summary>
    public const string SelectCpLanguagesStats = """
        SELECT
        COUNT(*) AS language_count,
        SUM(CASE WHEN IFNULL(`active`,0)=1 THEN 1 ELSE 0 END) AS active_count,
        SUM(CASE WHEN IFNULL(`is_default`,0)=1 THEN 1 ELSE 0 END) AS default_count,
        SUM(CASE WHEN IFNULL(`active`,0)=0 THEN 1 ELSE 0 END) AS inactive_count
        FROM `lang_languages`
        """;

    /// <summary>Wave 22 languages rows — translation string bodies.</summary>
    public const string SelectCpLanguagesRows = """
        SELECT IFNULL(`lang_code`,'') AS lang_code, IFNULL(`active`,0) AS active,
        IFNULL(`is_default`,0) AS is_default
        FROM `lang_languages`
        ORDER BY `is_default` DESC, `active` DESC, `lang_code` ASC
        LIMIT @limit
        """;

    /// <summary>Wave 22 plugins-manager KPIs (plugins).</summary>
    public const string SelectCpPluginsManagerStats = """
        SELECT
        COUNT(*) AS plugin_count,
        SUM(CASE WHEN IFNULL(`activated`,0)=1 THEN 1 ELSE 0 END) AS activated_count,
        SUM(CASE WHEN IFNULL(`is_frontend`,0)=1 THEN 1 ELSE 0 END) AS frontend_count,
        SUM(CASE WHEN IFNULL(`control_lock`,0)=1 THEN 1 ELSE 0 END) AS locked_count
        FROM `plugins`
        """;

    /// <summary>Wave 22 plugins-manager rows — data_value JSON + filesystem delete side-effects.</summary>
    public const string SelectCpPluginsManagerRows = """
        SELECT `id`, IFNULL(`caption`,'') AS caption, IFNULL(`order`,0) AS sort_order,
        IFNULL(`activated`,0) AS activated, IFNULL(`is_frontend`,0) AS is_frontend,
        IFNULL(`control_lock`,0) AS control_lock
        FROM `plugins`
        ORDER BY `order` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>Wave 22 templates-manager KPIs (templates).</summary>
    public const string SelectCpTemplatesManagerStats = """
        SELECT
        COUNT(*) AS template_count,
        SUM(CASE WHEN IFNULL(`is_frontend`,0)=1 THEN 1 ELSE 0 END) AS frontend_count,
        SUM(CASE WHEN IFNULL(`is_frontend`,0)=1 AND IFNULL(`current`,0)=1 THEN 1 ELSE 0 END) AS current_frontend_count,
        SUM(CASE WHEN IFNULL(`is_frontend`,0)=0 AND IFNULL(`current`,0)=1 THEN 1 ELSE 0 END) AS current_backend_count
        FROM `templates`
        """;

    /// <summary>Wave 22 templates-manager rows — data_value JSON + FS delete.</summary>
    public const string SelectCpTemplatesManagerRows = """
        SELECT `id`, IFNULL(`caption`,'') AS caption, IFNULL(`name`,'') AS name,
        IFNULL(`current`,0) AS current_flag, IFNULL(`is_frontend`,0) AS is_frontend,
        IFNULL(`phone_support`,0) AS phone_support, IFNULL(`tablet_support`,0) AS tablet_support
        FROM `templates`
        ORDER BY `is_frontend` DESC, `current` DESC, `id` ASC
        LIMIT @limit
        """;

    public const string CountCpDesignTokensTokenCount = "SELECT COUNT(*) FROM `epc_settings` WHERE `setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login'";
    public const string CountCpDesignTokensTenantCount = "SELECT COUNT(DISTINCT IFNULL(`site_key`,'')) FROM `epc_settings` WHERE `setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login'";
    public const string CountCpDesignTokensWhiteLabelCount = "SELECT COUNT(*) FROM `epc_settings` WHERE `setting_key`='white_label_login' AND IFNULL(`setting_value`,'') NOT IN ('','0','false')";
    public const string CountCpDesignTokensUpdatedRecentCount = "SELECT COUNT(*) FROM `epc_settings` WHERE (`setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login') AND `updated_at` >= DATE_SUB(NOW(), INTERVAL 30 DAY)";

    /// <summary>Wave 22 design-tokens rows — setting_value (colors/URLs); ASP.NET also tolerates missing site_key via resilient KPIs.</summary>
    public const string SelectCpDesignTokensRows = """
        SELECT IFNULL(`site_key`,'') AS site_key, IFNULL(`setting_key`,'') AS setting_key,
        IFNULL(CAST(`updated_at` AS CHAR),'') AS updated_at
        FROM `epc_settings`
        WHERE `setting_key` LIKE 'brand_%' OR `setting_key`='white_label_login'
        ORDER BY `updated_at` DESC, `site_key` ASC, `setting_key` ASC
        LIMIT @limit
        """;

    public const string CountCpSitemapContentUrlCount = "SELECT COUNT(*) FROM `content` WHERE IFNULL(`alias`,'')!=''";
    public const string CountCpSitemapCategoryCount = "SELECT COUNT(*) FROM `shop_catalogue_categories`";
    public const string CountCpSitemapProductCount = "SELECT COUNT(*) FROM `shop_catalogue_products`";
    public const string CountCpSitemapFrontendContentCount = "SELECT COUNT(*) FROM `content` WHERE IFNULL(`is_frontend`,0)=1";

    /// <summary>Wave 22 sitemap rows — sitemap.xml file artifact (generation remains PHP); content HTML omitted.</summary>
    public const string SelectCpSitemapRows = """
        SELECT `id`, IFNULL(`alias`,'') AS alias, IFNULL(`value`,0) AS value_lang_id,
        IFNULL(`is_frontend`,0) AS is_frontend, IFNULL(`published_flag`,0) AS published_flag
        FROM `content`
        WHERE IFNULL(`is_frontend`,0)=1
        ORDER BY `id` DESC
        LIMIT @limit
        """;


    // ---- Wave 23 ops guides / remaining surfaces ----
public const string SelectCpOpsGuidesStats = """
        SELECT
            (SELECT COUNT(*) FROM `control_groups`) AS group_count,
            (SELECT COUNT(*) FROM `control_items`) AS item_count,
            (SELECT COUNT(*) FROM `control_items` WHERE IFNULL(`show_anyway`,0)=1) AS show_anyway_count,
            (SELECT COUNT(*) FROM `control_items` WHERE IFNULL(`url`,'')!='') AS url_item_count
        """;

    /// <summary>Wave 23 ops-guides rows — guide HTML omitted; caption may be lang id.</summary>
    public const string SelectCpOpsGuidesRows = """
        SELECT `id`, IFNULL(`items_group`,0) AS items_group, IFNULL(`caption`,'') AS caption,
               IFNULL(`url`,'') AS url, IFNULL(`show_anyway`,0) AS show_anyway, IFNULL(`order`,0) AS sort_order
        FROM `control_items`
        ORDER BY `order` ASC, `id` ASC
        LIMIT @limit
        """;

    /// <summary>
    /// On-premises license registry metadata — never selects notes, fingerprint, ip, or modules_json.
    /// License keys are masked in the reporter (not returned raw).
    /// </summary>
    public const string SelectOnPremisesLicenses = """
        SELECT `id`, IFNULL(`license_key`, '') AS license_key,
               IFNULL(`customer_name`, '') AS customer_name, IFNULL(`tier`, '') AS tier,
               IFNULL(`users_max`, 0) AS users_max, IFNULL(`status`, '') AS status,
               IFNULL(`hostname`, '') AS hostname,
               IFNULL(`issued_at`, 0) AS issued_at,
               IFNULL(`activated_at`, 0) AS activated_at,
               IFNULL(`last_seen_at`, 0) AS last_seen_at,
               IFNULL(`expires_at`, 0) AS expires_at
        FROM `epc_onprem_licenses`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>ERP delivery notes (notes/pdf omitted) — PHP epc_erp_delivery_notes.</summary>
    public const string SelectErpDeliveryNotes = """
        SELECT `id`, IFNULL(`note_no`,'') AS note_no, IFNULL(`order_id`,0) AS order_id,
               IFNULL(`carrier`,'') AS carrier, IFNULL(`tracking_no`,'') AS tracking_no,
               IFNULL(`status`,'') AS status, IFNULL(`shipped_at`,0) AS shipped_at,
               IFNULL(`delivered_at`,0) AS delivered_at, IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_delivery_notes`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>ERP supplier RFQs (description omitted) — PHP epc_erp_rfq.</summary>
    public const string SelectErpRfqs = """
        SELECT `id`, IFNULL(`rfq_no`,'') AS rfq_no, IFNULL(`supplier_id`,0) AS supplier_id,
               IFNULL(`title`,'') AS title, IFNULL(`amount_est`,0) AS amount_est,
               IFNULL(`currency_code`,'AED') AS currency_code, IFNULL(`status`,'') AS status,
               IFNULL(`due_date`,0) AS due_date, IFNULL(`order_id`,0) AS order_id,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_rfq`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>ERP three-way match rows — PHP epc_erp_three_way_match_rows.</summary>
    public const string SelectErpThreeWayMatch = """
        SELECT po.`id` AS po_id, IFNULL(po.`po_no`,'') AS po_no, IFNULL(po.`status`,'') AS po_status,
               IFNULL(po.`total_amount`,0) AS po_total,
               IFNULL(p.`id`,0) AS purchase_id, IFNULL(p.`invoice_number`,'') AS invoice_number,
               IFNULL(p.`total_amount`,0) AS invoice_total, IFNULL(p.`status`,'') AS purchase_status,
               (SELECT COUNT(*) FROM `epc_erp_po_receipts` r WHERE r.`po_id` = po.`id`) AS receipt_count
        FROM `epc_erp_purchase_orders` po
        LEFT JOIN `epc_erp_purchases` p ON p.`id` = po.`purchase_id` OR (po.`order_id` > 0 AND p.`order_id` = po.`order_id`)
        WHERE po.`status` IN ('approved', 'partial', 'received')
        ORDER BY po.`id` DESC
        LIMIT @limit
        """;

    /// <summary>ERP contacts (address/notes omitted) — PHP epc_erp_contacts.</summary>
    public const string SelectErpContacts = """
        SELECT `id`, IFNULL(`party_type`,'') AS party_type, IFNULL(`name`,'') AS name,
               IFNULL(`company`,'') AS company, IFNULL(`email`,'') AS email,
               IFNULL(`phone`,'') AS phone, IFNULL(`trn`,'') AS trn,
               IFNULL(`city`,'') AS city, IFNULL(`country_code`,'AE') AS country_code,
               IFNULL(`linked_user_id`,0) AS linked_user_id,
               IFNULL(`linked_supplier_id`,0) AS linked_supplier_id,
               IFNULL(`active`,1) AS active, IFNULL(`time_updated`,0) AS time_updated
        FROM `epc_erp_contacts`
        WHERE IFNULL(`active`,1) = 1
        ORDER BY `name` ASC
        LIMIT @limit
        """;

    /// <summary>ERP payment batches (notes omitted) — PHP epc_erp_payment_batches.</summary>
    public const string SelectErpPaymentBatches = """
        SELECT b.`id`, IFNULL(b.`batch_no`,'') AS batch_no, IFNULL(b.`batch_type`,'') AS batch_type,
               IFNULL(b.`account_id`,0) AS account_id, IFNULL(a.`name`,'') AS account_name,
               IFNULL(b.`total_amount`,0) AS total_amount, IFNULL(b.`line_count`,0) AS line_count,
               IFNULL(b.`status`,'') AS status, IFNULL(b.`execution_date`,0) AS execution_date,
               IFNULL(b.`time_updated`,0) AS time_updated
        FROM `epc_erp_payment_batches` b
        LEFT JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = b.`account_id`
        ORDER BY b.`time_updated` DESC
        LIMIT @limit
        """;

    /// <summary>ERP fiscal periods peek — PHP epc_erp_periods (period_close).</summary>
    public const string SelectErpFiscalPeriods = """
        SELECT `id`, IFNULL(`year_month`,'') AS year_month, IFNULL(`status`,'') AS status,
               CASE WHEN IFNULL(`status`,'') IN ('soft_close','locked') THEN 1 ELSE 0 END AS soft_closed,
               CASE WHEN IFNULL(`status`,'') = 'locked' THEN 1 ELSE 0 END AS locked,
               IFNULL(`updated_at`,0) AS time_updated
        FROM `epc_erp_periods`
        ORDER BY `year_month` DESC
        LIMIT @limit
        """;


    /// <summary>ERP agenda events (notes omitted) — PHP epc_erp_agenda_events.</summary>
    public const string SelectErpAgendaEvents = """
        SELECT `id`, IFNULL(`title`,'') AS title, IFNULL(`event_type`,'') AS event_type,
               IFNULL(`start_at`,0) AS start_at, IFNULL(`end_at`,0) AS end_at,
               IFNULL(`entity_type`,'') AS entity_type, IFNULL(`entity_id`,0) AS entity_id,
               IFNULL(`location`,'') AS location, IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_agenda_events`
        ORDER BY `start_at` DESC
        LIMIT @limit
        """;

    /// <summary>ERP documents library (notes/path omitted) — PHP epc_erp_documents.</summary>
    public const string SelectErpDocuments = """
        SELECT `id`, IFNULL(`entity_type`,'') AS entity_type, IFNULL(`entity_id`,0) AS entity_id,
               IFNULL(`doc_category`,'') AS doc_category, IFNULL(`file_name`,'') AS file_name,
               IFNULL(`file_size`,0) AS file_size, IFNULL(`mime_type`,'') AS mime_type,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_documents`
        WHERE IFNULL(`active`,1) = 1
        ORDER BY `time_created` DESC
        LIMIT @limit
        """;

    /// <summary>ERP expense reports (notes omitted) — PHP epc_erp_expense_reports.</summary>
    public const string SelectErpExpenseReports = """
        SELECT `id`, IFNULL(`report_no`,'') AS report_no, IFNULL(`staff_user_id`,0) AS staff_user_id,
               IFNULL(`title`,'') AS title, IFNULL(`total_amount`,0) AS total_amount,
               IFNULL(`status`,'') AS status, IFNULL(`period_from`,0) AS period_from,
               IFNULL(`period_to`,0) AS period_to, IFNULL(`time_updated`,0) AS time_updated
        FROM `epc_erp_expense_reports`
        ORDER BY `time_updated` DESC
        LIMIT @limit
        """;

    public const string SelectCpOfficesStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_offices`) AS office_count,
            (SELECT COUNT(DISTINCT `office_id`) FROM `shop_offices_storages_map`) AS mapped_storage_count,
            (SELECT COUNT(DISTINCT `office_id`) FROM `shop_offices_geo_map`) AS geo_mapped_count
        """;

    public const string SelectCpOffices = """
        SELECT `id`, IFNULL(`caption`,'') AS caption, IFNULL(`city`,'') AS city,
               IFNULL(`address`,'') AS address, IFNULL(`phone`,'') AS phone
        FROM `shop_offices`
        ORDER BY `id` ASC
        LIMIT @limit
        """;

    /// <summary>PHP workshop_main_page / epc_ws_dashboard KPIs (phone/email/notes omitted).</summary>
    public const string SelectCpWorkshopStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_ws_jobs` WHERE IFNULL(`status`,'') NOT IN ('delivered','cancelled')) AS open_count,
            (SELECT COUNT(*) FROM `epc_ws_jobs` WHERE IFNULL(`status`,'') IN ('in_progress','qc')) AS in_progress_count,
            (SELECT COUNT(*) FROM `epc_ws_jobs` WHERE IFNULL(`status`,'')='ready') AS ready_count,
            (SELECT COUNT(*) FROM `epc_ws_jobs` WHERE IFNULL(`status`,'')='delivered' AND IFNULL(`time_updated`,0) >= UNIX_TIMESTAMP(CURRENT_DATE())) AS delivered_today,
            (SELECT IFNULL(SUM(`grand_total`),0) FROM `epc_ws_jobs` WHERE IFNULL(`status`,'') NOT IN ('delivered','cancelled')) AS revenue_open
        """;

    public const string SelectCpWorkshopJobs = """
        SELECT j.`id`, IFNULL(j.`job_no`,'') AS job_no, IFNULL(j.`status`,'') AS status,
               IFNULL(j.`customer_name`,'') AS customer_name, IFNULL(j.`plate`,'') AS plate,
               IFNULL(j.`make`,'') AS make, IFNULL(j.`model`,'') AS model,
               IFNULL(j.`year`,'') AS year, IFNULL(b.`name`,'') AS bay_name,
               IFNULL(t.`name`,'') AS tech_name, IFNULL(j.`grand_total`,0) AS grand_total
        FROM `epc_ws_jobs` j
        LEFT JOIN `epc_ws_bays` b ON b.`id` = j.`bay_id`
        LEFT JOIN `epc_ws_technicians` t ON t.`id` = j.`tech_id`
        ORDER BY j.`id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP devices.php / kkt_root_page — shop_kkt_devices + fiscal checks (customer contact omitted).</summary>
    public const string SelectCpKktStats = """
        SELECT
            (SELECT COUNT(*) FROM `shop_kkt_devices`) AS device_count,
            (SELECT COUNT(*) FROM `shop_kkt_devices` WHERE IFNULL(`handler`,'')!='') AS wired_device_count,
            (SELECT COUNT(*) FROM `shop_kkt_checks`) AS check_count,
            (SELECT COUNT(*) FROM `shop_kkt_checks` WHERE IFNULL(`sent_to_real_device_flag`,0)=1) AS sent_count
        """;

    public const string SelectCpKktDevices = """
        SELECT d.`id`, IFNULL(d.`name`,'') AS name, IFNULL(d.`handler`,'') AS handler,
               IFNULL((SELECT `description` FROM `shop_kkt_interfaces_types` WHERE `handler` = d.`handler` LIMIT 1),'') AS interface_description
        FROM `shop_kkt_devices` d
        ORDER BY d.`name` ASC
        LIMIT @limit
        """;

    /// <summary>PHP bulk_upload_hub — operator history (all users; no file bodies).</summary>
    public const string SelectCpBulkUploadStats = """
        SELECT
            (SELECT COUNT(*) FROM `epc_bulk_upload_history`) AS upload_count,
            (SELECT IFNULL(SUM(`uploaded_count`),0) FROM `epc_bulk_upload_history`) AS uploaded_lines,
            (SELECT IFNULL(SUM(`available_count`),0) FROM `epc_bulk_upload_history`) AS available_count,
            (SELECT IFNULL(SUM(`notfound_count`),0) FROM `epc_bulk_upload_history`) AS notfound_count
        """;

    public const string SelectCpBulkUploadRows = """
        SELECT `id`, IFNULL(`file_name`,'') AS file_name, IFNULL(`priority`,'') AS priority,
               IFNULL(`uploaded_count`,0) AS uploaded_count, IFNULL(`available_count`,0) AS available_count,
               IFNULL(`cross_count`,0) AS cross_count, IFNULL(`short_count`,0) AS short_count,
               IFNULL(`notfound_count`,0) AS notfound_count, IFNULL(`created_at`,'') AS created_at
        FROM `epc_bulk_upload_history`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_erp_workflow_list</c> KPIs from <c>epc_erp_workflow_tasks</c>.</summary>
    public const string SelectErpWorkflowTaskStats = """
        SELECT
            COUNT(*) AS task_count,
            SUM(CASE WHEN `status` IN ('pending','in_progress') THEN 1 ELSE 0 END) AS open_count,
            SUM(CASE WHEN `status` = 'done' THEN 1 ELSE 0 END) AS done_count,
            SUM(CASE WHEN `status` IN ('pending','in_progress') AND `due_at` > 0 AND `due_at` < UNIX_TIMESTAMP() THEN 1 ELSE 0 END) AS overdue_count,
            SUM(CASE WHEN `status` = 'cancelled' THEN 1 ELSE 0 END) AS cancelled_count
        FROM `epc_erp_workflow_tasks`
        """;

    /// <summary>PHP <c>epc_erp_workflow_list</c> — department workflow board (writes remain PHP).</summary>
    public const string SelectErpWorkflowTasks = """
        SELECT t.`id`, IFNULL(t.`department_code`,'') AS department_code,
               IFNULL(t.`workflow_step`,'') AS workflow_step,
               IFNULL(t.`title`,'') AS title,
               IFNULL(t.`description`,'') AS description,
               IFNULL(t.`order_id`,0) AS order_id,
               IFNULL(t.`status`,'') AS status,
               IFNULL(t.`priority`,'') AS priority,
               IFNULL(t.`assigned_user_id`,0) AS assigned_user_id,
               IFNULL(p.`display_name`,'') AS assignee_name,
               IFNULL(t.`due_at`,0) AS due_at,
               IFNULL(t.`completed_at`,0) AS completed_at,
               IFNULL(t.`time_created`,0) AS time_created
        FROM `epc_erp_workflow_tasks` t
        LEFT JOIN `epc_erp_staff_profiles` p ON p.`user_id` = t.`assigned_user_id`
        ORDER BY FIELD(t.`status`, 'in_progress', 'pending', 'done', 'cancelled'),
                 t.`priority` DESC, t.`due_at` ASC, t.`id` DESC
        LIMIT @limit
        """;

    public const string SelectErpVatRatePercent = """
        SELECT IFNULL(`setting_value`,'5.00') AS vat_percent
        FROM `epc_price_settings`
        WHERE `setting_key` = 'vat_percent'
        LIMIT 1
        """;

    /// <summary>Operational VAT 201 sales box — completed shop orders in the period.</summary>
    public const string SelectErpVatReturnSales = """
        SELECT IFNULL(SUM(i.`price` * i.`count_need`), 0) AS sales_ex_vat
        FROM `shop_orders` o
        INNER JOIN `shop_orders_items` i ON i.`order_id` = o.`id`
        WHERE o.`successfully_created` = 1
          AND o.`time` >= @fromUnix
          AND o.`time` <= @toUnix
        """;

    public const string SelectErpVatReturnPurchases = """
        SELECT IFNULL(SUM(`amount_ex_vat`), 0) AS purchase_ex_vat,
               IFNULL(SUM(`vat_amount`), 0) AS input_vat,
               IFNULL(SUM(`total_amount`), 0) AS purchase_incl_vat
        FROM `epc_erp_purchases`
        WHERE `active` = 1
          AND `purchase_date` >= @fromUnix
          AND `purchase_date` <= @toUnix
        """;

    public const string SelectErpWithholdingCodes = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`rate`,0) AS rate, IFNULL(`account`,'') AS account,
               IFNULL(`active`,1) AS active
        FROM `epc_wht_code`
        ORDER BY `code` ASC
        LIMIT @limit
        """;

    public const string SelectErpWithholdingTxns = """
        SELECT t.`id`, IFNULL(t.`code_id`,0) AS code_id,
               IFNULL(c.`code`,'') AS code,
               IFNULL(t.`vendor`,'') AS vendor,
               IFNULL(t.`doc_ref`,'') AS doc_ref,
               IFNULL(t.`txn_date`,'') AS txn_date,
               IFNULL(t.`base_amount`,0) AS base_amount,
               IFNULL(t.`wht_amount`,0) AS wht_amount,
               IFNULL(t.`rate`,0) AS rate,
               IFNULL(t.`certificate_no`,'') AS certificate_no,
               IFNULL(t.`status`,'accrued') AS status,
               IFNULL(t.`time_created`,0) AS time_created
        FROM `epc_wht_txn` t
        LEFT JOIN `epc_wht_code` c ON c.`id` = t.`code_id`
        ORDER BY t.`id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_erp_petty_cash_list</c>.</summary>
    public const string SelectErpPettyCash = """
        SELECT pc.`id`, IFNULL(pc.`name`,'') AS name,
               IFNULL(pc.`account_id`,0) AS account_id,
               IFNULL(a.`name`,'') AS account_name,
               IFNULL(pc.`float_amount`,0) AS float_amount,
               IFNULL(a.`opening_balance`,0) AS account_balance,
               IFNULL(pc.`custodian_user_id`,0) AS custodian_user_id,
               IFNULL(pc.`active`,1) AS active,
               IFNULL(pc.`time_created`,0) AS time_created
        FROM `epc_erp_petty_cash` pc
        LEFT JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = pc.`account_id`
        WHERE pc.`active` = 1
        ORDER BY pc.`name` ASC
        LIMIT @limit
        """;

    public const string SelectErpCashForecasts = """
        SELECT `id`, IFNULL(`name`,'') AS name,
               IFNULL(`opening_balance`,0) AS opening_balance,
               IFNULL(`currency`,'') AS currency,
               IFNULL(`notes`,'') AS notes,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_cft_forecast`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpCashForecastLines = """
        SELECT `id`, `forecast_id`, IFNULL(`due_date`,'') AS due_date,
               IFNULL(`direction`,'in') AS direction,
               IFNULL(`amount`,0) AS amount,
               IFNULL(`category`,'') AS category,
               IFNULL(`source`,'') AS source,
               IFNULL(`notes`,'') AS notes
        FROM `epc_cft_line`
        WHERE `forecast_id` = @forecastId
        ORDER BY `due_date` ASC, `id` ASC
        LIMIT @limit
        """;

    public const string SelectErpBankInstruments = """
        SELECT `id`, IFNULL(`ref`,'') AS ref, IFNULL(`type`,'lc') AS type,
               IFNULL(`beneficiary`,'') AS beneficiary,
               IFNULL(`applicant`,'') AS applicant,
               IFNULL(`bank`,'') AS bank,
               IFNULL(`amount`,0) AS amount,
               IFNULL(`currency`,'') AS currency,
               IFNULL(`issue_date`,'') AS issue_date,
               IFNULL(`expiry_date`,'') AS expiry_date,
               IFNULL(`status`,'draft') AS status,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_cft_instrument`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpSubscriptions = """
        SELECT `id`, IFNULL(`code`,'') AS code,
               IFNULL(`customer`,'') AS customer,
               IFNULL(`plan_name`,'') AS plan_name,
               IFNULL(`amount`,0) AS amount,
               IFNULL(`currency`,'AED') AS currency,
               IFNULL(`cycle`,'monthly') AS cycle,
               IFNULL(`term_months`,12) AS term_months,
               IFNULL(`start_date`,0) AS start_date,
               IFNULL(`next_bill_date`,0) AS next_bill_date,
               IFNULL(`status`,'active') AS status,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_subscriptions`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpSupplierPortalSuppliers = """
        SELECT `id`, IFNULL(`name`,'') AS name,
               IFNULL(`contact_email`,'') AS email,
               IFNULL(`contact_phone`,'') AS phone
        FROM `epc_erp_suppliers`
        WHERE `active` = 1
        ORDER BY `name` ASC
        LIMIT @limit
        """;

    public const string SelectErpSupplierPortalPoAgg = """
        SELECT `supplier_id`,
               COUNT(*) AS po_count,
               IFNULL(SUM(`total_amount`),0) AS spend,
               SUM(CASE WHEN `status` = 'received' THEN 1 ELSE 0 END) AS received,
               SUM(CASE WHEN `status` = 'received' AND `received_at` > 0 AND `approved_at` > 0 THEN (`received_at` - `approved_at`) ELSE 0 END) AS lead_sum,
               SUM(CASE WHEN `status` = 'received' AND `received_at` > 0 AND `approved_at` > 0 THEN 1 ELSE 0 END) AS lead_n,
               SUM(CASE WHEN `status` = 'received' AND `received_at` > 0 AND `approved_at` > 0 AND (`received_at` - `approved_at`) <= 2592000 THEN 1 ELSE 0 END) AS ontime
        FROM `epc_erp_purchase_orders`
        GROUP BY `supplier_id`
        """;

    public const string SelectErpSupplierPortalRfqAgg = """
        SELECT `supplier_id`,
               COUNT(*) AS rfq_count,
               SUM(CASE WHEN `status` IN ('quoted','accepted','rejected') THEN 1 ELSE 0 END) AS responded,
               SUM(CASE WHEN `status` = 'accepted' THEN 1 ELSE 0 END) AS won
        FROM `epc_erp_rfq`
        GROUP BY `supplier_id`
        """;

    public const string SelectErpSupplierPortalBalanceAgg = """
        SELECT `supplier_id`,
               IFNULL(SUM(CASE WHEN `is_credit` = 1 THEN `amount` ELSE -`amount` END),0) AS bal
        FROM `epc_erp_supplier_accounting`
        WHERE `active` = 1
        GROUP BY `supplier_id`
        """;

    /// <summary>Virtual / exhibition / consignment locations from the warehouse master (no jewellery seed rows).</summary>
    public const string SelectErpVirtualWarehouses = """
        SELECT `id`, IFNULL(`storage_id`, 0) AS storage_id, IFNULL(`code`, '') AS code,
               IFNULL(`name`, '') AS name, `active`, IFNULL(`time_created`, 0) AS time_created
        FROM `epc_erp_inv_warehouses`
        WHERE `active` = 1
          AND (
                `code` LIKE 'VW-%'
             OR `code` LIKE 'VW_%'
             OR LOWER(`name`) LIKE '%virtual%'
             OR LOWER(`name`) LIKE '%exhibition%'
             OR LOWER(`name`) LIKE '%display%'
             OR LOWER(`name`) LIKE '%consignment%'
          )
        ORDER BY `name` ASC
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_erp_staff_list</c> — staff profiles (HR notes omitted).</summary>
    public const string SelectErpStaffProfiles = """
        SELECT p.`id`, IFNULL(p.`user_id`,0) AS user_id,
               IFNULL(p.`department_code`,'') AS department_code,
               IFNULL(p.`display_name`,'') AS display_name,
               IFNULL(p.`job_title`,'') AS job_title,
               IFNULL(p.`email`,'') AS email,
               IFNULL(p.`phone`,'') AS phone,
               IFNULL(p.`active`,1) AS active,
               IFNULL(p.`time_created`,0) AS time_created
        FROM `epc_erp_staff_profiles` p
        ORDER BY p.`department_code`, p.`display_name`, p.`id`
        LIMIT @limit
        """;

    /// <summary>PHP contracts register — body_text / ocr_text omitted.</summary>
    public const string SelectErpContracts = """
        SELECT `id`, IFNULL(`code`,'') AS code,
               IFNULL(`title`,'') AS title,
               IFNULL(`counterparty`,'') AS counterparty,
               IFNULL(`contract_value`,0) AS contract_value,
               IFNULL(`currency`,'AED') AS currency,
               IFNULL(`start_date`,0) AS start_date,
               IFNULL(`end_date`,0) AS end_date,
               IFNULL(`status`,'draft') AS status,
               IFNULL(`version`,1) AS version,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_contracts`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP opening-balance batches + line totals (meta_json omitted).</summary>
    public const string SelectErpOpeningBatches = """
        SELECT b.`id`, IFNULL(b.`module`,'combined') AS module,
               IFNULL(b.`as_of_date`,'') AS as_of_date,
               IFNULL(b.`reference`,'') AS reference,
               IFNULL(b.`status`,'draft') AS status,
               IFNULL(b.`time_created`,0) AS time_created,
               IFNULL(b.`time_posted`,0) AS time_posted,
               IFNULL((SELECT COUNT(*) FROM `epc_erp_opening_lines` l WHERE l.`batch_id` = b.`id`),0) AS line_count,
               IFNULL((SELECT SUM(l.`debit`) FROM `epc_erp_opening_lines` l WHERE l.`batch_id` = b.`id`),0) AS debit_total,
               IFNULL((SELECT SUM(l.`credit`) FROM `epc_erp_opening_lines` l WHERE l.`batch_id` = b.`id`),0) AS credit_total
        FROM `epc_erp_opening_batches` b
        ORDER BY b.`as_of_date` DESC, b.`id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_erp_marketing_list</c> — notes omitted.</summary>
    public const string SelectErpMarketingCampaigns = """
        SELECT `id`, IFNULL(`name`,'') AS name,
               IFNULL(`channel`,'') AS channel,
               IFNULL(`budget`,0) AS budget,
               IFNULL(`spent`,0) AS spent,
               IFNULL(`leads`,0) AS leads,
               IFNULL(`status`,'draft') AS status,
               IFNULL(`time_start`,0) AS time_start,
               IFNULL(`time_end`,0) AS time_end,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_marketing_campaigns`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_erp_payroll_list_runs</c> — notes omitted.</summary>
    public const string SelectErpPayrollRuns = """
        SELECT r.`id`, IFNULL(r.`period_label`,'') AS period_label,
               IFNULL(r.`period_start`,0) AS period_start,
               IFNULL(r.`period_end`,0) AS period_end,
               IFNULL(r.`status`,'draft') AS status,
               IFNULL(r.`total_gross`,0) AS total_gross,
               IFNULL(r.`total_deductions`,0) AS total_deductions,
               IFNULL(r.`total_net`,0) AS total_net,
               IFNULL(r.`paid_at`,0) AS paid_at,
               IFNULL(r.`time_created`,0) AS time_created,
               IFNULL((SELECT COUNT(*) FROM `epc_erp_payroll_lines` l WHERE l.`run_id` = r.`id`),0) AS employee_count
        FROM `epc_erp_payroll_runs` r
        ORDER BY r.`period_start` DESC, r.`id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP print designer templates — HTML/CSS bodies omitted.</summary>
    public const string SelectErpPrintTemplates = """
        SELECT `id`, IFNULL(`doc_type`,'') AS doc_type,
               IFNULL(`name`,'') AS name,
               IFNULL(`is_default`,0) AS is_default,
               IFNULL(`page_size`,'A4') AS page_size,
               IFNULL(`orientation`,'portrait') AS orientation,
               IFNULL(`active`,1) AS active,
               IFNULL(`time_updated`,0) AS time_updated
        FROM `epc_erp_print_templates`
        ORDER BY `doc_type`, `is_default` DESC, `id`
        LIMIT @limit
        """;

    public const string SelectErpOrderRecommendations = """
        SELECT r.`id`, IFNULL(r.`item_id`,0) AS item_id,
               IFNULL(i.`sku`,'') AS sku,
               IFNULL(i.`name`,'') AS item_name,
               IFNULL(r.`warehouse_id`,0) AS warehouse_id,
               IFNULL(r.`roq`,0) AS roq,
               IFNULL(r.`order_value`,0) AS order_value,
               IFNULL(r.`status`,'pending') AS status,
               IFNULL(r.`supplier`,'') AS supplier,
               IFNULL(r.`ordered_po_id`,0) AS ordered_po_id,
               IFNULL(r.`time_updated`,0) AS time_updated
        FROM `epc_erp_order_recommendations` r
        LEFT JOIN `epc_erp_inv_items` i ON i.`id` = r.`item_id`
        ORDER BY FIELD(r.`status`,'pending','confirmed','rejected','ordered'), r.`id` DESC
        LIMIT @limit
        """;

    public const string SelectErpPlanningParams = """
        SELECT `item_id`, `warehouse_id`,
               IFNULL(`lead_time_days`,30) AS lead_time_days,
               IFNULL(`target_service_level`,90) AS target_service_level,
               IFNULL(`review_period_days`,30) AS review_period_days,
               IFNULL(`min_order_qty`,0) AS min_order_qty,
               IFNULL(`order_multiple`,0) AS order_multiple,
               IFNULL(`supplier`,'') AS supplier,
               IFNULL(`stocked`,1) AS stocked
        FROM `epc_erp_planning_params`
        ORDER BY `item_id`, `warehouse_id`
        LIMIT @limit
        """;

    public const string SelectErpProcCategories = """
        SELECT `id`, IFNULL(`code`,'') AS code, IFNULL(`name`,'') AS name,
               IFNULL(`parent_id`,0) AS parent_id,
               IFNULL(`default_account`,'') AS default_account,
               IFNULL(`active`,1) AS active,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_proc_category`
        ORDER BY `code`, `id`
        LIMIT @limit
        """;

    public const string SelectErpProcPolicies = """
        SELECT `id`, IFNULL(`name`,'') AS name,
               IFNULL(`category_id`,0) AS category_id,
               IFNULL(`approval_threshold`,0) AS approval_threshold,
               IFNULL(`preferred_vendor`,'') AS preferred_vendor,
               IFNULL(`active`,1) AS active
        FROM `epc_proc_policy`
        ORDER BY `name`, `id`
        LIMIT @limit
        """;

    public const string SelectErpQmPlans = """
        SELECT p.`id`, IFNULL(p.`code`,'') AS code, IFNULL(p.`name`,'') AS name,
               IFNULL(p.`active`,1) AS active, IFNULL(p.`time_updated`,0) AS time_updated,
               IFNULL((SELECT COUNT(*) FROM `epc_qm_test` t WHERE t.`plan_id` = p.`id`),0) AS test_count
        FROM `epc_qm_plan` p
        ORDER BY p.`code`, p.`id`
        LIMIT @limit
        """;

    public const string SelectErpQmOrders = """
        SELECT `id`, IFNULL(`plan_id`,0) AS plan_id,
               IFNULL(`ref_type`,'item') AS ref_type,
               IFNULL(`ref_id`,'') AS ref_id,
               IFNULL(`item_id`,0) AS item_id,
               IFNULL(`qty`,0) AS qty,
               IFNULL(`status`,'open') AS status,
               IFNULL(`verdict`,'') AS verdict,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_qm_order`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpQmNcrs = """
        SELECT `id`, IFNULL(`order_id`,0) AS order_id,
               IFNULL(`title`,'') AS title,
               IFNULL(`severity`,'minor') AS severity,
               IFNULL(`disposition`,'') AS disposition,
               IFNULL(`status`,'open') AS status,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_qm_ncr`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpRfidTags = """
        SELECT `id`, IFNULL(`rfid_epc`,'') AS rfid_epc,
               IFNULL(`sku`,'') AS sku,
               IFNULL(`item_description`,'') AS item_description,
               IFNULL(`warehouse_id`,0) AS warehouse_id,
               IFNULL(`location_zone`,'') AS location_zone,
               IFNULL(`status`,'active') AS status,
               IFNULL(`last_scanned_at`,'') AS last_scanned_at,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_rfid_tags`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpRfidSessions = """
        SELECT `id`, IFNULL(`session_type`,'stocktake') AS session_type,
               IFNULL(`warehouse_id`,0) AS warehouse_id,
               IFNULL(`zone`,'') AS zone,
               IFNULL(`total_scanned`,0) AS total_scanned,
               IFNULL(`total_expected`,0) AS total_expected,
               IFNULL(`total_found`,0) AS total_found,
               IFNULL(`total_missing`,0) AS total_missing,
               IFNULL(`status`,'in_progress') AS status,
               IFNULL(`scanned_by_name`,'') AS scanned_by_name,
               IFNULL(`time_started`,0) AS time_started
        FROM `epc_rfid_scan_sessions`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpRecruitmentJobs = """
        SELECT `id`, IFNULL(`title`,'') AS title,
               IFNULL(`department`,'') AS department,
               IFNULL(`headcount`,1) AS headcount,
               IFNULL(`hired`,0) AS hired,
               IFNULL(`status`,'open') AS status,
               IFNULL(`hiring_manager`,'') AS hiring_manager,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_hrt_job`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpRecruitmentApplicants = """
        SELECT `id`, IFNULL(`job_id`,0) AS job_id,
               IFNULL(`name`,'') AS name,
               IFNULL(`email`,'') AS email,
               IFNULL(`phone`,'') AS phone,
               IFNULL(`stage`,'applied') AS stage,
               IFNULL(`rating`,0) AS rating,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_hrt_applicant`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpCustomerGroups = """
        SELECT g.`id`, IFNULL(g.`group_code`,'') AS group_code,
               IFNULL(g.`group_name`,'') AS group_name,
               IFNULL(g.`group_type`,'general') AS group_type,
               IFNULL(g.`discount_pct`,0) AS discount_pct,
               IFNULL(g.`credit_limit`,0) AS credit_limit,
               IFNULL(g.`payment_terms_days`,30) AS payment_terms_days,
               IFNULL(g.`is_active`,1) AS is_active,
               IFNULL(g.`time_created`,0) AS time_created,
               IFNULL((SELECT COUNT(*) FROM `epc_customer_group_members` m WHERE m.`group_id` = g.`id`),0) AS member_count
        FROM `epc_customer_groups` g
        ORDER BY g.`group_name`, g.`id`
        LIMIT @limit
        """;

    public const string SelectErpPerformanceReviews = """
        SELECT `id`, IFNULL(`employee_id`,0) AS employee_id,
               IFNULL(`employee_name`,'') AS employee_name,
               IFNULL(`period`,'') AS period,
               IFNULL(`status`,'draft') AS status,
               IFNULL(`reviewer`,'') AS reviewer,
               IFNULL(`overall_rating`,0) AS overall_rating,
               IFNULL(`time_updated`,0) AS time_updated
        FROM `epc_hrt_review`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpPerformanceGoals = """
        SELECT `id`, IFNULL(`review_id`,0) AS review_id,
               IFNULL(`title`,'') AS title,
               IFNULL(`weight`,1) AS weight,
               IFNULL(`target`,'') AS target,
               IFNULL(`rating`,0) AS rating
        FROM `epc_hrt_goal`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpProductInfoItems = """
        SELECT `id`, IFNULL(`sku`,'') AS sku,
               IFNULL(`name`,'') AS name,
               IFNULL(`product_id`,0) AS product_id,
               IFNULL(`item_type`,'standard') AS item_type,
               IFNULL(`unit`,'pcs') AS unit,
               IFNULL(`sales_price`,0) AS sales_price,
               IFNULL(`active`,1) AS active,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_inv_items`
        ORDER BY `sku`, `id`
        LIMIT @limit
        """;

    public const string SelectErpProductInfoFieldDefs = """
        SELECT `id`, IFNULL(`field_key`,'') AS field_key,
               IFNULL(`label`,'') AS label,
               IFNULL(`field_type`,'text') AS field_type,
               IFNULL(`field_role`,'inventory') AS field_role,
               IFNULL(`sort_order`,0) AS sort_order,
               IFNULL(`active`,1) AS active
        FROM `epc_erp_inv_field_defs`
        ORDER BY `sort_order`, `id`
        LIMIT @limit
        """;

    public const string SelectErpProductInfoVariants = """
        SELECT `id`, IFNULL(`item_id`,0) AS item_id,
               IFNULL(`base_sku`,'') AS base_sku,
               IFNULL(`variant_sku`,'') AS variant_sku,
               IFNULL(`variant_label`,'') AS variant_label,
               IFNULL(`active`,1) AS active,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_erp_prod_variants`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpReportSchedules = """
        SELECT `id`, IFNULL(`report_name`,'') AS report_name,
               IFNULL(`report_type`,'') AS report_type,
               IFNULL(`frequency`,'monthly') AS frequency,
               IFNULL(`day_of_week`,1) AS day_of_week,
               IFNULL(`day_of_month`,1) AS day_of_month,
               IFNULL(`time_of_day`,'08:00') AS time_of_day,
               IFNULL(`format`,'pdf') AS format,
               IFNULL(`is_active`,1) AS is_active,
               IFNULL(`last_status`,'') AS last_status,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_report_schedules`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpPrjaBudgets = """
        SELECT `id`, IFNULL(`project_id`,0) AS project_id,
               IFNULL(`category`,'general') AS category,
               IFNULL(`cost_budget`,0) AS cost_budget,
               IFNULL(`revenue_budget`,0) AS revenue_budget,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_prja_budget`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpPrjaTxns = """
        SELECT `id`, IFNULL(`project_id`,0) AS project_id,
               IFNULL(`txn_type`,'cost') AS txn_type,
               IFNULL(`category`,'general') AS category,
               IFNULL(`description`,'') AS description,
               IFNULL(`amount`,0) AS amount,
               IFNULL(`txn_date`,0) AS txn_date,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_prja_txn`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpPrjaRecognitions = """
        SELECT `id`, IFNULL(`project_id`,0) AS project_id,
               IFNULL(`method`,'poc') AS method,
               IFNULL(`as_of`,0) AS as_of,
               IFNULL(`pct_complete`,0) AS pct_complete,
               IFNULL(`recognized_revenue`,0) AS recognized_revenue,
               IFNULL(`recognized_cost`,0) AS recognized_cost,
               IFNULL(`wip`,0) AS wip,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_prja_recognition`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpDocAttachments = """
        SELECT `id`, IFNULL(`entity_type`,'') AS entity_type,
               IFNULL(`entity_id`,0) AS entity_id,
               IFNULL(`file_name`,'') AS file_name,
               IFNULL(`file_size`,0) AS file_size,
               IFNULL(`mime_type`,'') AS mime_type,
               IFNULL(`description`,'') AS description,
               IFNULL(`uploaded_by_name`,'') AS uploaded_by_name,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_doc_attachments`
        ORDER BY `id` DESC
        LIMIT @limit
        """;

    public const string SelectErpInventoryReportCategories = """
        SELECT `id`, IFNULL(`parent_id`,0) AS parent_id,
               IFNULL(`code`,'') AS code,
               IFNULL(`name`,'') AS name,
               IFNULL(`level`,1) AS level,
               IFNULL(`sort_order`,0) AS sort_order,
               IFNULL(`is_active`,1) AS is_active,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_inventory_categories`
        ORDER BY `level`, `sort_order`, `name`, `id`
        LIMIT @limit
        """;

    public const string SelectErpInventoryReportSnapshots = """
        SELECT `id`, IFNULL(`snapshot_date`,'') AS snapshot_date,
               IFNULL(`category_id`,0) AS category_id,
               IFNULL(`total_skus`,0) AS total_skus,
               IFNULL(`total_qty`,0) AS total_qty,
               IFNULL(`total_value`,0) AS total_value,
               IFNULL(`avg_age_days`,0) AS avg_age_days,
               IFNULL(`time_created`,0) AS time_created
        FROM `epc_inventory_snapshots`
        ORDER BY `snapshot_date` DESC, `id` DESC
        LIMIT @limit
        """;

    /// <summary>PHP <c>epc_erp_dashboard</c> purchase ex-VAT for a period (purchases register).</summary>
    public const string SumErpWorkspacePurchaseExVat = """
        SELECT IFNULL(SUM(`amount_ex_vat`), 0) FROM `epc_erp_purchases`
        WHERE `active` = 1 AND `purchase_date` >= @dateFrom AND `purchase_date` <= @dateTo
        """;

    /// <summary>PHP sales incl. VAT from completed shop orders.</summary>
    public const string SumErpWorkspaceSalesInclVat = """
        SELECT IFNULL(SUM(`price_total_wt`), 0) FROM `shop_orders`
        WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo
        """;

    /// <summary>PHP <c>receivable_due_orders</c> approximation — unpaid completed orders in period.</summary>
    public const string SumErpWorkspaceReceivableDueOrders = """
        SELECT IFNULL(SUM(`price_total_wt`), 0) FROM `shop_orders`
        WHERE `successfully_created` = 1 AND IFNULL(`paid`, 0) = 0
          AND `time` >= @dateFrom AND `time` <= @dateTo
        """;

    public const string CountErpWorkspaceConfirmedSalesOrders = """
        SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'confirmed'
        """;

    /// <summary>PHP NetSuite reminder — open POs in draft/sent/confirmed.</summary>
    public const string CountErpWorkspaceOpenPurchaseOrders = """
        SELECT COUNT(*) FROM `epc_erp_purchase_orders`
        WHERE `status` IN ('draft','sent','confirmed')
        """;

    /// <summary>PHP NetSuite reminder — e-invoices with balance due.</summary>
    public const string CountErpWorkspaceInvoicesDue = """
        SELECT COUNT(*) FROM `epc_einvoice_documents`
        WHERE `active` = 1 AND `status` <> 'cancelled'
          AND (`total_incl_vat` - `paid_amount`) > 0.005
        """;

    public const string SelectErpWorkspaceProcessByDepartment = """
        SELECT IFNULL(`current_department`,'') AS dept, COUNT(*) AS n
        FROM `epc_pf_cases`
        WHERE `status` = 'open'
        GROUP BY IFNULL(`current_department`,'')
        ORDER BY n DESC
        """;

    public const string SelectErpWorkspaceTopPerformers = """
        SELECT IFNULL(`current_assignee_id`,0) AS assignee,
               IFNULL(`current_department`,'') AS dept,
               COUNT(*) AS n
        FROM `epc_pf_cases`
        WHERE `status` = 'done'
        GROUP BY IFNULL(`current_assignee_id`,0), IFNULL(`current_department`,'')
        ORDER BY n DESC
        LIMIT 8
        """;

    public const string CountErpWorkspaceProcessBusy = """
        SELECT COUNT(DISTINCT `current_assignee_id`) FROM `epc_pf_cases`
        WHERE `status` = 'open' AND `current_assignee_id` > 0
        """;

    public const string CountErpWorkspaceProcessHeadcount = """
        SELECT COUNT(DISTINCT `current_assignee_id`) FROM `epc_pf_cases`
        WHERE `current_assignee_id` > 0
        """;

}
