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
            "storefront" => BuildStorefront(tenant),
            _ => new SurfaceShellResponse(normalized, "unknown", string.Empty, string.Empty, TenantModeName(tenant), [], ["Register surface shell before routing production traffic."])
        };
    }

    private static SurfaceShellResponse BuildControlPanel(TenantContext? tenant) => new(
        "Super CP / tenant CP",
        "presentation-shell-scaffolded",
        "cp/",
        "/cp",
        TenantModeName(tenant),
        [
            new("dashboard", "Dashboard", ["tenant overview", "orders summary", "pricing alerts"], "cp/index.php", "mapped"),
            new("users", "Users and permissions", ["admin users", "roles", "legacy permission bridge"], "cp/content/users/", "pending-port"),
            new("tenant-admin", "Tenant administration", ["tenant registry", "ERP-only mode", "live tenant controls"], "cp/content/control/portal/", "pending-port"),
            new("settings", "Settings", ["store settings", "integrations", "security"], "cp/content/", "pending-port")
        ],
        ["Validate login redirect behavior.", "Match menu visibility for super and tenant users.", "Compare HTML chrome CSS against PHP desktop.php.", "Run CP permission parity tests."]);

    private static SurfaceShellResponse BuildErp(TenantContext? tenant) => new(
        "Super ERP / tenant ERP",
        "presentation-shell-scaffolded",
        "cp/content/shop/finance/erp/",
        "/erp",
        TenantModeName(tenant),
        [
            new("finance-dashboard", "Finance dashboard", ["KPIs", "balances", "cash movement"], "cp/content/shop/finance/erp/erp_dashboard.php", "mapped"),
            new("chart-of-accounts", "Chart of accounts", ["account tree", "opening balances", "ledger links"], "content/shop/finance/epc_erp_gl.php", "pending-port"),
            new("vouchers", "Vouchers", ["journal vouchers", "posting", "audit trail"], "content/shop/finance/epc_erp_vouchers.php", "pending-port"),
            new("inventory", "Inventory and fulfillment", ["stock movements", "purchase orders", "sales orders"], "content/shop/finance/epc_erp_order_fulfillment.php", "pending-port")
        ],
        ["Match ERP-only tenant access.", "Compare finance totals against PHP fixtures.", "Compare HTML chrome CSS against PHP erp_desktop.php.", "Validate posting/audit permission rules."]);

    private static SurfaceShellResponse BuildBos(TenantContext? tenant) => new(
        "Super BOS",
        "presentation-shell-scaffolded",
        "bos/",
        "/bos",
        TenantModeName(tenant),
        [
            new("command-center", "Command center", ["fleet health", "tenant operations", "incident queue"], "bos/index.php", "mapped"),
            new("audit", "Audit and controls", ["privileged actions", "review queue", "rollback notes"], "cp/content/control/portal/", "pending-port"),
            new("operations", "Operations", ["tenant enablement", "job status", "system checks"], "cp/content/control/", "pending-port")
        ],
        ["Require super BOS permission.", "Mirror PHP audit log writes.", "Compare HTML chrome CSS against bos/epc_bos_shell.css.", "Validate emergency rollback path."]);


    private static SurfaceShellResponse BuildStorefront(TenantContext? tenant) => new(
        "Storefront / customer commerce",
        "presentation-shell-scaffolded",
        "content/shop/ and templates/",
        "/",
        TenantModeName(tenant),
        [
            new("home", "Home and CMS", ["landing page", "CMS pages", "SEO metadata"], "content/general_pages/", "pending-port"),
            new("catalog", "Catalog browsing", ["manufacturer catalog", "vehicle catalog", "part search"], "api/UCatalog/ and api/umapi_proxy.php", "pending-port"),
            new("cart", "Cart and checkout", ["cart", "checkout", "order submit"], "content/shop/", "pending-port"),
            new("account", "Customer account", ["login", "orders", "garage"], "content/users/", "pending-port")
        ],
        ["Compare rendered HTML metadata against PHP storefront.", "Reuse templates/modex CSS asset URLs for presentation parity.", "Validate cart/session compatibility.", "Run checkout parity with sandbox payment mode."]);

    private static string TenantModeName(TenantContext? tenant) => tenant?.Mode.ToString() ?? "Unknown";
}
