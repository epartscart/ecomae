using EcomAE.Platform.Security;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Host-scoped admin permission minting. Super CP/ERP/BOS claims are only granted on
/// platform Super hosts — tenant CP admins must never receive fleet-wide Super capabilities.
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

    private static readonly string[] TenantErpOnlyPermissions =
    [
        EcomAePermissions.TenantErpAccess,
        EcomAePermissions.ApiAccess
    ];

    private static readonly string[] TenantCpOnlyPermissions =
    [
        EcomAePermissions.TenantCpAccess,
        EcomAePermissions.ApiAccess
    ];

    public static string[] ForRequestHost(string? host)
        => ForRequest(host, TenantMode.Unknown);

    /// <summary>
    /// Mint surface capabilities from host + tenant mode. ERP-only tenants never receive CP
    /// claims; Super hosts keep the full operator set.
    /// </summary>
    public static string[] ForRequest(string? host, TenantMode mode)
    {
        if (PlatformHostPolicy.IsSuperCpHost(host))
        {
            return SuperHostAdminPermissions;
        }

        return mode switch
        {
            TenantMode.ErpOnlyTenant => TenantErpOnlyPermissions,
            _ => TenantHostAdminPermissions
        };
    }

    public static string[] ForCpOnlyTenant() => TenantCpOnlyPermissions;

    public static string[] ForErpOnlyTenant() => TenantErpOnlyPermissions;
}
