namespace EcomAE.Platform.Auth;

/// <summary>Sets PHP-compatible admin/customer session cookies on the response.</summary>
public static class LegacyLoginCookieWriter
{
    public static void Apply(HttpResponse response, LegacyLoginSuccess success, bool rememberMe)
    {
        var options = new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = false, // matches PHP setcookie(..., secure=false, httponly=true)
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        };
        if (rememberMe)
        {
            options.Expires = DateTimeOffset.UtcNow.AddDays(115);
        }

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
}
