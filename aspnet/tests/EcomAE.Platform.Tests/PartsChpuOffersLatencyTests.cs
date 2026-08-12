using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the ~1s CHPU warehouse + cross first-paint budget for brand+article pages
/// (e.g. /en/parts/JS%20ASAKASHI/C110J, /en/parts/AISIN/DT068).
/// </summary>
public sealed class PartsChpuOffersLatencyTests
{
    [Fact]
    public void ChpuClient_FiresProtocol3BeforeSearchBunches()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        var immediate = text.IndexOf("Immediate protocol-3 poll", StringComparison.Ordinal);
        var bunches = text.IndexOf("search-bunches is diagnostic only", StringComparison.Ordinal);
        Assert.True(immediate >= 0, "missing immediate protocol-3 poll marker");
        Assert.True(bunches > immediate, "search-bunches enrichment must come after immediate poll");
        Assert.Contains("AbortSignal.timeout(3000)", text, StringComparison.Ordinal);
        Assert.Contains("data-enhance-nav=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("epcRunChpuPriceSearchBootstrap", text, StringComparison.Ordinal);
        Assert.Contains("data-ssr-offers", text, StringComparison.Ordinal);
        Assert.Contains("fetchCross(200,", text, StringComparison.Ordinal);
        Assert.Contains("include_crossbase=1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("return pollOne(p3);", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChpuClient_UsesAspNetCrossSearchOnly_NoProductPhpUrls()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("/storefront/cross-search?", text, StringComparison.Ordinal);
        Assert.Contains("loadAspNetCrossSearch", text, StringComparison.Ordinal);
        Assert.Contains("AbortSignal.timeout(ms)", text, StringComparison.Ordinal);
        Assert.Contains("include_crossbase=1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ajax_epc_cross_search.php", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ajax_getProductsOfBunch.php", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/content/shop/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WarehouseParityJs_HasNoProductPhpUrls()
    {
        var text = File.ReadAllText(FindRepoFile("content/general_pages/epc_warehouse_search_parity.js"));
        Assert.Contains("/storefront/cross-search?", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/cart/add", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/quotes/add-item", text, StringComparison.Ordinal);
        Assert.Contains("__epcChpuCrossBootstrapped", text, StringComparison.Ordinal);
        Assert.DoesNotContain(".php?", text, StringComparison.Ordinal);
        Assert.DoesNotContain(".php\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain(".php'", text, StringComparison.Ordinal);
        Assert.DoesNotContain("umapi_proxy", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/content/shop/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_BrandedFastPath_SkipsMissCascadeWhenArticleSearchExists()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("skip SimpleEquality/ExactTrim miss cascades", text, StringComparison.Ordinal);
        Assert.Contains("ResolveWarehouseBrandForArticleAsync", text, StringComparison.Ordinal);
        Assert.Contains("QueryStorefrontPartOffersForBrandAliasesAsync", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync(article, brand, 80,", text, StringComparison.Ordinal);
        Assert.Contains("ScoreWarehouseBrandMatch", text, StringComparison.Ordinal);
        Assert.Contains("shared >= 4", text, StringComparison.Ordinal);
    }

    [Fact]
    public void NginxSafeBak_PrunesLongNamesAndSkipsBakLitter()
    {
        var text = File.ReadAllText(FindRepoFile("scripts/lib/nginx_safe_bak.py"));
        Assert.Contains("ENAMETOOLONG", text, StringComparison.Ordinal);
        Assert.Contains("prune_sites_enabled_bak_litter", text, StringComparison.Ordinal);
        Assert.Contains("/root/nginx-bak", text, StringComparison.Ordinal);
        var warmup = File.ReadAllText(FindRepoFile("scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh"));
        Assert.Contains("nginx_safe_bak", warmup, StringComparison.Ordinal);
        Assert.Contains("is_bak_litter", warmup, StringComparison.Ordinal);
        var prove = File.ReadAllText(FindRepoFile("scripts/cloudpanel_EPARTSCART_SEARCH_FASTER_THAN_PHP_PROVE.sh"));
        Assert.Contains("JAASHIKA", prove, StringComparison.Ordinal);
        Assert.Contains("SSR_FLAG=SOFT", prove, StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_DefaultsToAspNetOnly_NoPhpBridgeUnlessOptIn()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("ECOMAE_ALLOW_PHP_WAREHOUSE_BRIDGE", text, StringComparison.Ordinal);
        Assert.Contains("AllowPhpWarehouseBridge", text, StringComparison.Ordinal);
        Assert.Contains("aspnet-warehouse-empty", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChpuSsr_SeedsOffersAndDoesNotAwaitGenuineBrands()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("SSR-seed local warehouse rows", text, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource(TimeSpan.FromMilliseconds(350))", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync(_articleInput, _brandInput, 40", text, StringComparison.Ordinal);
        Assert.Contains("BuildStorefrontCrossSearchAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ListStorefrontGenuineBrandsAsync()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductsOfBunch_HardCapsProtocol3Budget()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("CancelAfter(TimeSpan.FromMilliseconds(2500))", text, StringComparison.Ordinal);
        Assert.Contains("timeoutSeconds: 2", text, StringComparison.Ordinal);
        Assert.Contains("command.CommandTimeout = 2", text, StringComparison.Ordinal);
        Assert.Contains("BuildStorefrontCrossSearchAsync", text, StringComparison.Ordinal);
        Assert.Contains("aspnet-cross-local", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModule_MapsCrossSearchRoute()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("StorefrontCrossSearch = \"/storefront/cross-search\"", routes, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.StorefrontCrossSearch", module, StringComparison.Ordinal);
        Assert.Contains("BuildStorefrontCrossSearchAsync", module, StringComparison.Ordinal);
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
