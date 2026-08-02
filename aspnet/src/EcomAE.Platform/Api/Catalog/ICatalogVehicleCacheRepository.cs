namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogVehicleCacheRepository
{
    Task<IReadOnlyList<CatalogModelRow>> FindModelsAsync(string section, int mfaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogModificationRow>> FindModificationsAsync(string section, int msId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogBrandRow>> FindBrandsAsync(CancellationToken cancellationToken = default);
}
