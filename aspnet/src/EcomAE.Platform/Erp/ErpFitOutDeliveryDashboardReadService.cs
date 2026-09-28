using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutDeliveryDashboardReadService
{
    Task<ErpFitOutDeliveryDashboardResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutDeliveryDashboardRow(
    long ProjectId,
    string RecordType,
    string Status,
    int Count,
    decimal Amount,
    decimal AverageCompletionPercent);

public sealed record ErpFitOutDeliveryDashboardResult(
    IReadOnlyList<ErpFitOutDeliveryDashboardRow> Rows,
    string Source,
    string Message);

public sealed class ErpFitOutDeliveryDashboardReadService : IErpFitOutDeliveryDashboardReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutDeliveryDashboardReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutDeliveryDashboardResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], "migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = projectId is > 0
            ? ErpDb.Positional("""
                SELECT `project_id`,`record_type`,`status`,COUNT(*) AS `record_count`,
                       COALESCE(SUM(`amount`),0) AS `record_amount`,
                       COALESCE(AVG(`completion_percent`),0) AS `avg_completion`
                FROM `ecomae_fitout_delivery_records`
                WHERE `project_id`=?
                GROUP BY `project_id`,`record_type`,`status`
                ORDER BY `project_id`,`record_type`,`status`
                """)
            : """
                SELECT `project_id`,`record_type`,`status`,COUNT(*) AS `record_count`,
                       COALESCE(SUM(`amount`),0) AS `record_amount`,
                       COALESCE(AVG(`completion_percent`),0) AS `avg_completion`
                FROM `ecomae_fitout_delivery_records`
                GROUP BY `project_id`,`record_type`,`status`
                ORDER BY `project_id`,`record_type`,`status`
                """;
        if (projectId is > 0)
        {
            ErpDb.AddParameters(command, projectId.Value);
        }

        var rows = new List<ErpFitOutDeliveryDashboardRow>();
        try
        {
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5)));
            }
        }
        catch (DbException exception)
        {
            return new([], "database-error", exception.Message);
        }

        return new(rows, "database", string.Empty);
    }
}
