using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpInventoryScanReadService
{
    Task<ErpInventoryScanLookupResult> LookupAsync(string? code, CancellationToken cancellationToken = default);
}

public sealed record ErpInventoryScanLookupResult(
    ErpSimpleWriteResult Result,
    IReadOnlyDictionary<string, object?>? Item,
    decimal OnHand);

/// <summary>PHP <c>inv_scan_lookup</c> twin: barcode-then-SKU match on active items, on-hand across all warehouses as of now.</summary>
public sealed class ErpInventoryScanReadService : IErpInventoryScanReadService
{
    public const string NoMatch = "No item matches that barcode/SKU";
    public const string NotProvisioned = "Inventory tables are not provisioned";

    private static readonly HashSet<string> InTypes = ["opening", "purchase_in", "transfer_in", "return_in"];
    private static readonly HashSet<string> OutTypes = ["sale_out", "transfer_out", "return_out"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpInventoryScanReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpInventoryScanLookupResult> LookupAsync(string? code, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), null, 0m);
        }

        var c = (code ?? string.Empty).Trim();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var tables = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('epc_erp_inv_items','epc_erp_inv_movements')"),
            cancellationToken).ConfigureAwait(false);
        if (tables < 2)
        {
            return new(ErpSimpleWriteResult.Fail("db", NotProvisioned), null, 0m);
        }

        if (c.Length == 0)
        {
            return new(ErpSimpleWriteResult.Fail("invalid", NoMatch), null, 0m);
        }

        IReadOnlyDictionary<string, object?>? row = null;
        var hasBarcode = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_erp_inv_items' AND COLUMN_NAME = 'barcode'"),
            cancellationToken).ConfigureAwait(false) > 0;
        if (hasBarcode)
        {
            row = await FirstRowAsync(connection, ErpDb.Positional("SELECT * FROM `epc_erp_inv_items` WHERE `barcode` = ? AND `active` = 1 LIMIT 1"), cancellationToken, c).ConfigureAwait(false);
        }

        row ??= await FirstRowAsync(connection, ErpDb.Positional("SELECT * FROM `epc_erp_inv_items` WHERE `sku` = ? AND `active` = 1 LIMIT 1"), cancellationToken, c).ConfigureAwait(false);
        if (row is null)
        {
            return new(ErpSimpleWriteResult.Fail("invalid", NoMatch), null, 0m);
        }

        var itemId = Convert.ToInt64(row.GetValueOrDefault("id") ?? 0L, CultureInfo.InvariantCulture);
        var onHand = await OnHandAsync(connection, itemId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), cancellationToken).ConfigureAwait(false);

        var item = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = itemId,
            ["sku"] = Str(row, "sku") ?? string.Empty,
            ["name"] = Str(row, "name") ?? string.Empty,
            ["barcode"] = Str(row, "barcode") ?? string.Empty,
            ["item_type"] = Str(row, "item_type") ?? "standard",
        };

        return new(new ErpSimpleWriteResult(true, "ok", "Item found", itemId, 0), item, onHand);
    }

    private static async Task<decimal> OnHandAsync(DbConnection connection, long itemId, long asOf, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `movement_type`, `qty` FROM `epc_erp_inv_movements` WHERE `active` = 1 AND `movement_date` <= ? AND `item_id` = ? ORDER BY `movement_date` ASC, `id` ASC");
        ErpDb.AddParameters(command, [asOf, itemId]);
        var qty = 0m;
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var type = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var q = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
            if (InTypes.Contains(type) || type == "adjustment")
            {
                qty += q;
            }
            else if (OutTypes.Contains(type))
            {
                qty -= q;
                if (qty < 0)
                {
                    qty = 0m;
                }
            }
        }

        return Math.Round(qty, 3, MidpointRounding.AwayFromZero);
    }

    private static string? Str(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    private static async Task<IReadOnlyDictionary<string, object?>?> FirstRowAsync(DbConnection connection, string sql, CancellationToken ct, params object?[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        var map = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            map[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return map;
    }
}
