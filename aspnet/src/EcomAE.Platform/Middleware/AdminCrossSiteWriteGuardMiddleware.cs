namespace EcomAE.Platform.Middleware;

/// <summary>
/// Cross-site write guard for the admin surfaces (/cp /erp /bos /ip). Their write endpoints are
/// cookie-authenticated and many run with <c>DisableAntiforgery()</c>, so an unsafe-method request
/// whose browser-supplied <c>Origin</c>/<c>Referer</c> (or <c>Sec-Fetch-Site</c>) points at another
/// site is refused before any handler runs. Requests without browser origin metadata (CLI, tests,
/// same-document fetches) pass through unchanged.
/// </summary>
public sealed class AdminCrossSiteWriteGuardMiddleware
{
    public const string Header = "X-EcomAE-Cross-Site-Guard";

    private readonly RequestDelegate _next;

    public AdminCrossSiteWriteGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!ShouldReject(
                request.Method,
                request.Path.Value ?? "/",
                request.Host.Host,
                request.Headers.Origin.ToString(),
                request.Headers.Referer.ToString(),
                request.Headers["Sec-Fetch-Site"].ToString()))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers[Header] = "rejected";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(
            "{\"ok\":false,\"code\":\"cross_site_write_rejected\",\"message\":\"Error! CSRF 4\"}");
    }

    public static bool ShouldReject(string method, string path, string requestHost, string origin, string referer, string secFetchSite)
    {
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return false;
        }

        if (!AdminSurfaceAuthGateMiddleware.IsAdminSurface(path.Trim())
            || path.StartsWith("/php-reference", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(secFetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var source = !string.IsNullOrWhiteSpace(origin) && !string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase)
            ? origin
            : referer;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return true;
        }

        return !string.Equals(uri.Host, requestHost, StringComparison.OrdinalIgnoreCase);
    }
}
