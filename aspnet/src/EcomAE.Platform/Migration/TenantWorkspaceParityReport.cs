namespace EcomAE.Platform.Migration;

public sealed record TenantWorkspaceParityReport(
    string Surface,
    string LegacyRoutes,
    string AspNetRoutes,
    string Status,
    IReadOnlyCollection<string> VerifiedCapabilities,
    IReadOnlyCollection<string> RemainingGaps);
