namespace EcomAE.Platform.Api.Catalog;

public sealed class MigrationCatalogManufacturerRepository : ICatalogManufacturerRepository
{
    public Task<IReadOnlyList<CatalogManufacturerRow>> FindBySectionAsync(string section, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<CatalogManufacturerRow>>([]);
    }
}
