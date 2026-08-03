namespace EcomAE.Platform.Migration;

public sealed class SurfaceParityReporter : ISurfaceParityReporter
{
    public SurfaceParityReport BuildReport()
    {
        SurfaceParityItem[] items =
        [
            new("Login", "legacy session and permission bridge", "/cp/login.php", "/auth/session/probe", "bridge-started", "Validated PHP cookie, user id, role, tenant, and permission parity for CP/ERP/BOS users."),
            new("Super CP", "dashboard shell", "ecomae.com/CP", "/CP", "presentation-shell-scaffolded", "ASP.NET Core route renders presentation-preserving shell using PHP CP CSS assets; menu, tenant selector, and access denial still need full parity evidence."),
            new("Platform ERP", "finance dashboard", "ecomae.com/ERP", "/ERP", "presentation-shell-scaffolded", "ASP.NET Core route renders presentation-preserving ERP chrome using PHP ERP theme CSS; finance KPIs/workflows still need full parity evidence."),
            new("Super BOS", "operations command center", "ecomae.com/BOS", "/BOS", "presentation-shell-scaffolded", "ASP.NET Core route renders presentation-preserving BOS chrome using bos/epc_bos_shell.css; privileged ops still need full parity evidence."),
            new("Tenant CP", "tenant administration", "tenant.com/CP", "/CP", "tenant-routing-started", "Live tenant CP login, menus, user scopes, settings, and order/pricing modules pass parity tests."),
            new("Tenant ERP", "tenant finance operations", "tenant.com/ERP", "/ERP", "tenant-routing-started", "Live-tenant and ERP-only tenant ERP workflows pass parity tests against production fixtures."),
            new("Storefront", "customer-facing commerce", "tenant storefront", "/", "presentation-shell-scaffolded", "Account shell can negotiate HTML chrome using templates/modex CSS; catalog/cart/checkout/SEO still need full parity evidence."),
            new("Public API", "catalog and price lookup", "/api/v1/catalog.php and /api/v1/price/lookup.php", "/api/v1/catalog/status and /api/v1/price/lookup", "catalog-cache-routes-wired-awaiting-staging", "Catalog/price DB/cache readers + API-key auth are wired; dual-sample compare_*_parity.py + authenticated smoke still required before shadows."),
            new("Workers", "scheduled jobs", "PHP cron/setup scripts", "EcomAE.Workers", "dry-run-validator-layer-complete", "Tracked worker dry-run validators cover cataloged cron/queue jobs (writes blocked); live schedule cutover still PHP-authoritative.")
        ];

        return new SurfaceParityReport(
            "parity-not-yet-reached",
            items,
            [
                "On CloudPanel: ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh → validate_final_gate_env.sh → capture/commit.",
                "Run scripts/run_surface_parity_harness.sh and attach field-by-field dual samples under docs/migration/evidence/surface-parity/samples/.",
                "Use /migration/surface-field-parity contracts before any exact-route shadow promotion.",
                "Validate legacy session bridge against real CP/ERP/BOS login cookies and permissions.",
                "Keep broad /cp /erp /bos /storefront cutover blocked until every contracted digest/presentation match is true."
            ]);
    }
}
