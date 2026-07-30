namespace EcomAE.Platform.Api.Catalog;

public sealed record CatalogParityReport(
    string LegacySource,
    string AspNetSource,
    string Status,
    bool ReadyForShadowTraffic,
    IReadOnlyCollection<string> RemainingGaps);
