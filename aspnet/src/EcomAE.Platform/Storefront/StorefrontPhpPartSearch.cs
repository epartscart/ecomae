using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public static object AnalogsEmptyArticle()
        => new AnalogsBody(0, 1, [], null, null);

    public static bool AnalogsArticleEmpty(string? searchObjectJson)
        => NormalizePriceArticle(ReadJsonString(searchObjectJson, "article")).Length == 0;

    public static ManufacturersBody DatabaseUnavailableManufacturers(object storage)
        => new(false, "Database unavailable", "0.000", [], storage);

    public static bool RefererAllowed(string? referer, string host)
    {
        if (string.IsNullOrWhiteSpace(referer) || string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase);
    }

    public static object PartInfoUnconfigured()
        => new PartInfoBody(0);

    public static async Task<object> ManufacturersFromStorageAsync(
        DbConnection connection,
        string? queryJson,
        int storageId,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `shop_storages_interfaces_types`.`handler_folder` FROM `shop_storages` INNER JOIN `shop_storages_interfaces_types` ON `shop_storages`.`interface_type` = `shop_storages_interfaces_types`.`id` WHERE `shop_storages`.`id` = ? LIMIT 1");
            ErpDb.AddParameters(command, storageId);
            var handler = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            _ = queryJson;
            _ = handler;
            return ManufacturersEnvelope(false, StorageHandlerManufacturersError, started, [], storageId);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return ManufacturersEnvelope(false, "Storages are not in this database.", started, [], storageId);
        }
    }

    public static async Task<object> ManufacturersFromPricesAsync(
        DbConnection connection,
        string? queryJson,
        string? bunchesJson,
        int groupId,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var article = NormalizePriceArticle(ReadJsonString(queryJson, "article"));
        var bunches = ReadBunches(bunchesJson);
        if (article.Length == 0 || bunches.Count == 0)
        {
            return ManufacturersEnvelope(true, string.Empty, started, [], PricesStorageLabel);
        }

        try
        {
            var disabledStorages = await DisabledIdsAsync(connection, "shop_storages", cancellationToken).ConfigureAwait(false);
            var disabledPrices = await DisabledIdsAsync(connection, "shop_docpart_prices", cancellationToken).ConfigureAwait(false);
            var pairs = new List<(int OfficeId, int StorageId)>();
            foreach (var bunch in bunches)
            {
                if (bunch.StorageId < 1 || disabledStorages.Contains(bunch.StorageId))
                {
                    continue;
                }

                pairs.Add((bunch.OfficeId, bunch.StorageId));
            }

            if (pairs.Count == 0)
            {
                return ManufacturersEnvelope(true, string.Empty, started, [], PricesStorageLabel);
            }

            var priceToPairs = await PricePairsAsync(connection, pairs, disabledPrices, cancellationToken).ConfigureAwait(false);
            if (priceToPairs.Count == 0)
            {
                return ManufacturersEnvelope(true, string.Empty, started, [], PricesStorageLabel);
            }

            var rows = await PriceRowsAsync(connection, article, priceToPairs.Keys.ToList(), cancellationToken).ConfigureAwait(false);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var manufacturers = new List<DocpartManufacturerJson>();
            foreach (var row in rows)
            {
                if (!priceToPairs.TryGetValue(row.PriceId, out var targets))
                {
                    continue;
                }

                if (!await BrandVisibleAsync(connection, groupId, row.Manufacturer, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    var item = MakeManufacturer(row.Manufacturer, row.Name, target.OfficeId, target.StorageId, new Dictionary<string, string> { ["type"] = "prices" });
                    if (!item.Valid)
                    {
                        continue;
                    }

                    var hash = Md5Hex(item.Manufacturer + "|" + target.StorageId.ToString(CultureInfo.InvariantCulture) + "|" + target.OfficeId.ToString(CultureInfo.InvariantCulture));
                    if (seen.Add(hash))
                    {
                        manufacturers.Add(item);
                    }
                }
            }

            return ManufacturersEnvelope(true, string.Empty, started, manufacturers, PricesStorageLabel);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return ManufacturersEnvelope(true, "Price lists are not in this database.", started, [], PricesStorageLabel);
        }
    }

    public static async Task<object> ManufacturersFromCrossServerAsync(
        DbConnection connection,
        string? queryJson,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var article = NormalizeCrossArticle(ReadJsonString(queryJson, "article"));
        if (article.Length == 0)
        {
            return ManufacturersEnvelope(true, string.Empty, started, [], CrossStorageLabel);
        }

        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var manufacturers = new List<DocpartManufacturerJson>();
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `article`, `analog`, `manufacturer_article`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE `article` = ? OR `analog` = ?");
            ErpDb.AddParameters(command, article, article);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var storedArticle = Cell(reader, 0);
                var storedAnalog = Cell(reader, 1);
                var manufacturer = string.Equals(article, storedArticle, StringComparison.Ordinal)
                    ? Cell(reader, 2)
                    : string.Equals(article, storedAnalog, StringComparison.Ordinal)
                        ? Cell(reader, 3)
                        : string.Empty;
                if (manufacturer.Length == 0)
                {
                    continue;
                }

                var item = MakeManufacturer(manufacturer, string.Empty, 0, 0, new Dictionary<string, string> { ["type"] = "table" });
                if (!item.Valid)
                {
                    continue;
                }

                if (seen.Add(Md5Hex(item.Manufacturer)))
                {
                    manufacturers.Add(item);
                }
            }

            return ManufacturersEnvelope(true, string.Empty, started, manufacturers, CrossStorageLabel);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return ManufacturersEnvelope(true, "Cross references are not in this database.", started, [], CrossStorageLabel);
        }
    }

    public static async Task<object> AnalogsListAsync(
        DbConnection connection,
        string? searchObjectJson,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var article = NormalizePriceArticle(ReadJsonString(searchObjectJson, "article"));
        if (article.Length == 0)
        {
            return AnalogsEmptyArticle();
        }

        var manufacturers = ReadManufacturerNames(searchObjectJson);
        try
        {
            var analogs = new List<AnalogRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (manufacturers.Count == 0)
            {
                await WalkAnalogsAsync(connection, article, string.Empty, 0, analogs, seen, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                foreach (var manufacturer in manufacturers)
                {
                    await WalkAnalogsAsync(connection, article, manufacturer, 0, analogs, seen, cancellationToken).ConfigureAwait(false);
                }
            }

            return new AnalogsBody(1, 1, analogs, FormatSeconds(started), null);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new AnalogsBody(0, 1, [], null, "Cross references are not in this database.");
        }
    }

    public static async Task<AsynchronOutcome> AsynchronAsync(
        DbConnection connection,
        string? requestJson,
        bool pricesVisible,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var document = ParseObject(requestJson);
        var root = document?.RootElement ?? default;
        var action = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("action", out var actionNode)
            ? actionNode.GetString() ?? string.Empty
            : string.Empty;
        var article = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("article", out var articleNode)
            ? articleNode.GetString() ?? string.Empty
            : string.Empty;
        var storages = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("storages", out var storagesNode) && storagesNode.ValueKind == JsonValueKind.Array
            ? storagesNode
            : default;

        if (!string.Equals(action, "get_manufacturers", StringComparison.Ordinal)
            && !string.Equals(action, "get_articles", StringComparison.Ordinal))
        {
            return AsynchronOutcome.Json(new AsynchronBody(0, UnknownActionStringKey, null, null));
        }

        if (storages.ValueKind != JsonValueKind.Array || storages.GetArrayLength() == 0)
        {
            return AsynchronOutcome.Json(new AsynchronBody(0, EmptyStoragesStringKey, null, null));
        }

        var data = new List<object>();
        foreach (var item in storages.EnumerateArray())
        {
            if (string.Equals(action, "get_manufacturers", StringComparison.Ordinal))
            {
                data.Add(await AsynchronManufacturersAsync(connection, article, item, cancellationToken).ConfigureAwait(false));
            }
            else
            {
                data.Add(await AsynchronArticlesAsync(connection, item, pricesVisible, cancellationToken).ConfigureAwait(false));
            }
        }

        return AsynchronOutcome.Json(new AsynchronBody(1, null, data, FormatSeconds(started)));
    }

    public static async Task<object> ProductsOfBunch2Async(
        DbConnection connection,
        string? article,
        int officeId,
        int storageId,
        string? queryJson,
        int userId,
        int groupId,
        bool pricesVisible,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizePriceArticle(article);
        if (normalized.Length == 0)
        {
            return new Bunch2Body(0, "Empty article", [], userId, groupId, pricesVisible);
        }

        if (officeId != 0)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(
                    "SELECT `min_point` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? LIMIT 1");
                ErpDb.AddParameters(command, officeId, storageId, groupId);
                _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new Bunch2Body(0, "Office storage markups are not in this database.", [], userId, groupId, pricesVisible);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_storages` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(command, storageId);
                _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new Bunch2Body(0, "Storages are not in this database.", [], userId, groupId, pricesVisible);
            }

            return new Bunch2Body(0, StorageHandlerError, [], userId, groupId, pricesVisible);
        }

        var bunchesJson = queryJson;
        if (!string.IsNullOrWhiteSpace(queryJson))
        {
            using var parsed = ParseObject(queryJson);
            if (parsed is not null && parsed.RootElement.TryGetProperty("office_storage_bunches", out var nested))
            {
                bunchesJson = nested.GetRawText();
            }
        }

        try
        {
            var pairs = ReadBunches(bunchesJson);
            var products = new List<Bunch2Product>();
            if (pairs.Count > 0)
            {
                var priceToPairs = await PricePairsAsync(connection, pairs.Select(p => (p.OfficeId, p.StorageId)).ToList(), [], cancellationToken).ConfigureAwait(false);
                if (priceToPairs.Count > 0)
                {
                    var rows = await PriceRowsAsync(connection, normalized, priceToPairs.Keys.ToList(), cancellationToken).ConfigureAwait(false);
                    foreach (var row in rows)
                    {
                        if (!priceToPairs.TryGetValue(row.PriceId, out var targets))
                        {
                            continue;
                        }

                        foreach (var target in targets)
                        {
                            products.Add(BunchProduct(row, target.OfficeId, target.StorageId, pricesVisible));
                        }
                    }
                }
            }

            var result = products.Count > 0 ? 1 : 0;
            var message = products.Count > 0 ? string.Empty : StorageHandlerError;
            return new Bunch2Body(result, message, products, userId, groupId, pricesVisible);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new Bunch2Body(0, "Price lists are not in this database.", [], userId, groupId, pricesVisible);
        }
    }

    private static async Task<object> AsynchronManufacturersAsync(
        DbConnection connection,
        string article,
        JsonElement item,
        CancellationToken cancellationToken)
    {
        var version = ProtocolVersion(item);
        var query = JsonSerializer.Serialize(new Dictionary<string, string> { ["article"] = article });
        if (version == "2")
        {
            var storageId = JsonInt(item, "storage_id");
            return await ManufacturersFromStorageAsync(connection, query, storageId, cancellationToken).ConfigureAwait(false);
        }

        if (version == "3")
        {
            var bunches = item.TryGetProperty("office_storage_bunches", out var nested) ? nested.GetRawText() : "[]";
            return await ManufacturersFromPricesAsync(connection, query, bunches, JsonInt(item, "group_id"), cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(version, "server", StringComparison.Ordinal))
        {
            return await ManufacturersFromCrossServerAsync(connection, query, cancellationToken).ConfigureAwait(false);
        }

        return ManufacturersEnvelope(false, StorageHandlerManufacturersError, Stopwatch.GetTimestamp(), [], 0);
    }

    private static async Task<object> AsynchronArticlesAsync(
        DbConnection connection,
        JsonElement item,
        bool pricesVisible,
        CancellationToken cancellationToken)
    {
        var search = item.TryGetProperty("search_object", out var searchNode) ? searchNode.GetRawText() : "{}";
        var article = ReadJsonString(search, "article");
        if (article.Length == 0)
        {
            article = item.TryGetProperty("article", out var articleNode) ? articleNode.GetString() ?? string.Empty : string.Empty;
        }

        var officeId = JsonInt(item, "office_id");
        var storageId = JsonInt(item, "storage_id");
        if (!search.Contains("office_storage_bunches", StringComparison.Ordinal) && item.TryGetProperty("office_storage_bunches", out var nestedBunches))
        {
            search = "{\"article\":" + JsonSerializer.Serialize(article) + ",\"office_storage_bunches\":" + nestedBunches.GetRawText() + "}";
        }
        else if (officeId == 0 && storageId > 0 && !search.Contains("office_storage_bunches", StringComparison.Ordinal))
        {
            search = "{\"article\":" + JsonSerializer.Serialize(article) + ",\"office_storage_bunches\":[{\"office_id\":0,\"storage_id\":" + storageId.ToString(CultureInfo.InvariantCulture) + "}]}";
        }

        return await ProductsOfBunch2Async(connection, article, officeId, storageId, search, 0, 0, pricesVisible, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WalkAnalogsAsync(
        DbConnection connection,
        string article,
        string manufacturer,
        int level,
        List<AnalogRow> analogs,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        if (analogs.Count >= 40)
        {
            return;
        }

        level++;
        var manufacturerName = manufacturer.Trim();
        if (manufacturerName.Length == 0)
        {
            await using var lookup = connection.CreateCommand();
            lookup.CommandText = ErpDb.Positional(
                "SELECT `article`, `analog`, `manufacturer_article`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE `article` = ? OR `analog` = ? LIMIT 1");
            ErpDb.AddParameters(lookup, article, article);
            await using var reader = await lookup.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                manufacturerName = string.Equals(article, Cell(reader, 0), StringComparison.Ordinal)
                    ? Cell(reader, 2)
                    : Cell(reader, 3);
            }
        }

        await using var command = connection.CreateCommand();
        if (manufacturerName.Length > 0)
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `article`, `analog`, `manufacturer_article`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE (`article` = ? AND `manufacturer_article` = ?) OR (`analog` = ? AND `manufacturer_analog` = ?)");
            ErpDb.AddParameters(command, article, manufacturerName, article, manufacturerName);
        }
        else
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `article`, `analog`, `manufacturer_article`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE `article` = ? OR `analog` = ?");
            ErpDb.AddParameters(command, article, article);
        }

        var next = new List<(string Article, string Manufacturer)>();
        await using (var rows = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await rows.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var leftArticle = NormalizePriceArticle(Cell(rows, 0));
                var leftManufacturer = Cell(rows, 2).Trim();
                var rightArticle = NormalizePriceArticle(Cell(rows, 1));
                var rightManufacturer = Cell(rows, 3).Trim();
                var partnerArticle = string.Equals(leftArticle, article, StringComparison.Ordinal) ? rightArticle : leftArticle;
                var partnerManufacturer = string.Equals(leftArticle, article, StringComparison.Ordinal) ? rightManufacturer : leftManufacturer;
                if (partnerArticle.Length == 0 || string.Equals(partnerArticle, article, StringComparison.Ordinal))
                {
                    continue;
                }

                var hash = Md5Hex(partnerArticle + partnerManufacturer);
                if (!seen.Add(hash))
                {
                    continue;
                }

                analogs.Add(new AnalogRow(partnerArticle, partnerManufacturer, "table"));
                next.Add((partnerArticle, partnerManufacturer));
                if (analogs.Count >= 40)
                {
                    break;
                }
            }
        }

        if (level <= 2)
        {
            foreach (var step in next)
            {
                await WalkAnalogsAsync(connection, step.Article, step.Manufacturer, level, analogs, seen, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<Dictionary<int, List<(int OfficeId, int StorageId)>>> PricePairsAsync(
        DbConnection connection,
        IReadOnlyList<(int OfficeId, int StorageId)> pairs,
        IReadOnlyCollection<int> disabledPrices,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<int, List<(int OfficeId, int StorageId)>>();
        var storageIds = pairs.Select(p => p.StorageId).Distinct().ToList();
        if (storageIds.Count == 0)
        {
            return map;
        }

        var placeholders = string.Join(",", Enumerable.Repeat("?", storageIds.Count));
        var storagePrice = new Dictionary<int, int>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `id`, `connection_options` FROM `shop_storages` WHERE `id` IN (" + placeholders + ")");
            ErpDb.AddParameters(command, storageIds.Cast<object>().ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                var options = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var priceId = ReadPriceId(options);
                if (priceId > 0 && !disabledPrices.Contains(priceId))
                {
                    storagePrice[id] = priceId;
                }
            }
        }

        foreach (var pair in pairs)
        {
            if (!storagePrice.TryGetValue(pair.StorageId, out var priceId))
            {
                continue;
            }

            if (!map.TryGetValue(priceId, out var list))
            {
                list = [];
                map[priceId] = list;
            }

            list.Add(pair);
        }

        return map;
    }

    private static async Task<List<PriceRow>> PriceRowsAsync(
        DbConnection connection,
        string article,
        IReadOnlyList<int> priceIds,
        CancellationToken cancellationToken)
    {
        if (priceIds.Count == 0)
        {
            return [];
        }

        var placeholders = string.Join(",", Enumerable.Repeat("?", priceIds.Count));
        var parameters = new List<object> { article };
        parameters.AddRange(priceIds.Cast<object>());
        try
        {
            return await ReadPriceRowsAsync(
                connection,
                "SELECT `manufacturer`, `name`, `price_id`, `article`, `exist`, `price`, IFNULL(`storage`,''), IFNULL(`time_to_exe`,0) FROM `shop_docpart_prices_data` WHERE `article_search` = ? AND `price_id` IN (" + placeholders + ")",
                parameters,
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (ex.Message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadPriceRowsAsync(
                connection,
                "SELECT `manufacturer`, `name`, `price_id`, `article`, `exist`, `price`, IFNULL(`storage`,''), IFNULL(`time_to_exe`,0) FROM `shop_docpart_prices_data` WHERE UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(`article`,' ',''),'-',''),'.',''),'_',''),'/','')) = ? AND `price_id` IN (" + placeholders + ")",
                parameters,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<PriceRow>> ReadPriceRowsAsync(
        DbConnection connection,
        string sql,
        IReadOnlyList<object> parameters,
        CancellationToken cancellationToken)
    {
        var rows = new List<PriceRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, parameters.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new PriceRow(
                Cell(reader, 0),
                Cell(reader, 1),
                reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                Cell(reader, 3),
                reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                Cell(reader, 6),
                reader.IsDBNull(7) ? "0" : Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? "0"));
        }

        return rows;
    }

    private static async Task<bool> BrandVisibleAsync(
        DbConnection connection,
        int groupId,
        string manufacturer,
        CancellationToken cancellationToken)
    {
        if (groupId <= 0 || string.IsNullOrWhiteSpace(manufacturer))
        {
            return true;
        }

        try
        {
            var visible = await ErpDb.ScalarAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `visible` FROM `epc_price_profile_brand_rules` WHERE `group_id` = ? AND `manufacturer` = ? LIMIT 1"),
                cancellationToken,
                groupId,
                manufacturer.Trim().ToUpperInvariant()).ConfigureAwait(false);
            return visible is null || Convert.ToInt32(visible, CultureInfo.InvariantCulture) != 0;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return true;
        }
    }

    private static async Task<HashSet<int>> DisabledIdsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var ids = new HashSet<int>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id` FROM `" + table + "` WHERE IFNULL(`storefront_temp_disabled`, 0) = 1";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
        }

        return ids;
    }

    private static Bunch2Product BunchProduct(PriceRow row, int officeId, int storageId, bool pricesVisible)
    {
        if (pricesVisible)
        {
            return new Bunch2Product(row.Manufacturer, row.Article, row.Name, row.Price, row.Exist, row.TimeToExe, row.Storage, storageId, row.PriceId, officeId);
        }

        return new Bunch2Product(
            row.Manufacturer,
            row.Article,
            row.Name,
            0m,
            row.Exist > 0 ? 1 : 0,
            "0",
            string.Empty,
            0,
            row.PriceId,
            officeId);
    }

    private static DocpartManufacturerJson MakeManufacturer(
        string manufacturer,
        string name,
        int officeId,
        int storageId,
        Dictionary<string, string> parameters)
    {
        var trimmed = (manufacturer ?? string.Empty).Trim();
        var cleanName = (name ?? string.Empty).Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\t", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\\", string.Empty, StringComparison.Ordinal).Trim();
        return new DocpartManufacturerJson(
            trimmed,
            0,
            trimmed.ToUpperInvariant(),
            cleanName,
            storageId,
            officeId,
            true,
            parameters,
            trimmed.Length > 1);
    }

    private static ManufacturersBody ManufacturersEnvelope(
        bool status,
        string message,
        long started,
        IReadOnlyList<DocpartManufacturerJson> manufacturers,
        object storage)
        => new(status, message, FormatSeconds(started), manufacturers, storage);

    public static string NormalizePriceArticle(string? article)
    {
        if (string.IsNullOrEmpty(article))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(article.Length);
        foreach (var ch in article)
        {
            if (ch is ' ' or '-' or '_' or '`' or '/' or '\'' or '"' or '\\' or '.' or ',' or '#' or '\r' or '\n' or '\t')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(ch));
        }

        return builder.ToString();
    }

    public static string NormalizeCrossArticle(string? article)
    {
        if (string.IsNullOrEmpty(article))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(article.Length);
        foreach (var ch in article)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is >= '\u0400' and <= '\u04FF')
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static string FormatSeconds(long started)
    {
        var seconds = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
        if (seconds < 0)
        {
            seconds = 0;
        }

        return seconds.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static string Md5Hex(string value)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Cell(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static int ReadPriceId(string options)
    {
        if (string.IsNullOrWhiteSpace(options))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(options);
            if (document.RootElement.TryGetProperty("price_id", out var price) && price.TryGetInt32(out var id))
            {
                return id;
            }
        }
        catch (JsonException)
        {
        }

        return 0;
    }

    private static string ReadJsonString(string? json, string name)
    {
        using var document = ParseObject(json);
        if (document is null || !document.RootElement.TryGetProperty(name, out var node))
        {
            return string.Empty;
        }

        return node.ValueKind == JsonValueKind.String ? node.GetString() ?? string.Empty : node.ToString();
    }

    private static List<(int OfficeId, int StorageId)> ReadBunches(string? json)
    {
        var bunches = new List<(int OfficeId, int StorageId)>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return bunches;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return bunches;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                bunches.Add((JsonInt(item, "office_id"), JsonInt(item, "storage_id")));
            }
        }
        catch (JsonException)
        {
        }

        return bunches;
    }

    private static List<string> ReadManufacturerNames(string? json)
    {
        var names = new List<string>();
        using var document = ParseObject(json);
        if (document is null || !document.RootElement.TryGetProperty("manufacturers", out var node) || node.ValueKind != JsonValueKind.Array)
        {
            return names;
        }

        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("manufacturer", out var name))
            {
                var text = name.GetString() ?? string.Empty;
                if (text.Length > 0)
                {
                    names.Add(text);
                }
            }
        }

        return names;
    }

    private static JsonDocument? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                return null;
            }

            return document;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int JsonInt(JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(name, out var node))
        {
            return 0;
        }

        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(node.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static string ProtocolVersion(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("protocol_version", out var node))
        {
            return string.Empty;
        }

        return node.ValueKind == JsonValueKind.Number ? node.ToString() : node.GetString() ?? string.Empty;
    }

    private sealed record PriceRow(
        string Manufacturer,
        string Name,
        int PriceId,
        string Article,
        int Exist,
        decimal Price,
        string Storage,
        string TimeToExe);

    public sealed record AsynchronOutcome(bool PlainText, object Payload)
    {
        public static AsynchronOutcome Json(object payload) => new(false, payload);

        public static AsynchronOutcome Text(string text) => new(true, text);
    }

    public sealed record ManufacturersBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("time")] string Time,
        [property: JsonPropertyName("ProductsManufacturers")] IReadOnlyList<DocpartManufacturerJson> ProductsManufacturers,
        [property: JsonPropertyName("storage")] object Storage);

    public sealed record DocpartManufacturerJson(
        [property: JsonPropertyName("manufacturer")] string Manufacturer,
        [property: JsonPropertyName("manufacturer_id")] int ManufacturerId,
        [property: JsonPropertyName("manufacturer_show")] string ManufacturerShow,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("storage_id")] int StorageId,
        [property: JsonPropertyName("office_id")] int OfficeId,
        [property: JsonPropertyName("synonyms_single_query")] bool SynonymsSingleQuery,
        [property: JsonPropertyName("params")] Dictionary<string, string> Params,
        [property: JsonPropertyName("valid")] bool Valid);

    public sealed record AnalogsBody(
        [property: JsonPropertyName("result")] int Result,
        [property: JsonPropertyName("check")] int Check,
        [property: JsonPropertyName("analogs")] IReadOnlyList<AnalogRow> Analogs,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("time")] string? Time,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("message")] string? Message);

    public sealed record AnalogRow(
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("manufacturer")] string Manufacturer,
        [property: JsonPropertyName("type")] string Type);

    public sealed record AsynchronBody(
        [property: JsonPropertyName("result")] int Result,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("data")] object? Data,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("time")] string? Time);

    public sealed record PartInfoBody(
        [property: JsonPropertyName("result")] int Result);

    public sealed record Bunch2Body(
        [property: JsonPropertyName("result")] int Result,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("Products")] IReadOnlyList<Bunch2Product> Products,
        [property: JsonPropertyName("user_id")] int UserId,
        [property: JsonPropertyName("group_id")] int GroupId,
        [property: JsonPropertyName("prices_visible")] bool PricesVisible);

    public sealed record Bunch2Product(
        [property: JsonPropertyName("manufacturer")] string Manufacturer,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("exist")] int Exist,
        [property: JsonPropertyName("time_to_exe")] string TimeToExe,
        [property: JsonPropertyName("storage")] string Storage,
        [property: JsonPropertyName("storage_id")] int StorageId,
        [property: JsonPropertyName("price_id")] int PriceId,
        [property: JsonPropertyName("office_id")] int OfficeId);
}
