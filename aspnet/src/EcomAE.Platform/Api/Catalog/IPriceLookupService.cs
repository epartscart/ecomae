namespace EcomAE.Platform.Api.Catalog;

public interface IPriceLookupService
{
    ValueTask<PriceLookupResult> LookupAsync(PriceLookupRequest request, CancellationToken cancellationToken = default);
}
