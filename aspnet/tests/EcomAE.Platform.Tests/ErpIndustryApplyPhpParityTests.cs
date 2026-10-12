using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIndustryApplyPhpParityTests
{
    [Fact]
    public void Catalog_MatchesPhpIndustryKeys()
    {
        var keys = ErpIndustryApplyWriteService.Catalog.Select(b => b.Key).ToArray();
        Assert.Equal(
            ["general", "auto_parts", "electronics", "fashion", "jewellery", "food_perishable", "pharma", "cosmetics_beauty",
             "furniture_home", "building_construction", "industrial_manufacturing", "agriculture", "books_media", "hospitality_fnb", "services_professional", "real_estate"],
            keys);
        Assert.All(ErpIndustryApplyWriteService.Catalog, b =>
        {
            Assert.NotEmpty(b.Fields);
            Assert.Contains(b.DefaultUnit, b.Units);
            Assert.All(b.Fields, f => Assert.Contains(f.Type, new[] { "text", "number", "date", "select" }));
            Assert.All(b.Fields.Where(f => f.Type == "select"), f => Assert.NotEmpty(f.Options!));
        });
    }

    [Fact]
    public void Blueprint_AutoPartsMatchesPhp()
    {
        var bp = ErpIndustryApplyWriteService.Blueprint("auto_parts");
        Assert.NotNull(bp);
        Assert.Equal("standard", bp!.ItemType);
        Assert.Equal(0, bp.TrackExpiry);
        Assert.Equal("goods", bp.TaxCategory);
        Assert.Equal(["oem_number", "brand", "fits_make", "fits_model", "fits_year", "side_position"], bp.Fields.Select(f => f.Key).ToArray());
        Assert.Null(ErpIndustryApplyWriteService.Blueprint("nope"));
        Assert.Equal("services", ErpIndustryApplyWriteService.Blueprint("services_professional")!.TaxCategory);
        Assert.Equal(1, ErpIndustryApplyWriteService.Blueprint("pharma")!.TrackExpiry);
    }

    [Fact]
    public void Program_RegistersIndustryApplyService()
    {
        var text = File.ReadAllText(ErpPmSavePhpParityTests.FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpIndustryApplyWriteService", text, StringComparison.Ordinal);
    }
}
