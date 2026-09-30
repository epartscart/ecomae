using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwPurchaseHistoryReadService
{
    Task<ErpJwPurchaseHistoryResult> ReadAsync(
        int companyId, string type = "METAL", int limit = 100, CancellationToken cancellationToken = default);
}

public sealed record ErpJwPurchaseHistoryRow(
    long Id, string Branch, string VoucherType, string VoucherDate, int VoucherNumber,
    string PartyCode, string PartyName, string CustomerName, string Mobile, string Salesman,
    decimal NetAmount, decimal VatAmount, decimal TotalWithVat, string Status, bool Authorized);

public sealed record ErpJwPurchaseHistoryResult(
    IReadOnlyList<ErpJwPurchaseHistoryRow> Rows, string Source, string Message);

public sealed class ErpJwPurchaseHistoryReadService : IErpJwPurchaseHistoryReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    public ErpJwPurchaseHistoryReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpJwPurchaseHistoryResult> ReadAsync(
        int companyId, string type = "METAL", int limit = 100, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0) return Empty("Company is required.", "invalid");
        if (!_connections.IsConfigured) return Empty("TenantRegistry DB is not configured.", "migration");
        var codes = (type ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "DIAMOND" => ("DMP", "DLP"),
            _ => ("MMP", "MLP")
        };
        var boundedLimit = Math.Clamp(limit, 1, 200);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`party_code`,
                   `party_name`,`customer_name`,`mobile`,`salesman`,`net_amount`,
                   `vat_amount`,`total_with_vat`,`status`,`authorized`
            FROM `epc_jewel_voucher`
            WHERE `company_id`=? AND `voc_type` IN (?,?)
            ORDER BY `voc_date` DESC
            LIMIT {boundedLimit}
            """);
        ErpDb.AddParameters(command, companyId, codes.Item1, codes.Item2);
        var rows = new List<ErpJwPurchaseHistoryRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(reader.GetInt64(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                    reader.GetInt32(4), Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8),
                    Text(reader, 9), reader.GetDecimal(10), reader.GetDecimal(11), reader.GetDecimal(12),
                    Text(reader, 13), reader.GetBoolean(14)));
            }
        }
        catch (DbException exception) { return Empty(exception.Message, "database-error"); }
        return new(rows, "database", string.Empty);
    }

    private static ErpJwPurchaseHistoryResult Empty(string message, string source) => new([], source, message);
    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
