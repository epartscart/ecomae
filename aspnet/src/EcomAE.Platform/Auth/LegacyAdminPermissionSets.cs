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

    public static string[] ForRequestHost(string? host)
        => PlatformHostPolicy.IsSuperCpHost(host) ? SuperHostAdminPermissions : TenantHostAdminPermissions;
}
