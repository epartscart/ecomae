using EcomAE.Platform.Services;

namespace EcomAE.Platform.Configuration;

public sealed class TenantSeedOptions
{
    public string Host { get; set; } = string.Empty;

    public string? SiteKey { get; set; }

    public string? DatabaseName { get; set; }

    public TenantMode Mode { get; set; } = TenantMode.LiveTenant;

    public bool StorefrontEnabled { get; set; } = true;

    public bool ErpEnabled { get; set; } = true;

    public bool ControlPanelEnabled { get; set; } = true;

    public bool BosEnabled { get; set; }
}
