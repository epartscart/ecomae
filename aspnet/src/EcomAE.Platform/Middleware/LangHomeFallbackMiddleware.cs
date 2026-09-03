namespace EcomAE.Platform.Middleware;

/// <summary>
/// Exact CMS lang homes (<c>/en/</c>, <c>/ar/</c>, <c>/me/</c>, <c>/ru/</c>) must
/// render the same storefront home as <c>/</c>. Internally rewrite to
/// <c>/storefront/app</c> before <c>UseRouting</c> so a missing Blazor
/// <c>@page "/en/"</c> cannot 404 (live symptom). Browser URL stays
/// <c>/en/</c> for sticky English / hreflang.
/// </summary>
public sealed class LangHomeFallbackMiddleware
{
    public const string OriginalPathItem = "EpcLangHomeOriginalPath";
    public const string LangItem = "EpcLangHomeCode";
    public const string HeaderName = "X-EcomAE-Lang-Home";

    private readonly RequestDelegate _next;

    public LangHomeFallbackMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!TryMatchLangHome(path, out var lang))
        {
            return _next(context);
        }

        context.Items[OriginalPathItem] = path;
        context.Items[LangItem] = lang;
        context.Response.Headers[HeaderName] = lang;
        context.Request.Path = "/storefront/app";
        return _next(context);
    }

    public static bool TryMatchLangHome(string path, out string lang)
    {
        lang = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var q = path.IndexOf('?', StringComparison.Ordinal);
        var only = (q < 0 ? path : path[..q]).TrimEnd('/');
        if (only.Equals("/en", StringComparison.OrdinalIgnoreCase)
            || only.Equals("/ar", StringComparison.OrdinalIgnoreCase)
            || only.Equals("/me", StringComparison.OrdinalIgnoreCase)
            || only.Equals("/ru", StringComparison.OrdinalIgnoreCase))
        {
            lang = only.TrimStart('/').ToLowerInvariant();
            return true;
        }

        return false;
    }

    public static string RequestCmsLang(HttpContext? http, string fallback = "en")
    {
        if (http?.Items[LangItem] is string stored && stored.Length == 2)
        {
            return stored;
        }

        var path = http?.Request.Path.Value ?? "/";
        if (http?.Items[OriginalPathItem] is string original && !string.IsNullOrWhiteSpace(original))
        {
            path = original;
        }

        var m = System.Text.RegularExpressions.Regex.Match(
            path, "^/([a-z]{2})(?:/|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : fallback;
    }
}
