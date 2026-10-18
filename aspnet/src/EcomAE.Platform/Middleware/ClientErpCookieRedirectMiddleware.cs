using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// ERP chrome uses absolute <c>/erp/…</c> links. After a client-erp login the Super host
/// sends those links back under <c>/cp/client-erp/{key}/</c>.
/// Opening <c>/erp</c> itself stays the platform workspace and drops the company cookie.
/// </summary>
public sealed class ClientErpCookieRedirectMiddleware
{
    private readonly RequestDelegate _next;

    public ClientErpCookieRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (!PlatformHostPolicy.IsSuperCpHost(host))
        {
            return _next(context);
        }

        var path = context.Request.Path.Value ?? "/";
        if (ClientErpPath.IsClientErpPath(path) || ClientErpPath.IsLogoutPath(path))
        {
            return _next(context);
        }

        if (!AdminSurfaceAuthGateMiddleware.IsErpPath(path))
        {
            return _next(context);
        }

        var key = ClientErpPath.CookieSiteKey(context.Request);
        if (key is null)
        {
            return _next(context);
        }

        // ecomae.com/erp is the platform workspace. A leftover company cookie must not
        // send that address to /cp/client-erp/{key}. A click that started inside the
        // company (Dashboard) still returns there.
        if (ClientErpPath.IsPlatformErpEntry(path) && !ClientErpPath.RefererKeepsCompany(context.Request, key))
        {
            ClientErpPath.ClearCookie(context.Response);
            context.Response.Headers["X-EcomAE-Client-Erp"] = "platform-entry";
            context.Response.Headers["Cache-Control"] = "no-store";
            return _next(context);
        }

        // Fleet / Super-mail list every tenant. Never prefix those onto a company URL.
        var dest = ClientErpPath.IsSuperOnlyErpPath(path)
            ? ClientErpPath.ErpHomeUrl(key)
            : ClientErpPath.PrefixErpPath(key, path);
        if (!ClientErpPath.IsSuperOnlyErpPath(path) && context.Request.QueryString.HasValue)
        {
            dest += context.Request.QueryString.Value;
        }

        // GET can 302. POST/PUT/PATCH/DELETE must 307 or the browser drops the body
        // and company Save buttons (pm/save, vouchers, …) appear to do nothing.
        var method = context.Request.Method;
        var preserve = HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
        context.Response.StatusCode = preserve
            ? StatusCodes.Status307TemporaryRedirect
            : StatusCodes.Status302Found;
        context.Response.Headers.Location = dest;
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-EcomAE-Client-Erp"] = preserve ? "cookie-redirect-write" : "cookie-redirect";
        return Task.CompletedTask;
    }
}
