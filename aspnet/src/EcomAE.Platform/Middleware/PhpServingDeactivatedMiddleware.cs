using EcomAE.Platform.Configuration;
using EcomAE.Platform.Presentation;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// When <see cref="PhpReferenceOptions.TemporarilyDeactivatePhpServing"/> is true,
/// blocks PHP reference URLs so ASP.NET can be tested without PHP hops.
/// PreferAspNetApps remaps interim /en|/ar commerce only — it must not 503 compare URLs.
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
        var deactivate = _options.TemporarilyDeactivatePhpServing;
        var preferAspNet = StorefrontSurfaceLinks.PreferAspNetApps;
        if (!deactivate && !preferAspNet)
        {
            await _next(context);
            return;
        }

        if (deactivate)
        {
            context.Response.Headers[FlagHeader] = FlagValue;
            context.Response.Headers["X-EcomAE-Platform"] = "primary";
            context.Response.Headers["X-EcomAE-Compat"] = "paused";
        }

        var path = context.Request.Path.Value ?? "/";
        if (deactivate
            && (path.StartsWith("/php-reference", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/php-reference", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                "Reference archive is temporarily unavailable for platform deep testing.\n" +
                "Product surfaces remain online. Contact the platform operator to restore archive access.\n");
            return;
        }

        // Interim /en/* and /ar/* commerce URLs: map into the ASP.NET apps
        // (search/warehouse/catalog/cart) instead of the PHP warm-up splash;
        // anything unmapped goes home rather than dead-ending.
        if (preferAspNet
            && (path.StartsWith("/en/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/en", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/ar/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/ar", StringComparison.OrdinalIgnoreCase)))
        {
            var pathAndQuery = path + context.Request.QueryString.Value;
            if (PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(pathAndQuery, out var mapped)
                && !string.Equals(mapped, pathAndQuery, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect(mapped);
                return;
            }

            await _next(context);
            return;
        }

        await _next(context);
    }
}
