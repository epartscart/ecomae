namespace EcomAE.Platform.Api.Catalog;

public sealed record CatalogManufacturerRow(
    string Section,
    int MfaId,
    string Manufacturer,
    string? ManufacturerRu,
    string? Type,
    string? Country,
    bool Popular,
    bool IsLogo,
    string? RawJson,
    long UpdatedAt);
