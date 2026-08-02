namespace EcomAE.Platform.Migration;

public sealed class MigrationReadinessReporter : IMigrationReadinessReporter
{
    public MigrationReadinessReport BuildReport()
    {
        MigrationReadinessItem[] items =
        [
            new("Super CP", "/cp", "EcomAE.Platform CP module", "foundation", true, "Port login, users, tenant administration, settings, and dashboard workflows before routing ecomae.com/CP to ASP.NET Core."),
            new("Platform ERP", "/cp/content/shop/finance/erp", "EcomAE.Platform ERP module", "foundation", true, "Port finance dashboard, chart of accounts, inventory, invoices, reports, and permission checks before routing ecomae.com/ERP to ASP.NET Core."),
            new("Super BOS", "/bos", "EcomAE.Platform BOS module", "foundation", true, "Port operations command center and privileged admin actions before routing ecomae.com/BOS to ASP.NET Core."),
            new("Tenant CP", "tenant.com/CP", "tenant-aware CP module", "foundation", true, "Connect tenant registry and legacy sessions to tenant-scoped CP workflows for live tenants."),
            new("Tenant ERP", "tenant.com/ERP", "tenant-aware ERP module", "foundation", true, "Port tenant ERP workflows and validate live-tenant and ERP-only tenant modes."),
            new("Public APIs", "/api/v1", "ASP.NET Core Web API", "catalog-cache-routes-wired-awaiting-staging", true, "Catalog/price DB/cache readers + API-key auth are wired; dual-sample compare_*_parity.py + authenticated smoke still required before shadows."),
            new("Background jobs", "PHP cron/setup scripts", "EcomAE.Workers", "dry-run-validator-layer-complete", true, "Tracked worker dry-run validators cover cataloged cron/queue jobs (writes blocked); live schedule cutover still PHP-authoritative.")
        ];

        return new MigrationReadinessReport(
            "not-ready-for-php-removal",
            false,
            items,
            [
                "On CloudPanel: ensure→issue→validate→capture staging-smoke before any exact-route shadow.",
                "ASP.NET Core endpoints must pass response parity against current PHP routes (compare_*_parity.py).",
                "Authentication and authorization must match legacy CP/ERP/BOS behavior.",
                "Tenant routing must be validated for platform, live tenant, and ERP-only tenant hosts.",
                "Background jobs must run from EcomAE.Workers with monitored success/failure telemetry before PHP cron removal.",
                "Production proxy must route only validated location = surfaces to ASP.NET Core; all other PHP routes remain active until parity."
            ]);
    }
}
