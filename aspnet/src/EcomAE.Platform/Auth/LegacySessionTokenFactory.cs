using System.Globalization;

namespace EcomAE.Platform.Auth;

/// <summary>
/// PHP-compatible session token / CSRF formulas for the opt-in login bridge.
/// Admin: <c>md5(contact + time + secret)</c> (cp/plugins/authentication).
/// Customer: <c>md5(contact + userId + time + secret)</c> (plugins/authentication + epc_auth_common).
/// </summary>
public static class LegacySessionTokenFactory
{
    public static string AdminSessionToken(string contactRaw, long unixTimeSeconds, string secretSuccession)
        => LegacyPasswordVerifier.Md5Hex(
            contactRaw + unixTimeSeconds.ToString(CultureInfo.InvariantCulture) + secretSuccession);

    public static string CustomerSessionToken(string contactRaw, int userId, long unixTimeSeconds, string secretSuccession)
        => LegacyPasswordVerifier.Md5Hex(
            contactRaw
            + userId.ToString(CultureInfo.InvariantCulture)
            + unixTimeSeconds.ToString(CultureInfo.InvariantCulture)
            + secretSuccession);

    public static string CsrfGuardKey(string secretSuccession, string sessionToken, string? remoteIp, string? userAgent)
        => LegacyPasswordVerifier.Sha1Hex(
            secretSuccession + sessionToken + (remoteIp ?? string.Empty) + (userAgent ?? string.Empty));

    /// <summary>
    /// Prefer first <c>X-Forwarded-For</c> hop when present (nginx → Kestrel), else connection remote IP.
    /// </summary>
    public static string? ResolveClientIp(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
