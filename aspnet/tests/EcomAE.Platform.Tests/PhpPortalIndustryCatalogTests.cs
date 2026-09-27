using System;
using System.Linq;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Contract tests for the typed twin of PHP <c>epc_portal_industries()</c>.</summary>
public sealed class PhpPortalIndustryCatalogTests
{
    [Theory]
    [InlineData("auto_parts", "Auto spare parts", "fa-car")]
    [InlineData("electronics", "Electronics & gadgets", "fa-microchip")]
    [InlineData("fashion", "Fashion & apparel", "fa-shopping-bag")]
    [InlineData("jewellery", "Jewellery & luxury goods", "fa-diamond")]
    [InlineData("construction_contracting", "Construction & contracting", "fa-building")]
    public void Php_rows_keep_their_name_and_icon(string code, string name, string icon)
    {
        Assert.Equal(name, PhpPortalIndustryCatalog.LabelFor(code));
        Assert.Equal(icon, PhpPortalIndustryCatalog.IconFor(code));
    }

    [Fact]
    public void Resolve_falls_back_to_auto_parts_for_unknown_or_empty_codes()
    {
        Assert.Equal("auto_parts", PhpPortalIndustryCatalog.Resolve(null).Code);
        Assert.Equal("auto_parts", PhpPortalIndustryCatalog.Resolve("   ").Code);
        Assert.Equal("auto_parts", PhpPortalIndustryCatalog.Resolve("not_an_industry").Code);
        Assert.False(PhpPortalIndustryCatalog.Knows("not_an_industry"));
        Assert.True(PhpPortalIndustryCatalog.Knows(" jewellery "));
    }

    [Fact]
    public void Every_row_is_unique_and_renderable()
    {
        Assert.NotEmpty(PhpPortalIndustryCatalog.All);
        Assert.Equal(
            PhpPortalIndustryCatalog.All.Count,
            PhpPortalIndustryCatalog.All.Select(r => r.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var row in PhpPortalIndustryCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Name));
            Assert.StartsWith("fa-", row.Icon, StringComparison.Ordinal);
            Assert.StartsWith("#", row.Primary, StringComparison.Ordinal);
            Assert.StartsWith("#", row.PrimaryDark, StringComparison.Ordinal);
            Assert.StartsWith("#", row.Accent, StringComparison.Ordinal);
        }
    }
}
