namespace EcomAE.Platform.Auth;

public sealed record LegacyAdminIdentity(
    string Email,
    IReadOnlyList<int> GroupIds,
    bool HasBackendAccess);
