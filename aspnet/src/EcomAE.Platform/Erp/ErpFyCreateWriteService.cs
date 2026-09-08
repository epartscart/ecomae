using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fy_create_year</c> / ajax <c>fy_create</c> twin. UPSERT
/// <c>epc_fy_years</c> on unique <c>label</c> and optional monthly
/// <c>epc_fy_periods</c>. Does not CREATE tables. Reopen and period status
/// stay ASP.NET-live. Year-end close and schema ensure stay PHP.
/// </summary>
public interface IErpFyCreateWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpFyCreateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFyCreateWriteRequest(
    string? Label = null,
    long StartDate = 0,
    long EndDate = 0,
    bool Monthly = false);

public sealed class ErpFyCreateWriteService : IErpFyCreateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFyCreateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpFyCreateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.StartDate <= 0 || request.EndDate <= 0 || request.EndDate < request.StartDate)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Valid start and end dates are required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var label = Clip((request.Label ?? string.Empty).Trim(), 40);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_fy_years", "label", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_fy_years", "start_date", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_fy_years", "end_date", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Fiscal year table is not provisioned");
        }

        if (request.Monthly
            && (!await ColumnExistsAsync(connection, "epc_fy_periods", "year_id", cancellationToken).ConfigureAwait(false)
                || !await ColumnExistsAsync(connection, "epc_fy_periods", "period_no", cancellationToken).ConfigureAwait(false)))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Fiscal year table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_fy_years` (`label`,`start_date`,`end_date`,`status`,`time_created`) VALUES (?,?,?,'open',?) ON DUPLICATE KEY UPDATE `start_date` = VALUES(`start_date`), `end_date` = VALUES(`end_date`)"),
            cancellationToken,
            label, request.StartDate, request.EndDate, now).ConfigureAwait(false);
        var yearId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (yearId <= 0)
        {
            yearId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_fy_years` WHERE `label`=? LIMIT 1"),
                cancellationToken,
                label).ConfigureAwait(false);
        }

        if (request.Monthly && yearId > 0)
        {
            var cursor = DateTimeOffset.FromUnixTimeSeconds(request.StartDate);
            var p = 1;
            while (cursor.ToUnixTimeSeconds() < request.EndDate && p <= 12)
            {
                var next = cursor.AddMonths(1);
                var periodEnd = next.ToUnixTimeSeconds() - 1;
                if (periodEnd > request.EndDate)
                {
                    periodEnd = request.EndDate;
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_fy_periods` (`year_id`,`period_no`,`start_date`,`end_date`,`status`) VALUES (?,?,?,?,'open') ON DUPLICATE KEY UPDATE `start_date` = VALUES(`start_date`), `end_date` = VALUES(`end_date`)"),
                    cancellationToken,
                    yearId, p, cursor.ToUnixTimeSeconds(), periodEnd).ConfigureAwait(false);
                cursor = next;
                p++;
            }
        }

        return ErpSimpleWriteResult.Ok("Fiscal year created", yearId);
    }

    public static bool TryParseUnix(string? text, long numeric, out long unix)
    {
        if (numeric > 0)
        {
            unix = numeric;
            return true;
        }

        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            unix = 0;
            return false;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asLong) && asLong > 0)
        {
            unix = asLong;
            return true;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            unix = dto.ToUnixTimeSeconds();
            return unix > 0;
        }

        unix = 0;
        return false;
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
