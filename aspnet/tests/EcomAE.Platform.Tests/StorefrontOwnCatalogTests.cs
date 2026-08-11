using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontOwnCatalogTests
{
    [Fact]
    public void AspNetOwnCatalogIsDedicatedAppNotProductFamilyHash()
    {
        Assert.Equal("/storefront/own-catalog-app", StorefrontAspNetCanonical.OwnCatalog);
        Assert.NotEqual(StorefrontAspNetCanonical.ProductFamily, StorefrontAspNetCanonical.OwnCatalog);
        Assert.Equal("/storefront/own-catalog-app", EcomAeRoutes.StorefrontOwnCatalogApp);
        Assert.Equal("/storefront/catalogue/tree", EcomAeRoutes.StorefrontCatalogueTree);
        Assert.Equal("/storefront/catalogue/products", EcomAeRoutes.StorefrontCatalogueProducts);
    }

    [Fact]
    public void PreferAspNetSurfaceLinkPointsAtOwnCatalogApp()
    {
        var prior = StorefrontSurfaceLinks.PreferAspNetApps;
        try
        {
            StorefrontSurfaceLinks.PreferAspNetApps = true;
            Assert.Equal("/storefront/own-catalog-app", StorefrontSurfaceLinks.OwnCatalog);
            Assert.Equal(
                "/storefront/own-catalog-app?url=shiny",
                StorefrontSurfaceLinks.ForOwnCatalogCategory("shiny"));
            Assert.Equal(
                "/storefront/own-catalog-app?category_id=12",
                StorefrontSurfaceLinks.ForOwnCatalogCategory(categoryId: 12));
        }
        finally
        {
            StorefrontSurfaceLinks.PreferAspNetApps = prior;
        }
    }

    [Fact]
    public void TreeBuilderFiltersApaiAndBuildsHrefs()
    {
        var rows = new[]
        {
            new StorefrontCatalogueCategoryRow(1, "shiny", "shiny", 0, 1, 1, 10, "", "Tires"),
            new StorefrontCatalogueCategoryRow(2, "apai-umapi-brakes", "apai-umapi-brakes", 0, 1, 0, 20, "", "Brakes"),
            new StorefrontCatalogueCategoryRow(3, "summer", "shiny/summer", 1, 2, 0, 1, "", "Summer"),
        };

        var tree = StorefrontOwnCatalogueTreeBuilder.Build(rows, filterApai: true);
        Assert.Single(tree);
        Assert.Equal(1, tree[0].Id);
        Assert.Equal("/storefront/own-catalog-app?url=shiny", tree[0].Href);
        Assert.Single(tree[0].Data);
        Assert.Equal("summer", tree[0].Data[0].Alias);
        Assert.True(StorefrontOwnCatalogueTreeBuilder.IsApai("apai-x", "x"));
        Assert.False(StorefrontOwnCatalogueTreeBuilder.IsApai("shiny", "shiny"));
    }

    [Theory]
    [InlineData("shiny", "1324", 62, "Shiny")]
    [InlineData("masla-i-avtohimiya", "1332", 64, "Masla I Avtohimiya")]
    [InlineData("diski", "???", 63, "Diski")]
    [InlineData("tires", "Tires", 1, "Tires")]
    public void LabelFor_HumanizesUnresolvedLangIds(string alias, string value, int id, string expected)
    {
        Assert.Equal(expected, StorefrontOwnCatalogueTreeBuilder.LabelFor(alias, value, id));
    }

    [Fact]
    public void CatalogueSqlResolvesLangTranslationsWhenPresent()
    {
        Assert.Contains(
            "lang_text_strings_translation",
            LegacySurfaceDashboardSql.SelectStorefrontCatalogueCategoriesTranslated,
            StringComparison.Ordinal);
        Assert.Contains(
            "value_translated",
            LegacySurfaceDashboardSql.SelectStorefrontCatalogueCategoriesTranslated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ChromeWiresCatalogOfProductsMegaMenuAndOwnCatalog()
    {
        var chrome = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("StorefrontSurfaceLinks.OwnCatalog", chrome, StringComparison.Ordinal);
        Assert.Contains(">Own Catalog<", chrome, StringComparison.Ordinal);
        Assert.Contains("id=\"dp_menu\"", chrome, StringComparison.Ordinal);
        Assert.Contains("showCatalogMenu", chrome, StringComparison.Ordinal);
        Assert.Contains("epc_own_catalog_menu.js", chrome, StringComparison.Ordinal);
        Assert.Contains("class=\"header-cat-btn\"", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "header-cat-btn\" href=\"@StorefrontSurfaceLinks.ProductFamily\"",
            chrome,
            StringComparison.Ordinal);

        var app = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOwnCatalogApp.razor"));
        Assert.Contains("@page \"/storefront/own-catalog-app\"", app, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-own-catalog\"", app, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCatalogueTreeAsync", app, StringComparison.Ordinal);

        Assert.Contains(
            "SelectStorefrontCatalogueCategories",
            File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "epc_own_catalog_menu.js",
            File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs")),
            StringComparison.Ordinal);
        Assert.True(File.Exists(Find("content/general_pages/epc_own_catalog_menu.js")));
        Assert.True(File.Exists(Find("content/general_pages/epc_own_catalog.css")));
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
