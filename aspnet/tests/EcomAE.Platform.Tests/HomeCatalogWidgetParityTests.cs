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
    public void ShopPrivacyPath_IsOnlyPrivacy()
    {
        Assert.True(EcomaeMarketingSnapshots.IsShopPrivacyPath("/privacy"));
        Assert.True(EcomaeMarketingSnapshots.IsShopPrivacyPath("/en/privacy"));
        Assert.False(EcomaeMarketingSnapshots.IsShopPrivacyPath("/customers"));
        Assert.False(EcomaeMarketingSnapshots.IsShopPrivacyPath("/platform"));
    }
}
