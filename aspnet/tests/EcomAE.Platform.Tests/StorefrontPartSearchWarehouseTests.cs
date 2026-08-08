using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPartSearchWarehouseTests
{
    [Fact]
    public void SearchAppCanonicalIsAspNetNotPhpPartSearchLoop()
    {
        Assert.Equal("/storefront/search-app", StorefrontAspNetCanonical.PartSearch);
        Assert.Equal("/storefront/search-app", EcomAeRoutes.StorefrontSearchApp);
        Assert.Equal("/en/shop/part_search", StorefrontPhpCanonical.PartSearch);
        Assert.False(
            StorefrontPhpCanonical.TryMapStorefrontStubToPhp("/storefront/search-app?article=1", out _),
            "search-app must stay on ASP.NET (stub→PHP remap would loop with PreferAspNet).");
    }

    [Fact]
    public void SqlIncludesPriceStorageFallbackAndBunchesSelect()
    {
        var sql = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectStorefrontOfficeStorageBunches", sql, StringComparison.Ordinal);
        Assert.Contains("SelectStorefrontPriceStorageFallback", sql, StringComparison.Ordinal);
        Assert.Contains("handler_folder`,'') = 'prices'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ReporterFallsThroughEmptyProtocol3ToPhpBridge()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("TryLoadBunchesAsync", text, StringComparison.Ordinal);
        Assert.Contains("SelectStorefrontPriceStorageFallback", text, StringComparison.Ordinal);
        Assert.Contains("php-chpu", text, StringComparison.Ordinal);
        // Must not short-circuit empty database protocol-3 without PHP fall-through.
        Assert.DoesNotContain(
            "Empty DB still returns aspnet-warehouse (0 rows) so UI does not fall through to PHP",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeAndPhpAjaxBunchesTwinExist()
    {
        var bridge = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/PhpWarehouseSearchBridge.cs"));
        Assert.Contains("TryLoadBunchesAsync", bridge, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_office_storage_bunches.php", bridge, StringComparison.Ordinal);
        Assert.True(File.Exists(Find("content/shop/docpart/ajax_epc_office_storage_bunches.php")));

        var app = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("officeId: 0, storageId: 0, protocolVersion: 3", app, StringComparison.Ordinal);
        Assert.Contains("/storefront/products-of-bunch", app, StringComparison.Ordinal);
        Assert.Contains("/storefront/search-bunches", app, StringComparison.Ordinal);
    }

    [Fact]
    public void NginxAndForceLiveDoNotRedirectSearchAppToPartSearch()
    {
        var ngx = File.ReadAllText(Find("deploy/aspnet/nginx-presentation-app-shadow-example.conf"));
        var searchBlockStart = ngx.IndexOf("location = /storefront/search-app", StringComparison.Ordinal);
        Assert.True(searchBlockStart >= 0);
        var slice = ngx.Substring(searchBlockStart, Math.Min(500, ngx.Length - searchBlockStart));
        Assert.Contains("proxy_pass http://127.0.0.1:5100", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("return 302 /en/shop/part_search", slice, StringComparison.Ordinal);

        var force = File.ReadAllText(Find("scripts/cloudpanel_FORCE_LIVE_NOW.sh"));
        Assert.Contains("storefront-search-app-warehouse-results", force, StringComparison.Ordinal);
        Assert.Contains("FAIL local search-app still redirects to part_search", force, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return 302 /en/shop/part_search$is_args$args;",
            force,
            StringComparison.Ordinal);

        Assert.True(File.Exists(Find("scripts/cloudpanel_FIX_SEARCH_APP_WAREHOUSE.sh")));
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
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
