using EcomAE.Platform.Presentation;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Homepage widgets call the PHP URLs. The ASP.NET routes must return the same JSON
/// shapes: product-family summary/group, and umapi validation errors.
/// </summary>
public sealed class HomeCatalogWidgetParityTests
{
    [Fact]
    public void UnknownProductFamilyAction_MatchesPhp()
    {
        Assert.Equal("""{"status":false,"message":"Unknown action"}""", HomeCatalogWidgets.UnknownActionJson());
        Assert.Equal("""{"status":false,"message":"Database unavailable"}""", HomeCatalogWidgets.UnavailableJson());
        Assert.Equal("""{"status":false,"message":"Product family not found"}""", HomeCatalogWidgets.FamilyNotFoundJson());
    }

    [Fact]
    public void Summary_GroupsStockLines_LikePhpInfer()
    {
        var lines = new List<ProductFamilyLine>
        {
            new("Bosch", "F-026", "F026", "Oil filter element", 4),
            new("Mahle", "OX", "OX", "oil filter", 2),
            new("NPR", "P-1", "P1", "Piston ring set", 9),
        };
        var json = HomeCatalogWidgets.SummaryJson(lines, refresh: false);
        Assert.Contains("\"status\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"action\":\"summary\"", json, StringComparison.Ordinal);
        Assert.Contains("\"cached\":true", json, StringComparison.Ordinal);
        Assert.Contains("Oil filter", json, StringComparison.Ordinal);
        Assert.Contains("Piston", json, StringComparison.Ordinal);
        Assert.Contains("\"parts_count\":3", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Group_MissingLabel_IsNull_SoEndpointReturnsNotFound()
    {
        var lines = new List<ProductFamilyLine> { new("Bosch", "1", "1", "Piston", 1) };
        Assert.Null(HomeCatalogWidgets.GroupJson(lines, "Gasket", null));
        var found = HomeCatalogWidgets.GroupJson(lines, "piston", null);
        Assert.NotNull(found);
        Assert.Contains("\"action\":\"group\"", found, StringComparison.Ordinal);
        Assert.Contains("Bosch", found, StringComparison.Ordinal);
    }

    [Fact]
    public void UmapiValidation_MatchesPhp()
    {
        Assert.Equal("""{"message":"Unknown catalog action."}""", HomeCatalogWidgets.UmapiUnknownJson());
        Assert.Equal("""{"message":"Brand is required."}""", HomeCatalogWidgets.UmapiMessageJson("Brand is required."));
        Assert.Equal("passenger", HomeCatalogWidgets.SectionOf(null, null));
        Assert.Equal("commercial", HomeCatalogWidgets.SectionOf("passenger", "CV"));
        Assert.Equal("motorbike", HomeCatalogWidgets.SectionOf("nope", "Motorcycle"));
        Assert.Equal("FILTEROIL01", HomeCatalogWidgets.NormalizeArticle("filter-oil 01"));
    }

    [Theory]
    [InlineData("/customers", "platform__customer-results")]
    [InlineData("/en/privacy", "privacy")]
    [InlineData("/privacy", "privacy")]
    public void CustomersAndShopPrivacy_MapToExistingSnapshots(string path, string slug)
    {
        Assert.Equal(slug, EcomaeMarketingSnapshots.SlugFor(path));
        var html = EcomaeMarketingSnapshots.HtmlFor(path);
        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SuppliersJson_UnionsPriceListBrands_LikePhp()
    {
        var json = HomeCatalogWidgets.SuppliersJson(
            [new HomeCatalogWidgets.CachedUmapiBrand(12, "Bosch", "Bosch GmbH")],
            [
                new HomeCatalogWidgets.StockUmapiBrand("BOSCH", 4),
                new HomeCatalogWidgets.StockUmapiBrand("MAHLE", 9),
            ]);
        Assert.Contains("\"rows\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"source\":\"database\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SUP_ID\":12", json, StringComparison.Ordinal);
        Assert.Contains("Bosch GmbH", json, StringComparison.Ordinal);
        Assert.Contains("Loaded price-list brand: 9 part numbers", json, StringComparison.Ordinal);
        Assert.Contains("\"LOCAL_STOCK_COUNT\":9", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Loaded price-list brand: 4 part numbers", json, StringComparison.Ordinal);
        Assert.Equal("""{"rows":0,"data":[],"source":"database"}""", HomeCatalogWidgets.SuppliersJson([], []));
        Assert.Equal("[]", HomeCatalogWidgets.EmptyVehicleCatalogJson());
    }

    [Fact]
    public void UmapiCacheSchema_CreatesThePhpCacheTables()
    {
        var sql = string.Join('\n', HomeCatalogWidgets.UmapiCacheSchemaStatements());
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_umapi_manufacturers`", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_umapi_brands`", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_umapi_models`", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_umapi_modifications`", sql, StringComparison.Ordinal);
        Assert.Equal(8, HomeCatalogWidgets.UmapiCacheSchemaStatements().Count);
    }

    [Fact]
    public void UmapiImageUrl_MatchesPhp()
    {
        Assert.False(HomeCatalogWidgets.TryUmapiImageUrl("", "1", out _));
        Assert.False(HomeCatalogWidgets.TryUmapiImageUrl("supplier", "0", out _));
        Assert.True(HomeCatalogWidgets.TryUmapiImageUrl("Supplier", "12", out var supplier));
        Assert.Equal("https://image.umapi.ru/SUPPLIERS/12.png", supplier);
        Assert.True(HomeCatalogWidgets.TryUmapiImageUrl("manufacturer", "4", out var make));
        Assert.Equal("https://image.umapi.ru/MANUFACTURERS/4.png", make);
    }

    [Fact]
    public void BrochureProcessPhoto_UsesPhpTopicAndCrc()
    {
        Assert.Equal(0xCBF43926u, BrochureProcessPhoto.PhpCrc32("123456789"));
        var svg = BrochureProcessPhoto.Build("ai", "Web tracker", "AI hooks", "fn-seed");
        Assert.Contains("aria-label=\"Web tracker\"", svg, StringComparison.Ordinal);
        Assert.Contains(">AI</text>", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_brochure_process_photo.php", EcomaeMarketingSnapshots.RewritePhpAssetUrls(
            "<img src=\"/content/general_pages/epc_brochure_process_photo.php?topic=ai&amp;t=Web\" />"), StringComparison.Ordinal);
        var rewritten = EcomaeMarketingSnapshots.RewritePhpAssetUrls(
            "<img src=\"/content/general_pages/epc_brochure_process_photo.php?topic=ai&amp;t=Web\" />");
        Assert.Contains("/platform-assets/brochure-process.svg?topic=ai", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessoriesSearch_MatchesPhpUrlAndFilters()
    {
        Assert.Equal("/content/shop/docpart/ajax_epc_accessories_search.php", AccessoriesMarketplaceSearch.Path);
        Assert.Equal("/en/accessories-spare-parts?id=9&category=car-care&subcategory=car-wax",
            AccessoriesMarketplaceSearch.StorefrontDetailUrl(9, "car-care", "car-wax"));
        Assert.False(AccessoriesMarketplaceSearch.IsOutboundExternalUrl("/en/accessories?category=car-care"));
        Assert.False(AccessoriesMarketplaceSearch.IsOutboundExternalUrl("https://www.epartscart.com/en/accessories-spare-parts?id=4"));
        Assert.True(AccessoriesMarketplaceSearch.IsOutboundExternalUrl("https://example.com/item"));
        Assert.Equal(24, AccessoriesMarketplaceSearch.NormalizePerPage(null));
        Assert.Equal(12, AccessoriesMarketplaceSearch.NormalizePerPage("1"));
        Assert.Equal(48, AccessoriesMarketplaceSearch.NormalizePerPage("99"));
    }

    [Fact]
    public void ShopPrivacyPath_IsOnlyPrivacy()
    {
        Assert.True(EcomaeMarketingSnapshots.IsShopPrivacyPath("/privacy"));
        Assert.True(EcomaeMarketingSnapshots.IsShopPrivacyPath("/en/privacy"));
        Assert.False(EcomaeMarketingSnapshots.IsShopPrivacyPath("/customers"));
        Assert.False(EcomaeMarketingSnapshots.IsShopPrivacyPath("/platform"));
    }
}
