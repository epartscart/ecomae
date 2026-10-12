using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// After tenant bind, map <c>/cp/client-erp/{key}/erp…</c> onto the ERP Blazor routes
/// without leaving the Super-host isolation rule for bare <c>/cp</c> and <c>/erp</c>.
/// </summary>
public sealed class ClientErpPathRewriteMiddleware
{
    public const string OriginalPathItem = "EcomAE.ClientErpOriginalPath";

    private readonly RequestDelegate _next;

    public ClientErpPathRewriteMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!ClientErpPath.TryParse(path, out var parsed) || parsed.SiteKey.Length == 0)
        {
            return _next(context);
        }

        context.Items[ClientErpPath.HttpContextItemKey] = parsed.SiteKey;
        if (!parsed.IsErpShell)
        {
            return _next(context);
        }

        context.Items[OriginalPathItem] = path;
        context.Request.Path = "/" + parsed.Rest;
        context.Response.Headers["X-EcomAE-Client-Erp"] = parsed.SiteKey;
        return _next(context);
    }
}
