using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutExecutiveDashboardReadService
{
    Task<ErpFitOutExecutiveDashboardResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutExecutiveDashboardRow(
    long ProjectId,
    int EstimateCount,
    decimal BoqValue,
    decimal CommittedAmount,
    decimal DeliveryAmount,
    decimal DeliveryCompletionPercent,
    decimal PendingApprovalAmount);

public sealed record ErpFitOutExecutiveDashboardResult(
    IReadOnlyList<ErpFitOutExecutiveDashboardRow> Rows,
    string Source,
    string Message);

public sealed class ErpFitOutExecutiveDashboardReadService
    : IErpFitOutExecutiveDashboardReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutExecutiveDashboardReadService(
        IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutExecutiveDashboardResult> ReadAsync(
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
        var filter = projectId is > 0 ? "WHERE `project_id`=?" : string.Empty;
        command.CommandText = ErpDb.Positional($"""
            SELECT `project_id`,
                   (SELECT COUNT(*) FROM `ecomae_fitout_estimates` e
                    WHERE e.`project_id`=p.`project_id`) AS `estimate_count`,
                   (SELECT COALESCE(SUM(b.`quantity`*b.`unit_rate`),0)
                    FROM `ecomae_fitout_estimates` e
                    INNER JOIN `ecomae_fitout_boq_lines` b ON b.`estimate_id`=e.`id`
                    WHERE e.`project_id`=p.`project_id`) AS `boq_value`,
                   (SELECT COALESCE(SUM(`committed_amount`),0)
                    FROM `ecomae_fitout_procurement_links`
                    WHERE `project_id`=p.`project_id`) AS `committed_amount`,
                   (SELECT COALESCE(SUM(`amount`),0)
                    FROM `ecomae_fitout_delivery_records`
                    WHERE `project_id`=p.`project_id`) AS `delivery_amount`,
                   (SELECT COALESCE(AVG(`completion_percent`),0)
                    FROM `ecomae_fitout_delivery_records`
                    WHERE `project_id`=p.`project_id`) AS `delivery_completion`,
                   (SELECT COALESCE(SUM(`amount`),0)
                    FROM `ecomae_fitout_delivery_records`
                    WHERE `project_id`=p.`project_id`
                      AND `record_type`='approval_request'
                      AND `status`='pending') AS `pending_approval_amount`
            FROM (
                SELECT `project_id` FROM `ecomae_fitout_estimates` {filter}
                UNION
                SELECT `project_id` FROM `ecomae_fitout_procurement_links` {filter}
                UNION
                SELECT `project_id` FROM `ecomae_fitout_delivery_records` {filter}
            ) p
            ORDER BY `project_id`
            """);
        if (projectId is > 0)
        {
            ErpDb.AddParameters(command, projectId.Value, projectId.Value, projectId.Value);
        }

        var rows = new List<ErpFitOutExecutiveDashboardRow>();
        try
        {
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    reader.GetDecimal(2),
                    reader.GetDecimal(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5),
                    reader.GetDecimal(6)));
            }
        }
        catch (DbException exception)
        {
            return new([], "database-error", exception.Message);
        }

        return new(rows, "database", string.Empty);
    }
}
