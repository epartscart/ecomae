using EcomAE.Platform.Observability;
using EcomAE.Platform.Security;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Validates admin/customer cookies against PHP sessions when TenantRegistry DB is configured.
/// Admin backend access mirrors PHP <c>epc_auth_user_has_backend_access</c> (groups.for_backend).
/// Falls back to cookie-presence bridge when DB is unavailable (migration/diagnostics only).
/// </summary>
public sealed class DbBackedLegacySessionValidator : ILegacySessionValidator
{
    private static readonly string[] FullAdminPermissions =
    [
        EcomAePermissions.SuperCpAccess,
        EcomAePermissions.SuperErpAccess,
        EcomAePermissions.SuperBosAccess,
        EcomAePermissions.TenantCpAccess,
        EcomAePermissions.TenantErpAccess,
        EcomAePermissions.ApiAccess
    ];

    private readonly ILegacySessionStore _sessions;

    public DbBackedLegacySessionValidator(ILegacySessionStore sessions)
    {
        _sessions = sessions;
    }

    public async ValueTask<LegacySessionContext> ValidateCustomerAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        using var activity = EcomAeActivitySources.Auth.StartActivity("auth.legacy-session.validate-customer");
        var customer = await TryCustomerAsync(httpContext, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("ecomae.session.kind", customer.Kind.ToString().ToLowerInvariant());
        return customer;
    }

    public async ValueTask<LegacySessionContext> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        using var activity = EcomAeActivitySources.Auth.StartActivity("auth.legacy-session.validate");

        var adminSession = httpContext.Request.Cookies["admin_session"];
        var adminUser = ParseInt(httpContext.Request.Cookies["admin_u_id"]);
        if (!string.IsNullOrWhiteSpace(adminSession) && adminUser > 0)
        {
            if (_sessions.IsConfigured)
            {
                var exists = await _sessions.AdminSessionExistsAsync(adminSession, adminUser, cancellationToken).ConfigureAwait(false);
                if (exists)
                {
                    var identity = await _sessions.GetAdminIdentityAsync(adminUser, cancellationToken).ConfigureAwait(false);
                    if (identity is not null && identity.HasBackendAccess)
                    {
                        activity?.SetTag("ecomae.session.kind", "admin");
                        return new LegacySessionContext(
                            LegacySessionKind.Admin,
                            adminUser,
                            adminSession,
                            FullAdminPermissions,
                            identity.Email,
                            identity.GroupIds,
                            HasBackendAccess: true,
                            ModuleAcl: identity.Modules);
                    }
                }

                // Stale/invalid admin cookies must NOT wipe a valid customer storefront session —
                // that hid prices/terms on /en/parts/* after retail login (PHP fall-through parity).
            }
            else
            {
                activity?.SetTag("ecomae.session.kind", "admin");
                return new LegacySessionContext(
                    LegacySessionKind.Admin,
                    adminUser,
                    adminSession,
                    FullAdminPermissions,
                    HasBackendAccess: true);
            }
        }

        var customer = await TryCustomerAsync(httpContext, cancellationToken).ConfigureAwait(false);
        if (customer.Kind == LegacySessionKind.Customer)
        {
            activity?.SetTag("ecomae.session.kind", "customer");
            return customer;
        }

        var apiKey = httpContext.Request.Headers["X-API-Key"].FirstOrDefault()
            ?? LegacyApiClientKeyParser.ExtractFromAuthorizationHeader(httpContext.Request.Headers.Authorization.FirstOrDefault());
        var parsedApiKey = LegacyApiClientKeyParser.Parse(apiKey);
        if (parsedApiKey is not null)
        {
            activity?.SetTag("ecomae.session.kind", "api-key");
            return new LegacySessionContext(
                LegacySessionKind.ApiKey,
                0,
                parsedApiKey.Prefix,
                [EcomAePermissions.ApiAccess]);
        }

        activity?.SetTag("ecomae.session.kind", "anonymous");
        return Anonymous();
    }

    private async ValueTask<LegacySessionContext> TryCustomerAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var customerSession = httpContext.Request.Cookies["session"];
        var customerUser = ParseInt(httpContext.Request.Cookies["u_id"]);
        if (string.IsNullOrWhiteSpace(customerSession) || customerUser <= 0)
        {
            return Anonymous();
        }

        if (_sessions.IsConfigured)
        {
            var exists = await _sessions.CustomerSessionExistsAsync(customerSession, customerUser, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                return Anonymous();
            }
        }

        return new LegacySessionContext(
            LegacySessionKind.Customer,
            customerUser,
            customerSession,
            []);
    }

    private static LegacySessionContext Anonymous()
        => new(LegacySessionKind.Anonymous, 0, null, []);

    private static int ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : 0;
}
