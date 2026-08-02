using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CsvPriceOfferRepositoryTests
{
    [Fact]
    public async Task FindOffersMatchesLegacyPriceLookupFilteringAndOrdering()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "supplier,brand,article,article_show,name,price,stock_hint,lead_time\n" +
                "slow,TOYOTA,04465 0K020,04465-0K020,Brake Pad Set,150.00,4,2 days\n" +
                "fast,TOYOTA,044650K020,04465-0K020,Brake Pad Set,120.00,8,same day\n" +
                "wrong,BOSCH,044650K020,04465-0K020,Wrong Brand,80.00,1,next day\n" +
                "free,TOYOTA,044650K020,04465-0K020,Invalid Free,0,9,next day\n");

            var repository = new CsvPriceOfferRepository(path);

            var rows = await repository.FindOffersAsync("TOYOTA", "044650K020");

            Assert.Collection(rows,
                first =>
                {
                    Assert.Equal("fast", first.Supplier);
                    Assert.Equal(120.00m, first.Price);
                    Assert.Equal(8, first.StockHint);
                },
                second =>
                {
                    Assert.Equal("slow", second.Supplier);
                    Assert.Equal(150.00m, second.Price);
                });
        }
        finally
        {
            File.Delete(path);
        }
    }
}
