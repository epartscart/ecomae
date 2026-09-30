using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutEstimateRevisionReadService
{
    Task<ErpFitOutEstimateRevisionComparison> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutEstimateRevisionRow(
    long EstimateId,
    string Code,
    string Title,
    int Revision,
    decimal MarkupPercent,
    string Status,
    int BoqLineCount,
    decimal TotalCost,
    decimal SellingAmount);

public sealed record ErpFitOutEstimateRevisionDelta(
    int FromRevision,
    int ToRevision,
    decimal CostDelta,
    decimal SellingDelta,
    int BoqLineDelta);

public sealed record ErpFitOutEstimateRevisionComparison(
    long ProjectId,
    IReadOnlyList<ErpFitOutEstimateRevisionRow> Revisions,
    IReadOnlyList<ErpFitOutEstimateRevisionDelta> Deltas,
    string Source,
    string Message);

public sealed class ErpFitOutEstimateRevisionReadService
    : IErpFitOutEstimateRevisionReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutEstimateRevisionReadService(
        IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutEstimateRevisionComparison> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId <= 0)
        {
            return Empty(projectId, "Project id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return Empty(projectId, "TenantRegistry DB is not configured.", "migration");
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT e.`id`, e.`code`, e.`title`, e.`revision`, e.`markup_percent`,
                   e.`status`, COUNT(l.`id`),
                   COALESCE(SUM(l.`total_cost`), 0),
                   COALESCE(SUM(l.`selling_amount`), 0)
            FROM `ecomae_fitout_estimates` e
            LEFT JOIN `ecomae_fitout_boq_lines` l ON l.`estimate_id` = e.`id`
            WHERE e.`project_id` = ?
            GROUP BY e.`id`, e.`code`, e.`title`, e.`revision`,
                     e.`markup_percent`, e.`status`
            ORDER BY e.`revision`, e.`id`
            """);
        ErpDb.AddParameters(command, projectId);

        var revisions = new List<ErpFitOutEstimateRevisionRow>();
        try
        {
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                revisions.Add(new(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetDecimal(4),
                    reader.GetString(5),
                    reader.GetInt32(6),
                    reader.GetDecimal(7),
                    reader.GetDecimal(8)));
            }
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }

        var deltas = revisions
            .Zip(revisions.Skip(1), (from, to) => new ErpFitOutEstimateRevisionDelta(
                from.Revision,
                to.Revision,
                decimal.Round(to.TotalCost - from.TotalCost, 4),
                decimal.Round(to.SellingAmount - from.SellingAmount, 4),
                to.BoqLineCount - from.BoqLineCount))
            .ToArray();
        return new(projectId, revisions, deltas, "database", string.Empty);
    }

    private static ErpFitOutEstimateRevisionComparison Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(projectId, Array.Empty<ErpFitOutEstimateRevisionRow>(), Array.Empty<ErpFitOutEstimateRevisionDelta>(), source, message);
}
