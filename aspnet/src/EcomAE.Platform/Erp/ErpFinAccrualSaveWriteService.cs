using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fin_accrual_save</c> / ajax <c>fin_accrual_save</c> twin: straight-line schedule
/// (PHP <c>epc_fin_accrual_schedule</c> rounding, remainder on last period), fy/period roll-over,
/// UPDATE <c>epc_fin_accrual</c> when <c>id</c> &gt; 0 else INSERT with status 'open'. Schema ensure stays PHP.
/// </summary>
public interface IErpFinAccrualSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(ErpFinAccrualSaveWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record ErpFinAccrualSaveWriteRequest(
    long Id = 0,
    string? Code = null,
    string? Description = null,
    decimal TotalAmount = 0,
    int Periods = 1,
    int StartFy = 0,
    int StartPeriod = 1,
    long CompanyHint = 0);

public sealed record ErpFinAccrualScheduleRow(int Seq, int Fy, int PeriodNo, decimal Amount, int Reversed);

public sealed class ErpFinAccrualSaveWriteService : IErpFinAccrualSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFinAccrualSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(ErpFinAccrualSaveWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var periods = Math.Max(1, request.Periods);
        var startFy = request.StartFy;
        var startPeriod = Math.Max(1, request.StartPeriod);
        var code = request.Code ?? string.Empty;
        var description = request.Description ?? string.Empty;
        var schedJson = EncodeSchedule(BuildRows(request.TotalAmount, periods, startFy, startPeriod));

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpFinAdvancedCompany.ColumnExistsAsync(connection, "epc_fin_accrual", "schedule_json", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Accrual table is not provisioned");
        }

        if (request.Id > 0)
        {
            var n = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_fin_accrual` SET `code`=?, `description`=?, `total_amount`=?, `periods`=?, `start_fy`=?, `start_period`=?, `schedule_json`=? WHERE `id`=?"),
                cancellationToken,
                code,
                description,
                request.TotalAmount,
                periods,
                startFy,
                startPeriod,
                schedJson,
                request.Id).ConfigureAwait(false);
            return n == 0
                ? ErpSimpleWriteResult.Fail("invalid", "Accrual scheme not found")
                : ErpSimpleWriteResult.Ok("Accrual scheme created", request.Id);
        }

        var companyId = await ErpFinAdvancedCompany.ResolveAsync(connection, request.CompanyHint, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_fin_accrual` (`company_id`,`code`,`description`,`total_amount`,`periods`,`start_fy`,`start_period`,`status`,`schedule_json`,`time_created`) VALUES (?,?,?,?,?,?,?,'open',?,?)"),
            cancellationToken,
            companyId,
            code,
            description,
            request.TotalAmount,
            periods,
            startFy,
            startPeriod,
            schedJson,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Accrual scheme created", id);
    }

    /// <summary>PHP <c>epc_fin_accrual_schedule</c>.</summary>
    public static decimal[] Schedule(decimal total, int periods)
    {
        if (periods < 1)
        {
            periods = 1;
        }

        var per = Math.Round(total / periods, 2, MidpointRounding.AwayFromZero);
        var outArr = Enumerable.Repeat(per, periods).ToArray();
        var sum = Math.Round(per * periods, 2, MidpointRounding.AwayFromZero);
        var diff = Math.Round(total - sum, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(diff) >= 0.01m)
        {
            outArr[periods - 1] = Math.Round(outArr[periods - 1] + diff, 2, MidpointRounding.AwayFromZero);
        }

        return outArr;
    }

    public static IReadOnlyList<ErpFinAccrualScheduleRow> BuildRows(decimal total, int periods, int startFy, int startPeriod)
    {
        periods = Math.Max(1, periods);
        startPeriod = Math.Max(1, startPeriod);
        var sched = Schedule(total, periods);
        var rows = new List<ErpFinAccrualScheduleRow>(periods);
        for (var i = 0; i < periods; i++)
        {
            var m = startPeriod + i;
            rows.Add(new(i + 1, startFy + (m - 1) / 12, ((m - 1) % 12) + 1, sched[i], 0));
        }

        return rows;
    }

    public static string EncodeSchedule(IReadOnlyList<ErpFinAccrualScheduleRow> rows)
        => JsonSerializer.Serialize(rows.Select(r => new { seq = r.Seq, fy = r.Fy, period_no = r.PeriodNo, amount = r.Amount, reversed = r.Reversed }));
}
