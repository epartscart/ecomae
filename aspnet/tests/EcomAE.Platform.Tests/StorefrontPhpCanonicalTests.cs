using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPhpCanonicalTests
{
    [Theory]
    [InlineData("/storefront/search-app", "/en/shop/part_search")]
    [InlineData("/storefront/search-app?article=1310154101", "/en/shop/part_search?article=1310154101")]
    [InlineData("/storefront/search-app?mode=attr&q=oil&field=all", "/en/shop/warehouse-search?q=oil&field=all")]
    [InlineData("/storefront/search-app?mode=vin&identString=ABC", "/en/katalog-laximo?identString=ABC")]
    [InlineData("/storefront/search-app?mode=car", "/en/vehicle-catalog")]
    [InlineData("/storefront/cart-app", "/en/shop/cart")]
    [InlineData("/storefront/login", "/en/users/login")]
    [InlineData("/storefront/garage-app", "/en/garage/login")]
    public void StubMapsToPhpCanonical(string stub, string expected)
    {
        Assert.True(StorefrontPhpCanonical.TryMapStorefrontStubToPhp(stub, out var mapped));
        Assert.Equal(expected, mapped);
    }

    [Fact]
    public void ChromeNeverPostsSearchToStorefrontSearchApp()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("StorefrontPhpCanonical.PartSearch", text, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"/storefront/search-app\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"/storefront/search-app?", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpEdgeStubRedirectHandlesArticleSearch()
    {
        var path = FindRepoFile("epc_storefront_stub_redirect.php");
        var text = File.ReadAllText(path);
        Assert.Contains("/storefront/search-app", text, StringComparison.Ordinal);
        Assert.Contains("/en/shop/part_search", text, StringComparison.Ordinal);
        Assert.Contains("epc_storefront_stub_redirect_maybe_exit", text, StringComparison.Ordinal);
        Assert.Contains(
            "epc_storefront_stub_redirect.php",
            File.ReadAllText(FindRepoFile("index.php")),
            StringComparison.Ordinal);
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

    [Fact]
    public void HomeAppIsNotRemapped()
    {
        Assert.False(StorefrontPhpCanonical.TryMapStorefrontStubToPhp("/storefront/app", out _));
    }

    [Fact]
    public void LogoutStaysOnAspNetForLegacyLogoutService()
    {
        Assert.False(StorefrontPhpCanonical.TryMapStorefrontStubToPhp("/storefront/logout", out _));
    }

    [Fact]
    public void CatalogBrowseKeepsPhpStylePages()
    {
        Assert.Equal("/en/product-family", StorefrontPhpCanonical.ForCatalogBrowse("/product-family"));
        Assert.Equal("/en/umapi_catalog", StorefrontPhpCanonical.ForCatalogBrowse("/umapi_catalog"));
        Assert.Equal("/en/available-brands", StorefrontPhpCanonical.ForCatalogBrowse("/available-brands"));
    }

    [Fact]
    public void ManufacturerAndBrandDeepLinksMatchPhpQueryShape()
    {
        Assert.Equal("/en/product-family?manufacturer=BMW", StorefrontPhpCanonical.ForManufacturer("BMW"));
        Assert.Equal("/en/umapi_catalog?brand=bosch", StorefrontPhpCanonical.ForUmapiBrand("Bosch"));
        Assert.Equal("/en/umapi_catalog?brand=mann-filter", StorefrontPhpCanonical.ForUmapiBrand("Mann-Filter"));
    }
}
