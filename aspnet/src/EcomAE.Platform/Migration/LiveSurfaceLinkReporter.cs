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
            Link("aspnet-diagnostics", "Surface field parity", "https://www.ecomae.com/migration/surface-field-parity", "aspnet", "/migration/surface-field-parity", "Field/function contracts for all wired digests + catalog routes."),
            Link("aspnet-diagnostics", "CP parity board", "https://www.ecomae.com/cp/parity", "aspnet", "/cp/parity", "Control Panel RemainingGaps / verified capabilities (loopback 127.0.0.1:5100 or allowlisted)."),
            Link("aspnet-diagnostics", "ERP parity board", "https://www.ecomae.com/erp/parity", "aspnet", "/erp/parity", "ERP RemainingGaps / verified capabilities (loopback or allowlisted)."),
            Link("aspnet-diagnostics", "BOS parity board", "https://www.ecomae.com/bos/parity", "aspnet", "/bos/parity", "BOS RemainingGaps / verified capabilities (loopback or allowlisted)."),
            Link("aspnet-diagnostics", "Storefront parity board", "https://www.ecomae.com/storefront/parity", "aspnet", "/storefront/parity", "Storefront RemainingGaps (loopback or allowlisted)."),
            Link("aspnet-diagnostics", "Data parity board", "https://www.ecomae.com/migration/data-parity", "aspnet", "/migration/data-parity", "Production data source contracts; cutover blocked."),
            Link("aspnet-diagnostics", "Session parity board", "https://www.ecomae.com/auth/session/parity", "aspnet", "/auth/session/parity", "Legacy session RemainingGaps (loopback; use /auth/session/probe with cookie)."),
            Link("aspnet-diagnostics", "API client parity board", "https://www.ecomae.com/auth/api-client/parity", "aspnet", "/auth/api-client/parity", "epc_api_clients auth RemainingGaps (loopback)."),
            Link("aspnet-diagnostics", "Catalog parity board", "https://www.ecomae.com/api/v1/catalog/parity", "aspnet", "/api/v1/catalog/parity", "Catalog RemainingGaps (loopback or after catalog shadow)."),
            Link("aspnet-diagnostics", "Price parity board", "https://www.ecomae.com/api/v1/price/parity", "aspnet", "/api/v1/price/parity", "Price lookup RemainingGaps (loopback or after price shadow)."),
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

            // Exact-route ASP.NET digests (enable one location= at a time; Cookie proxy)
            Link("aspnet-exact-route-shadow-live", "CP dashboard digest", "https://www.ecomae.com/cp/dashboard-summary", "aspnet", "/cp/dashboard-summary", "Live exact-route nginx shadow on www (unauth 401 unauthorized; admin cookie for 200)."),
            Link("aspnet-exact-route-shadow-live", "CP tenants digest", "https://www.ecomae.com/cp/tenants", "aspnet", "/cp/tenants", "Live exact-route nginx shadow on www (unauth 401 unauthorized; admin CP capability for 200)."),
            Link("aspnet-exact-route-shadow-live", "CP users digest", "https://www.ecomae.com/cp/users", "aspnet", "/cp/users", "Live exact-route nginx shadow on www (unauth 401 unauthorized). Installer may briefly see CDN-cached PHP HTML 200 — re-probe public. Surface digests 3/30."),
            Link("aspnet-digest-pending-shadow", "CP groups digest", "https://www.ecomae.com/cp/groups", "php-fallback", "/cp/groups", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "CP modules digest", "https://www.ecomae.com/cp/modules", "php-fallback", "/cp/modules", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "CP menus digest", "https://www.ecomae.com/cp/menus", "php-fallback", "/cp/menus", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "CP pages digest", "https://www.ecomae.com/cp/pages", "php-fallback", "/cp/pages", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "CP currencies digest", "https://www.ecomae.com/cp/currencies", "php-fallback", "/cp/currencies", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "CP api-clients digest", "https://www.ecomae.com/cp/api-clients", "php-fallback", "/cp/api-clients", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "CP config-items digest", "https://www.ecomae.com/cp/config-items", "php-fallback", "/cp/config-items", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "CP admin-sessions digest", "https://www.ecomae.com/cp/admin-sessions", "php-fallback", "/cp/admin-sessions", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "CP storages digest", "https://www.ecomae.com/cp/storages", "php-fallback", "/cp/storages", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP dashboard digest", "https://www.ecomae.com/erp/dashboard-summary", "php-fallback", "/erp/dashboard-summary", "Needs exact-route nginx shadow after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP accounts-summary digest", "https://www.ecomae.com/erp/accounts-summary", "php-fallback", "/erp/accounts-summary", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP suppliers digest", "https://www.ecomae.com/erp/suppliers", "php-fallback", "/erp/suppliers", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP purchases digest", "https://www.ecomae.com/erp/purchases", "php-fallback", "/erp/purchases", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP cash-accounts digest", "https://www.ecomae.com/erp/cash-accounts", "php-fallback", "/erp/cash-accounts", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP cash-entries digest", "https://www.ecomae.com/erp/cash-entries", "php-fallback", "/erp/cash-entries", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP COA accounts digest", "https://www.ecomae.com/erp/coa-accounts", "php-fallback", "/erp/coa-accounts", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP warehouses digest", "https://www.ecomae.com/erp/warehouses", "php-fallback", "/erp/warehouses", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP sales-orders digest", "https://www.ecomae.com/erp/sales-orders", "php-fallback", "/erp/sales-orders", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP purchase-orders digest", "https://www.ecomae.com/erp/purchase-orders", "php-fallback", "/erp/purchase-orders", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP inventory-stock digest", "https://www.ecomae.com/erp/inventory-stock", "php-fallback", "/erp/inventory-stock", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP invoices digest", "https://www.ecomae.com/erp/invoices", "php-fallback", "/erp/invoices", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "ERP GL journals digest", "https://www.ecomae.com/erp/gl-journals", "php-fallback", "/erp/gl-journals", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "BOS fleet digest", "https://www.ecomae.com/bos/fleet-summary", "php-fallback", "/bos/fleet-summary", "Needs exact-route nginx shadow after smoke."),
            Link("aspnet-digest-pending-shadow", "BOS tenants digest", "https://www.ecomae.com/bos/tenants", "php-fallback", "/bos/tenants", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "BOS fleet-health digest", "https://www.ecomae.com/bos/fleet-health", "php-fallback", "/bos/fleet-health", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "BOS fleet-readiness digest", "https://www.ecomae.com/bos/fleet-readiness", "php-fallback", "/bos/fleet-readiness", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-digest-pending-shadow", "BOS audit-log digest", "https://www.ecomae.com/bos/audit-log", "php-fallback", "/bos/audit-log", "Covered by nginx-surface-digests-shadow-example.conf after smoke."),
            Link("aspnet-exact-route-shadow-live", "Catalog status", "https://www.ecomae.com/api/v1/catalog/status", "aspnet", "/api/v1/catalog/status", "Live exact-route nginx shadow on www (401/200 ASP.NET JSON). PHP remains for chrome/digests."),
            Link("aspnet-exact-route-shadow-live", "Catalog manufacturers", "https://www.ecomae.com/api/v1/catalog/manufacturers?section=passenger", "aspnet", "/api/v1/catalog/manufacturers", "Live exact-route nginx shadow on www (401/200; section=passenger). Dual-sample compare still recommended before more list routes."),
            Link("aspnet-exact-route-shadow-live", "Catalog models", "https://www.ecomae.com/api/v1/catalog/models?section=passenger&mfa_id=1", "aspnet", "/api/v1/catalog/models", "Live exact-route nginx shadow on www (unauth 401; auth needs section+mfa_id>0 for 200; row keys MFA_ID). Dual-sample compare still recommended."),
            Link("aspnet-exact-route-shadow-live", "Catalog modifications", "https://www.ecomae.com/api/v1/catalog/modifications?section=passenger&ms_id=1", "aspnet", "/api/v1/catalog/modifications", "Live exact-route nginx shadow on www (unauth 401; auth needs section+ms_id>0 for 200; row keys MS_ID). Dual-sample compare still recommended."),
            Link("aspnet-exact-route-shadow-live", "Catalog brands", "https://www.ecomae.com/api/v1/catalog/brands", "aspnet", "/api/v1/catalog/brands", "Live exact-route nginx shadow on www (401/200 ASP.NET JSON; no mfa_id/ms_id required). Dual-sample compare still recommended."),
            Link("aspnet-exact-route-shadow-live", "Catalog suppliers", "https://www.ecomae.com/api/v1/catalog/suppliers", "aspnet", "/api/v1/catalog/suppliers", "Live exact-route nginx shadow on www (401/200; brands-table alias, 1314 rows). Dual-sample compare still recommended."),
            Link("aspnet-exact-route-shadow-live", "Catalog VIN", "https://www.ecomae.com/api/v1/catalog/vin?vin=WBAXG1103CDW29096", "aspnet", "/api/v1/catalog/vin", "Live exact-route nginx shadow on www (unauth 401; auth cache hit 200 / miss 404 vin_cache_miss). PHP/UMAPI remains for live fills."),
            Link("aspnet-exact-route-shadow-live", "Catalog engines", "https://www.ecomae.com/api/v1/catalog/engines?section=passenger&mfa_id=16", "aspnet", "/api/v1/catalog/engines", "Live exact-route nginx shadow on www (unauth 401; auth needs section+mfa_id; cache miss 404 remains PHP/UMAPI)."),
            Link("aspnet-exact-route-shadow-live", "Catalog analogs", "https://www.ecomae.com/api/v1/catalog/analogs?section=passenger&article=0986424590&brand=BOSCH", "aspnet", "/api/v1/catalog/analogs", "Live exact-route nginx shadow on www (unauth 401; auth needs article+brand; cache miss 404 remains PHP/UMAPI)."),
            Link("aspnet-exact-route-shadow-live", "Catalog article-brands", "https://www.ecomae.com/api/v1/catalog/article-brands?section=passenger&article=0986424590", "aspnet", "/api/v1/catalog/article-brands", "Live exact-route nginx shadow on www (unauth 401; auth needs article; UMAPI action=brands cache; miss 404 remains PHP)."),
            Link("aspnet-exact-route-shadow-live", "Catalog categories", "https://www.ecomae.com/api/v1/catalog/categories?section=passenger&id=1", "aspnet", "/api/v1/catalog/categories", "Live exact-route nginx shadow on www (unauth 401; auth optional id; cache miss 404 remains PHP/UMAPI)."),
            Link("aspnet-exact-route-shadow-live", "Catalog products", "https://www.ecomae.com/api/v1/catalog/products?section=passenger&category_id=1&id=1", "aspnet", "/api/v1/catalog/products", "Live exact-route nginx shadow on www (unauth 401; auth needs category/id params for cache hit; miss 404 remains PHP/UMAPI)."),
            Link("aspnet-exact-route-shadow-live", "Catalog engine-search", "https://www.ecomae.com/api/v1/catalog/engine-search?section=passenger&code=3L&mfa_id=0", "aspnet", "/api/v1/catalog/engine-search", "Live exact-route nginx shadow on www (unauth 401; auth needs engine_search in allowed_actions_json — smoke key may 403 until re-issue; miss 404 remains PHP)."),
            Link("aspnet-exact-route-shadow-live", "Catalog article-links", "https://www.ecomae.com/api/v1/catalog/article-links?section=passenger&id=123", "aspnet", "/api/v1/catalog/article-links", "Live exact-route nginx shadow on www (unauth 401; auth uses catalog action=article; offline-cache miss 404 remains PHP/UMAPI). Installer may FAIL on CDN lag while location= is already inserted — re-probe public."),
            Link("aspnet-exact-route-shadow-live", "Catalog article", "https://www.ecomae.com/api/v1/catalog/article?section=passenger&id=123", "aspnet", "/api/v1/catalog/article", "Live exact-route nginx shadow on www (unauth 401; auth needs id + action=article; offline-cache miss 404 remains PHP/UMAPI). Exact-match installer fix required so article is not confused with article-links."),
            Link("aspnet-exact-route-shadow-live", "Catalog articles", "https://www.ecomae.com/api/v1/catalog/articles?section=passenger&CATEGORY_ID=1", "aspnet", "/api/v1/catalog/articles", "Live exact-route nginx shadow on www (unauth 401; auth action=articles; opportunistic cache, miss 404 remains PHP/UMAPI)."),
            Link("aspnet-exact-route-shadow-live", "Catalog engine", "https://www.ecomae.com/api/v1/catalog/engine?section=passenger&id=1", "aspnet", "/api/v1/catalog/engine", "Live exact-route nginx shadow on www (unauth 401; auth uses engines action; offline-cache miss 404 remains PHP/UMAPI). Exact-match installer avoids false hit on engines/engine-search."),
            Link("aspnet-exact-route-shadow-live", "Catalog brand-parts", "https://www.ecomae.com/api/v1/catalog/brand-parts?section=passenger&brand=BOSCH", "aspnet", "/api/v1/catalog/brand-parts", "Live exact-route nginx shadow on www (unauth 401; auth needs brand/params; miss 404 remains PHP). Completes wired catalog API exact-route set (18/18)."),
            Link("aspnet-digest-pending-shadow", "Storefront account summary", "https://www.ecomae.com/storefront/account-summary", "php-fallback", "/storefront/account-summary", "Optional customer digest; needs ECOMAE_CUSTOMER_COOKIE_* + nginx-storefront-digests-shadow-example.conf."),
            Link("aspnet-digest-pending-shadow", "Storefront orders", "https://www.ecomae.com/storefront/orders", "php-fallback", "/storefront/orders", "Optional customer digest; not required for ReadyToRemovePhp."),
            Link("aspnet-digest-pending-shadow", "Storefront garage", "https://www.ecomae.com/storefront/garage", "php-fallback", "/storefront/garage", "Optional customer digest after smoke."),
            Link("aspnet-digest-pending-shadow", "Storefront profile", "https://www.ecomae.com/storefront/profile", "php-fallback", "/storefront/profile", "Optional customer digest after smoke.")
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
                "Diagnose: bash scripts/cloudpanel_diagnose_smoke_db.sh — then apply DDL (clpctl) or use_php_dp_config_as_tenant_registry.sh if CREATE denied.",
                "On CloudPanel: ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh",
                "Then: ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES bash scripts/cloudpanel_issue_smoke_credentials.sh",
                "Validate (redacted): bash scripts/cloudpanel_validate_final_gate_env.sh (or bash scripts/cloudpanel_prepare_smoke_secrets.sh).",
                "source /etc/ecomae-aspnet/platform.env && bash scripts/cloudpanel_capture_final_gate_artifacts.sh && bash scripts/cloudpanel_commit_final_gate_smoke.sh",
                "Optional storefront digests: set ECOMAE_CUSTOMER_COOKIE_HEADER=session=...; u_id=<digits> (not required for ReadyToRemovePhp).",
                "Auth chain probe: source /etc/ecomae-aspnet/platform.env && bash scripts/cloudpanel_probe_catalog_vehicle_chain.sh",
                "Warm VIN probe: bash scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh vin",
                "Warm UMAPI cache: bash scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh umapi engine_search",
                "Warm UMAPI cache: bash scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh umapi article_links",
                "Warm UMAPI cache: bash scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh umapi article",
                "If engine-search/article auth 403 action_not_allowed: re-issue smoke creds (allowlist now includes engine_search+article) via ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh",
                "Wired catalog exact-routes complete (18/18). Surface digests: 3/30 live (cp/dashboard-summary, cp/tenants, cp/users).",
                "Next exact-route: ECOMAE_CONFIRM_INSTALL_EXACT_ROUTE_SHADOW=YES bash scripts/cloudpanel_install_exact_route_shadow.sh /cp/groups",
                "Then continue digests from deploy/aspnet/nginx-surface-digests-shadow-example.conf (modules, menus, …) one location= at a time. Never broad /cp|/erp|/bos.",
                "Dual-sample: python3 scripts/compare_catalog_list_parity.py manufacturers|models|modifications|brands|suppliers php.json aspnet.json — PHP chrome stays until human APPROVED_TO_REMOVE_PHP_FALLBACK."
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
