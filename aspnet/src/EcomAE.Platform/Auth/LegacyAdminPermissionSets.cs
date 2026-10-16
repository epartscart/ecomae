using EcomAE.Platform.Middleware;
using EcomAE.Platform.Security;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Host-scoped admin permission minting. Super CP/ERP/BOS claims are only granted on
/// an unbound platform Super host. A company opened at <c>/cp/client-erp/{key}</c> on
/// that same host stays a tenant: it can run its own books and cannot receive fleet scope.
/// SOP: <c>docs/PROJECT_SOP_SECURITY_TENANT_ISOLATION.md</c> P3 least privilege.
/// </summary>
public static class LegacyAdminPermissionSets
{
    private static readonly string[] SuperHostAdminPermissions =
    [
        EcomAePermissions.SuperCpAccess,
        EcomAePermissions.SuperErpAccess,
        EcomAePermissions.SuperBosAccess,
        EcomAePermissions.TenantCpAccess,
        EcomAePermissions.TenantErpAccess,
        EcomAePermissions.ApiAccess
    ];

    private static readonly string[] TenantHostAdminPermissions =
    [
        EcomAePermissions.TenantCpAccess,
        EcomAePermissions.TenantErpAccess,
        EcomAePermissions.ApiAccess
    ];

    public static string[] ForRequestHost(string? host)
        => PlatformHostPolicy.IsSuperCpHost(host) ? SuperHostAdminPermissions : TenantHostAdminPermissions;

    /// <summary>
    /// Super claims follow the host only when this request is the platform workspace.
    /// A bound company, including one served on www.ecomae.com, receives tenant claims.
    /// </summary>
    public static string[] ForRequest(HttpContext? http)
    {
        if (http is null || IsCompanyRequest(http))
        {
            return TenantHostAdminPermissions;
        }

        return ForRequestHost(http.Request.Host.Host);
    }

    /// <summary>
    /// True when this request is a company workspace. A leftover company cookie on
    /// unbound <c>/erp</c> or <c>/bos</c> does not count — those stay the platform.
    /// </summary>
    public static bool IsCompanyRequest(HttpContext? http)
    {
        if (http is null)
        {
            return false;
        }

        if (ClientErpPath.TryParse(http.Request.Path.Value, out var parsed) && parsed.SiteKey.Length > 0)
        {
            return true;
        }

        if (http.Items.TryGetValue(ClientErpPath.HttpContextItemKey, out var raw)
            && raw is string item
            && ClientErpPath.NormalizeSiteKey(item).Length > 0)
        {
            return true;
        }

        if (http.Items.TryGetValue(ClientErpPathRewriteMiddleware.OriginalPathItem, out var original)
            && original is string originalPath
            && ClientErpPath.TryParse(originalPath, out var fromOriginal)
            && fromOriginal.SiteKey.Length > 0)
        {
            return true;
        }

        return http.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant
            && tenant.IsTenant;
    }
}
