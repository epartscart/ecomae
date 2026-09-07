using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_plt_batch_job_save</c> / ajax <c>plt_job_save</c> twin.
/// INSERT/UPDATE <c>epc_plt_batch_job</c>. Job run, feature save, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPltJobSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPltJobSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPltJobSaveWriteRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    int RecurrenceMin = 0,
    int? Active = null);

public sealed class ErpPltJobSaveWriteService : IErpPltJobSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPltJobSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPltJobSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        var invalid = Validate(code);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        code = Clip(code, 60);
        var name = Clip(request.Name ?? string.Empty, 160);
        var rec = Math.Max(0, request.RecurrenceMin);
        var active = request.Active is > 0 ? 1 : 0;
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var next = NextRun(now, rec, active == 1);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_plt_batch_job", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_plt_batch_job", "recurrence_min", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Batch job table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_plt_batch_job` (`company_id`,`code`,`name`,`recurrence_min`,`status`,`next_run`,`active`,`time_updated`) VALUES (?,?,?,?, 'waiting', ?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `recurrence_min`=VALUES(`recurrence_min`), `active`=VALUES(`active`), `next_run`=VALUES(`next_run`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            companyId,
            code,
            name,
            rec,
            next,
            active,
            now).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT id FROM `epc_plt_batch_job` WHERE company_id=? AND code=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Batch job saved", id);
    }

    public static string? Validate(string code)
        => code.Length == 0 ? "Job code is required" : null;

    public static long NextRun(long fromUnix, int recurrenceMin, bool active)
    {
        if (!active || recurrenceMin <= 0)
        {
            return 0;
        }

        return fromUnix + (recurrenceMin * 60L);
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
