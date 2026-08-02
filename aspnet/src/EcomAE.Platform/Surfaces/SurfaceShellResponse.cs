namespace EcomAE.Platform.Surfaces;

public sealed record SurfaceShellResponse(
    string Surface,
    string ShellStatus,
    string LegacyEntry,
    string AspNetRoute,
    string TenantMode,
    IReadOnlyCollection<SurfaceShellSection> Sections,
    string[] NextParityChecks);
