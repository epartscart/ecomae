using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the ~2s ERP first-paint budget: request-cached chrome, 2s SQL caps,
/// slimmer workspace home, and clamped list/picker scans.
/// </summary>
public sealed class ErpFirstPaintBudgetTests
{
    [Fact]
    public void BudgetConstants_AreTwoSecondsAndSmallLists()
    {
        Assert.Equal(2, ErpFirstPaint.CommandTimeoutSeconds);
        Assert.Equal(50, ErpFirstPaint.ListLimit);
        Assert.Equal(80, ErpFirstPaint.PickerLimit);
        Assert.Equal(200, ErpFirstPaint.AgingScanLimit);
        Assert.Equal(5, ErpFirstPaint.PhpBridgeTimeoutSeconds);
    }

    [Fact]
    public void IsErpPath_MatchesErpSurfacesOnly()
    {
        Assert.True(ErpFirstPaint.IsErpPath("/erp"));
        Assert.True(ErpFirstPaint.IsErpPath("/erp/sales-orders-app"));
        Assert.True(ErpFirstPaint.IsErpPath("/ERP/quality-app"));
        Assert.False(ErpFirstPaint.IsErpPath("/cp/users"));
        Assert.False(ErpFirstPaint.IsErpPath("/en/parts/BOSCH/0986424590"));
    }

    [Fact]
    public void IsPaintPath_CoversCpBosAndStorefront()
    {
        Assert.True(ErpFirstPaint.IsPaintPath("/"));
        Assert.True(ErpFirstPaint.IsPaintPath("/erp/sales-orders-app"));
        Assert.True(ErpFirstPaint.IsPaintPath("/cp/orders"));
        Assert.True(ErpFirstPaint.IsPaintPath("/CP/users"));
        Assert.True(ErpFirstPaint.IsPaintPath("/bos/fleet-summary-app"));
        Assert.True(ErpFirstPaint.IsPaintPath("/storefront/app"));
        Assert.True(ErpFirstPaint.IsPaintPath("/en"));
        Assert.True(ErpFirstPaint.IsPaintPath("/en/parts/BOSCH/0986424590"));
        Assert.True(ErpFirstPaint.IsPaintPath("/ar/shop/part_search"));
        Assert.False(ErpFirstPaint.IsPaintPath("/migration/php-reference-mode"));
        Assert.False(ErpFirstPaint.IsPaintPath("/php-reference/en"));
        Assert.False(ErpFirstPaint.IsPaintPath("/platform-assets/x.css"));
    }

