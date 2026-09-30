using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Middleware;

public sealed class IndustrySpecificRouteGateMiddleware(
    RequestDelegate next,
    ISurfaceDashboardSummaryReporter dashboards)
{
    private readonly RequestDelegate _next = next;
    private readonly ISurfaceDashboardSummaryReporter _dashboards = dashboards;

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
        var company = await ResolveActiveCompanyAsync(context);
        var allowed = IndustrySpecificRoutePolicy.Allows(requiredIndustry, hostIndustry, company);
        if (allowed)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("The requested industry module is not enabled for this tenant.");
    }

    private async Task<ErpCompanyDigest?> ResolveActiveCompanyAsync(HttpContext context)
    {
        var requested = ErpHostContext.ActiveCompanyIdFromQuery(context.Request);
        if (requested is not > 0)
        {
            return null;
        }

        try
        {
            return await _dashboards.BuildErpCompanyDigestAsync(requested.Value, context.RequestAborted);
        }
        catch
        {
            return null;
        }
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
        => Allows(requiredIndustry, hostIndustry, null);

    public static bool Allows(
        string requiredIndustry,
        string? hostIndustry,
        ErpCompanyDigest? company)
    {
        if (requiredIndustry.Equals("jewellery", StringComparison.OrdinalIgnoreCase))
        {
            return hostIndustry is "jewellery" or "jewelry"
                || ErpIndustryNav.IsJewelleryCompany(company);
        }

        if (requiredIndustry.Equals("fitout", StringComparison.OrdinalIgnoreCase))
        {
            return hostIndustry is "fitout"
                or "fit_out"
                or "construction"
                or "construction_contracting"
                || ErpIndustryNav.IsFitOutCompany(company);
        }

        return false;
    }
}
