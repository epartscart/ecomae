using EcomAE.Platform.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LangHomeFallbackMiddlewareTests
{
    [Theory]
    [InlineData("/en", "en")]
    [InlineData("/en/", "en")]
    [InlineData("/EN/", "en")]
    [InlineData("/ar", "ar")]
    [InlineData("/me/", "me")]
    [InlineData("/ru", "ru")]
    public void MatchesExactLangHomes(string path, string lang)
    {
        Assert.True(LangHomeFallbackMiddleware.TryMatchLangHome(path, out var matched));
        Assert.Equal(lang, matched);
    }

    [Theory]
    [InlineData("/en/umapi_catalog")]
    [InlineData("/en/shop/part_search")]
    [InlineData("/")]
    [InlineData("/storefront/app")]
    [InlineData("/en/product-family")]
    public void DoesNotMatchDeeperOrNonLangPaths(string path)
    {
        Assert.False(LangHomeFallbackMiddleware.TryMatchLangHome(path, out _));
    }

    [Fact]
    public void RequestCmsLang_UsesStoredLangItem()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/storefront/app";
        ctx.Items[LangHomeFallbackMiddleware.LangItem] = "ar";
        Assert.Equal("ar", LangHomeFallbackMiddleware.RequestCmsLang(ctx));
    }

    [Fact]
    public void RequestCmsLang_UsesOriginalPathAfterRewrite()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/storefront/app";
        ctx.Items[LangHomeFallbackMiddleware.OriginalPathItem] = "/en/";
        Assert.Equal("en", LangHomeFallbackMiddleware.RequestCmsLang(ctx));
    }

    [Fact]
    public void RequestCmsLang_ReadsPrefixFromRequestPath()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/ru/umapi_catalog";
        Assert.Equal("ru", LangHomeFallbackMiddleware.RequestCmsLang(ctx, "en"));
    }

    [Fact]
    public void RequestCmsLang_FallsBackWhenNoLangPrefix()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/storefront/app";
        Assert.Equal("en", LangHomeFallbackMiddleware.RequestCmsLang(ctx));
        Assert.Equal("en", LangHomeFallbackMiddleware.RequestCmsLang(null, "en"));
    }

    [Fact]
    public async Task RewritesEnHomeToStorefrontAppAndKeepsLang()
    {
        var nextPath = "";
        var mw = new LangHomeFallbackMiddleware(ctx =>
        {
            nextPath = ctx.Request.Path.Value ?? "";
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/en/";
        await mw.InvokeAsync(ctx);

        Assert.Equal("/storefront/app", nextPath);
        Assert.Equal("/en/", ctx.Items[LangHomeFallbackMiddleware.OriginalPathItem]);
        Assert.Equal("en", ctx.Items[LangHomeFallbackMiddleware.LangItem]);
        Assert.Equal("en", ctx.Response.Headers[LangHomeFallbackMiddleware.HeaderName].ToString());
        Assert.Equal("en", LangHomeFallbackMiddleware.RequestCmsLang(ctx));
    }
}
