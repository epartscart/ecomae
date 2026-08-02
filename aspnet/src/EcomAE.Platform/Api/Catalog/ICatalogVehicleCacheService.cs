namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogVehicleCacheService
{
    Task<CatalogCacheListResult> GetModelsAsync(string section, int mfaId, CancellationToken cancellationToken = default);

    Task<CatalogCacheListResult> GetModificationsAsync(string section, int msId, CancellationToken cancellationToken = default);

    Task<CatalogCacheListResult> GetBrandsAsync(CancellationToken cancellationToken = default);
}
