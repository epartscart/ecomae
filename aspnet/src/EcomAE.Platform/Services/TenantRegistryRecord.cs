namespace EcomAE.Platform.Services;

public sealed record TenantRegistryRecord(
    string Host,
    TenantMode Mode,
    string? SiteKey,
    string? DatabaseName,
    bool StorefrontEnabled,
    bool ErpEnabled,
    bool ControlPanelEnabled,
    bool BosEnabled);
