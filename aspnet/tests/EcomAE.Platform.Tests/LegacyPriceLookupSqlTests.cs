using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyPriceLookupSqlTests
{
    [Fact]
    public void LookupOffersMatchesPhpPriceLookupContract()
    {
        Assert.Equal("shop_docpart_prices_data", LegacyPriceLookupSql.SourceTable);
        Assert.Equal(25, LegacyPriceLookupSql.DefaultLimit);
        Assert.Contains("manufacturer", LegacyPriceLookupSql.LookupOffers);
        Assert.Contains("article_show", LegacyPriceLookupSql.LookupOffers);
        Assert.Contains("time_to_exe", LegacyPriceLookupSql.LookupOffers);
        Assert.Contains("ORDER BY `price` ASC", LegacyPriceLookupSql.LookupOffers);
    }
}
