namespace EcomAE.Platform.Api.Catalog;

public sealed record CatalogVinCacheRow(
    string Vin,
    string Language,
    string Region,
    string ResponseJson,
    int VehicleCount,
    string? Manufacturer,
    string? ModelLabel,
    int HttpStatus,
    long UpdatedAt);

public sealed record CatalogActionCacheRow(
    string CacheKey,
    string Action,
    string Section,
    string Language,
    string Region,
    string ResponseJson,
    int RowsCount,
    int HttpStatus,
    long LastSync);

public sealed record CatalogVinLookupResult(
    bool Ok,
    int StatusCode,
    string Code,
    string Message,
    object? Payload);

public sealed record CatalogActionCacheLookupResult(
    bool Ok,
    int StatusCode,
    string Code,
    string Message,
    string Action,
    string Section,
    object? Payload,
    int Rows,
    bool Stale,
    string Source);
