namespace EcomAE.Platform.Migration;

public sealed record PhpReferenceModeReport(
    string Status,
    string Mode,
    bool Enabled,
    bool ArchitectureConfirmed,
    bool KeepPhpProjectAvailable,
    bool PreferAspNetStorefrontApps,
    bool TemporarilyDeactivatePhpServing,
    bool CutoverAllowed,
    bool ReadyForPhpRemoval,
    bool RequirePhpFallback,
    bool StorefrontAspNetEnabled,
    bool AdminAspNetEnabled,
    string WwwPhpBaseUrl,
    string TenantPhpBaseUrl,
    string DedicatedCpPhpBaseUrl,
    string AspNetPrimaryBaseUrl,
    string? PhpDocRoot,
    IReadOnlyList<PhpReferenceComparePair> ComparePairs,
    IReadOnlyList<string> OperatorSteps,
    IReadOnlyList<string> HardLocks,
    string Note);

public sealed record PhpReferenceComparePair(
    string Area,
    string PhpUrl,
    string AspNetUrl,
    string Role);
