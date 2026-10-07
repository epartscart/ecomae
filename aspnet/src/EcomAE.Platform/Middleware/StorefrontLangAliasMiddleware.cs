using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// PHP storefront pages live under every CMS lang prefix (<c>/en</c>, <c>/ar</c>,
/// <c>/me</c>, <c>/ru</c>). ASP.NET Blazor routes are registered on <c>/en/…</c>.
/// Arabic, Russian, and Montenegrin deep links from the lang home
/// (<c>/ar/parts</c>, <c>/ru/vehicle-catalog</c>, …) rewrite internally onto
/// those routes. The browser URL stays on the visitor's prefix.
/// Exact lang homes stay with <see cref="LangHomeFallbackMiddleware"/>.
/// </summary>
public sealed class StorefrontLangAliasMiddleware
{
    public const string HeaderName = "X-EcomAE-Lang-Alias";

    private static readonly HashSet<string> SkipFirstSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "assets",
        "auth",
        "blockchain",
        "bos",
        "content",
        "cp",
        "epc-static.php",
        "erp",
        "health",
        "ip",
        "lifeos",
        "marketing",
        "migration",
        "php-reference",
        "platform",
        "platform-assets",
        "ready",
        "storefront",
    };

    private readonly RequestDelegate _next;

    public StorefrontLangAliasMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!TryRewrite(path, out var lang, out var aspNetPath))
        {
            return _next(context);
        }

        context.Items[LangHomeFallbackMiddleware.OriginalPathItem] = path;
        context.Items[LangHomeFallbackMiddleware.LangItem] = lang;
        context.Response.Headers[HeaderName] = lang;
        context.Request.Path = aspNetPath;
        return _next(context);
    }

    /// <summary>
    /// <c>/ar/parts</c> → <c>/en/parts</c>. English paths and exact lang homes are unchanged.
    /// </summary>
    public static bool TryRewrite(string path, out string lang, out string aspNetPath)
    {
        lang = string.Empty;
        aspNetPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || LangHomeFallbackMiddleware.TryMatchLangHome(path, out _))
        {
            return false;
        }

        var q = path.IndexOf('?', StringComparison.Ordinal);
        var only = (q < 0 ? path : path[..q]).TrimEnd('/');
        if (only.Length < 4 || only[0] != '/')
        {
            return false;
        }

        var slash = only.IndexOf('/', 1);
        if (slash < 0)
        {
            return false;
        }

        lang = only[1..slash].ToLowerInvariant();
        if (lang is not ("ar" or "ru" or "me"))
        {
            return false;
        }

        var rest = only[slash..];
        if (rest.Length <= 1 || IsSkipped(rest))
        {
            return false;
        }

        aspNetPath = "/en" + rest;
        return true;
    }

    private static bool IsSkipped(string rest)
    {
        var segment = rest.TrimStart('/');
        var cut = segment.IndexOf('/', StringComparison.Ordinal);
        if (cut >= 0)
        {
            segment = segment[..cut];
        }

        return SkipFirstSegments.Contains(segment);
    }
}
