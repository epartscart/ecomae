using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpVsAspNetRemovalMatrixTests
{
    [Fact]
    public void RemovalFlagsStayLocked()
    {
        Assert.False(PhpVsAspNetRemovalMatrix.ReadyForPhpRemoval);
        Assert.False(PhpVsAspNetRemovalMatrix.PhpSourceDeletionAllowed);
        Assert.False(PhpVsAspNetRemovalMatrix.CutoverAllowed);
        Assert.Equal(0, PhpVsAspNetRemovalMatrix.AspNetInteractiveCompleteCount);

        var report = PhpVsAspNetRemovalMatrix.BuildReport();
        Assert.Equal(false, report["readyForPhpRemoval"]);
        Assert.Equal(false, report["phpSourceDeletionAllowed"]);
        Assert.Equal(false, report["cutoverAllowed"]);
        Assert.Equal(0, report["aspNetInteractiveCompleteCount"]);
        Assert.Equal(true, report["keepPhpProjectAvailable"]);
        Assert.Equal(EcomAeRoutes.PhpVsAspNetMatrix, report["endpoint"]);
    }

    [Fact]
    public void MatrixCoversStorefrontCpErpAndWrites()
    {
        var surfaces = PhpVsAspNetRemovalMatrix.Rows.Select(r => r.Surface).Distinct(StringComparer.Ordinal).ToHashSet();
        Assert.Contains("storefront", surfaces);
        Assert.Contains("cp", surfaces);
        Assert.Contains("erp", surfaces);
        Assert.Contains("writes", surfaces);
        Assert.True(PhpVsAspNetRemovalMatrix.Rows.Count >= 40);
        Assert.DoesNotContain(PhpVsAspNetRemovalMatrix.Rows, r => r.Status == "missing-app");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.WritesOwner == "php");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "sf-cart" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-orders" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "sf-returns" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-users" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-lang" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-channels" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-carriers" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-workshop" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-currencies" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-prices-edit" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-synonyms" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-crosses" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-catalogue" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-warranty-rma" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-vin-requests" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-payroll" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-inventory" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-workspace-favorites" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-jw-repairs" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-prices-upload" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-storages" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-quote-requests" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-vendor-approvals" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-api-clients" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-price-storage-rules" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-content-pages" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-offices-cash" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-wms-locations" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-subscriptions" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-contracts" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-workflow" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-collections-cases" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-procurement-reqs" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-ins-claims" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-vat-refund-status" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-pf-case-cancel" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-bos-wf-disable-rule" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-bos-compliance-disable" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-hr-leave-expense" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-cons-deletes" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-fy-reopen-period" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-wht-settle" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-multi-entity" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "write-storefront-cart" && r.WritesOwner == "php");
    }

    [Theory]
    [InlineData("/CP/shop/finance", "/erp")]
    [InlineData("/CP/shop/finance/epc_credit_limit", "/cp/credit-limits-app")]
    [InlineData("/CP/shop/finance/epc_po_approval", "/cp/po-approvals-app")]
    [InlineData("/CP/shop/finance/epc_warranty_rma", "/cp/returns-rma-app")]
    [InlineData("/CP/shop/finance/epc_wps_payroll", "/erp/payroll-app")]
    [InlineData("/CP/shop/finance/epc_order_erp_pipeline", "/erp/order-pipeline-app")]
    [InlineData("/CP/shop/finance/epc_inventory_forecast", "/erp/inventory-forecast-app")]
    [InlineData("/CP/shop/finance/epc_multi_entity", "/erp/multi-entity-app")]
    [InlineData("/CP/shop/finance/epc_multi_currency_gl", "/erp/multi-currency-gl-app")]
    public void FinanceHrefsMapToDedicatedAppsNotBareHub(string phpHref, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.MapCpPhpPath(phpHref));
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref));
    }

    [Fact]
    public void NewFinanceDigestAppsExist()
    {
        var root = FindRepoRoot();
        foreach (var name in new[]
        {
            "ErpOrderPipelineApp.razor",
            "ErpInventoryForecastApp.razor",
            "ErpMultiEntityApp.razor",
            "ErpMultiCurrencyGlApp.razor"
        })
        {
            var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", name);
            Assert.True(File.Exists(path), path);
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("Compare PHP reference", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LiveWriteAppsUseNativeFormsNotBlazorHandlers()
    {
        var root = FindRepoRoot();
        var cases = new (string File, string Needle)[]
        {
            ("StorefrontCartApp.razor", "/storefront/cart/change-count-need"),
            ("ErpPayrollApp.razor", "/erp/ajax/payroll-approve"),
            ("ErpInventoryForecastApp.razor", "/erp/inventory-forecast/recompute"),
            ("ErpMultiEntityApp.razor", "/erp/multi-entity/write"),
            ("CpCreditLimitsApp.razor", "/cp/credit-limits/set"),
            ("CpPoApprovalsApp.razor", "/cp/po-approvals/approve"),
            ("CpOrdersApp.razor", "/cp/orders/set-item-status"),
            ("CpOrdersApp.razor", "/cp/orders/set-items-status"),
            ("CpOrdersApp.razor", "/cp/orders/add-comment"),
            ("CpOrdersApp.razor", "/cp/orders/fulfillment-set-stage"),
            ("StorefrontReturnsApp.razor", "ReturnsMessageHref"),
            ("StorefrontReturnsApp.razor", "ReturnsCreateHref"),
            ("CpOrdersApp.razor", "/cp/orders/update-item"),
            ("CpUsersApp.razor", "/cp/users/set-comment"),
            ("CpUsersApp.razor", "/cp/users/set-unlocked"),
            ("CpUsersApp.razor", "/cp/vendors/approvals"),
            ("CpQuoteRequestsApp.razor", "/cp/quote-requests/note"),
            ("CpQuoteRequestsApp.razor", "/cp/quote-requests/send"),
            ("CpApiClientsApp.razor", "/cp/api-clients/toggle"),
            ("CpPriceListsApp.razor", "/cp/prices/storage-rules"),
            ("CpPagesApp.razor", "/cp/content/published"),
            ("CpPagesApp.razor", "/cp/content/main"),
            ("ErpCashAccountsApp.razor", "/erp/offices-cash/add"),
            ("ErpCashAccountsApp.razor", "/erp/offices-cash/codes/delete"),
            ("CpWarehouseWmsApp.razor", "/erp/wms/locations/delete"),
            ("CpWarehouseWmsApp.razor", "/erp/wms/waves/release"),
            ("ErpSalesOrdersApp.razor", "/erp/subscriptions/status"),
            ("ErpContractsApp.razor", "/erp/ajax/ctr-status"),
            ("ErpWorkflowApp.razor", "/erp/workflow/status"),
            ("CpCollectionsDunningApp.razor", "/erp/collections/cases/status"),
            ("CpPurchaseRequestsApp.razor", "/erp/procurement/requisitions/submit"),
            ("CpPurchaseRequestsApp.razor", "/erp/procurement/requisitions/decision"),
            ("CpPurchaseRequestsApp.razor", "/erp/ajax/proc-req-convert"),
            ("CpInsuranceComplianceApp.razor", "/erp/ajax/ins-claim-status"),
            ("CpInsuranceComplianceApp.razor", "/erp/ajax/ins-doc-delete"),
            ("CpHrOverviewApp.razor", "/erp/ajax/hr-leave-status"),
            ("CpHrOverviewApp.razor", "/erp/ajax/hr-expense-status"),
            ("CpConsolidationsApp.razor", "/erp/ajax/cons-entity-delete"),
            ("CpConsolidationsApp.razor", "/erp/ajax/cons-ic-delete"),
            ("ErpVatApp.razor", "/erp/ajax/bos-vat-refund-status"),
            ("ErpSalesOrdersApp.razor", "/erp/ajax/sub-invoice-paid"),
            ("ErpProcessFlowTasksApp.razor", "/erp/ajax/pf-case-cancel"),
            ("ErpProcessFlowTasksApp.razor", "/erp/ajax/pf-step-delete"),
            ("ErpApprovalsApp.razor", "/erp/ajax/bos-wf-disable-rule"),
            ("CpSoc2ComplianceApp.razor", "/erp/ajax/bos-compliance-disable-obligation"),
            ("ErpPeriodCloseApp.razor", "/erp/ajax/fy-reopen"),
            ("ErpPeriodCloseApp.razor", "/erp/ajax/fy-period-status"),
            ("ErpWithholdingApp.razor", "/erp/ajax/wht-settle"),
            ("CpLanguagesApp.razor", "/cp/lang/set-is-custom"),
            ("CpLanguagesApp.razor", "/cp/lang/set-is-error"),
            ("CpLanguagesApp.razor", "/cp/lang/set-same"),
            ("CpLanguagesApp.razor", "/cp/lang/set-used-found"),
            ("CpLanguagesApp.razor", "/cp/lang/save-translation"),
            ("CpLanguagesApp.razor", "/cp/lang/save-description"),
            ("CpLanguagesApp.razor", "/cp/lang/delete-not-used"),
            ("CpMarketplaceChannelsApp.razor", "/cp/channels/write"),
            ("CpCarriersApp.razor", "/cp/logistics/write"),
            ("CpWorkshopApp.razor", "/cp/workshop/write"),
            ("CpSynonymsApp.razor", "/cp/synonyms/write"),
            ("CpCrossesApp.razor", "/cp/crosses/write"),
            ("CpPricesEditApp.razor", "/cp/prices-edit/write"),
            ("CpCurrenciesApp.razor", "/cp/currencies/set-rate"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/set-min-limit"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/templates-actions"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/erp-fav-add"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/erp-fav-remove"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/shortcut-delete"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/shortcut-delete-key"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/shortcut-reset"),
            ("CpStoragesApp.razor", "/cp/storages/groups"),
            ("CpPricesUploadApp.razor", "/cp/prices/complete-session"),
            ("ErpPayrollApp.razor", "/erp/ajax/hr-update-days"),
            ("CpHrOverviewApp.razor", "/erp/ajax/hr-update-days"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-set-reorder-level"),
            ("CpJewelleryRepairsApp.razor", "/erp/ajax/jw-repair-update-status"),
            ("CpReturnsRmaApp.razor", "/cp/returns/action"),
            ("CpSystemRequestsApp.razor", "/cp/requests/set-vin-viewed"),
            ("StorefrontWishlistApp.razor", "WishlistRemoveHref"),
            ("StorefrontCompareApp.razor", "CompareRemoveHref"),
            ("StorefrontProfileApp.razor", "ProfileWriteHref"),
        };
        foreach (var (name, needle) in cases)
        {
            var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", name);
            var text = File.ReadAllText(path);
            Assert.Contains(needle, text, StringComparison.Ordinal);
            Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@onchange", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MappedMatrixRowsAgreeWithLinkMap()
    {
        foreach (var row in PhpVsAspNetRemovalMatrix.Rows.Where(r =>
                     r.PhpHref.StartsWith("/CP/", StringComparison.OrdinalIgnoreCase)
                     || r.PhpHref.StartsWith("/ERP/", StringComparison.OrdinalIgnoreCase)))
        {
            var mapped = PhpSurfaceLinkMap.AspNetPrimaryHref(row.PhpHref);
            var expectedPath = row.AspNetRoute.Split('?', 2)[0];
            Assert.StartsWith(expectedPath, mapped, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root from " + AppContext.BaseDirectory);
    }
}
