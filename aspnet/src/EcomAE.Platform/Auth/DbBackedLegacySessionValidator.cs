using EcomAE.Platform.Security;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Validates admin cookies against PHP <c>sessions</c> when TenantRegistry DB is configured.
/// Falls back to cookie-presence bridge when DB is unavailable (migration/diagnostics only).
/// </summary>
public sealed class DbBackedLegacySessionValidator : ILegacySessionValidator
{
    private readonly ILegacySessionStore _sessions;

    public DbBackedLegacySessionValidator(ILegacySessionStore sessions)
    {
        _sessions = sessions;
    }

    public async ValueTask<LegacySessionContext> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var adminSession = httpContext.Request.Cookies["admin_session"];
        var adminUser = ParseInt(httpContext.Request.Cookies["admin_u_id"]);
        if (!string.IsNullOrWhiteSpace(adminSession) && adminUser > 0)
        {
            if (_sessions.IsConfigured)
            {
                var exists = await _sessions.AdminSessionExistsAsync(adminSession, adminUser, cancellationToken).ConfigureAwait(false);
                if (!exists)
                {
                    return new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, []);
                }
            }

            return new LegacySessionContext(
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
                ]);
        }

        var customerSession = httpContext.Request.Cookies["session"];
        var customerUser = ParseInt(httpContext.Request.Cookies["u_id"]);
        if (!string.IsNullOrWhiteSpace(customerSession) && customerUser > 0)
        {
            if (_sessions.IsConfigured)
            {
                var exists = await _sessions.CustomerSessionExistsAsync(customerSession, customerUser, cancellationToken).ConfigureAwait(false);
                if (!exists)
                {
                    return new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, []);
                }
            }

            return new LegacySessionContext(
                LegacySessionKind.Customer,
                customerUser,
                customerSession,
                []);
        }

        var apiKey = httpContext.Request.Headers["X-API-Key"].FirstOrDefault()
            ?? LegacyApiClientKeyParser.ExtractFromAuthorizationHeader(httpContext.Request.Headers.Authorization.FirstOrDefault());
        var parsedApiKey = LegacyApiClientKeyParser.Parse(apiKey);
        if (parsedApiKey is not null)
        {
            return new LegacySessionContext(
                LegacySessionKind.ApiKey,
                0,
                parsedApiKey.Prefix,
                [EcomAePermissions.ApiAccess]);
        }

        return new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, []);
    }

    private static int ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : 0;
}
