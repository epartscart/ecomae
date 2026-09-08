using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_prj_log_time</c> twin. INSERT <c>epc_prj_timesheets</c>.
/// Schema ensure, project save, and task save stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpPrjLogTimeWriteService
{
    Task<ErpSimpleWriteResult> LogAsync(
        ErpPrjLogTimeWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPrjLogTimeWriteRequest(
    long ProjectId = 0,
    long TaskId = 0,
    long EmployeeId = 0,
    long WorkDate = 0,
    decimal Hours = 0,
    decimal CostRate = 0,
    decimal BillRate = 0,
    bool Billable = false);

public sealed class ErpPrjLogTimeWriteService : IErpPrjLogTimeWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPrjLogTimeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> LogAsync(
        ErpPrjLogTimeWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select a project");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var taskId = request.TaskId < 0 ? 0 : request.TaskId;
        var employeeId = request.EmployeeId < 0 ? 0 : request.EmployeeId;
        var workDate = request.WorkDate > 0 ? request.WorkDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hours = decimal.Round(request.Hours, 2, MidpointRounding.AwayFromZero);
        var cost = decimal.Round(request.CostRate, 2, MidpointRounding.AwayFromZero);
        var bill = decimal.Round(request.BillRate, 2, MidpointRounding.AwayFromZero);
        var billable = request.Billable ? 1 : 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_prj_timesheets", "hours", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_prj_timesheets", "cost_rate", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project timesheet table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_prj_timesheets` (`project_id`,`task_id`,`employee_id`,`work_date`,`hours`,`cost_rate`,`bill_rate`,`billable`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            request.ProjectId,
            taskId,
            employeeId,
            workDate,
            hours,
            cost,
            bill,
            billable).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Time logged", inserted);
    }

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
