using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-beam article-match helpers. PHP identifiers kept for the inventory:
/// <c>docpart_normalize_article_for_price</c>, <c>docpart_sql_article_normalized_expr</c>,
/// <c>docpart_analogs_has_search_columns</c>, <c>docpart_analogs_match_exprs</c>,
/// <c>docpart_price_data_ensure_article_search_column</c>,
/// <c>docpart_price_data_backfill_article_search</c>,
/// <c>docpart_sql_article_match_expr</c>, <c>docpart_sql_article_values_match_clause</c>,
/// <c>docpart_collect_article_candidates</c>, <c>docpart_resolve_article_search_values</c>,
/// <c>docpart_price_ids_from_office_storage_bunches</c>, <c>epc_chpu_build_part_url</c>,
/// <c>epc_chpu_distinct_warehouse_brands_for_article</c>,
/// <c>epc_chpu_single_brand_from_umapi</c>,
/// <c>epc_chpu_resolve_single_brand_for_article</c>,
/// <c>epc_chpu_single_brand_redirect_url</c>.
/// Path: <c>content/shop/docpart/docpart_article_match.php</c>.
/// GET never mints a session cookie. Stock/pricing/synonym/UMAPI parents stay injected.
/// </summary>
public static class PhpPlanQ1Beam
{
    public const string ArticleMatchPath = "content/shop/docpart/docpart_article_match.php";

    private static readonly string[] Sweep =
    [
        " ", "-", "_", "`", "/", "'", "\"", "\\", ".", ",", "#", "\r\n", "\r", "\n", "\t"
    ];

    private static readonly string[] SqlChars = [" ", "-", "_", "`", "/", "'", "\"", ".", ",", "#"];

    public static Func<double?>? HostLoad { get; set; }
    public static Func<string>? ReplaceFallbackEnv { get; set; }
    public static Func<MySqlConnection, List<int>>? StockPriceIds { get; set; }
    public static Func<MySqlConnection, List<int>>? StockPriceIdsWithStock { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>?>? PricingBrandRule { get; set; }
    public static Func<MySqlConnection, Dictionary<string, string>>? LoadCanonicalMap { get; set; }
    public static Func<string, Dictionary<string, string>, string>? SynonymCanonical { get; set; }
    public static Func<string, string>? SynonymNormalize { get; set; }
    public static Func<object?, string, Dictionary<string, Dictionary<string, object?>>, Dictionary<string, bool>, int>? ArticleBrandsFromUmapi { get; set; }

    private static bool? _analogsSearchReady;
    private static bool? _articleSearchReady;

    public static void Reset()
    {
        HostLoad = null;
        ReplaceFallbackEnv = () => Environment.GetEnvironmentVariable("EPC_ARTICLE_MATCH_REPLACE_FALLBACK") ?? "";
        StockPriceIds = null;
        StockPriceIdsWithStock = null;
        PricingBrandRule = null;
        LoadCanonicalMap = null;
        SynonymCanonical = null;
        SynonymNormalize = null;
        ArticleBrandsFromUmapi = null;
        _analogsSearchReady = null;
        _articleSearchReady = null;
    }

    public static string DocpartNormalizeArticleForPrice(object? articleInput)
    {
        if (articleInput is null)
        {
            return "";
        }

        var text = Convert.ToString(articleInput, CultureInfo.InvariantCulture) ?? "";
        if (text == "")
        {
            return "";
        }

        foreach (var token in Sweep)
        {
            text = text.Replace(token, "", StringComparison.Ordinal);
        }

        return text.ToUpperInvariant();
    }

    public static string DocpartSqlArticleNormalizedExpr(string column = "`article`")
    {
        var expr = column;
        foreach (var ch in SqlChars)
        {
            var lit = ch == "'" ? "''" : ch;
            expr = "REPLACE(" + expr + ", '" + lit + "', '')";
        }

        expr = "REPLACE(" + expr + ", CHAR(92), '')";
        expr = "REPLACE(REPLACE(REPLACE(REPLACE(" + expr + ", CHAR(13,10), ''), CHAR(13), ''), CHAR(10), ''), CHAR(9), '')";
        return "UPPER(" + expr + ")";
    }

    public static bool DocpartAnalogsHasSearchColumns(MySqlConnection db)
    {
        if (_analogsSearchReady is not null)
        {
            return _analogsSearchReady.Value;
        }

        _analogsSearchReady = false;
        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT `article_search`, `analog_search` FROM `shop_docpart_articles_analogs_list` LIMIT 1";
            cmd.ExecuteNonQuery();
            _analogsSearchReady = true;
        }
        catch
        {
            _analogsSearchReady = false;
        }

        return _analogsSearchReady.Value;
    }

