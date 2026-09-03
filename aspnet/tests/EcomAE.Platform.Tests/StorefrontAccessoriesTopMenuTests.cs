using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontAccessoriesTopMenuTests
{
    [Fact]
    public void AspNetAccessoriesIsDedicatedMarketplaceNotProductFamilyHash()
    {
        Assert.Equal("/storefront/accessories-app", StorefrontAspNetCanonical.Accessories);
        Assert.NotEqual(StorefrontAspNetCanonical.ProductFamily, StorefrontAspNetCanonical.Accessories);
        Assert.Equal("/en/accessories-spare-parts", StorefrontPhpCanonical.Accessories);
    }

    [Fact]
    public void PreferAspNetCatalogBrowseMapsAccessoriesPaths()
    {
        var prior = StorefrontSurfaceLinks.PreferAspNetApps;
        try
        {
            StorefrontSurfaceLinks.PreferAspNetApps = true;
            Assert.Equal("/storefront/accessories-app", StorefrontSurfaceLinks.Accessories);
            Assert.Equal(
                "/storefront/accessories-app",
                StorefrontSurfaceLinks.ForCatalogBrowse("/en/accessories-spare-parts"));
            Assert.Equal(
                "/storefront/accessories-app?id=648",
                StorefrontSurfaceLinks.ForCatalogBrowse("/en/accessories-spare-parts?id=648"));
            Assert.Equal(
                "/storefront/accessories-app?category=ev-hybrid",
                StorefrontSurfaceLinks.ForCatalogBrowse("/accessories?category=ev-hybrid"));
            Assert.Equal(
                "/storefront/product-family-app",
                StorefrontSurfaceLinks.ForCatalogBrowse("/en/product-family"));
        }
        finally
        {
            StorefrontSurfaceLinks.PreferAspNetApps = prior;
        }
    }

    [Fact]
    public void IncomingPhpAccessoriesPathRemapsToAccessoriesApp()
    {
        var prior = StorefrontSurfaceLinks.PreferAspNetApps;
        try
        {
            StorefrontSurfaceLinks.PreferAspNetApps = true;
            Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(
                "/en/accessories-spare-parts", out _));
            Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(
                "/en/accessories-spare-parts?id=648", out _));
            Assert.Equal(
                "/storefront/accessories-app",
                PhpSurfaceLinkMap.AspNetPrimaryHref("/en/accessories"));
        }
        finally
        {
            StorefrontSurfaceLinks.PreferAspNetApps = prior;
        }
    }

    [Fact]
    public void TopMenuChromePointsAtAccessoriesSurfaceLink()
    {
        var chrome = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("StorefrontSurfaceLinks.Accessories", chrome, StringComparison.Ordinal);
        Assert.Contains(">Accessories<", chrome, StringComparison.Ordinal);

        var app = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontAccessoriesApp.razor"));
        Assert.Contains("@page \"/storefront/accessories-app\"", app, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-accessories\"", app, StringComparison.Ordinal);
        Assert.Contains("data-base=\"/storefront/accessories-app\"", app, StringComparison.Ordinal);
        Assert.Contains("epc_accessories_marketplace.js", app, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_accessories_search.php", app, StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
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
