using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Loopback deploy probes (<c>/health</c>, <c>/ready</c>, <c>/migration/release</c>)
/// must not run tenant resolution or Blazor. A CloudPanel publish that 500s those
/// paths rolls back the new binary.
/// </summary>
public sealed class ProbeBypassMiddleware
{
    private readonly RequestDelegate _next;

    public ProbeBypassMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context, IHostEnvironment environment)
    {
        var path = context.Request.Path.Value ?? "/";
        if (path.Equals(EcomAeRoutes.Health, StringComparison.OrdinalIgnoreCase)
            || path.Equals(EcomAeRoutes.Health + "/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("Healthy");
        }

        if (path.Equals(EcomAeRoutes.Readiness, StringComparison.OrdinalIgnoreCase)
            || path.Equals(EcomAeRoutes.Readiness + "/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            return context.Response.WriteAsync("""{"status":"ready"}""");
        }

        if (path.Equals(EcomAeRoutes.ReleaseIdentity, StringComparison.OrdinalIgnoreCase)
            || path.Equals(EcomAeRoutes.ReleaseIdentity + "/", StringComparison.OrdinalIgnoreCase))
        {
            var releaseFile = Path.Combine(environment.ContentRootPath, "RELEASE_SHA");
            var commitSha = File.Exists(releaseFile)
                ? File.ReadAllText(releaseFile).Trim()
                : "unknown";
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            return context.Response.WriteAsync(
                "{\"commitSha\":\"" + commitSha + "\",\"phpReferenceOnly\":true,\"broadCutoverAllowed\":false}");
        }

        return _next(context);
    }
}
