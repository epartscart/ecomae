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

public sealed record LegacyLoginFailure(string Message, string Code, int WaitMinutes = 0)
{
    /// <summary>The login page query for this failure: <c>error=code</c>, plus <c>wait</c> minutes for a rate-limited attempt.</summary>
    public string Query
        => "error=" + Uri.EscapeDataString(Code) + (WaitMinutes > 0 ? "&wait=" + WaitMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty);
}

public sealed record LegacyLoginOutcome(bool Ok, LegacyLoginSuccess? Success, LegacyLoginFailure? Failure)
{
    public static LegacyLoginOutcome Succeeded(LegacyLoginSuccess success) => new(true, success, null);

    public static LegacyLoginOutcome Failed(string message, string code = "invalid_credentials", int waitMinutes = 0)
        => new(false, null, new LegacyLoginFailure(message, code, waitMinutes));
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
