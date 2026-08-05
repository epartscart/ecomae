using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPhpCanonicalTests
{
    [Theory]
    [InlineData("/storefront/search-app", "/en/shop/part_search")]
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
    public void HomeAppIsNotRemapped()
    {
        Assert.False(StorefrontPhpCanonical.TryMapStorefrontStubToPhp("/storefront/app", out _));
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
