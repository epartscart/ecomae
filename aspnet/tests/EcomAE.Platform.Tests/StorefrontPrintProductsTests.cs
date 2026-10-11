using EcomAE.Platform.Migration;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPrintProductsTests
{
    [Fact]
    public void NamesPrintProductsCustomerPathsAndFunctions()
    {
        Assert.Equal("content/shop/catalogue/printProducts.php", StorefrontPrintProducts.PrintProductsPath);
        Assert.Equal("content/shop/catalogue/printProducts_2.php", StorefrontPrintProducts.PrintProducts2Path);
        Assert.Equal("content/shop/catalogue/printCategories.php", StorefrontPrintProducts.PrintCategoriesPath);
        Assert.Equal("content/shop/catalogue/set_cookie_products_style.php", StorefrontPrintProducts.StyleCookiePath);
        Assert.Equal("content/shop/catalogue/text_search_algorithm.php", StorefrontPrintProducts.TextSearchAlgorithmPath);
        Assert.Equal("/content/shop/catalogue/set_cookie_products_style.php", StorefrontPhpAjax.CatalogueStyleCookiePath);
        Assert.Equal("write_log_txt", StorefrontPrintProducts.write_log_txt);
        Assert.Equal("initProperiesValues", StorefrontPrintProducts.initProperiesValues);
        Assert.Equal("URL_Filters_to_url", StorefrontPrintProducts.URL_Filters_to_url);
        Assert.Equal("show_pagination", StorefrontPrintProducts.show_pagination);
        Assert.Equal("AJAXgetProductsHTML", StorefrontPrintProducts.AJAXgetProductsHTML);
        Assert.Equal("showRestyle", StorefrontPrintProducts.showRestyle);
    }

    [Theory]
    [InlineData("1", true, 1)]
    [InlineData("2", true, 2)]
    [InlineData("3", true, 3)]
    [InlineData("4", false, 0)]
    [InlineData("", false, 0)]
    [InlineData("grid", false, 0)]
    public void StyleCookie_AcceptsOnlyPhpStyles(string raw, bool ok, int style)
    {
        Assert.Equal(ok, StorefrontPrintProducts.TryStyleCookie(raw, out var parsed));
        Assert.Equal(style, parsed);
        if (ok)
        {
            Assert.Equal(raw, StorefrontPrintProducts.StyleCookieJson(parsed));
        }
    }

    [Fact]
    public void ResolveStyle_PrefersQueryThenCookie()
    {
        Assert.Equal(3, StorefrontPrintProducts.ResolveStyle("3", "1"));
        Assert.Equal(2, StorefrontPrintProducts.ResolveStyle(null, "2"));
        Assert.Equal(1, StorefrontPrintProducts.ResolveStyle("9", "nope"));
    }

    [Fact]
    public void Pagination_ShowsMoreWhenAFullPageLoaded()
    {
        Assert.Equal(1, StorefrontPrintProducts.NormalizePage(null));
        Assert.Equal(24, StorefrontPrintProducts.LoadLimit(2));
        var links = StorefrontPrintProducts.Pagination("/storefront/own-catalog-app?sort=name_asc&s=1", 2, 24, hasMore: true);
        Assert.Contains(links, link => link.Label == "Next" && link.Href.Contains("page=3", StringComparison.Ordinal) && !link.Disabled);
        Assert.Contains(links, link => link.Active && link.Label == "2");
    }

    [Fact]
    public void CustomerTiles_UseCurrentCategoryChildren()
    {
        var summer = new StorefrontCatalogueCategoryNode(3, "summer", "shiny/summer", 1, 2, 0, 1, "", "Summer", "/storefront/own-catalog-app?url=shiny/summer", []);
        var tires = new StorefrontCatalogueCategoryNode(1, "shiny", "shiny", 0, 1, 1, 10, "tires.png", "Tires", "/storefront/own-catalog-app?url=shiny", [summer]);
        var tiles = StorefrontPrintProducts.CustomerCategoryTiles([tires], 1, "shiny");
        Assert.Single(tiles);
        Assert.Equal("Summer", tiles[0].Value);
        Assert.Equal([tires], StorefrontPrintProducts.CustomerCategoryTiles([tires], 0, null));
    }

    [Fact]
    public void OwnCatalogAppWiresPrintProductsChrome()
    {
        var app = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOwnCatalogApp.razor"));
        Assert.Contains("StorefrontPrintProducts", app, StringComparison.Ordinal);
        Assert.Contains("printCategories", app, StringComparison.Ordinal);
        Assert.Contains("bottom_show_more_btn", app, StringComparison.Ordinal);
        Assert.Contains("bottom_pagination_div", app, StringComparison.Ordinal);
        Assert.Contains("CatalogueStyleCookiePath", app, StringComparison.Ordinal);
        Assert.Contains("epcShowRestyle", app, StringComparison.Ordinal);

        var endpoints = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Storefront/StorefrontPhpAjaxEndpoints.cs"));
        Assert.Contains("CatalogueStyleCookiePath", endpoints, StringComparison.Ordinal);
        Assert.Contains("CatalogueStyleCookieAsync", endpoints, StringComparison.Ordinal);
        Assert.Equal("content/shop/catalogue/compare/compare.php", StorefrontComparePages.ComparePath);
        Assert.Equal("content/shop/catalogue/bookmarks/bookmarks.php", StorefrontComparePages.BookmarksPath);
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
