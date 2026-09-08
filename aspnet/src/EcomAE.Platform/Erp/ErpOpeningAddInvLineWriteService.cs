using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_opening_add_line</c> / ajax <c>opening_add_inv_line</c> twin.
/// INSERT an inventory line on <c>epc_erp_opening_lines</c> with warehouse/batch/expiry meta.
/// Batch create, COA line, post, and schema stay PHP. Does not CREATE tables.
/// PHP does not require a positive batch id.
/// </summary>
public interface IErpOpeningAddInvLineWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpOpeningAddInvLineWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOpeningAddInvLineWriteRequest(
    long BatchId = 0,
    long ItemId = 0,
    decimal Qty = 0,
    decimal UnitCost = 0,
    long WarehouseId = 0,
    string? BatchNo = null,
    string? ExpiryDate = null);

public sealed class ErpOpeningAddInvLineWriteService : IErpOpeningAddInvLineWriteService
{
    private static readonly JsonSerializerOptions MetaJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpOpeningAddInvLineWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpOpeningAddInvLineWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_opening_lines", "qty", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Opening lines table is not provisioned");
        }

        var meta = EncodeMeta(request.WarehouseId, request.BatchNo, request.ExpiryDate);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_opening_lines` (`batch_id`,`line_type`,`entity_id`,`entity_ref`,`debit`,`credit`,`qty`,`unit_cost`,`meta_json`) VALUES (?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            request.BatchId,
            "inventory",
            request.ItemId,
            "",
            0m,
            0m,
            request.Qty,
            request.UnitCost,
            meta).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Inventory opening line added", id);
    }

    /// <summary>
    /// PHP always sends a <c>meta</c> array, so <c>!empty($line['meta'])</c> is true and json_encode always runs.
    /// </summary>
    public static string EncodeMeta(long warehouseId, string? batchNo, string? expiryDate)
        => JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["warehouse_id"] = warehouseId,
                ["batch_no"] = batchNo ?? "",
                ["expiry_date"] = expiryDate ?? "",
            },
            MetaJson);

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    public static string JsonText(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
    }

    public static long JsonLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
        }

        return 0;
    }

    public static decimal JsonDec(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d))
            {
                return d;
            }

            if (prop.ValueKind == JsonValueKind.String
                && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
        }

        return 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
