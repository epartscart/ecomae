using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_period_soft_close</c> / ajax <c>period_soft_close</c> twin.
/// Auto-creates the month row when missing, then UPDATE <c>epc_erp_periods</c> and
/// INSERT <c>epc_erp_period_close_log</c>. Schema CREATE, period lock, and reopen stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPeriodSoftCloseWriteService
{
    Task<ErpSimpleWriteResult> SoftCloseAsync(
        ErpPeriodSoftCloseWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPeriodSoftCloseWriteRequest(
    string? YearMonth = null,
    string? Note = null,
    long AdminId = 0);

public sealed class ErpPeriodSoftCloseWriteService : IErpPeriodSoftCloseWriteService
{
    public const string AlreadyLocked = "Period is already locked. Reopen first to modify.";
    public const string AlreadySoftClose = "Period is already in soft-close state.";

    private static readonly Regex PhpYearMonth = new(@"^\d{4}-\d{2}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPeriodSoftCloseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SoftCloseAsync(
        ErpPeriodSoftCloseWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var yearMonth = request.YearMonth ?? "";
        var note = request.Note ?? "";
        if (!IsPhpYearMonth(yearMonth))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid year_month format: " + yearMonth);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_periods", "year_month", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_period_close_log", "action", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Period close tables are not provisioned");
        }

        var year = int.Parse(yearMonth[..4], CultureInfo.InvariantCulture);
        var month = int.Parse(yearMonth[5..], CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var status = await ErpDb.StringAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `status` FROM `epc_erp_periods` WHERE `year_month` = ? LIMIT 1"),
            cancellationToken,
            yearMonth).ConfigureAwait(false);
        if (status is null)
        {
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_periods` (`year_month`, `year`, `month`, `status`, `created_at`, `updated_at`) VALUES (?, ?, ?, 'open', ?, ?) ON DUPLICATE KEY UPDATE `year_month` = `year_month`"),
                cancellationToken,
                yearMonth,
                year,
                month,
                now,
                now).ConfigureAwait(false);
            status = await ErpDb.StringAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `status` FROM `epc_erp_periods` WHERE `year_month` = ? LIMIT 1"),
                cancellationToken,
                yearMonth).ConfigureAwait(false) ?? "open";
        }

        if (status == "locked")
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", AlreadyLocked);
        }

        if (status == "soft_close")
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", AlreadySoftClose);
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "UPDATE `epc_erp_periods` SET `status` = 'soft_close', `closed_by` = ?, `closed_at` = ?, `note` = ?, `updated_at` = ? WHERE `year_month` = ?"),
            cancellationToken,
            request.AdminId,
            now,
            note,
            now,
            yearMonth).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_period_close_log` (`year_month`, `action`, `old_status`, `new_status`, `admin_id`, `note`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            yearMonth,
            "soft_close",
            status,
            "soft_close",
            request.AdminId,
            note,
            now).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Period soft-closed: " + yearMonth, 0);
    }

    /// <summary>PHP <c>preg_match('/^\d{4}-\d{2}$/', $yearMonth)</c>. Does not require month 1–12.</summary>
    public static bool IsPhpYearMonth(string? yearMonth)
        => yearMonth is not null && PhpYearMonth.IsMatch(yearMonth);

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
