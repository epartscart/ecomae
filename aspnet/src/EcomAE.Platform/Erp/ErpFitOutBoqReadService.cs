using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutBoqReadService
{
    Task<ErpFitOutBoqReadResult> ReadAsync(
        long estimateId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutBoqEstimate(
    long Id,
    long ProjectId,
    string Code,
    string Title,
    int Revision,
    decimal MarkupPercent,
    string Status);

public sealed record ErpFitOutBoqLine(
    long Id,
    string Section,
    string Description,
    string CostType,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    decimal MaterialRate,
    decimal LabourRate,
    decimal SubcontractRate,
    decimal EquipmentRate,
    decimal OverheadRate,
    decimal TotalCost,
    decimal SellingRate,
    decimal SellingAmount,
    int SortOrder);

public sealed record ErpFitOutBoqReadResult(
    long EstimateId,
    ErpFitOutBoqEstimate? Estimate,
    IReadOnlyList<ErpFitOutBoqLine> Lines,
    decimal TotalCost,
    decimal SellingAmount,
    string Source,
    string Message);

public sealed class ErpFitOutBoqReadService : IErpFitOutBoqReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutBoqReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutBoqReadResult> ReadAsync(
        long estimateId,
        CancellationToken cancellationToken = default)
    {
        if (estimateId <= 0)
        {
            return Empty(estimateId, "Estimate id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return Empty(estimateId, "TenantRegistry DB is not configured.", "migration");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var estimate = await ReadEstimateAsync(connection, estimateId, cancellationToken)
                .ConfigureAwait(false);
            if (estimate is null)
            {
                return Empty(estimateId, "Estimate was not found.");
            }

            var lines = await ReadLinesAsync(connection, estimateId, cancellationToken)
                .ConfigureAwait(false);
            return new(
                estimateId,
                estimate,
                lines,
                lines.Sum(line => line.TotalCost),
                lines.Sum(line => line.SellingAmount),
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(estimateId, exception.Message, "database-error");
        }
    }

    private static async Task<ErpFitOutBoqEstimate?> ReadEstimateAsync(
        DbConnection connection,
        long estimateId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `id`,`project_id`,`code`,`title`,`revision`,`markup_percent`,`status`
            FROM `ecomae_fitout_estimates`
            WHERE `id`=?
            LIMIT 1
            """);
        ErpDb.AddParameters(command, estimateId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetDecimal(5),
            reader.GetString(6));
    }

    private static async Task<IReadOnlyList<ErpFitOutBoqLine>> ReadLinesAsync(
        DbConnection connection,
        long estimateId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `id`,`section`,`description`,`cost_type`,`quantity`,`unit`,
                   `unit_rate`,`material_rate`,`labour_rate`,`subcontract_rate`,
                   `equipment_rate`,`overhead_rate`,`total_cost`,`selling_rate`,
                   `selling_amount`,`sort_order`
            FROM `ecomae_fitout_boq_lines`
            WHERE `estimate_id`=?
            ORDER BY `sort_order`,`id`
            """);
        ErpDb.AddParameters(command, estimateId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var lines = new List<ErpFitOutBoqLine>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lines.Add(new(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetString(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetDecimal(10),
                reader.GetDecimal(11),
                reader.GetDecimal(12),
                reader.GetDecimal(13),
                reader.GetDecimal(14),
                reader.GetInt32(15)));
        }

        return lines;
    }

    private static ErpFitOutBoqReadResult Empty(
        long estimateId,
        string message,
        string source = "migration")
        => new(estimateId, null, [], 0m, 0m, source, message);
}
