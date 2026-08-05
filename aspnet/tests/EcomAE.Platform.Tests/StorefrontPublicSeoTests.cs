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
    public void RobotsContent_IndexesStorefrontHome_NotCp()
    {
        var home = new DefaultHttpContext();
        home.Request.Path = "/storefront/app";
        Assert.Equal("index,follow", StorefrontPublicSeo.RobotsContentFor(home));

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
