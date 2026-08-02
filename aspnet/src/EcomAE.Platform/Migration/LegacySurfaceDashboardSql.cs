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
}
