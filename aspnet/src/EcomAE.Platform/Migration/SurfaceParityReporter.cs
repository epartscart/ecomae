namespace EcomAE.Platform.Migration;

public sealed class SurfaceParityReporter : ISurfaceParityReporter
{
    public SurfaceParityReport BuildReport()
    {
        SurfaceParityItem[] items =
        [
            new("Login", "legacy session and permission bridge", "/cp/login.php", "/auth/session/probe", "bridge-started", "Validated PHP cookie, user id, role, tenant, and permission parity for CP/ERP/BOS users."),
            new("Super CP", "dashboard shell", "ecomae.com/CP", "/CP", "shell-placeholder", "ASP.NET route renders equivalent dashboard shell, menu, tenant selector, and access denial behavior."),
            new("Platform ERP", "finance dashboard", "ecomae.com/ERP", "/ERP", "shell-placeholder", "ASP.NET route matches finance dashboard KPIs, chart of accounts, vouchers, invoices, inventory, and reports."),
            new("Super BOS", "operations command center", "ecomae.com/BOS", "/BOS", "shell-placeholder", "ASP.NET route matches privileged BOS operations, audit logging, tenant fleet health, and rollback safety."),
            new("Tenant CP", "tenant administration", "tenant.com/CP", "/CP", "tenant-routing-started", "Live tenant CP login, menus, user scopes, settings, and order/pricing modules pass parity tests."),
            new("Tenant ERP", "tenant finance operations", "tenant.com/ERP", "/ERP", "tenant-routing-started", "Live-tenant and ERP-only tenant ERP workflows pass parity tests against production fixtures."),
            new("Storefront", "customer-facing commerce", "tenant storefront", "/", "pending", "Catalog browsing, cart, checkout, account, SEO, and asset rendering match PHP storefront output."),
            new("Public API", "catalog and price lookup", "/api/v1/catalog.php and /api/v1/price/lookup.php", "/api/v1/catalog/status and /api/v1/price/lookup", "scaffold-started", "Response schema, error handling, authentication, database reads, and latency match PHP API behavior."),
            new("Workers", "scheduled jobs", "PHP cron/setup scripts", "EcomAE.Workers", "placeholder", "Imports, sitemap, notifications, backups, cleanup, retries, and telemetry run without PHP cron dependency.")
        ];

        return new SurfaceParityReport(
            "parity-not-yet-reached",
            items,
            [
                "Port at least one customer-facing shell beyond placeholder status for CP, ERP, or BOS.",
                "Connect catalog/price APIs to existing MySQL tables with response parity tests.",
                "Validate legacy session bridge against real CP/ERP/BOS login cookies and permissions.",
                "Add production proxy flags and rollback telemetry for the first ASP.NET-routed surface."
            ]);
    }
}
