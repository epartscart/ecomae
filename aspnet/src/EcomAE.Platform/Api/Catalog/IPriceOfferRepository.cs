namespace EcomAE.Platform.Api.Catalog;

public interface IPriceOfferRepository
{
    Task<IReadOnlyCollection<PriceOfferRow>> FindOffersAsync(string normalizedBrand, string normalizedArticle, CancellationToken cancellationToken = default);
}
