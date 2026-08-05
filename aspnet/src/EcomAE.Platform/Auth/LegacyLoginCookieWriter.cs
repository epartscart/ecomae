namespace EcomAE.Platform.Auth;

/// <summary>Sets / clears PHP-compatible admin/customer session cookies on the response.</summary>
public static class LegacyLoginCookieWriter
{
    public static void Apply(HttpResponse response, LegacyLoginSuccess success, bool rememberMe)
    {
        var options = BuildOptions(rememberMe ? DateTimeOffset.UtcNow.AddDays(115) : null);

        if (success.AdminSession)
        {
            response.Cookies.Append("admin_session", success.SessionToken, options);
            response.Cookies.Append("admin_u_id", success.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture), options);
        }
        else
        {
            response.Cookies.Append("session", success.SessionToken, options);
            response.Cookies.Append("u_id", success.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture), options);
        }
    }

    /// <summary>Expire PHP-compatible auth cookies (admin + customer + ERP shell hints).</summary>
    public static void ClearAll(HttpResponse response)
    {
        var expired = BuildOptions(DateTimeOffset.UnixEpoch);
        foreach (var name in new[]
                 {
                     "admin_session",
                     "admin_u_id",
                     "session",
                     "u_id",
                     "epc_erp_shell",
                 })
        {
            response.Cookies.Append(name, string.Empty, expired);
            response.Cookies.Delete(name, new CookieOptions { Path = "/" });
        }
    }

    private static CookieOptions BuildOptions(DateTimeOffset? expires)
        => new()
        {
            Path = "/",
            HttpOnly = true,
            Secure = false, // matches PHP setcookie(..., secure=false, httponly=true)
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = expires,
        };
}
