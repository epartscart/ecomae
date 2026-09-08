using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_edit_lock_release</c> / ajax <c>edit_lock_release</c> twin.
/// DELETE from <c>epc_erp_edit_locks</c> by entity + user (and token when non-empty).
/// Acquire, heartbeat, force-lock, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpEditLockReleaseWriteService
{
    Task<ErpSimpleWriteResult> ReleaseAsync(
        ErpEditLockReleaseWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpEditLockReleaseWriteRequest(
    string? EntityType = null,
    string? EntityId = null,
    string? LockToken = null,
    long AdminId = 0);

public sealed class ErpEditLockReleaseWriteService : IErpEditLockReleaseWriteService
{
    public const string ReleasedMessage = "Edit lock released";
    public const string TableMissing = "Edit locks table is not provisioned";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpEditLockReleaseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ReleaseAsync(
        ErpEditLockReleaseWriteRequest request,
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
        var userId = (int)request.AdminId;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_edit_locks", "lock_token", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_edit_locks", "entity_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", TableMissing);
        }

        if (lockToken != "")
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "DELETE FROM `epc_erp_edit_locks` WHERE `entity_type`=? AND `entity_id`=? AND `lock_token`=? AND `user_id`=?"),
                cancellationToken,
                entityType,
                entityId,
                lockToken,
                userId).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "DELETE FROM `epc_erp_edit_locks` WHERE `entity_type`=? AND `entity_id`=? AND `user_id`=?"),
                cancellationToken,
                entityType,
                entityId,
                userId).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(ReleasedMessage, 0);
    }

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
