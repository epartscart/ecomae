namespace EcomAE.Platform.Services;

public enum TenantSurface
{
    Storefront,
    ControlPanel,
    Erp,
    Bos,
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

public sealed record TenantContext(
    string Host,
    string Path,
    TenantSurface Surface,
    TenantMode Mode,
    string? SiteKey = null,
    string? DatabaseName = null)
{
    public static TenantContext ForKnownTenant(
        string siteKey,
        string host,
        TenantMode mode,
        TenantSurface surface,
        string path,
        string? databaseName = null)
    {
        return new TenantContext(
            Host: host,
            Path: path,
            Surface: surface,
            Mode: mode,
            SiteKey: siteKey,
            DatabaseName: databaseName);
    }

    public bool IsPlatform => Mode == TenantMode.Platform;

    public bool IsTenant => Mode is TenantMode.LiveTenant or TenantMode.ErpOnlyTenant or TenantMode.DemoTenant;
}
