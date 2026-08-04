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
    /// CP OMS recent orders (platform-wide read digest).
    /// Office-manager ACL filtering remains PHP-authoritative.
    /// </summary>
    public const string SelectCpShopOrders = """
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

    public const string SelectCpUsers = """
        SELECT `user_id`, `email`, `phone`, `unlocked`, `time_registered`, `time_last_visit`
        FROM `users`
        ORDER BY `user_id` DESC
        LIMIT @limit
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
               IFNULL(j.`status`, '') AS status,
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

    public const string SelectErpCoaAccounts = """
        SELECT `id`, IFNULL(`code`, '') AS code, IFNULL(`name`, '') AS name,
               IFNULL(`account_type`, '') AS account_type, IFNULL(`normal_side`, '') AS normal_side,
               IFNULL(`parent_id`, 0) AS parent_id, IFNULL(`opening_balance`, 0) AS opening_balance,
               `active`
        FROM `epc_erp_coa_accounts`
        WHERE `active` = 1
        ORDER BY `code` ASC
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

    public const string SelectErpInventoryStockSummary = """
        SELECT COUNT(*) AS row_count,
               IFNULL(SUM(`qty_on_hand`), 0) AS qty_on_hand,
               IFNULL(SUM(`qty_on_hand` * `avg_unit_cost`), 0) AS stock_value,
               COUNT(DISTINCT `warehouse_id`) AS warehouse_count,
               COUNT(DISTINCT `item_id`) AS item_count
        FROM `epc_erp_inv_stock`
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

    /// <summary>
    /// Batch 4 storefront part search (mirrors pyapi <c>part_search</c> / warehouse offers).
    /// Read-only — cart/checkout and full PHP part_search tabs remain PHP.
    /// </summary>
    public const string SelectStorefrontPartSearch = """
        SELECT d.`price_id`, IFNULL(p.`name`, '') AS price_list,
               IFNULL(d.`manufacturer`, '') AS manufacturer,
               IFNULL(d.`article`, '') AS article,
               IFNULL(d.`article_show`, '') AS article_show,
               IFNULL(d.`name`, '') AS name,
               IFNULL(d.`price`, 0) AS price,
               IFNULL(d.`exist`, 0) AS exist,
               IFNULL(d.`storage`, '') AS storage
        FROM `shop_docpart_prices_data` d
        INNER JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
        WHERE d.`article_search` = @article
          AND IFNULL(p.`storefront_temp_disabled`, 0) = 0
          AND IFNULL(d.`price`, 0) > 0
        ORDER BY d.`price` ASC
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
               IFNULL(`t2_min_order`, 0) AS min_order
        FROM `shop_carts`
        WHERE `user_id` = @userId
          AND `session_id` = 0
        ORDER BY `id` DESC
        LIMIT @limit
        """;

}
