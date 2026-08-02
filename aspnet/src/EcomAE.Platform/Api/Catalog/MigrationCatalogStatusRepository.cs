namespace EcomAE.Platform.Api.Catalog;

public sealed class MigrationCatalogStatusRepository : ICatalogStatusRepository
{
    public Task<CatalogStatusPayload> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CatalogStatusPayload(
            Connected: false,
            Message: "No Epart catalog check saved yet.",
            LastChecked: 0,
            LastSuccess: 0,
            LastError: 0,
            StatusCode: 0,
            Counts: new CatalogStatusCounts(0, 0, 0, 0, 0),
            Sections: new Dictionary<string, int>(),
            CacheRows: 0,
            OfflineReady: false,
            ActionRequired:
            [
                "Configure TenantRegistry MySQL so DbCatalogStatusRepository can read epc_umapi_sync_status.",
                "Run /epc-offline-resilience-warm.php while Epart catalog is online to save catalog data."
            ],
            Source: "migration-placeholder"));
    }
}
