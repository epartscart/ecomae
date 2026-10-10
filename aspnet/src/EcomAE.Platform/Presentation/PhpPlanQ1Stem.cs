using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-stem accessories catalog. PHP identifiers kept for the inventory:
/// <c>epc_acc_cache_path</c>, <c>epc_acc_fetch_raw_rows</c>, <c>epc_acc_build_items</c>,
/// <c>epc_acc_load_catalog</c>, <c>epc_acc_search</c>.
/// Path: <c>content/shop/docpart/epc_accessories_catalog.php</c>.
/// GET never mints a session cookie. Leftover article-match parent stays injected.
/// Taxonomy helpers are already mentioned and stay injectable.
/// </summary>
public static class PhpPlanQ1Stem
{
    public const string AccessoriesCatalogPath = "content/shop/docpart/epc_accessories_catalog.php";

    public static string DocumentRoot { get; set; } = "";
    public static string TempDir { get; set; } = "";
    public static Func<string, string>? NormalizeArticle { get; set; }
    public static Func<string, string, Dictionary<string, string>>? Classify { get; set; }
    public static Func<Dictionary<string, string>>? WarehouseRegions { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? Taxonomy { get; set; }
    public static Func<long>? Clock { get; set; }
    public static Func<string, string?>? ReadFile { get; set; }
    public static Action<string, string>? WriteFile { get; set; }

    public static void Reset()
    {
        DocumentRoot = "";
        TempDir = Path.GetTempPath().TrimEnd('/', '\\');
        NormalizeArticle = value => System.Text.RegularExpressions.Regex.Replace((value ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
        Classify = (name, brand) => PhpReadyNamed.EpcAccClassify(name, brand);
        WarehouseRegions = PhpReadyNamed.EpcAccWarehouseRegions;
        Taxonomy = DefaultTaxonomy;
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ReadFile = path => File.Exists(path) ? File.ReadAllText(path) : null;
        WriteFile = File.WriteAllText;
    }

    public static string EpcAccCachePath()
    {
        var doc = DocumentRoot != "" ? DocumentRoot : "epartscart";
        var tmp = (TempDir != "" ? TempDir : Path.GetTempPath()).TrimEnd('/', '\\');
        return tmp + "/epc_acc_catalog_v1_" + Md5Hex(doc) + ".json";
    }

    public static List<Dictionary<string, object?>> EpcAccFetchRawRows(MySqlConnection db, int limit = 8000)
    {
        limit = Math.Max(200, Math.Min(limit, 12000));
        var sql = "SELECT TRIM(p.`manufacturer`) AS `brand`, TRIM(p.`article`) AS `article`, "
            + "COALESCE(NULLIF(TRIM(`article_show`), ''), TRIM(`article`)) AS `article_show`, "
            + "TRIM(IFNULL(p.`name`, '')) AS `name`, IFNULL(p.`exist`, 0) AS `qty`, IFNULL(p.`price`, 0) AS `price`, "
            + "TRIM(IFNULL(p.`storage`, '')) AS `warehouse`, IFNULL(p.`price_id`, 0) AS `price_id` "
            + "FROM `shop_docpart_prices_data` p "
            + "WHERE IFNULL(p.`price`, 0) > 0 AND IFNULL(p.`exist`, 0) > 0 "
            + "AND TRIM(IFNULL(p.`manufacturer`, '')) != '' AND TRIM(p.`article`) != '' "
            + "ORDER BY IFNULL(p.`exist`, 0) DESC LIMIT " + limit;
        try
        {
            var rows = new List<Dictionary<string, object?>>();
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["brand"] = Convert.ToString(r["brand"]) ?? "",
                    ["article"] = Convert.ToString(r["article"]) ?? "",
                    ["article_show"] = Convert.ToString(r["article_show"]) ?? "",
                    ["name"] = Convert.ToString(r["name"]) ?? "",
                    ["qty"] = ToDouble(r["qty"]),
                    ["price"] = ToDouble(r["price"]),
                    ["warehouse"] = Convert.ToString(r["warehouse"]) ?? "",
                    ["price_id"] = ToInt(r["price_id"])
                });
            }

            return rows;
        }
        catch
        {
            return [];
        }
    }

