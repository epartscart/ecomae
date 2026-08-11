using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPublicSeoTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/storefront/app", true)]
    [InlineData("/storefront", true)]
    [InlineData("/marketing/app", true)]
    [InlineData("/marketing/platform", true)]
    [InlineData("/en/parts/MITSUBISHI/MD191472", true)]
    [InlineData("/parts/MITSUBISHI/MD191472", true)]
    [InlineData("/en/parts/brands/MD191472", false)]
    [InlineData("/cp/app", false)]
    [InlineData("/erp/app", false)]
    [InlineData("/bos/app", false)]
    [InlineData("/storefront/login", false)]
    [InlineData("/storefront/cart-app", false)]
    [InlineData("/storefront/search-app", false)]
    public void PublicIndexablePathsMatchProductSurfaces(string path, bool expected)
    {
        Assert.Equal(expected, StorefrontPublicSeo.IsPublicIndexablePath(path));
    }

    [Fact]
    public void RobotsContent_IndexesStorefrontHomeAndChpu_NotCp()
    {
        var home = new DefaultHttpContext();
        home.Request.Path = "/storefront/app";
        Assert.Equal("index,follow", StorefrontPublicSeo.RobotsContentFor(home));

        var chpu = new DefaultHttpContext();
        chpu.Request.Path = "/en/parts/MITSUBISHI/MD191472";
        Assert.Equal("index,follow", StorefrontPublicSeo.RobotsContentFor(chpu));

        var cp = new DefaultHttpContext();
        cp.Request.Path = "/cp/app";
        Assert.Equal("noindex,nofollow,noarchive", StorefrontPublicSeo.RobotsContentFor(cp));
    }

    [Fact]
    public void CanonicalAndJsonLdMatchPhpShape()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("www.epartscart.com");
        ctx.Request.Path = "/storefront/app";

        Assert.Equal("https://www.epartscart.com/", StorefrontPublicSeo.CanonicalForStorefrontHome(ctx.Request));

        var jsonLd = StorefrontPublicSeo.JsonLdBlock(ctx.Request, "eParts Cart (Autoparts)");
        Assert.Contains("application/ld+json", jsonLd, StringComparison.Ordinal);
        Assert.Contains("AutoPartsStore", jsonLd, StringComparison.Ordinal);
        Assert.Contains("/en/shop/search?search_string=", jsonLd, StringComparison.Ordinal);
        Assert.Contains("United Arab Emirates", jsonLd, StringComparison.Ordinal);

        var alts = StorefrontPublicSeo.HreflangAlternates(ctx.Request);
        Assert.Contains(alts, a => a.Hreflang == "x-default" && a.Href.EndsWith("/en/", StringComparison.Ordinal));
        Assert.Contains(alts, a => a.Hreflang == "en-AE");
    }

    [Fact]
    public void PartsChpuSeoMatchesPhpProductSignals()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("www.epartscart.com");

        Assert.True(StorefrontPublicSeo.TryParsePartsChpu("/en/parts/MITSUBISHI/MD191472", out var brand, out var article));
        Assert.Equal("MITSUBISHI", brand);
        Assert.Equal("MD191472", article);

        var canonical = StorefrontPublicSeo.CanonicalForPartsChpu(ctx.Request, brand, article);
        Assert.Equal("https://www.epartscart.com/en/parts/MITSUBISHI/MD191472", canonical);

        Assert.Equal("index,follow", StorefrontPublicSeo.PartsChpuRobots(inStock: true));
        Assert.Equal("noindex,follow", StorefrontPublicSeo.PartsChpuRobots(inStock: false));

        var desc = StorefrontPublicSeo.PartsChpuDescription(brand, article, "OIL FILTER", inStock: true);
        Assert.Contains("MITSUBISHI MD191472", desc, StringComparison.Ordinal);
        Assert.Contains("In stock", desc, StringComparison.Ordinal);

        var productLd = StorefrontPublicSeo.ProductJsonLdBlock(
            ctx.Request,
            brand,
            article,
            "OIL FILTER",
            12.50m,
            inStock: true,
            "AED",
            [("TOYOTA", "90915YZZD1")]);
        Assert.Contains("\"@type\":\"Product\"", productLd, StringComparison.Ordinal);
        Assert.Contains("InStock", productLd, StringComparison.Ordinal);
        Assert.Contains("90915YZZD1", productLd, StringComparison.Ordinal);
        Assert.Contains("\"price\":\"12.50\"", productLd, StringComparison.Ordinal);
    }

    [Fact]
    public void AppRazorUsesPathAwareRobots()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/App.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("StorefrontPublicSeo.RobotsContentFor", text, StringComparison.Ordinal);
        Assert.DoesNotContain("content=\"noindex,nofollow,noarchive\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontHomeEnablesPublicSeoHead()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("IncludePublicSeo=\"true\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchAppEmitsChpuSeoAndParallelDigests()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("ApplyChpuSeo", text, StringComparison.Ordinal);
        Assert.Contains("ProductJsonLdBlock", text, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll", text, StringComparison.Ordinal);
        Assert.Contains("ssrOfferLimit", text, StringComparison.Ordinal);
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
