using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwRepairReadService
{
    Task<ErpJwRepairResult> ReadAsync(
        int companyId,
        string from,
        string to,
        string? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwRepairRow(
    long Id,
    string Branch,
    string VoucherType,
    string VoucherDate,
    int VoucherNumber,
    long CustomerId,
    string CustomerName,
    string Mobile,
    string Salesman,
    string Currency,
    string DeliveryDate,
    string Status,
    bool Authorized,
    string Remarks,
    string RepairNarration);

public sealed record ErpJwRepairResult(
    IReadOnlyList<ErpJwRepairRow> Rows,
    string Source,
    string Message);

public sealed class ErpJwRepairReadService : IErpJwRepairReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRepairReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpJwRepairResult> ReadAsync(
        int companyId,
        string from,
        string to,
        string? status = null,
        int limit = 100,
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

        var normalizedStatus = status?.Trim() ?? string.Empty;
        var boundedLimit = Math.Clamp(limit, 1, 200);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`customer_id`,
                   `customer_name`,`mobile`,`salesman`,`currency`,`delivery_date`,
                   `status`,`authorized`,`remarks`,`repair_narration`
            FROM `epc_jewel_repair`
            WHERE `company_id`=? AND `voc_date` BETWEEN ? AND ?
              AND (? = '' OR `status`=?)
            ORDER BY `voc_no` DESC
            LIMIT {boundedLimit}
            """);
        ErpDb.AddParameters(command, companyId, from.Trim(), to.Trim(), normalizedStatus, normalizedStatus);

        var rows = new List<ErpJwRepairRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    Text(reader, 1),
                    Text(reader, 2),
                    Text(reader, 3),
                    reader.GetInt32(4),
                    reader.GetInt64(5),
                    Text(reader, 6),
                    Text(reader, 7),
                    Text(reader, 8),
                    Text(reader, 9),
                    Text(reader, 10),
                    Text(reader, 11),
                    reader.GetBoolean(12),
                    Text(reader, 13),
                    Text(reader, 14)));
            }
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpJwRepairResult Empty(string message, string source)
        => new([], source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
