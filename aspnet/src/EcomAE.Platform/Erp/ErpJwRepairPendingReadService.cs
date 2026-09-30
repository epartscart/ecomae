using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwRepairPendingReadService
{
    Task<ErpJwRepairPendingResult> ReadAsync(
        int companyId,
        string? division = null,
        string? branch = null,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwRepairPendingRow(
    long RepairId,
    string Branch,
    string VoucherDate,
    int VoucherNumber,
    string CustomerName,
    string RepairStatus,
    string Division,
    string StockCode,
    string Description,
    string BagNumber,
    int Pieces,
    decimal GrossWeight,
    string RepairType,
    string ItemStatus);

public sealed record ErpJwRepairPendingResult(
    IReadOnlyList<ErpJwRepairPendingRow> Rows,
    string Source,
    string Message);

public sealed class ErpJwRepairPendingReadService : IErpJwRepairPendingReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRepairPendingReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpJwRepairPendingResult> ReadAsync(
        int companyId,
        string? division = null,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
        {
            return Empty("Company is required.", "invalid");
        }

        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.", "migration");
        }

        var normalizedDivision = division?.Trim() ?? string.Empty;
        var normalizedBranch = branch?.Trim() ?? string.Empty;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT r.`id`,r.`branch`,r.`voc_date`,r.`voc_no`,r.`customer_name`,
                   r.`status`,ri.`division`,ri.`stock_code`,ri.`description`,
                   ri.`bag_no`,ri.`pcs`,ri.`gr_wt`,ri.`repair_type`,
                   ri.`repair_status`
            FROM `epc_jewel_repair` r
            JOIN `epc_jewel_repair_items` ri ON ri.`repair_id`=r.`id`
            WHERE r.`company_id`=? AND r.`status` IN ('received','in_progress')
              AND (? = '' OR ri.`division`=?)
              AND (? = '' OR r.`branch`=?)
            ORDER BY r.`voc_no` DESC
            """);
        ErpDb.AddParameters(
            command, companyId, normalizedDivision, normalizedDivision, normalizedBranch, normalizedBranch);

        var rows = new List<ErpJwRepairPendingRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    Text(reader, 1),
                    Text(reader, 2),
                    reader.GetInt32(3),
                    Text(reader, 4),
                    Text(reader, 5),
                    Text(reader, 6),
                    Text(reader, 7),
                    Text(reader, 8),
                    Text(reader, 9),
                    reader.GetInt32(10),
                    reader.GetDecimal(11),
                    Text(reader, 12),
                    Text(reader, 13)));
            }
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpJwRepairPendingResult Empty(string message, string source)
        => new([], source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
