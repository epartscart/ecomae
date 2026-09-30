using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwMetalStockBalanceReadService
{
    Task<ErpJwMetalStockBalanceResult> ReadAsync(
        int companyId,
        string? metal = null,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwMetalStockBalanceRow(
    string Metal,
    string Karat,
    decimal TotalPieces,
    decimal TotalGrams,
    decimal TotalValue);

public sealed record ErpJwMetalStockBalanceResult(
    IReadOnlyList<ErpJwMetalStockBalanceRow> Rows,
    string Source,
    string Message);

public sealed class ErpJwMetalStockBalanceReadService : IErpJwMetalStockBalanceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwMetalStockBalanceReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpJwMetalStockBalanceResult> ReadAsync(
        int companyId,
        string? metal = null,
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

        var normalizedMetal = metal?.Trim() ?? string.Empty;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `metal`,`karat`,SUM(`stock_pcs`),SUM(`stock_gms`),SUM(`stock_value`)
            FROM `epc_jewel_metal_stock`
            WHERE `company_id`=? AND `stock_qty` > 0
              AND (? = '' OR `metal` = ?)
            GROUP BY `metal`,`karat`
            ORDER BY `metal`,`karat`
            """);
        ErpDb.AddParameters(command, companyId, normalizedMetal, normalizedMetal);

        var rows = new List<ErpJwMetalStockBalanceRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    Text(reader, 0),
                    Text(reader, 1),
                    reader.GetDecimal(2),
                    reader.GetDecimal(3),
                    reader.GetDecimal(4)));
            }
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpJwMetalStockBalanceResult Empty(string message, string source)
        => new([], source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
