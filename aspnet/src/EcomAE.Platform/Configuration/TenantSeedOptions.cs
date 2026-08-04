using EcomAE.Platform.Services;

namespace EcomAE.Platform.Configuration;

public sealed class TenantSeedOptions
{
    public string Host { get; set; } = string.Empty;

    public string? SiteKey { get; set; }

    public string? DatabaseName { get; set; }

    /// <summary>Optional dedicated DB user (never commit secrets; prefer env / registry).</summary>
    public string? DbUser { get; set; }

    /// <summary>Optional dedicated DB password (never commit secrets).</summary>
    public string? DbPassword { get; set; }

    public bool DedicatedDb { get; set; }

    public string? ScalePolicy { get; set; }

    public TenantMode Mode { get; set; } = TenantMode.LiveTenant;

    public bool StorefrontEnabled { get; set; } = true;

    public bool ErpEnabled { get; set; } = true;

    public bool ControlPanelEnabled { get; set; } = true;

    public bool BosEnabled { get; set; }
}
