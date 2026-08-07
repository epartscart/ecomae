namespace EcomAE.Platform.Auth;

public enum LegacyLoginSurface
{
    ControlPanel,
    Erp,
    Bos,
    Ip,
    LifeOs,
    Storefront
}

public sealed record LegacyLoginRequest(
    string Contact,
    string Password,
    string ContactType,
    bool RememberMe,
    LegacyLoginSurface Surface);

public sealed record LegacyLoginSuccess(
    int UserId,
    string Email,
    string SessionToken,
    string CsrfGuardKey,
    bool AdminSession,
    string RedirectPath);

public sealed record LegacyLoginFailure(string Message, string Code);

public sealed record LegacyLoginOutcome(bool Ok, LegacyLoginSuccess? Success, LegacyLoginFailure? Failure)
{
    public static LegacyLoginOutcome Succeeded(LegacyLoginSuccess success) => new(true, success, null);

    public static LegacyLoginOutcome Failed(string message, string code = "invalid_credentials")
        => new(false, null, new LegacyLoginFailure(message, code));
}

public interface ILegacyAdminLoginService
{
    bool IsConfigured { get; }

    Task<LegacyLoginOutcome> LoginAsync(
        LegacyLoginRequest request,
        string? remoteIp,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
