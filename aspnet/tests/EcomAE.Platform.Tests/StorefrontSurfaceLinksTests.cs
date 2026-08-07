using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection(PreferAspNetAppsCollection.Name)]
public sealed class StorefrontSurfaceLinksTests : IDisposable
{
    public StorefrontSurfaceLinksTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
    }

    public void Dispose()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
    }

    [Fact]
    public void DefaultProductLinksPreferAspNetAppsNotPhpEn()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
        Assert.Equal(StorefrontAspNetCanonical.PartSearch, StorefrontSurfaceLinks.PartSearch);
        Assert.Equal("/storefront/search-app", StorefrontSurfaceLinks.PartSearch);
        Assert.Equal("/storefront/cart-app", StorefrontSurfaceLinks.Cart);
        Assert.Equal("/storefront/garage-app", StorefrontSurfaceLinks.GarageLogin);
        Assert.Equal("/storefront/bulk-upload-app", StorefrontSurfaceLinks.BulkUpload);
        Assert.DoesNotContain("/en/", StorefrontSurfaceLinks.WarehouseSearch, StringComparison.Ordinal);
        Assert.DoesNotContain("/en/", StorefrontSurfaceLinks.Quotes, StringComparison.Ordinal);
    }

    [Fact]
    public void InterimPhpCanonicalStillAvailableWhenPreferAspNetAppsOff()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
        Assert.Equal(StorefrontPhpCanonical.PartSearch, StorefrontSurfaceLinks.PartSearch);
        Assert.Equal(StorefrontPhpCanonical.Cart, StorefrontSurfaceLinks.Cart);
        Assert.Equal("/en/shop/part_search", StorefrontSurfaceLinks.PartSearch);
    }
}
