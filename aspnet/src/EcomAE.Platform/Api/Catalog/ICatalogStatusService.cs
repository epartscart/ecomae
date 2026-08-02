namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogStatusService
{
    Task<CatalogStatusPayload> GetStatusAsync(CancellationToken cancellationToken = default);
}
