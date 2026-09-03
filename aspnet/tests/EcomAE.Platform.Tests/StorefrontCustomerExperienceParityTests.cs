using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection(PreferAspNetAppsCollection.Name)]
public sealed class StorefrontCustomerExperienceParityTests : IDisposable
{
    public StorefrontCustomerExperienceParityTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
    }

    public void Dispose()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
    }

    [Theory]
    [InlineData("/en/shop/part_search", "/storefront/search-app")]
    [InlineData("/en/shop/search", "/storefront/search-app?mode=name")]
    [InlineData("/en/shop/warehouse-search", "/storefront/search-app?mode=attr")]
    [InlineData("/en/shop/cart", "/storefront/cart-app")]
    [InlineData("/en/shop/checkout", "/storefront/checkout-app")]
    [InlineData("/en/shop/checkout/how_get", "/storefront/checkout-app?step=how_get")]
    [InlineData("/en/shop/checkout/confirm", "/storefront/checkout-app?step=confirm")]
    [InlineData("/en/shop/orders", "/storefront/orders-app")]
    [InlineData("/en/users/login", "/storefront/login")]
    [InlineData("/en/users/registration", "/storefront/register-app")]
    [InlineData("/en/shop/quotes", "/storefront/quotes-app")]
    [InlineData("/en/shop/zakladki", "/storefront/wishlist-app")]
    [InlineData("/en/shop/sravneniya", "/storefront/compare-app")]
    [InlineData("/en/shop/balans", "/storefront/account-summary-app")]
    [InlineData("/en/shop/bulk-upload", "/storefront/bulk-upload-app")]
    [InlineData("/en/katalog-laximo", "/storefront/vin-app")]
    [InlineData("/en/vehicle-catalog", "/storefront/vehicle-catalog-app")]
    public void CustomerJourneyPhpPathsHaveDedicatedAspNetApps(string phpHref, string aspNet)
    {
        Assert.Equal(aspNet, PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref));
    }

    [Fact]
    public void ArticleQueryOnPartsStaysSearchNotBrandsPage()
    {
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/parts?article=DA320", out var mapped));
        Assert.Contains("search-app", mapped, StringComparison.Ordinal);
        Assert.Contains("article=DA320", mapped, StringComparison.Ordinal);
        Assert.DoesNotContain("available-brands", mapped, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/en/shop/quotes")]
    [InlineData("/en/shop/zakladki")]
    [InlineData("/en/shop/sravneniya")]
    [InlineData("/en/shop/balans")]
    [InlineData("/en/shop/bulk-upload")]
    [InlineData("/en/shop/search")]
    [InlineData("/en/garage/login")]
    [InlineData("/en/users/profile")]
    [InlineData("/en/shop/checkout/how_get")]
    public void IncomingCustomerAccountPathsStayOnBlazorSameUrl(string incoming)
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(incoming, out _));
    }

    [Fact]
    public void TenantCpCustomerOpsMapToDedicatedApps()
    {
        Assert.Equal("/cp/orders", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/orders/orders"));
        Assert.Equal("/cp/product-catalogue-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/catalogue/products"));
        Assert.Equal("/cp/offices-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/logistics/offices"));
        Assert.Equal("/cp/workshop-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/workshop"));
        Assert.Equal("/cp/kkt-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/kkt"));
        Assert.Equal("/cp/bulk-upload-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/bulk_upload"));
        Assert.Equal("/cp/integrations-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/control/portal/epc_integrations_hub"));
    }
}
