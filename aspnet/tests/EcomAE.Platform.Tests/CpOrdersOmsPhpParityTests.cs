using System.Reflection;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /cp/orders against inventing a non-PHP OMS shell.
/// Must keep dual-pane workspace + epc-orders / epc-od / epc-scp markers from PHP orders.php.
/// </summary>
public sealed class CpOrdersOmsPhpParityTests
{
    [Fact]
    public void CpOrdersApp_EmitsPhpOmsConsoleMarkers()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpOrdersApp.razor"));
        Assert.Contains("epc-orders-page", text, StringComparison.Ordinal);
        Assert.Contains("epc-orders-page__hero", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-kpi", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi__card", text, StringComparison.Ordinal);
        Assert.Contains("epc-orders-tabs", text, StringComparison.Ordinal);
        Assert.Contains("epc-orders-tab", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-workspace", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-workspace__list", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-workspace__detail", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-data-table", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-row", text, StringComparison.Ordinal);
        Assert.Contains("epc-od epc-od--oms", text, StringComparison.Ordinal);
        Assert.Contains("epc-od__tabs", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-od-tab", text, StringComparison.Ordinal);
        Assert.Contains("epc-od__lines", text, StringComparison.Ordinal);
        Assert.Contains("epc-od__doc-grid", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-detail__erp-fulfillment", text, StringComparison.Ordinal);
        Assert.Contains("epc-od__supplier-fulfillment", text, StringComparison.Ordinal);
        Assert.Contains("CpOrdersOmsStylesheets", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-oms-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference OMS", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET OMS dry-runs", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CpOrdersOmsStylesheets_ArePlatformAssets()
    {
        Assert.Contains(LegacyPresentationAssets.CpOrdersOmsStylesheets, href => href.Contains("/platform-assets/epc_orders_cp.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.CpOrdersOmsStylesheets, href => href.Contains("/platform-assets/epc_statuses_cp.css", StringComparison.Ordinal));
        Assert.True(File.Exists(FindRepoFile("cp/content/shop/order_process/epc_orders_cp.css")));
        Assert.True(File.Exists(FindRepoFile("cp/content/shop/order_process/epc_statuses_cp.css")));
    }

    [Fact]
    public void PhpLegacyAssetBridge_MapsOrdersCss()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("/platform-assets/epc_orders_cp.css", text, StringComparison.Ordinal);
        Assert.Contains("cp/content/shop/order_process/epc_orders_cp.css", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_ExposesOrderDetailDigest()
    {
        var iface = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/ISurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("GetCpOrderDetailAsync", iface, StringComparison.Ordinal);
        var sql = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectCpShopOrderById", sql, StringComparison.Ordinal);
        Assert.Contains("SelectCpShopOrderItems", sql, StringComparison.Ordinal);
        Assert.Contains("CountCpOrdersCompleted", sql, StringComparison.Ordinal);
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

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
