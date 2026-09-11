using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>promotions_engine</c> <c>record_usage</c> / <c>epc_promo_record_usage</c>
/// and <c>create</c> / <c>epc_promo_create</c>.
/// Apply and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found checks.
/// </summary>
public interface IBosPromoWriteService
{
    Task<ErpSimpleWriteResult> RecordUsageAsync(
        long promotionId,
        string? siteKey,
        long customerId,
        string? orderRef,
        decimal discount,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateAsync(
        string? siteKey,
        string? promoDataJson,
        CancellationToken cancellationToken = default);
}

public sealed class BosPromoWriteService : IBosPromoWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosPromoWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    public async Task<ErpSimpleWriteResult> RecordUsageAsync(
        long promotionId,
        string? siteKey,
        long customerId,
        string? orderRef,
        decimal discount,
        CancellationToken cancellationToken = default)
    {
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
                    INSERT INTO `epc_promotion_usage` (`promotion_id`,`site_key`,`customer_id`,`order_ref`,`discount_amount`) VALUES (?,?,?,?,?)
                    """),
                cancellationToken, promotionId, PhpBosSiteKey(siteKey), customerId, orderRef ?? "", discount).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_promotions` SET `used_count`=`used_count`+1 WHERE `id`=?"),
                cancellationToken, promotionId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Promotion usage recorded", promotionId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Promotions table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        string? siteKey,
        string? promoDataJson,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        var promo = ParsePromoData(promoDataJson);
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
                    INSERT INTO `epc_promotions`
                        (`site_key`,`name`,`code`,`type`,`value`,`min_order`,`max_discount`,`start_date`,`end_date`,`usage_limit`,`per_customer`,`stackable`,`priority`,`conditions`,`applies_to`,`customer_segments`,`created_by`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                key,
                promo.Name,
                promo.Code,
                promo.Type,
                promo.Value,
                promo.MinOrder,
                promo.MaxDiscount,
                promo.StartDate,
                promo.EndDate,
                promo.UsageLimit,
                promo.PerCustomer,
                promo.Stackable,
                promo.Priority,
                promo.ConditionsJson,
                promo.AppliesToJson,
                promo.CustomerSegmentsJson,
                promo.CreatedBy).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Promotion created", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Promotions table is missing — schema-ensure stays Classic.");
        }
    }

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

    /// <summary>PHP <c>(float)</c> on a token (leading numeric / optional exponent; trailing junk ignored).</summary>
    public static decimal PhpFloat(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        var text = raw.TrimStart();
        if (text.Length == 0)
        {
            return 0;
        }

        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        var sawDigit = false;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            sawDigit = true;
            i++;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                sawDigit = true;
                i++;
            }
        }

        if (!sawDigit)
        {
            return 0;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var exp = i + 1;
            if (exp < text.Length && text[exp] is '+' or '-')
            {
                exp++;
            }

            var expDigits = exp;
            while (expDigits < text.Length && char.IsDigit(text[expDigits]))
            {
                expDigits++;
            }

            if (expDigits > exp)
            {
                i = expDigits;
            }
        }

        return decimal.TryParse(text[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>
    /// PHP <c>json_decode((string)($_POST['promo_data'] ?? '{}'), true) ?: array()</c>
    /// then <c>code</c> via <c>strtoupper</c>, <c>type ?? percentage</c>,
    /// <c>start_date ?? today</c>, <c>end_date ?? today+30d</c>.
    /// </summary>
    public static (
        string Name,
        string Code,
        string Type,
        decimal Value,
        decimal MinOrder,
        decimal MaxDiscount,
        string StartDate,
        string EndDate,
        long UsageLimit,
        long PerCustomer,
        long Stackable,
        long Priority,
        string ConditionsJson,
        string AppliesToJson,
        string CustomerSegmentsJson,
        long CreatedBy) ParsePromoData(string? json)
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
        var plus30 = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var type = hasObject && root.TryGetProperty("type", out _) ? ReadLooseString(root, "type") : "percentage";
        if (type.Length == 0 && !(hasObject && root.TryGetProperty("type", out _)))
        {
            type = "percentage";
        }

        var start = hasObject && root.TryGetProperty("start_date", out _)
            ? ReadLooseString(root, "start_date")
            : today;
        if (string.IsNullOrWhiteSpace(start))
        {
            start = today;
        }

        var end = hasObject && root.TryGetProperty("end_date", out _)
            ? ReadLooseString(root, "end_date")
            : plus30;
        if (string.IsNullOrWhiteSpace(end))
        {
            end = plus30;
        }

        return (
            hasObject ? ReadLooseString(root, "name") : "",
            (hasObject ? ReadLooseString(root, "code") : "").ToUpperInvariant(),
            string.IsNullOrEmpty(type) ? "percentage" : type,
            hasObject ? ReadNumber(root, "value") : 0,
            hasObject ? ReadNumber(root, "min_order") : 0,
            hasObject ? ReadNumber(root, "max_discount") : 0,
            start,
            end,
            hasObject ? ReadLong(root, "usage_limit") : 0,
            hasObject ? ReadLong(root, "per_customer") : 0,
            hasObject ? ReadLong(root, "stackable") : 0,
            hasObject ? ReadLong(root, "priority") : 0,
            SerializeJsonField(hasObject ? ReadRaw(root, "conditions") : null),
            SerializeJsonField(hasObject ? ReadRaw(root, "applies_to") : null),
            SerializeJsonField(hasObject ? ReadRaw(root, "customer_segments") : null),
            hasObject ? ReadLong(root, "created_by") : 0);
    }

    public static string SerializeJsonField(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "[]";
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json));
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (!doc.RootElement.EnumerateObject().Any())
                {
                    return "[]";
                }

                return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json));
            }

            return "[]";
        }
        catch (JsonException)
        {
            return "[]";
        }
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
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0,
        };
    }

    private static decimal ReadNumber(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetDecimal(out var n) => n,
            JsonValueKind.String => PhpFloat(prop.GetString()),
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

    private static string? ReadRaw(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop)
            || prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return prop.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? prop.GetRawText()
            : prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
    }
}
