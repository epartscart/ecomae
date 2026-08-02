namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogOfflineCacheRepository
{
    Task<CatalogVinCacheRow?> FindVinAsync(string vin, string language, string region, CancellationToken cancellationToken = default);

    Task<CatalogActionCacheRow?> FindActionCacheAsync(string cacheKey, CancellationToken cancellationToken = default);

    Task<CatalogActionCacheRow?> FindArticleByIdAsync(string section, string language, string region, int articleId, CancellationToken cancellationToken = default);

    Task<CatalogActionCacheRow?> FindEngineByIdAsync(string section, string language, string region, int engineId, CancellationToken cancellationToken = default);
}
