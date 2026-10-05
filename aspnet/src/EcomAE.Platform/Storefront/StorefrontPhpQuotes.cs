using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    private static readonly JsonSerializerOptions PhpJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public sealed record QuoteBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("code")] string? Code = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("message")] string? Message = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("quote_id")] long? QuoteId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("login_url")] string? LoginUrl = null);

    public static Task<object> AddToQuoteAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? productObjects,
        string? techKey,
        CancellationToken cancellationToken)
        => AddLinesAsync(connection, session, userCookie, productObjects, techKey, cancellationToken);

    public static async Task<object> AddManualQuoteAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? manufacturer,
        string? article,
        string? articleShow,
        string? name,
        string? countNeed,
        CancellationToken cancellationToken)
    {
        var user = await QuoteUserAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        if (user.Stop is not null)
        {
            return user.Stop;
        }

        manufacturer = (manufacturer ?? string.Empty).Trim();
        article = (article ?? string.Empty).Trim();
        articleShow = (articleShow ?? string.Empty).Trim();
        name = (name ?? string.Empty).Trim();
        if (manufacturer.Length == 0 || article.Length == 0)
        {
            return new QuoteBody(false, Message: QuoteBrandRequired);
        }

        var articleNorm = ArticleNorm(article);
        manufacturer = PhpHtml(manufacturer.ToUpper(CultureInfo.InvariantCulture));
        if (articleShow.Length == 0)
        {
            articleShow = articleNorm;
        }

        articleShow = PhpHtml(articleShow);
        if (name.Length == 0)
        {
            name = "Quote request — " + System.Net.WebUtility.HtmlDecode(manufacturer) + " " + System.Net.WebUtility.HtmlDecode(articleShow);
        }

        name = PhpHtml(name);
        var count = ManualCount(countNeed);
        var product = new JsonObject
        {
            ["product_type"] = 2,
            ["manufacturer"] = manufacturer,
            ["article"] = articleNorm,
            ["article_show"] = articleShow,
            ["name"] = name,
            ["exist"] = 0,
            ["price"] = 0,
            ["time_to_exe"] = 0,
            ["time_to_exe_guaranteed"] = 0,
            ["storage"] = string.Empty,
            ["min_order"] = 1,
            ["probability"] = 0,
            ["office_id"] = 0,
            ["storage_id"] = 0,
            ["price_purchase"] = 0,
            ["markup"] = 0,
            ["json_params"] = string.Empty,
            ["check_hash"] = "manual",
            ["epc_manual_quote"] = 1,
            ["count_need"] = count
        };
        try
        {
            var quoteId = await DraftQuoteAsync(connection, user.UserId, cancellationToken).ConfigureAwait(false);
            await InsertQuoteItemAsync(connection, quoteId, product.ToJsonString(PhpJson), count, cancellationToken).ConfigureAwait(false);
            return new QuoteBody(true, QuoteId: quoteId);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new QuoteBody(false, Message: QuotesMissing);
        }
    }

    public static async Task<object> SubmitQuoteAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? quoteIdText,
        string? customerNote,
        CancellationToken cancellationToken)
    {
        var user = await QuoteUserAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        if (user.Stop is not null)
        {
            return user.Stop;
        }

        if (!int.TryParse(quoteIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quoteId) || quoteId <= 0)
        {
            return new QuoteBody(false, Message: QuoteInvalid);
        }

        try
        {
            var status = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `status` FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                quoteId,
                user.UserId).ConfigureAwait(false);
            if (!string.Equals(status, "draft", StringComparison.Ordinal))
            {
                return new QuoteBody(false, Message: QuoteNotDraft);
            }

            var lines = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ?"),
                cancellationToken,
                quoteId).ConfigureAwait(false);
            if (lines < 1)
            {
                return new QuoteBody(false, Message: QuoteNeedLine);
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_quote_requests` SET `status` = 'submitted', `time_submitted` = ?, `time_updated` = ?, `customer_note` = ? WHERE `id` = ? AND `user_id` = ?"),
                cancellationToken,
                now,
                now,
                (customerNote ?? string.Empty).Trim(),
                quoteId,
                user.UserId).ConfigureAwait(false);
            return new QuoteBody(true);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new QuoteBody(false, Message: QuotesMissing);
        }
    }

    public static async Task<object> AcceptQuoteAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? quoteIdText,
        string? techKey,
        CancellationToken cancellationToken)
    {
        var user = await QuoteUserAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        if (user.Stop is not null)
        {
            return user.Stop;
        }

        if (!int.TryParse(quoteIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quoteId) || quoteId <= 0)
        {
            return new QuoteBody(false, Message: QuoteInvalid);
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var status = await ErpDb.StringAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `status` FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                quoteId,
                user.UserId).ConfigureAwait(false);
            if (!string.Equals(status, "quoted", StringComparison.Ordinal))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new QuoteBody(false, Message: QuoteNotAcceptable);
            }

            var items = await ReadAcceptItemsAsync(connection, tx, quoteId, cancellationToken).ConfigureAwait(false);
            if (items.Count < 1)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new QuoteBody(false, Message: QuoteNoLines);
            }

            foreach (var item in items)
            {
                if (EffectivePrice(item) <= 0)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new QuoteBody(false, Message: QuoteIncomplete);
                }
            }

            foreach (var item in items)
            {
                if (!TryPrepareCartLine(item, techKey, out var line))
                {
                    throw new InvalidOperationException("bad_line");
                }

                if (!line.SkipAlready)
                {
                    var already = await ErpDb.LongAsync(
                        connection,
                        tx,
                        ErpDb.Positional("""
                            SELECT COUNT(*) FROM `shop_carts` WHERE
                            `product_type` = 2 AND `user_id` = ? AND `session_id` = ? AND
                            `t2_manufacturer` = ? AND `t2_article` = ? AND `t2_exist` = ? AND
                            `t2_time_to_exe` = ? AND `t2_time_to_exe_guaranteed` = ? AND
                            `t2_probability` = ? AND `t2_office_id` = ? AND `t2_storage_id` = ? AND
                            CAST(`price` AS DECIMAL(12,4)) = CAST(? AS DECIMAL(12,4))
                            """),
                        cancellationToken,
                        user.UserId,
                        0,
                        line.Manufacturer,
                        line.Article,
                        line.Exist,
                        line.TimeToExe,
                        line.TimeGuaranteed,
                        line.Probability,
                        line.OfficeId,
                        line.StorageId,
                        line.Price).ConfigureAwait(false);
                    if (already > 0)
                    {
                        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return new QuoteBody(false, "already", QuoteAlreadyInCart);
                    }
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("""
                        INSERT INTO `shop_carts` (
                            `product_type`, `price`, `count_need`, `time`, `user_id`, `session_id`,
                            `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                            `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                            `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
                            `t2_product_json`, `t2_json_params`)
                        VALUES (2,?,?,?,?,0,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                        """),
                    cancellationToken,
                    line.Price,
                    line.CountNeed,
                    now,
                    user.UserId,
                    line.Manufacturer,
                    line.Article,
                    line.ArticleShow,
                    line.Name,
                    line.Exist,
                    line.TimeToExe,
                    line.TimeGuaranteed,
                    line.Storage,
                    line.MinOrder,
                    line.Probability,
                    line.Markup,
                    line.PricePurchase,
                    line.OfficeId,
                    line.StorageId,
                    line.ProductJson,
                    line.JsonParams).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `shop_quote_requests` SET `status` = 'accepted', `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                quoteId).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new QuoteBody(true);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            await RollbackAsync(tx, cancellationToken).ConfigureAwait(false);
            return new QuoteBody(false, Message: ex.Message.Contains("shop_carts", StringComparison.OrdinalIgnoreCase)
                ? QuoteAcceptFailed
                : QuotesMissing);
        }
        catch (Exception)
        {
            await RollbackAsync(tx, cancellationToken).ConfigureAwait(false);
            return new QuoteBody(false, Message: QuoteAcceptFailed);
        }
    }

    private static async Task<object> AddLinesAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? productObjects,
        string? techKey,
        CancellationToken cancellationToken)
    {
        var user = await QuoteUserAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        if (user.Stop is not null)
        {
            return user.Stop;
        }

        JsonDocument document;
        try
        {
            document = string.IsNullOrWhiteSpace(productObjects) ? null! : JsonDocument.Parse(productObjects);
        }
        catch (JsonException)
        {
            document = null!;
        }

        if (document is null || document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return new QuoteBody(false, "data", QuoteInvalidPayload);
        }

        var lines = new List<(string Json, int Count)>();
        foreach (var product in document.RootElement.EnumerateArray())
        {
            if (product.ValueKind != JsonValueKind.Object || PhpInt(product, "product_type") != 2)
            {
                continue;
            }

            var hash = QuoteCheckHash(product, PhpScalar(product, "price"), null, null, techKey);
            if (!string.Equals(hash, PhpScalar(product, "check_hash").Trim(), StringComparison.Ordinal))
            {
                return new QuoteBody(false, "35", QuoteHashFailed);
            }

            lines.Add((product.GetRawText(), CountNeed(product)));
        }

        if (lines.Count < 1)
        {
            return new QuoteBody(false, Message: QuoteType2Only);
        }

        try
        {
            var quoteId = await DraftQuoteAsync(connection, user.UserId, cancellationToken).ConfigureAwait(false);
            foreach (var line in lines)
            {
                await InsertQuoteItemAsync(connection, quoteId, line.Json, line.Count, cancellationToken).ConfigureAwait(false);
            }

            return new QuoteBody(true, QuoteId: quoteId);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new QuoteBody(false, Message: QuotesMissing);
        }
    }

    private static async Task<QuoteUser> QuoteUserAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        CancellationToken cancellationToken)
    {
        try
        {
            var knownUser = int.TryParse(userCookie, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `user_id` = ?"),
                cancellationToken,
                session,
                knownUser ? parsed : userCookie).ConfigureAwait(false);
            if (count != 1 || !knownUser)
            {
                return count > 1
                    ? new QuoteUser(0, new RawHttp(string.Empty, "text/html; charset=utf-8"))
                    : new QuoteUser(0, new QuoteBody(false, "auth", GuestAuthMessage, null, GuestLoginUrl));
            }

            return new QuoteUser(parsed, null);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new QuoteUser(0, new QuoteBody(false, Message: SessionsMissing));
        }
    }

    private static async Task<long> DraftQuoteAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_quote_requests` WHERE `user_id` = ? AND `status` = 'draft' ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            userId).ConfigureAwait(false);
        if (existing > 0)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_quote_requests` (`user_id`, `session_id`, `status`, `time_created`, `time_updated`) VALUES (?, 0, 'draft', ?, ?)"),
            cancellationToken,
            userId,
            now,
            now).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertQuoteItemAsync(
        DbConnection connection,
        long quoteId,
        string productJson,
        int countNeed,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`) VALUES (?, 2, ?, ?)"),
            cancellationToken,
            quoteId,
            productJson,
            countNeed).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            quoteId).ConfigureAwait(false);
    }

    private static async Task<List<AcceptItem>> ReadAcceptItemsAsync(
        DbConnection connection,
        DbTransaction transaction,
        int quoteId,
        CancellationToken cancellationToken)
    {
        var items = new List<AcceptItem>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("""
            SELECT `product_object_json`, `count_need`, `quoted_price`, `quoted_time_to_exe`,
                   `offer_alternative`, `alt_manufacturer`, `alt_article`, `alt_article_show`, `alt_name`,
                   `alt_count_need`, `alt_quoted_price`, `alt_storage_id`
            FROM `shop_quote_items` WHERE `quote_id` = ? ORDER BY `id` ASC
            """);
        ErpDb.AddParameters(command, quoteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var altManufacturer = reader.IsDBNull(5) ? string.Empty : Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty;
            var altArticle = reader.IsDBNull(6) ? string.Empty : Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty;
            var offer = !reader.IsDBNull(4) && Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) == 1;
            items.Add(new AcceptItem(
                reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.IsDBNull(3) ? null : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture),
                offer && altManufacturer.Length > 0 && altArticle.Length > 0,
                altManufacturer,
                altArticle,
                reader.IsDBNull(7) ? string.Empty : Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(8) ? string.Empty : Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(9) ? 0 : Convert.ToInt32(reader.GetValue(9), CultureInfo.InvariantCulture),
                reader.IsDBNull(10) ? null : Convert.ToString(reader.GetValue(10), CultureInfo.InvariantCulture),
                reader.IsDBNull(11) ? 0 : Convert.ToInt32(reader.GetValue(11), CultureInfo.InvariantCulture)));
        }

        return items;
    }

    private static decimal EffectivePrice(AcceptItem item)
        => decimal.TryParse(item.UseAlt ? item.AltPrice : item.QuotedPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            ? price
            : 0m;

    private static bool TryPrepareCartLine(AcceptItem item, string? techKey, out CartLine line)
    {
        line = null!;
        JsonObject? node;
        try
        {
            node = JsonNode.Parse(item.ProductJson) as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }

        if (node is null || PhpInt(JsonDocument.Parse(node.ToJsonString(PhpJson)).RootElement, "product_type") != 2)
        {
            return false;
        }

        var requestedManufacturer = NodeText(node, "manufacturer");
        var requestedArticle = NodeText(node, "article_show");
        if (requestedArticle.Length == 0)
        {
            requestedArticle = NodeText(node, "article");
        }

        var price = EffectivePrice(item);
        var count = Math.Max(1, item.UseAlt ? (item.AltCount == 0 ? 1 : item.AltCount) : item.CountNeed);
        if (item.UseAlt)
        {
            var altManufacturer = item.AltManufacturer.Trim().ToUpper(CultureInfo.InvariantCulture);
            var altShow = item.AltArticleShow.Length > 0 ? item.AltArticleShow.Trim() : item.AltArticle.Trim();
            var altArticle = ArticleNorm(altShow);
            var altName = item.AltName.Trim();
            if (altName.Length == 0)
            {
                altName = altManufacturer + " " + altShow + " (alternative)";
            }

            node["manufacturer"] = altManufacturer;
            node["article"] = altArticle;
            node["article_show"] = altShow;
            node["name"] = altName;
            node["epc_quote_alternative"] = 1;
            node["epc_requested_manufacturer"] = requestedManufacturer;
            node["epc_requested_article"] = requestedArticle;
            if (item.AltStorageId > 0)
            {
                node["storage_id"] = item.AltStorageId;
                node["storage"] = "Storage #" + item.AltStorageId.ToString(CultureInfo.InvariantCulture);
                node["epc_alt_storage_id"] = item.AltStorageId;
            }
        }

        node["price"] = price;
        if (!string.IsNullOrEmpty(item.QuotedTime))
        {
            var time = (int)decimal.Truncate(decimal.Parse(item.QuotedTime, CultureInfo.InvariantCulture));
            node["time_to_exe"] = time;
            node["time_to_exe_guaranteed"] = time;
        }

        using var prepared = JsonDocument.Parse(node.ToJsonString(PhpJson));
        var hash = QuoteCheckHash(prepared.RootElement, PhpNumber(price), null, null, techKey);
        node["check_hash"] = hash;
        node["count_need"] = count;
        var jsonParams = NodeText(node, "json_params");
        var skipAlready = false;
        try
        {
            if (!string.IsNullOrEmpty(jsonParams))
            {
                using var flags = JsonDocument.Parse(jsonParams);
                skipAlready = flags.RootElement.ValueKind == JsonValueKind.Object && PhpInt(flags.RootElement, "used") == 1;
            }
        }
        catch (JsonException)
        {
            skipAlready = false;
        }

        using var finalDocument = JsonDocument.Parse(node.ToJsonString(PhpJson));
        var final = finalDocument.RootElement;
        line = new CartLine(
            NodeText(node, "manufacturer"),
            NodeText(node, "article"),
            NodeText(node, "article_show"),
            NodeText(node, "name"),
            PhpScalar(final, "exist"),
            PhpScalar(final, "time_to_exe"),
            PhpScalar(final, "time_to_exe_guaranteed"),
            NodeText(node, "storage"),
            PhpScalar(final, "min_order"),
            PhpScalar(final, "probability"),
            PhpScalar(final, "markup"),
            PhpScalar(final, "price_purchase"),
            PhpScalar(final, "office_id"),
            PhpScalar(final, "storage_id"),
            price,
            count,
            node.ToJsonString(PhpJson),
            jsonParams,
            skipAlready);
        return true;
    }

    private static string NodeText(JsonObject node, string name)
    {
        if (node[name] is not JsonValue value)
        {
            return string.Empty;
        }

        return value.TryGetValue<string>(out var text) ? text ?? string.Empty : value.ToString();
    }

    private static async Task RollbackAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string QuoteCheckHash(JsonElement product, string priceText, string? timeText, string? guaranteedText, string? techKey)
    {
        var raw = PhpScalar(product, "manufacturer")
            + PhpScalar(product, "article")
            + PhpScalar(product, "article_show")
            + PhpScalar(product, "name")
            + PhpScalar(product, "exist")
            + priceText
            + (timeText ?? PhpScalar(product, "time_to_exe"))
            + (guaranteedText ?? PhpScalar(product, "time_to_exe_guaranteed"))
            + PhpScalar(product, "storage")
            + PhpScalar(product, "min_order")
            + PhpScalar(product, "probability")
            + PhpScalar(product, "office_id")
            + PhpScalar(product, "storage_id")
            + PhpScalar(product, "price_purchase")
            + PhpScalar(product, "markup")
            + PhpScalar(product, "json_params")
            + "2"
            + (techKey ?? string.Empty);
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    private static string PhpScalar(JsonElement product, string name)
    {
        if (!product.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => "Array"
        };
    }

    private static int PhpInt(JsonElement product, string name)
    {
        if (!product.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static int CountNeed(JsonElement product)
    {
        if (!product.TryGetProperty("count_need", out var value) || value.ValueKind is JsonValueKind.Null)
        {
            return 1;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number == 0 ? 1 : Math.Max(1, number);
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        if (text.Length == 0 || text == "0")
        {
            return 1;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? Math.Max(1, parsed) : 1;
    }

    private static int ManualCount(string? countNeed)
        => int.TryParse(countNeed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? Math.Max(1, parsed) : 1;

    private static string ArticleNorm(string article)
        => Regex.Replace(article, @"[^0-9A-Za-zА-Яа-яЁё]+", string.Empty).ToUpper(CultureInfo.InvariantCulture);

    private static string PhpHtml(string value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string PhpNumber(decimal value)
        => value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    private readonly record struct QuoteUser(int UserId, object? Stop);

    private sealed record AcceptItem(
        string ProductJson,
        int CountNeed,
        string? QuotedPrice,
        string? QuotedTime,
        bool UseAlt,
        string AltManufacturer,
        string AltArticle,
        string AltArticleShow,
        string AltName,
        int AltCount,
        string? AltPrice,
        int AltStorageId);

    private sealed record CartLine(
        string Manufacturer,
        string Article,
        string ArticleShow,
        string Name,
        string Exist,
        string TimeToExe,
        string TimeGuaranteed,
        string Storage,
        string MinOrder,
        string Probability,
        string Markup,
        string PricePurchase,
        string OfficeId,
        string StorageId,
        decimal Price,
        int CountNeed,
        string ProductJson,
        string JsonParams,
        bool SkipAlready);
}
