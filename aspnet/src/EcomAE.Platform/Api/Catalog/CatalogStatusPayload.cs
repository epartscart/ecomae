namespace EcomAE.Platform.Api.Catalog;

public sealed record CatalogStatusPayload(
    bool Connected,
    string Message,
    long LastChecked,
    long LastSuccess,
    long LastError,
    int StatusCode,
    CatalogStatusCounts Counts,
    IReadOnlyDictionary<string, int> Sections,
    int CacheRows,
    bool OfflineReady,
    IReadOnlyCollection<string> ActionRequired,
    string Source);

public sealed record CatalogStatusCounts(
    int Manufacturers,
    int Models,
    int Modifications,
    int Brands,
    int Vins);
