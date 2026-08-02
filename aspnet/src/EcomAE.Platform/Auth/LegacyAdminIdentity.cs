namespace EcomAE.Platform.Auth;

public sealed record LegacyAdminIdentity(
    string Email,
    IReadOnlyList<int> GroupIds,
    bool HasBackendAccess,
    IReadOnlyList<ModuleAclEntry>? ModuleAcl = null)
{
    public IReadOnlyList<ModuleAclEntry> Modules => ModuleAcl ?? Array.Empty<ModuleAclEntry>();
}