    [Fact]
    public void CompaniesDigest_UsesRequestCache()
    {
        var reporter = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("ErpFirstPaint.CompaniesCacheKey", reporter, StringComparison.Ordinal);
        Assert.Contains("TryGetRequestCache", reporter, StringComparison.Ordinal);
        Assert.Contains("SetRequestCache", reporter, StringComparison.Ordinal);
        Assert.Contains("ErpFirstPaint.Observe", reporter, StringComparison.Ordinal);
        Assert.Contains("ApplyIfErp", reporter, StringComparison.Ordinal);
        Assert.Contains("ClampLimitValue", reporter, StringComparison.Ordinal);
        var homeStart = reporter.IndexOf(
            "public async Task<ErpWorkspaceHomeDigest> BuildErpWorkspaceHomeAsync",
            StringComparison.Ordinal);
        Assert.True(homeStart >= 0, "workspace home method missing");
        var homeEnd = reporter.IndexOf(
            "private async Task<ErpDashboardSummary> ReadErpDashboardForPeriodAsync",
            homeStart,
            StringComparison.Ordinal);
        Assert.True(homeEnd > homeStart, "workspace home method bounds missing");
        var home = reporter[homeStart..homeEnd];
        Assert.DoesNotContain("BuildErpAgingDigestAsync", home, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildErpSupplierPortalDigestAsync", home, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildErpOrderPlanningDigestAsync", home, StringComparison.Ordinal);
        Assert.Contains("ReadWorkspaceTopSuppliersAsync", home, StringComparison.Ordinal);
        Assert.Contains("skip 12 extra month scans", home, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionValidator_MemoizesOnHttpContextItems()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Auth/DbBackedLegacySessionValidator.cs"));
        Assert.Contains("ecomae.legacy-session.validated", text, StringComparison.Ordinal);
        Assert.Contains("ValidateCoreAsync", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AgingSql_ScansTwoHundredRowsNotTwoThousand()
    {
        Assert.Contains("LIMIT 200", LegacySurfaceDashboardSql.SelectErpAgingArDocuments, StringComparison.Ordinal);
        Assert.Contains("LIMIT 200", LegacySurfaceDashboardSql.SelectErpAgingApDocuments, StringComparison.Ordinal);
        Assert.Contains("LIMIT 200", LegacySurfaceDashboardSql.SelectErpAgingInventoryRows, StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT 2000", LegacySurfaceDashboardSql.SelectErpAgingArDocuments, StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT 2000", LegacySurfaceDashboardSql.SelectErpAgingApDocuments, StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT 2000", LegacySurfaceDashboardSql.SelectErpAgingInventoryRows, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceTopSupplierSql_IsSelectOnlyAndLimited()
    {
        Assert.StartsWith("SELECT", LegacySurfaceDashboardSql.SelectErpWorkspaceTopSupplierSpend.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epc_erp_purchase_orders", LegacySurfaceDashboardSql.SelectErpWorkspaceTopSupplierSpend, StringComparison.Ordinal);
        Assert.Contains("LIMIT 5", LegacySurfaceDashboardSql.SelectErpWorkspaceTopSupplierSpend, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", LegacySurfaceDashboardSql.SelectErpWorkspaceTopSupplierSpend, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacySurfaceDashboardSql.SelectErpWorkspaceTopSupplierSpend, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SalesAndPurchaseOrderApps_UseFirstPaintPickerLimit()
    {
        var so = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        var po = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchaseOrdersApp.razor"));
        Assert.Contains("ErpFirstPaint.PickerLimit", so, StringComparison.Ordinal);
        Assert.Contains("ErpFirstPaint.PickerLimit", po, StringComparison.Ordinal);
        Assert.DoesNotContain("ListErpInventoryItemsForPickerAsync(1000", so, StringComparison.Ordinal);
        Assert.DoesNotContain("ListErpInventoryItemsForPickerAsync(1000", po, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestCache_RoundTripsOnHttpContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/erp/sales-orders-app";
        try
        {
            ErpFirstPaint.Observe(ctx);
            Assert.True(ErpFirstPaint.IsActive);
            ErpFirstPaint.SetRequestCache(ctx, "k", 7);
            Assert.True(ErpFirstPaint.TryGetRequestCache(ctx, "k", out int value));
            Assert.Equal(7, value);
            Assert.Equal(50, ErpFirstPaint.ClampList(200));
            Assert.Equal(50, ErpFirstPaint.ClampLimitValue("@limit", 200));
        }
        finally
        {
            ErpFirstPaint.Observe(null);
        }
    }

    [Fact]
    public void CpAndStorefront_ObserveActivatesTwoSecondBudget()
    {
        var cp = new DefaultHttpContext();
        cp.Request.Path = "/cp/orders";
        try
        {
            ErpFirstPaint.Observe(cp);
            Assert.True(ErpFirstPaint.IsActive);
            Assert.Equal(50, ErpFirstPaint.ClampList(200));
            Assert.Equal(5, ErpFirstPaint.ClampPhpBridgeTimeout(45));
        }
        finally
        {
            ErpFirstPaint.Observe(null);
        }

        Assert.False(ErpFirstPaint.IsActive);
        Assert.Equal(45, ErpFirstPaint.ClampPhpBridgeTimeout(45));
    }

    [Fact]
    public void DashboardScalarFallback_IsGatedOnFirstPaint()
    {
        var reporter = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        var start = reporter.IndexOf(
            "private async Task<ErpDashboardSummary> ReadErpDashboardScalarsAsync",
            StringComparison.Ordinal);
        Assert.True(start >= 0, "scalar fallback missing");
        var end = reporter.IndexOf(
            "public async Task<BosFleetSummary> BuildBosAsync",
            start,
            StringComparison.Ordinal);
        Assert.True(end > start, "scalar fallback bounds missing");
        var body = reporter[start..end];
        Assert.Contains("ErpFirstPaint.IsActive", body, StringComparison.Ordinal);
        Assert.Contains("EmptyErpSummary", body, StringComparison.Ordinal);
        Assert.Contains("Cloudflare 524", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Middleware_ObservesBeforeAdminGate()
    {
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        var paint = program.IndexOf("UseMiddleware<SurfaceFirstPaintMiddleware>", StringComparison.Ordinal);
        var gate = program.IndexOf("UseMiddleware<AdminSurfaceAuthGateMiddleware>", StringComparison.Ordinal);
        Assert.True(paint >= 0, "first-paint middleware missing");
        Assert.True(gate > paint, "first-paint middleware must run before admin gate");
        var middleware = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Middleware/SurfaceFirstPaintMiddleware.cs"));
        Assert.Contains("ErpFirstPaint.Observe", middleware, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", middleware, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpBridgeAndSession_CapFirstPaintWaits()
    {
        var bridge = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/PhpWarehouseSearchBridge.cs"));
        Assert.Contains("ClampPhpBridgeTimeout", bridge, StringComparison.Ordinal);
        Assert.Contains("CancelAfter", bridge, StringComparison.Ordinal);
        Assert.Contains("ErpFirstPaint.IsActive", bridge, StringComparison.Ordinal);
        var session = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Auth/DbLegacySessionStore.cs"));
        Assert.Contains("ErpFirstPaint.ApplyIfErp", session, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
