using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-text helpers. PHP identifiers kept for the inventory:
/// <c>epc_stock_brand_price_ids</c>, <c>epc_stock_brand_price_ids_with_stock</c>,
/// <c>epc_stock_brands_with_counts</c>, <c>epc_stock_brand_match_params</c>,
/// <c>epc_stock_brand_parts_for_manufacturer</c>, <c>epc_stock_brands_tag_part_types</c>.
/// </summary>
public static class PhpPlanQ1Text
{
    public const string StockBrandsPath = "content/shop/docpart/epc_stock_brands_helpers.php";
    public const string TreeListItemsPath = "content/shop/catalogue/tree_lists/get_tree_list_items.php";
    public const string TextSearchPath = "content/shop/catalogue/text_search_algorithm.php";

    private static readonly Regex CompactKeep = new("[^A-Z0-9А-ЯЁ]+", RegexOptions.CultureInvariant);
    private static readonly Regex ArticleKeep = new("[^a-zA-Z0-9А-Яа-яёЁ]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions CacheJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string DocumentRoot { get; set; } = "";
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<IReadOnlyList<int>> CustomerOffices { get; set; } = () => [];
    public static Func<Dictionary<string, object?>?> GenuineIndex { get; set; } = () => null;
    public static Func<IReadOnlyList<(string Manufacturer, string Section)>> GenuineRows { get; set; } = () => [];
    public static Func<IReadOnlyList<(int Id, string Name)>> Manufacturers { get; set; } = () => [];
    public static Func<IReadOnlyList<(int ManufacturerId, string Synonym)>> Synonyms { get; set; } = () => [];
    public static Func<object?, string> Translate { get; set; } = value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    public static Func<IReadOnlyList<int>> ArticleLangKeys { get; set; } = () => [];
    public static string CatalogUrl { get; set; } = "";

    public static void Reset()
    {
        DocumentRoot = "";
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CustomerOffices = () => [];
        GenuineIndex = () => null;
        GenuineRows = () => [];
        Manufacturers = () => [];
        Synonyms = () => [];
        Translate = value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        ArticleLangKeys = () => [];
        CatalogUrl = "";
    }

    public sealed class TextStore
    {
        public PhpPlanQ1Open.OpenStore Open { get; } = new();
        public List<OfficeMapRow> OfficeMaps { get; } = [];
        public List<TreeItemRow> TreeItems { get; } = [];
        public List<CatalogueProductRow> Products { get; } = [];
        public List<LangRow> Translations { get; } = [];
        public List<ProductTextRow> ProductTexts { get; } = [];
        public List<PropertyMapRow> PropertyMap { get; } = [];
        public List<PropertyValueRow> PropertyValues { get; } = [];
        public List<DiscoveryRow> Discovery { get; } = [];
        public bool DiscoveryMissing { get; set; }
    }

    public sealed class OfficeMapRow
    {
        public int OfficeId { get; set; }
        public int StorageId { get; set; }
    }

    public sealed class TreeItemRow
    {
        public int Id { get; set; }
        public int TreeListId { get; set; }
        public object? Count { get; set; }
        public object? Level { get; set; }
        public object? Value { get; set; }
        public object? Alias { get; set; }
        public object? Url { get; set; }
        public object? Parent { get; set; }
        public object? Image { get; set; }
        public object? Order { get; set; }
        public object? Open { get; set; }
    }

    public sealed class CatalogueProductRow
    {
        public int Id { get; set; }
        public object? Caption { get; set; }
        public string Alias { get; set; } = "";
        public int PublishedFlag { get; set; } = 1;
    }

    public sealed class LangRow
    {
        public object? StrId { get; set; }
        public string LangCode { get; set; } = "";
        public string Value { get; set; } = "";
        public object? StrKey { get; set; }
    }

    public sealed class ProductTextRow
    {
        public int ProductId { get; set; }
        public object? Content { get; set; }
    }

    public sealed class PropertyMapRow
    {
        public int Id { get; set; }
        public object? Value { get; set; }
        public int PropertyTypeId { get; set; }
    }

    public sealed class PropertyValueRow
    {
        public int ProductId { get; set; }
        public int PropertyId { get; set; }
        public object? Value { get; set; }
    }

    public sealed class DiscoveryRow
    {
        public int ProductId { get; set; }
        public string Status { get; set; } = "";
        public string BrandArticleKey { get; set; } = "";
        public string MetaJson { get; set; } = "";
    }

    public sealed class TreeListItem
    {
        public int Id { get; set; }
        public int Count { get; set; }
        public int Level { get; set; }
        public string Value { get; set; } = "";
        public object? ValueLangStrId { get; set; }
        public int Parent { get; set; }
        public object? Image { get; set; } = "";
        public object? Order { get; set; } = 0;
        public bool Open { get; set; } = true;
        public string ImageUrl { get; set; } = "";
        public int IsNew { get; set; }
        public List<TreeListItem> Data { get; set; } = [];
        public string Alias { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public static List<int> EpcStockBrandPriceIds(TextStore db)
    {
        var priceIds = new Dictionary<int, bool>();
        foreach (var officeId in CustomerOffices())
        {
            var storageIds = db.OfficeMaps.Where(m => m.OfficeId == (int)officeId).Select(m => m.StorageId).ToHashSet();
            foreach (var storage in db.Open.Storages)
            {
                if (!storageIds.Contains(storage.Id)
                    || storage.InterfaceType != 2
                    || storage.Hidden != 0
                    || storage.StorefrontTempDisabled != 0)
                {
                    continue;
                }

                var priceId = PriceIdFromOptions(storage.ConnectionOptions);
                if (priceId != 0)
                {
                    priceIds[priceId] = true;
                }
            }
        }

        var ids = PhpPlanQ1Open.EpcSsfFilterEnabledPriceIds(db.Open, priceIds.Keys.Cast<object?>());
        return ids.Count == 0 ? [1] : ids;
    }

    public static List<int> EpcStockBrandPriceIdsWithStock(TextStore db)
    {
        var ids = new List<int>();
        try
        {
            foreach (var row in db.Open.PriceData)
            {
                if (row.Exist <= 0 || row.Price <= 0)
                {
                    continue;
                }

                if (row.Manufacturer.Trim() == "" || row.Article.Trim() == "")
                {
                    continue;
                }

                var price = db.Open.Prices.FirstOrDefault(p => p.Id == row.PriceId);
                if (price is null || price.StorefrontTempDisabled != 0)
                {
                    continue;
                }

                ids.Add(row.PriceId);
            }
        }
        catch
        {
            // PHP swallows query errors.
        }

        ids = ids.Where(id => id != 0).Distinct().ToList();
        return ids.Count > 0 ? PhpPlanQ1Open.EpcSsfFilterEnabledPriceIds(db.Open, ids.Cast<object?>()) : [1];
    }

    public static List<Dictionary<string, object?>> EpcStockBrandsWithCounts(TextStore db, IReadOnlyList<int> priceIds)
    {
        if (priceIds.Count == 0)
        {
            return [];
        }

        var cacheKey = "brands_" + Md5Hex(string.Join(",", priceIds));
        var cacheDir = Path.Combine(DocumentRoot.TrimEnd('/', '\\'), "content", "files", "epc_brands_cache");
        Directory.CreateDirectory(cacheDir);
        var cacheFile = Path.Combine(cacheDir, cacheKey + ".json");
        if (TryReadBrandCache(cacheFile) is { Count: > 0 } hit)
        {
            return hit;
        }

        var idSet = priceIds.ToHashSet();
        var groups = new Dictionary<string, List<PhpPlanQ1Open.PriceDataRow>>(StringComparer.Ordinal);
        foreach (var row in db.Open.PriceData)
        {
            if (!idSet.Contains(row.PriceId) || row.Price <= 0 || row.Exist <= 0)
            {
                continue;
            }

            var name = row.Manufacturer.Trim();
            if (name == "")
            {
                continue;
            }

            var article = ArticleExpr(row);
            if (article == "")
            {
                continue;
            }

            var key = name.ToUpperInvariant();
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(row);
        }

        var brands = new List<Dictionary<string, object?>>();
        foreach (var key in groups.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var name = groups[key].Select(r => r.Manufacturer.Trim()).Where(n => n != "").DefaultIfEmpty("").Min(StringComparer.Ordinal) ?? "";
            if (name == "")
            {
                continue;
            }

            var first = name.Length == 0 ? "" : char.ToUpperInvariant(name[0]).ToString();
            string letter;
            if (first.Length > 0 && first[0] is >= '0' and <= '9')
            {
                letter = "0-9";
            }
            else if (first.Length == 1 && first[0] is >= 'A' and <= 'Z')
            {
                letter = first;
            }
            else
            {
                letter = "#";
            }

            brands.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["parts_count"] = groups[key].Select(ArticleExpr).Distinct(StringComparer.Ordinal).Count(),
                ["letter"] = letter
            });
        }

        if (brands.Count > 0)
        {
            File.WriteAllText(cacheFile, JsonSerializer.Serialize(brands, CacheJson));
        }

        return brands;
    }

    public static object?[] EpcStockBrandMatchParams(object? brand)
    {
        var upper = (brand is null ? "" : Convert.ToString(brand, CultureInfo.InvariantCulture) ?? "").Trim().ToUpperInvariant();
        var compact = CompactKeep.Replace(upper, "");
        return [upper, compact];
    }

    public static List<Dictionary<string, object?>> EpcStockBrandPartsForManufacturer(TextStore db, object? brand, int limit = 5000)
    {
        var name = (brand is null ? "" : Convert.ToString(brand, CultureInfo.InvariantCulture) ?? "").Trim();
        if (name == "")
        {
            return [];
        }

        if (limit < 1 || limit > 10000)
        {
            limit = 5000;
        }

        var pair = EpcStockBrandMatchParams(name);
        var brandUpper = Str(pair[0]);
        var brandCompact = Str(pair[1]);
        var rows = LoadParts(db, EpcStockBrandPriceIds(db), brandUpper, brandCompact, limit);
        if (rows.Count == 0)
        {
            rows = LoadParts(db, EpcStockBrandPriceIdsWithStock(db), brandUpper, brandCompact, limit);
        }

        return rows;
    }

    public static List<Dictionary<string, object?>> EpcStockBrandsTagPartTypes(
        TextStore db,
        IReadOnlyList<Dictionary<string, object?>> brands,
        string catalogUrl = "")
    {
        if (brands.Count == 0)
        {
            return brands.ToList();
        }

        var synonymMap = PhpReadyNamed.DocpartLoadManufacturerSynonymMap(Manufacturers(), Synonyms());
        var genuineIndex = GenuineIndex() ?? PhpPlanQ1.EpcGenuineBuildFrontendIndex(GenuineRows(), synonymMap, catalogUrl);
        var genuineKeys = Field(genuineIndex, "brands") as Dictionary<string, bool>
            ?? new Dictionary<string, bool>(StringComparer.Ordinal);
        if (Field(genuineIndex, "brands") is Dictionary<string, object?> boxed)
        {
            foreach (var kv in boxed)
            {
                if (!Empty(kv.Value))
                {
                    genuineKeys[kv.Key] = true;
                }
            }
        }

        var output = new List<Dictionary<string, object?>>();
        foreach (var brandRow in brands)
        {
            var copy = new Dictionary<string, object?>(brandRow, StringComparer.Ordinal);
            var rowName = Str(Field(copy, "name")).Trim();
            var isGenuine = false;
            if (rowName != "")
            {
                foreach (var equiv in PhpReadyNamed.DocpartSynonymNamesForBrand(rowName, synonymMap))
                {
                    var key = PhpReadyNamed.DocpartSynonymNormalizeBrand(equiv);
                    if (key != "" && genuineKeys.TryGetValue(key, out var flag) && !Empty(flag))
                    {
                        isGenuine = true;
                        break;
                    }
                }
            }

            copy["part_type"] = isGenuine ? "genuine" : "aftermarket";
            output.Add(copy);
        }

        return output;
    }

    public static string GetTreeListItemsJson(TextStore db, object? treeListId)
    {
        var root = new TreeListItem();
        var json = EncodeTreeArray(root.Data, hexTag: true);
        var needed = IntVal(treeListId);
        var rows = db.TreeItems
            .Where(r => r.TreeListId == needed)
            .OrderBy(r => IntVal(r.Level))
            .ThenBy(r => IntVal(r.Order))
            .ToList();
        if (rows.Count == 0)
        {
            return json;
        }

        foreach (var record in rows)
        {
            var current = new TreeListItem
            {
                Id = IntVal(record.Id),
                Count = IntVal(record.Count),
                Level = IntVal(record.Level),
                ValueLangStrId = record.Value,
                Value = Translate(record.Value),
                Alias = Str(record.Alias),
                Url = Str(record.Url),
                Parent = IntVal(record.Parent),
                Image = record.Image ?? "",
                Order = record.Order ?? 0,
                Open = PhpBool(record.Open)
            };
            root.Data = AddTreeItem(current, root.Data, root.Id);
        }

        json = EncodeTreeArray(root.Data, hexTag: true);
        json = json.Replace("\"level\"", "\"$level\"", StringComparison.Ordinal);
        json = json.Replace("\"parent\"", "\"$parent\"", StringComparison.Ordinal);
        json = json.Replace("\"count\"", "\"$count\"", StringComparison.Ordinal);
        return json;
    }

    public static List<int> TextSearchProducts(TextStore db, string searchString, string lang)
    {
        var words = new List<string>();
        if (!Empty(searchString))
        {
            foreach (var raw in searchString.Trim().Split(' '))
            {
                var item = raw.Trim();
                if (item.Length < 2)
                {
                    continue;
                }

                words.Add(item);
            }
        }

        var products = new List<int>();
        foreach (var product in db.Products)
        {
            if (CaptionMatches(db, product.Caption, words))
            {
                products.Add(product.Id);
            }
        }

        foreach (var text in db.ProductTexts)
        {
            if (CaptionMatches(db, text.Content, words))
            {
                products.Add(text.ProductId);
            }
        }

        var articleNorm = ArticleKeep.Replace(searchString ?? "", "").ToUpperInvariant();
        var articleKeys = ArticleLangKeys().ToHashSet();
        var articlePropertyIds = db.PropertyMap
            .Where(p => p.PropertyTypeId == 3 && articleKeys.Contains(IntVal(p.Value)))
            .Select(p => p.Id)
            .ToHashSet();
        foreach (var value in db.PropertyValues)
        {
            if (!articlePropertyIds.Contains(value.PropertyId))
            {
                continue;
            }

            var hit = db.Translations.Any(t =>
                SameId(t.StrId, value.Value)
                && string.Equals(t.LangCode, lang, StringComparison.Ordinal)
                && string.Equals(t.Value, articleNorm, StringComparison.Ordinal));
            if (hit)
            {
                products.Add(value.ProductId);
            }
        }

        if (articleNorm != "")
        {
            var lowerNorm = articleNorm.ToLowerInvariant();
            foreach (var product in db.Products)
            {
                if (product.PublishedFlag != 1)
                {
                    continue;
                }

                var alias = product.Alias ?? "";
                if (alias.EndsWith("/" + articleNorm, StringComparison.Ordinal)
                    || alias.StartsWith(articleNorm, StringComparison.Ordinal)
                    || string.Equals(alias, lowerNorm, StringComparison.Ordinal))
                {
                    products.Add(product.Id);
                }
            }
        }

        var searchLower = (searchString ?? "").Trim().ToLowerInvariant();
        if (searchLower != "" && !db.DiscoveryMissing)
        {
            try
            {
                var metaNeedle = "\"brand_article_key\":\"" + searchLower;
                foreach (var row in db.Discovery)
                {
                    if (row.Status != "imported" || row.ProductId <= 0)
                    {
                        continue;
                    }

                    if (row.BrandArticleKey.Contains(searchLower, StringComparison.Ordinal)
                        || row.MetaJson.Contains(metaNeedle, StringComparison.Ordinal))
                    {
                        products.Add(row.ProductId);
                    }
                }
            }
            catch
            {
                // PHP swallows a missing discovery table.
            }
        }

        return products;
    }

    private static List<Dictionary<string, object?>> LoadParts(
        TextStore db,
        IReadOnlyList<int> priceIds,
        string brandUpper,
        string brandCompact,
        int limit)
    {
        var enabled = PhpPlanQ1Open.EpcSsfFilterEnabledPriceIds(db.Open, priceIds.Cast<object?>());
        if (enabled.Count == 0)
        {
            return [];
        }

        var idSet = enabled.ToHashSet();
        var groups = new Dictionary<string, List<PhpPlanQ1Open.PriceDataRow>>(StringComparer.Ordinal);
        foreach (var row in db.Open.PriceData)
        {
            if (!idSet.Contains(row.PriceId) || row.Price <= 0 || row.Exist <= 0)
            {
                continue;
            }

            var manufacturer = row.Manufacturer.Trim();
            if (manufacturer == "")
            {
                continue;
            }

            var article = ArticleExpr(row);
            if (article == "")
            {
                continue;
            }

            var upper = manufacturer.ToUpperInvariant();
            var compactSql = upper.Replace(" ", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .Replace(".", "", StringComparison.Ordinal);
            if (upper != brandUpper && compactSql != brandCompact)
            {
                continue;
            }

            var key = upper + "\0" + article;
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(row);
        }

        var output = new List<Dictionary<string, object?>>();
        foreach (var list in groups.Values)
        {
            var manufacturer = list.Select(r => r.Manufacturer.Trim()).Where(n => n != "").DefaultIfEmpty("").Min(StringComparer.Ordinal) ?? "";
            var article = list.Select(r => r.Article).DefaultIfEmpty("").Min(StringComparer.Ordinal) ?? "";
            var articleShow = list.Select(ArticleExpr).DefaultIfEmpty("").Min(StringComparer.Ordinal) ?? "";
            var name = list.Select(r => r.Name).DefaultIfEmpty("").Min(StringComparer.Ordinal) ?? "";
            var exist = list.Sum(r => r.Exist);
            var price = list.Min(r => r.Price);
            var time = list.Select(r => r.TimeToExe).DefaultIfEmpty("").Min(StringComparer.Ordinal) ?? "";
            var storage = list.Select(r => r.Storage).DefaultIfEmpty("").Min(StringComparer.Ordinal) ?? "";
            var minOrder = list.Select(r => r.MinOrder).DefaultIfEmpty("").Min(StringComparer.Ordinal) ?? "";
            output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["manufacturer_key"] = manufacturer.ToUpperInvariant(),
                ["manufacturer"] = manufacturer,
                ["article"] = article,
                ["article_show"] = articleShow,
                ["name"] = name,
                ["exist"] = exist.ToString(CultureInfo.InvariantCulture),
                ["price"] = FormatDec(price),
                ["time_to_exe"] = time,
                ["storage"] = storage,
                ["min_order"] = minOrder
            });
        }

        return output
            .OrderBy(r => decimal.TryParse(Str(r["price"]), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0m)
            .ThenBy(r => Str(r["article_show"]), StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    private static List<TreeListItem> AddTreeItem(TreeListItem item, List<TreeListItem> candidateData, object? candidateId)
    {
        if (PhpLooseEquals(item.Parent, candidateId))
        {
            candidateData.Add(item);
            return candidateData;
        }

        foreach (var node in candidateData)
        {
            if (node.Count == 0)
            {
                continue;
            }

            node.Data = AddTreeItem(item, node.Data, node.Id);
        }

        return candidateData;
    }

    private static string EncodeTreeArray(IReadOnlyList<TreeListItem> items, bool hexTag)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(EncodeTreeItem(items[i], hexTag));
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static string EncodeTreeItem(TreeListItem item, bool hexTag)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        AppendJson(sb, "id", item.Id, hexTag);
        sb.Append(',');
        AppendJson(sb, "count", item.Count, hexTag);
        sb.Append(',');
        AppendJson(sb, "level", item.Level, hexTag);
        sb.Append(',');
        AppendJson(sb, "value", item.Value, hexTag);
        sb.Append(',');
        AppendJson(sb, "value_lang_str_id", item.ValueLangStrId, hexTag);
        sb.Append(',');
        AppendJson(sb, "parent", item.Parent, hexTag);
        sb.Append(',');
        AppendJson(sb, "image", item.Image, hexTag);
        sb.Append(',');
        AppendJson(sb, "order", item.Order, hexTag);
        sb.Append(',');
        AppendJson(sb, "open", item.Open, hexTag);
        sb.Append(',');
        AppendJson(sb, "image_url", item.ImageUrl, hexTag);
        sb.Append(',');
        AppendJson(sb, "is_new", item.IsNew, hexTag);
        sb.Append(",\"data\":");
        sb.Append(EncodeTreeArray(item.Data, hexTag));
        sb.Append(',');
        AppendJson(sb, "alias", item.Alias, hexTag);
        sb.Append(',');
        AppendJson(sb, "url", item.Url, hexTag);
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendJson(StringBuilder sb, string key, object? value, bool hexTag)
    {
        sb.Append(PhpJsonString(key, hexTag));
        sb.Append(':');
        sb.Append(PhpJsonValue(value, hexTag));
    }

    private static string PhpJsonValue(object? value, bool hexTag)
        => value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            _ => PhpJsonString(Str(value), hexTag)
        };

    private static string PhpJsonString(string value, bool hexTag)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '/':
                    sb.Append("\\/");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '<' when hexTag:
                    sb.Append("\\u003C");
                    break;
                case '>' when hexTag:
                    sb.Append("\\u003E");
                    break;
                default:
                    if (ch < 0x20 || ch > 0x7E)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }

    private static List<Dictionary<string, object?>>? TryReadBrandCache(string cacheFile)
    {
        if (!File.Exists(cacheFile))
        {
            return null;
        }

        var age = Clock() - new DateTimeOffset(File.GetLastWriteTimeUtc(cacheFile)).ToUnixTimeSeconds();
        if (age >= 600)
        {
            return null;
        }

        try
        {
            var cached = File.ReadAllText(cacheFile);
            var decoded = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(cached);
            return decoded is { Count: > 0 } ? decoded : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool CaptionMatches(TextStore db, object? captionId, IReadOnlyList<string> words)
    {
        var translations = db.Translations.Where(t => SameId(t.StrId, captionId)).Select(t => t.Value).ToList();
        if (translations.Count == 0)
        {
            return false;
        }

        if (words.Count == 0)
        {
            return true;
        }

        return translations.Any(value => words.All(word =>
            value.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool SameId(object? left, object? right)
        => IntVal(left) != 0 && IntVal(left) == IntVal(right)
            || string.Equals(Str(left), Str(right), StringComparison.Ordinal);

    private static int PriceIdFromOptions(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (!doc.RootElement.TryGetProperty("price_id", out var raw) || Empty(JsonBox(raw)))
            {
                return 0;
            }

            return IntVal(JsonBox(raw));
        }
        catch
        {
            return 0;
        }
    }

    private static object? JsonBox(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt64(out var n) ? n : el.GetDouble(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };

    private static string ArticleExpr(PhpPlanQ1Open.PriceDataRow row)
    {
        var show = row.ArticleShow.Trim();
        return show != "" ? show : row.Article.Trim();
    }

    private static string FormatDec(decimal value)
    {
        var s = value.ToString(CultureInfo.InvariantCulture);
        return s.Contains('.', StringComparison.Ordinal) ? s : s + ".00";
    }

    private static string Md5Hex(string value)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool PhpLooseEquals(object? left, object? right)
        => IntVal(left) == IntVal(right);

    private static bool PhpBool(object? value)
        => value switch
        {
            null => false,
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => d != 0 && !double.IsNaN(d),
            string s => s != "",
            _ => !Empty(value)
        };

    private static object? Field(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static string Str(object? value)
        => value switch
        {
            null => "",
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? "",
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetRawText(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static int IntVal(object? value)
    {
        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static bool Empty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            string s => s is "" or "0",
            System.Collections.ICollection c => c.Count == 0,
            JsonElement je => je.ValueKind is JsonValueKind.Null or JsonValueKind.False
                || (je.ValueKind == JsonValueKind.String && je.GetString() is "" or "0")
                || (je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0),
            _ => Str(value) is "" or "0"
        };
}
