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
        context.Response.Headers["X-EcomAE-Target-Runtime"] = "aspnet-only-deep-test";
        context.Response.Headers["X-EcomAE-PHP-Fallback"] = "required-but-serving-deactivated";
        context.Response.Headers["X-EcomAE-Keep-Php-Project"] = _options.KeepPhpProjectAvailable ? "true" : "false";
        context.Response.Headers["X-EcomAE-Cutover-Allowed"] = "false";
        context.Response.Headers["X-EcomAE-Ready-For-Php-Removal"] = "false";

        var path = context.Request.Path.Value ?? "/";
        if (path.StartsWith("/php-reference", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/php-reference", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                "PHP reference serving is temporarily deactivated for ASP.NET Core deep testing.\n" +
                "PHP project files remain on disk (KeepPhpProjectAvailable=true).\n" +
                "cutoverAllowed=false · readyForPhpRemoval=false · RequirePhpFallback still required.\n" +
                "Restore: ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES bash scripts/cloudpanel_restore_php_reference_serving.sh\n");
            return;
        }

        await _next(context);
    }
}
