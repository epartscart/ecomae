using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class RepositoryPriceLookupServiceTests
{
    [Fact]
    public async Task LookupMapsRepositoryRowsToPhpCompatibleOffers()
    {
        var service = new RepositoryPriceLookupService(new StaticPriceOfferRepository([
            new PriceOfferRow("warehouse-a", "BOSCH", "0986424590", "Brake Pad Set", 125.50m, 7, "same day")
        ]));

        var result = await service.LookupAsync(new PriceLookupRequest("bosch", "0 986 424 590"));

        Assert.True(result.Status);
        Assert.Equal("repository", result.MigrationStatus);
        var offer = Assert.Single(result.Offers);
        Assert.Equal("warehouse-a", offer.Supplier);
        Assert.Equal("BOSCH", offer.Brand);
        Assert.Equal("0986424590", offer.Article);
        Assert.Equal("Brake Pad Set", offer.Name);
        Assert.Equal(7, offer.StockHint);
        Assert.Equal("same day", offer.LeadTime);
    }

    private sealed class StaticPriceOfferRepository : IPriceOfferRepository
    {
        private readonly IReadOnlyCollection<PriceOfferRow> _rows;

        public StaticPriceOfferRepository(IReadOnlyCollection<PriceOfferRow> rows)
        {
            _rows = rows;
        }

        public Task<IReadOnlyCollection<PriceOfferRow>> FindOffersAsync(string normalizedBrand, string normalizedArticle, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rows);
        }
    }
}
