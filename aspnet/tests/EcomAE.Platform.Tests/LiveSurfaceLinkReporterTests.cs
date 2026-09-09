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
        Assert.Contains(report.Links, link => link.Url == "https://www.ecomae.com/php-reference/cp");
        Assert.Contains(report.Links, link => link.Url == "https://www.ecomae.com/php-reference/erp");
        Assert.Contains(report.Links, link => link.Url == "https://www.epartscart.com/php-reference/cp");
        Assert.Contains(report.Links, link => link.Url == "https://www.epartscart.com/php-reference/erp");
        Assert.Contains(report.Links, link => link.HostClass == "tenant" && link.Url.Contains("electronicae.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "aspnet-diagnostics" && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/migration/php-reference-mode");
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
        Assert.Equal(132, report.Links.Count(link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && (link.AspNetRouteHint.StartsWith("/cp/", StringComparison.Ordinal)
                || link.AspNetRouteHint.StartsWith("/erp/", StringComparison.Ordinal)
                || link.AspNetRouteHint.StartsWith("/bos/", StringComparison.Ordinal))));
        Assert.Contains(report.CutoverRules, rule => rule.Contains("NO HALF-AND-HALF", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.CutoverRules, rule => rule.Contains("named live tenants", StringComparison.OrdinalIgnoreCase)
            && rule.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase));
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
        Assert.Equal(202, report.Links.Count(link => link.HostClass == "aspnet-presentation-preview"));
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
            && link.AspNetRouteHint == "/storefront/orders/pay-on-place");
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
            && link.AspNetRouteHint == "/erp/ajax/jw-repair-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/karat-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/rate-type-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/currency-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/diamond-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/design-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/pearl-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/color-stone-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/barcode-generate");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/tag-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/tag-sell");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/gold-scheme-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/gold-scheme-enroll");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/gold-scheme-pay");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/fix-unfix-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/fix-unfix-settle");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/barcode-purchase-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/barcode-purchase-sell");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/sla/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/tourist-refund/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/tourist-refund/validate");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/rfid/register");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/rfid/start-session");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/rfid/scan");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/gold-rate/set");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/aml/kyc-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/aml/alert-status");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/tickets/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/tickets/reply");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/projects/tasks/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/prj-task-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/projects/timesheets/log");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/prj-log-time");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/consolidations/figures/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/cons-figures-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/custom-shipping/delete");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/edit-lock/heartbeat");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/edit-lock/release");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/budgets/advance");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/automation/deactivate");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/bank-reconciliation/match");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/fin/periods/generate");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/recruitment/applicants/stage");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/opening/add-inv-line");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/opening/add-coa-line");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/opening/create-batch");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/uae-tax/ct-adjustments/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/contracts/sign");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/contracts/ocr");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/bos-intel/toggle");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/quality/ncr-update");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/order-planning/set-status");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/mfgr/planned/firm");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/mfgr/routes/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/mfgr-wc-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/mfgr/work-centers/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pm-listing-attach");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/pm/listings/attach");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pm-cheque-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/pm/cheques/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pm-toggle");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/pm/toggle");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pm-budget-line-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/pm/budget-lines/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pm-budget-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/pm/budgets/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pm-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/pm/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/uae-tax-legislation-checklist-set");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/uae-tax/legislation/checklist/set");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/print-designer-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/print-designer/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/tenant-config-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/tenant-config/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/docx-delete");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/doc-expiry/delete");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/fin-alloc-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/fin/alloc/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/docx-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/doc-expiry/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/cft-instrument-status");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/bank-instruments/status");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/cft-instrument-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/bank-instruments/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/cft-line-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/cash-forecast/lines/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/cft-forecast-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/cash-forecast/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/opl-params-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/order-planning/params/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/hrt-goal-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/performance/goals/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/hrt-review-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/performance/reviews/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bplan-position-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/budget-planning/positions/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/budget-planning/lines/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bplan-line-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/budget-planning/plans/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bplan-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/recruitment/applicants/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/hrt-applicant-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/recruitment/jobs/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/hrt-job-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/procurement/policies/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/proc-policy-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/procurement/categories/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/proc-category-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/quality/test-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/qm-test-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/quality/plan-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/qm-plan-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/security/users/assign-role");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rbac-user-role");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/security/roles/attach-duty");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rbac-role-duty");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/security/duties/attach-priv");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rbac-duty-priv");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/security/roles/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rbac-role-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/security/duties/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rbac-duty-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/security/privileges/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rbac-priv-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/retail/discounts/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rtl-discount-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/platform/jobs/run");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/plt-job-run");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/retail/assortments/set");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rtl-assortment-set");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/project-accounting/txns/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/prja-txn-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/cost-models/txns/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/costm-txn-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/integrations/events/raise");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/intg-event-raise");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/cost-models/items/set");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/costm-item-set");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/multi-entity/preference/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/multi-entity-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/integrations/subscriptions/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/intg-sub-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/agenda/events/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/agenda-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/guide/articles/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/kb-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/process-flow/processes/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pf-process-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/process-flow/steps/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pf-step-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/integrations/entities/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/intg-entity-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/platform/features/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/plt-feature-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/platform/jobs/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/plt-job-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/project-accounting/budgets/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/prja-budget-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/org/calendars/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/oa-calendar-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/org/holidays/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/oa-holiday-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/contacts/party-contacts/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/oa-contact-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/retail/channels/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/rtl-channel-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/electronic-reporting/fields/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/er-field-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/contacts/addresses/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/oa-address-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/electronic-reporting/formats/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/er-format-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/contacts/parties/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/oa-party-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/approvals/requests/raise");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bos-wf-raise-test");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/fiscal-years/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/fy-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/approvals/requests/decide");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bos-wf-decide");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/compliance/filings/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bos-compliance-file");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/process-flow/dept-heads/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pf-set-dept-head");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/compliance/retention/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bos-compliance-save-retention");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/approvals/rules/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bos-wf-save-rule");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/process-flow/cases/reassign");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/pf-case-reassign");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/compliance/obligations/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/bos-compliance-add-obligation");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/manufacturing/work-orders/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/mfg-wo-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/manufacturing/bom/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/mfg-bom-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/insurance/delete");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/ins-delete");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/insurance/docs/add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/ins-doc-add");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/insurance/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/ins-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/consolidations/ic/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/cons-ic-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/customer-groups/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/customer-groups/assign");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/report-scheduler/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/virtual-warehouses/create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/virtual-warehouses/transfer");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/metal-stock-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/fixing-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/voucher-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/petty-cash-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/tourist-vat-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/repair-receipt-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/repair-transfer-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/workshop-receive-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/repair-delivery-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/jewellery/stock-verify-save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/aftersales/rma-resolve");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/aftersales/warranty-register");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/aftersales/job-create");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/aftersales/job-add-line");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/aftersales/job-close");
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
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/wms/receive");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/wms/work/complete");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/payroll/generate");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/payroll-generate");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/procurement/requisitions/add-line");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/collections/cases/promise");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/collections/activity/log");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/collections/hold/set");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/collections/dunning/run");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/withholding/codes/save");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/withholding/txns/record");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/withholding/txns/certificate");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/payroll-pay");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/payroll/pay");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/payroll/update-days");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/payroll-update-days");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/ajax/sub-generate");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/subscriptions/generate");

        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/erp/marketing/create");
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
            && link.AspNetRouteHint == "/erp/tax-external-reporting-app");
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
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/order-statuses-app");
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
            && link.AspNetRouteHint == "/erp/report-center-app");
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
            && link.AspNetRouteHint == "/erp/marketing-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/workspace-favorites-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/fixed-assets-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/recruitment-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/performance-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/report-scheduler-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/project-accounting-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/product-info-app");
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
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_install_classic_entry_aspnet_primary.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_probe_classic_entry_aspnet_primary.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("TENANT_MIGRATION_SAFETY.md", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action =>
            action.Contains("ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW", StringComparison.Ordinal)
            || action.Contains("named live tenants", StringComparison.OrdinalIgnoreCase)
            || action.Contains("aspnet-zero-php-path", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link =>
            link.AspNetRouteHint == "/migration/live-tenant-presentation-lock");
        Assert.Contains(report.Links, link =>
            link.AspNetRouteHint == "/migration/aspnet-zero-php-path");
        Assert.Contains(report.CutoverRules, note => note.Contains("CONFIRMED", StringComparison.Ordinal)
            && note.Contains("ASP.NET", StringComparison.Ordinal)
            && note.Contains("reference", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.CutoverRules, note => note.Contains("NO HALF-AND-HALF", StringComparison.Ordinal));
        Assert.Contains(report.Links, link => link.HostClass == "tenant" && link.StackToday == "aspnet");
        Assert.DoesNotContain(report.Links, link => link.HostClass == "tenant" && link.StackToday == "php");
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.StackToday == "aspnet");
        Assert.DoesNotContain(report.Links, link => link.HostClass == "super-cp" && link.StackToday == "php");
        Assert.Contains(report.Links, link => link.HostClass == "industry-frontend" && link.StackToday == "aspnet");
        Assert.DoesNotContain(report.Links, link => link.HostClass == "industry-frontend" && link.StackToday == "php");
        Assert.Contains(report.Links, link => link.HostClass == "lifeos" && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/migration/all-sites-aspnet-primary");
        Assert.Contains(report.NextActions, action => action.Contains("FORCE_LIVE_ALL_SITES", StringComparison.Ordinal)
            || action.Contains("all-sites-aspnet-primary", StringComparison.Ordinal));
        Assert.Equal("catalogued-aspnet-primary-destination-php-reference-kept", report.Status);
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
