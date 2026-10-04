using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twin of <c>epc_erp_presence_heartbeat</c> (ajax <c>presence_heartbeat</c>) and the
/// <c>concurrency_status</c> dispatcher case in <c>cp/content/shop/finance/erp/ajax_erp.php</c>.
/// Upserts the caller's presence row, purges expired rows best-effort and reads the active sample,
/// the (token-redacted) edit lock and the invoice <c>row_version</c>.
/// Never provisions schema: missing PHP-owned presence / lock schema fails closed.
/// </summary>
public interface IErpPresenceWriteService
{
    Task<ErpPresenceHeartbeatResult> HeartbeatAsync(ErpPresenceContext context, CancellationToken cancellationToken = default);

    Task<ErpConcurrencyStatusResult> ConcurrencyStatusAsync(ErpPresenceContext context, CancellationToken cancellationToken = default);
}

public sealed record ErpPresenceContext(
    int UserId,
    string Tab,
    string Area,
    string EntityType,
    string EntityId,
    string SessionCookie,
    string UserAgent,
    string ClientIp,
    bool HasBackendAccess);

public sealed record ErpPresenceHeartbeatResult(
    ErpSimpleWriteResult Result,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Sample,
    long Count,
    int SelfUserId,
    bool CanForceLock);

public sealed record ErpConcurrencyStatusResult(
    ErpSimpleWriteResult Result,
    ErpPresenceHeartbeatResult Presence,
    IReadOnlyDictionary<string, object?>? Lock,
    long RowVersion,
    long ServerTime,
    bool CanForceLock);

public sealed class ErpPresenceWriteService : IErpPresenceWriteService
{
    public const string TableMissing = "Concurrency tables are not provisioned";
    public const int ActiveWindowSeconds = 120;
    public const int SampleLimit = 12;
    private const int PurgeThrottleSeconds = 45;

    private static long _lastPurge;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpPresenceWriteService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>PHP <c>epc_erp_concurrency_session_token</c>: sha256 of the session cookie, first 40 hex chars.</summary>
    public static string SessionToken(string sessionCookie, string userAgent, string ip, int userId)
    {
        var source = sessionCookie.Length > 0 ? sessionCookie : userAgent + "|" + ip + "|" + userId.ToString(CultureInfo.InvariantCulture);
        var hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        return hex[..40];
    }

    /// <summary>PHP <c>epc_erp_audit_client_ip</c>: first valid IP from CF / XFF / X-Real-IP, else remote address.</summary>
    public static string ClientIp(HttpContext context)
    {
        foreach (var header in new[] { "CF-Connecting-IP", "X-Forwarded-For", "X-Real-IP" })
        {
            var raw = context.Request.Headers[header].ToString();
            if (raw.Length == 0) continue;
            var first = raw.Split(',')[0].Trim();
            if (IPAddress.TryParse(first, out _)) return first;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    /// <summary>PHP <c>ctype_digit</c>.</summary>
    public static bool IsDigits(string s) => s.Length > 0 && s.All(ch => ch is >= '0' and <= '9');

    public async Task<ErpPresenceHeartbeatResult> HeartbeatAsync(ErpPresenceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_connections.IsConfigured)
        {
            return Empty(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await HeartbeatCoreAsync(connection, context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpConcurrencyStatusResult> ConcurrencyStatusAsync(ErpPresenceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (!_connections.IsConfigured)
        {
            var fail = ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
            return new(fail, Empty(fail, 0), null, 0, now, false);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var presence = await HeartbeatCoreAsync(connection, context, cancellationToken).ConfigureAwait(false);
        if (!presence.Result.Succeeded)
        {
            return new(presence.Result, presence, null, 0, now, false);
        }

        IReadOnlyDictionary<string, object?>? lockRow = null;
        if (context.EntityType.Length > 0 && context.EntityId.Length > 0)
        {
            if (!await TableExistsAsync(connection, "epc_erp_edit_locks", cancellationToken).ConfigureAwait(false))
            {
                var fail = ErpSimpleWriteResult.Fail("invalid", TableMissing);
                return new(fail, presence, null, 0, now, false);
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `user_id`,`user_label`,`expires_at`,`heartbeat_at` FROM `epc_erp_edit_locks` WHERE `entity_type`=? AND `entity_id`=? AND `expires_at`>=? LIMIT 1");
            ErpDb.AddParameters(cmd, context.EntityType, context.EntityId, now);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lockRow = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["user_id"] = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ["user_label"] = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    ["expires_at"] = Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                    ["heartbeat_at"] = Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                };
            }
        }

        long version = 0;
        if (context.EntityType == "invoice" && IsDigits(context.EntityId) && long.TryParse(context.EntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var invoiceId))
        {
            version = await SafeLongAsync(connection, "SELECT `row_version` FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1", cancellationToken, invoiceId).ConfigureAwait(false);
        }

        return new(presence.Result, presence, lockRow, version, now, presence.CanForceLock);
    }

    private async Task<ErpPresenceHeartbeatResult> HeartbeatCoreAsync(DbConnection connection, ErpPresenceContext context, CancellationToken ct)
    {
        var userId = context.UserId;
        if (userId <= 0)
        {
            return Empty(new ErpSimpleWriteResult(false, "ok", "0 active", 0, 0), 0);
        }

        if (!await TableExistsAsync(connection, "epc_erp_presence", ct).ConfigureAwait(false))
        {
            return Empty(ErpSimpleWriteResult.Fail("invalid", TableMissing), userId);
        }

        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        var label = await UserLabelAsync(connection, userId, ct).ConfigureAwait(false);
        var token = SessionToken(context.SessionCookie, context.UserAgent, context.ClientIp, userId);
        var ip = context.ClientIp.Length > 45 ? context.ClientIp[..45] : context.ClientIp;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_presence` (`user_id`,`session_token`,`user_label`,`tab`,`area`,`entity_type`,`entity_id`,`last_seen`,`ip_address`) VALUES (?,?,?,?,?,?,?,?,?)"
                + " ON DUPLICATE KEY UPDATE `session_token`=VALUES(`session_token`), `user_label`=VALUES(`user_label`), `tab`=VALUES(`tab`), `area`=VALUES(`area`),"
                + " `entity_type`=VALUES(`entity_type`), `entity_id`=VALUES(`entity_id`), `last_seen`=VALUES(`last_seen`), `ip_address`=VALUES(`ip_address`)"),
            ct,
            userId,
            token,
            Cut(label, 128),
            Cut(context.Tab, 64),
            Cut(context.Area, 64),
            Cut(context.EntityType, 48),
            Cut(context.EntityId, 64),
            now,
            ip).ConfigureAwait(false);

        await PurgeAsync(connection, now, ct).ConfigureAwait(false);

        var cut = now - ActiveWindowSeconds;
        var count = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_presence` WHERE `last_seen` >= ?"), ct, cut).ConfigureAwait(false);
        var sample = new List<IReadOnlyDictionary<string, object?>>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT `user_id`,`user_label`,`tab`,`area`,`entity_type`,`entity_id`,`last_seen` FROM `epc_erp_presence` WHERE `last_seen` >= ? ORDER BY `last_seen` DESC LIMIT " + SampleLimit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(cmd, cut);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                sample.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["user_id"] = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ["user_label"] = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    ["tab"] = Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    ["area"] = Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                    ["entity_type"] = Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    ["entity_id"] = Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty,
                    ["last_seen"] = Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture),
                });
            }
        }

        var canForce = await CanForceLockAsync(connection, context, ct).ConfigureAwait(false);
        return new(ErpSimpleWriteResult.Ok(count.ToString(CultureInfo.InvariantCulture) + " active", userId), sample, count, userId, canForce);
    }

