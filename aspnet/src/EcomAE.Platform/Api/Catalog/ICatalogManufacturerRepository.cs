namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogManufacturerRepository
{
    Task<IReadOnlyList<CatalogManufacturerRow>> FindBySectionAsync(string section, CancellationToken cancellationToken = default);
}
