namespace EcomAE.Platform.Api.Catalog;

public sealed class MigrationPriceOfferRepository : IPriceOfferRepository
{
    public Task<IReadOnlyCollection<PriceOfferRow>> FindOffersAsync(string normalizedBrand, string normalizedArticle, CancellationToken cancellationToken = default)
    {
        // The repository boundary is now in place. The next step is replacing this
        // no-op implementation with a provider-backed reader that executes
        // LegacyPriceLookupSql.LookupOffers against the tenant/platform database.
        IReadOnlyCollection<PriceOfferRow> rows = [];
        return Task.FromResult(rows);
    }
}
