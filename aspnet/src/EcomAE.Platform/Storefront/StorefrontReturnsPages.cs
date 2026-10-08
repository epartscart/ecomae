using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The customer returns pages PHP renders from <c>content/shop/returns</c>: <c>returns.php</c> (shop/returns/returns_list),
/// <c>return.php</c> with <c>return_messages.php</c> (shop/returns/return), <c>add_return.php</c> (shop/returns/add_return)
/// and the <c>assets/add_return.js.php</c> script. <c>config</c> is the tenant's DP_Config; values PHP echoes raw stay raw.
/// </summary>
public static partial class StorefrontReturnsPages
{
    public const string AddReturnScriptPath = "/content/shop/returns/assets/add_return.js.php";

    /// <summary>PHP <c>returns.php</c>; <c>read</c> is <c>$_GET["read"]</c> (0 keeps returns with unread manager messages).</summary>
    public static async Task<string> ListAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        long userId,
        string? read,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        if (userId == 0)
        {
            return await translator.TextAsync("4582", cancellationToken).ConfigureAwait(false);
        }

        var where = read is not null && ShopPayForOrderService.PhpIntCast(read) == 0
            ? " AND `shop_orders_returns`.`id` IN(SELECT DISTINCT `return_id` FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0 AND `return_id` > 0)"
            : string.Empty;
        var returns = await RowsAsync(
            connection,
            "SELECT `shop_orders_returns`.`id` AS `main_return_id`, `shop_orders_returns_statuses`.`color`, `shop_orders_returns_statuses`.`caption`, "
            + "(SELECT COUNT(*) FROM `shop_orders_messages` WHERE `return_id` = `shop_orders_returns`.`id` AND `read` = 0 AND `is_customer` = 0) AS `count_not_viewed_msg` "
            + "FROM `shop_orders_returns` INNER JOIN `shop_orders_returns_statuses` ON `shop_orders_returns`.`status_id` = `shop_orders_returns_statuses`.`id` WHERE `user_id` = ?" + where,
            [userId],
            cancellationToken).ConfigureAwait(false);

        var dp = Config(config, "domain_path");
        var html = new StringBuilder(await FillAsync(ListHead, translator, null, cancellationToken).ConfigureAwait(false));
        var orderId = string.Empty;
        foreach (var row in returns)
        {
            var returnId = row["main_return_id"] ?? string.Empty;
            var itemIds = await RowsAsync(connection, "SELECT `item_id` FROM `shop_orders_returns_items` WHERE `return_id` = ?", [returnId], cancellationToken).ConfigureAwait(false);
            var total = 0d;
            var filter = new StringBuilder();
            foreach (var item in itemIds)
            {
                var line = await OrderItemJsonAsync(connection, item["item_id"], cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    orderId = string.Empty;
                    filter.Append("false");
                    continue;
                }

                total += StorefrontPhpAjax.PhpFloatCast(line.Value.Price) * StorefrontPhpAjax.PhpFloatCast(line.Value.CountNeed);
                orderId = line.Value.OrderId;
                filter.Append(line.Value.Json);
            }

            var unreadCount = ShopPayForOrderService.PhpIntCast(row["count_not_viewed_msg"]);
            var unread = unreadCount > 0
                ? await FillAsync(ListUnread, translator, new() { ["id"] = returnId, ["count"] = row["count_not_viewed_msg"] ?? "0" }, cancellationToken).ConfigureAwait(false)
                : string.Empty;
            html.Append(await FillAsync(ListRow, translator, new()
            {
                ["color"] = row["color"] ?? string.Empty,
                ["dp"] = dp,
                ["id"] = returnId,
                ["unread"] = unread,
                ["caption"] = row["caption"] ?? string.Empty,
                ["order"] = orderId,
                ["total"] = StorefrontPhpAjax.PhpFloatString(total),
                ["filter"] = filter.ToString(),
            }, cancellationToken).ConfigureAwait(false));
        }

