using EcomAE.Platform.Services;

namespace EcomAE.Platform.Surfaces;

public sealed class MigrationSurfaceShellCatalog : ISurfaceShellCatalog
{
    public SurfaceShellResponse Build(string surfaceKey, TenantContext? tenant)
    {
        var normalized = surfaceKey.Trim().ToLowerInvariant();
        return normalized switch
        {
            "cp" => BuildControlPanel(tenant),
            "erp" => BuildErp(tenant),
            "bos" => BuildBos(tenant),
            _ => new SurfaceShellResponse(normalized, "unknown", string.Empty, string.Empty, TenantModeName(tenant), [], ["Register surface shell before routing production traffic."])
        };
    }

    private static SurfaceShellResponse BuildControlPanel(TenantContext? tenant) => new(
        "Super CP / tenant CP",
        "shell-started",
        "cp/",
        "/CP",
        TenantModeName(tenant),
        [
            new("dashboard", "Dashboard", ["tenant overview", "orders summary", "pricing alerts"], "cp/index.php", "mapped"),
            new("users", "Users and permissions", ["admin users", "roles", "legacy permission bridge"], "cp/content/users/", "pending-port"),
            new("tenant-admin", "Tenant administration", ["tenant registry", "ERP-only mode", "live tenant controls"], "cp/content/control/portal/", "pending-port"),
            new("settings", "Settings", ["store settings", "integrations", "security"], "cp/content/", "pending-port")
        ],
        ["Validate login redirect behavior.", "Match menu visibility for super and tenant users.", "Run CP permission parity tests."]);

    private static SurfaceShellResponse BuildErp(TenantContext? tenant) => new(
        "Super ERP / tenant ERP",
        "shell-started",
        "cp/content/shop/finance/erp/",
        "/ERP",
        TenantModeName(tenant),
        [
            new("finance-dashboard", "Finance dashboard", ["KPIs", "balances", "cash movement"], "cp/content/shop/finance/erp/erp_dashboard.php", "mapped"),
            new("chart-of-accounts", "Chart of accounts", ["account tree", "opening balances", "ledger links"], "content/shop/finance/epc_erp_gl.php", "pending-port"),
            new("vouchers", "Vouchers", ["journal vouchers", "posting", "audit trail"], "content/shop/finance/epc_erp_vouchers.php", "pending-port"),
            new("inventory", "Inventory and fulfillment", ["stock movements", "purchase orders", "sales orders"], "content/shop/finance/epc_erp_order_fulfillment.php", "pending-port")
        ],
        ["Match ERP-only tenant access.", "Compare finance totals against PHP fixtures.", "Validate posting/audit permission rules."]);

    private static SurfaceShellResponse BuildBos(TenantContext? tenant) => new(
        "Super BOS",
        "shell-started",
        "bos/",
        "/BOS",
        TenantModeName(tenant),
        [
            new("command-center", "Command center", ["fleet health", "tenant operations", "incident queue"], "bos/index.php", "mapped"),
            new("audit", "Audit and controls", ["privileged actions", "review queue", "rollback notes"], "cp/content/control/portal/", "pending-port"),
            new("operations", "Operations", ["tenant enablement", "job status", "system checks"], "cp/content/control/", "pending-port")
        ],
        ["Require super BOS permission.", "Mirror PHP audit log writes.", "Validate emergency rollback path."]);

    private static string TenantModeName(TenantContext? tenant) => tenant?.Mode.ToString() ?? "Unknown";
}
