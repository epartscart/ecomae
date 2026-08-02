namespace EcomAE.Platform.Migration;

public sealed record StorefrontParityReport(
    string Surface,
    string LegacyRoute,
    string AspNetRoute,
    string Status,
    IReadOnlyCollection<string> VerifiedCapabilities,
    IReadOnlyCollection<string> RemainingGaps);
