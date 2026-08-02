namespace EcomAE.Platform.Api.Catalog;

public sealed class RepositoryPriceLookupService : IPriceLookupService
{
    private readonly IPriceOfferRepository _offers;

    public RepositoryPriceLookupService(IPriceOfferRepository offers)
    {
        _offers = offers;
    }

    public async ValueTask<PriceLookupResult> LookupAsync(PriceLookupRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.IsValid)
        {
            return new PriceLookupResult(false, request.Brand, request.Article, [], "rejected", "brand and article are required");
        }

        var rows = await _offers.FindOffersAsync(request.NormalizedBrand, request.NormalizedArticle, cancellationToken);
        var offers = rows.Select(row => new PriceOfferDto(
            row.Supplier,
            row.Brand,
            row.Article,
            row.Name,
            row.Price,
            "AED",
            row.StockHint,
            row.LeadTime)).ToArray();

        return new PriceLookupResult(
            true,
            request.NormalizedBrand,
            request.NormalizedArticle,
            offers,
            offers.Length > 0 ? "repository" : "repository-empty",
            offers.Length > 0 ? string.Empty : "No offers returned for this brand/article; confirm tenant DB data, CSV fixture, or PHP baseline samples.");
    }
}
