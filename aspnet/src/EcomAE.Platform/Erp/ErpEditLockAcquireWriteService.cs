using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

public interface IErpEditLockAcquireWriteService
{
    Task<ErpEditLockAcquireResult> AcquireAsync(ErpPresenceContext context, int ttlSeconds, bool force, CancellationToken cancellationToken = default);
}

public sealed record ErpEditLockAcquireResult(
    bool Ok,
    string Message,
    IReadOnlyDictionary<string, object?>? Lock,
    IReadOnlyDictionary<string, object?>? Conflict,
    string? ConflictCode,
    bool CanForce,
    int Writes);

/// <summary>
/// Live PHP <c>epc_erp_edit_lock_acquire</c> (ajax <c>edit_lock_acquire</c>) twin.
/// Provisions the PHP concurrency schema like the PHP ensure-schema step.
/// </summary>
public sealed class ErpEditLockAcquireWriteService : IErpEditLockAcquireWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;
    private readonly TimeProvider _clock;

    public ErpEditLockAcquireWriteService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit, TimeProvider? clock = null)
    {
        _connections = connections;
        _audit = audit;
        _clock = clock ?? TimeProvider.System;
    }
    /// <summary>PHP <c>epc_erp_edit_lock_acquire</c> twin.</summary>
    public async Task<ErpEditLockAcquireResult> AcquireAsync(ErpPresenceContext context, int ttlSeconds, bool force, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (!_connections.IsConfigured)
        {
            return new ErpEditLockAcquireResult(false, "TenantRegistry DB is not configured.", null, null, null, false, 0);
        }

        var entityType = Regex.Replace(context.EntityType, "[^a-z0-9_\\-]", string.Empty, RegexOptions.IgnoreCase);
        var entityId = ErpPresenceWriteService.Cut(Regex.Replace(context.EntityId, "[^\\w\\-.:]", string.Empty), 64);
        if (entityType.Length == 0 || entityId.Length == 0)
        {
            return new ErpEditLockAcquireResult(false, "Invalid entity for edit lock", null, null, null, false, 0);
        }

        var userId = context.UserId;
        if (userId <= 0)
        {
            return new ErpEditLockAcquireResult(false, "Sign in required for edit locks", null, null, null, false, 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureConcurrencySchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpPresenceWriteService.PurgeAsync(connection, now, cancellationToken).ConfigureAwait(false);

        var canForce = await ErpPresenceWriteService.CanForceLockAsync(connection, context, cancellationToken).ConfigureAwait(false);
        if (force && !canForce)
        {
            return new ErpEditLockAcquireResult(
                false,
                "Only an ERP administrator can force-take an edit lock held by another user",
                null,
                null,
                "force_denied",
                false,
                0);
        }

        var label = await ErpPresenceWriteService.UserLabelAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        var session = ErpPresenceWriteService.SessionToken(context.SessionCookie, context.UserAgent, context.ClientIp, userId);
        var expires = now + Math.Max(30, Math.Min(600, ttlSeconds));
        var token = LockToken(entityType, entityId, userId, session, now);

        LockRow? existing = await LoadLockAsync(connection, entityType, entityId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            var sameUser = existing.UserId == userId;
            var expired = existing.ExpiresAt < now;
            if (!sameUser && !expired && !force)
            {
                return ConflictResult(existing, null, canForce);
            }

            await UpdateLockAsync(connection, entityType, entityId, userId, label, session, token, now, expires, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT INTO `epc_erp_edit_locks` (`entity_type`,`entity_id`,`user_id`,`user_label`,`session_token`,`lock_token`,`acquired_at`,`heartbeat_at`,`expires_at`) VALUES (?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    entityType,
                    entityId,
                    userId,
                    ErpPresenceWriteService.Cut(label, 128),
                    ErpPresenceWriteService.Cut(session, 64),
                    token,
                    now,
                    now,
                    expires).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // PHP race path: another user inserted first — re-read and reconcile.
                existing = await LoadLockAsync(connection, entityType, entityId, cancellationToken).ConfigureAwait(false);
                if (existing is not null && existing.UserId != userId && existing.ExpiresAt >= now)
                {
                    return ConflictResult(existing, null, canForce);
                }

                await UpdateLockAsync(connection, entityType, entityId, userId, label, session, token, now, expires, cancellationToken).ConfigureAwait(false);
            }
        }

                    var entityNumeric = long.TryParse(entityId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0L;
        await _audit.LogAsync(
            connection,
            null,
            userId,
            "edit_lock_acquire",
            entityType,
            entityNumeric,
            "Edit lock acquired by " + label,
            new Dictionary<string, string?>
            {
                ["lock_token"] = token[..12],
                ["expires_at"] = expires.ToString(CultureInfo.InvariantCulture),
                ["force"] = force ? "1" : "0",
            },
            cancellationToken).ConfigureAwait(false);

        return new ErpEditLockAcquireResult(
            true,
            "Edit lock acquired",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["entity_type"] = entityType,
                ["entity_id"] = entityId,
                ["lock_token"] = token,
                ["expires_at"] = expires,
                ["user_id"] = (long)userId,
                ["user_label"] = label,
            },
            null,
            null,
            canForce,
            1);
    }

    private sealed record LockRow(long UserId, string UserLabel, long ExpiresAt, long HeartbeatAt);

    private static ErpEditLockAcquireResult ConflictResult(LockRow held, string? _, bool canForce)
        => new(
            false,
            "Record is being edited by " + (held.UserLabel.Length > 0 ? held.UserLabel : "user #" + held.UserId.ToString(CultureInfo.InvariantCulture)),
            null,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["user_id"] = held.UserId,
                ["user_label"] = held.UserLabel,
                ["expires_at"] = held.ExpiresAt,
                ["heartbeat_at"] = held.HeartbeatAt,
            },
            null,
            canForce,
            0);

    private static string LockToken(string entityType, string entityId, int userId, string session, long now)
    {
        var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var hex = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(
            entityType + "|" + entityId + "|" + userId.ToString(CultureInfo.InvariantCulture) + "|" + session + "|" + now.ToString(CultureInfo.InvariantCulture) + "|" + random)));
        return hex.ToLowerInvariant();
    }

    private static async Task<LockRow?> LoadLockAsync(DbConnection connection, string entityType, string entityId, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT `user_id`,`user_label`,`expires_at`,`heartbeat_at` FROM `epc_erp_edit_locks` WHERE `entity_type` = ? AND `entity_id` = ? LIMIT 1");
        ErpDb.AddParameters(cmd, entityType, entityId);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        return new LockRow(
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
            Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture));
    }

    private static async Task UpdateLockAsync(DbConnection connection, string entityType, string entityId, int userId, string label, string session, string token, long now, long expires, CancellationToken ct)
        => await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_edit_locks` SET `user_id`=?, `user_label`=?, `session_token`=?, `lock_token`=?, `acquired_at`=?, `heartbeat_at`=?, `expires_at`=? WHERE `entity_type`=? AND `entity_id`=?"),
            ct,
            userId,
            ErpPresenceWriteService.Cut(label, 128),
            ErpPresenceWriteService.Cut(session, 64),
            token,
            now,
            now,
            expires,
            entityType,
            entityId).ConfigureAwait(false);

    /// <summary>
    /// PHP <c>epc_erp_concurrency_ensure_schema</c>: creates the three PHP concurrency tables,
    /// fixes the idempotency PK and adds row_version on hot shared tables (best-effort).
    /// </summary>
    private static async Task EnsureConcurrencySchemaAsync(DbConnection connection, CancellationToken ct)
    {
        foreach (var ddl in new[]
                 {
                     "CREATE TABLE IF NOT EXISTS `epc_erp_edit_locks` (\n  `id` int(11) NOT NULL AUTO_INCREMENT,\n  `entity_type` varchar(48) NOT NULL,\n  `entity_id` varchar(64) NOT NULL,\n  `user_id` int(11) NOT NULL DEFAULT 0,\n  `user_label` varchar(128) NOT NULL DEFAULT '',\n  `session_token` varchar(64) NOT NULL DEFAULT '',\n  `lock_token` char(40) NOT NULL,\n  `acquired_at` int(11) NOT NULL DEFAULT 0,\n  `heartbeat_at` int(11) NOT NULL DEFAULT 0,\n  `expires_at` int(11) NOT NULL DEFAULT 0,\n  PRIMARY KEY (`id`),\n  UNIQUE KEY `x_entity` (`entity_type`, `entity_id`),\n  KEY `x_user` (`user_id`),\n  KEY `x_expires` (`expires_at`)\n) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP soft edit locks (multi-user)'",
                     "CREATE TABLE IF NOT EXISTS `epc_erp_presence` (\n  `user_id` int(11) NOT NULL,\n  `session_token` varchar(64) NOT NULL DEFAULT '',\n  `user_label` varchar(128) NOT NULL DEFAULT '',\n  `tab` varchar(64) NOT NULL DEFAULT '',\n  `area` varchar(64) NOT NULL DEFAULT '',\n  `entity_type` varchar(48) NOT NULL DEFAULT '',\n  `entity_id` varchar(64) NOT NULL DEFAULT '',\n  `last_seen` int(11) NOT NULL DEFAULT 0,\n  `ip_address` varchar(45) DEFAULT NULL,\n  PRIMARY KEY (`user_id`),\n  KEY `x_seen` (`last_seen`)\n) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP active user presence'",
                     "CREATE TABLE IF NOT EXISTS `epc_erp_idempotency` (\n  `idem_key` varchar(80) NOT NULL,\n  `user_id` int(11) NOT NULL DEFAULT 0,\n  `action` varchar(64) NOT NULL DEFAULT '',\n  `response_json` mediumtext,\n  `time_created` int(11) NOT NULL DEFAULT 0,\n  `expires_at` int(11) NOT NULL DEFAULT 0,\n  PRIMARY KEY (`idem_key`, `user_id`),\n  KEY `x_expires` (`expires_at`)\n) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP ajax idempotent replay cache'",
                 })
        {
            await ErpDb.TryExecuteAsync(connection, ddl, ct).ConfigureAwait(false);
        }

        try
        {
            var singlePk = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_erp_idempotency' AND CONSTRAINT_NAME = 'PRIMARY'"),
                ct).ConfigureAwait(false) == 1;
            var hasUserCol = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_erp_idempotency' AND CONSTRAINT_NAME = 'PRIMARY' AND COLUMN_NAME = 'user_id'"),
                ct).ConfigureAwait(false) == 1;
            if (singlePk && !hasUserCol)
            {
                await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_erp_idempotency` DROP PRIMARY KEY, ADD PRIMARY KEY (`idem_key`, `user_id`)", ct).ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
            // PHP: best-effort — table may already be correct
        }

        foreach (var table in new[]
                 {
                     "epc_einvoice_documents",
                     "epc_erp_purchases",
                     "epc_erp_purchase_orders",
                     "epc_document_company",
                     "epc_document_templates",
                     "epc_erp_suppliers",
                     "epc_erp_contacts",
                 })
        {
            if (!await ErpPresenceWriteService.TableExistsAsync(connection, table, ct).ConfigureAwait(false)) continue;
            var has = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = 'row_version'"),
                ct,
                table).ConfigureAwait(false) > 0;
            if (!has)
            {
                await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `" + table + "` ADD COLUMN `row_version` int(11) NOT NULL DEFAULT 1", ct).ConfigureAwait(false);
            }
        }
    }

}
