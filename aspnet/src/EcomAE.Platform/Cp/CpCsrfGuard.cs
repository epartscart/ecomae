using System.Data.Common;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>content/users/stop_csrf.php</c> twin for admin CP forms: the request must carry
/// <c>csrf_guard_key</c> and it must equal the <c>sessions.csrf_guard_key</c> row of the
/// authenticated admin session. Native pages render the same key into their forms via
/// <see cref="KeyForAsync"/>.
/// </summary>
public interface ICpCsrfGuard
{
    Task<string> KeyForAsync(HttpContext context, LegacySessionContext session, CancellationToken cancellationToken = default);

    Task<CpCsrfVerdict> VerifyAsync(HttpContext context, LegacySessionContext session, string? submittedKey, CancellationToken cancellationToken = default);
}

public sealed record CpCsrfVerdict(bool Ok, string Code, string Message)
{
    public static readonly CpCsrfVerdict Pass = new(true, "ok", "OK");

    /// <summary>PHP <c>error_exit('Error! CSRF 1')</c>: key missing from the request.</summary>
    public static readonly CpCsrfVerdict Missing = new(false, "csrf_missing", "Error! CSRF 1");

    /// <summary>PHP <c>error_exit('Error! CSRF 3.1')</c>: no session row to compare against.</summary>
    public static readonly CpCsrfVerdict NoSession = new(false, "csrf_no_session", "Error! CSRF 3.1");

    /// <summary>PHP <c>error_exit('Error! CSRF 4')</c>: key does not match the session.</summary>
    public static readonly CpCsrfVerdict Mismatch = new(false, "csrf_mismatch", "Error! CSRF 4");
}

public sealed class CpCsrfGuard : ICpCsrfGuard
{
    public const string FieldName = "csrf_guard_key";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly string _secretSuccession;

    public CpCsrfGuard(IErpWriteConnectionFactory connections, IOptions<EcomAeOptions> options)
    {
        _connections = connections;
        _secretSuccession = options.Value.SecretSuccession ?? string.Empty;
    }

    public static bool Matches(string? stored, string? submitted)
        => !string.IsNullOrEmpty(stored)
           && !string.IsNullOrEmpty(submitted)
           && string.Equals(stored.Trim(), submitted.Trim(), StringComparison.Ordinal);

    public async Task<string> KeyForAsync(HttpContext context, LegacySessionContext session, CancellationToken cancellationToken = default)
    {
        if (!session.IsAuthenticated || string.IsNullOrWhiteSpace(session.SessionId))
        {
            return string.Empty;
        }

        var stored = await StoredKeyAsync(session, cancellationToken).ConfigureAwait(false);
        if (stored is not null)
        {
            return stored;
        }

        if (_connections.IsConfigured)
        {
            return string.Empty;
        }

        // Cookie-presence bridge (no tenant DB): the key is derivable from the session token exactly
        // as the login services mint it, so pages and the verifier agree without a sessions row.
        return LegacySessionTokenFactory.CsrfGuardKey(
            _secretSuccession,
            session.SessionId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());
    }

    public async Task<CpCsrfVerdict> VerifyAsync(HttpContext context, LegacySessionContext session, string? submittedKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(submittedKey))
        {
            return CpCsrfVerdict.Missing;
        }

        var expected = await KeyForAsync(context, session, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(expected))
        {
            return CpCsrfVerdict.NoSession;
        }

        return Matches(expected, submittedKey) ? CpCsrfVerdict.Pass : CpCsrfVerdict.Mismatch;
    }

    private async Task<string?> StoredKeyAsync(LegacySessionContext session, CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var sql = session.Kind == LegacySessionKind.Admin
                ? "SELECT IFNULL(`csrf_guard_key`,'') FROM `sessions` WHERE `session`=? AND `user_id`=? AND `type`=1 LIMIT 1"
                : "SELECT IFNULL(`csrf_guard_key`,'') FROM `sessions` WHERE `session`=? AND `user_id`=? LIMIT 1";
            var value = await ErpDb.StringAsync(
                connection, null, ErpDb.Positional(sql), cancellationToken, session.SessionId, session.UserId).ConfigureAwait(false);
            return value ?? string.Empty;
        }
        catch (DbException)
        {
            return string.Empty;
        }
    }
}
