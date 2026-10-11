using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-swell multi-vendor price ingest. PHP identifiers kept for the inventory:
/// <c>epc_multivendor_header_aliases</c>, <c>epc_multivendor_normalize_data_type</c>,
/// <c>epc_multivendor_is_combine_mode</c>, <c>epc_multivendor_resolve_data_type_mode</c>,
/// <c>epc_multivendor_data_type_list_suffix</c>, <c>epc_multivendor_collapse_product_candidates</c>,
/// <c>epc_multivendor_map_headers</c>, <c>epc_multivendor_sanitize_short</c>,
/// <c>epc_multivendor_sanitize_full</c>, <c>epc_multivendor_vendor_key</c>,
/// <c>epc_multivendor_list_base_name</c>, <c>epc_multivendor_ensure_warehouse</c>,
/// <c>epc_multivendor_read_source_rows</c>, <c>epc_multivendor_group_by_vendor</c>,
/// <c>epc_multivendor_write_docpart_csv</c>, <c>epc_multivendor_import_vendor</c>,
/// <c>epc_multivendor_import_csv_local</c>, <c>epc_multivendor_ingest_file</c>,
/// <c>epc_multivendor_ingest_for_vendor</c>, <c>epc_vendor_portal_sample_csv</c>,
/// <c>epc_multivendor_sample_csv</c>, <c>epc_multivendor_vendor_codes_list</c>,
/// <c>epc_multivendor_vendor_code_save</c>.
/// Path: <c>content/shop/docpart/epc_multivendor_price_ingest.php</c>.
/// GET never mints a session cookie. History / import / extra-field / ACL parents stay injected.
/// Commerce helpers are the Surge twin. Do not write leftover finance CRM helpers.
/// </summary>
public static class PhpPlanQ1Swell
{
    public const string MultivendorPriceIngestPath = "content/shop/docpart/epc_multivendor_price_ingest.php";
    public const string EpcMvMinTier = "epc_mv_min";
    public const string EpcMvMaxTier = "epc_mv_max";

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SlashHash = new(@"[/#'""\\]+", RegexOptions.Compiled);
    private static readonly Regex DashUnder = new(@"[\s\-_]+", RegexOptions.Compiled);
    private static readonly Regex NameSkip = new(@"\b(vendor|supplier|warehouse|company|legal|short)\b", RegexOptions.Compiled);
    private static readonly Regex DecPair = new(@"^-?\d+[.,]\d+$", RegexOptions.Compiled);
    private static readonly Regex NotDigit = new(@"[^0-9]", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Func<int>? Clock { get; set; }
    public static Func<object?, int>? ParseStock { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>>? ResolveOrCreateList { get; set; }
    public static Action<MySqlConnection, string, int>? LinkStorageToList { get; set; }
    public static Func<string, int, string, string>? HistoryArchive { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>, int>? HistorySave { get; set; }
    public static Func<MySqlConnection, int, int>? HistoryCountBrands { get; set; }
    public static Action<MySqlConnection, int, int>? HistorySetActive { get; set; }
    public static Func<int>? ShopCurrency { get; set; }
    public static Func<bool>? ArticleSearchReady { get; set; }

    public static void Reset()
    {
        Clock = null;
        ParseStock = null;
        ResolveOrCreateList = null;
        LinkStorageToList = null;
        HistoryArchive = null;
        HistorySave = null;
        HistoryCountBrands = null;
        HistorySetActive = null;
        ShopCurrency = null;
        ArticleSearchReady = null;
    }

    public static Dictionary<string, List<string>> EpcMultivendorHeaderAliases() => new(StringComparer.Ordinal)
    {
        ["manufacturer"] = ["manufacturer", "brand", "mfr", "make", "brend", "vendor_brand"],
        ["article"] = ["article", "sku", "number", "part", "partno", "part_number", "part number", "oem", "code", "item_code", "articlenumber"],
        ["name"] = ["name", "description", "title", "product", "item", "product_name", "item_name"],
        ["exist"] = ["exist", "qty", "quantity", "stock", "available", "onhand", "on_hand", "on hand", "balance", "qty_on_hand"],
        ["price"] = ["price", "sales_price", "sales price", "selling_price", "selling price", "unit_price", "unit price", "amount", "sale", "sales", "retail"],
        ["vendor_full"] =
        [
            "vendor_full", "vendor full", "vendor_full_name", "vendor full name",
            "supplier_full", "supplier full", "supplier_full_name", "supplier full name",
            "warehouse_full", "warehouse full", "warehouse_full_name", "full_vendor",
            "full vendor", "vendor_name_full", "company", "company_name", "company name",
            "legal_name", "legal name"
        ],
        ["vendor_short"] =
        [
            "vendor_short", "vendor short", "vendor_short_name", "vendor short name",
            "vendor_code", "vendor code", "supplier_code", "supplier code",
            "supplier_short", "supplier short", "supplier_short_name", "supplier short name",
            "warehouse", "warehouse_short", "warehouse short", "warehouse_name", "warehouse name",
            "short_name", "short name", "short_vendor", "short vendor", "wh", "wh_code",
            "wh code", "storage", "storage_short", "storage short"
        ],
        ["data_type"] = ["data_type", "data type", "datatype", "price_type", "price type", "list_type", "list type", "channel", "doc_type", "doc type"],
        ["time_to_exe"] = ["time_to_exe", "delivery", "days", "lead_time", "lead time", "term"],
        ["min_order"] = ["min_order", "min order", "moq", "minimum_order", "minimum order"]
    };

    public static string EpcMultivendorNormalizeDataType(string raw, string fallback = "inventory")
    {
        raw = raw.Trim().ToLowerInvariant();
        raw = Spaces.Replace(raw, " ");
        raw = raw.Replace("-", " ", StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal);
        if (raw is "" or "default" or "stock")
        {
            raw = fallback.Trim().ToLowerInvariant();
        }

        if (raw is "inventory" or "inv" or "wh" or "warehouse" or "stock on hand" or "on hand"
            || raw.StartsWith("invent", StringComparison.Ordinal))
        {
            return "inventory";
        }

        if (raw is "sales" or "sale" or "sell" or "selling" or "retail" or "offer"
            || raw.StartsWith("sale", StringComparison.Ordinal))
        {
            return "sales";
        }

        if (raw is "purchase" or "purchases" or "buy" or "buying" or "cost" or "procurement" or "po"
            || raw.StartsWith("purch", StringComparison.Ordinal) || raw.StartsWith("buy", StringComparison.Ordinal))
        {
            return "purchase";
        }

        var fb = fallback.Trim().ToLowerInvariant();
        return fb is "inventory" or "sales" or "purchase" ? fb : "inventory";
    }

    public static bool EpcMultivendorIsCombineMode(string raw)
    {
        raw = raw.Trim().ToLowerInvariant();
        raw = raw.Replace("-", " ", StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal);
        raw = Spaces.Replace(raw, " ");
        return raw is "combine" or "combined" or "mixed" or "mix" or "auto" or "all"
            or "from file" or "fromfile" or "one file" or "onefile" or "per row" or "perrow";
    }

    public static string EpcMultivendorResolveDataTypeMode(string raw)
        => EpcMultivendorIsCombineMode(raw) || raw.Trim() == "" ? "combine" : EpcMultivendorNormalizeDataType(raw, "inventory");

    public static string EpcMultivendorDataTypeListSuffix(string dataType)
        => dataType == "sales" ? " · Sales" : dataType == "purchase" ? " · Purchase" : "";

    public static List<Dictionary<string, object?>> EpcMultivendorCollapseProductCandidates(
        IReadOnlyList<Dictionary<string, object?>> candidates,
        string dataType)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        if (candidates.Count == 1)
        {
            return [Copy(candidates[0])];
        }

        if (dataType == "inventory")
        {
            var keep = Copy(candidates[0]);
            var sumExist = 0;
            double? minPrice = null;
            foreach (var row in candidates)
            {
                sumExist += IntOf(row, "exist");
                var p = Num(row, "price");
                if (p > 0 && (minPrice is null || p < minPrice))
                {
                    minPrice = p;
                    keep = Copy(row);
                }
                else if (minPrice is null)
                {
                    keep = Copy(row);
                }

                if (PhpBytes(Str(row, "name")) > PhpBytes(Str(keep, "name")))
                {
                    keep["name"] = Str(row, "name");
                }
            }

            keep["exist"] = sumExist;
            if (minPrice is not null)
            {
                keep["price"] = minPrice;
            }

            return [keep];
        }

        Dictionary<string, object?>? minRow = null;
        Dictionary<string, object?>? maxRow = null;
        var totalExist = 0;
        foreach (var row in candidates)
        {
            totalExist += IntOf(row, "exist");
            var p = Num(row, "price");
            if (minRow is null || p < Num(minRow, "price")
                || (p == Num(minRow, "price") && IntOf(row, "exist") > IntOf(minRow, "exist")))
            {
                minRow = Copy(row);
            }

            if (maxRow is null || p > Num(maxRow, "price")
                || (p == Num(maxRow, "price") && IntOf(row, "exist") > IntOf(maxRow, "exist")))
            {
                maxRow = Copy(row);
            }
        }

        if (minRow is null)
        {
            return [];
        }

        if (maxRow is null || Num(minRow, "price") == Num(maxRow, "price"))
        {
            minRow["exist"] = totalExist > 0 ? totalExist : IntOf(minRow, "exist");
            minRow["storage"] = EpcMvMaxTier;
            minRow["epc_price_tier"] = "max";
            return [minRow];
        }

        minRow["exist"] = totalExist;
        maxRow["exist"] = totalExist;
        minRow["storage"] = EpcMvMinTier;
        minRow["epc_price_tier"] = "min";
        maxRow["storage"] = EpcMvMaxTier;
        maxRow["epc_price_tier"] = "max";
        return [minRow, maxRow];
    }

    public static Dictionary<string, int> EpcMultivendorMapHeaders(IReadOnlyList<string> headerRow)
    {
        var aliases = EpcMultivendorHeaderAliases();
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var role in aliases.Keys)
        {
            map[role] = -1;
        }

        var usedCols = new HashSet<int>();
        foreach (var (role, list) in aliases)
        {
            var listSorted = list.Select((alias, i) => (alias, i))
                .OrderByDescending(x => PhpBytes(x.alias))
                .ThenBy(x => x.i)
                .Select(x => x.alias)
                .ToList();
            for (var idx = 0; idx < headerRow.Count; idx++)
            {
                if (usedCols.Contains(idx))
                {
                    continue;
                }

                var norm = PhpPlanQ1Surge.EpcCommerceNormalizeHeaderCell(headerRow[idx] ?? "");
                if (norm == "")
                {
                    continue;
                }

                foreach (var alias in listSorted)
                {
                    if (norm == alias)
                    {
                        map[role] = idx;
                        usedCols.Add(idx);
                        goto NextRole;
                    }
                }
            }

            NextRole:;
        }

        string[] roleOrder = ["vendor_full", "vendor_short", "data_type", "manufacturer", "article", "exist", "price", "time_to_exe", "min_order", "name"];
        foreach (var role in roleOrder)
        {
            if (!aliases.TryGetValue(role, out var list) || map[role] >= 0)
            {
                continue;
            }

            var listSorted = list.Select((alias, i) => (alias, i))
                .OrderByDescending(x => PhpBytes(x.alias))
                .ThenBy(x => x.i)
                .Select(x => x.alias)
                .ToList();
            for (var idx = 0; idx < headerRow.Count; idx++)
            {
                if (usedCols.Contains(idx))
                {
                    continue;
                }

                var norm = PhpPlanQ1Surge.EpcCommerceNormalizeHeaderCell(headerRow[idx] ?? "");
                if (norm == "")
                {
                    continue;
                }

                if (role == "name" && NameSkip.IsMatch(norm))
                {
                    continue;
                }

                foreach (var alias in listSorted)
                {
                    if (alias == "" || PhpBytes(alias) < 2)
                    {
                        continue;
                    }

                    if (norm == alias || Regex.IsMatch(norm, @"(^|[^a-z0-9])" + Regex.Escape(alias) + @"([^a-z0-9]|$)"))
                    {
                        map[role] = idx;
                        usedCols.Add(idx);
                        goto NextPass2;
                    }
                }
            }

            NextPass2:;
        }

        return map;
    }

    public static string EpcMultivendorSanitizeShort(string raw)
    {
        raw = raw.Trim();
        raw = Spaces.Replace(raw, " ");
        raw = SlashHash.Replace(raw, "");
        raw = raw.Trim();
        return ClipRunes(raw, 64);
    }

    public static string EpcMultivendorSanitizeFull(string raw)
    {
        raw = raw.Trim();
        raw = Spaces.Replace(raw, " ");
        raw = raw.Trim();
        return ClipRunes(raw, 255);
    }

    public static string EpcMultivendorVendorKey(string full, string shortName = "")
    {
        var fullN = Spaces.Replace(full.Trim(), " ").ToUpperInvariant();
        var shortN = Spaces.Replace(shortName.Trim(), " ").ToUpperInvariant();
        if (shortN == "" && fullN != "")
        {
            shortN = fullN;
        }

        if (shortN == "")
        {
            return "";
        }

        if (fullN == "")
        {
            fullN = shortN;
        }

        return fullN + "\x1e" + shortN;
    }

    public static string EpcMultivendorListBaseName(string vendorShort, string vendorFull)
    {
        vendorShort = EpcMultivendorSanitizeShort(vendorShort);
        vendorFull = EpcMultivendorSanitizeFull(vendorFull);
        if (vendorShort == "")
        {
            return vendorFull != "" ? vendorFull : "Vendor";
        }

        if (vendorFull == "" || string.Equals(vendorFull, vendorShort, StringComparison.OrdinalIgnoreCase))
        {
            return vendorShort;
        }

        var suffix = vendorFull;
        if (RuneCount(suffix) > 90)
        {
            suffix = ClipRunes(suffix, 90);
        }

        return vendorShort + " · " + suffix;
    }

    public static int EpcMultivendorEnsureWarehouse(
        MySqlConnection? db,
        string vendorFull,
        string vendorShort,
        int priceId,
        bool setPrimaryPriceId = true)
    {
        vendorFull = EpcMultivendorSanitizeFull(vendorFull);
        vendorShort = EpcMultivendorSanitizeShort(vendorShort);
        if (db is null || vendorShort == "" || priceId <= 0)
        {
            return 0;
        }

        if (vendorFull == "")
        {
            vendorFull = vendorShort;
        }

        Dictionary<string, object?>? row = null;
        try
        {
            using var q = db.CreateCommand();
            q.CommandText = """
                SELECT `id`, `name`, `short_name`, `connection_options`
                FROM `shop_storages`
                WHERE UPPER(TRIM(`name`)) = UPPER(@f)
                  AND UPPER(TRIM(`short_name`)) = UPPER(@s)
                LIMIT 1
                """;
            q.Parameters.AddWithValue("@f", vendorFull);
            q.Parameters.AddWithValue("@s", vendorShort);
            using var reader = q.ExecuteReader();
            if (reader.Read())
            {
                row = ReadRow(reader);
            }
        }
        catch
        {
            row = null;
        }

        if (row is null)
        {
            try
            {
                using var q = db.CreateCommand();
                q.CommandText = """
                    SELECT `id`, `name`, `short_name`, `connection_options`
                    FROM `shop_storages`
                    WHERE UPPER(TRIM(`name`)) = UPPER(@f)
                      AND (TRIM(COALESCE(`short_name`, '')) = '' OR UPPER(TRIM(`short_name`)) = UPPER(@s))
                    LIMIT 1
                    """;
                q.Parameters.AddWithValue("@f", vendorFull);
                q.Parameters.AddWithValue("@s", vendorShort);
                using var reader = q.ExecuteReader();
                if (reader.Read())
                {
                    row = ReadRow(reader);
                }
            }
            catch
            {
                row = null;
            }
        }

        var opts = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["probability"] = "100",
            ["epc_mv_vendor_full"] = vendorFull,
            ["epc_mv_vendor_code"] = vendorShort
        };
        if (setPrimaryPriceId)
        {
            opts["price_id"] = priceId.ToString(CultureInfo.InvariantCulture);
        }

        int storageId;
        if (row is not null)
        {
            storageId = IntOf(row, "id");
            var existing = DecodeOpts(Str(row, "connection_options"));
            if (existing.Count > 0)
            {
                foreach (var (k, v) in opts)
                {
                    existing[k] = v;
                }

                opts = existing;
                if (!setPrimaryPriceId && existing.TryGetValue("price_id", out var keep) && Convert.ToString(keep, CultureInfo.InvariantCulture) is { Length: > 0 })
                {
                    opts["price_id"] = Convert.ToString(keep, CultureInfo.InvariantCulture);
                }
                else if (setPrimaryPriceId)
                {
                    opts["price_id"] = priceId.ToString(CultureInfo.InvariantCulture);
                }
            }

            var typed = TypedIds(opts);
            typed[priceId.ToString(CultureInfo.InvariantCulture)] = true;
            opts["epc_typed_price_ids"] = typed.Keys.ToList();
            using var upd = db.CreateCommand();
            upd.CommandText = "UPDATE `shop_storages` SET `name` = @n, `short_name` = @s, `interface_type` = 2, `connection_options` = @o, `hidden` = 0 WHERE `id` = @id";
            upd.Parameters.AddWithValue("@n", vendorFull);
            upd.Parameters.AddWithValue("@s", vendorShort);
            upd.Parameters.AddWithValue("@o", JsonSerializer.Serialize(opts, JsonOpts));
            upd.Parameters.AddWithValue("@id", storageId);
            upd.ExecuteNonQuery();
        }
        else
        {
            var currency = ShopCurrency?.Invoke() ?? 784;
            var users = "[]";
            try
            {
                using var admin = db.CreateCommand();
                admin.CommandText = "SELECT `id` FROM `users` WHERE `user_type` = 2 ORDER BY `id` ASC LIMIT 1";
                var adminId = admin.ExecuteScalar();
                if (adminId is not null && adminId is not DBNull && Convert.ToInt32(adminId, CultureInfo.InvariantCulture) > 0)
                {
                    users = JsonSerializer.Serialize(new[] { Convert.ToInt32(adminId, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) });
                }
            }
            catch
            {
                // ignore
            }

            if (!opts.ContainsKey("price_id"))
            {
                opts["price_id"] = priceId.ToString(CultureInfo.InvariantCulture);
            }

            opts["epc_typed_price_ids"] = new List<string> { priceId.ToString(CultureInfo.InvariantCulture) };
            using var ins = db.CreateCommand();
            ins.CommandText = "INSERT INTO `shop_storages` (`name`, `interface_type`, `users`, `connection_options`, `currency`, `short_name`, `hidden`, `bg_line_color`) VALUES (@n, 2, @u, @o, @c, @s, 0, 0)";
            ins.Parameters.AddWithValue("@n", vendorFull);
            ins.Parameters.AddWithValue("@u", users);
            ins.Parameters.AddWithValue("@o", JsonSerializer.Serialize(opts, JsonOpts));
            ins.Parameters.AddWithValue("@c", currency);
            ins.Parameters.AddWithValue("@s", vendorShort);
            ins.ExecuteNonQuery();
            storageId = (int)ins.LastInsertedId;
        }

        if (storageId <= 0)
        {
            return 0;
        }

        if (setPrimaryPriceId)
        {
            var listBase = EpcMultivendorListBaseName(vendorShort, vendorFull);
            (LinkStorageToList ?? ((_, _, _) => { }))(db, listBase, priceId);
            (LinkStorageToList ?? ((_, _, _) => { }))(db, vendorShort, priceId);
            if (!string.Equals(vendorFull, vendorShort, StringComparison.OrdinalIgnoreCase))
            {
                (LinkStorageToList ?? ((_, _, _) => { }))(db, vendorFull, priceId);
            }
        }

        try
        {
            using var off = db.CreateCommand();
            off.CommandText = "SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1";
            var officeObj = off.ExecuteScalar();
            var officeId = officeObj is null or DBNull ? 0 : Convert.ToInt32(officeObj, CultureInfo.InvariantCulture);
            if (officeId > 0)
            {
                using var chk = db.CreateCommand();
                chk.CommandText = "SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = @o AND `storage_id` = @s";
                chk.Parameters.AddWithValue("@o", officeId);
                chk.Parameters.AddWithValue("@s", storageId);
                if (Convert.ToInt32(chk.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                {
                    try
                    {
                        using var map = db.CreateCommand();
                        map.CommandText = "INSERT INTO `shop_offices_storages_map` (`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`) VALUES (@o, @s, 2, 0, 999999999, 0, 0)";
                        map.Parameters.AddWithValue("@o", officeId);
                        map.Parameters.AddWithValue("@s", storageId);
                        map.ExecuteNonQuery();
                    }
                    catch
                    {
                        try
                        {
                            using var map2 = db.CreateCommand();
                            map2.CommandText = "INSERT INTO `shop_offices_storages_map` (`office_id`, `storage_id`) VALUES (@o, @s)";
                            map2.Parameters.AddWithValue("@o", officeId);
                            map2.Parameters.AddWithValue("@s", storageId);
                            map2.ExecuteNonQuery();
                        }
                        catch
                        {
                            // ignore schema differences
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore
        }

        return storageId;
    }

    public static Dictionary<string, object?> EpcMultivendorReadSourceRows(string csvPath, string defaultDataType = "combine")
    {
        var mode = EpcMultivendorResolveDataTypeMode(defaultDataType);
        var combine = mode == "combine";
        var rowFallback = combine ? "" : mode;
        var delimiter = PhpPlanQ1Surge.EpcCommerceDetectDelimiter(csvPath);
        List<string[]> rawRows;
        try
        {
            rawRows = ReadCsv(csvPath, delimiter[0]);
        }
        catch
        {
            return FailRead("Cannot open file", [], [], mode);
        }

        if (rawRows.Count == 0 || rawRows[0].Length == 0)
        {
            return FailRead("Missing header row", [], [], mode);
        }

        var header = rawRows[0].ToList();
        var map = EpcMultivendorMapHeaders(header);
        if (map["article"] < 0)
        {
            return FailRead("Could not find Article/SKU column", header, map, mode);
        }

        if (map["price"] < 0)
        {
            return FailRead("Could not find Price column", header, map, mode);
        }

        if (map["vendor_short"] < 0)
        {
            return FailRead("Could not find Vendor short / Warehouse short column (customer-facing warehouse name)", header, map, mode);
        }

        if (map["vendor_full"] < 0)
        {
            return FailRead("Could not find Vendor full name column (backend-only warehouse name)", header, map, mode);
        }

        if (combine && map["data_type"] < 0)
        {
            return FailRead("Combine mode needs a Data type column (inventory / sales / purchase) so one file can load all three. Or pick a single type override in the form.", header, map, mode);
        }

        var rows = new List<Dictionary<string, object?>>();
        var skipped = 0;
        var skippedNoType = 0;
        for (var i = 1; i < rawRows.Count; i++)
        {
            var raw = rawRows[i];
            if (raw.Length == 0 || (raw.Length == 1 && raw[0].Trim() == ""))
            {
                continue;
            }

            var articleShow = Cell(raw, map["article"]).Trim();
            var article = PhpPlanQ1Surge.EpcCommerceNormalizeArticle(articleShow);
            var price = PhpPlanQ1Surge.EpcCommerceParseNumber(Cell(raw, map["price"]));
            var vendorShort = EpcMultivendorSanitizeShort(Cell(raw, map["vendor_short"]));
            var vendorFull = EpcMultivendorSanitizeFull(Cell(raw, map["vendor_full"]));
            if (article == "" || price <= 0 || vendorShort == "")
            {
                skipped++;
                continue;
            }

            if (vendorFull == "")
            {
                vendorFull = vendorShort;
            }

            var manufacturer = map["manufacturer"] >= 0 ? PhpPlanQ1Surge.EpcCommerceClip(Cell(raw, map["manufacturer"]).Trim()) : "";
            var name = map["name"] >= 0 ? PhpPlanQ1Surge.EpcCommerceClip(Cell(raw, map["name"]).Trim()) : "";
            var exist = map["exist"] >= 0 ? StockQty(Cell(raw, map["exist"])) : 0;
            var timeToExe = map["time_to_exe"] >= 0 ? (int)PhpPlanQ1Surge.EpcCommerceParseNumber(Cell(raw, map["time_to_exe"])) : 0;
            var minOrder = map["min_order"] >= 0 ? (int)PhpPlanQ1Surge.EpcCommerceParseNumber(Cell(raw, map["min_order"])) : 0;
            var dataTypeRaw = map["data_type"] >= 0 ? Cell(raw, map["data_type"]).Trim() : "";
            string dataType;
            if (combine)
            {
                if (dataTypeRaw == "")
                {
                    skippedNoType++;
                    skipped++;
                    continue;
                }

                var normalizedProbe = DashUnder.Replace(dataTypeRaw, " ").ToLowerInvariant();
                var known = normalizedProbe is "inventory" or "inv" or "sales" or "sale" or "purchase" or "buy"
                    || normalizedProbe.StartsWith("invent", StringComparison.Ordinal)
                    || normalizedProbe.StartsWith("sale", StringComparison.Ordinal)
                    || normalizedProbe.StartsWith("purch", StringComparison.Ordinal)
                    || normalizedProbe.StartsWith("buy", StringComparison.Ordinal);
                if (!known)
                {
                    skippedNoType++;
                    skipped++;
                    continue;
                }

                dataType = EpcMultivendorNormalizeDataType(dataTypeRaw, "inventory");
            }
            else
            {
                dataType = EpcMultivendorNormalizeDataType(dataTypeRaw, rowFallback != "" ? rowFallback : "inventory");
            }

            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["manufacturer"] = manufacturer,
                ["article"] = article,
                ["article_show"] = PhpPlanQ1Surge.EpcCommerceClip(articleShow),
                ["name"] = name,
                ["exist"] = exist,
                ["price"] = price,
                ["time_to_exe"] = Math.Max(0, timeToExe),
                ["min_order"] = Math.Max(0, minOrder),
                ["vendor_full"] = vendorFull,
                ["vendor_short"] = vendorShort,
                ["vendor_key"] = EpcMultivendorVendorKey(vendorFull, vendorShort),
                ["data_type"] = dataType,
                ["extras"] = new Dictionary<string, string>(StringComparer.Ordinal)
            });
        }

        if (rows.Count == 0)
        {
            var msg = "No valid product rows found";
            if (combine && skippedNoType > 0)
            {
                msg = "Combine mode: every row needs Data type = inventory, sales, or purchase ("
                    + skippedNoType + " row" + (skippedNoType == 1 ? "" : "s") + " missing/invalid type).";
            }

            return FailRead(msg, header, map, mode);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["rows"] = rows,
            ["message"] = "OK",
            ["headers"] = header,
            ["map"] = map,
            ["extra_columns"] = new Dictionary<int, string>(),
            ["mode"] = mode,
            ["rows_skipped"] = skipped,
            ["rows_skipped_no_type"] = skippedNoType
        };
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcMultivendorGroupByVendor(
        IEnumerable<Dictionary<string, object?>> rows)
    {
        var groups = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var buckets = new Dictionary<string, Dictionary<string, List<Dictionary<string, object?>>>>(StringComparer.Ordinal);
        foreach (var incoming in rows)
        {
            var vendorFull = EpcMultivendorSanitizeFull(Str(incoming, "vendor_full"));
            var vendorShort = EpcMultivendorSanitizeShort(Str(incoming, "vendor_short"));
            var vendorKey = Str(incoming, "vendor_key");
            if (vendorKey == "")
            {
                vendorKey = EpcMultivendorVendorKey(vendorFull, vendorShort);
            }

            if (vendorKey == "" || vendorShort == "")
            {
                continue;
            }

            var dataType = EpcMultivendorNormalizeDataType(Str(incoming, "data_type", "inventory"));
            var row = Copy(incoming);
            row["data_type"] = dataType;
            row["vendor_full"] = vendorFull != "" ? vendorFull : vendorShort;
            row["vendor_short"] = vendorShort;
            row["vendor_key"] = vendorKey;
            var key = vendorKey + "|" + dataType;
            if (!groups.TryGetValue(key, out var group))
            {
                group = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["vendor_full"] = Str(row, "vendor_full"),
                    ["vendor_short"] = Str(row, "vendor_short"),
                    ["data_type"] = dataType
                };
                groups[key] = group;
                buckets[key] = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
            }
            else if (PhpBytes(Str(row, "vendor_full")) > PhpBytes(Str(group, "vendor_full")))
            {
                group["vendor_full"] = Str(row, "vendor_full");
            }

            var prodKey = Str(row, "manufacturer").ToUpperInvariant() + "|" + Str(row, "article");
            if (!buckets[key].TryGetValue(prodKey, out var list))
            {
                list = [];
                buckets[key][prodKey] = list;
            }

            list.Add(row);
        }

        foreach (var (key, group) in groups)
        {
            var collapsed = new List<Dictionary<string, object?>>();
            var dataType = Str(group, "data_type", "inventory");
            foreach (var candidates in buckets[key].Values)
            {
                collapsed.AddRange(EpcMultivendorCollapseProductCandidates(candidates, dataType));
            }

            group["products"] = collapsed;
        }

        return groups;
    }

    public static bool EpcMultivendorWriteDocpartCsv(string path, IEnumerable<Dictionary<string, object?>> products)
    {
        try
        {
            using var fh = new StreamWriter(path, false, new UTF8Encoding(false));
            fh.Write("Brand,Number,Name,Qty,Price,Delivery,MinOrder,Storage,ExtraJSON\n");
            foreach (var p in products)
            {
                var tier = (Str(p, "epc_price_tier") != "" ? Str(p, "epc_price_tier") : Str(p, "storage")).Trim().ToLowerInvariant();
                string storage;
                if (tier is "min" || tier == EpcMvMinTier)
                {
                    storage = EpcMvMinTier;
                }
                else if (tier is "max" || tier == EpcMvMaxTier)
                {
                    storage = EpcMvMaxTier;
                }
                else
                {
                    storage = Str(p, "storage").Trim();
                }

                var show = Str(p, "article_show");
                var number = show != "" ? show : Str(p, "article");
                fh.Write(PhpCsvLine([
                    Str(p, "manufacturer"),
                    number,
                    Str(p, "name"),
                    IntOf(p, "exist").ToString(CultureInfo.InvariantCulture),
                    Num(p, "price").ToString("0.00", CultureInfo.InvariantCulture),
                    IntOf(p, "time_to_exe").ToString(CultureInfo.InvariantCulture),
                    IntOf(p, "min_order").ToString(CultureInfo.InvariantCulture),
                    storage,
                    ""
                ]) + "\n");
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static Dictionary<string, object?> EpcMultivendorImportVendor(MySqlConnection db, Dictionary<string, object?> group)
    {
        var vendorShort = EpcMultivendorSanitizeShort(Str(group, "vendor_short"));
        var vendorFull = EpcMultivendorSanitizeFull(Str(group, "vendor_full"));
        var dataType = EpcMultivendorNormalizeDataType(Str(group, "data_type", "inventory"));
        var products = group.TryGetValue("products", out var raw) && raw is List<Dictionary<string, object?>> list ? list : [];
        if (vendorShort == "" || products.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = "Empty vendor group",
                ["vendor_short"] = vendorShort
            };
        }

        if (vendorFull == "")
        {
            vendorFull = vendorShort;
        }

        var listName = EpcMultivendorListBaseName(vendorShort, vendorFull) + EpcMultivendorDataTypeListSuffix(dataType);
        var price = (ResolveOrCreateList ?? DefaultResolve)(db, listName);
        if (price.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = "Could not create price list for " + vendorShort,
                ["vendor_short"] = vendorShort,
                ["vendor_full"] = vendorFull
            };
        }

        var priceId = IntOf(price, "id");
        var storageId = EpcMultivendorEnsureWarehouse(db, vendorFull, vendorShort, priceId, dataType == "inventory");
        var tmpCsv = Path.Combine(Path.GetTempPath(), "epc_mv_" + Environment.ProcessId + "_" + UnixTime() + "_" + Random.Shared.Next(1000, 9999) + ".csv");
        if (!EpcMultivendorWriteDocpartCsv(tmpCsv, products))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = "Could not write temp CSV",
                ["vendor_short"] = vendorShort,
                ["vendor_full"] = vendorFull,
                ["price_id"] = priceId
            };
        }

        var result = EpcMultivendorImportCsvLocal(db, price, tmpCsv);
        var recordsInDb = 0;
        using (var countQ = db.CreateCommand())
        {
            countQ.CommandText = "SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = @p";
            countQ.Parameters.AddWithValue("@p", priceId);
            recordsInDb = Convert.ToInt32(countQ.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        var uploadedBy = IntOf(group, "uploaded_by");
        var uploadSource = Str(group, "upload_source");
        if (uploadSource == "")
        {
            uploadSource = "multivendor";
        }

        var storedRel = (HistoryArchive ?? ((_, _, name) => "archive/" + name))(tmpCsv, priceId, vendorShort + ".csv");
        var historyId = (HistorySave ?? ((_, _) => 7))(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["price_id"] = priceId,
            ["price_name"] = listName,
            ["upload_source"] = uploadSource,
            ["original_filename"] = vendorShort + ".csv",
            ["stored_relpath"] = storedRel,
            ["file_size"] = File.Exists(tmpCsv) ? new FileInfo(tmpCsv).Length : 0,
            ["status"] = Truthy(result, "status") ? "ok" : "failed",
            ["rows_imported"] = IntOf(result, "records_handled"),
            ["rows_skipped"] = IntOf(result, "rows_skipped"),
            ["rows_in_db"] = recordsInDb,
            ["brands_count"] = (HistoryCountBrands ?? ((_, _) => 1))(db, priceId),
            ["items_count"] = recordsInDb,
            ["error_text"] = Truthy(result, "status") ? "" : Str(result, "message"),
            ["uploaded_by"] = uploadedBy,
            ["stats_json"] = JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["multivendor"] = true,
                ["vendor_full"] = vendorFull,
                ["vendor_short"] = vendorShort,
                ["data_type"] = dataType,
                ["storage_id"] = storageId,
                ["frontend_vendor"] = uploadSource == "vendor_portal"
            }, JsonOpts)
        });
        if (historyId > 0 && Truthy(result, "status"))
        {
            (HistorySetActive ?? ((_, _, _) => { }))(db, priceId, historyId);
        }

        try { File.Delete(tmpCsv); } catch { /* ignore */ }
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = Truthy(result, "status"),
            ["message"] = Str(result, "message"),
            ["vendor_full"] = vendorFull,
            ["vendor_short"] = vendorShort,
            ["data_type"] = dataType,
            ["price_id"] = priceId,
            ["price_name"] = listName,
            ["storage_id"] = storageId,
            ["records_handled"] = IntOf(result, "records_handled"),
            ["rows_skipped"] = IntOf(result, "rows_skipped"),
            ["records_in_db"] = recordsInDb,
            ["history_id"] = historyId,
            ["warehouse_linked"] = storageId > 0
        };
    }

    public static Dictionary<string, object?> EpcMultivendorImportCsvLocal(
        MySqlConnection db,
        Dictionary<string, object?> price,
        string filePath)
    {
        var priceId = IntOf(price, "id");
        using (var del = db.CreateCommand())
        {
            del.CommandText = "DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = @p";
            del.Parameters.AddWithValue("@p", priceId);
            del.ExecuteNonQuery();
        }

        List<string[]> rawRows;
        try
        {
            rawRows = ReadCsv(filePath, ',');
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = "Cannot open CSV",
                ["records_handled"] = 0,
                ["rows_skipped"] = 0
            };
        }

        var nextId = 0;
        using (var max = db.CreateCommand())
        {
            max.CommandText = "SELECT COALESCE(MAX(`id`), 0) FROM `shop_docpart_prices_data`";
            nextId = Convert.ToInt32(max.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        var hasArticleSearch = ArticleSearchReady?.Invoke() ?? false;
        var inserted = 0;
        var skipped = 0;
        var extrasSaved = 0;
        for (var i = 1; i < rawRows.Count; i++)
        {
            var row = rawRows[i];
            var mfr = PhpPlanQ1Surge.EpcCommerceClip(Cell(row, 0).Trim());
            var show = PhpPlanQ1Surge.EpcCommerceClip(Cell(row, 1).Trim());
            var art = PhpPlanQ1Surge.EpcCommerceNormalizeArticle(show);
            var name = PhpPlanQ1Surge.EpcCommerceClip(Cell(row, 2).Trim());
            var exist = StockQty(Cell(row, 3));
            var priceVal = PhpPlanQ1Surge.EpcCommerceParseNumber(Cell(row, 4));
            var timeToExe = (int)PhpPlanQ1Surge.EpcCommerceParseNumber(Cell(row, 5));
            var minOrder = (int)PhpPlanQ1Surge.EpcCommerceParseNumber(Cell(row, 6));
            var storageRaw = Cell(row, 7).Trim();
            var storage = "";
            if (storageRaw == EpcMvMinTier || string.Equals(storageRaw, "min", StringComparison.OrdinalIgnoreCase))
            {
                storage = EpcMvMinTier;
            }
            else if (storageRaw == EpcMvMaxTier || string.Equals(storageRaw, "max", StringComparison.OrdinalIgnoreCase))
            {
                storage = EpcMvMaxTier;
            }
            else if (storageRaw != "")
            {
                storage = PhpPlanQ1Surge.EpcCommerceClip(storageRaw);
            }

            if (art == "" || priceVal <= 0)
            {
                skipped++;
                continue;
            }

            nextId++;
            using var ins = db.CreateCommand();
            if (hasArticleSearch)
            {
                ins.CommandText = "INSERT INTO `shop_docpart_prices_data` (`id`,`price_id`,`manufacturer`,`article`,`article_search`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`) VALUES (@id,@p,@m,@a,@as,@sh,@n,@e,@pr,@t,@st,@mo)";
                ins.Parameters.AddWithValue("@as", art);
            }
            else
            {
                ins.CommandText = "INSERT INTO `shop_docpart_prices_data` (`id`,`price_id`,`manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`) VALUES (@id,@p,@m,@a,@sh,@n,@e,@pr,@t,@st,@mo)";
            }

            ins.Parameters.AddWithValue("@id", nextId);
            ins.Parameters.AddWithValue("@p", priceId);
            ins.Parameters.AddWithValue("@m", mfr);
            ins.Parameters.AddWithValue("@a", art);
            ins.Parameters.AddWithValue("@sh", show);
            ins.Parameters.AddWithValue("@n", name);
            ins.Parameters.AddWithValue("@e", exist);
            ins.Parameters.AddWithValue("@pr", priceVal);
            ins.Parameters.AddWithValue("@t", Math.Max(0, timeToExe));
            ins.Parameters.AddWithValue("@st", storage);
            ins.Parameters.AddWithValue("@mo", Math.Max(0, minOrder));
            ins.ExecuteNonQuery();
            inserted++;
        }

        using (var upd = db.CreateCommand())
        {
            upd.CommandText = "UPDATE `shop_docpart_prices` SET `last_updated` = @t WHERE `id` = @p";
            upd.Parameters.AddWithValue("@t", UnixTime());
            upd.Parameters.AddWithValue("@p", priceId);
            upd.ExecuteNonQuery();
        }

        try
        {
            using var cntQ = db.CreateCommand();
            cntQ.CommandText = "SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = @p";
            cntQ.Parameters.AddWithValue("@p", priceId);
            var count = Convert.ToInt32(cntQ.ExecuteScalar(), CultureInfo.InvariantCulture);
            using var rec = db.CreateCommand();
            rec.CommandText = "UPDATE `shop_docpart_prices` SET `records_count` = @c WHERE `id` = @p";
            rec.Parameters.AddWithValue("@c", count);
            rec.Parameters.AddWithValue("@p", priceId);
            rec.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = inserted > 0,
            ["message"] = inserted > 0 ? "Import completed" : "No valid rows imported",
            ["records_handled"] = inserted,
            ["rows_skipped"] = skipped,
            ["extras_saved"] = extrasSaved
        };
    }

    public static Dictionary<string, object?> EpcMultivendorIngestFile(
        MySqlConnection db,
        string sourcePath,
        string originalFilename = "",
        string defaultDataType = "combine")
    {
        var mode = EpcMultivendorResolveDataTypeMode(defaultDataType);
        var converted = PhpPlanQ1Surge.EpcCommerceExcelToCsv(sourcePath);
        if (!Truthy(converted, "ok"))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = Str(converted, "message", "File conversion failed"),
                ["vendors"] = new List<Dictionary<string, object?>>()
            };
        }

        var csvPath = Str(converted, "path");
        var read = EpcMultivendorReadSourceRows(csvPath, mode);
        if (!string.Equals(csvPath, sourcePath, StringComparison.Ordinal))
        {
            try { File.Delete(csvPath); } catch { /* ignore */ }
        }

        if (!Truthy(read, "ok"))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = Str(read, "message", "Parse failed"),
                ["headers"] = read.TryGetValue("headers", out var h) ? h : new List<string>(),
                ["vendors"] = new List<Dictionary<string, object?>>()
            };
        }

        var sourceRows = (List<Dictionary<string, object?>>)read["rows"]!;
        var groups = EpcMultivendorGroupByVendor(sourceRows);
        var vendorResults = new List<Dictionary<string, object?>>();
        var okCount = 0;
        var failCount = 0;
        var totalRows = 0;
        var warehousesLinked = 0;
        foreach (var group in groups.Values)
        {
            var one = EpcMultivendorImportVendor(db, group);
            vendorResults.Add(one);
            if (Truthy(one, "status"))
            {
                okCount++;
                totalRows += IntOf(one, "records_handled");
                if (IntOf(one, "storage_id") > 0)
                {
                    warehousesLinked++;
                }
            }
            else
            {
                failCount++;
            }
        }

        var overallOk = okCount > 0;
        var typeLabel = mode == "combine" ? "combine (per-row Data type)" : mode;
        var message = overallOk
            ? "Imported " + okCount + " vendor list" + (okCount == 1 ? "" : "s")
                + " (" + totalRows.ToString("N0", CultureInfo.InvariantCulture) + " rows, mode: " + typeLabel + ")."
                + (failCount > 0 ? " " + failCount + " failed." : "")
            : "No vendors imported. " + (failCount > 0 ? failCount + " failed." : "Check columns.");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = overallOk,
            ["message"] = message,
            ["data_type_default"] = mode,
            ["original_filename"] = originalFilename != "" ? originalFilename : Path.GetFileName(sourcePath),
            ["vendors_total"] = groups.Count,
            ["vendors_ok"] = okCount,
            ["vendors_failed"] = failCount,
            ["rows_source"] = sourceRows.Count,
            ["rows_skipped_source"] = IntOf(read, "rows_skipped"),
            ["rows_imported"] = totalRows,
            ["warehouses_linked"] = warehousesLinked,
            ["headers"] = read.TryGetValue("headers", out var headers) ? headers : new List<string>(),
            ["column_map"] = read.TryGetValue("map", out var cmap) ? cmap : new Dictionary<string, int>(StringComparer.Ordinal),
            ["vendors"] = vendorResults
        };
    }

    public static Dictionary<string, object?> EpcMultivendorIngestForVendor(
        MySqlConnection db,
        string sourcePath,
        string originalFilename,
        string vendorFull,
        string vendorShort,
        string defaultDataType = "inventory",
        int uploadedBy = 0)
    {
        vendorFull = EpcMultivendorSanitizeFull(vendorFull);
        vendorShort = EpcMultivendorSanitizeShort(vendorShort);
        defaultDataType = EpcMultivendorNormalizeDataType(defaultDataType, "inventory");
        if (vendorShort == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = "Vendor code is required",
                ["vendors"] = new List<Dictionary<string, object?>>()
            };
        }

        if (vendorFull == "")
        {
            vendorFull = vendorShort;
        }

        var converted = PhpPlanQ1Surge.EpcCommerceExcelToCsv(sourcePath);
        if (!Truthy(converted, "ok"))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = Str(converted, "message", "File conversion failed"),
                ["vendors"] = new List<Dictionary<string, object?>>()
            };
        }

        var csvPath = Str(converted, "path");
        var read = EpcMultivendorReadSourceRows(csvPath, defaultDataType);
        if (!string.Equals(csvPath, sourcePath, StringComparison.Ordinal))
        {
            try { File.Delete(csvPath); } catch { /* ignore */ }
        }

        if (!Truthy(read, "ok"))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = Str(read, "message", "Parse failed"),
                ["headers"] = read.TryGetValue("headers", out var h) ? h : new List<string>(),
                ["vendors"] = new List<Dictionary<string, object?>>()
            };
        }

        var rows = read["rows"] is List<Dictionary<string, object?>> source ? source : [];
        var forced = new List<Dictionary<string, object?>>();
        var rejectedOtherVendor = 0;
        var accountShort = EpcMultivendorSanitizeShort(vendorShort);
        foreach (var incoming in rows)
        {
            var fileShort = EpcMultivendorSanitizeShort(Str(incoming, "vendor_short"));
            if (fileShort != "" && !string.Equals(fileShort, accountShort, StringComparison.OrdinalIgnoreCase))
            {
                rejectedOtherVendor++;
                continue;
            }

            var row = Copy(incoming);
            row["vendor_full"] = vendorFull;
            row["vendor_short"] = vendorShort;
            row["vendor_key"] = EpcMultivendorVendorKey(vendorFull, vendorShort);
            if (Str(row, "data_type") == "")
            {
                row["data_type"] = defaultDataType;
            }

            forced.Add(row);
        }

        if (forced.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = rejectedOtherVendor > 0
                    ? "No rows for your vendor code. " + rejectedOtherVendor + " rows belonged to other vendors."
                    : "No valid product rows found. Need Brand, Article, Price columns.",
                ["vendors"] = new List<Dictionary<string, object?>>(),
                ["rows_rejected_other_vendor"] = rejectedOtherVendor
            };
        }

        var groups = EpcMultivendorGroupByVendor(forced);
        var vendorResults = new List<Dictionary<string, object?>>();
        var okCount = 0;
        var failCount = 0;
        var totalRows = 0;
        foreach (var group in groups.Values)
        {
            group["vendor_full"] = vendorFull;
            group["vendor_short"] = vendorShort;
            group["uploaded_by"] = uploadedBy;
            group["upload_source"] = "vendor_portal";
            var one = EpcMultivendorImportVendor(db, group);
            vendorResults.Add(one);
            if (Truthy(one, "status"))
            {
                okCount++;
                totalRows += IntOf(one, "records_handled");
            }
            else
            {
                failCount++;
            }
        }

        var overallOk = okCount > 0;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = overallOk,
            ["message"] = overallOk
                ? "Uploaded " + totalRows.ToString("N0", CultureInfo.InvariantCulture) + " rows for " + vendorShort + "."
                    + (rejectedOtherVendor > 0 ? " Skipped " + rejectedOtherVendor + " other-vendor rows." : "")
                : "Upload failed. " + (failCount > 0 ? failCount + " list(s) failed." : "Check columns."),
            ["data_type_default"] = defaultDataType,
            ["original_filename"] = originalFilename != "" ? originalFilename : Path.GetFileName(sourcePath),
            ["vendor_full"] = vendorFull,
            ["vendor_short"] = vendorShort,
            ["vendors_ok"] = okCount,
            ["vendors_failed"] = failCount,
            ["rows_source"] = rows.Count,
            ["rows_imported"] = totalRows,
            ["rows_rejected_other_vendor"] = rejectedOtherVendor,
            ["headers"] = read.TryGetValue("headers", out var headers) ? headers : new List<string>(),
            ["column_map"] = read.TryGetValue("map", out var cmap) ? cmap : new Dictionary<string, int>(StringComparer.Ordinal),
            ["vendors"] = vendorResults
        };
    }

    public static string EpcVendorPortalSampleCsv()
        => CsvOf([
            ["Brand", "Article", "Name", "Qty", "Price", "Data type", "Delivery"],
            ["TOYOTA", "446610010", "PAD KIT, DISC BRAKE", "8", "103.51", "inventory", "0"],
            ["AISIN", "DT068", "WATER PUMP", "3", "45.00", "inventory", "0"],
            ["DENSO", "0671007450", "FILTER", "12", "18.00", "sales", "0"]
        ]);

    public static string EpcMultivendorSampleCsv()
        => CsvOf([
            ["Brand", "Article", "Name", "Qty", "Price", "Vendor full name", "Vendor short", "Data type", "Delivery", "Engine code", "Country code", "Size", "Cross reference", "OE number", "Other information"],
            ["TOYOTA", "446610010", "PAD KIT, DISC BRAKE", "8", "103.51", "S-UAE Trading LLC", "S-UAE", "inventory", "0", "2JZGE", "JP", "15\"", "04465-YZZD2", "044650K090", "Ceramic; front"],
            ["AISIN", "DT068", "WATER PUMP", "3", "45.00", "R-UAE Spare Parts FZE", "R-UAE", "inventory", "0", "1KZTE", "TH", "STD", "16100-69355", "1610069355", ""],
            ["BOSCH", "F026400039", "FILTER", "4", "15.00", "Gulf Parts Trading", "S-UAE", "inventory", "0", "", "DE", "OE", "0986AF0078", "", "Oil filter"],
            ["DENSO", "0671007450", "FILTER", "12", "18.00", "S-UAE Trading LLC", "S-UAE", "sales", "0", "3L", "JP", "", "", "", ""],
            ["DENSO", "0671007450", "FILTER", "5", "22.50", "S-UAE Trading LLC", "S-UAE", "sales", "0", "3L", "JP", "", "", "", ""],
            ["DENSO", "0671007450", "FILTER", "2", "29.90", "S-UAE Trading LLC", "S-UAE", "sales", "0", "3L", "JP", "", "", "", ""]
        ]);

    public static List<Dictionary<string, object?>> EpcMultivendorVendorCodesList(MySqlConnection db)
    {
        List<Dictionary<string, object?>> rows;
        try
        {
            using var st = db.CreateCommand();
            st.CommandText = """
                SELECT `id`, `name`, `short_name`, `hidden`, `connection_options`, `interface_type`
                FROM `shop_storages`
                WHERE `interface_type` = 2
                ORDER BY TRIM(`short_name`) ASC, TRIM(`name`) ASC, `id` ASC
                LIMIT 2000
                """;
            using var reader = st.ExecuteReader();
            rows = [];
            while (reader.Read())
            {
                rows.Add(ReadRow(reader));
            }
        }
        catch
        {
            return [];
        }

        var outbound = new List<Dictionary<string, object?>>();
        foreach (var r in rows)
        {
            var opts = DecodeOpts(Str(r, "connection_options"));
            var priceId = 0;
            if (opts.TryGetValue("price_id", out var pid) && pid is not null)
            {
                int.TryParse(Convert.ToString(pid, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out priceId);
            }

            var isMv = opts.TryGetValue("epc_mv_vendor_code", out var code) && !IsEmpty(code)
                || opts.TryGetValue("epc_typed_price_ids", out var typed) && typed is IEnumerable<object?> en && en.Cast<object?>().Any();
            outbound.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = IntOf(r, "id"),
                ["vendor_full"] = Str(r, "name"),
                ["vendor_code"] = Str(r, "short_name"),
                ["vendor_short"] = Str(r, "short_name"),
                ["hidden"] = IntOf(r, "hidden"),
                ["price_id"] = priceId,
                ["is_multivendor"] = isMv
            });
        }

        return outbound;
    }

    public static Dictionary<string, object?> EpcMultivendorVendorCodeSave(
        MySqlConnection db,
        int storageId,
        string newCode,
        string newFull = "")
    {
        if (storageId <= 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Invalid warehouse id"
            };
        }

        newCode = EpcMultivendorSanitizeShort(newCode);
        if (newCode == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Vendor code cannot be empty"
            };
        }

        Dictionary<string, object?>? row;
        using (var st = db.CreateCommand())
        {
            st.CommandText = "SELECT `id`, `name`, `short_name`, `connection_options` FROM `shop_storages` WHERE `id` = @id LIMIT 1";
            st.Parameters.AddWithValue("@id", storageId);
            using var reader = st.ExecuteReader();
            row = reader.Read() ? ReadRow(reader) : null;
        }

        if (row is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Warehouse not found"
            };
        }

        var oldCode = Str(row, "short_name");
        var full = newFull != "" ? EpcMultivendorSanitizeFull(newFull) : Str(row, "name");
        if (full == "")
        {
            full = newCode;
        }

        using (var chk = db.CreateCommand())
        {
            chk.CommandText = "SELECT `id` FROM `shop_storages` WHERE UPPER(TRIM(`name`)) = UPPER(@f) AND UPPER(TRIM(`short_name`)) = UPPER(@c) AND `id` <> @id LIMIT 1";
            chk.Parameters.AddWithValue("@f", full);
            chk.Parameters.AddWithValue("@c", newCode);
            chk.Parameters.AddWithValue("@id", storageId);
            if (chk.ExecuteScalar() is not null and not DBNull)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = false,
                    ["message"] = "Another warehouse already uses this vendor name + code"
                };
            }
        }

        var opts = DecodeOpts(Str(row, "connection_options"));
        opts["epc_mv_vendor_full"] = full;
        opts["epc_mv_vendor_code"] = newCode;
        using (var upd = db.CreateCommand())
        {
            upd.CommandText = "UPDATE `shop_storages` SET `name` = @n, `short_name` = @s, `connection_options` = @o WHERE `id` = @id";
            upd.Parameters.AddWithValue("@n", full);
            upd.Parameters.AddWithValue("@s", newCode);
            upd.Parameters.AddWithValue("@o", JsonSerializer.Serialize(opts, JsonOpts));
            upd.Parameters.AddWithValue("@id", storageId);
            upd.ExecuteNonQuery();
        }

        var priceIds = new List<int>();
        if (opts.TryGetValue("price_id", out var pid) && pid is not null
            && int.TryParse(Convert.ToString(pid, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var onePid)
            && onePid > 0)
        {
            priceIds.Add(onePid);
        }

        if (opts.TryGetValue("epc_typed_price_ids", out var typedObj) && typedObj is IEnumerable<object?> typedList)
        {
            foreach (var item in typedList)
            {
                if (int.TryParse(Convert.ToString(item, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var t) && t > 0)
                {
                    priceIds.Add(t);
                }
            }
        }

        priceIds = priceIds.Distinct().ToList();
        if (oldCode != "" && priceIds.Count > 0)
        {
            foreach (var pidVal in priceIds)
            {
                try
                {
                    using var pq = db.CreateCommand();
                    pq.CommandText = "SELECT `id`, `name` FROM `shop_docpart_prices` WHERE `id` = @id LIMIT 1";
                    pq.Parameters.AddWithValue("@id", pidVal);
                    using var reader = pq.ExecuteReader();
                    if (!reader.Read())
                    {
                        continue;
                    }

                    var oldName = Convert.ToString(reader["name"], CultureInfo.InvariantCulture) ?? "";
                    reader.Close();
                    var newList = EpcMultivendorListBaseName(newCode, full);
                    foreach (var suf in new[] { "", " · Sales", " · Purchase" })
                    {
                        var candidateOld = EpcMultivendorListBaseName(oldCode, Str(row, "name") != "" ? Str(row, "name") : oldCode) + suf;
                        var candidateOldShort = oldCode + suf;
                        if (oldName == candidateOld || oldName == candidateOldShort || oldName == oldCode + suf)
                        {
                            using var rename = db.CreateCommand();
                            rename.CommandText = "UPDATE `shop_docpart_prices` SET `name` = @n WHERE `id` = @id";
                            rename.Parameters.AddWithValue("@n", newList + suf);
                            rename.Parameters.AddWithValue("@id", pidVal);
                            rename.ExecuteNonQuery();
                            break;
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = "Vendor code updated — storefront shows the new code; CP still shows the vendor name",
            ["vendor"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = storageId,
                ["vendor_full"] = full,
                ["vendor_code"] = newCode,
                ["vendor_short"] = newCode
            }
        };
    }

    private static Dictionary<string, object?> FailRead(string message, List<string> headers, Dictionary<string, int> map, string mode)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["rows"] = new List<Dictionary<string, object?>>(),
            ["message"] = message,
            ["headers"] = headers,
            ["map"] = map,
            ["mode"] = mode
        };

    private static Dictionary<string, object?> DefaultResolve(MySqlConnection db, string listName)
    {
        using (var q = db.CreateCommand())
        {
            q.CommandText = "SELECT * FROM `shop_docpart_prices` WHERE `name` = @n LIMIT 1";
            q.Parameters.AddWithValue("@n", listName);
            using var reader = q.ExecuteReader();
            if (reader.Read())
            {
                return ReadRow(reader);
            }
        }

        using var ins = db.CreateCommand();
        ins.CommandText = "INSERT INTO `shop_docpart_prices` (`name`) VALUES (@n)";
        ins.Parameters.AddWithValue("@n", listName);
        ins.ExecuteNonQuery();
        var newId = (int)ins.LastInsertedId;
        using var q2 = db.CreateCommand();
        q2.CommandText = "SELECT * FROM `shop_docpart_prices` WHERE `id` = @id";
        q2.Parameters.AddWithValue("@id", newId);
        using var reader2 = q2.ExecuteReader();
        reader2.Read();
        return ReadRow(reader2);
    }

    private static Dictionary<string, object?> ReadRow(MySqlDataReader reader)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static Dictionary<string, object?> DecodeOpts(string raw)
    {
        if (raw.Trim() == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonToDict(doc.RootElement);
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }

    private static Dictionary<string, object?> JsonToDict(JsonElement el)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (el.ValueKind != JsonValueKind.Object)
        {
            return row;
        }

        foreach (var p in el.EnumerateObject())
        {
            row[p.Name] = JsonValue(p.Value);
        }

        return row;
    }

    private static object? JsonValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => JsonToDict(el),
        JsonValueKind.Array => el.EnumerateArray().Select(JsonValue).ToList(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var n) ? n : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private static Dictionary<string, bool> TypedIds(Dictionary<string, object?> opts)
    {
        var typed = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (!opts.TryGetValue("epc_typed_price_ids", out var raw) || raw is null)
        {
            return typed;
        }

        if (raw is IEnumerable<object?> list)
        {
            foreach (var item in list)
            {
                var key = Convert.ToString(item, CultureInfo.InvariantCulture) ?? "";
                if (key != "")
                {
                    typed[key] = true;
                }
            }
        }

        return typed;
    }

    private static Dictionary<string, object?> Copy(Dictionary<string, object?> row)
        => new(row, StringComparer.Ordinal);

    private static string CsvOf(IReadOnlyList<IReadOnlyList<string>> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.Append(PhpCsvLine(line));
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static string PhpCsvLine(IReadOnlyList<string> cells)
    {
        var parts = new List<string>(cells.Count);
        foreach (var cell in cells)
        {
            if (cell.Contains(',') || cell.Contains('"') || cell.Contains('\n') || cell.Contains('\r') || cell.Contains(' ') || cell.Contains('\t'))
            {
                parts.Add("\"" + cell.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
            }
            else
            {
                parts.Add(cell);
            }
        }

        return string.Join(",", parts);
    }

    private static List<string[]> ReadCsv(string path, char delimiter)
    {
        var rows = new List<string[]>();
        using var fh = new StreamReader(path, Encoding.UTF8, true);
        string? line;
        while ((line = fh.ReadLine()) is not null)
        {
            rows.Add(SplitCsv(line, delimiter));
        }

        return rows;
    }

    private static string[] SplitCsv(string line, char delimiter)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (ch == '\\' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                cells.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        cells.Add(sb.ToString());
        return cells.ToArray();
    }

    private static string Cell(IReadOnlyList<string> row, int idx) => idx >= 0 && idx < row.Count ? row[idx] : "";

    private static string Str(Dictionary<string, object?> row, string key, string fallback = "")
        => row.TryGetValue(key, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    private static int IntOf(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v is null)
        {
            return 0;
        }

        return v switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            float f => (int)f,
            decimal m => (int)m,
            _ => int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0
        };
    }

    private static double Num(Dictionary<string, object?> row, string key, double fallback = 0)
    {
        if (!row.TryGetValue(key, out var v) || v is null)
        {
            return fallback;
        }

        return v switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            _ => double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : fallback
        };
    }

    private static bool Truthy(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v is null)
        {
            return false;
        }

        return v switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string s => s != "" && s != "0",
            _ => true
        };
    }

    private static bool IsEmpty(object? v)
        => v is null or "" or 0 or 0L or false or "0";

    private static int StockQty(object? raw)
    {
        if (ParseStock is not null)
        {
            return ParseStock(raw);
        }

        var text = (Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "").Trim();
        if (text == "")
        {
            return 0;
        }

        var compact = text.Replace(" ", "").Replace("\u00a0", "");
        if (DecPair.IsMatch(compact))
        {
            var parts = Regex.Split(compact, "[.,]");
            compact = parts.Length > 0 ? parts[0] : "0";
        }

        var digits = NotDigit.Replace(compact, "");
        if (digits == "")
        {
            return 0;
        }

        var qty = int.Parse(digits, CultureInfo.InvariantCulture);
        return qty > 999999 ? 999999 : qty;
    }

    private static string ClipRunes(string value, int max)
        => string.Concat(value.EnumerateRunes().Take(max));

    private static int RuneCount(string value) => value.EnumerateRunes().Count();

    private static int PhpBytes(string value) => Encoding.UTF8.GetByteCount(value);

    private static int UnixTime() => Clock?.Invoke() ?? (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
