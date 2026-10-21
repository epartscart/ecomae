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

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Dedicated operations host: bare "/" is the ERP home. Rewriting here —
        // before AdminSurfaceAuthGate — gives "/" exactly the same auth gate a
        // typed "/erp" gets (guests are sent to /erp/login).
        if ((path is null or "" or "/")
            && ErpOperationsHost.TryParse(context.Request.Host.Host, out var operationsKey, out _))
        {
            context.Request.Path = "/erp";
            context.Response.Headers["X-EcomAE-Operations-Host"] = operationsKey;
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!ClientErpPath.TryParse(path, out var parsed) || parsed.SiteKey.Length == 0)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Canonical host for ERP-only tenants is {key}-operations-{ccc}.ecomae.com —
        // GETs through the staff /cp/client-erp/{key}/… form permanently redirect
        // there. Writes pass through unchanged (then the next GET moves the user).
        if (HttpMethods.IsGet(context.Request.Method)
            && !ErpOperationsHost.IsOperationsHost(context.Request.Host.Host)
            && !parsed.Rest.Equals("erp/logout", StringComparison.OrdinalIgnoreCase)
            && ErpOperationsHost.IsValidSlug(parsed.SiteKey))
        {
            var lookup = context.RequestServices.GetService<IClientErpTenantLookup>();
            var tenant = lookup is null
                ? null
                : await lookup.FindActiveErpOnlyAsync(parsed.SiteKey, context.RequestAborted).ConfigureAwait(false);
            var target = tenant is null
                ? string.Empty
                : ErpOperationsHost.CanonicalUrl(parsed.SiteKey, null, "/" + parsed.Rest);
            if (target.Length > 0)
            {
                var query = context.Request.QueryString.Value ?? string.Empty;
                context.Response.Redirect(target + query, permanent: true);
                return;
            }
        }

        context.Items[ClientErpPath.HttpContextItemKey] = parsed.SiteKey;
        if (!parsed.IsErpShell)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Items[OriginalPathItem] = path;
        context.Request.Path = "/" + parsed.Rest;
        context.Response.Headers["X-EcomAE-Client-Erp"] = parsed.SiteKey;
        await _next(context).ConfigureAwait(false);
    }
}
