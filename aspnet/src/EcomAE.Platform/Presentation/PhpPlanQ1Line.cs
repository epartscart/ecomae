using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-line article brand collector. PHP identifiers kept for the inventory:
/// <c>epc_article_brands_fetch_url</c>, <c>epc_article_brands_umapi_key</c>,
/// <c>epc_article_brands_crossbase_brand</c>, <c>epc_article_brands_add</c>,
/// <c>epc_article_brands_from_local_crosses</c>, <c>epc_article_brands_from_crossbase</c>,
/// <c>epc_article_brands_from_umapi</c>, <c>epc_article_brands_apply_synonyms</c>,
/// <c>epc_collect_article_catalog_brands</c>.
/// Path: <c>content/shop/docpart/docpart_epc_article_brands.php</c>.
/// GET never mints a session cookie. Leftover article-match / synonym / cache parents stay injected.
/// </summary>
public static class PhpPlanQ1Line
{
    public const string ArticleBrandsPath = "content/shop/docpart/docpart_epc_article_brands.php";
    public const int CrossbaseMax = 800;

    public static Func<string, string>? NormalizeArticle { get; set; }
    public static Func<MySqlConnection, (string Art, string Analog)>? AnalogExprs { get; set; }
    public static Func<string, string>? CacheRead { get; set; }
    public static Action<string, string>? CacheWrite { get; set; }
    public static Func<string, string>? FetchUrl { get; set; }
    public static Func<MySqlConnection, Dictionary<string, string>>? CanonicalMap { get; set; }
    public static Func<string, Dictionary<string, string>, string>? SynonymCanonical { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>, string, IEnumerable<string>>? WarehouseBrands { get; set; }
    public static Func<string, Dictionary<string, string>?, string>? HttpGet { get; set; }

    public static void Reset()
    {
        NormalizeArticle = value => Regex.Replace((value ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
        AnalogExprs = _ => ("`article`", "`analog`");
        CacheRead = _ => "";
        CacheWrite = (_, _) => { };
        FetchUrl = url => HttpGet != null ? HttpGet(url, null) : "";
        CanonicalMap = _ => new Dictionary<string, string>(StringComparer.Ordinal);
        SynonymCanonical = (_, _) => "";
        WarehouseBrands = (_, _, _) => [];
        HttpGet = (_, _) => "";
    }

    public static string EpcArticleBrandsFetchUrl(string url)
        => FetchUrl != null ? FetchUrl(url) : (HttpGet != null ? HttpGet(url, null) : "");

    public static string EpcArticleBrandsUmapiKey(Dictionary<string, object?> config)
    {
        var key = Str(config, "umapi_api_key");
        if (key == "")
        {
            key = Str(config, "umapi_api_url");
        }

        if (key.Contains('/'))
        {
            var parts = key.TrimEnd('/').Split('/');
            key = parts.Length > 0 ? parts[^1] : key;
        }

        return key.Trim();
    }

    public static string EpcArticleBrandsCrossbaseBrand(string number, string text)
    {
        text = WebUtility.HtmlDecode(Regex.Replace(text ?? "", "<[^>]*>", "")).Trim();
        if (text == "")
        {
            return "";
        }

        var brand = Regex.Replace(text, @"\s+" + Regex.Escape(number) + @"\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        if (brand == "")
        {
            var norm = NormalizeArticle != null ? NormalizeArticle(number) : number;
            brand = Regex.Replace(text, @"\s+" + Regex.Escape(norm) + @"\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        }

        return brand;
    }

    public static bool EpcArticleBrandsAdd(
        Dictionary<string, Dictionary<string, object?>> brands,
        Dictionary<string, bool> seen,
        object? brandObj,
        string name,
        string source)
    {
        var brand = Convert.ToString(brandObj)?.Trim() ?? "";
        if (brand == "" || brand.EnumerateRunes().Count() < 2)
        {
            return false;
        }

        var brandShow = brand.ToUpperInvariant();
        var key = brandShow;
        if (seen.ContainsKey(key))
        {
            if (name != "" && (Str(brands[key], "name") == "" || Str(brands[key], "name") == "Name not specified by the supplier"))
            {
                brands[key]["name"] = name;
            }

            var sources = (List<string>)brands[key]["sources"]!;
            if (!sources.Contains(source, StringComparer.Ordinal))
            {
                sources.Add(source);
            }

            return false;
        }

        seen[key] = true;
        brands[key] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["manufacturer"] = brand,
            ["manufacturer_show"] = brandShow,
            ["name"] = name != "" ? name : "Name not specified by the supplier",
            ["sources"] = new List<string> { source }
        };
        return true;
    }

    public static int EpcArticleBrandsFromLocalCrosses(
        MySqlConnection db,
        Dictionary<string, object?> config,
        string articleNorm,
        Dictionary<string, Dictionary<string, object?>> brands,
        Dictionary<string, bool> seen)
    {
        if (PhpEmpty(config.TryGetValue("local_crosses", out var lc) ? lc : null))
        {
            return 0;
        }

        var count = 0;
        try
        {
            var (artExpr, analogExpr) = AnalogExprs != null ? AnalogExprs(db) : ("`article`", "`analog`");
            using var q = db.CreateCommand();
            q.CommandText = "SELECT `article`, `manufacturer_article`, `analog`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE "
                + artExpr + " = @a OR " + analogExpr + " = @b ORDER BY `manufacturer_article`, `article`, `manufacturer_analog`, `analog` LIMIT 3000";
            q.Parameters.AddWithValue("@a", articleNorm);
            q.Parameters.AddWithValue("@b", articleNorm);
            using var r = q.ExecuteReader();
            while (r.Read())
            {
                var rowArticle = NormalizeArticle != null ? NormalizeArticle(Convert.ToString(r["article"]) ?? "") : Convert.ToString(r["article"]) ?? "";
                var rowAnalog = NormalizeArticle != null ? NormalizeArticle(Convert.ToString(r["analog"]) ?? "") : Convert.ToString(r["analog"]) ?? "";
                if (rowArticle == articleNorm && EpcArticleBrandsAdd(brands, seen, r["manufacturer_article"], "", "cp_crosses"))
                {
                    count++;
                }

                if (rowAnalog == articleNorm && EpcArticleBrandsAdd(brands, seen, r["manufacturer_analog"], "", "cp_crosses"))
                {
                    count++;
                }
            }
        }
        catch
        {
            return count;
        }

        return count;
    }

    public static int EpcArticleBrandsFromCrossbase(
        string articleInput,
        string articleNorm,
        Dictionary<string, Dictionary<string, object?>> brands,
        Dictionary<string, bool> seen)
    {
        var added = 0;
        var html = CacheRead != null ? CacheRead(articleInput) : "";
        if (html == "")
        {
            html = EpcArticleBrandsFetchUrl("https://crossbase.ru/cross/?q=" + Uri.EscapeDataString(articleInput));
            if (html != "")
            {
                CacheWrite?.Invoke(articleInput, html);
            }
            else
            {
                html = CacheRead != null ? CacheRead(articleInput) : "";
            }
        }

        if (html == "")
        {
            return 0;
        }

        var patterns = new[]
        {
            @"<tr>\s*<td[^>]*>\s*[0-9]+\s*</td>\s*<td[^>]*>\s*<a[^>]*href=[""']/cross/\?q=([^""']+)[""'][^>]*>(.*?)</a>",
            @"<a\s+[^>]*href=[""']/cross/\?q=([^""']+)[""'][^>]*>(.*?)</a>",
            @"<a\s+[^>]*href=[""']/cross/\?q=([^""']+)[""']"
        };
        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (matches.Count == 0)
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var number = Uri.UnescapeDataString(match.Groups[1].Value).Trim();
                var numberNorm = NormalizeArticle != null ? NormalizeArticle(number) : number;
                if (numberNorm == "" || numberNorm != articleNorm)
                {
                    continue;
                }

                var brand = match.Groups.Count > 2 ? EpcArticleBrandsCrossbaseBrand(number, match.Groups[2].Value) : "";
                if (EpcArticleBrandsAdd(brands, seen, brand, "", "crossbase"))
                {
                    added++;
                }

                if (added >= CrossbaseMax)
                {
                    return added;
                }
            }

            if (added > 0)
            {
                break;
            }
        }

        return added;
    }

    public static int EpcArticleBrandsFromUmapi(
        Dictionary<string, object?> config,
        string articleInput,
        Dictionary<string, Dictionary<string, object?>> brands,
        Dictionary<string, bool> seen)
    {
        var key = EpcArticleBrandsUmapiKey(config);
        if (key == "")
        {
            return 0;
        }

        var url = "https://api.umapi.ru/BrandRefinement/" + Uri.EscapeDataString(articleInput);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Accept"] = "application/json",
            ["X-App-Key"] = key
        };
        var body = HttpGet != null ? HttpGet(url, headers) : "";
        if (string.IsNullOrEmpty(body))
        {
            return 0;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            IEnumerable<System.Text.Json.JsonElement> list;
            if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                list = data.EnumerateArray();
            }
            else if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                list = root.EnumerateArray();
            }
            else
            {
                return 0;
            }

            var added = 0;
            foreach (var row in list)
            {
                if (row.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    continue;
                }

                var brand = FirstStr(row, "BRAND", "SUP_BRAND", "MANUFACTURER");
                var name = FirstStr(row, "TITLE", "DES");
                if (EpcArticleBrandsAdd(brands, seen, brand, name, "umapi"))
                {
                    added++;
                }
            }

            return added;
        }
        catch
        {
            return 0;
        }
    }

