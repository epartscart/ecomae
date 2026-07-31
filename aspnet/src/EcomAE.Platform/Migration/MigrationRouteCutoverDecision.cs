using EcomAE.Platform.Services;

namespace EcomAE.Platform.Migration;

public sealed record MigrationRouteCutoverDecision(
    TenantSurface Surface,
    TenantMode TenantMode,
    string TargetRuntime,
    string Reason,
    bool RequiresPhpFallback,
    bool ReadyForAspNetTraffic);
