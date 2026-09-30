using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

public sealed class IndustrySpecificRouteGateMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var route = context.Request.Path.Value ?? string.Empty;
        var requiredIndustry = IndustrySpecificRoutePolicy.RequiredIndustry(route);
        if (requiredIndustry is null)
        {
            await _next(context);
            return;
        }

        var hostIndustry = ErpHostContext.Resolve(context.Request.Host.Host).IndustryCode;
        var allowed = IndustrySpecificRoutePolicy.Allows(requiredIndustry, hostIndustry);
        if (allowed)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("The requested industry module is not enabled for this tenant.");
    }
}

public static class IndustrySpecificRoutePolicy
{
    public static string? RequiredIndustry(string path)
    {
        if (path.StartsWith("/cp/jewellery-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/jewellery-", StringComparison.OrdinalIgnoreCase))
        {
            return "jewellery";
        }

        if (path.Equals("/erp/project-accounting-app", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/erp/contracts-app", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/fitout/", StringComparison.OrdinalIgnoreCase))
        {
            return "fitout";
        }

        return null;
    }

    public static bool Allows(string requiredIndustry, string? hostIndustry)
    {
        if (requiredIndustry.Equals("jewellery", StringComparison.OrdinalIgnoreCase))
        {
            return hostIndustry is "jewellery" or "jewelry";
        }

        if (requiredIndustry.Equals("fitout", StringComparison.OrdinalIgnoreCase))
        {
            return hostIndustry is "fitout" or "fit_out" or "construction" or "construction_contracting";
        }

        return false;
    }
}
