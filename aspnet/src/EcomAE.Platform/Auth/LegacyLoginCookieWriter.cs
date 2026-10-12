namespace EcomAE.Platform.Auth;

/// <summary>Sets / clears PHP-compatible admin/customer session cookies on the response.</summary>
public static class LegacyLoginCookieWriter
{
    public static readonly string[] AuthCookieNames =
    [
        "admin_session",
        "admin_u_id",
        "session",
        "u_id",
        "epc_erp_shell",
        "epc_platform_erp",
        "epc_erp_tenant",
    ];

    public static void Apply(HttpResponse response, LegacyLoginSuccess success, bool rememberMe)
        => Apply(response, success, rememberMe, secure: false);

    public static void Apply(HttpResponse response, LegacyLoginSuccess success, bool rememberMe, bool secure)
    {
        var options = BuildOptions(rememberMe ? DateTimeOffset.UtcNow.AddDays(115) : null, secure);

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

    /// <summary>
    /// Expire PHP + ASP.NET auth cookies. Browsers only drop a cookie when Path / Domain / Secure match,
    /// so HTTPS Super and tenant hosts clear both Secure and non-Secure, host-only and parent-domain copies.
    /// </summary>
    public static void ClearAll(HttpResponse response)
        => ClearAll(response, host: null, https: false);

    public static void ClearAll(HttpResponse response, string? host, bool https)
    {
        // Always expire Secure and non-Secure copies. HTTPS is used by callers to
        // decide login writes; a mis-detected scheme must still drop the live cookie.
        _ = https;
        foreach (var name in AuthCookieNames)
        {
            foreach (var variant in ClearVariants(host))
            {
                response.Cookies.Append(name, string.Empty, variant);
                response.Cookies.Delete(name, new CookieOptions
                {
                    Path = variant.Path,
                    Secure = variant.Secure,
                    Domain = variant.Domain,
                    SameSite = variant.SameSite,
                    HttpOnly = variant.HttpOnly,
                });
            }
        }
    }

    public static bool RequestIsHttps(HttpRequest request)
        => request.IsHttps
           || string.Equals(request.Headers["X-Forwarded-Proto"].ToString(), "https", StringComparison.OrdinalIgnoreCase);

    internal static IReadOnlyList<string?> DomainVariants(string? host)
    {
        var rows = new List<string?> { null };
        var h = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        if (h.Length == 0 || h.Contains(':', StringComparison.Ordinal) || System.Net.IPAddress.TryParse(h, out _))
        {
            return rows;
        }

        rows.Add(h);
        var parts = h.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var parent = parts[^2] + "." + parts[^1];
            rows.Add(parent);
            rows.Add("." + parent);
        }

        return rows.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<CookieOptions> ClearVariants(string? host)
    {
        foreach (var domain in DomainVariants(host))
        {
            yield return BuildOptions(DateTimeOffset.UnixEpoch, secure: false, domain);
            yield return BuildOptions(DateTimeOffset.UnixEpoch, secure: true, domain);
        }
    }

    private static CookieOptions BuildOptions(DateTimeOffset? expires, bool secure, string? domain = null)
        => new()
        {
            Path = "/",
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = expires,
            Domain = domain,
        };
}
