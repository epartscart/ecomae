using System.Text.RegularExpressions;

namespace EcomAE.Platform.Services;

/// <summary>
/// PHP <c>epc_client_erp_router.php</c> URL space on the Super host:
/// Super CP stays <c>/cp</c>; company ERP-only login/shell is <c>/cp/client-erp/{site_key}/</c>.
/// </summary>
public static class ClientErpPath
{
    public const string Prefix = "/cp/client-erp";
    public const string CookieName = "epc_client_erp_site_key";
    public const string HttpContextItemKey = "EcomAE.ClientErpSiteKey";

    private static readonly Regex UnsafeKey = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public readonly record struct Parsed(string SiteKey, string Rest, bool IsBare, bool IsLoginRoot, bool IsErpShell);

    public static string NormalizeSiteKey(string? siteKey)
        => UnsafeKey.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static bool IsClientErpPath(string? path)
    {
        var bare = NormalizeRequestPath(path);
        return bare.Equals(Prefix, StringComparison.OrdinalIgnoreCase)
            || bare.StartsWith(Prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParse(string? path, out Parsed parsed)
    {
        parsed = default;
        var bare = NormalizeRequestPath(path);
        if (!bare.Equals(Prefix, StringComparison.OrdinalIgnoreCase)
            && !bare.StartsWith(Prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var restAll = bare.Length <= Prefix.Length ? string.Empty : bare[(Prefix.Length + 1)..];
        var slash = restAll.IndexOf('/', StringComparison.Ordinal);
        var rawKey = slash < 0 ? restAll : restAll[..slash];
        var siteKey = NormalizeSiteKey(rawKey);
        var rest = slash < 0 ? string.Empty : restAll[(slash + 1)..].Trim('/');
        var isBare = siteKey.Length == 0;
        var isLogin = siteKey.Length > 0 && rest.Length == 0;
        var isErp = rest.Equals("erp", StringComparison.OrdinalIgnoreCase)
            || rest.StartsWith("erp/", StringComparison.OrdinalIgnoreCase);
        parsed = new Parsed(siteKey, rest, isBare, isLogin, isErp);
        return true;
    }

    public static bool TryParseSiteKey(string? path, out string siteKey)
    {
        if (TryParse(path, out var parsed) && parsed.SiteKey.Length > 0)
        {
            siteKey = parsed.SiteKey;
            return true;
        }

        siteKey = string.Empty;
        return false;
    }

    public static string LoginUrl(string siteKey)
        => Prefix + "/" + NormalizeSiteKey(siteKey) + "/";

    public static string ErpHomeUrl(string siteKey)
        => LoginUrl(siteKey) + "erp";

    /// <summary>Map a Super-host <c>/erp…</c> path onto the client-erp prefix so chrome links stay isolated.</summary>
    public static string PrefixErpPath(string siteKey, string? erpPath)
    {
        var key = NormalizeSiteKey(siteKey);
        var value = NormalizeRequestPath(erpPath);
        if (value.Equals("/erp/login", StringComparison.OrdinalIgnoreCase))
        {
            return LoginUrl(key);
        }

        if (value.Equals("/erp", StringComparison.OrdinalIgnoreCase))
        {
            return ErpHomeUrl(key);
        }

        if (value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase))
        {
            return LoginUrl(key) + "erp/" + value["/erp/".Length..];
        }

        return LoginUrl(key);
    }

    public static string? CookieSiteKey(HttpRequest? request)
    {
        if (request?.Cookies.TryGetValue(CookieName, out var raw) != true)
        {
            return null;
        }

        var key = NormalizeSiteKey(raw);
        return key.Length == 0 ? null : key;
    }

    public static void SetCookie(HttpResponse response, string siteKey)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return;
        }

        response.Cookies.Append(CookieName, key, CookieOptions(expires: DateTimeOffset.UtcNow.AddDays(14)));
    }

    public static void ClearCookie(HttpResponse response)
    {
        response.Cookies.Append(CookieName, string.Empty, CookieOptions(DateTimeOffset.UnixEpoch));
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
    }

    public static string NormalizeRequestPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var cut = path.IndexOf('?', StringComparison.Ordinal);
        var only = (cut < 0 ? path : path[..cut]).Replace('\\', '/');
        if (!only.StartsWith('/'))
        {
            only = "/" + only;
        }

        return only.TrimEnd('/').Length == 0 ? "/" : only.TrimEnd('/');
    }

    private static CookieOptions CookieOptions(DateTimeOffset? expires)
        => new()
        {
            Path = "/",
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = expires,
        };
}
