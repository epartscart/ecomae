namespace EcomAE.Platform.Api.Catalog;

public sealed record CatalogBrandPartRow(
    string Manufacturer,
    string ArticleShow,
    string Article,
    string? Name,
    decimal Exist,
    decimal Price,
    string? TimeToExe,
    string? Storage);

public sealed record CatalogBrandPartsResult(
    bool Ok,
    string Brand,
    int Rows,
    string Source,
    IReadOnlyList<object> Data,
    string Message);
