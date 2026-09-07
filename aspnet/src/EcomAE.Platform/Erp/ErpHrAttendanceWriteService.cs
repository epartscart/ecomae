using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hr_attendance_log</c> twin. Upsert <c>epc_hr_attendance</c>
/// per employee+day. Schema ensure stays PHP. Does not CREATE tables.
/// </summary>
public interface IErpHrAttendanceWriteService
{
    Task<ErpSimpleWriteResult> LogAsync(
        ErpHrAttendanceWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrAttendanceWriteRequest(
    long EmployeeId = 0,
    string? WorkDate = null,
    string? WorkDateStr = null,
    decimal Hours = 0,
    string? Status = null);

public sealed class ErpHrAttendanceWriteService : IErpHrAttendanceWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrAttendanceWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> LogAsync(
        ErpHrAttendanceWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EmployeeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select an employee");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var day = NormalizeWorkDateUnix(request.WorkDate, request.WorkDateStr, now);
        var hours = decimal.Round(request.Hours, 2, MidpointRounding.AwayFromZero);
        var status = Clip((request.Status ?? string.Empty).Trim(), 12);
        if (status.Length == 0)
        {
            status = "present";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_hr_attendance", "employee_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hr_attendance", "work_date", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "HR attendance table is not provisioned");
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_hr_attendance` WHERE `employee_id`=? AND `work_date`=?"),
            cancellationToken,
            request.EmployeeId,
            day).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_hr_attendance` SET `hours`=?, `status`=? WHERE `id`=?"),
                cancellationToken,
                hours,
                status,
                existing).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Attendance recorded", existing);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_hr_attendance` (`employee_id`,`work_date`,`hours`,`status`) VALUES (?,?,?,?)"),
            cancellationToken,
            request.EmployeeId,
            day,
            hours,
            status).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Attendance recorded", id);
    }

    public static long NormalizeWorkDateUnix(string? workDate, string? workDateStr, long now)
    {
        var raw = 0L;
        var explicitUnix = (workDate ?? string.Empty).Trim();
        if (explicitUnix.Length > 0
            && long.TryParse(explicitUnix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
            && numeric > 0)
        {
            raw = numeric;
        }
        else
        {
            var fromStr = ErpHrLeaveRequestWriteService.ResolveDateUnix(workDateStr);
            raw = fromStr > 0 ? fromStr : now;
        }

        if (raw <= 0)
        {
            raw = now;
        }

        var utc = DateTimeOffset.FromUnixTimeSeconds(raw).UtcDateTime;
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
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
