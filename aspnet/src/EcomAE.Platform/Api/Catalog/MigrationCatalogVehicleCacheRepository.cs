namespace EcomAE.Platform.Api.Catalog;

public sealed class MigrationCatalogVehicleCacheRepository : ICatalogVehicleCacheRepository
{
    public Task<IReadOnlyList<CatalogModelRow>> FindModelsAsync(string section, int mfaId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CatalogModelRow>>([]);

    public Task<IReadOnlyList<CatalogModificationRow>> FindModificationsAsync(string section, int msId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CatalogModificationRow>>([]);

    public Task<IReadOnlyList<CatalogBrandRow>> FindBrandsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CatalogBrandRow>>([]);
}
