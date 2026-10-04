using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontSearchTableSeoTests
{
    [Fact]
    public void SearchTable_UsesWarehouseCaptionSellPurchaseAndBreadcrumb()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("epc-part-breadcrumb", text, StringComparison.Ordinal);
        Assert.Contains("epc-price-purchase", text, StringComparison.Ordinal);
        Assert.Contains("epc-price-sell", text, StringComparison.Ordinal);
        Assert.Contains("WarehouseCaption", text, StringComparison.Ordinal);
        Assert.Contains("data-warehouse", text, StringComparison.Ordinal);
        Assert.Contains("fromCrossStock: true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Cross stock", text, StringComparison.Ordinal);
        Assert.DoesNotContain("infoLabel = \"Warehouse\"", text, StringComparison.Ordinal);
        Assert.Contains("NormalizeArticle", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PartsDesk_IsCollapsedLauncher()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/StorefrontPartsAgentChip.razor"));
        Assert.Contains("epc-parts-desk__launcher", text, StringComparison.Ordinal);
        Assert.Contains("hidden", text, StringComparison.Ordinal);
        Assert.DoesNotContain("position:fixed;right:1rem", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicCanonical_IsHttpsEvenWhenRequestIsHttp()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.Host = new HostString("www.epartscart.com");
        Assert.Equal(
            "https://www.epartscart.com/en/parts/BOSCH/0986494527",
            StorefrontPublicSeo.CanonicalForPartsChpu(ctx.Request, "bosch", "0986494527"));

        var home = new DefaultHttpContext();
        home.Request.Scheme = "http";
        home.Request.Host = new HostString("www.ecomae.com");
        Assert.Equal("https://www.ecomae.com/", StorefrontPublicSeo.CanonicalForStorefrontHome(home.Request));
    }

    [Fact]
    public void Sitemap_ListsMarketingPathsAndPartChpu_NotPhpIndex()
    {
        var marketing = StorefrontPublicSeo.SitemapUrlset(
            "https://www.ecomae.com",
            StorefrontPublicSeo.EcomaeMarketingSitemapPaths);
        Assert.Contains("https://www.ecomae.com/platform", marketing, StringComparison.Ordinal);
        Assert.Contains("https://www.ecomae.com/platform/pricing", marketing, StringComparison.Ordinal);
        Assert.Contains("https://www.ecomae.com/blockchain", marketing, StringComparison.Ordinal);
        Assert.Contains("https://www.ecomae.com/privacy", marketing, StringComparison.Ordinal);
        Assert.DoesNotContain("/marketing/platform", marketing, StringComparison.Ordinal);
        Assert.DoesNotContain("sitemap-index.php", marketing, StringComparison.Ordinal);

        var parts = StorefrontPublicSeo.SitemapUrlset(
            "https://www.epartscart.com",
            ["/", "/en/parts/BOSCH/0986494527"]);
        Assert.Contains("https://www.epartscart.com/en/parts/BOSCH/0986494527", parts, StringComparison.Ordinal);

        var robots = StorefrontPublicSeo.RobotsTxt("www.epartscart.com");
        Assert.Contains("Sitemap: https://www.epartscart.com/sitemap.xml", robots, StringComparison.Ordinal);
        Assert.Contains("Disallow: /en/parts/brands/", robots, StringComparison.Ordinal);

        var program = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("SitemapUrlset", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Results.Redirect(StorefrontPublicSeo.PhpSitemapIndex", program, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingHome_HasPhpCanonicalAndOverviewHrefsLeaveStubs()
    {
        var home = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/MarketingPreviewApp.razor"));
        Assert.Contains("https://www.ecomae.com/", home, StringComparison.Ordinal);
        Assert.Contains("og:description", home, StringComparison.Ordinal);
        Assert.Contains("Unified ERP", home, StringComparison.Ordinal);

        var platform = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaePlatformOverview.razor"));
        Assert.Contains("href=\"/platform\"", platform, StringComparison.Ordinal);
        Assert.Contains("href=\"/platform/pricing\"", platform, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/marketing/platform\"", platform, StringComparison.Ordinal);

        var chrome = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeMarketingChrome.razor"));
        Assert.Contains("href=\"/blockchain\"", chrome, StringComparison.Ordinal);
        Assert.Contains("href=\"/privacy\"", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/marketing/platform\"", chrome, StringComparison.Ordinal);
    }

    private static string Find(string relative)
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
