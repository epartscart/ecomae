namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP storefront lang prefixes used on live tenants (<c>/en</c>, <c>/ar</c>, <c>/me</c>, <c>/ru</c>).
/// </summary>
public static class StorefrontLangPrefix
{
    public static readonly string[] All = ["/en", "/ar", "/me", "/ru"];

    public static bool TryStrip(string pathAndQuery, out string remainder)
    {
        remainder = pathAndQuery;
        if (string.IsNullOrWhiteSpace(pathAndQuery))
        {
            return false;
        }

        var qIndex = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        var path = qIndex < 0 ? pathAndQuery : pathAndQuery[..qIndex];
        var query = qIndex < 0 ? string.Empty : pathAndQuery[qIndex..];
        foreach (var lang in All)
        {
            if (path.Equals(lang, StringComparison.OrdinalIgnoreCase))
            {
                remainder = "/" + query;
                return true;
            }

            if (path.StartsWith(lang + "/", StringComparison.OrdinalIgnoreCase))
            {
                remainder = path[lang.Length..] + query;
                return true;
            }
        }

        return false;
    }

    public static string Strip(string pathAndQuery)
        => TryStrip(pathAndQuery, out var remainder) ? remainder : pathAndQuery;

    /// <summary>
    /// <c>/en/cp</c>, <c>/ar/erp/foo</c>, <c>/en/bos</c> → same path without the lang prefix
    /// so admin gates and Blazor routes match PHP header links on custom-package tenants.
    /// </summary>
    public static bool TryStripAdminShell(string path, out string stripped)
    {
        stripped = path;
        if (!TryStrip(path, out var rest))
        {
            return false;
        }

        var q = rest.IndexOf('?', StringComparison.Ordinal);
        var only = (q < 0 ? rest : rest[..q]).TrimEnd('/');
        if (only.Equals("/cp", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || only.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || only.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || only.Equals("/ip", StringComparison.OrdinalIgnoreCase)
            || only.StartsWith("/ip/", StringComparison.OrdinalIgnoreCase)
            || only.Equals("/CP", StringComparison.Ordinal)
            || only.StartsWith("/CP/", StringComparison.Ordinal)
            || only.Equals("/ERP", StringComparison.Ordinal)
            || only.StartsWith("/ERP/", StringComparison.Ordinal)
            || only.Equals("/BOS", StringComparison.Ordinal)
            || only.StartsWith("/BOS/", StringComparison.Ordinal))
        {
            stripped = rest;
            return true;
        }

        return false;
    }
}
