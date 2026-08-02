namespace EcomAE.Platform.Api.Catalog;

public sealed class MigrationCatalogOfflineCacheRepository : ICatalogOfflineCacheRepository
{
    public Task<CatalogVinCacheRow?> FindVinAsync(string vin, string language, string region, CancellationToken cancellationToken = default)
        => Task.FromResult<CatalogVinCacheRow?>(null);

    public Task<CatalogActionCacheRow?> FindActionCacheAsync(string cacheKey, CancellationToken cancellationToken = default)
        => Task.FromResult<CatalogActionCacheRow?>(null);
}
