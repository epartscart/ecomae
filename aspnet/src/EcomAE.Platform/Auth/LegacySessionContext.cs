using EcomAE.Platform.Security;

namespace EcomAE.Platform.Auth;

public enum LegacySessionKind
{
    Anonymous,
    Customer,
    Admin,
    ApiKey
}

public sealed record LegacySessionContext(
    LegacySessionKind Kind,
    int UserId,
    string? SessionId,
    string[] Permissions,
    string Email = "",
    IReadOnlyList<int>? GroupIds = null,
    bool HasBackendAccess = false,
    IReadOnlyList<ModuleAclEntry>? ModuleAcl = null)
{
    public IReadOnlyList<int> Groups => GroupIds ?? Array.Empty<int>();

    public IReadOnlyList<ModuleAclEntry> Modules => ModuleAcl ?? Array.Empty<ModuleAclEntry>();

    public bool IsAuthenticated => Kind != LegacySessionKind.Anonymous && (UserId > 0 || Kind == LegacySessionKind.ApiKey);

    /// <summary>
    /// Coarse surface capabilities derived from session kind/permissions.
    /// Fine-grained PHP module ACL is exposed separately via <see cref="Modules"/>.
    /// </summary>
    public IReadOnlyList<string> Capabilities
    {
        get
        {
            var caps = new List<string>();
            if (Permissions.Contains(EcomAePermissions.SuperCpAccess) || Permissions.Contains(EcomAePermissions.TenantCpAccess))
            {
                caps.Add("cp");
            }

            if (Permissions.Contains(EcomAePermissions.SuperErpAccess) || Permissions.Contains(EcomAePermissions.TenantErpAccess))
            {
                caps.Add("erp");
            }

            if (Permissions.Contains(EcomAePermissions.SuperBosAccess))
            {
                caps.Add("bos");
            }

            if (Permissions.Contains(EcomAePermissions.ApiAccess))
            {
                caps.Add("api");
            }

            if (Kind == LegacySessionKind.Customer)
            {
                caps.Add("storefront_account");
            }

            return caps;
        }
    }
}
