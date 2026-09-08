using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_fiscal_set_lock</c> / ajax <c>fiscal_set_lock</c> twin.
/// Deactivates open locks, then optionally INSERTs a cut-off on <c>epc_erp_fiscal_locks</c>.
/// Empty / omitted <c>lock_date</c> clears the lock. Audit and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpFiscalSetLockWriteService
{
    Task<ErpSimpleWriteResult> SetAsync(
        ErpFiscalSetLockWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFiscalSetLockWriteRequest(
    long LockDateUnix = 0,
    string? Note = null,
    long AdminId = 0);

public sealed class ErpFiscalSetLockWriteService : IErpFiscalSetLockWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFiscalSetLockWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetAsync(
        ErpFiscalSetLockWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_fiscal_locks", "lock_date", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Fiscal lock table is not provisioned");
        }

        var note = ClipNote(request.Note);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `epc_erp_fiscal_locks` SET `active` = 0 WHERE `active` = 1"),
            cancellationToken).ConfigureAwait(false);

        var id = 0L;
        if (request.LockDateUnix > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_fiscal_locks` (`lock_date`, `note`, `admin_id`, `active`, `time_created`) VALUES (?, ?, ?, 1, ?)"),
                cancellationToken,
                request.LockDateUnix,
                note,
                request.AdminId,
                now).ConfigureAwait(false);
            id = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(SuccessMessage(request.LockDateUnix), id);
    }

    public static string SuccessMessage(long lockDateUnix)
        => lockDateUnix > 0
            ? "Periods locked up to " + FormatYmd(lockDateUnix)
            : "Fiscal lock cleared";

    public static string FormatYmd(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ClipNote(string? note)
    {
        var trimmed = (note ?? "").Trim();
        return trimmed.Length <= 255 ? trimmed : trimmed[..255];
    }

    /// <summary>
    /// PHP <c>!empty(lock_date) ? strtotime(lock_date + ' 23:59:59') : 0</c>,
    /// with JSON <c>lockDateUnix</c> when the date string is omitted.
    /// </summary>
    public static long ResolveLockDateUnix(string? lockDate, long lockDateUnix)
    {
        var raw = lockDate ?? "";
        if (raw.Length > 0 && raw != "0")
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed == "0")
            {
                return 0;
            }

            if (DateTime.TryParseExact(
                    trimmed,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var day))
            {
                return new DateTimeOffset(day.Year, day.Month, day.Day, 23, 59, 59, TimeSpan.Zero)
                    .ToUnixTimeSeconds();
            }

            return 0;
        }

        return lockDateUnix > 0 ? lockDateUnix : 0;
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

    public static long JsonLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
        }

        return 0;
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
