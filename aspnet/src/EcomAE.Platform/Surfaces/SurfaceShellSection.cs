namespace EcomAE.Platform.Surfaces;

public sealed record SurfaceShellSection(
    string Key,
    string Title,
    string[] Capabilities,
    string LegacyPath,
    string MigrationStatus);
