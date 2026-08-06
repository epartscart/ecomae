using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection(PreferAspNetAppsCollection.Name)]
public sealed class StorefrontSurfaceLinksTests : IDisposable
{
    public StorefrontSurfaceLinksTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
    }

    public void Dispose()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
    }

    [Fact]
    public void DefaultLinksPreferPhpCanonicalEnPaths()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
        Assert.Equal(StorefrontPhpCanonical.PartSearch, StorefrontSurfaceLinks.PartSearch);
        Assert.Equal(StorefrontPhpCanonical.Cart, StorefrontSurfaceLinks.Cart);
        Assert.Equal("/en/shop/part_search", StorefrontSurfaceLinks.PartSearch);
    }

    [Fact]
    public void PreferAspNetAppsUsesStorefrontAppsNotPhpEn()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
        Assert.Equal(StorefrontAspNetCanonical.PartSearch, StorefrontSurfaceLinks.PartSearch);
        Assert.Equal("/storefront/search-app", StorefrontSurfaceLinks.PartSearch);
        Assert.Equal("/storefront/cart-app", StorefrontSurfaceLinks.Cart);
        Assert.Equal("/storefront/garage-app", StorefrontSurfaceLinks.GarageLogin);
        Assert.DoesNotContain("/en/", StorefrontSurfaceLinks.WarehouseSearch, StringComparison.Ordinal);
    }
}
