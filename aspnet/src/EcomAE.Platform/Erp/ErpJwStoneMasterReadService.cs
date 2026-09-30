using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwStoneMasterReadService
{
    Task<ErpJwStoneMasterResult> ReadAsync(
        int companyId, string kind, int limit = 200, CancellationToken cancellationToken = default);
}

public sealed record ErpJwStoneMasterRow(
    long Id,
    string Code,
    string Description,
    string Design,
    string Category,
    string SubCategory,
    string Vendor,
    string Country,
    string Currency,
    decimal CostAmount,
    decimal Price1,
    decimal Price2,
    string Status);

public sealed record ErpJwStoneMasterResult(
    IReadOnlyList<ErpJwStoneMasterRow> Rows, string Source, string Message);

public sealed class ErpJwStoneMasterReadService : IErpJwStoneMasterReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    public ErpJwStoneMasterReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpJwStoneMasterResult> ReadAsync(
        int companyId, string kind, int limit = 200, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0 || string.IsNullOrWhiteSpace(kind))
            return Empty("Company and master kind are required.", "invalid");
        if (!_connections.IsConfigured)
            return Empty("TenantRegistry DB is not configured.", "migration");

        var table = kind.Trim().ToUpperInvariant() switch
        {
            "PEARL" => "epc_jewel_pearl_master",
            "COLOR_STONE" => "epc_jewel_color_stone_master",
            _ => string.Empty
        };
        if (table.Length == 0) return Empty("Unsupported master kind.", "invalid");

        var boundedLimit = Math.Clamp(limit, 1, 200);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`code`,`description`,`design`,`category`,`sub_category`,
                   `vendor`,`country`,`currency`,`cost_amount`,`price1_lc`,`price2_lc`,
                   `promotional`
            FROM `{table}`
            WHERE `company_id`=?
            ORDER BY `code` ASC
            LIMIT {boundedLimit}
            """);
        ErpDb.AddParameters(command, companyId);
        var rows = new List<ErpJwStoneMasterRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(reader.GetInt64(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                    Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8),
                    reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11),
                    reader.GetBoolean(12) ? "promotional" : "standard"));
            }
        }
        catch (DbException exception) { return Empty(exception.Message, "database-error"); }
        return new(rows, "database", string.Empty);
    }

    private static ErpJwStoneMasterResult Empty(string message, string source) => new([], source, message);
    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
