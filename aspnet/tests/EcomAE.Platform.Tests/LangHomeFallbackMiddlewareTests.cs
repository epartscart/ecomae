using EcomAE.Platform.Middleware;
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
}
