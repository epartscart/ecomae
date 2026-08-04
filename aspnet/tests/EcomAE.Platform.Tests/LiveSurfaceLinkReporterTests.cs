using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LiveSurfaceLinkReporterTests
{
    [Fact]
    public void BuildReportCataloguesSuperCpTenantAndAspNetDiagnostics()
    {
        var report = new LiveSurfaceLinkReporter().BuildReport();

        Assert.Equal("www.ecomae.com", report.PlatformHost);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.True(report.Links.Count >= 109);
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/BOS/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/CP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/ERP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "tenant" && link.Url.Contains("electronicae.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "aspnet-diagnostics" && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/migration/surface-field-parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/cp/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/erp/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/bos/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/auth/session/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/auth/api-client/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/migration/data-parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/models");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/brand-parts");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/article-brands");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/engine-search");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/cp/groups");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/cp/users");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/erp/gl-journals");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/erp/inventory-stock");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/bos/tenants");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/bos/audit-log");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/storefront/account-summary");
        Assert.Contains(report.Links, link => link.Surface.Contains("Price lookup", StringComparison.OrdinalIgnoreCase) && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/status"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/manufacturers"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/models"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/modifications"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/brands"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/suppliers"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/vin"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/engines"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/analogs"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/article-brands"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/categories"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/products"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/engine-search"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/article-links"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/article"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/articles"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/engine"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/brand-parts"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/dashboard-summary"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/tenants"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/users"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/groups"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/modules"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/erp/dashboard-summary"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/bos/audit-log"
            && link.StackToday == "aspnet");
        Assert.Equal(128, report.Links.Count(link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && (link.AspNetRouteHint.StartsWith("/cp/", StringComparison.Ordinal)
                || link.AspNetRouteHint.StartsWith("/erp/", StringComparison.Ordinal)
                || link.AspNetRouteHint.StartsWith("/bos/", StringComparison.Ordinal))));
        Assert.Contains(report.CutoverRules, rule => rule.Contains("Broad", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.CutoverRules, rule => rule.Contains("tenant", StringComparison.OrdinalIgnoreCase)
            && rule.Contains("PHP", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/migration/console"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/app"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/app"
            && link.StackToday == "aspnet");
        Assert.Equal(4, report.Links.Count(link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint.StartsWith("/storefront/", StringComparison.Ordinal)));
        Assert.Equal(179, report.Links.Count(link => link.HostClass == "aspnet-presentation-preview"));
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/on-premises-app"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/migration/on-premises-parity");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/on-premises/license-activate-dry-run");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/on-premises/licenses");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/storefront/quotes/add-manual");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/storefront/garage/check-car");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/cp/orders/fulfillment-set-stage");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/cp/orders/fulfillment-advance");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/purchases/amend");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/sales-orders/delete");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/customers/master-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/aftersales/rma-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/cp/orders/refresh-item-cost");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/periods/soft-close");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/fiscal/set-lock");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/workflow/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/gl-journals/post-sales");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/terms");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/cookie-policy");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/security-policy");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/right-to-use");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/trademark");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/copyright");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/data-protection");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/acceptable-use");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/confidentiality");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/intellectual-property");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/blockchain-disclaimer");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/dmca");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/brochure-cp");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/marketing/app"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/migration/marketing-presentation-lock");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/audit-trail-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/doc-expiry-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/tenant-config-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/jewellery-stock-verification-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/audit-trail");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/doc-expiry");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/tenant-config");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/jewellery-stock-verification");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/tax-external-reporting-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/po-approvals-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/finance-close-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/jewellery-fixing-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/tax-external-reporting");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/po-approvals");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/finance-close");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/jewellery-fixing");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/geo-regions-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/geo-regions");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/product-filters-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/product-filters");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/search-tabs-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/search-tabs");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/system-requests-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/system-requests");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/bos/audit-log-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/tenants-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/currencies-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/storages-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/admin-sessions-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/api-clients-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/config-items-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/orders");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/abandoned-carts-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/abandoned-carts");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/users-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/groups-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/modules-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/pages-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/menus-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/sales-orders-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/purchase-orders-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/invoices-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/cash-accounts-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/coa-accounts-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/gl-journals-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/warehouses-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/suppliers-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/purchases-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/storefront/search-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/storefront/cart-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/storefront/checkout-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/storefront/orders-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/storefront/garage-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/storefront/profile-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/storefront/account-summary-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/inventory-stock-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/bos/tenants-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/bos/fleet-health-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/bos/fleet-readiness-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/accounts-summary-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/dashboard-summary-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/dashboard-summary-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/bos/fleet-summary-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/cash-entries-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/budgets-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/carriers-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/payment-gateways-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/workflows-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/purchase-requests-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/promotions-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/crm-opportunities-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/integrations-app");

        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/bank-reconciliation-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/stock-transfers-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/sales-quotations-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/workspace-favorites-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/fixed-assets-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/page-builder-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/product-catalogue-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/platform-governance-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/einvoice-documents-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/jewellery-repairs-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/crm-tickets-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/marketing-growth-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/soc2-compliance-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/cost-models-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/fin-advanced-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/blockchain-proofs-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/landed-cost-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/warehouse-wms-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/ai-service-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/returns-rma-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/isolation-audit-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/aml-compliance-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/jewellery-masters-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/consolidations-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/crm-activities-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/auth-mfa-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/electronic-reporting-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/collections-dunning-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/marketplace-channels-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/demand-intelligence-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/credit-limits-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/insurance-compliance-app");
        Assert.Equal(4, report.Links.Count(link => link.HostClass == "aspnet-login-bridge"));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_ensure_epc_api_clients_table.sh", StringComparison.Ordinal));

        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_capture_final_gate_artifacts.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_probe_catalog_vehicle_chain.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_list_warm_catalog_vehicle_ids.sh vin", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("umapi engine_search", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("umapi article_links", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("umapi article", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_probe_catalog_miss_path.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("compare_catalog_miss_dual_samples.py", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("catalog-miss-fill", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("miss-fill-dry-run-report.json", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_capture_hybrid_ui_dual_samples.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("compare_hybrid_ui_dual_samples.py", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("hybrid-ui-dual-samples", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("action_not_allowed", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("Wired catalog exact-routes complete", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("Surface digests: wired+live 128/128", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("Storefront digests: wired+live 6/6", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_install_storefront_digest_shadows.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_probe_storefront_digest_shadows.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("/migration/console", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("pairsChecked=185", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("/cp/login", StringComparison.Ordinal)
            || action.Contains("/cp|/erp|/bos|/storefront/{app,login}", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_install_presentation_app_shadows.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("SecretSuccession", StringComparison.Ordinal)
            || action.Contains("secret_succession", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("CHROME_PARITY_GAP_MATRIX", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("RELEASE_OWNER_APPROVAL.md", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_verify_tenant_hosts_still_php.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_probe_live_tenant_php_chrome.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("TENANT_MIGRATION_SAFETY.md", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action =>
            action.Contains("ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW", StringComparison.Ordinal)
            || action.Contains("named live tenants", StringComparison.OrdinalIgnoreCase)
            || action.Contains("aspnet-zero-php-path", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link =>
            link.AspNetRouteHint == "/migration/live-tenant-presentation-lock");
        Assert.Contains(report.Links, link =>
            link.AspNetRouteHint == "/migration/aspnet-zero-php-path");
        Assert.Contains(report.CutoverRules, note => note.Contains("PARITY GATE", StringComparison.Ordinal)
            && note.Contains("100% ASP.NET", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteLiveSurfaceLinkProbeSnapshotWhenRequested()
    {
        // ECOMAE_WRITE_LIVE_SURFACE_LINK_PROBE=1 dotnet test --filter WriteLiveSurfaceLinkProbeSnapshotWhenRequested
        if (!string.Equals(Environment.GetEnvironmentVariable("ECOMAE_WRITE_LIVE_SURFACE_LINK_PROBE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var report = new LiveSurfaceLinkReporter().BuildReport();
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }) + "\n";

        var root = FindRepoRoot();
        var path = Path.Combine(root, "docs", "migration", "evidence", "decommission", "public-probes", "www-live-surface-links.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "run_zero_php_final_gate_checklist.sh")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
