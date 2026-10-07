using EcomAE.Platform.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontLangAliasMiddlewareTests
{
    [Theory]
    [InlineData("/ar/parts", "ar", "/en/parts")]
    [InlineData("/ar/umapi_catalog", "ar", "/en/umapi_catalog")]
    [InlineData("/ar/available-brands", "ar", "/en/available-brands")]
    [InlineData("/ar/product-family", "ar", "/en/product-family")]
    [InlineData("/ar/vehicle-catalog", "ar", "/en/vehicle-catalog")]
    [InlineData("/ar/zapros-prodavczu", "ar", "/en/zapros-prodavczu")]
    [InlineData("/ru/vehicle-catalog", "ru", "/en/vehicle-catalog")]
    [InlineData("/ru/shop/cart", "ru", "/en/shop/cart")]
    [InlineData("/me/users/login", "me", "/en/users/login")]
    [InlineData("/AR/parts/", "ar", "/en/parts")]
    [InlineData("/ar/parts/JS%20ASAKASHI/C110J", "ar", "/en/parts/JS%20ASAKASHI/C110J")]
    public void RewritesNonEnglishStorefrontPathsOntoEnglishTwins(string path, string lang, string aspNet)
    {
        Assert.True(StorefrontLangAliasMiddleware.TryRewrite(path, out var matched, out var rewritten));
        Assert.Equal(lang, matched);
        Assert.Equal(aspNet, rewritten);
    }

    [Theory]
    [InlineData("/en/parts")]
    [InlineData("/en/")]
    [InlineData("/ar")]
    [InlineData("/ar/")]
    [InlineData("/ru/")]
    [InlineData("/me")]
    [InlineData("/")]
    [InlineData("/ar/cp")]
    [InlineData("/ar/erp/sales-orders")]
    [InlineData("/ru/platform/industries")]
    [InlineData("/ar/marketing/app")]
    [InlineData("/ar/storefront/cart-app")]
    [InlineData("/ar/api/v1/price/lookup")]
    public void LeavesEnglishHomesAndNonStorefrontPaths(string path)
    {
        Assert.False(StorefrontLangAliasMiddleware.TryRewrite(path, out _, out _));
    }

    [Fact]
    public async Task InvokeKeepsBrowserLangAndRewritesPath()
    {
        var nextPath = "";
        var mw = new StorefrontLangAliasMiddleware(ctx =>
        {
            nextPath = ctx.Request.Path.Value ?? "";
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/ar/parts";
        await mw.InvokeAsync(ctx);

        Assert.Equal("/en/parts", nextPath);
        Assert.Equal("/ar/parts", ctx.Items[LangHomeFallbackMiddleware.OriginalPathItem]);
        Assert.Equal("ar", ctx.Items[LangHomeFallbackMiddleware.LangItem]);
        Assert.Equal("ar", ctx.Response.Headers[StorefrontLangAliasMiddleware.HeaderName].ToString());
        Assert.Equal("ar", LangHomeFallbackMiddleware.RequestCmsLang(ctx));
    }
}