    /// <summary>PHP <c>epc_erp_concurrency_purge</c>: throttled, best-effort DELETEs.</summary>
    private static async Task PurgeAsync(DbConnection connection, long now, CancellationToken ct)
    {
        var last = Interlocked.Read(ref _lastPurge);
        if (now - last < PurgeThrottleSeconds) return;
        Interlocked.Exchange(ref _lastPurge, now);
        foreach (var (sql, arg) in new (string, long)[]
                 {
                     ("DELETE FROM `epc_erp_edit_locks` WHERE `expires_at` < ?", now),
                     ("DELETE FROM `epc_erp_presence` WHERE `last_seen` < ?", now - 180),
                     ("DELETE FROM `epc_erp_idempotency` WHERE `expires_at` < ?", now),
                 })
        {
            try
            {
                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(sql), ct, arg).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // PHP: best-effort
            }
        }
    }

    /// <summary>PHP <c>epc_erp_concurrency_user_label</c>: users.name, then email, else "User #id".</summary>
    private static async Task<string> UserLabelAsync(DbConnection connection, int userId, CancellationToken ct)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT `email`, `name` FROM `users` WHERE `user_id` = ? LIMIT 1");
            ErpDb.AddParameters(cmd, userId);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var name = (Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty).Trim();
                if (name.Length > 0) return name;
                var email = (Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty).Trim();
                if (email.Length > 0) return email;
            }
        }
        catch (DbException)
        {
            // PHP: fall through
        }

        return "User #" + userId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// PHP <c>epc_erp_concurrency_can_force_lock</c>: backend-tree membership (session) or an
    /// Administrator-named group binding.
    /// </summary>
    private static async Task<bool> CanForceLockAsync(DbConnection connection, ErpPresenceContext context, CancellationToken ct)
    {
        if (context.HasBackendAccess) return true;
        var n = await SafeLongAsync(
            connection,
            "SELECT COUNT(*) FROM `users_groups_bind` ugb INNER JOIN `groups` g ON g.`id` = ugb.`group_id` WHERE ugb.`user_id` = ? AND (g.`value` LIKE ? OR g.`value` LIKE ?)",
            ct,
            context.UserId,
            "%Администратор%",
            "%Administrator%").ConfigureAwait(false);
        return n > 0;
    }

    private static async Task<long> SafeLongAsync(DbConnection c, string sql, CancellationToken ct, params object?[] p)
    {
        try
        {
            return await ErpDb.LongAsync(c, null, ErpDb.Positional(sql), ct, p).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken ct)
        => await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"), ct, table).ConfigureAwait(false) > 0;

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;

    private static ErpPresenceHeartbeatResult Empty(ErpSimpleWriteResult result, int userId)
        => new(result, Array.Empty<IReadOnlyDictionary<string, object?>>(), 0, userId, false);
}
