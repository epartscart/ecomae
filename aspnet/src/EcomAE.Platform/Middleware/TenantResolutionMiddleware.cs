using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

public sealed class TenantResolutionMiddleware
{
    public const string HttpContextItemKey = "EcomAE.Tenant";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver)
    {
        context.Items[HttpContextItemKey] = await resolver.ResolveAsync(context, context.RequestAborted);
        await _next(context);
    }
}
