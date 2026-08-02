namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogOfflineCacheService
{
    Task<CatalogVinLookupResult> LookupVinAsync(string? vin, string? language, string? region, CancellationToken cancellationToken = default);

    Task<CatalogActionCacheLookupResult> LookupEnginesAsync(string? section, int mfaId, string? language, string? region, CancellationToken cancellationToken = default);

    Task<CatalogActionCacheLookupResult> LookupAnalogsAsync(string? section, string? article, string? brand, string? language, string? region, CancellationToken cancellationToken = default);

    Task<CatalogActionCacheLookupResult> LookupArticleBrandsAsync(string? section, string? article, string? language, string? region, CancellationToken cancellationToken = default);
}