    public static (string ArticleExpr, string AnalogExpr) DocpartAnalogsMatchExprs(MySqlConnection db)
        => DocpartAnalogsHasSearchColumns(db)
            ? ("`article_search`", "`analog_search`")
            : (DocpartSqlArticleNormalizedExpr("`article`"), DocpartSqlArticleNormalizedExpr("`analog`"));

    public static bool DocpartPriceDataEnsureArticleSearchColumn(MySqlConnection db, bool allowAlter = false)
    {
        if (_articleSearchReady is not null)
        {
            return _articleSearchReady.Value;
        }

        _articleSearchReady = false;
        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT `article_search` FROM `shop_docpart_prices_data` LIMIT 1";
            cmd.ExecuteNonQuery();
            _articleSearchReady = true;
        }
        catch
        {
            if (!allowAlter)
            {
                return false;
            }

            try
            {
                using var alter = db.CreateCommand();
                alter.CommandText = """
                    ALTER TABLE `shop_docpart_prices_data`
                     ADD COLUMN `article_search` VARCHAR(64) NOT NULL DEFAULT '' AFTER `article`,
                     ADD INDEX `x_article_search_price` (`article_search`, `price_id`),
                     ADD INDEX `x_price_article_search` (`price_id`, `article_search`)
                    """;
                alter.ExecuteNonQuery();
                _articleSearchReady = true;
            }
            catch
            {
                try
                {
                    using var probe = db.CreateCommand();
                    probe.CommandText = "SELECT `article_search` FROM `shop_docpart_prices_data` LIMIT 1";
                    probe.ExecuteNonQuery();
                    _articleSearchReady = true;
                }
                catch
                {
                    _articleSearchReady = false;
                }
            }
        }

