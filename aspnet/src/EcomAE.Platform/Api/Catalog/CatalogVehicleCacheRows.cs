namespace EcomAE.Platform.Api.Catalog;

public sealed record CatalogModelRow(
    string Section,
    int MfaId,
    int MsId,
    string ModelSeries,
    string? YearFrom,
    string? YearTo,
    string? RawJson,
    long UpdatedAt);

public sealed record CatalogModificationRow(
    string Section,
    int MsId,
    int ModificationId,
    string Title,
    string? YearFrom,
    string? YearTo,
    string? PowerKw,
    string? CapacityLt,
    string? FuelType,
    string? RawJson,
    long UpdatedAt);

public sealed record CatalogBrandRow(
    int SupId,
    string Brand,
    string? FullName,
    string? RawJson,
    long UpdatedAt);

public sealed record CatalogCacheListResult(
    bool Ok,
    string Action,
    string Section,
    int Rows,
    string Source,
    IReadOnlyList<object> Data,
    string Message,
    int? MfaId = null,
    int? MsId = null);
