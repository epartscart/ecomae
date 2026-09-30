using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwDesignDiamondReadService
{
    Task<ErpJwDesignDiamondResult> ReadAsync(
        int companyId, string kind, int limit = 100, int offset = 0,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwDesignDiamondRow(
    long Id, string Code, string Description, string Design, string Category,
    string SubCategory, string Vendor, string Country, string Currency,
    decimal CostAmount, decimal Price1, decimal Price2, bool Promotional);

public sealed record ErpJwDesignDiamondResult(
    IReadOnlyList<ErpJwDesignDiamondRow> Rows, string Source, string Message);

public sealed class ErpJwDesignDiamondReadService : IErpJwDesignDiamondReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    public ErpJwDesignDiamondReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpJwDesignDiamondResult> ReadAsync(
        int companyId, string kind, int limit = 100, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0 || string.IsNullOrWhiteSpace(kind))
            return Empty("Company and master kind are required.", "invalid");
        if (!_connections.IsConfigured) return Empty("TenantRegistry DB is not configured.", "migration");
        var isDiamond = kind.Trim().Equals("DIAMOND", StringComparison.OrdinalIgnoreCase);
        var boundedLimit = Math.Clamp(limit, 1, 200);
        var boundedOffset = Math.Max(offset, 0);
        var table = isDiamond ? "epc_jewel_diamond_master" : "epc_jewel_design";
        var codeColumn = isDiamond ? "item_code" : "design_code";
        var designColumn = isDiamond ? "design" : "design_code";
        var promotional = isDiamond ? "`promotional`" : "0";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`{codeColumn}` AS `code`,`description`,`{designColumn}` AS `design`,
                   `category`,`sub_category`,`vendor`,`country`,`currency`,`cost_amount`,
                   `price1_lc`,`price2_lc`,{promotional} AS `promotional`
            FROM `{table}`
            WHERE `company_id`=?
            ORDER BY `{codeColumn}` ASC
            LIMIT {boundedLimit} OFFSET {boundedOffset}
            """);
        ErpDb.AddParameters(command, companyId);
        var rows = new List<ErpJwDesignDiamondRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(reader.GetInt64(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                    Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8),
                    reader.GetDecimal(9), reader.GetDecimal(10), reader.GetDecimal(11), reader.GetBoolean(12)));
            }
        }
        catch (DbException exception) { return Empty(exception.Message, "database-error"); }
        return new(rows, "database", string.Empty);
    }

    private static ErpJwDesignDiamondResult Empty(string message, string source) => new([], source, message);
    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