        return _articleSearchReady.Value;
    }

    public static int DocpartPriceDataBackfillArticleSearch(MySqlConnection db, int priceId = 0, int limit = 25000)
    {
        if (!DocpartPriceDataEnsureArticleSearchColumn(db))
        {
            return 0;
        }

        var expr = DocpartSqlArticleNormalizedExpr("`article`");
        limit = Math.Max(100, Math.Min(2000, limit));
        try
        {
            var idSql = "SELECT `id` FROM `shop_docpart_prices_data` WHERE (`article_search` = '' OR `article_search` IS NULL)"
                + (priceId > 0 ? " AND `price_id` = " + priceId : "")
                + " ORDER BY `id` ASC LIMIT " + limit;
            var ids = new List<int>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = idSql;
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    ids.Add(Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture));
                }
            }

            if (ids.Count == 0)
            {
                return 0;
            }

            var ph = string.Join(",", ids.Select((_, i) => "@id" + i));
            using var upd = db.CreateCommand();
            upd.CommandText = "UPDATE `shop_docpart_prices_data` SET `article_search` = " + expr + " WHERE `id` IN (" + ph + ")";
            for (var i = 0; i < ids.Count; i++)
            {
                upd.Parameters.AddWithValue("@id" + i, ids[i]);
            }

            upd.ExecuteNonQuery();
            return ids.Count;
        }
        catch
        {
            return 0;
        }
    }

    public static string DocpartSqlArticleMatchExpr(MySqlConnection? db, string column = "`article`")
        => db is not null && DocpartPriceDataEnsureArticleSearchColumn(db)
            ? "`article_search`"
            : DocpartSqlArticleNormalizedExpr(column);

    public static string DocpartSqlArticleValuesMatchClause(MySqlConnection db, IEnumerable<string> articleValues, List<object?> bindingValues, string column = "`article`")
    {
        var values = articleValues.Select(v => v ?? "").Where(v => v != "").ToList();
        if (values.Count == 0)
        {
            return "0";
        }

        var ph = string.Join(",", values.Select(_ => "?"));
        var normExpr = DocpartSqlArticleNormalizedExpr(column);
        if (DocpartPriceDataEnsureArticleSearchColumn(db))
        {
            var allowReplace = (ReplaceFallbackEnv?.Invoke() ?? "") == "1";
            foreach (var value in values)
            {
                bindingValues.Add(value);
            }

            if (!allowReplace)
            {
                return "(`article_search` IN (" + ph + "))";
            }

            foreach (var value in values)
            {
                bindingValues.Add(value);
            }

            return "(`article_search` IN (" + ph + ") OR " + normExpr + " IN (" + ph + "))";
        }

        foreach (var value in values)
        {
            bindingValues.Add(value);
        }

        return "(" + normExpr + " IN (" + ph + "))";
    }

    public static List<string> DocpartCollectArticleCandidates(MySqlConnection? db, string articleNorm, bool useCrosses)
    {
        var candidates = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (articleNorm == "")
        {
            return [];
        }

        candidates[articleNorm] = true;
        if (!useCrosses || db is null)
        {
            return candidates.Keys.ToList();
        }

        var load1 = HostLoad?.Invoke();
        if (load1 is >= 6.0)
        {
            return candidates.Keys.ToList();
        }

        var (artExpr, analogExpr) = DocpartAnalogsMatchExprs(db);
        var limit = load1 is >= 4.0 ? 80 : 400;
        try
        {
            TrySetTimeout(db, 2, 2000);
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT `article`, `analog` FROM `shop_docpart_articles_analogs_list` WHERE "
                + artExpr + " = @a OR " + analogExpr + " = @b LIMIT " + limit;
            cmd.Parameters.AddWithValue("@a", articleNorm);
            cmd.Parameters.AddWithValue("@b", articleNorm);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var articleCandidate = DocpartNormalizeArticleForPrice(reader["article"]);
                var analogCandidate = DocpartNormalizeArticleForPrice(reader["analog"]);
                if (articleCandidate != "")
                {
                    candidates[articleCandidate] = true;
                }

                if (analogCandidate != "")
                {
                    candidates[analogCandidate] = true;
                }
            }
        }
        catch
        {
        }

        return candidates.Keys.ToList();
    }

    public static List<string> DocpartResolveArticleSearchValues(MySqlConnection db, object? config, string articleInput, IEnumerable<int>? priceIds = null)
    {
        var articleNorm = DocpartNormalizeArticleForPrice(articleInput);
        if (articleNorm == "")
        {
            return [];
        }

        var useCrosses = !PhpEmpty(Prop(config, "local_crosses"));
        var ids = (priceIds ?? []).Select(i => i).Distinct().ToList();
        if (useCrosses && ids.Count > 0)
        {
            var bindings = new List<object?>();
            var clause = DocpartSqlArticleValuesMatchClause(db, [articleNorm], bindings);
            try
            {
                using var cmd = db.CreateCommand();
                var pricePh = string.Join(",", ids.Select((_, i) => "@p" + i));
                cmd.CommandText = "SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE " + clause
                    + " AND `price_id` IN (" + pricePh + ") LIMIT 1";
                BindPositional(cmd, bindings);
                for (var i = 0; i < ids.Count; i++)
                {
                    cmd.Parameters.AddWithValue("@p" + i, ids[i]);
                }

                var count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
                if (count > 0)
                {
                    return [articleNorm];
                }
            }
            catch
            {
            }
        }

        return DocpartCollectArticleCandidates(db, articleNorm, useCrosses);
    }

    public static List<int> DocpartPriceIdsFromOfficeStorageBunches(MySqlConnection? db, IEnumerable<Dictionary<string, object?>>? bunches)
    {
        var priceIds = new List<int>();
        if (bunches is null || db is null)
        {
            return priceIds;
        }

        var storageIds = new Dictionary<int, bool>();
        foreach (var bunch in bunches)
        {
            var storageId = ToInt(bunch.TryGetValue("storage_id", out var sid) ? sid : 0);
            if (storageId > 0)
            {
                storageIds[storageId] = true;
            }
        }

        if (storageIds.Count == 0)
        {
            return priceIds;
        }

        var idList = storageIds.Keys.ToList();
        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT `connection_options` FROM `shop_storages` WHERE `id` IN ("
                + string.Join(",", idList.Select((_, i) => "@s" + i)) + ")";
            for (var i = 0; i < idList.Count; i++)
            {
                cmd.Parameters.AddWithValue("@s" + i, idList[i]);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var raw = Convert.ToString(reader["connection_options"]) ?? "";
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty("price_id", out var price) && price.ValueKind != JsonValueKind.Null)
                    {
                        priceIds.Add(price.GetInt32());
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return priceIds.Distinct().ToList();
    }

    public static string EpcChpuBuildPartUrl(object? config, string langHref, string brand, string article)
    {
        var articleNorm = DocpartNormalizeArticleForPrice(article);
        if (articleNorm == "")
        {
            return "";
        }

        var partsSeg = "parts";
        var brandsSeg = "brands";
        var slashCode = "---";
        var chpu = Prop(config, "chpu_search_config");
        if (!PhpEmpty(chpu))
        {
            var level1 = Nested(chpu, "level_1");
            var url = Convert.ToString(Nested(level1, "url") ?? "");
            if (!PhpEmpty(url))
            {
                partsSeg = url;
            }

            var mode1 = Nested(Nested(chpu, "level_2"), "mode_1");
            var brandsUrl = Convert.ToString(Nested(mode1, "url") ?? "");
            if (!PhpEmpty(brandsUrl))
            {
                brandsSeg = brandsUrl;
            }

            if (HasKey(chpu, "slash_code"))
            {
                slashCode = Convert.ToString(Nested(chpu, "slash_code") ?? "") ?? "";
            }
        }

        langHref = (langHref ?? "").TrimEnd('/');
        brand = (brand ?? "").Trim();
        if (brand == "")
        {
            return langHref + "/" + partsSeg + "/" + brandsSeg + "/" + RawUrlEncode(articleNorm);
        }

        brand = brand.ToUpperInvariant();
        var brandAlias = brand.Replace("/", slashCode, StringComparison.Ordinal);
        return langHref + "/" + partsSeg + "/" + RawUrlEncode(brandAlias) + "/" + RawUrlEncode(articleNorm);
    }

    public static List<string> EpcChpuDistinctWarehouseBrandsForArticle(MySqlConnection? db, object? config, string articleInput, IEnumerable<int>? priceIds = null)
    {
        var articleNorm = DocpartNormalizeArticleForPrice(articleInput);
        if (articleNorm == "" || db is null)
        {
            return [];
        }

        var ids = (priceIds ?? []).ToList();
        if (ids.Count == 0)
        {
            ids = StockPriceIdsWithStock?.Invoke(db) ?? [];
            var office = StockPriceIds?.Invoke(db) ?? [];
            if (office.Count > 0)
            {
                ids = ids.Concat(office).Distinct().ToList();
            }

            if (ids.Count == 0)
            {
                try
                {
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = "SELECT DISTINCT `id` FROM `shop_docpart_prices` ORDER BY `id` ASC";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        ids.Add(Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture));
                    }
                }
                catch
                {
                    ids = [1];
                }
            }
        }

        ids = ids.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var articleValues = DocpartResolveArticleSearchValues(db, config, articleInput, ids);
        if (articleValues.Count == 0)
        {
            articleValues = [articleNorm];
        }

        var artExpr = DocpartSqlArticleMatchExpr(db, "`article`");
        var brands = QueryWarehouseBrands(db, artExpr, articleValues, ids);
        if (brands.Count == 0 && artExpr == "`article_search`")
        {
            brands = QueryWarehouseBrands(db, DocpartSqlArticleNormalizedExpr("`article`"), articleValues, ids);
        }

        if (brands.Count > 1 && LoadCanonicalMap is not null)
        {
            var map = LoadCanonicalMap(db);
            var collapsed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var brandName in brands)
            {
                var canonical = SynonymCanonical?.Invoke(brandName, map) ?? "";
                var key = canonical != "" ? canonical : (SynonymNormalize?.Invoke(brandName) ?? "");
                if (key == "" || collapsed.ContainsKey(key))
                {
                    continue;
                }

                collapsed[key] = canonical != "" ? canonical : brandName;
            }

            brands = collapsed.Values.ToList();
            brands.Sort(StringComparer.Ordinal);
        }

        return brands;
    }

    public static string? EpcChpuSingleBrandFromUmapi(object? config, string articleInput)
    {
        if (ArticleBrandsFromUmapi is null)
        {
            return null;
        }

        var brands = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var added = ArticleBrandsFromUmapi(config, articleInput, brands, seen);
        if (added != 1 || brands.Count != 1)
        {
            return null;
        }

        var row = brands.Values.First();
        var brand = (Convert.ToString(row.TryGetValue("manufacturer", out var m) ? m : "", CultureInfo.InvariantCulture) ?? "").Trim();
        return brand != "" ? brand : null;
    }

    public static string? EpcChpuResolveSingleBrandForArticle(MySqlConnection? db, object? config, string articleInput)
    {
        var warehouse = EpcChpuDistinctWarehouseBrandsForArticle(db, config, articleInput);
        if (warehouse.Count == 1)
        {
            return warehouse[0];
        }

        if (warehouse.Count > 1)
        {
            return null;
        }

        return EpcChpuSingleBrandFromUmapi(config, articleInput);
    }

    public static string EpcChpuSingleBrandRedirectUrl(MySqlConnection? db, object? config, string articleInput, string langHref)
    {
        var chpu = Prop(config, "chpu_search_config");
        if (PhpEmpty(Nested(chpu, "chpu_search_on")))
        {
            return "";
        }

        var single = EpcChpuResolveSingleBrandForArticle(db, config, articleInput);
        if (string.IsNullOrEmpty(single))
        {
            return "";
        }

        return EpcChpuBuildPartUrl(config, langHref, single, articleInput);
    }

    private static List<string> QueryWarehouseBrands(MySqlConnection db, string artExpr, List<string> articleValues, List<int> priceIds)
    {
        var brands = new List<string>();
        try
        {
            using var cmd = db.CreateCommand();
            var artPh = string.Join(",", articleValues.Select((_, i) => "@a" + i));
            var pricePh = string.Join(",", priceIds.Select((_, i) => "@p" + i));
            cmd.CommandText = "SELECT MIN(TRIM(`manufacturer`)) AS `brand_name` FROM `shop_docpart_prices_data` WHERE "
                + artExpr + " IN (" + artPh + ") AND `price_id` IN (" + pricePh + ")"
                + " AND TRIM(IFNULL(`manufacturer`, '')) != '' GROUP BY UPPER(TRIM(`manufacturer`)) ORDER BY UPPER(TRIM(`manufacturer`)) ASC";
            for (var i = 0; i < articleValues.Count; i++)
            {
                cmd.Parameters.AddWithValue("@a" + i, articleValues[i]);
            }

            for (var i = 0; i < priceIds.Count; i++)
            {
                cmd.Parameters.AddWithValue("@p" + i, priceIds[i]);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var brandName = (Convert.ToString(reader["brand_name"]) ?? "").Trim();
                if (brandName == "")
                {
                    continue;
                }

                if (PricingBrandRule is not null)
                {
                    var rule = PricingBrandRule(db, brandName);
                    if (rule is not null && ToInt(rule.TryGetValue("visible", out var vis) ? vis : 1) == 0)
                    {
                        continue;
                    }
                }

                brands.Add(brandName);
            }
        }
        catch
        {
        }

        return brands;
    }

    private static void TrySetTimeout(MySqlConnection db, int seconds, int millis)
    {
        try
        {
            using var a = db.CreateCommand();
            a.CommandText = "SET SESSION max_statement_time = " + seconds;
            a.ExecuteNonQuery();
        }
        catch
        {
        }

        try
        {
            using var b = db.CreateCommand();
            b.CommandText = "SET SESSION MAX_EXECUTION_TIME = " + millis;
            b.ExecuteNonQuery();
        }
        catch
        {
        }
    }

    private static void BindPositional(MySqlCommand cmd, List<object?> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            cmd.CommandText = ReplaceFirst(cmd.CommandText, "?", "@b" + i);
            cmd.Parameters.AddWithValue("@b" + i, values[i] ?? "");
        }
    }

    private static string ReplaceFirst(string text, string find, string replace)
    {
        var at = text.IndexOf(find, StringComparison.Ordinal);
        return at < 0 ? text : text.Remove(at, find.Length).Insert(at, replace);
    }

    private static string RawUrlEncode(string value)
        => Uri.EscapeDataString(value).Replace("%20", "%20", StringComparison.Ordinal);

    private static object? Prop(object? cfg, string name)
    {
        if (cfg is null)
        {
            return null;
        }

        if (cfg is Dictionary<string, object?> map && map.TryGetValue(name, out var value))
        {
            return value;
        }

        var prop = cfg.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return prop?.GetValue(cfg);
    }

    private static bool HasKey(object? node, string name)
        => node is Dictionary<string, object?> map && map.ContainsKey(name);

    private static object? Nested(object? node, string name)
    {
        if (node is Dictionary<string, object?> map)
        {
            return map.TryGetValue(name, out var value) ? value : null;
        }

        return Prop(node, name);
    }

    private static bool PhpEmpty(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is bool flag)
        {
            return !flag;
        }

        if (value is int n)
        {
            return n == 0;
        }

        if (value is long l)
        {
            return l == 0;
        }

        if (value is double d)
        {
            return d == 0;
        }

        if (value is Dictionary<string, object?> map)
        {
            return map.Count == 0;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return text == "" || text == "0";
    }

    private static int ToInt(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
