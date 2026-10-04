using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Named product tenants and industries.ecomae.com must be served by ASP.NET
/// the same way www.epartscart.com already is: storefront home + CP login,
/// and the industries directory uses the /platform/industries snapshot.
/// </summary>
public sealed class TenantHostHomeRoutingTests
{
    public static readonly string[] NamedTenantHosts =
    [
        "epartscart.com",
        "www.epartscart.com",
        "electronicae.com",
        "www.electronicae.com",
        "stylenlook.com",
        "www.stylenlook.com",
        "thejewellerytrend.com",
        "www.thejewellerytrend.com",
        "taxofinca.com",
        "www.taxofinca.com",
    ];

    [Theory]
    [MemberData(nameof(NamedTenantHostData))]
    public void NamedTenantHomeRewritesToStorefront(string host)
    {
        Assert.True(LiveTenantPresentationLock.IsLockedHost(host));
        Assert.True(PublicHomeRewriteMiddleware.TryMap(host, "/", out var rewrite, out var kind));
        Assert.Equal("/storefront/app", rewrite);
        Assert.Equal("storefront", kind);
        Assert.False(PublicHomeRewriteMiddleware.TryMap(host, "/cp", out _, out _));
        Assert.False(PublicHomeRewriteMiddleware.TryMap(host, "/en/", out _, out _));
    }

    [Fact]
    public void MarketingHomeStaysOnTheMarketingApp()
    {
        Assert.True(PublicHomeRewriteMiddleware.TryMap("www.ecomae.com", "/", out var rewrite, out var kind));
        Assert.Equal("/marketing/app", rewrite);
        Assert.Equal("marketing", kind);
        Assert.False(PublicHomeRewriteMiddleware.TryMap("electronics.ecomae.com", "/", out _, out _));
        Assert.False(PublicHomeRewriteMiddleware.TryMap("industries.ecomae.com", "/", out _, out _));
    }

    [Theory]
    [InlineData("industries.ecomae.com", "/")]
    [InlineData("www.industries.ecomae.com", "/")]
    [InlineData("industries.ecomae.com", "/platform/industries")]
    [InlineData("industries.ecomae.com", "/industries")]
    [InlineData("INDUSTRIES.ECOMAE.COM", "/storefront/app")]
    public void IndustriesDirectoryHostServesThePlatformIndustriesPage(string host, string path)
    {
        Assert.True(EcomaeIndustryShowcaseSnapshots.IsIndustriesDirectoryHost(host));
        Assert.False(EcomaeIndustryShowcaseSnapshots.TryResolveHostSlug(host, out _));
        var html = EcomaeIndustryShowcaseSnapshots.HtmlFor(host, path);
        var canonical = EcomaeMarketingSnapshots.HtmlFor("/platform/industries");
        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Equal(canonical, html);
        Assert.Contains("Industries — ECOM AE", html, StringComparison.Ordinal);
        Assert.Contains("/platform/industry/auto_parts", html, StringComparison.Ordinal);
    }

    [Fact]
    public void IndustriesDirectoryDoesNotSwallowControlPanel()
    {
        Assert.False(EcomaeIndustryShowcaseSnapshots.IsIndustriesDirectoryPath("/cp"));
        Assert.False(EcomaeIndustryShowcaseSnapshots.IsIndustriesDirectoryPath("/cp/login"));
        Assert.True(string.IsNullOrEmpty(
            EcomaeIndustryShowcaseSnapshots.HtmlFor("industries.ecomae.com", "/cp")));
    }

    [Fact]
    public async Task StorefrontRewriteKeepsTheBrowserPath()
    {
        var nextPath = "";
        var mw = new PublicHomeRewriteMiddleware(ctx =>
        {
            nextPath = ctx.Request.Path.Value ?? "";
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Host = new HostString("www.electronicae.com");
        ctx.Request.Path = "/";
        await mw.InvokeAsync(ctx);

        Assert.Equal("/storefront/app", nextPath);
        Assert.Equal("storefront", ctx.Response.Headers[PublicHomeRewriteMiddleware.HeaderName].ToString());
    }

    public static IEnumerable<object[]> NamedTenantHostData()
        => NamedTenantHosts.Select(host => new object[] { host });
}
