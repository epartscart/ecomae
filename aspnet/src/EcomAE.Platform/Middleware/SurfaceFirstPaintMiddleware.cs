using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Binds the 2s SSR first-paint budget before session gates and Blazor run.
/// Does not change routing or cutover flags.
/// </summary>
public sealed class SurfaceFirstPaintMiddleware
{
    private readonly RequestDelegate _next;

    public SurfaceFirstPaintMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        ErpFirstPaint.Observe(context);
        return _next(context);
    }
}
