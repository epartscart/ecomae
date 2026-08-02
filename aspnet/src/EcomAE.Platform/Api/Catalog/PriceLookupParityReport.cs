namespace EcomAE.Platform.Api.Catalog;

public sealed record PriceLookupParityReport(
    string LegacySource,
    string AspNetSource,
    string SampleBrand,
    string SampleArticle,
    string Status,
    bool ReadyForShadowTraffic,
    IReadOnlyCollection<string> RemainingGaps);
