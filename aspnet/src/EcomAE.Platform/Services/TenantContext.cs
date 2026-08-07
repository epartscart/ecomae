namespace EcomAE.Platform.Services;

public enum TenantSurface
{
    Storefront,
    ControlPanel,
    Erp,
    Bos,
    /// <summary>Intelligence Platform — Super-CP ecosystem hub (LifeOS, HealthOS, …).</summary>
    Ip,
    /// <summary>LifeOS customer product surface (lifeos.ecomae.com).</summary>
    LifeOs,
    Api
}

public enum TenantMode
{
    Platform,
    LiveTenant,
    ErpOnlyTenant,
    DemoTenant,
    IndustrySubdomain,
    Unknown
}

/// <summary>
/// Request-scoped tenant identity. DB credentials are connection-only — never log or put in digests.
/// </summary>
public sealed record TenantContext(
    string Host,
    string Path,
    TenantSurface Surface,
    TenantMode Mode,
    string? SiteKey = null,
    string? DatabaseName = null,
    string? DbUser = null,
    string? DbPassword = null,
    bool DedicatedDb = false)
{
    public static TenantContext ForKnownTenant(
        string siteKey,
        string host,
        TenantMode mode,
        TenantSurface surface,
        string path,
        string? databaseName = null,
        string? dbUser = null,
        string? dbPassword = null,
        bool dedicatedDb = false)
    {
        return new TenantContext(
            Host: host,
            Path: path,
            Surface: surface,
            Mode: mode,
            SiteKey: siteKey,
            DatabaseName: databaseName,
            DbUser: dbUser,
            DbPassword: dbPassword,
            DedicatedDb: dedicatedDb);
    }

    public bool IsPlatform => Mode == TenantMode.Platform;

    public bool IsTenant => Mode is TenantMode.LiveTenant or TenantMode.ErpOnlyTenant or TenantMode.DemoTenant;

    /// <summary>True when this request should open a dedicated tenant schema (not the registry DB).</summary>
    public bool HasTenantDatabase => !string.IsNullOrWhiteSpace(DatabaseName);
}
