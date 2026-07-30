namespace EcomAE.Platform.Api.Catalog;

public sealed record PriceLookupResult(
    bool Status,
    string Brand,
    string Article,
    IReadOnlyCollection<PriceOfferDto> Offers,
    string MigrationStatus,
    string Message);

public sealed record PriceOfferDto(
    string Supplier,
    string Brand,
    string Article,
    string Name,
    decimal Price,
    string Currency,
    int StockHint,
    string LeadTime);
