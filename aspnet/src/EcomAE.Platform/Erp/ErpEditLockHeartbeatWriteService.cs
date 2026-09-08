using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_edit_lock_heartbeat</c> / ajax <c>edit_lock_heartbeat</c> twin.
/// UPDATE <c>heartbeat_at</c> / <c>expires_at</c> on the caller's lock row.
/// Acquire, force-lock, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpEditLockHeartbeatWriteService
{
    Task<ErpSimpleWriteResult> HeartbeatAsync(
        ErpEditLockHeartbeatWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpEditLockHeartbeatWriteRequest(
    string? EntityType = null,
    string? EntityId = null,
    string? LockToken = null,
    int TtlSeconds = 120,
    long AdminId = 0);

public sealed class ErpEditLockHeartbeatWriteService : IErpEditLockHeartbeatWriteService
{
    public const string RefreshedMessage = "Lock refreshed";
    public const string LostMessage = "Edit lock lost or expired — reload before saving";
    public const string TableMissing = "Edit locks table is not provisioned";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpEditLockHeartbeatWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> HeartbeatAsync(
        ErpEditLockHeartbeatWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var entityType = request.EntityType ?? "";
        var entityId = request.EntityId ?? "";
        var lockToken = request.LockToken ?? "";
        var ttl = ClampTtl(request.TtlSeconds);
        var userId = (int)request.AdminId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expires = now + ttl;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_edit_locks", "heartbeat_at", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_edit_locks", "lock_token", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", TableMissing);
        }

        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_edit_locks` SET `heartbeat_at`=?, `expires_at`=? WHERE `entity_type`=? AND `entity_id`=? AND `lock_token`=? AND `user_id`=?"),
            cancellationToken,
            now,
            expires,
            entityType,
            entityId,
            lockToken,
            userId).ConfigureAwait(false);
        if (rows < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", LostMessage);
        }

        return ErpSimpleWriteResult.Ok(RefreshedMessage, expires);
    }

    /// <summary>PHP <c>max(30, min(600, $ttlSeconds))</c>.</summary>
    public static int ClampTtl(int ttlSeconds) => Math.Max(30, Math.Min(600, ttlSeconds));

    /// <summary>PHP ajax casts POST fields with <c>(string)</c> and does not trim.</summary>
    public static string FormRaw(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            if (form.ContainsKey(name))
            {
                return form[name].ToString();
            }
        }

        return "";
    }

    public static int FormInt(IFormCollection form, int fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!form.ContainsKey(name))
            {
                continue;
            }

            if (int.TryParse(form[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                return n;
            }
        }

        return fallback;
    }

    public static string JsonText(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
    }

    public static int JsonInt(JsonElement root, int fallback, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return fallback;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
