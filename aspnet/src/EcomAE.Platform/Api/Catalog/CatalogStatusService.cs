namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogStatusService : ICatalogStatusService
{
    private readonly ICatalogStatusRepository _repository;

    public CatalogStatusService(ICatalogStatusRepository repository)
    {
        _repository = repository;
    }

    public Task<CatalogStatusPayload> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetStatusAsync(cancellationToken);
    }
}
