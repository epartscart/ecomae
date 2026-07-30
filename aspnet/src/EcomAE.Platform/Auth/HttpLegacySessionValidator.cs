using EcomAE.Platform.Security;

namespace EcomAE.Platform.Auth;

public sealed class HttpLegacySessionValidator : ILegacySessionValidator
{
    public ValueTask<LegacySessionContext> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var adminSession = httpContext.Request.Cookies["admin_session"];
        var adminUser = ParseInt(httpContext.Request.Cookies["admin_u_id"]);
        if (!string.IsNullOrWhiteSpace(adminSession) && adminUser > 0)
        {
            return ValueTask.FromResult(new LegacySessionContext(
                LegacySessionKind.Admin,
                adminUser,
                adminSession,
                [
                    EcomAePermissions.SuperCpAccess,
                    EcomAePermissions.SuperErpAccess,
                    EcomAePermissions.SuperBosAccess,
                    EcomAePermissions.TenantCpAccess,
                    EcomAePermissions.TenantErpAccess,
                    EcomAePermissions.ApiAccess
                ]));
        }

        var customerSession = httpContext.Request.Cookies["session"];
        var customerUser = ParseInt(httpContext.Request.Cookies["u_id"]);
        if (!string.IsNullOrWhiteSpace(customerSession) && customerUser > 0)
        {
            return ValueTask.FromResult(new LegacySessionContext(
                LegacySessionKind.Customer,
                customerUser,
                customerSession,
                []));
        }

        var apiKey = httpContext.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return ValueTask.FromResult(new LegacySessionContext(
                LegacySessionKind.ApiKey,
                0,
                apiKey,
                [EcomAePermissions.ApiAccess]));
        }

        return ValueTask.FromResult(new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, []));
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }
}