    public static void EpcArticleBrandsApplySynonyms(MySqlConnection? db, Dictionary<string, Dictionary<string, object?>> brands)
    {
        var map = db != null && CanonicalMap != null ? CanonicalMap(db) : new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in brands.Values)
        {
            var canonical = SynonymCanonical != null ? SynonymCanonical(Str(row, "manufacturer"), map) : "";
            if (canonical != "")
            {
                row["manufacturer_show"] = canonical;
            }
        }

        var merged = new List<Dictionary<string, object?>>();
        var seenShow = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var brandRow in brands.Values)
        {
            var show = Str(brandRow, "manufacturer_show").ToUpperInvariant().Trim();
            if (show == "")
            {
                show = Str(brandRow, "manufacturer").ToUpperInvariant().Trim();
            }

            if (show == "")
            {
                continue;
            }

            if (seenShow.TryGetValue(show, out var idx))
            {
                if (Str(brandRow, "name") != "" && (Str(merged[idx], "name") == "" || Str(merged[idx], "name") == "Name not specified by the supplier"))
                {
                    merged[idx]["name"] = brandRow["name"];
                }

                var dest = (List<string>)merged[idx]["sources"]!;
                foreach (var source in (List<string>)brandRow["sources"]!)
                {
                    if (!dest.Contains(source, StringComparer.Ordinal))
                    {
                        dest.Add(source);
                    }
                }

                continue;
            }

            seenShow[show] = merged.Count;
            brandRow["manufacturer_show"] = show;
            merged.Add(brandRow);
        }

        brands.Clear();
        var i = 0;
        foreach (var row in merged)
        {
            brands[(i++).ToString(CultureInfo.InvariantCulture)] = row;
        }
    }

    public static Dictionary<string, object?> EpcCollectArticleCatalogBrands(
        MySqlConnection db,
        Dictionary<string, object?> config,
        string articleInput)
    {
        var articleNorm = NormalizeArticle != null ? NormalizeArticle(articleInput) : articleInput;
        if (articleNorm == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = false,
                ["message"] = "Empty article",
                ["manufacturers"] = new List<Dictionary<string, object?>>()
            };
        }

        var brands = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var warehouseCount = 0;
        try
        {
            foreach (var warehouseBrand in WarehouseBrands != null ? WarehouseBrands(db, config, articleInput) : [])
            {
                if (EpcArticleBrandsAdd(brands, seen, warehouseBrand, "", "warehouse"))
                {
                    warehouseCount++;
                }
            }
        }
        catch
        {
            warehouseCount = 0;
        }

        var cpCount = EpcArticleBrandsFromLocalCrosses(db, config, articleNorm, brands, seen);
        var crossbaseCount = EpcArticleBrandsFromCrossbase(articleInput, articleNorm, brands, seen);
        var umapiCount = EpcArticleBrandsFromUmapi(config, articleInput, brands, seen);
        EpcArticleBrandsApplySynonyms(db, brands);
        var manufacturers = new List<Dictionary<string, object?>>();
        foreach (var brandRow in brands.Values)
        {
            var sources = (List<string>)brandRow["sources"]!;
            manufacturers.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["manufacturer"] = brandRow["manufacturer"],
                ["manufacturer_show"] = brandRow["manufacturer_show"],
                ["manufacturer_id"] = 0,
                ["name"] = brandRow["name"],
                ["storage_id"] = 0,
                ["office_id"] = 0,
                ["synonyms_single_query"] = true,
                ["params"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["type"] = sources.Contains("warehouse", StringComparer.Ordinal) ? "prices" : "catalog",
                    ["sources"] = sources
                }
            });
        }

        manufacturers.Sort((a, b) => string.CompareOrdinal(Convert.ToString(a["manufacturer_show"]), Convert.ToString(b["manufacturer_show"])));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = true,
            ["article"] = articleNorm,
            ["warehouse_count"] = warehouseCount,
            ["cp_crosses_count"] = cpCount,
            ["crossbase_count"] = crossbaseCount,
            ["umapi_count"] = umapiCount,
            ["manufacturers"] = manufacturers
        };
    }

    private static string FirstStr(System.Text.Json.JsonElement row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var s = v.GetString() ?? "";
                if (s != "")
                {
                    return s;
                }
            }
        }

        return "";
    }

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? Convert.ToString(v) ?? "" : "";

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d
            || (value is string s && (s == "" || s == "0"));
}
