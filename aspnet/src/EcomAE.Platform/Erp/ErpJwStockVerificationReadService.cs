using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwStockVerificationReadService
{
    Task<ErpJwStockVerificationResult> ReadAsync(
        int companyId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwStockVerificationRow(
    long Id,
    int CompanyId,
    string Branch,
    string VoucherType,
    string VoucherDate,
    int VoucherNumber,
    string VerifiedBy,
    string Location,
    string MetalStone,
    string Division,
    decimal TotalPcs,
    decimal ScannedPcs,
    decimal RemainingPcs,
    decimal TotalWeight,
    decimal ScannedWeight,
    decimal RemainingWeight,
    string Remarks,
    string Status);

public sealed record ErpJwStockVerificationResult(
    IReadOnlyList<ErpJwStockVerificationRow> Rows,
    string Source,
    string Message);

public sealed class ErpJwStockVerificationReadService : IErpJwStockVerificationReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwStockVerificationReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpJwStockVerificationResult> ReadAsync(
        int companyId,
        int limit = 50,
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

        var boundedLimit = Math.Clamp(limit, 1, 200);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,
                   `verified_by`,`location`,`metal_stone`,`division`,
                   `total_pcs`,`scanned_pcs`,`remaining_pcs`,
                   `total_wgt`,`scanned_wgt`,`remaining_wgt`,`remarks`,`status`
            FROM `epc_jewel_stock_verification`
            WHERE `company_id`=?
            ORDER BY `id` DESC
            LIMIT {boundedLimit}
            """);
        ErpDb.AddParameters(command, companyId);

        var rows = new List<ErpJwStockVerificationRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    Text(reader, 2),
                    Text(reader, 3),
                    Text(reader, 4),
                    reader.GetInt32(5),
                    Text(reader, 6),
                    Text(reader, 7),
                    Text(reader, 8),
                    Text(reader, 9),
                    reader.GetDecimal(10),
                    reader.GetDecimal(11),
                    reader.GetDecimal(12),
                    reader.GetDecimal(13),
                    reader.GetDecimal(14),
                    reader.GetDecimal(15),
                    Text(reader, 16),
                    Text(reader, 17)));
            }
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpJwStockVerificationResult Empty(string message, string source)
        => new([], source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
