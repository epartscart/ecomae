namespace EcomAE.Platform.Migration;

/// <summary>
/// Operator-facing catalog of live Super CP / tenant / ERP / storefront URLs.
/// StackToday reflects production cutover reality: operator chrome remains PHP until exact-route shadows + approval.
/// </summary>
public sealed class LiveSurfaceLinkReporter : ILiveSurfaceLinkReporter
{
    public LiveSurfaceLinkReport BuildReport()
    {
        LiveSurfaceLink[] links =
        [
            // Super CP / platform operator
            Link("super-cp", "Frontend / marketing", "https://www.ecomae.com/", "php", "/", "Platform marketing home remains PHP."),
            Link("super-cp", "Control Panel", "https://www.ecomae.com/CP/", "php", "/cp", "PHP chrome authoritative; ASP.NET shell available on loopback when AdminAspNetEnabled."),
            Link("super-cp", "Control Panel (alias)", "https://www.ecomae.com/cp/", "php", "/cp", "Case-insensitive alias of Super CP."),
            Link("super-cp", "ERP", "https://www.ecomae.com/ERP/", "php", "/erp", "Platform ERP desktop remains PHP."),
            Link("super-cp", "ERP (alias)", "https://www.ecomae.com/erp/", "php", "/erp", "Case-insensitive alias of Platform ERP."),
            Link("super-cp", "BOS", "https://www.ecomae.com/BOS/", "php", "/bos", "Super BOS command center remains PHP."),
            Link("super-cp", "BOS (alias)", "https://www.ecomae.com/bos/", "php", "/bos", "Case-insensitive alias of Super BOS."),
            Link("super-cp", "Dedicated Super CP host", "https://cp.ecomae.com/CP/", "php", "/cp", "cp.ecomae.com is the dedicated Super CP hostname."),
            Link("super-cp", "Dedicated Super CP BOS", "https://cp.ecomae.com/BOS/", "php", "/bos", "BOS on dedicated Super CP host."),
            Link("super-cp", "Dedicated Super CP ERP", "https://cp.ecomae.com/ERP/", "php", "/erp", "ERP on dedicated Super CP host."),

            // ASP.NET diagnostics already live publicly
            Link("aspnet-diagnostics", "Health", "https://www.ecomae.com/health", "aspnet", "/health", "ASP.NET health check."),
            Link("aspnet-diagnostics", "Zero-PHP completion", "https://www.ecomae.com/migration/zero-php-completion", "aspnet", "/migration/zero-php-completion", "Weighted completion (95%/5%)."),
            Link("aspnet-diagnostics", "PHP decommission readiness", "https://www.ecomae.com/migration/php-decommission-readiness", "aspnet", "/migration/php-decommission-readiness", "Final-gate checklist; ReadyToRemovePhp=false."),
            Link("aspnet-diagnostics", "Presentation parity", "https://www.ecomae.com/migration/presentation-parity", "aspnet", "/migration/presentation-parity", "PHP chrome asset contract for ASP.NET shells."),
            Link("aspnet-diagnostics", "Live surface links", "https://www.ecomae.com/migration/live-surface-links", "aspnet", "/migration/live-surface-links", "This catalog."),
            Link("aspnet-diagnostics", "Surface parity", "https://www.ecomae.com/migration/surface-parity", "aspnet", "/migration/surface-parity", "Surface-by-surface parity statuses."),
            Link("aspnet-api", "Price lookup", "https://www.ecomae.com/api/v1/price/lookup", "aspnet", "/api/v1/price/lookup", "Already on ASP.NET; unauthenticated returns JSON missing_api_key."),

            // Industry showcase frontends (*.ecomae.com) — not dedicated client tenants
            Link("industry-frontend", "Healthcare frontend", "https://healthcare.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),
            Link("industry-frontend", "Healthcare CP", "https://healthcare.ecomae.com/CP/", "php", "/cp", "Industry CP chrome (PHP)."),
            Link("industry-frontend", "Healthcare ERP", "https://healthcare.ecomae.com/ERP/", "php", "/erp", "Industry ERP chrome (PHP)."),
            Link("industry-frontend", "Home & living frontend", "https://homeliving.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),
            Link("industry-frontend", "Retail frontend", "https://retail.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),
            Link("industry-frontend", "Fashion frontend", "https://fashion.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),
            Link("industry-frontend", "Jewellery frontend", "https://jewellery.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),
            Link("industry-frontend", "Food frontend", "https://food.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),
            Link("industry-frontend", "Beauty frontend", "https://beauty.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),
            Link("industry-frontend", "Sports frontend", "https://sports.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),
            Link("industry-frontend", "Pet frontend", "https://pet.ecomae.com/", "php", "/", "Industry subdomain showcase storefront."),

            // Known dedicated tenant / brand hosts
            Link("tenant", "Electronicae frontend", "https://www.electronicae.com/", "php", "/", "Dedicated tenant storefront."),
            Link("tenant", "Electronicae CP", "https://www.electronicae.com/CP/", "php", "/cp", "Tenant CP."),
            Link("tenant", "Electronicae ERP", "https://www.electronicae.com/ERP/", "php", "/erp", "Tenant ERP."),
            Link("tenant", "Style N Look frontend", "https://www.stylenlook.com/", "php", "/", "Dedicated tenant storefront."),
            Link("tenant", "Style N Look CP", "https://www.stylenlook.com/CP/", "php", "/cp", "Tenant CP."),
            Link("tenant", "Style N Look ERP", "https://www.stylenlook.com/ERP/", "php", "/erp", "Tenant ERP."),
            Link("tenant", "Jewellery Trend frontend", "https://www.thejewellerytrend.com/", "php", "/", "Dedicated tenant storefront."),
            Link("tenant", "Jewellery Trend CP", "https://www.thejewellerytrend.com/CP/", "php", "/cp", "Tenant CP."),
            Link("tenant", "Jewellery Trend ERP", "https://www.thejewellerytrend.com/ERP/", "php", "/erp", "Tenant ERP."),
            Link("tenant", "Taxofin CA frontend", "https://www.taxofinca.com/", "php", "/", "Dedicated tenant storefront."),
            Link("tenant", "Taxofin CA CP", "https://www.taxofinca.com/CP/", "php", "/cp", "Tenant CP."),
            Link("tenant", "Taxofin CA ERP", "https://www.taxofinca.com/ERP/", "php", "/erp", "Tenant ERP."),
            Link("tenant", "ePartsCart frontend", "https://epartscart.com/", "php", "/", "Live auto-parts tenant/storefront host."),
            Link("tenant", "ePartsCart CP", "https://epartscart.com/CP/", "php", "/cp", "Tenant CP."),
            Link("tenant", "ePartsCart ERP", "https://epartscart.com/ERP/", "php", "/erp", "Tenant ERP."),
            Link("tenant", "ePartsCart www frontend", "https://www.epartscart.com/", "php", "/", "www alias for ePartsCart."),

            // Exact-route ASP.NET digests (not yet publicly proxied; enable one location= at a time)
            Link("aspnet-digest-pending-shadow", "CP dashboard digest", "https://www.ecomae.com/cp/dashboard-summary", "php-fallback", "/cp/dashboard-summary", "Needs exact-route nginx shadow after smoke; currently PHP 404 HTML."),
            Link("aspnet-digest-pending-shadow", "ERP dashboard digest", "https://www.ecomae.com/erp/dashboard-summary", "php-fallback", "/erp/dashboard-summary", "Needs exact-route nginx shadow after smoke."),
            Link("aspnet-digest-pending-shadow", "BOS fleet digest", "https://www.ecomae.com/bos/fleet-summary", "php-fallback", "/bos/fleet-summary", "Needs exact-route nginx shadow after smoke."),
            Link("aspnet-digest-pending-shadow", "Catalog status", "https://www.ecomae.com/api/v1/catalog/status", "php-fallback", "/api/v1/catalog/status", "Needs exact-route nginx shadow + API key smoke.")
        ];

        return new LiveSurfaceLinkReport(
            "catalogued-php-authoritative-except-allowlisted-aspnet",
            "www.ecomae.com",
            links,
            [
                "Broad /, /api, /cp, /erp, /bos, storefront nginx cutover remains forbidden.",
                "Only approved location = exact-route shadows may be enabled after staging smoke.",
                "Keep MigrationRouteCutover StorefrontAspNetEnabled=false, AdminAspNetEnabled=false, RequirePhpFallback=true until final gate.",
                "ReadyToRemovePhp stays false without staging-smoke artifacts + RELEASE_OWNER_APPROVAL.md."
            ],
            [
                "On CloudPanel: set ECOMAE_PRICE_LOOKUP_API_KEY / ECOMAE_CATALOG_API_KEY / optional ECOMAE_ADMIN_COOKIE_HEADER in /etc/ecomae-aspnet/platform.env.",
                "Run bash scripts/cloudpanel_capture_final_gate_artifacts.sh and commit staging-smoke/*.json.",
                "Enable deploy/aspnet/nginx-surface-digests-shadow-example.conf one location block at a time.",
                "Capture PHP-vs-ASP.NET parity samples, then human APPROVED_TO_REMOVE_PHP_FALLBACK."
            ]);
    }

    private static LiveSurfaceLink Link(
        string hostClass,
        string surface,
        string url,
        string stackToday,
        string aspNetRouteHint,
        string notes) => new(hostClass, surface, url, stackToday, aspNetRouteHint, notes);
}
