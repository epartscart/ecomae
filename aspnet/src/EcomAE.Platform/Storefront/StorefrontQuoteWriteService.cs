using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP twins: <c>ajax_quote_submit.php</c>, <c>ajax_quote_accept.php</c>,
/// <c>ajax_add_to_quote.php</c>, <c>ajax_add_to_quote_manual.php</c>.
/// </summary>
public interface IStorefrontQuoteWriteService
{
    Task<ErpSimpleWriteResult> SubmitAsync(int userId, long quoteId, string? customerNote, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AcceptAsync(int userId, long quoteId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddItemAsync(int userId, StorefrontQuoteAddItemWriteRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddManualAsync(int userId, string? manufacturer, string? article, int countNeed = 1, CancellationToken cancellationToken = default);
}

public sealed record StorefrontQuoteAddItemWriteRequest(
    int ProductType,
    string? Manufacturer,
    string? Article,
    int CountNeed = 1,
    string? ArticleShow = null,
    string? Name = null,
    decimal Exist = 0,
    decimal Price = 0,
    int TimeToExe = 0,
    int TimeToExeGuaranteed = 0,
    string? Storage = null,
    decimal MinOrder = 1,
    int Probability = 0,
    int OfficeId = 0,
    int StorageId = 0,
    decimal PricePurchase = 0,
    decimal Markup = 0,
    string? JsonParams = null);

public sealed class StorefrontQuoteWriteService : IStorefrontQuoteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontQuoteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SubmitAsync(
        int userId,
        long quoteId,
        string? customerNote,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quote is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Quote database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            quoteId, userId);
        if (string.IsNullOrWhiteSpace(status))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Quote not found or already submitted.");
        }

        if (!string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("quote_not_draft", "Quote not found or already submitted.");
        }

        var lines = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ?"),
            cancellationToken,
            quoteId);
        if (lines < 1)
        {
            return ErpSimpleWriteResult.Fail("empty", "Add at least one line before submitting.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `status` = 'submitted', `time_submitted` = ?, `time_updated` = ?, `customer_note` = ? WHERE `id` = ? AND `user_id` = ?"),
            cancellationToken,
            now, now, customerNote ?? string.Empty, quoteId, userId);
        if (rows <= 0)
        {
            return ErpSimpleWriteResult.Fail("update_failed", "Could not submit quote.");
        }

