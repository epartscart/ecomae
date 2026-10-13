using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpJwMetalStockListRow(
    long Id,
    string Metal,
    string ItemCode,
    string Description,
    string Karat,
    decimal Purity,
    string Type,
    string Brand,
    string Category,
    string Vendor,
    string McUnit,
    decimal StdCost,
    decimal PurCostGms,
    decimal SalePriceGms,
    decimal StockPcs,
    decimal StockGms,
    decimal StockValue,
    string CcMaking,
    string CcMetal,
    string Country,
    string HsCode,
    string Price1Code,
    string Price1Label);

public sealed record ErpJwMetalStockListResult(
    IReadOnlyList<ErpJwMetalStockListRow> Rows,
    ErpJwMetalStockListRow? Opened,
    string Source,
    string Message);

public interface IErpJwMetalStockListReadService
{
    Task<ErpJwMetalStockListResult> ReadAsync(
        int companyId,
        long openedId = 0,
        string? openedCode = null,
        int limit = 200,
        CancellationToken cancellationToken = default);
}

public sealed class ErpJwMetalStockListReadService : IErpJwMetalStockListReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwMetalStockListReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpJwMetalStockListResult> ReadAsync(
        int companyId,
        long openedId = 0,
        string? openedCode = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], null, "migration", "TenantRegistry DB is not configured.");
        }

        var cap = Math.Clamp(limit <= 0 ? 200 : limit, 1, 500);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return new([], null, "database", "Jewellery metal stock tables are not provisioned.");
        }

        var rows = new List<ErpJwMetalStockListRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`metal`,`item_code`,`description`,`karat`,`purity`,`type`,`brand`,`category`,`vendor`,`mc_unit`,`std_cost`,`pur_cost_gms`,`sale_price_gms`,`stock_pcs`,`stock_gms`,`stock_value`,`cc_making`,`cc_metal`,`country`,`hs_code`,`price1_code`,`price1_label` FROM `epc_jewel_metal_stock`"
                + (companyId > 0 ? " WHERE `company_id`=?" : "")
                + " ORDER BY `item_code` ASC LIMIT " + cap.ToString(CultureInfo.InvariantCulture));
            if (companyId > 0)
            {
                ErpDb.AddParameters(command, companyId);
            }

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(ReadRow(reader));
                }
            }
            catch (DbException ex)
            {
                return new([], null, "database-error", ex.Message);
            }
        }

        ErpJwMetalStockListRow? opened = null;
        var code = (openedCode ?? string.Empty).Trim();
        if (openedId > 0)
        {
            opened = rows.FirstOrDefault(row => row.Id == openedId);
        }

        if (opened is null && code.Length > 0)
        {
            opened = rows.FirstOrDefault(row => string.Equals(row.ItemCode, code, StringComparison.OrdinalIgnoreCase));
        }

        return new(rows, opened, "database", string.Empty);
    }

    private static ErpJwMetalStockListRow ReadRow(DbDataReader reader)
        => new(
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            Text(reader, 1),
            Text(reader, 2),
            Text(reader, 3),
            Text(reader, 4),
            Dec(reader, 5),
            Text(reader, 6),
            Text(reader, 7),
            Text(reader, 8),
            Text(reader, 9),
            Text(reader, 10),
            Dec(reader, 11),
            Dec(reader, 12),
            Dec(reader, 13),
            Dec(reader, 14),
            Dec(reader, 15),
            Dec(reader, 16),
            Text(reader, 17),
            Text(reader, 18),
            Text(reader, 19),
            Text(reader, 20),
            Text(reader, 21),
            Text(reader, 22));

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static decimal Dec(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static async Task<bool> TableExistsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            "epc_jewel_metal_stock").ConfigureAwait(false);
        return n > 0;
    }
}
