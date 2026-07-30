using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

public sealed class RouteCutoverDecisionMiddleware
{
    public const string TargetRuntimeHeader = "X-EcomAE-Target-Runtime";
    public const string PhpFallbackHeader = "X-EcomAE-PHP-Fallback";

    private readonly RequestDelegate _next;

    public RouteCutoverDecisionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context, IMigrationRouteCutoverPolicy policy)
    {
        if (context.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant)
        {
            var decision = policy.Decide(tenant);
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[TargetRuntimeHeader] = decision.TargetRuntime;
                context.Response.Headers[PhpFallbackHeader] = decision.RequiresPhpFallback ? "required" : "disabled";
                return Task.CompletedTask;
            });
        }

        return _next(context);
    }
}
