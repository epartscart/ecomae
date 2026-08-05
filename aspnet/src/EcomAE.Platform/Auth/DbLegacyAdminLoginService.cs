using System.Data.Common;
using System.Globalization;
using System.Net;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Creates PHP-compatible <c>sessions</c> rows and returns cookie material.
/// Opt-in: requires <see cref="EcomAeOptions.SecretSuccession"/> + DB. Does not upgrade password hashes (PHP remains authoritative for upgrades).
/// </summary>
public sealed class DbLegacyAdminLoginService : ILegacyAdminLoginService
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly ILegacySessionStore _sessions;
    private readonly EcomAeOptions _options;

    public DbLegacyAdminLoginService(
        ITenantDbConnectionFactory connections,
        ILegacySessionStore sessions,
        IOptions<EcomAeOptions> options)
    {
        _connections = connections;
        _sessions = sessions;
        _options = options.Value;
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

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var user = await LoadUserAsync(connection, contactLookup, contactType, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                return LegacyLoginOutcome.Failed("Incorrect login or password.", "invalid_credentials");
            }

            if (!LegacyPasswordVerifier.Verify(password, user.PasswordHash, _options.SecretSuccession))
            {
                return LegacyLoginOutcome.Failed("Incorrect login or password.", "invalid_credentials");
            }

            var adminSession = request.Surface != LegacyLoginSurface.Storefront;
            if (adminSession)
            {
                var identity = await _sessions.GetAdminIdentityAsync(user.UserId, cancellationToken).ConfigureAwait(false);
                if (identity is null || !identity.HasBackendAccess)
                {
                    return LegacyLoginOutcome.Failed("Account lacks backend permissions.", "no_backend_access");
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

            return LegacyLoginOutcome.Succeeded(new LegacyLoginSuccess(
                user.UserId,
                user.Email,
                sessionToken,
                csrf,
                adminSession,
                RedirectFor(request.Surface)));
        }
        catch (Exception)
        {
            return LegacyLoginOutcome.Failed(
                "Login backend error. Check TenantRegistry DB connection and sessions table.",
                "login_backend_error");
        }
    }

    private static string RedirectFor(LegacyLoginSurface surface) => surface switch
    {
        LegacyLoginSurface.Erp => "/erp/app",
        LegacyLoginSurface.Bos => "/bos/app",
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record UserRow(int UserId, string PasswordHash, string Email);
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
