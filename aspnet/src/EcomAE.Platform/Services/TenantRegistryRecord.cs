namespace EcomAE.Platform.Services;

/// <summary>
/// Hostname registry row. <see cref="DbUser"/> / <see cref="DbPassword"/> are for connection open only —
/// never serialize into digests, dual-samples, or logs.
/// </summary>
public sealed record TenantRegistryRecord(
    string Host,
    TenantMode Mode,
    string? SiteKey,
    string? DatabaseName,
    bool StorefrontEnabled,
    bool ErpEnabled,
    bool ControlPanelEnabled,
    bool BosEnabled,
    string? DbUser = null,
    string? DbPassword = null,
    bool DedicatedDb = false,
    string? ScalePolicy = null);
