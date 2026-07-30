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
    string[] Permissions)
{
    public bool IsAuthenticated => Kind != LegacySessionKind.Anonymous && (UserId > 0 || Kind == LegacySessionKind.ApiKey);
}
