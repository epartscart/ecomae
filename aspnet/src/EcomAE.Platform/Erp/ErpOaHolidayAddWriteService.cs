using System.Data.Common;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_oa_holiday_add</c> twin. INSERT/UPDATE <c>epc_oa_holiday</c>.
/// Party, address, contact, calendar, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpOaHolidayAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpOaHolidayAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOaHolidayAddWriteRequest(
    long CalendarId = 0,
    string? HolidayDate = null,
    string? Name = null);

public sealed class ErpOaHolidayAddWriteService : IErpOaHolidayAddWriteService
{
    private static readonly Regex Ymd = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpOaHolidayAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpOaHolidayAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var date = (request.HolidayDate ?? string.Empty).Trim();
        var invalid = Validate(date);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var name = Clip(request.Name ?? string.Empty, 160);
        var calendarId = request.CalendarId < 0 ? 0 : request.CalendarId;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_oa_holiday", "holiday_date", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_oa_holiday", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Calendar holiday table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_oa_holiday` (`calendar_id`,`holiday_date`,`name`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`)"),
            cancellationToken,
            calendarId,
            date,
            name).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT id FROM `epc_oa_holiday` WHERE calendar_id=? AND holiday_date=? LIMIT 1"),
            cancellationToken,
            calendarId,
            date).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Holiday added", id);
    }

    public static string? Validate(string holidayDate)
    {
        if (!Ymd.IsMatch(holidayDate))
        {
            return "Holiday date must be Y-m-d";
        }

        return null;
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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
