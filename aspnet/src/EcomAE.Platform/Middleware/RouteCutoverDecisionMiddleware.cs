using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

public sealed class RouteCutoverDecisionMiddleware
{
    /// <summary>Opaque platform header — do not put stack names (tenants can see response headers).</summary>
    public const string TargetRuntimeHeader = "X-EcomAE-Platform";
    /// <summary>Opaque compat flag — replaces former PHP-named header.</summary>
    public const string PhpFallbackHeader = "X-EcomAE-Compat";

    private readonly RequestDelegate _next;

    public RouteCutoverDecisionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context, IMigrationRouteCutoverPolicy policy)
    {
        // Migration/ops boards may still inspect cutover JSON; product HTML must not advertise stack.
        var path = context.Request.Path.Value ?? "/";
        var isOpsBoard = path.StartsWith("/migration/", StringComparison.OrdinalIgnoreCase);

        if (context.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant)
        {
            var decision = policy.Decide(tenant);
            context.Response.OnStarting(() =>
            {
                if (isOpsBoard)
                {
                    // Ops-only: keep detailed values on /migration/*
                    context.Response.Headers[TargetRuntimeHeader] = decision.TargetRuntime;
                    context.Response.Headers[PhpFallbackHeader] = decision.RequiresPhpFallback ? "required" : "disabled";
                }
                else
                {
                    context.Response.Headers[TargetRuntimeHeader] = "primary";
                    context.Response.Headers[PhpFallbackHeader] = decision.RequiresPhpFallback ? "on" : "off";
                }
                return Task.CompletedTask;
            });
        }

        return _next(context);
    }
}
