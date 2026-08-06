using EcomAE.Platform.Configuration;
using EcomAE.Platform.Presentation;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// When <see cref="PhpReferenceOptions.TemporarilyDeactivatePhpServing"/> is true,
/// blocks PHP reference URLs so ASP.NET can be tested without PHP hops.
/// Does not delete PHP files and does not flip cutover / ReadyToRemovePhp.
/// </summary>
public sealed class PhpServingDeactivatedMiddleware
{
    public const string FlagHeader = "X-EcomAE-Php-Serving";
    public const string FlagValue = "temporarily-deactivated";

    private readonly RequestDelegate _next;
    private readonly PhpReferenceOptions _options;

    public PhpServingDeactivatedMiddleware(RequestDelegate next, IOptions<PhpReferenceOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.TemporarilyDeactivatePhpServing && !StorefrontSurfaceLinks.PreferAspNetApps)
        {
            await _next(context);
            return;
        }

        context.Response.Headers[FlagHeader] = FlagValue;
        // Opaque headers only — never expose stack names on tenant-visible responses.
        context.Response.Headers["X-EcomAE-Platform"] = "primary";
        context.Response.Headers["X-EcomAE-Compat"] = "paused";

        var path = context.Request.Path.Value ?? "/";
        if (path.StartsWith("/php-reference", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/php-reference", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                "Reference archive is temporarily unavailable for platform deep testing.\n" +
                "Product surfaces remain online. Contact the platform operator to restore archive access.\n");
            return;
        }

        // Interim /en/* commerce URLs while paused: map into the ASP.NET apps
        // (search/warehouse/catalog/cart) instead of the PHP warm-up splash;
        // anything unmapped goes home rather than dead-ending.
        if (path.StartsWith("/en/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/en", StringComparison.OrdinalIgnoreCase))
        {
            var pathAndQuery = path + context.Request.QueryString.Value;
            var target = PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(pathAndQuery, out var mapped)
                         && !string.Equals(mapped, pathAndQuery, StringComparison.OrdinalIgnoreCase)
                ? mapped
                : "/";
            context.Response.Redirect(target);
            return;
        }

        await _next(context);
    }
}
