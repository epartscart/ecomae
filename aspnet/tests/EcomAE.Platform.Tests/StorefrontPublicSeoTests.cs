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

        var title = StorefrontPublicSeo.PartsChpuTitle(brand, article);
        Assert.Contains("Part number MD191472", title, StringComparison.Ordinal);
        Assert.StartsWith("MITSUBISHI MD191472", title, StringComparison.Ordinal);

        var desc = StorefrontPublicSeo.PartsChpuDescription(brand, article, "OIL FILTER", inStock: true);
        Assert.Contains("Part number / article: MD191472", desc, StringComparison.Ordinal);
        Assert.Contains("Brand: MITSUBISHI", desc, StringComparison.Ordinal);
        Assert.Contains("In stock at UAE warehouse", desc, StringComparison.Ordinal);
        Assert.Contains("UAE-Oman-KSA warehouse", desc, StringComparison.Ordinal);

        var keywords = StorefrontPublicSeo.PartsChpuKeywords(brand, article);
        Assert.Contains("part number MD191472", keywords, StringComparison.Ordinal);
        Assert.Contains("auto parts UAE", keywords, StringComparison.Ordinal);

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
        Assert.Contains("shippingDetails", productLd, StringComparison.Ordinal);
        Assert.Contains("areaServed", productLd, StringComparison.Ordinal);

        var alts = StorefrontPublicSeo.HreflangAlternatesForParts(ctx.Request, brand, article);
        Assert.Contains(alts, a => a.Hreflang == "en-SA");
        Assert.Contains(alts, a => a.Hreflang == "en-OM");
        Assert.Contains(alts, a => a.Hreflang == "en-PK");
        Assert.True(StorefrontPublicSeo.PageOwnsRobotsMeta("/en/parts/MITSUBISHI/MD191472"));
    }

    [Fact]
    public void AppRazorUsesPathAwareRobots()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/App.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("StorefrontPublicSeo.RobotsContentFor", text, StringComparison.Ordinal);
        Assert.Contains("PageOwnsRobotsMeta", text, StringComparison.Ordinal);
        Assert.DoesNotContain("content=\"noindex,nofollow,noarchive\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontHomeEnablesPublicSeoHead()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("IncludePublicSeo=\"true\"", text, StringComparison.Ordinal);
        var head = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/PhpSurfaceHead.razor"));
        Assert.Contains("HomeMetaDescription", head, StringComparison.Ordinal);
        Assert.Contains("Body-stream public SEO", head, StringComparison.Ordinal);
    }

    [Fact]
    public void CpSeoAppSurfacesAspNetParityStatus()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpSeoApp.razor"));
        Assert.Contains("ASP.NET-primary storefront SEO", text, StringComparison.Ordinal);
        Assert.Contains("Probe CHPU SEO", text, StringComparison.Ordinal);
        Assert.Contains("/sitemap.xml", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchAppEmitsChpuSeoAndParallelDigests()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("ApplyChpuSeo", text, StringComparison.Ordinal);
        Assert.Contains("ProductJsonLdBlock", text, StringComparison.Ordinal);
        // Brand+article CHPU: capped SEO stock + local CP cross seed; warehouse/genuine via AJAX.
        Assert.DoesNotContain("await Task.WhenAll(genuineTask, stockTask)", text, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource(TimeSpan.FromMilliseconds(250))", text, StringComparison.Ordinal);
        Assert.Contains("ProbeStorefrontPartStockAsync", text, StringComparison.Ordinal);
        Assert.Contains("BuildStorefrontCrossSearchAsync", text, StringComparison.Ordinal);
        Assert.Contains("_chpuSeoInStock", text, StringComparison.Ordinal);
        Assert.Contains("ajax-fast-path", text, StringComparison.Ordinal);
        Assert.Contains("Immediate protocol-3 poll", text, StringComparison.Ordinal);
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
