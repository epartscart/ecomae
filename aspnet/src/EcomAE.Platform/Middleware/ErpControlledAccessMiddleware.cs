using EcomAE.Platform.Auth;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Stops a user who has posted area grants from opening or posting a module they were not given.
/// Pages that are not tied to a capability area stay available so the dashboard and guide still open.
/// </summary>
public sealed class ErpControlledAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILegacySessionValidator sessions)
    {
        var path = ErpControlledAccess.Normalize(context.Request.Path.Value);
        if (ErpControlledAccess.AreaForPath(path) is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var session = await sessions.ValidateAsync(context, context.RequestAborted).ConfigureAwait(false);
        if (ErpControlledAccess.Allows(session, path, context.Request.Method))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var area = ErpControlledAccess.AreaForPath(path) ?? "area";
        var label = ErpCapabilityCatalog.Groups.FirstOrDefault(group =>
            string.Equals(group.Key, area, StringComparison.OrdinalIgnoreCase))?.Label ?? area;
        var message = label + " is not granted for this user.";
        if (LiveWriteFormBinder.WantsHtml(context))
        {
            var company = context.Request.Query["company"].ToString();
            var dest = "/erp?access_denied=" + Uri.EscapeDataString(label);
            if (long.TryParse(company, out var companyId) && companyId > 0)
            {
                dest += "&company=" + companyId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            context.Response.Redirect(ClientErpPath.PrefixIfBound(context, dest));
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            new { ok = false, code = "area_denied", message },
            context.RequestAborted).ConfigureAwait(false);
    }
}
