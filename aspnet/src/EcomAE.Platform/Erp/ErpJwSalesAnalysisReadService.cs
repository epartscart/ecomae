using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwSalesAnalysisReadService
{
    Task<ErpJwSalesAnalysisResult> ReadAsync(
        int companyId,
        string from,
        string to,
        string? groupBy = null,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwSalesAnalysisRow(
    string GroupKey,
    int VoucherCount,
    decimal TotalPieces,
    decimal TotalWeight,
    decimal MetalTotal,
    decimal MakingTotal,
    decimal GrandTotal);

public sealed record ErpJwSalesAnalysisResult(
    IReadOnlyList<ErpJwSalesAnalysisRow> Rows,
    string Source,
    string Message);

public sealed class ErpJwSalesAnalysisReadService : IErpJwSalesAnalysisReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwSalesAnalysisReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpJwSalesAnalysisResult> ReadAsync(
        int companyId,
        string from,
        string to,
        string? groupBy = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0 || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return Empty("Company and date range are required.", "invalid");
        }

        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.", "migration");
        }

        var grouping = groupBy?.Trim().ToLowerInvariant() switch
        {
            "salesman" => ("v.`salesman`", "v.`salesman`"),
            "division" => ("vl.`division`", "vl.`division`"),
            _ => ("v.`voc_date`", "v.`voc_date`")
        };
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT {grouping.Item1}, COUNT(DISTINCT v.`id`), SUM(vl.`pcs`),
                   SUM(vl.`gr_wt`), SUM(vl.`metal_amount`),
                   SUM(vl.`mkg_amount`), SUM(vl.`total_amount`)
            FROM `epc_jewel_voucher` v
            JOIN `epc_jewel_voucher_lines` vl ON vl.`voucher_id`=v.`id`
            WHERE v.`company_id`=? AND v.`voc_type` IN ('RIN','MSL','RSL')
              AND v.`voc_date` BETWEEN ? AND ?
            GROUP BY {grouping.Item2}
            ORDER BY {grouping.Item2}
            """);
        ErpDb.AddParameters(command, companyId, from.Trim(), to.Trim());

        var rows = new List<ErpJwSalesAnalysisRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    Text(reader, 0),
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
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpJwSalesAnalysisResult Empty(string message, string source)
        => new([], source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
