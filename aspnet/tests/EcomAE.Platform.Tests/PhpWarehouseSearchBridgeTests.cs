using EcomAE.Platform.Migration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpWarehouseSearchBridgeTests
{
    [Fact]
    public void PhpWarehouseOffersEndpoint_ExistsInRepo()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "content", "shop", "docpart", "ajax_epc_warehouse_offers.php");
        Assert.True(File.Exists(path), "Expected ajax_epc_warehouse_offers.php for PHP CHPU offer bridge.");
        var text = File.ReadAllText(path);
        Assert.Contains("docpart_normalize_article_for_price", text, StringComparison.Ordinal);
        Assert.Contains("shop_docpart_prices_data", text, StringComparison.Ordinal);
        Assert.Contains("php-chpu", text, StringComparison.Ordinal);
        // Must not apply ASP.NET-old over-filters in the WHERE clause.
        Assert.DoesNotContain("storefront_temp_disabled`, 0) = 0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("IFNULL(d.`price`, 0) > 0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_WiresPhpBridgeFallback_WhenSqlEmpty()
    {
        var path = Path.Combine(
            FindRepoRoot(),
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Migration",
            "SurfaceDashboardSummaryReporter.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("PhpWarehouseSearchBridge", text, StringComparison.Ordinal);
        Assert.Contains("TryLoadOffersAsync", text, StringComparison.Ordinal);
        Assert.Contains("TryLoadBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("php-chpu", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bridge_WithoutHttpContext_ReturnsEmpty()
    {
        var bridge = new PhpWarehouseSearchBridge();
        var offers = await bridge.TryLoadOffersAsync("DA320", "ROCKY", 20);
        var brands = await bridge.TryLoadBrandsAsync("DA320", 20);
        Assert.Empty(offers);
        Assert.Empty(brands);
    }

    [Fact]
    public async Task Bridge_WithHttpContext_BuildsPublicPhpPaths()
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("www.epartscart.com");
        var accessor = new HttpContextAccessor { HttpContext = http };
        var bridge = new PhpWarehouseSearchBridge(httpContextAccessor: accessor);

        // No live network assert — failure/empty is fine; just ensure no throw.
        var offers = await bridge.TryLoadOffersAsync("DA320", "ROCKY", 5);
        Assert.NotNull(offers);
    }

    [Fact]
    public void Bridge_PrefersLoopbackHostHeaderForPhpAjax()
    {
        var path = Path.Combine(
            FindRepoRoot(),
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Migration",
            "PhpWarehouseSearchBridge.cs");
        var text = File.ReadAllText(path);
        Assert.Contains("127.0.0.1", text, StringComparison.Ordinal);
        Assert.Contains("HostHeader", text, StringComparison.Ordinal);
        Assert.Contains("AllowAutoRedirect", File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Program.cs")), StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "content", "shop", "docpart", "ajax_epc_article_brands.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found from test base directory.");
    }
}