    public static List<Dictionary<string, object?>> EpcAccBuildItems(IEnumerable<Dictionary<string, object?>> rows)
    {
        var regions = WarehouseRegions != null ? WarehouseRegions() : new Dictionary<string, string>(StringComparer.Ordinal);
        var merged = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var brand = (Convert.ToString(row.TryGetValue("brand", out var b) ? b : "") ?? "").Trim().ToUpperInvariant();
            var article = (Convert.ToString(row.TryGetValue("article_show", out var ash) && !string.IsNullOrEmpty(Convert.ToString(ash)) ? ash : row.TryGetValue("article", out var a) ? a : "") ?? "").Trim();
            var articleNorm = NormalizeArticle != null ? NormalizeArticle(article) : article;
            if (brand == "" || articleNorm == "")
            {
                continue;
            }

            var qty = ToDouble(row.TryGetValue("qty", out var q) ? q : 0);
            if (qty <= 0 || qty > 50000)
            {
                continue;
            }

            var price = ToDouble(row.TryGetValue("price", out var p) ? p : 0);
            if (price <= 0)
            {
                continue;
            }

            var name = (Convert.ToString(row.TryGetValue("name", out var n) ? n : "") ?? "").Trim();
            var warehouse = (Convert.ToString(row.TryGetValue("warehouse", out var w) ? w : "") ?? "").Trim();
            var key = brand + "|" + articleNorm;
            if (!merged.TryGetValue(key, out var item))
            {
                var cls = Classify != null ? Classify(name != "" ? name : article, brand) : OtherClass();
                var region = regions.TryGetValue(warehouse, out var reg) ? reg : (warehouse != "" ? warehouse : "UAE warehouse");
                merged[key] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = Md5Hex(key)[..12],
                    ["brand"] = brand,
                    ["article"] = article,
                    ["article_norm"] = articleNorm,
                    ["name"] = name != "" ? name : (brand + " " + article),
                    ["qty"] = qty,
                    ["price"] = price,
                    ["warehouse"] = warehouse,
                    ["region"] = region,
                    ["category"] = cls["category"],
                    ["subcategory"] = cls["subcategory"],
                    ["category_label"] = cls["category_label"],
                    ["subcategory_label"] = cls["subcategory_label"]
                };
                continue;
            }

            item["qty"] = ToDouble(item["qty"]) + qty;
            if (price < ToDouble(item["price"]))
            {
                item["price"] = price;
            }

            if (Convert.ToString(item["name"]) == brand + " " + article && name != "")
            {
                item["name"] = name;
                var cls = Classify != null ? Classify(name, brand) : OtherClass();
                item["category"] = cls["category"];
                item["subcategory"] = cls["subcategory"];
                item["category_label"] = cls["category_label"];
                item["subcategory_label"] = cls["subcategory_label"];
            }

