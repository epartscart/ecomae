namespace EcomAE.Platform.Api.Catalog;

public sealed record PriceOfferRow(
    string Supplier,
    string Brand,
    string Article,
    string Name,
    decimal Price,
    int StockHint,
    string LeadTime);
