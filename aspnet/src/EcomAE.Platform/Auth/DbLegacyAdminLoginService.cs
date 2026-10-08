using System.Data.Common;
using System.Globalization;
using System.Net;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Creates PHP-compatible <c>sessions</c> rows and returns cookie material.
/// Opt-in: requires <see cref="EcomAeOptions.SecretSuccession"/> + DB. Admin surfaces apply the PHP login rate limit, and a legacy
/// md5 hash is upgraded to bcrypt after a successful password check, as the PHP authentication plugins do.
/// </summary>
public sealed class DbLegacyAdminLoginService : ILegacyAdminLoginService
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly ILegacySessionStore _sessions;
    private readonly EcomAeOptions _options;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<DbLegacyAdminLoginService> _log;

    public DbLegacyAdminLoginService(
        ITenantDbConnectionFactory connections,
        ILegacySessionStore sessions,
        IOptions<EcomAeOptions> options,
        IHttpContextAccessor http,
        ILogger<DbLegacyAdminLoginService> log)
    {
        _connections = connections;
        _sessions = sessions;
        _options = options.Value;
        _http = http;
        _log = log;
    }

    public bool IsConfigured
        => _connections.IsConfigured && !string.IsNullOrWhiteSpace(_options.SecretSuccession);

    public async Task<LegacyLoginOutcome> LoginAsync(
        LegacyLoginRequest request,
        string? remoteIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return LegacyLoginOutcome.Failed(
                "ASP.NET login bridge is not configured (need DB + EcomAE:SecretSuccession). Use PHP login.",
                "bridge_not_configured");
        }

        var contactRaw = (request.Contact ?? string.Empty).Trim();
        // PHP authentication plugin looks up with htmlentities($auth_contact).
        var contactLookup = WebUtility.HtmlEncode(contactRaw);
        var contactType = NormalizeContactType(request.ContactType);
        var password = request.Password ?? string.Empty;
        if (contactRaw.Length == 0 || password.Length == 0)
        {
            return LegacyLoginOutcome.Failed("Enter login and password.", "missing_fields");
        }

        var tenant = _http.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var host = _http.HttpContext?.Request.Host.Host ?? string.Empty;
        var superCpHost = PlatformHostPolicy.IsSuperCpHost(host);

        // Named tenants (storefront/CP/ERP) must use the shop DB — never silently fall through to
        // portal registry users (that yields false invalid_credentials). Super-CP hosts keep
        // registry fallback for platform operator accounts.
        var needsTenantShopDb = !superCpHost
            && request.Surface is (LegacyLoginSurface.ControlPanel
                or LegacyLoginSurface.Erp
                or LegacyLoginSurface.Storefront);
        if (needsTenantShopDb && (tenant is null || !tenant.HasTenantDatabase))
        {
            _log.LogWarning(
                "Tenant login blocked: shop db_name unbound host={Host} siteKey={SiteKey} surface={Surface}",
                host,
                tenant?.SiteKey,
                request.Surface);
            return LegacyLoginOutcome.Failed(
                "Shop database is not bound for this host. Operator must sync epc_portal_tenants.db_name (scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh) and republish.",
                "tenant_db_unbound");
        }

        var adminSession = request.Surface is not (LegacyLoginSurface.Storefront or LegacyLoginSurface.LifeOs);
        var clientIp = LegacyLoginSecurity.ClientIp(_http.HttpContext);
        try
        {
            await using var connection = await _connections
                .OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);

            async Task<LegacyLoginOutcome> Fail(string message, string code, int waitMinutes = 0)
            {
                if (adminSession)
                {
                    await LegacyLoginSecurity.RecordAsync(connection, clientIp, request.Contact ?? string.Empty, false, cancellationToken).ConfigureAwait(false);
                }

                return LegacyLoginOutcome.Failed(message, code, waitMinutes);
            }

            if (adminSession)
            {
                var limit = await LegacyLoginSecurity.CheckAsync(connection, clientIp, request.Contact ?? string.Empty, cancellationToken).ConfigureAwait(false);
                if (limit.Blocked)
                {
                    return await Fail(limit.Message, "rate_limited", limit.WaitMinutes).ConfigureAwait(false);
                }
            }

            _log.LogInformation(
                "Login attempt host={Host} siteKey={SiteKey} db={Database} surface={Surface} contactType={ContactType}",
                host,
                tenant?.SiteKey,
                tenant?.DatabaseName ?? "(registry-default)",
                request.Surface,
                contactType);

            var user = await LoadUserAsync(connection, contactLookup, contactType, cancellationToken)
                .ConfigureAwait(false);
            // Retry raw contact if HtmlEncode form differed (rare for emails; keeps PHP parity).
            if (user is null && !string.Equals(contactLookup, contactRaw, StringComparison.Ordinal))
            {
                user = await LoadUserAsync(connection, contactRaw, contactType, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (user is null)
            {
                var probe = await ProbeUserAsync(connection, contactLookup, contactType, cancellationToken)
                    .ConfigureAwait(false);
                if (probe is null && !string.Equals(contactLookup, contactRaw, StringComparison.Ordinal))
                {
                    probe = await ProbeUserAsync(connection, contactRaw, contactType, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (probe is not null)
                {
                    if (!probe.Confirmed)
                    {
                        return await Fail(
                            contactType == "phone"
                                ? "Phone is not confirmed on this account."
                                : "Email is not confirmed on this account.",
                            "email_unconfirmed").ConfigureAwait(false);
                    }

                    if (!probe.Unlocked)
                    {
                        return await Fail("This account is locked.", "account_locked").ConfigureAwait(false);
                    }
                }

                _log.LogWarning(
                    "Login user not found host={Host} db={Database} siteKey={SiteKey} (wrong tenant host? taxofinca email on epartscart?)",
                    host,
                    tenant?.DatabaseName,
                    tenant?.SiteKey);
                return await Fail("Incorrect login or password.", "invalid_credentials").ConfigureAwait(false);
            }

            if (!LegacyPasswordVerifier.Verify(password, user.PasswordHash, _options.SecretSuccession))
            {
                _log.LogWarning(
                    "Login password mismatch host={Host} db={Database} userId={UserId} hashKind={HashKind}",
                    host,
                    tenant?.DatabaseName,
                    user.UserId,
                    LegacyPasswordVerifier.IsLegacyMd5(user.PasswordHash) ? "md5" : "modern");
                return await Fail("Incorrect login or password.", "invalid_credentials").ConfigureAwait(false);
            }

            try
            {
                await LegacyLoginSecurity.UpgradePasswordIfNeededAsync(connection, user.UserId, password, user.PasswordHash, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException ex)
            {
                _log.LogWarning(ex, "Password hash upgrade failed userId={UserId}", user.UserId);
            }

            // LifeOS personal join uses customer cookies (any signed-in account).
            // Storefront stays customer; CP/ERP/BOS/IP stay admin-gated.
            if (adminSession)
            {
                var identity = await _sessions.GetAdminIdentityAsync(user.UserId, cancellationToken).ConfigureAwait(false);
                if (identity is null || !identity.HasBackendAccess)
                {
                    return await Fail("Account lacks backend permissions.", "no_backend_access").ConfigureAwait(false);
                }
            }

            var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Admin PHP: md5($auth_contact.$time.$secret). Customer PHP: md5($auth_contact.$user_id.$time.$secret).
            // Always use the posted contact (not htmlentities) for the token — matches PHP plugins.
            var sessionToken = adminSession
                ? LegacySessionTokenFactory.AdminSessionToken(contactRaw, time, _options.SecretSuccession)
                : LegacySessionTokenFactory.CustomerSessionToken(contactRaw, user.UserId, time, _options.SecretSuccession);
            var csrf = LegacySessionTokenFactory.CsrfGuardKey(
                _options.SecretSuccession, sessionToken, remoteIp, userAgent);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = adminSession
                    ? LegacyAdminLoginSql.InsertAdminSession
                    : LegacyAdminLoginSql.InsertCustomerSession;
                AddParameter(command, "@session", sessionToken);
                AddParameter(command, "@userId", user.UserId);
                AddParameter(command, "@time", time);
                AddParameter(command, "@csrf", csrf);
                if (adminSession)
                {
                    AddParameter(command, "@contactType", contactType);
                }

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (adminSession)
            {
                await LegacyLoginSecurity.ClearAsync(connection, clientIp, request.Contact ?? string.Empty, cancellationToken).ConfigureAwait(false);
                await LegacyLoginSecurity.RecordAsync(connection, clientIp, request.Contact ?? string.Empty, true, cancellationToken).ConfigureAwait(false);
            }

            return LegacyLoginOutcome.Succeeded(new LegacyLoginSuccess(
                user.UserId,
                user.Email,
                sessionToken,
                csrf,
                adminSession,
                RedirectFor(request.Surface)));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Login backend error host={Host} db={Database}", host, tenant?.DatabaseName);
            return LegacyLoginOutcome.Failed(
                "Login backend error. Check TenantRegistry DB connection and sessions table.",
                "login_backend_error");
        }
    }

    private static string RedirectFor(LegacyLoginSurface surface) => surface switch
    {
        LegacyLoginSurface.Erp => "/erp/app",
        LegacyLoginSurface.Bos => "/bos/app",
        LegacyLoginSurface.Ip => "/ip/app",
        LegacyLoginSurface.LifeOs => "/lifeos",
        LegacyLoginSurface.Storefront => "/storefront/app",
        _ => "/cp/app"
    };

    private static string NormalizeContactType(string? contactType)
        => string.Equals(contactType, "phone", StringComparison.OrdinalIgnoreCase) ? "phone" : "email";

    private static async Task<UserRow?> LoadUserAsync(
        DbConnection connection,
        string contact,
        string contactType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = contactType == "phone"
            ? LegacyAdminLoginSql.SelectUserByPhone
            : LegacyAdminLoginSql.SelectUserByEmail;
        AddParameter(command, "@contact", contact);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new UserRow(
            Convert.ToInt32(reader["user_id"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["password"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["email"], CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static async Task<UserProbe?> ProbeUserAsync(
        DbConnection connection,
        string contact,
        string contactType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = contactType == "phone"
            ? LegacyAdminLoginSql.SelectUserProbeByPhone
            : LegacyAdminLoginSql.SelectUserProbeByEmail;
        AddParameter(command, "@contact", contact);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var confirmedCol = contactType == "phone" ? "phone_confirmed" : "email_confirmed";
        var confirmed = Convert.ToInt32(reader[confirmedCol], CultureInfo.InvariantCulture) == 1;
        var unlocked = Convert.ToInt32(reader["unlocked"], CultureInfo.InvariantCulture) == 1;
        return new UserProbe(confirmed, unlocked);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record UserRow(int UserId, string PasswordHash, string Email);
    private sealed record UserProbe(bool Confirmed, bool Unlocked);
}

/// <summary>No-op login when DB/secret missing — forces PHP login fallback.</summary>
public sealed class UnconfiguredLegacyAdminLoginService : ILegacyAdminLoginService
{
    public bool IsConfigured => false;

    public Task<LegacyLoginOutcome> LoginAsync(
        LegacyLoginRequest request,
        string? remoteIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
        => Task.FromResult(LegacyLoginOutcome.Failed(
            "ASP.NET login bridge is not configured. Use the live PHP login page.",
            "bridge_not_configured"));
}
