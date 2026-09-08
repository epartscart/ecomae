using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_prj_task_save</c> twin. INSERT/UPDATE <c>epc_prj_tasks</c>.
/// Schema ensure, project save, and timesheet log stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpPrjTaskSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPrjTaskSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPrjTaskSaveWriteRequest(
    long Id = 0,
    long ProjectId = 0,
    string? Name = null,
    decimal PlannedHours = 0,
    decimal PercentComplete = 0,
    string? Status = null);

public sealed class ErpPrjTaskSaveWriteService : IErpPrjTaskSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPrjTaskSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPrjTaskSaveWriteRequest request,
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

        var name = Clip((request.Name ?? string.Empty).Trim(), 160);
        var status = Clip((request.Status ?? string.Empty).Trim(), 12);
        if (status.Length == 0)
        {
            status = "open";
        }

        var planned = decimal.Round(request.PlannedHours, 2, MidpointRounding.AwayFromZero);
        var percent = decimal.Round(request.PercentComplete, 2, MidpointRounding.AwayFromZero);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_prj_tasks", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_prj_tasks", "planned_hours", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project task table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_prj_tasks` SET `name`=?, `planned_hours`=?, `percent_complete`=?, `status`=? WHERE `id`=?"),
                cancellationToken,
                name,
                planned,
                percent,
                status,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Task saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_prj_tasks` (`project_id`,`name`,`planned_hours`,`percent_complete`,`status`) VALUES (?,?,?,?, 'open')"),
            cancellationToken,
            request.ProjectId,
            name,
            planned,
            percent).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Task saved", inserted);
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
