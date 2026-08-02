namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogManufacturerService
{
    Task<CatalogManufacturersResult> GetBySectionAsync(string section, CancellationToken cancellationToken = default);
}

public sealed record CatalogManufacturersResult(
    bool Ok,
    string Section,
    int Rows,
    string Source,
    IReadOnlyList<object> Data,
    string Message);
