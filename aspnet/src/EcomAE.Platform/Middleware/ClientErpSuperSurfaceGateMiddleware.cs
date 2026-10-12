using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Company ERP on the Super host must never render Super fleet / tenant lists.
/// After <see cref="ClientErpPathRewriteMiddleware"/> the path looks like
/// <c>/erp/fleet-app</c> while the bind item/cookie still names the company.
/// </summary>
public sealed class ClientErpSuperSurfaceGateMiddleware
{
    private readonly RequestDelegate _next;

    public ClientErpSuperSurfaceGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (!ClientErpPath.IsSuperOnlyErpPath(path))
        {
            return _next(context);
        }

        var key = ClientErpPath.BoundSiteKey(context);
        if (key is null
            && context.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant
            && tenant.IsTenant
            && !string.IsNullOrWhiteSpace(tenant.SiteKey))
        {
            key = ClientErpPath.NormalizeSiteKey(tenant.SiteKey);
        }

        if (key is null || key.Length == 0)
        {
            return _next(context);
        }

        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.Headers.Location = ClientErpPath.ErpHomeUrl(key);
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-EcomAE-Client-Erp"] = "super-surface-blocked";
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return Task.CompletedTask;
    }
}
