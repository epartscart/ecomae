using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PriceLookupServiceTests
{
    [Theory]
    [InlineData("bosch", "0 986 424 590", "BOSCH", "0986424590")]
    [InlineData(" toyota ", " c-110/j ", "TOYOTA", "C110J")]
    public async Task LookupNormalizesBrandAndArticle(string brand, string article, string expectedBrand, string expectedArticle)
    {
        var service = new MigrationPriceLookupService();

        var result = await service.LookupAsync(new PriceLookupRequest(brand, article));

        Assert.True(result.Status);
        Assert.Equal(expectedBrand, result.Brand);
        Assert.Equal(expectedArticle, result.Article);
    }

    [Fact]
    public async Task LookupRejectsMissingBrandOrArticle()
    {
        var service = new MigrationPriceLookupService();

        var result = await service.LookupAsync(new PriceLookupRequest("", "C110J"));

        Assert.False(result.Status);
    }
}