        return ErpSimpleWriteResult.Ok("Quote submitted.", quoteId);
    }

    public async Task<ErpSimpleWriteResult> AcceptAsync(
        int userId,
        long quoteId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quote is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Quote database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var status = await ErpDb.StringAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `status` FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                quoteId, userId);
            if (!string.Equals(status, "quoted", StringComparison.OrdinalIgnoreCase))
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("quote_not_quoted", "Quote is not available for acceptance");
            }

            await using var itemsCmd = connection.CreateCommand();
            itemsCmd.Transaction = tx;
            itemsCmd.CommandText = ErpDb.Positional("""
                SELECT `id`,
                       IFNULL(`product_type`, 2) AS product_type,
                       IFNULL(`product_object_json`,'') AS product_object_json,
                       IFNULL(`count_need`, 1) AS count_need,
                       IFNULL(`quoted_price`, 0) AS quoted_price,
                       IFNULL(`quoted_time_to_exe`, 0) AS quoted_time_to_exe,
                       IFNULL(`offer_alternative`, 0) AS offer_alternative,
                       IFNULL(`alt_manufacturer`,'') AS alt_manufacturer,
                       IFNULL(`alt_article`,'') AS alt_article,
                       IFNULL(`alt_article_show`,'') AS alt_article_show,
                       IFNULL(`alt_name`,'') AS alt_name,
                       IFNULL(`alt_count_need`, 0) AS alt_count_need,
                       IFNULL(`alt_quoted_price`, 0) AS alt_quoted_price,
                       IFNULL(`alt_storage_id`, 0) AS alt_storage_id
                FROM `shop_quote_items`
                WHERE `quote_id` = ?
                ORDER BY `id` ASC
                """);
            ErpDb.AddParameters(itemsCmd, quoteId);
            await using var reader = await itemsCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var items = new List<AcceptLine>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new AcceptLine(
                    Convert.ToInt32(reader["product_type"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["product_object_json"], CultureInfo.InvariantCulture) ?? "",
                    Convert.ToInt32(reader["count_need"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["quoted_price"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["quoted_time_to_exe"] is DBNull ? 0 : reader["quoted_time_to_exe"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["offer_alternative"], CultureInfo.InvariantCulture) == 1,
                    Convert.ToString(reader["alt_manufacturer"], CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader["alt_article"], CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader["alt_article_show"], CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader["alt_name"], CultureInfo.InvariantCulture) ?? "",
                    Convert.ToInt32(reader["alt_count_need"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["alt_quoted_price"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["alt_storage_id"], CultureInfo.InvariantCulture)));
            }

            await reader.DisposeAsync();
            if (items.Count < 1)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("empty", "No lines in quote");
            }

            foreach (var item in items)
            {
                var useAlt = item.OfferAlternative
                    && !string.IsNullOrWhiteSpace(item.AltManufacturer)
                    && !string.IsNullOrWhiteSpace(item.AltArticle);
                var price = useAlt ? item.AltQuotedPrice : item.QuotedPrice;
                if (price <= 0)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("incomplete", "Quote is incomplete — wait for staff pricing on all lines");
                }
            }

            var writes = 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var item in items)
            {
                JsonObjectBag bag;
                try
                {
                    bag = JsonObjectBag.Parse(item.ProductJson);
                }
                catch (Exception)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("bad_line", "Could not complete acceptance");
                }

                if (bag.Int("product_type") != 2)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("bad_line", "Could not complete acceptance");
                }

                var useAlt = item.OfferAlternative
                    && !string.IsNullOrWhiteSpace(item.AltManufacturer)
                    && !string.IsNullOrWhiteSpace(item.AltArticle);
                var requestedMfr = bag.Str("manufacturer");
                var requestedArt = FirstNonEmpty(bag.Str("article_show"), bag.Str("article"));
                decimal price;
                var countNeed = Math.Max(1, item.CountNeed);
                if (useAlt)
                {
                    var altMfr = item.AltManufacturer.Trim().ToUpperInvariant();
                    var altArtShow = FirstNonEmpty(item.AltArticleShow, item.AltArticle).Trim();
                    var altArt = NormalizeArticle(altArtShow);
                    var altName = item.AltName.Trim();
                    if (altName.Length == 0)
                    {
                        altName = altMfr + " " + altArtShow + " (alternative)";
                    }

                    bag.Set("manufacturer", altMfr);
                    bag.Set("article", altArt);
                    bag.Set("article_show", altArtShow);
                    bag.Set("name", altName);
                    bag.Set("epc_quote_alternative", 1);
                    bag.Set("epc_requested_manufacturer", requestedMfr);
                    bag.Set("epc_requested_article", requestedArt);
                    price = item.AltQuotedPrice;
                    countNeed = Math.Max(1, item.AltCountNeed > 0 ? item.AltCountNeed : 1);
                    if (item.AltStorageId > 0)
                    {
                        var caption = await ErpDb.StringAsync(
                            connection,
                            tx,
                            ErpDb.Positional("SELECT COALESCE(NULLIF(TRIM(`short_name`), ''), `name`) FROM `shop_storages` WHERE `id` = ? LIMIT 1"),
                            cancellationToken,
                            item.AltStorageId) ?? "";
                        bag.Set("storage_id", item.AltStorageId);
                        bag.Set("storage", caption.Length > 0 ? caption : "Storage #" + item.AltStorageId.ToString(CultureInfo.InvariantCulture));
                        bag.Set("epc_alt_storage_id", item.AltStorageId);
                    }
                }
                else
                {
                    price = item.QuotedPrice;
                    countNeed = Math.Max(1, item.CountNeed);
                }

                bag.Set("price", price);
                if (item.QuotedTimeToExe > 0)
                {
                    bag.Set("time_to_exe", item.QuotedTimeToExe);
                    bag.Set("time_to_exe_guaranteed", item.QuotedTimeToExe);
                }

                bag.Set("count_need", countNeed);

                var manufacturer = bag.Str("manufacturer");
                var article = bag.Str("article");
                var articleShow = bag.Str("article_show");
                var name = bag.Str("name");
                var exist = bag.Dec("exist");
                var timeToExe = bag.Int("time_to_exe");
                var timeToExeG = bag.Int("time_to_exe_guaranteed");
                var storage = bag.Str("storage");
                var minOrder = bag.Dec("min_order");
                var probability = bag.Int("probability");
                var pricePurchase = bag.Dec("price_purchase");
                var markup = bag.Dec("markup");
                var officeId = bag.Int("office_id");
                var storageId = bag.Int("storage_id");
                var jsonParams = bag.Str("json_params");
                var skipDup = JsonUsedFlag(jsonParams);
                if (!skipDup)
                {
                    var already = await ErpDb.LongAsync(
                        connection,
                        tx,
                        ErpDb.Positional("""
                            SELECT COUNT(*) FROM `shop_carts` WHERE
                            `product_type` = 2 AND `user_id` = ? AND `session_id` = 0 AND
                            `t2_manufacturer` = ? AND `t2_article` = ? AND `t2_exist` = ? AND
                            `t2_time_to_exe` = ? AND `t2_time_to_exe_guaranteed` = ? AND
                            `t2_probability` = ? AND `t2_office_id` = ? AND `t2_storage_id` = ? AND
                            CAST(`price` AS DECIMAL(12,4)) = CAST(? AS DECIMAL(12,4))
                            """),
                        cancellationToken,
                        userId, manufacturer, article, exist, timeToExe, timeToExeG,
                        probability, officeId, storageId, price);
                    if (already > 0)
                    {
                        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return ErpSimpleWriteResult.Fail("already", "One or more items are already in your cart at this price — adjust the cart and try again");
                    }
                }

                var productJson = bag.ToJson();
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
                        VALUES (2, ?, ?, ?, ?, 0, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                        """),
                    cancellationToken,
                    price, countNeed, now, userId,
                    manufacturer, article, articleShow, name, exist,
                    timeToExe, timeToExeG, storage, minOrder <= 0 ? 1 : minOrder,
                    probability, markup, pricePurchase, officeId, storageId,
                    productJson, jsonParams);
                writes++;
            }

            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `shop_quote_requests` SET `status` = 'accepted', `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                now, quoteId);
            writes++;
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Quote accepted. Lines added to your cart.", quoteId, writes);
        }
        catch (Exception)
        {
            try
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // already rolled back
            }

            return ErpSimpleWriteResult.Fail("accept_failed", "Could not complete acceptance");
        }
    }

    public async Task<ErpSimpleWriteResult> AddItemAsync(
        int userId,
        StorefrontQuoteAddItemWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        if (request.ProductType != 2)
        {
            return ErpSimpleWriteResult.Fail("product_type_unsupported", "Only supplier price-search lines can be added to a quote.");
        }

        var manufacturer = (request.Manufacturer ?? string.Empty).Trim();
        var article = (request.Article ?? string.Empty).Trim();
        if (manufacturer.Length == 0 || article.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Brand and part number are required.");
        }

        var countNeed = request.CountNeed < 1 ? 1 : request.CountNeed;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Quote database is not configured.");
        }

        var articleShow = FirstNonEmpty(request.ArticleShow, article);
        var name = FirstNonEmpty(request.Name, manufacturer + " " + articleShow);
        var product = new Dictionary<string, object?>
        {
            ["product_type"] = 2,
            ["manufacturer"] = manufacturer,
            ["article"] = article,
            ["article_show"] = articleShow,
            ["name"] = name,
            ["exist"] = request.Exist,
            ["price"] = request.Price,
            ["time_to_exe"] = request.TimeToExe,
            ["time_to_exe_guaranteed"] = request.TimeToExeGuaranteed,
            ["storage"] = request.Storage ?? "",
            ["min_order"] = request.MinOrder <= 0 ? 1 : request.MinOrder,
            ["probability"] = request.Probability,
            ["office_id"] = request.OfficeId,
            ["storage_id"] = request.StorageId,
            ["price_purchase"] = request.PricePurchase,
            ["markup"] = request.Markup,
            ["json_params"] = request.JsonParams ?? "",
            ["count_need"] = countNeed
        };

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var quoteId = await EnsureDraftAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("insert_failed", "Could not create quote draft.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`) VALUES (?, 2, ?, ?)"),
            cancellationToken,
            quoteId, JsonSerializer.Serialize(product), countNeed);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            now, quoteId);
        return new ErpSimpleWriteResult(true, "ok", "Line added to quote.", quoteId, 1);
    }

    public async Task<ErpSimpleWriteResult> AddManualAsync(
        int userId,
        string? manufacturer,
        string? article,
        int countNeed = 1,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        var brand = (manufacturer ?? string.Empty).Trim();
        var part = (article ?? string.Empty).Trim();
        if (brand.Length == 0 || part.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Brand and part number are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Quote database is not configured.");
        }

        var articleNorm = NormalizeArticle(part);
        var brandEnc = WebUtility.HtmlEncode(brand.ToUpperInvariant());
        var articleShow = WebUtility.HtmlEncode(articleNorm);
        var name = WebUtility.HtmlEncode("Quote request — " + brand.ToUpperInvariant() + " " + articleNorm);
        var qty = countNeed < 1 ? 1 : countNeed;
        var product = new Dictionary<string, object?>
        {
            ["product_type"] = 2,
            ["manufacturer"] = brandEnc,
            ["article"] = articleNorm,
            ["article_show"] = articleShow,
            ["name"] = name,
            ["exist"] = 0,
            ["price"] = 0,
            ["time_to_exe"] = 0,
            ["time_to_exe_guaranteed"] = 0,
            ["storage"] = "",
            ["min_order"] = 1,
            ["probability"] = 0,
            ["office_id"] = 0,
            ["storage_id"] = 0,
            ["price_purchase"] = 0,
            ["markup"] = 0,
            ["json_params"] = "",
            ["check_hash"] = "manual",
            ["epc_manual_quote"] = 1,
            ["count_need"] = qty
        };

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var quoteId = await EnsureDraftAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("insert_failed", "Could not create quote draft.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`) VALUES (?, 2, ?, ?)"),
            cancellationToken,
            quoteId, JsonSerializer.Serialize(product), qty);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            now, quoteId);
        return new ErpSimpleWriteResult(true, "ok", "Manual line added to quote.", quoteId, 1);
    }

    private static async Task<long> EnsureDraftAsync(
        System.Data.Common.DbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_quote_requests` WHERE `user_id` = ? AND `status` = 'draft' ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            userId);
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
            userId, now, now);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeArticle(string article)
        => new string(article.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static bool JsonUsedFlag(string jsonParams)
    {
        if (string.IsNullOrWhiteSpace(jsonParams))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonParams);
            return doc.RootElement.TryGetProperty("used", out var used)
                   && used.ValueKind == JsonValueKind.Number
                   && used.TryGetInt32(out var flag)
                   && flag == 1;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record AcceptLine(
        int ProductType,
        string ProductJson,
        int CountNeed,
        decimal QuotedPrice,
        int QuotedTimeToExe,
        bool OfferAlternative,
        string AltManufacturer,
        string AltArticle,
        string AltArticleShow,
        string AltName,
        int AltCountNeed,
        decimal AltQuotedPrice,
        int AltStorageId);

    private sealed class JsonObjectBag
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public static JsonObjectBag Parse(string json)
        {
            var bag = new JsonObjectBag();
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                bag._values[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.Number when prop.Value.TryGetDecimal(out var d) => d,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }

            return bag;
        }

        public void Set(string name, object? value) => _values[name] = value;

        public string Str(string name)
            => _values.TryGetValue(name, out var value) && value is not null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
                : "";

        public int Int(string name)
        {
            if (!_values.TryGetValue(name, out var value) || value is null)
            {
                return 0;
            }

            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        public decimal Dec(string name)
        {
            if (!_values.TryGetValue(name, out var value) || value is null)
            {
                return 0;
            }

            return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        public string ToJson() => JsonSerializer.Serialize(_values);
    }
}
