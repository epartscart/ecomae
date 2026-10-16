using EcomAE.Platform.Erp;
using EcomAE.Platform.Observability;
using EcomAE.Platform.Security;
using Microsoft.Extensions.DependencyInjection;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Validates admin/customer cookies against PHP sessions when TenantRegistry DB is configured.
/// Admin backend access mirrors PHP <c>epc_auth_user_has_backend_access</c> (groups.for_backend).
/// Falls back to cookie-presence bridge when DB is unavailable (migration/diagnostics only).
/// </summary>
public sealed class DbBackedLegacySessionValidator : ILegacySessionValidator
{
    private readonly ILegacySessionStore _sessions;
    private readonly IServiceScopeFactory? _scopes;

    public DbBackedLegacySessionValidator(ILegacySessionStore sessions, IServiceScopeFactory? scopes = null)
    {
        _sessions = sessions;
        _scopes = scopes;
    }

    public async ValueTask<LegacySessionContext> ValidateCustomerAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        using var activity = EcomAeActivitySources.Auth.StartActivity("auth.legacy-session.validate-customer");
        try
        {
            var customer = await TryCustomerAsync(httpContext, cancellationToken).ConfigureAwait(false);
            activity?.SetTag("ecomae.session.kind", customer.Kind.ToString().ToLowerInvariant());
            return customer;
        }
        catch
        {
            activity?.SetTag("ecomae.session.kind", "anonymous");
            return Anonymous();
        }
    }

    public async ValueTask<LegacySessionContext> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        using var activity = EcomAeActivitySources.Auth.StartActivity("auth.legacy-session.validate");
        const string requestCacheKey = "ecomae.legacy-session.validated";
        if (httpContext.Items.TryGetValue(requestCacheKey, out var boxed)
            && boxed is LegacySessionContext cached)
        {
            activity?.SetTag("ecomae.session.kind", cached.Kind.ToString().ToLowerInvariant());
            activity?.SetTag("ecomae.session.request_cache", "hit");
            return cached;
        }

        LegacySessionContext resolved;
        try
        {
            resolved = await ValidateCoreAsync(httpContext, activity, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Timeout / DB fault must paint the storefront as guest, not 500 the page.
            resolved = new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, []);
        }

        httpContext.Items[requestCacheKey] = resolved;
        return resolved;
    }

    private async ValueTask<LegacySessionContext> ValidateCoreAsync(
        HttpContext httpContext,
        System.Diagnostics.Activity? activity,
        CancellationToken cancellationToken)
    {
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
                        var areaGrants = await TryLoadAreaGrantsAsync(adminUser, cancellationToken).ConfigureAwait(false);
                        return new LegacySessionContext(
                            LegacySessionKind.Admin,
                            adminUser,
                            adminSession,
                            LegacyAdminPermissionSets.ForRequest(httpContext),
                            identity.Email,
                            identity.GroupIds,
                            HasBackendAccess: true,
                            ModuleAcl: identity.Modules,
                            AreaGrants: areaGrants);
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
                    LegacyAdminPermissionSets.ForRequest(httpContext),
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

    private async Task<IReadOnlyList<ErpSessionAreaGrant>?> TryLoadAreaGrantsAsync(int userId, CancellationToken cancellationToken)
    {
        if (_scopes is null || userId <= 0)
        {
            return null;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await using var scope = _scopes.CreateAsyncScope();
            var reader = scope.ServiceProvider.GetService<IErpPermissionScopeReadService>();
            if (reader is null)
            {
                return null;
            }

            var listed = await reader.ListForUserAsync(userId, timeout.Token).ConfigureAwait(false);
            var grants = new List<ErpSessionAreaGrant>();
            foreach (var grant in listed.Grants)
            {
                grants.Add(ToAreaGrant(grant.GroupKey, grant.Actions));
            }

            foreach (var delegation in listed.Delegations)
            {
                grants.Add(ToAreaGrant(delegation.GroupKey, delegation.Actions));
            }

            return grants;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // A missing grant table or a slow read must not sign the operator out.
            return null;
        }
    }

    private static ErpSessionAreaGrant ToAreaGrant(string groupKey, IReadOnlySet<string>? actions)
    {
        var all = actions is null || actions.Count == 0;
        return new ErpSessionAreaGrant(groupKey ?? string.Empty, actions ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), all);
    }

    private static LegacySessionContext Anonymous()
        => new(LegacySessionKind.Anonymous, 0, null, []);

    private static int ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : 0;
}
