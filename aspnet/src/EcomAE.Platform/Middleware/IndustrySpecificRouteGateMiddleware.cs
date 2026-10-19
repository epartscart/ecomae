using EcomAE.Platform.Auth;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

public sealed class IndustrySpecificRouteGateMiddleware(
    RequestDelegate next,
    ISurfaceDashboardSummaryReporter dashboards,
    ILegacySessionValidator validator)
{
    private readonly RequestDelegate _next = next;
    private readonly ISurfaceDashboardSummaryReporter _dashboards = dashboards;
    private readonly ILegacySessionValidator _validator = validator;

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
        var tenantIndustry = TenantContext.IndustryFrom(context);
        var company = await ResolveActiveCompanyAsync(context);
        var allowed = IndustrySpecificRoutePolicy.Allows(requiredIndustry, hostIndustry, company, tenantIndustry);
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
        var requested = await ResolveRequestedCompanyIdAsync(context);
        try
        {
            var session = await _validator.ValidateAsync(context, context.RequestAborted);
            if (!ErpJewelleryModuleChrome.HasJewelleryStaffAccess(session))
            {
                return null;
            }

            var host = context.Request.Host.Host;
            var companies = ErpIndustryNav.EnsureSwitchableCompanies(
                (await _dashboards.BuildErpCompaniesDigestAsync(50, context.RequestAborted)).Companies,
                PlatformHostPolicy.IsSuperCpHost(host),
                ErpHostContext.Resolve(host).BrandLabel);
            if (requested is > 0)
            {
                return companies.FirstOrDefault(c => c.Id == requested.Value)
                    ?? await _dashboards.BuildErpCompanyDigestAsync(requested.Value, context.RequestAborted);
            }

            // PHP epc_erp_active_company_id: no ?company= → lowest-id legal entity.
            return companies.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int?> ResolveRequestedCompanyIdAsync(HttpContext context)
    {
        var fromQuery = ErpHostContext.ActiveCompanyIdFromQuery(context.Request);
        if (fromQuery is > 0)
        {
            return fromQuery;
        }

        if (!context.Request.HasFormContentType)
        {
            return null;
        }

        context.Request.EnableBuffering();
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (context.Request.Body.CanSeek)
        {
            context.Request.Body.Position = 0;
        }

        foreach (var key in new[] { "company", "companyId", "company_id" })
        {
            if (int.TryParse(form[key].ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id)
                && id > 0)
            {
                return id;
            }
        }

        return null;
    }
}

public static class IndustrySpecificRoutePolicy
{
    public static string? RequiredIndustry(string path)
    {
        if (path.StartsWith("/cp/jewellery-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/jewellery-", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/jewellery/", StringComparison.OrdinalIgnoreCase))
        {
            return "jewellery";
        }

        if (path.Equals("/erp/project-accounting-app", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/fitout/", StringComparison.OrdinalIgnoreCase))
        {
            return "fitout";
        }

        if (path.Equals("/erp/real-estate-app", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/real-estate/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/erp/landlord-app", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/erp/landlord/", StringComparison.OrdinalIgnoreCase))
        {
            return "real_estate";
        }

        return null;
    }

    public static bool Allows(string requiredIndustry, string? hostIndustry)
        => Allows(requiredIndustry, hostIndustry, null);

    public static bool Allows(
        string requiredIndustry,
        string? hostIndustry,
        ErpCompanyDigest? company)
        => Allows(requiredIndustry, hostIndustry, company, tenantIndustry: null);

    /// <summary>
    /// Industry modules follow the bound tenant (hybrid host, ERP-only site key,
    /// or company pack). A jewellery update therefore reaches thejewellerytrend,
    /// jewellery.ecomae.com, Super CP company 2, and INDJ ERP-only.
    /// </summary>
    public static bool Allows(
        string requiredIndustry,
        string? hostIndustry,
        ErpCompanyDigest? company,
        string? tenantIndustry)
    {
        if (Matches(requiredIndustry, hostIndustry) || Matches(requiredIndustry, tenantIndustry))
        {
            return true;
        }

        if (requiredIndustry.Equals("jewellery", StringComparison.OrdinalIgnoreCase))
        {
            return ErpIndustryNav.IsJewelleryCompany(company);
        }

        if (requiredIndustry.Equals("fitout", StringComparison.OrdinalIgnoreCase))
        {
            return ErpIndustryNav.IsFitOutCompany(company);
        }

        if (requiredIndustry.Equals("real_estate", StringComparison.OrdinalIgnoreCase)
            || requiredIndustry.Equals("realestate", StringComparison.OrdinalIgnoreCase))
        {
            return ErpIndustryNav.IsRealEstateCompany(company);
        }

        return false;
    }

    public static bool Matches(string requiredIndustry, string? actual)
    {
        var required = (requiredIndustry ?? string.Empty).Trim().ToLowerInvariant();
        var value = (actual ?? string.Empty).Trim().ToLowerInvariant();
        if (required.Length == 0 || value.Length == 0)
        {
            return false;
        }

        if (required is "jewellery" or "jewelry")
        {
            return value is "jewellery" or "jewelry"
                || value.StartsWith("jewellery", StringComparison.Ordinal)
                || value.Contains("jewel", StringComparison.Ordinal);
        }

        if (required is "fitout" or "fit_out")
        {
            return value is "fitout" or "fit_out" or "construction" or "construction_contracting" or "building_materials"
                || value.Contains("fitout", StringComparison.Ordinal)
                || value.Contains("fit-out", StringComparison.Ordinal)
                || value.Contains("construction", StringComparison.Ordinal)
                || value.Contains("building_materials", StringComparison.Ordinal);
        }

        if (required is "real_estate" or "realestate")
        {
            return value is "real_estate" or "realestate" or "real-estate"
                || value.Contains("real_estate", StringComparison.Ordinal)
                || value.Contains("real-estate", StringComparison.Ordinal)
                || value.Contains("realestate", StringComparison.Ordinal);
        }

        return value.Equals(required, StringComparison.Ordinal)
            || value.StartsWith(required + "_", StringComparison.Ordinal)
            || value.StartsWith(required + "-", StringComparison.Ordinal);
    }
}
