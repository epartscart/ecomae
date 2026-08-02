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

    public const string SumBankBalances = """
        SELECT COALESCE(SUM(`balance`), 0) FROM `epc_bank_accounts`
        """;

    public const string SumArOutstanding = """
        SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_invoices` WHERE `status` <> 'paid'
        """;

    public const string SumApOutstanding = """
        SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_bills` WHERE `status` <> 'paid'
        """;

    public const string SumStockValue = """
        SELECT COALESCE(SUM(`quantity` * `avg_cost`), 0) FROM `epc_inventory_stock`
        """;
}
