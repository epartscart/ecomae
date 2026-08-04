namespace EcomAE.Platform.Migration;

public sealed record LiveSurfaceLinkReport(
    string Status,
    string PlatformHost,
    IReadOnlyCollection<LiveSurfaceLink> Links,
    IReadOnlyCollection<string> CutoverRules,
    IReadOnlyCollection<string> NextActions,
    bool CutoverAllowed = false,
    bool ReadyForPhpRemoval = false);
