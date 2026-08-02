namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogBrandPartsService
{
    Task<CatalogBrandPartsResult> ListAsync(string? brand, int limit, int offset, CancellationToken cancellationToken = default);
}