        return html.Append(ListTail).ToString();
    }

    /// <summary>
    /// PHP <c>return.php</c> and <c>return_messages.php</c> for <c>$_GET["return_id"]</c>; marks the return's manager messages read.
    /// Relative photo paths resolve against <paramref name="documentRoot"/> like PHP's working directory.
    /// </summary>
    public static async Task<string> ReturnAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        long userId,
        string? returnId,
        string csrfGuardKey,
        IReadOnlyDictionary<string, string> config,
        string? documentRoot,
        CancellationToken cancellationToken)
    {
        var dp = Config(config, "domain_path");
        if (userId == 0)
        {
            return await FillAsync(ReturnGuest, translator, new() { ["dp"] = dp }, cancellationToken).ConfigureAwait(false);
        }

        var owned = await RowsAsync(connection, "SELECT `id` FROM `shop_orders_returns` WHERE `id` = ? AND `user_id` = ?", [returnId, userId], cancellationToken).ConfigureAwait(false);
        if (owned.Count == 0)
        {
            return await translator.TextAsync("5691", cancellationToken).ConfigureAwait(false);
        }

        var lines = await RowsAsync(
            connection,
            "SELECT `id`, `item_id`, `comment`, `return_success`, (SELECT `caption` FROM `shop_orders_returns_reasons` WHERE `shop_orders_returns_items`.`reason_id` = `id`) AS `reason` FROM `shop_orders_returns_items` WHERE `return_id` = ?",
            [returnId],
            cancellationToken).ConfigureAwait(false);
        var byItem = new Dictionary<string, Dictionary<string, string?>>(StringComparer.Ordinal);
        var itemIds = new List<long>();
        foreach (var line in lines)
        {
            if (line["item_id"] is { } itemId)
            {
                byItem[itemId] = line;
                itemIds.Add(long.Parse(itemId, CultureInfo.InvariantCulture));
            }
        }

        if (ShopPayForOrderService.PhpIntCast(returnId) > 0)
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_orders_messages` SET `read` = 1 WHERE `return_id` = ? AND `is_customer` = 0"), cancellationToken, returnId).ConfigureAwait(false);
        }

        var status = await RowsAsync(
            connection,
            "SELECT (SELECT `caption` FROM `shop_orders_returns_statuses` WHERE `shop_orders_returns`.`status_id` = id) AS `status` FROM `shop_orders_returns` WHERE `id` = ?",
            [returnId],
            cancellationToken).ConfigureAwait(false);
        var shownId = ErpDocumentControlRender.H(returnId);
        var html = new StringBuilder(await FillAsync(ReturnHead, translator, new() { ["id"] = shownId }, cancellationToken).ConfigureAwait(false))
            .Append(status.Count > 0 ? status[0]["status"] : string.Empty)
            .Append(await FillAsync(ReturnTableHead, translator, null, cancellationToken).ConfigureAwait(false));

        var items = itemIds.Count == 0
            ? []
            : await RowsAsync(
                connection,
                "SELECT `id`, `count_need`, `price`, `price`*`count_need` AS `price_sum`, `t2_name` AS `product_name`, `t2_article` AS `article`, `t2_manufacturer` AS `manufacturer` FROM `shop_orders_items` WHERE `id` IN ("
                + string.Join(",", itemIds.Select(id => id.ToString(CultureInfo.InvariantCulture))) + ")",
                [],
                cancellationToken).ConfigureAwait(false);
        var total = 0d;
        foreach (var item in items)
        {
            var line = byItem[item["id"]!];
            string decision;
            switch (line["return_success"])
            {
                case "1":
                    decision = await translator.TextAsync("3804", cancellationToken).ConfigureAwait(false);
                    total += StorefrontPhpAjax.PhpFloatCast(item["price_sum"]);
                    break;
                case "0":
                    decision = await translator.TextAsync("3805", cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    decision = await translator.TextAsync("3806", cancellationToken).ConfigureAwait(false);
                    break;
            }

            var images = new StringBuilder();
            foreach (var image in await RowsAsync(connection, "SELECT `image` FROM `shop_orders_returns_items_images` WHERE `return_item_id` = ?", [line["id"]], cancellationToken).ConfigureAwait(false))
            {
                images.Append(ReturnImage.Replace("{{base64}}", Convert.ToBase64String(await ReadImageAsync(image["image"], documentRoot, cancellationToken).ConfigureAwait(false)), StringComparison.Ordinal));
            }

            html.Append(await FillAsync(ReturnRow, translator, new()
            {
                ["manufacturer"] = item["manufacturer"] ?? string.Empty,
                ["article"] = item["article"] ?? string.Empty,
                ["name"] = item["product_name"] ?? string.Empty,
                ["price"] = NumberFormat(item["price"]),
                ["count"] = item["count_need"] ?? string.Empty,
                ["sum"] = NumberFormat(item["price_sum"]),
                ["images"] = images.ToString(),
                ["reason"] = " " + line["reason"],
                ["comment"] = line["comment"] ?? string.Empty,
                ["status"] = decision,
            }, cancellationToken).ConfigureAwait(false));
        }

        html.Append(await FillAsync(ReturnTail, translator, new() { ["total"] = StorefrontPhpAjax.PhpFloatString(total), ["dp"] = dp }, cancellationToken).ConfigureAwait(false));
        return html.Append(await FillAsync(ReturnMessages, translator, new() { ["id"] = shownId, ["csrf"] = csrfGuardKey }, cancellationToken).ConfigureAwait(false)).ToString();
    }

    /// <summary>
    /// PHP <c>add_return.php</c> for <c>$_GET["items"]</c> (a JSON list of order item ids, as my_order.php sends). <paramref name="formKey"/>
    /// fills the forms' <c>tech_key</c> field (<see cref="FormKey"/>).
    /// </summary>
    public static async Task<string> AddReturnAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        long userId,
        string? itemsJson,
        string csrfGuardKey,
        string formKey,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        if (ShopPayForOrderService.PhpNumber(Config(config, "return_available")) != 1m)
        {
            return await translator.TextAsync("5684", cancellationToken).ConfigureAwait(false);
        }

        var ids = ItemIds(itemsJson);
        var noteKey = Config(config, "retention_percentage_text");
        var note = noteKey.Length > 0 && ShopPayForOrderService.PhpNumber(noteKey) != 0m
            ? await FillAsync(AddNote, translator, new() { ["note"] = await translator.TextAsync(noteKey, cancellationToken).ConfigureAwait(false) }, cancellationToken).ConfigureAwait(false)
            : string.Empty;
        var html = new StringBuilder(await FillAsync(AddHead, translator, new() { ["note"] = note }, cancellationToken).ConfigureAwait(false));

        var items = ids.Count == 0
            ? []
            : await RowsAsync(
                connection,
                "SELECT `id`, `count_need`, `price`, `price`*`count_need` AS `price_sum`, `t2_name` AS `product_name`, `t2_article` AS `article`, `t2_manufacturer` AS `manufacturer`, `t2_office_id`, "
                + "(SELECT `user_id` FROM `shop_orders` WHERE `shop_orders`.`id` = `shop_orders_items`.`order_id`) AS `customer_id` FROM `shop_orders_items` WHERE `id` IN ("
                + string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)))
                + ") AND `status` IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `check_for_return` = 1) AND `id` NOT IN (SELECT `item_id` FROM `shop_orders_returns_items`)",
                [],
                cancellationToken).ConfigureAwait(false);
        var reasons = items.Count == 0
            ? []
            : await RowsAsync(connection, "SELECT `id`, `caption` FROM `shop_orders_returns_reasons`", [], cancellationToken).ConfigureAwait(false);
        var options = string.Concat(reasons.Select(r => AddReason.Replace("{{id}}", r["id"], StringComparison.Ordinal).Replace("{{caption}}", r["caption"], StringComparison.Ordinal)));

        var access = false;
        var total = 0d;
        var officeId = "0";
        foreach (var item in items)
        {
            if (item["customer_id"] is not { } customer || ShopPayForOrderService.PhpNumber(customer) != userId)
            {
                access = false;
                break;
            }

            access = true;
            officeId = item["t2_office_id"] ?? string.Empty;
            total += StorefrontPhpAjax.PhpFloatCast(item["price_sum"]);
            html.Append(await FillAsync(AddRow, translator, new()
            {
                ["manufacturer"] = item["manufacturer"] ?? string.Empty,
                ["article"] = item["article"] ?? string.Empty,
                ["name"] = item["product_name"] ?? string.Empty,
                ["id"] = item["id"] ?? string.Empty,
                ["price_raw"] = item["price"] ?? string.Empty,
                ["price"] = NumberFormat(item["price"]),
                ["count"] = item["count_need"] ?? string.Empty,
                ["sum"] = NumberFormat(item["price_sum"]),
                ["csrf"] = csrfGuardKey,
                ["tech"] = formKey,
                ["reasons"] = options,
            }, cancellationToken).ConfigureAwait(false));
        }

        html.Append(AddTableTail);
        if (!access)
        {
            return html.Append(await translator.TextAsync("5685", cancellationToken).ConfigureAwait(false)).ToString();
        }

        var retention = Config(config, "retention_percentage");
        var retentionPercent = StorefrontPhpAjax.PhpFloatCast(retention);
        if (retentionPercent > 0)
        {
            total -= total / 100 * retentionPercent;
        }

        return html.Append(await FillAsync(AddFoot, translator, new()
        {
            ["total"] = StorefrontPhpAjax.PhpFloatString(total),
            ["user"] = userId.ToString(CultureInfo.InvariantCulture),
            ["csrf"] = csrfGuardKey,
            ["tech"] = formKey,
            ["office"] = officeId,
            ["dp"] = Config(config, "domain_path"),
            ["retention"] = retentionPercent > 0 ? "\"" + retention + "\"" : "0",
        }, cancellationToken).ConfigureAwait(false)).ToString();
    }

    /// <summary>PHP <c>assets/add_return.js.php</c>; <paramref name="langHrefSlashAfter"/> is <c>$multilang_params['lang_href_slash_after']</c>.</summary>
    public static Task<string> AddReturnScriptAsync(StorefrontPhpTranslator translator, string langHrefSlashAfter, CancellationToken cancellationToken)
        => FillAsync(AddScript, translator, new() { ["lang"] = langHrefSlashAfter }, cancellationToken);

    /// <summary>
    /// The value the add-return forms post as <c>tech_key</c>. PHP prints DP_Config <c>tech_key</c> itself into the customer's page;
    /// ASP.NET prints a key derived from it and the session's CSRF key, which the returns endpoint maps back to <c>tech_key</c>.
    /// </summary>
    public static string FormKey(string techKey, string csrfGuardKey)
        => techKey.Length == 0
            ? string.Empty
            : Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(techKey), Encoding.UTF8.GetBytes("shop_returns:" + csrfGuardKey)));

    /// <summary>
    /// <c>lang_href_slash_after</c> for the standalone script: the language segment of the page that included it
    /// (<c>/en/shop/returns/add_return</c> gives <c>en/</c>), else none, which the unprefixed routes serve too.
    /// </summary>
    public static string LangHrefSlashAfter(string? referer)
    {
        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var match = LangSegment().Match(uri.AbsolutePath);
        return match.Success ? match.Groups[1].Value + "/" : string.Empty;
    }

    /// <summary>PHP <c>$user_session["csrf_guard_key"]</c>: the session row of the <c>session</c> cookie and user.</summary>
    public static async Task<string> SessionCsrfKeyAsync(DbConnection connection, string? sessionToken, long userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionToken) || userId <= 0)
        {
            return string.Empty;
        }

        var rows = await RowsAsync(connection, "SELECT `csrf_guard_key` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1", [sessionToken, userId], cancellationToken).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0]["csrf_guard_key"] ?? string.Empty : string.Empty;
    }

    /// <summary>PHP <c>number_format($n, 2, '.', ' ')</c>.</summary>
    public static string NumberFormat(string? raw)
    {
        var value = ShopPayForOrderService.PhpNumber(raw) ?? (decimal)StorefrontPhpAjax.PhpFloatCast(raw);
        return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(',', ' ');
    }

    /// <summary>
    /// <c>json_decode($_GET["items"])</c> as order item ids. PHP implodes the decoded values straight into SQL; only integers
    /// (JSON numbers or digit strings) are kept here.
    /// </summary>
    public static IReadOnlyList<long> ItemIds(string? itemsJson)
    {
        var ids = new List<long>();
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(itemsJson) ? "null" : itemsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ids;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var number))
                {
                    ids.Add(number);
                }
                else if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var text))
                {
                    ids.Add(text);
                }
            }
        }
        catch (JsonException)
        {
        }

        return ids;
    }

    /// <summary>The order item of a return line and its <c>json_encode</c> as PDO fetches it (FETCH_BOTH, native ints).</summary>
    private static async Task<(string Json, string? Price, string? CountNeed, string OrderId)?> OrderItemJsonAsync(DbConnection connection, string? itemId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT * FROM `shop_orders_items` WHERE `id` = ?");
        ErpDb.AddParameters(command, itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var json = new StringBuilder("{");
        string? price = null, countNeed = null, orderId = null;
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var value = PdoJson(reader, i);
            json.Append(i == 0 ? string.Empty : ",").Append(OAuthStart.PhpJsonString(reader.GetName(i))).Append(':').Append(value)
                .Append(",\"").Append(i.ToString(CultureInfo.InvariantCulture)).Append("\":").Append(value);
            var text = reader.IsDBNull(i) ? null : ErpDocumentControlRender.PdoString(reader, i);
            switch (reader.GetName(i))
            {
                case "price":
                    price = text;
                    break;
                case "count_need":
                    countNeed = text;
                    break;
                case "order_id":
                    orderId = text;
                    break;
            }
        }

        return (json.Append('}').ToString(), price, countNeed, orderId ?? string.Empty);
    }

    /// <summary>A column as PHP 8.1+ PDO MySQL returns it, JSON-encoded: integers and floats native, DECIMAL and text as strings.</summary>
    private static string PdoJson(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return "null";
        }

        return reader.GetValue(ordinal) switch
        {
            bool b => b ? "1" : "0",
            sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)!,
            float f => JsonFloat(f),
            double d => JsonFloat(d),
            DateTime t => OAuthStart.PhpJsonString(t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            byte[] bytes => OAuthStart.PhpJsonString(Encoding.UTF8.GetString(bytes)),
            _ => OAuthStart.PhpJsonString(ErpDocumentControlRender.PdoString(reader, ordinal)),
        };
    }

    private static string JsonFloat(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.', StringComparison.Ordinal) || text.Contains('E', StringComparison.Ordinal) ? text : text + ".0";
    }

    private static async Task<byte[]> ReadImageAsync(string? path, string? documentRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
        {
            return [];
        }

        var full = Path.IsPathRooted(path) || string.IsNullOrEmpty(documentRoot) ? path : Path.Combine(documentRoot, path);
        try
        {
            return File.Exists(full) ? await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false) : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string Config(IReadOnlyDictionary<string, string> config, string key)
        => config.TryGetValue(key, out var value) ? value : string.Empty;

    private static async Task<List<Dictionary<string, string?>>> RowsAsync(DbConnection connection, string sql, object?[] parameters, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<Dictionary<string, string?>>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : ErpDocumentControlRender.PdoString(reader, i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task<string> FillAsync(string template, StorefrontPhpTranslator translator, Dictionary<string, string>? values, CancellationToken cancellationToken)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Token().Matches(template))
        {
            var key = match.Groups[1].Value;
            if (key.StartsWith("t:", StringComparison.Ordinal) && !texts.ContainsKey(key))
            {
                texts[key] = await translator.TextAsync(key[2..], cancellationToken).ConfigureAwait(false);
            }
        }

        return Token().Replace(template, match => texts.TryGetValue(match.Groups[1].Value, out var text) ? text : values![match.Groups[1].Value]);
    }

    [GeneratedRegex(@"\{\{([a-z0-9_:]+)\}\}")]
    private static partial Regex Token();

    [GeneratedRegex("^/([a-z]{2})/")]
    private static partial Regex LangSegment();
}
