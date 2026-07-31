namespace EcomAE.Platform.Auth;

public sealed record LegacyApiClientParityReport(
    string LegacySource,
    string AspNetSource,
    string Status,
    IReadOnlyCollection<string> SupportedPrefixes,
    IReadOnlyCollection<string> EnforcedRules,
    IReadOnlyCollection<string> RemainingGaps);
