namespace EcomAE.Platform.Auth;

public sealed record LegacySessionParityReport(
    string LegacySource,
    string AspNetSource,
    string Status,
    IReadOnlyCollection<string> SupportedInputs,
    IReadOnlyCollection<string> RemainingGaps);
