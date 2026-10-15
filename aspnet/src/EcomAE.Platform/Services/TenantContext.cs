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
    bool DedicatedDb = false,
    string? IndustryCode = null,
    string? DisplayName = null)
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
        bool dedicatedDb = false,
        string? industryCode = null)
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
            DedicatedDb: dedicatedDb,
            IndustryCode: industryCode);
    }

    public bool IsPlatform => Mode == TenantMode.Platform;

    public bool IsTenant => Mode is TenantMode.LiveTenant or TenantMode.ErpOnlyTenant or TenantMode.DemoTenant;

    /// <summary>True when this request should open a dedicated tenant schema (not the registry DB).</summary>
    public bool HasTenantDatabase => !string.IsNullOrWhiteSpace(DatabaseName);

    /// <summary>
    /// Dedicated MySQL user+password only when both are present. ERP-only onboard
    /// stores <c>db_user=site_key</c> with an empty password — that must not invent
    /// a MySQL account (1045). Open <see cref="DatabaseName"/> with the registry CS.
    /// </summary>
    public bool HasUsableDedicatedCredentials => UsableDedicatedCredentials(DbUser, DbPassword);

    public static bool UsableDedicatedCredentials(string? dbUser, string? dbPassword)
        => !string.IsNullOrWhiteSpace(dbUser) && !string.IsNullOrWhiteSpace(dbPassword);

    /// <summary>
    /// Public diagnostic projection — never includes <see cref="DbUser"/> / <see cref="DbPassword"/>.
    /// </summary>
    public TenantContextPublicDto ToPublicDto() => new(
        Host,
        Path,
        Surface.ToString(),
        Mode.ToString(),
        SiteKey,
        HasTenantDatabase,
        DedicatedDb,
        HasDbCredentials: !string.IsNullOrWhiteSpace(DbUser) || !string.IsNullOrWhiteSpace(DbPassword),
        IndustryCode);

    public static string? IndustryFrom(HttpContext? context)
        => context?.Items[EcomAE.Platform.Middleware.TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant
            && !string.IsNullOrWhiteSpace(tenant.IndustryCode)
            ? tenant.IndustryCode.Trim()
            : null;
}

/// <summary>Safe tenant identity for HTTP digests — credentials never serialized.</summary>
public sealed record TenantContextPublicDto(
    string Host,
    string Path,
    string Surface,
    string Mode,
    string? SiteKey,
    bool HasTenantDatabase,
    bool DedicatedDb,
    bool HasDbCredentials,
    string? IndustryCode = null);
