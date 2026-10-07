using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string SetOrderStatusPath = "/content/shop/protocol/set_order_status.php";
    public const string SetOrderItemStatusPath = "/content/shop/protocol/set_order_item_status.php";

    /// <summary>
    /// PHP set_order_status.php: <c>initiator=1</c> is a CP manager (session + CSRF), <c>initiator=4</c> the robot with
    /// <c>key</c> = config <c>tech_key</c>; any other initiator ends with an empty body.
    /// </summary>
    public static Task<object> SetOrderStatusProtocolAsync(
        DbConnection connection,
        IShopOrderProtocolService protocol,
        string? initiator,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string? key,
        string techKey,
        string? ordersJson,
        string? status,
        CancellationToken cancellationToken)
    {
        var orders = ProtocolIds(ordersJson);
        var target = ProtocolLong(status);
        if (PhpLooseEquals(initiator, 1))
        {
            return WithCpAdminAsync(
                connection,
                adminSession,
                adminUser,
                postedCsrf,
                () => new CpCodedBody(false, ReturnsForbidden, 501),
                async adminId => (await protocol.SetOrderStatusAsync(connection, orders, target, ShopProtocolActor.Manager(adminId), cancellationToken).ConfigureAwait(false)).ToJson(),
                cancellationToken);
        }

        if (PhpLooseEquals(initiator, 4))
        {
            return !TechKeyMatches(key, techKey)
                ? Task.FromResult<object>(new CpCodedBody(false, "Wrong key", 503))
                : RobotAsync(() => protocol.SetOrderStatusAsync(connection, orders, target, ShopProtocolActor.Robot, cancellationToken));
        }

        return Task.FromResult<object>(new RawHttp(string.Empty, "application/json;charset=utf-8;"));
    }

    /// <summary>
    /// PHP set_order_item_status.php: <c>initiator=1</c> CP manager, <c>initiator=2</c> robot with the tech key;
    /// <c>retun=1</c> with <c>count</c> splits one line before the status change.
    /// </summary>
    public static Task<object> SetOrderItemStatusProtocolAsync(
        DbConnection connection,
        IShopOrderProtocolService protocol,
        string? initiator,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string? key,
        string techKey,
        string? itemsJson,
        string? status,
        string? retun,
        string? count,
        CancellationToken cancellationToken)
    {
        var items = ProtocolIds(itemsJson);
        var target = ProtocolLong(status);
        int? split = ProtocolLong(retun) == 1 ? (int)ProtocolLong(count) : null;
        if (PhpLooseEquals(initiator, 1))
        {
            return WithCpAdminAsync(
                connection,
                adminSession,
                adminUser,
                postedCsrf,
                () => new CpCodedBody(false, ReturnsForbidden, 501),
                async adminId => (await protocol.SetOrderItemStatusAsync(connection, items, target, ShopProtocolActor.Manager(adminId), split, cancellationToken).ConfigureAwait(false)).ToJson(),
                cancellationToken);
        }

        if (PhpLooseEquals(initiator, 2))
        {
            return !TechKeyMatches(key, techKey)
                ? Task.FromResult<object>(new CpCodedBody(false, ReturnsForbidden, 501))
                : RobotAsync(() => protocol.SetOrderItemStatusAsync(connection, items, target, ShopProtocolActor.Robot, split, cancellationToken));
        }

        return Task.FromResult<object>(new RawHttp(string.Empty, "text/html; charset=utf-8"));
    }

    /// <summary>PHP <c>$key != $DP_Config->tech_key</c>, except that an unset tech key never opens the robot path.</summary>
    public static bool TechKeyMatches(string? key, string techKey)
        => techKey.Length > 0 && string.Equals(key ?? string.Empty, techKey, StringComparison.Ordinal);

    private static async Task<object> RobotAsync(Func<Task<ShopProtocolResult>> run)
        => (await run().ConfigureAwait(false)).ToJson();

    /// <summary>PHP <c>$_GET["initiator"] != 1</c> under PHP 8: numeric strings compare as numbers, anything else never matches.</summary>
    public static bool PhpLooseEquals(string? raw, int value)
        => double.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == value;

    /// <summary><c>json_decode($_GET[…], true)</c> id list; anything that is not a JSON array yields no ids.</summary>
    public static IReadOnlyList<long> ProtocolIds(string? json)
    {
        var ids = new List<long>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return ids;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ids;
            }

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                ids.Add(element.ValueKind switch
                {
                    JsonValueKind.Number => element.TryGetInt64(out var n) ? n : (long)element.GetDouble(),
                    JsonValueKind.String => ProtocolLong(element.GetString()),
                    JsonValueKind.True => 1,
                    _ => 0,
                });
            }
        }
        catch (JsonException)
        {
        }

        return ids;
    }

    private static long ProtocolLong(string? raw)
    {
        raw = (raw ?? string.Empty).Trim();
        var end = 0;
        while (end < raw.Length && (char.IsAsciiDigit(raw[end]) || (end == 0 && raw[end] is '-' or '+')))
        {
            end++;
        }

        return long.TryParse(raw[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
