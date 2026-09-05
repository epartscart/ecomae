using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hr_leave_request</c> twin. Schema ensure, employee save,
/// attendance, and expense save stay PHP.
/// </summary>
public interface IErpHrLeaveRequestWriteService
{
    Task<ErpSimpleWriteResult> RequestAsync(
        long employeeId,
        string? type,
        decimal days,
        string? dateFrom,
        string? dateTo,
        CancellationToken cancellationToken = default);
}

public sealed class ErpHrLeaveRequestWriteService : IErpHrLeaveRequestWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrLeaveRequestWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RequestAsync(
        long employeeId,
        string? type,
        decimal days,
        string? dateFrom,
        string? dateTo,
        CancellationToken cancellationToken = default)
    {
        if (employeeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select an employee");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (days < 0)
        {
            days = 0;
        }

        days = decimal.Round(days, 2, MidpointRounding.AwayFromZero);
        var leaveType = NormalizeType(type);
        var fromUnix = ResolveDateUnix(dateFrom);
        var toUnix = ResolveDateUnix(dateTo);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_hr_leave` (`employee_id`,`type`,`days`,`date_from`,`date_to`,`status`,`time_created`) VALUES (?,?,?,?,?,'pending',?)"),
            cancellationToken,
            employeeId, leaveType, days, fromUnix, toUnix, now);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Leave request submitted", inserted);
    }

    public static string NormalizeType(string? raw)
    {
        var type = (raw ?? string.Empty).Trim();
        if (type.Length == 0)
        {
            return "annual";
        }

        return type.Length <= 24 ? type : type[..24];
    }

    public static long ResolveDateUnix(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            return unix < 0 ? 0 : unix;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return 0;
    }
}
