using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_oa_calendar_save</c> twin. INSERT/UPDATE <c>epc_oa_calendar</c>.
/// Party, address, contact, holiday, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpOaCalendarSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpOaCalendarSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOaCalendarSaveWriteRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    string? WorkingDays = null);

public sealed class ErpOaCalendarSaveWriteService : IErpOaCalendarSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpOaCalendarSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpOaCalendarSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        var workingDays = request.WorkingDays is null ? "1,2,3,4,5" : request.WorkingDays.Trim();
        var invalid = Validate(code, workingDays);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        code = Clip(code, 40);
        var name = Clip(request.Name ?? string.Empty, 160);
        workingDays = Clip(workingDays, 20);
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_oa_calendar", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_oa_calendar", "working_days", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Working calendar table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_oa_calendar` (`company_id`,`code`,`name`,`working_days`,`time_updated`) VALUES (?,?,?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `working_days`=VALUES(`working_days`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            companyId,
            code,
            name,
            workingDays,
            now).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT id FROM `epc_oa_calendar` WHERE company_id=? AND code=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Calendar saved", id);
    }

    public static string? Validate(string code, string workingDays)
    {
        if (code.Length == 0)
        {
            return "Calendar code is required";
        }

        if (WorkingSet(workingDays).Count == 0)
        {
            return "At least one working day is required";
        }

        return null;
    }

    public static HashSet<int> WorkingSet(string workingDays)
    {
        var set = new HashSet<int>();
        foreach (var part in workingDays.Split(','))
        {
            if (int.TryParse(part.Trim(), out var day) && day is >= 1 and <= 7)
            {
                set.Add(day);
            }
        }

        return set;
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
