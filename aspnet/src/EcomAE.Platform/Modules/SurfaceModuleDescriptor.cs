namespace EcomAE.Platform.Modules;

public sealed record SurfaceModuleDescriptor(
    string Key,
    string DisplayName,
    string RoutePrefix,
    string LegacyPhpArea,
    string MigrationStatus,
    string[] RequiredPermissions);
