namespace EcomAE.Platform.Api.Catalog;

public sealed class MigrationPriceLookupService : IPriceLookupService
{
    public ValueTask<PriceLookupResult> LookupAsync(PriceLookupRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.IsValid)
        {
            return ValueTask.FromResult(new PriceLookupResult(
                false,
                request.Brand,
                request.Article,
                [],
                "not_started",
                "brand and article are required"));
        }

        return ValueTask.FromResult(new PriceLookupResult(
            true,
            request.NormalizedBrand,
            request.NormalizedArticle,
            [],
            "placeholder",
            "ASP.NET Core route is available; database-backed price lookup will use LegacyPriceLookupSql against shop_docpart_prices_data before replacing api/v1/price/lookup.php."));
    }
}
