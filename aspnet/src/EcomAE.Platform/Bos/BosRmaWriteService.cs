using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>warranty_rma</c> <c>rma_transition</c> / <c>epc_rma_transition</c>
/// and <c>register</c> / <c>epc_warranty_register</c>.
/// RMA create and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosRmaWriteService
{
    Task<ErpSimpleWriteResult> TransitionAsync(
        long rmaId,
        string? status,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RegisterAsync(
        string? siteKey,
        string? warrantyDataJson,
        CancellationToken cancellationToken = default);
}

public sealed class BosRmaWriteService : IBosRmaWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, string[]> Valid = new(StringComparer.Ordinal)
    {
        ["pending"] = ["approved", "rejected", "cancelled"],
        ["approved"] = ["received", "cancelled"],
        ["received"] = ["inspecting"],
        ["inspecting"] = ["repair", "replacement", "refund", "rejected"],
        ["repair"] = ["completed"],
        ["replacement"] = ["completed"],
        ["refund"] = ["completed"],
    };

    private readonly IErpWriteConnectionFactory _connections;

    public BosRmaWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> TransitionAsync(
        long rmaId,
        string? status,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var newStatus = status ?? string.Empty;
        var noteSuffix = string.IsNullOrEmpty(notes) ? string.Empty : "\n" + notes;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var current = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `status` FROM `epc_rma_requests` WHERE `id` = ?"),
                cancellationToken, rmaId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(current))
            {
                return ErpSimpleWriteResult.Fail("invalid", "RMA not found");
            }

            if (!Valid.TryGetValue(current, out var allowed)
                || !allowed.Contains(newStatus, StringComparer.Ordinal))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Invalid transition: " + current + " → " + newStatus);
            }

            object? completedAt = string.Equals(newStatus, "completed", StringComparison.Ordinal)
                ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                : null;
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_rma_requests` SET `status` = ?, `resolution_notes` = CONCAT(IFNULL(`resolution_notes`,''), ?), `completed_at` = COALESCE(?, `completed_at`) WHERE `id` = ?
                    """),
                cancellationToken, newStatus, noteSuffix, completedAt, rmaId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("RMA transitioned", rmaId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "RMA requests table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> RegisterAsync(
        string? siteKey,
        string? warrantyDataJson,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        var data = ParseWarrantyData(warrantyDataJson);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_warranties`
                        (`site_key`, `product_sku`, `product_name`, `serial_number`, `customer_id`, `customer_name`, `order_ref`, `purchase_date`, `warranty_months`, `expiry_date`, `warranty_type`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                key,
                data.ProductSku,
                data.ProductName,
                data.SerialNumber,
                data.CustomerId,
                data.CustomerName,
                data.OrderRef,
                data.PurchaseDate,
                data.WarrantyMonths,
                data.ExpiryDate,
                data.WarrantyType).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Warranty registered", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Warranties table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>(int)</c> on a token (leading digits; trailing junk ignored).</summary>
    public static long PhpIntval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == 0 || (i == 1 && text[0] is '+' or '-'))
        {
            return 0;
        }

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>PHP <c>date('Y-m-d', strtotime($purchaseDate . ' + ' . $months . ' months'))</c>.</summary>
    public static string ExpiryDate(string? purchaseDate, long months)
    {
        var start = DateTime.Now.Date;
        if (!string.IsNullOrWhiteSpace(purchaseDate)
            && (DateTime.TryParse(purchaseDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
                || DateTime.TryParse(purchaseDate, out parsed)))
        {
            start = parsed.Date;
        }

        var add = months > int.MaxValue ? int.MaxValue : months < int.MinValue ? int.MinValue : (int)months;
        return start.AddMonths(add).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// PHP <c>json_decode((string)($_POST['warranty_data'] ?? '{}'), true) ?: array()</c>
    /// then <c>purchase_date ?? today</c>, <c>warranty_months ?? 12</c>, <c>warranty_type ?? standard</c>.
    /// </summary>
    public static (
        string ProductSku,
        string ProductName,
        string SerialNumber,
        long CustomerId,
        string CustomerName,
        string OrderRef,
        string PurchaseDate,
        long WarrantyMonths,
        string ExpiryDate,
        string WarrantyType) ParseWarrantyData(string? json)
    {
        JsonElement root = default;
        var hasObject = false;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    root = doc.RootElement.Clone();
                    hasObject = true;
                }
            }
            catch (JsonException)
            {
                hasObject = false;
            }
        }

        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var purchase = hasObject && root.TryGetProperty("purchase_date", out _)
            ? ReadLooseString(root, "purchase_date")
            : today;
        if (string.IsNullOrWhiteSpace(purchase))
        {
            purchase = today;
        }

        var months = hasObject && root.TryGetProperty("warranty_months", out _)
            ? ReadLong(root, "warranty_months")
            : 12;
        var type = hasObject && root.TryGetProperty("warranty_type", out _)
            ? ReadLooseString(root, "warranty_type")
            : "standard";
        if (string.IsNullOrEmpty(type))
        {
            type = "standard";
        }

        return (
            hasObject ? ReadLooseString(root, "product_sku") : "",
            hasObject ? ReadLooseString(root, "product_name") : "",
            hasObject ? ReadLooseString(root, "serial_number") : "",
            hasObject ? ReadLong(root, "customer_id") : 0,
            hasObject ? ReadLooseString(root, "customer_name") : "",
            hasObject ? ReadLooseString(root, "order_ref") : "",
            purchase,
            months,
            ExpiryDate(purchase, months),
            type);
    }

    private static long ReadLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt64(out var n) => n,
            JsonValueKind.String => PhpIntval(prop.GetString()),
            _ => 0,
        };
    }

    private static string ReadLooseString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return "";
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Number => prop.ToString(),
            _ => "",
        };
    }
}
