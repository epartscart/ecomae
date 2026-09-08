using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_opl_set_status</c> / ajax <c>opl_set_status</c> twin.
/// UPSERT <c>epc_erp_order_recommendations</c>. Does not call compute,
/// confirm-all, or draft-PO create. Schema ensure stays PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpOplSetStatusWriteService
{
    Task<ErpSimpleWriteResult> SetAsync(
        ErpOplSetStatusWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOplSetStatusWriteRequest(
    long ItemId = 0,
    long WarehouseId = 0,
    string? Status = null,
    bool StatusSpecified = false,
    decimal Roq = 0,
    decimal Value = 0,
    string? Supplier = null);

public sealed class ErpOplSetStatusWriteService : IErpOplSetStatusWriteService
{
    public const string ItemWarehouseRequired = "Item and warehouse required";
    public const string InvalidStatus = "Invalid recommendation status";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "pending", "confirmed", "rejected",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpOplSetStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetAsync(
        ErpOplSetStatusWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ItemId <= 0 || request.WarehouseId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", ItemWarehouseRequired);
        }

        var statusForWrite = request.StatusSpecified
            ? (request.Status ?? "")
            : "pending";
        if (!Allowed.Contains(statusForWrite))
        {
            return ErpSimpleWriteResult.Fail("invalid", InvalidStatus);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var supplier = request.Supplier ?? "";
        var messageStatus = request.StatusSpecified ? (request.Status ?? "") : "";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_order_recommendations", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Recommendation table is not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_order_recommendations` (`item_id`,`warehouse_id`,`roq`,`order_value`,`status`,`supplier`,`time_updated`) VALUES (?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `roq`=VALUES(`roq`), `order_value`=VALUES(`order_value`), `status`=VALUES(`status`), `supplier`=VALUES(`supplier`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            (int)request.ItemId, (int)request.WarehouseId, request.Roq, request.Value, statusForWrite, supplier, now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Recommendation " + messageStatus, request.ItemId);
    }

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

    public static bool JsonHas(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out _))
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
