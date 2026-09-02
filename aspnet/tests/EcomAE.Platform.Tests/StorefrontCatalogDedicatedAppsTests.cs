using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection(PreferAspNetAppsCollection.Name)]
public sealed class StorefrontCatalogDedicatedAppsTests : IDisposable
{
    public StorefrontCatalogDedicatedAppsTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
    }

    public void Dispose()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
    }

    [Fact]
    public void DedicatedCatalogAppsAreNotHomepageHashes()
    {
        Assert.Equal("/storefront/umapi-catalog-app", StorefrontAspNetCanonical.UmapiCatalog);
        Assert.Equal("/storefront/product-family-app", StorefrontAspNetCanonical.ProductFamily);
        Assert.Equal("/storefront/available-brands-app", StorefrontAspNetCanonical.AvailableBrands);
        Assert.Equal("/storefront/available-brands-app", StorefrontAspNetCanonical.PartsInStock);
        Assert.Equal("/storefront/original-catalog-app", StorefrontAspNetCanonical.OriginalCatalog);
        Assert.Equal("/storefront/original-catalog-app", StorefrontAspNetCanonical.LevamOem);
        Assert.Equal("/storefront/eparts-cata-app", StorefrontAspNetCanonical.EpartsCata);
        Assert.Equal("/storefront/eparts-cata-app", StorefrontAspNetCanonical.PartsApiCatalog);
        Assert.Equal("/storefront/eparts-mod-app", StorefrontAspNetCanonical.EpartsMod);
        Assert.Equal("/storefront/ucats-app", StorefrontAspNetCanonical.UcatsService);
        Assert.Equal("/storefront/demand-intelligence-app", StorefrontAspNetCanonical.DemandIntelligence);
        Assert.DoesNotContain("#epc-", StorefrontAspNetCanonical.UmapiCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("#epc-", StorefrontAspNetCanonical.ProductFamily, StringComparison.Ordinal);
        Assert.DoesNotContain("#epc-", StorefrontAspNetCanonical.UcatsService, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/en/umapi_catalog", "/storefront/umapi-catalog-app")]
    [InlineData("/en/product-family", "/storefront/product-family-app")]
    [InlineData("/en/available-brands", "/storefront/available-brands-app")]
    [InlineData("/en/original-catalog", "/storefront/original-catalog-app")]
    [InlineData("/en/eparts-cata", "/storefront/eparts-cata-app")]
    [InlineData("/en/eparts-mod", "/storefront/eparts-mod-app")]
    [InlineData("/en/shop/katalogi-ucats", "/storefront/ucats-app")]
    [InlineData("/en/shop/katalogi-ucats/shiny", "/storefront/ucats-app")]
    [InlineData("/en/demand-intelligence", "/storefront/demand-intelligence-app")]
    [InlineData("/en/partsapi-catalog", "/storefront/eparts-cata-app")]
    [InlineData("/en/levam-oem", "/storefront/original-catalog-app")]
    public void PreferAspNetCatalogBrowseMapsToDedicatedApps(string phpPath, string expected)
    {
        Assert.Equal(expected, StorefrontSurfaceLinks.ForCatalogBrowse(phpPath));
    }

    [Theory]
    [InlineData("/en/umapi_catalog")]
    [InlineData("/en/product-family")]
    [InlineData("/en/available-brands")]
    [InlineData("/en/original-catalog")]
    [InlineData("/en/eparts-cata")]
    [InlineData("/en/eparts-mod")]
    [InlineData("/en/shop/katalogi-ucats")]
    [InlineData("/en/shop/katalogi-ucats/shiny")]
    [InlineData("/en/demand-intelligence")]
    [InlineData("/en/accessories-spare-parts")]
    [InlineData("/en/parts")]
    [InlineData("/en/vehicle-catalog")]
    [InlineData("/en/katalog-laximo")]
    public void IncomingPhpCatalogPathsStayOnBlazorSameUrl(string incoming)
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(incoming, out _));
    }

    [Fact]
    public void UcatsHubHasEightPhpCatalogues()
    {
        Assert.Equal(8, StorefrontUcatsCatalog.Cards.Count);
        Assert.NotNull(StorefrontUcatsCatalog.Find("shiny"));
        Assert.NotNull(StorefrontUcatsCatalog.Find("akkumulyatory"));
        Assert.NotNull(StorefrontUcatsCatalog.Find("kolesnye-gajki-bolty-prostavki"));
    }

    [Fact]
    public void CatalogAppsDeclarePhpSameUrlAliases()
    {
        AssertPage("StorefrontUmapiCatalogApp.razor", "/en/umapi_catalog", "/storefront/umapi-catalog-app");
        AssertPage("StorefrontProductFamilyApp.razor", "/en/product-family", "/storefront/product-family-app");
        AssertPage("StorefrontAvailableBrandsApp.razor", "/en/available-brands", "/storefront/available-brands-app");
        AssertPage("StorefrontOriginalCatalogApp.razor", "/en/original-catalog", "/storefront/original-catalog-app");
        AssertPage("StorefrontEpartsCataApp.razor", "/en/eparts-cata", "/storefront/eparts-cata-app");
        AssertPage("StorefrontEpartsModApp.razor", "/en/eparts-mod", "/storefront/eparts-mod-app");
        AssertPage("StorefrontUcatsHubApp.razor", "/en/shop/katalogi-ucats", "/storefront/ucats-app");
        AssertPage("StorefrontDemandIntelligenceApp.razor", "/en/demand-intelligence", "/storefront/demand-intelligence-app");
        AssertPage("StorefrontPreviewApp.razor", "/en", "/storefront/app");
        AssertPage("StorefrontAccessoriesApp.razor", "/en/accessories-spare-parts", "/storefront/accessories-app");
    }

    [Fact]
    public void OfficesPhpPathMapsToDedicatedAppNotDeliveryMethods()
    {
        Assert.Equal("/cp/offices-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/logistics/offices"));
        Assert.Equal("/cp/offices-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/logistics/offices"));
        Assert.NotEqual("/cp/delivery-methods-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/logistics/offices"));
        Assert.Equal("/cp/delivery-methods-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/logistics"));
    }

    private static void AssertPage(string fileName, string phpAlias, string aspNetApp)
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Pages/" + fileName);
        var text = File.ReadAllText(path);
        Assert.Contains("@page \"" + phpAlias, text, StringComparison.Ordinal);
        Assert.Contains("@page \"" + aspNetApp, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sitemap_catalogue_and_brochure_apps_exist()
    {
        AssertPage("StorefrontSitemapApp.razor", "/en/sitemap", "/storefront/sitemap-app");
        AssertPage("StorefrontOwnCatalogApp.razor", "/en/shop/catalogue", "/storefront/own-catalog-app");
        AssertPage("StorefrontProductApp.razor", "/en/shop/product", "/storefront/product-app");
        var brochure = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontBrochureApp.razor");
        var text = File.ReadAllText(brochure);
        Assert.Contains("@page \"/storefront/brochure-app\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/brochure\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
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
