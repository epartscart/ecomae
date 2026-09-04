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
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-payroll" && r.WritesOwner == "aspnet");
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
            ("CpCreditLimitsApp.razor", "/cp/credit-limits/set"),
            ("CpPoApprovalsApp.razor", "/cp/po-approvals/approve"),
            ("CpOrdersApp.razor", "/cp/orders/set-item-status"),
            ("CpOrdersApp.razor", "/cp/orders/set-items-status"),
            ("CpOrdersApp.razor", "/cp/orders/add-comment"),
            ("CpOrdersApp.razor", "/cp/orders/fulfillment-set-stage"),
            ("StorefrontReturnsApp.razor", "ReturnsMessageHref"),
            ("StorefrontReturnsApp.razor", "ReturnsCreateHref"),
            ("CpOrdersApp.razor", "/cp/orders/update-item"),
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
