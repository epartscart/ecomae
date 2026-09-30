using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwRepairReceiptHistoryReadService
{
    Task<ErpJwRepairReceiptHistoryResult> ReadAsync(
        int companyId, string from, string to, string status = "", int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwRepairReceiptHistoryRow(
    long Id, string Branch, string VoucherType, string VoucherDate, int VoucherNumber,
    long CustomerId, string CustomerName, string Mobile, string Salesman,
    string Currency, string DeliveryDate, string Status, bool Authorized);

public sealed record ErpJwRepairReceiptHistoryResult(
    IReadOnlyList<ErpJwRepairReceiptHistoryRow> Rows, string Source, string Message);

public sealed class ErpJwRepairReceiptHistoryReadService : IErpJwRepairReceiptHistoryReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    public ErpJwRepairReceiptHistoryReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpJwRepairReceiptHistoryResult> ReadAsync(
        int companyId, string from, string to, string status = "", int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0 || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return Empty("Company and date range are required.", "invalid");
        if (!_connections.IsConfigured) return Empty("TenantRegistry DB is not configured.", "migration");
        var boundedLimit = Math.Clamp(limit, 1, 200);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`customer_id`,
                   `customer_name`,`mobile`,`salesman`,`currency`,`delivery_date`,
                   `status`,`authorized`
            FROM `epc_jewel_repair`
            WHERE `company_id`=? AND `voc_date` BETWEEN ? AND ?
            {(string.IsNullOrWhiteSpace(status) ? string.Empty : "AND `status`=?")}
            ORDER BY `voc_no` DESC
            LIMIT {boundedLimit}
            """);
        if (string.IsNullOrWhiteSpace(status))
            ErpDb.AddParameters(command, companyId, from, to);
        else
            ErpDb.AddParameters(command, companyId, from, to, status.Trim());
        var rows = new List<ErpJwRepairReceiptHistoryRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                rows.Add(new(reader.GetInt64(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                    reader.GetInt32(4), reader.GetInt64(5), Text(reader, 6), Text(reader, 7),
                    Text(reader, 8), Text(reader, 9), Text(reader, 10), Text(reader, 11),
                    reader.GetBoolean(12)));
        }
        catch (DbException exception) { return Empty(exception.Message, "database-error"); }
        return new(rows, "database", string.Empty);
    }

    private static ErpJwRepairReceiptHistoryResult Empty(string message, string source) => new([], source, message);
    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
