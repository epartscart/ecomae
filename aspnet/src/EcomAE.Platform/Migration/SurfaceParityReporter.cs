namespace EcomAE.Platform.Migration;

public sealed class SurfaceParityReporter : ISurfaceParityReporter
{
    public SurfaceParityReport BuildReport()
    {
        SurfaceParityItem[] items =
        [
            new("Login", "legacy session and permission bridge", "/CP/ auth plugin + /BOS/?action=login", "/cp/login|/erp/login|/bos/login|/storefront/login + POST /auth/login/admin + /auth/session/probe", "login-bridge-hybrid", "Batch 3: PHP-compatible admin/customer mint when SecretSuccession set + cookie dual-sample harness. Social/demo/shared-ERP picker stay PHP. DECISION: /BOS/ remains PHP-authoritative for $_SESSION modules."),
            new("Super CP", "dashboard shell", "ecomae.com/CP", "/cp/app (hybrid nav→PHP)", "hybrid-chrome-nav-login-bridge", "ASP.NET Blazor CP shell reuses PHP CSS; nav/quick actions open live PHP modules; full desktop widgets still need parity evidence."),
            new("Platform ERP", "finance dashboard", "ecomae.com/ERP", "/erp/app (hybrid nav→PHP areas)", "hybrid-chrome-nav-login-bridge", "ASP.NET Blazor ERP shell reuses PHP ERP theme CSS; category nav opens PHP ERP areas; finance writes still PHP."),
            new("Super BOS", "operations command center", "ecomae.com/BOS", "/bos/app (hybrid; digests+PHP /BOS/)", "hybrid-chrome-nav-login-bridge", "ASP.NET BOS shell + digests; native BOS $_SESSION modules remain on /BOS/."),

            new("Tenant CP", "tenant administration", "tenant.com/CP", "/CP", "tenant-routing-started", "Live tenant CP login, menus, user scopes, settings, and order/pricing modules pass parity tests."),
            new("Tenant ERP", "tenant finance operations", "tenant.com/ERP", "/ERP", "tenant-routing-started", "Live-tenant and ERP-only tenant ERP workflows pass parity tests against production fixtures."),
            new("Storefront", "customer-facing commerce", "tenant storefront", "/", "presentation-shell-scaffolded", "Account shell can negotiate HTML chrome using templates/nero CSS; catalog/cart/checkout/SEO still need full parity evidence."),
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
