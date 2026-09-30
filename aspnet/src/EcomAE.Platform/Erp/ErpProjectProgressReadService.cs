using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpProjectProgressReadService
{
    Task<ErpProjectProgressResult> ReadAsync(
        long projectId, int limit = 200, CancellationToken cancellationToken = default);
}

public sealed record ErpProjectProgressProject(
    long Id, string Code, string Name, long CustomerId, string BillingType,
    decimal BudgetCost, decimal ContractValue, string Status, long TimeCreated);

public sealed record ErpProjectProgressTask(
    long Id, long ProjectId, string Name, decimal PlannedHours,
    decimal PercentComplete, string Status);

public sealed record ErpProjectProgressTimesheet(
    long Id, long ProjectId, long TaskId, long EmployeeId, long WorkDate,
    decimal Hours, decimal CostRate, decimal BillRate, bool Billable, string TaskName);

public sealed record ErpProjectProgressSummary(
    decimal Hours, decimal Cost, decimal BudgetCost, decimal CostVariance,
    bool OverBudget, decimal BillableValue, decimal RevenueRecognized,
    decimal Margin, decimal PercentComplete);

public sealed record ErpProjectProgressResult(
    ErpProjectProgressProject? Project,
    IReadOnlyList<ErpProjectProgressTask> Tasks,
    IReadOnlyList<ErpProjectProgressTimesheet> Timesheets,
    ErpProjectProgressSummary? Summary,
    string Source,
    string Message);

public sealed class ErpProjectProgressReadService : IErpProjectProgressReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpProjectProgressReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpProjectProgressResult> ReadAsync(
        long projectId, int limit = 200, CancellationToken cancellationToken = default)
    {
        if (projectId <= 0)
            return Empty("A project id is required.", "invalid");
        if (!_connections.IsConfigured)
            return Empty("TenantRegistry DB is not configured.", "migration");

        var boundedLimit = Math.Clamp(limit, 1, 500);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var project = await ReadProjectAsync(connection, projectId, cancellationToken).ConfigureAwait(false);
            if (project is null)
                return Empty("Project was not found.", "not-found");

            var tasks = await ReadTasksAsync(connection, projectId, cancellationToken).ConfigureAwait(false);
            var timesheets = await ReadTimesheetsAsync(connection, projectId, boundedLimit, cancellationToken)
                .ConfigureAwait(false);
            var summary = BuildSummary(project, tasks, timesheets);
            return new(project, tasks, timesheets, summary, "database", string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }
    }

    private static async Task<ErpProjectProgressProject?> ReadProjectAsync(
        DbConnection connection, long projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `id`,`code`,`name`,`customer_id`,`billing_type`,
                   `budget_cost`,`contract_value`,`status`,`time_created`
            FROM `epc_prj_projects`
            WHERE `id`=?
            LIMIT 1
            """);
        ErpDb.AddParameters(command, projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;
        return new(
            reader.GetInt64(0), Text(reader, 1), Text(reader, 2), reader.GetInt64(3),
            Text(reader, 4), reader.GetDecimal(5), reader.GetDecimal(6), Text(reader, 7),
            reader.GetInt64(8));
    }

    private static async Task<IReadOnlyList<ErpProjectProgressTask>> ReadTasksAsync(
        DbConnection connection, long projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `id`,`project_id`,`name`,`planned_hours`,`percent_complete`,`status`
            FROM `epc_prj_tasks`
            WHERE `project_id`=?
            ORDER BY `id`
            """);
        ErpDb.AddParameters(command, projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<ErpProjectProgressTask>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                reader.GetInt64(0), reader.GetInt64(1), Text(reader, 2),
                reader.GetDecimal(3), reader.GetDecimal(4), Text(reader, 5)));
        }
        return rows;
    }

    private static async Task<IReadOnlyList<ErpProjectProgressTimesheet>> ReadTimesheetsAsync(
        DbConnection connection, long projectId, int limit, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT t.`id`,t.`project_id`,t.`task_id`,t.`employee_id`,t.`work_date`,
                   t.`hours`,t.`cost_rate`,t.`bill_rate`,t.`billable`,
                   COALESCE(k.`name`,'')
            FROM `epc_prj_timesheets` t
            LEFT JOIN `epc_prj_tasks` k ON k.`id`=t.`task_id`
            WHERE t.`project_id`=?
            ORDER BY t.`id` DESC
            LIMIT {limit}
            """);
        ErpDb.AddParameters(command, projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<ErpProjectProgressTimesheet>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
                reader.GetInt64(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7),
                reader.GetBoolean(8), Text(reader, 9)));
        }
        return rows;
    }

    private static ErpProjectProgressSummary BuildSummary(
        ErpProjectProgressProject project,
        IReadOnlyList<ErpProjectProgressTask> tasks,
        IReadOnlyList<ErpProjectProgressTimesheet> timesheets)
    {
        var hours = timesheets.Sum(row => row.Hours);
        var cost = decimal.Round(timesheets.Sum(row => row.Hours * row.CostRate), 2);
        var billable = decimal.Round(timesheets
            .Where(row => row.Billable)
            .Sum(row => row.Hours * row.BillRate), 2);
        var value = project.BillingType == "fixed" ? project.ContractValue : billable;
        var percent = decimal.Round(tasks.Count == 0 ? 0m : tasks.Average(row => row.PercentComplete), 2);
        var recognized = decimal.Round(value * percent / 100m, 2);
        return new(
            decimal.Round(hours, 2), cost, project.BudgetCost,
            decimal.Round(project.BudgetCost - cost, 2), cost > project.BudgetCost,
            billable, recognized, decimal.Round(value - cost, 2), percent);
    }

    private static ErpProjectProgressResult Empty(string message, string source)
        => new(null, [], [], null, source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