            if (Convert.ToString(item["warehouse"]) == "" && warehouse != "")
            {
                item["warehouse"] = warehouse;
                item["region"] = regions.TryGetValue(warehouse, out var reg) ? reg : warehouse;
            }
        }

        return merged.Values.ToList();
    }

    public static List<Dictionary<string, object?>> EpcAccLoadCatalog(MySqlConnection db, bool refresh = false)
    {
        var path = EpcAccCachePath();
        if (!refresh)
        {
            var raw = ReadFile != null ? ReadFile(path) : (File.Exists(path) ? File.ReadAllText(path) : null);
            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("built_at", out var built)
                        && doc.RootElement.TryGetProperty("items", out var itemsEl)
                        && itemsEl.ValueKind == JsonValueKind.Array
                        && (Clock != null ? Clock() : DateTimeOffset.UtcNow.ToUnixTimeSeconds()) - built.GetInt64() < 600)
                    {
                        return itemsEl.EnumerateArray().Select(JsonObj).ToList();
                    }
                }
                catch
                {
                    // rebuild
                }
            }
        }

        var items = EpcAccBuildItems(EpcAccFetchRawRows(db, 9000));
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["built_at"] = Clock != null ? Clock() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["items"] = items
        });
        try
        {
            WriteFile?.Invoke(path, payload);
        }
        catch
        {
            // ignore
        }

        return items;
    }

    public static Dictionary<string, object?> EpcAccSearch(MySqlConnection db, Dictionary<string, object?>? filters = null)
    {
        filters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var items = EpcAccLoadCatalog(db, !PhpEmpty(filters.TryGetValue("refresh", out var rf) ? rf : null));
        var q = (Convert.ToString(filters.TryGetValue("q", out var qv) ? qv : "") ?? "").Trim();
        var qNorm = q != "" && NormalizeArticle != null ? NormalizeArticle(q) : "";
        var qLower = q.ToLowerInvariant();
        var category = (Convert.ToString(filters.TryGetValue("category", out var cv) ? cv : "") ?? "").Trim();
        var subcategory = (Convert.ToString(filters.TryGetValue("subcategory", out var sv) ? sv : "") ?? "").Trim();
        var brand = (Convert.ToString(filters.TryGetValue("brand", out var bv) ? bv : "") ?? "").Trim().ToUpperInvariant();
        var region = (Convert.ToString(filters.TryGetValue("region", out var rv) ? rv : "") ?? "").Trim();
        var warehouse = (Convert.ToString(filters.TryGetValue("warehouse", out var wv) ? wv : "") ?? "").Trim();
        var priceMin = ToDouble(filters.TryGetValue("price_min", out var pmin) ? pmin : 0);
        var priceMax = ToDouble(filters.TryGetValue("price_max", out var pmax) ? pmax : 0);
        var inStock = !filters.ContainsKey("in_stock") || Convert.ToString(filters["in_stock"]) != "0";
        var sort = Convert.ToString(filters.TryGetValue("sort", out var so) ? so : "") ?? "";
        if (sort == "")
        {
            sort = "price-desc";
        }

        var page = Math.Max(1, ToInt(filters.TryGetValue("page", out var pg) ? pg : 1));
        var perPage = ToInt(filters.TryGetValue("per_page", out var pp) ? pp : 24);
        perPage = Math.Max(12, Math.Min(perPage, 60));

        var filtered = new List<Dictionary<string, object?>>();
        foreach (var item in items)
        {
            if (inStock && ToDouble(item["qty"]) <= 0)
            {
                continue;
            }

            if (category != "" && Convert.ToString(item["category"]) != category)
            {
                continue;
            }

            if (subcategory != "" && Convert.ToString(item["subcategory"]) != subcategory)
            {
                continue;
            }

            if (brand != "" && Convert.ToString(item["brand"]) != brand)
            {
                continue;
            }

            if (warehouse != "" && !string.Equals(Convert.ToString(item["warehouse"]), warehouse, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (region != "" && !string.Equals(Convert.ToString(item["region"]), region, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (priceMin > 0 && ToDouble(item["price"]) < priceMin)
            {
                continue;
            }

            if (priceMax > 0 && ToDouble(item["price"]) > priceMax)
            {
                continue;
            }

            if (q != "")
            {
                var hay = ((Convert.ToString(item["name"]) ?? "") + " " + (Convert.ToString(item["brand"]) ?? "") + " " + (Convert.ToString(item["article"]) ?? "")).ToLowerInvariant();
                var match = hay.Contains(qLower, StringComparison.Ordinal)
                    || (qNorm != "" && Convert.ToString(item["article_norm"]) == qNorm)
                    || (qNorm != "" && (Convert.ToString(item["article_norm"]) ?? "").Contains(qNorm, StringComparison.Ordinal));
                if (!match)
                {
                    continue;
                }
            }

            filtered.Add(item);
        }

        filtered.Sort((a, b) => sort switch
        {
            "price-asc" => ToDouble(a["price"]).CompareTo(ToDouble(b["price"])),
            "qty-desc" or "updated-desc" or "top-sales" => ToDouble(b["qty"]).CompareTo(ToDouble(a["qty"])),
            "name-asc" => string.Compare(Convert.ToString(a["name"]), Convert.ToString(b["name"]), StringComparison.OrdinalIgnoreCase),
            _ => ToDouble(b["price"]).CompareTo(ToDouble(a["price"]))
        });

        var total = filtered.Count;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)perPage));
        if (page > pages)
        {
            page = pages;
        }

        var offset = (page - 1) * perPage;
        var slice = filtered.Skip(offset).Take(perPage).ToList();
        var from = total == 0 ? 0 : offset + 1;
        var to = Math.Min(total, offset + slice.Count);

        var facetCats = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var facetBrands = new Dictionary<string, int>(StringComparer.Ordinal);
        var facetRegions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in filtered)
        {
            var c = Convert.ToString(item["category"]) ?? "";
            if (!facetCats.TryGetValue(c, out var catRef))
            {
                catRef = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["slug"] = c,
                    ["label"] = item["category_label"],
                    ["count"] = 0,
                    ["subs"] = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
                };
                facetCats[c] = catRef;
            }

            catRef["count"] = ToInt(catRef["count"]) + 1;
            var s = Convert.ToString(item["subcategory"]) ?? "";
            var subs = (Dictionary<string, Dictionary<string, object?>>)catRef["subs"]!;
            if (!subs.TryGetValue(s, out var subRef))
            {
                subRef = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["slug"] = s,
                    ["label"] = item["subcategory_label"],
                    ["count"] = 0
                };
                subs[s] = subRef;
            }

            subRef["count"] = ToInt(subRef["count"]) + 1;

            var bName = Convert.ToString(item["brand"]) ?? "";
            facetBrands[bName] = facetBrands.TryGetValue(bName, out var bc) ? bc + 1 : 1;
            var rName = Convert.ToString(item["region"]) ?? "";
            if (rName != "")
            {
                facetRegions[rName] = facetRegions.TryGetValue(rName, out var rc) ? rc + 1 : 1;
            }
        }

        var catList = facetCats.Values.Select(cat =>
        {
            var subs = ((Dictionary<string, Dictionary<string, object?>>)cat["subs"]!).Values.ToList();
            subs.Sort((a, b) => ToInt(b["count"]).CompareTo(ToInt(a["count"])));
            cat["subs"] = subs;
            return cat;
        }).ToList();
        catList.Sort((a, b) => ToInt(b["count"]).CompareTo(ToInt(a["count"])));
        var brandList = facetBrands.OrderByDescending(kv => kv.Value).Take(40)
            .Select(kv => new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = kv.Key, ["count"] = kv.Value }).ToList();
        var regionList = facetRegions.OrderByDescending(kv => kv.Value)
            .Select(kv => new Dictionary<string, object?>(StringComparer.Ordinal) { ["region"] = kv.Key, ["count"] = kv.Value }).ToList();

        var taxOut = new List<Dictionary<string, object?>>();
        var tax = Taxonomy != null ? Taxonomy() : DefaultTaxonomy();
        foreach (var (slug, cat) in tax)
        {
            var subsObj = cat.TryGetValue("subs", out var s) ? s : null;
            var subs = new List<Dictionary<string, object?>>();
            if (subsObj is Dictionary<string, Dictionary<string, object?>> dict)
            {
                foreach (var (subSlug, sub) in dict)
                {
                    subs.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["slug"] = subSlug, ["label"] = sub["label"] });
                }
            }

            taxOut.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["slug"] = slug,
                ["label"] = cat["label"],
                ["icon"] = cat.TryGetValue("icon", out var ic) && !string.IsNullOrEmpty(Convert.ToString(ic)) ? ic : "fa-tag",
                ["subs"] = subs
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = total,
            ["page"] = page,
            ["per_page"] = perPage,
            ["pages"] = pages,
            ["from"] = from,
            ["to"] = to,
            ["items"] = slice,
            ["facets"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["categories"] = catList,
                ["brands"] = brandList,
                ["regions"] = regionList
            },
            ["taxonomy"] = taxOut,
            ["sort"] = sort
        };
    }

    private static Dictionary<string, Dictionary<string, object?>> DefaultTaxonomy()
    {
        using var doc = JsonDocument.Parse(PhpReadyNamed.EpcAccTaxonomy().GetRawText());
        var outDict = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var cat in doc.RootElement.EnumerateObject())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = cat.Value.TryGetProperty("label", out var l) ? l.GetString() : cat.Name,
                ["icon"] = cat.Value.TryGetProperty("icon", out var i) ? i.GetString() : "fa-tag"
            };
            var subs = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            if (cat.Value.TryGetProperty("subs", out var subsEl) && subsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var sub in subsEl.EnumerateObject())
                {
                    subs[sub.Name] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["label"] = sub.Value.TryGetProperty("label", out var sl) ? sl.GetString() : sub.Name
                    };
                }
            }

            row["subs"] = subs;
            outDict[cat.Name] = row;
        }

        return outDict;
    }

    private static Dictionary<string, string> OtherClass()
        => new(StringComparer.Ordinal)
        {
            ["category"] = "other",
            ["subcategory"] = "general",
            ["category_label"] = "Other Parts",
            ["subcategory_label"] = "General"
        };

    private static Dictionary<string, object?> JsonObj(JsonElement el)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in el.EnumerateObject())
        {
            row[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.TryGetInt64(out var n) ? n : p.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => p.Value.GetRawText()
            };
        }

        return row;
    }

    private static string Md5Hex(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int ToInt(object? value)
        => int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static double ToDouble(object? value)
        => double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d
            || (value is string s && (s == "" || s == "0"));
}
