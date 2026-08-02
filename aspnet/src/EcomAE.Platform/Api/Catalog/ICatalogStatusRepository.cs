namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogStatusRepository
{
    Task<CatalogStatusPayload> GetStatusAsync(CancellationToken cancellationToken = default);
}
