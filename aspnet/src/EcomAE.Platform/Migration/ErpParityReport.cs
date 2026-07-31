namespace EcomAE.Platform.Migration;

public sealed record ErpParityReport(
    string Surface,
    string LegacyRoute,
    string AspNetRoute,
    string Status,
    IReadOnlyCollection<string> VerifiedCapabilities,
    IReadOnlyCollection<string> RemainingGaps);
