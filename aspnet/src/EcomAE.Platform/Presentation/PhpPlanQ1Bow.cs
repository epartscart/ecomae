using System.Globalization;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-bow product-family catalog. PHP identifiers kept for the inventory:
/// <c>epc_pf_load_demand_helpers</c>, <c>epc_pf_map_stock_rows</c>,
/// <c>epc_pf_infer_product_group</c>, <c>epc_pf_ensure_umapi_group_schema</c>,
/// <c>epc_pf_top_brands_slice</c>, <c>epc_pf_resolve_product_group</c>,
/// <c>epc_pf_fetch_catalog_lines</c>, <c>epc_pf_build_catalog_from_lines</c>,
/// <c>epc_pf_job_products_list</c>, <c>epc_pf_job_build_brands_summary</c>,
/// <c>epc_pf_products_for_cards</c>, <c>epc_pf_find_group</c>,
/// <c>epc_pf_group_detail</c>.
/// Path: <c>content/shop/docpart/epc_product_family.php</c>.
/// GET never mints a session cookie. Leftover article-match and demand parents stay injected.
/// </summary>
public static class PhpPlanQ1Bow
{
    public const string ProductFamilyPath = "content/shop/docpart/epc_product_family.php";

    private static readonly (string Label, string[] Needles)[] InferRules =
    [
        ("Piston", ["piston"]),
        ("Gasket", ["gasket"]),
        ("Oil filter", ["oil filter"]),
        ("Filter", ["filter"]),
        ("Brake", ["brake pad", "brake disc"]),
        ("Bearing", ["bearing"]),
        ("Engine", ["engine", "cylinder", "valve"])
    ];

    public static Func<string, string>? NormalizeArticle { get; set; }
    public static Action? LoadDemand { get; set; }
    public static Func<List<Dictionary<string, object?>>, List<Dictionary<string, object?>>>? DemandMapStockRows { get; set; }
    public static Func<string, string, string>? DemandInfer { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>, List<Dictionary<string, object?>>>? DemandJobProducts { get; set; }
    public static Func<List<Dictionary<string, object?>>, List<Dictionary<string, object?>>>? DemandJobBrands { get; set; }
    public static Func<object?, string, string, int>? DemandResolveArtId { get; set; }
    public static Func<object?, string>? DemandSiteBase { get; set; }
    public static Func<string, int, Dictionary<string, object?>?>? DemandHttpJson { get; set; }
    public static Func<long>? Clock { get; set; }

    public static void Reset()
    {
        NormalizeArticle = value => Regex.Replace((value ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
        LoadDemand = null;
        DemandMapStockRows = null;
        DemandInfer = null;
        DemandJobProducts = null;
        DemandJobBrands = null;
        DemandResolveArtId = null;
        DemandSiteBase = null;
        DemandHttpJson = null;
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static void EpcPfLoadDemandHelpers() => LoadDemand?.Invoke();

    public static List<Dictionary<string, object?>> EpcPfMapStockRows(IEnumerable<Dictionary<string, object?>> rows)
    {
        var list = rows.Select(Copy).ToList();
        EpcPfLoadDemandHelpers();
        if (DemandMapStockRows is not null)
        {
            return DemandMapStockRows(list);
        }

        var output = new List<Dictionary<string, object?>>();
        foreach (var row in list)
        {
            var brand = Str(row, "brand").Trim();
            var article = FirstNonEmpty(row, "article_show", "article").Trim();
            if (brand == "" || article == "")
            {
                continue;
            }

            output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = brand,
                ["article"] = article,
                ["article_show"] = article,
                ["name"] = Str(row, "name").Trim(),
                ["qty"] = ToDouble(row.TryGetValue("qty", out var qty) ? qty : 0),
                ["price"] = row.TryGetValue("price", out var price) ? price : "",
                ["warehouse"] = Str(row, "warehouse").Trim(),
                ["article_norm"] = Norm(row.TryGetValue("article", out var art) ? art : article)
            });
        }

        return output;
    }

    public static string EpcPfInferProductGroup(string umapiGroup, string partName)
    {
        EpcPfLoadDemandHelpers();
        if (DemandInfer is not null)
        {
            return DemandInfer(umapiGroup, partName);
        }

        var group = (umapiGroup ?? "").Trim();
        if (group != "")
        {
            return group;
        }

        var hay = (partName ?? "").ToLowerInvariant();
        foreach (var (label, needles) in InferRules)
        {
            foreach (var needle in needles)
            {
                if (needle != "" && hay.Contains(needle, StringComparison.Ordinal))
                {
                    return label;
                }
            }
        }

        return partName != "" ? "Other parts" : "Uncategorized";
    }

    public static void EpcPfEnsureUmapiGroupSchema(MySqlConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS `epc_umapi_product_group` (
                `manufacturer` VARCHAR(128) NOT NULL,
                `article_norm` VARCHAR(64) NOT NULL,
                `product_group` VARCHAR(128) NOT NULL DEFAULT '',
                `umapi_raw` VARCHAR(255) NOT NULL DEFAULT '',
                `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
                PRIMARY KEY (`manufacturer`, `article_norm`),
                KEY `product_group` (`product_group`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;
            """;
        cmd.ExecuteNonQuery();
    }

    public static Dictionary<string, object?> EpcPfTopBrandsSlice(IEnumerable<Dictionary<string, object?>> brands, int limit = 10)
    {
        var list = brands.Select(Copy).ToList();
        list.Sort((a, b) =>
        {
            var qa = ToDouble(a.TryGetValue("total_qty", out var av) ? av : 0);
            var qb = ToDouble(b.TryGetValue("total_qty", out var bv) ? bv : 0);
            if (qa != qb)
            {
                return qb.CompareTo(qa);
            }

            return string.CompareOrdinal(Str(a, "brand"), Str(b, "brand"));
        });
        var take = Math.Max(1, limit);
        var top = list.Take(take).ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["top"] = top,
            ["more_count"] = Math.Max(0, list.Count - top.Count),
            ["total"] = list.Count
        };
    }

    public static string EpcPfResolveProductGroup(object? config, MySqlConnection? db, string brand, string article, string name, ref int umapiBudget)
    {
        brand = (brand ?? "").Trim();
        var articleNorm = Norm(article);
        name = (name ?? "").Trim();
        if (brand == "" || articleNorm == "")
        {
            return EpcPfInferProductGroup("", name);
        }

        var umapiRaw = "";
        if (db is not null)
        {
            EpcPfEnsureUmapiGroupSchema(db);
            try
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT `product_group`, `umapi_raw` FROM `epc_umapi_product_group` WHERE `manufacturer` = @b AND `article_norm` = @a LIMIT 1";
                cmd.Parameters.AddWithValue("@b", brand);
                cmd.Parameters.AddWithValue("@a", articleNorm);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var cachedGroup = (Convert.ToString(reader["product_group"]) ?? "").Trim();
                    if (cachedGroup != "")
                    {
                        return cachedGroup;
                    }

                    umapiRaw = (Convert.ToString(reader["umapi_raw"]) ?? "").Trim();
                }
            }
            catch
            {
            }
        }

        var artId = 0;
        if (umapiRaw == "" && config is not null && umapiBudget > 0)
        {
            EpcPfLoadDemandHelpers();
            if (DemandResolveArtId is null)
            {
                return EpcPfInferProductGroup("", name);
            }

            umapiBudget--;
            artId = DemandResolveArtId(config, brand, article);
            if (artId > 0)
            {
                var baseUrl = DemandSiteBase?.Invoke(config) ?? "";
                if (baseUrl != "")
                {
                    var query = "action=article&id=" + artId + "&language=en";
                    var detail = DemandHttpJson?.Invoke(baseUrl + "/api/umapi_proxy.php?" + query, 12);
                    if (detail is not null)
                    {
                        umapiRaw = FirstNonEmpty(detail, "PT_DES", "PRODUCT_GROUP", "ART_PRODUCT_NAME").Trim();
                        if (umapiRaw == "")
                        {
                            umapiRaw = FirstNonEmpty(detail, "COMPLETE_DES", "DES").Trim();
                        }
                    }
                }
            }

            if (db is not null && (umapiRaw != "" || artId > 0))
            {
                try
                {
                    var label = EpcPfInferProductGroup(umapiRaw, name);
                    using var ins = db.CreateCommand();
                    ins.CommandText = """
                        INSERT INTO `epc_umapi_product_group` (`manufacturer`, `article_norm`, `product_group`, `umapi_raw`, `updated_at`)
                        VALUES (@b, @a, @g, @r, @t)
                        ON DUPLICATE KEY UPDATE `product_group` = VALUES(`product_group`), `umapi_raw` = VALUES(`umapi_raw`), `updated_at` = VALUES(`updated_at`)
                        """;
                    ins.Parameters.AddWithValue("@b", brand);
                    ins.Parameters.AddWithValue("@a", articleNorm);
                    ins.Parameters.AddWithValue("@g", label);
                    ins.Parameters.AddWithValue("@r", umapiRaw);
                    ins.Parameters.AddWithValue("@t", Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    ins.ExecuteNonQuery();
                    if (label != "" && label != "Other parts")
                    {
                        return label;
                    }
                }
                catch
                {
                }
            }
        }

        if (umapiRaw != "")
        {
            var fromUmapi = EpcPfInferProductGroup(umapiRaw, name);
            if (fromUmapi != "")
            {
                return fromUmapi;
            }
        }

        return EpcPfInferProductGroup("", name);
    }

    public static List<Dictionary<string, object?>> EpcPfFetchCatalogLines(MySqlConnection db, int fetchLimit = 2500)
    {
        fetchLimit = Math.Max(100, Math.Min(fetchLimit, 5000));
        var sql = "SELECT TRIM(`manufacturer`) AS `brand`, TRIM(`article`) AS `article`, "
            + "COALESCE(NULLIF(TRIM(`article_show`), ''), TRIM(`article`)) AS `article_show`, "
            + "TRIM(IFNULL(`name`, '')) AS `name`, IFNULL(`exist`, 0) AS `qty`, IFNULL(`price`, 0) AS `price`, "
            + "TRIM(IFNULL(`storage`, '')) AS `warehouse` "
            + "FROM `shop_docpart_prices_data` "
            + "WHERE IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0 "
            + "AND TRIM(IFNULL(`manufacturer`, '')) != '' AND TRIM(`article`) != '' "
            + "ORDER BY IFNULL(`exist`, 0) DESC LIMIT " + Math.Min(fetchLimit * 3, 12000);
        List<Dictionary<string, object?>> raw;
        try
        {
            raw = [];
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                raw.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["brand"] = Convert.ToString(reader["brand"]) ?? "",
                    ["article"] = Convert.ToString(reader["article"]) ?? "",
                    ["article_show"] = Convert.ToString(reader["article_show"]) ?? "",
                    ["name"] = Convert.ToString(reader["name"]) ?? "",
                    ["qty"] = ToDouble(reader["qty"]),
                    ["price"] = reader["price"],
                    ["warehouse"] = Convert.ToString(reader["warehouse"]) ?? ""
                });
            }
        }
        catch
        {
            return [];
        }

        var merged = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var row in EpcPfMapStockRows(raw))
        {
            if (ToDouble(row.TryGetValue("qty", out var q) ? q : 0) > 50000)
            {
                continue;
            }

            var key = MbUpper(Str(row, "brand").Trim()) + "|" + Norm(row.TryGetValue("article", out var art) ? art : "");
            if (key == "|")
            {
                continue;
            }

            if (!merged.TryGetValue(key, out var existing))
            {
                merged[key] = row;
                continue;
            }

            existing["qty"] = ToDouble(existing["qty"]) + ToDouble(row.TryGetValue("qty", out var add) ? add : 0);
            if (Str(existing, "name") == "" && Str(row, "name") != "")
            {
                existing["name"] = row["name"];
            }
        }

        var rows = merged.Values.ToList();
        rows.Sort((a, b) => ToDouble(b["qty"]).CompareTo(ToDouble(a["qty"])));
        return rows.Take(fetchLimit).ToList();
    }

    public static Dictionary<string, object?> EpcPfBuildCatalogFromLines(
        IEnumerable<Dictionary<string, object?>> lines,
        object? config = null,
        MySqlConnection? db = null,
        int umapiLookupBudget = 160)
    {
        var partLines = new List<Dictionary<string, object?>>();
        var productGroups = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var umapiBudget = Math.Max(0, umapiLookupBudget);
        var sorted = lines.Select(Copy).ToList();
        sorted.Sort((a, b) => ToDouble(b.TryGetValue("qty", out var qb) ? qb : 0).CompareTo(ToDouble(a.TryGetValue("qty", out var qa) ? qa : 0)));

        foreach (var row in sorted)
        {
            var brand = Str(row, "brand").Trim();
            var article = FirstNonEmpty(row, "article_show", "article").Trim();
            var articleNorm = Str(row, "article_norm").Trim();
            if (articleNorm == "")
            {
                articleNorm = Norm(article);
            }

            var name = Str(row, "name").Trim();
            var qty = ToDouble(row.TryGetValue("qty", out var qv) ? qv : 0);
            if (brand == "" || articleNorm == "")
            {
                continue;
            }

            string groupLabel;
            if (config is not null && db is not null)
            {
                groupLabel = EpcPfResolveProductGroup(config, db, brand, article, name, ref umapiBudget);
            }
            else
            {
                groupLabel = EpcPfInferProductGroup("", name);
            }

            var groupKey = MbLower(groupLabel);
            if (groupKey == "")
            {
                groupKey = "other";
                groupLabel = "Other parts";
            }

            var partRow = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = brand,
                ["article"] = article,
                ["article_norm"] = articleNorm,
                ["name"] = name,
                ["product_group"] = groupLabel,
                ["qty"] = qty,
                ["price"] = row.TryGetValue("price", out var price) ? price : 0
            };
            partLines.Add(partRow);

            if (!productGroups.TryGetValue(groupKey, out var group))
            {
                group = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = groupLabel,
                    ["parts_count"] = 0,
                    ["total_qty"] = 0.0,
                    ["samples"] = new List<object?>(),
                    ["brands"] = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal),
                    ["parts"] = new List<object?>()
                };
                productGroups[groupKey] = group;
            }

            var brandKey = MbUpper(brand);
            ((List<object?>)group["parts"]!).Add(partRow);
            group["parts_count"] = ToInt(group["parts_count"]) + 1;
            group["total_qty"] = ToDouble(group["total_qty"]) + qty;
            var samples = (List<object?>)group["samples"]!;
            if (samples.Count < 5)
            {
                samples.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = brand, ["article"] = article });
            }

            var brands = (Dictionary<string, Dictionary<string, object?>>)group["brands"]!;
            if (!brands.TryGetValue(brandKey, out var brandRow))
            {
                brandRow = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["brand"] = brand,
                    ["parts_count"] = 0,
                    ["total_qty"] = 0.0
                };
                brands[brandKey] = brandRow;
            }

            brandRow["parts_count"] = ToInt(brandRow["parts_count"]) + 1;
            brandRow["total_qty"] = ToDouble(brandRow["total_qty"]) + qty;
        }

        EpcPfLoadDemandHelpers();
        List<Dictionary<string, object?>> products;
        List<Dictionary<string, object?>> brandsSummary;
        if (DemandJobProducts is not null)
        {
            products = DemandJobProducts(productGroups);
            brandsSummary = DemandJobBrands is not null ? DemandJobBrands(partLines) : EpcPfJobBuildBrandsSummary(partLines);
        }
        else
        {
            products = EpcPfJobProductsList(productGroups);
            brandsSummary = EpcPfJobBuildBrandsSummary(partLines);
        }

        var totalQty = partLines.Sum(line => ToDouble(line.TryGetValue("qty", out var q) ? q : 0));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["part_lines"] = partLines,
            ["product_groups"] = productGroups,
            ["products"] = products,
            ["summary"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["parts_count"] = partLines.Count,
                ["product_groups_count"] = products.Count,
                ["brands_count"] = brandsSummary.Count,
                ["total_stock_qty"] = totalQty,
                ["brands"] = brandsSummary
            }
        };
    }

    public static List<Dictionary<string, object?>> EpcPfJobProductsList(Dictionary<string, Dictionary<string, object?>> productGroupsMap)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var group in productGroupsMap.Values)
        {
            var brandsMap = group.TryGetValue("brands", out var rawBrands) && rawBrands is Dictionary<string, Dictionary<string, object?>> map
                ? map
                : new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            var brandsList = brandsMap.Values.Select(Copy).ToList();
            brandsList.Sort((a, b) =>
            {
                var qa = ToDouble(a.TryGetValue("total_qty", out var av) ? av : 0);
                var qb = ToDouble(b.TryGetValue("total_qty", out var bv) ? bv : 0);
                if (qa != qb)
                {
                    return qb.CompareTo(qa);
                }

                return string.CompareOrdinal(Str(a, "brand"), Str(b, "brand"));
            });
            var parts = group.TryGetValue("parts", out var rawParts) && rawParts is List<object?> partList
                ? partList.ToList()
                : [];
            list.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = Str(group, "label"),
                ["parts_count"] = ToInt(group.TryGetValue("parts_count", out var pc) ? pc : 0),
                ["total_qty"] = ToDouble(group.TryGetValue("total_qty", out var tq) ? tq : 0),
                ["samples"] = group.TryGetValue("samples", out var samples) && samples is List<object?> sampleList ? sampleList : new List<object?>(),
                ["parts"] = parts,
                ["brands"] = brandsList
            });
        }

        list.Sort((a, b) =>
        {
            var ca = ToInt(a.TryGetValue("parts_count", out var ac) ? ac : 0);
            var cb = ToInt(b.TryGetValue("parts_count", out var bc) ? bc : 0);
            if (ca != cb)
            {
                return cb.CompareTo(ca);
            }

            return string.CompareOrdinal(Str(a, "label"), Str(b, "label"));
        });
        return list;
    }

    public static List<Dictionary<string, object?>> EpcPfJobBuildBrandsSummary(IEnumerable<Dictionary<string, object?>> partLines)
    {
        var brands = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var line in partLines)
        {
            var brand = Str(line, "brand").Trim();
            if (brand == "")
            {
                continue;
            }

            var key = MbUpper(brand);
            if (!brands.TryGetValue(key, out var row))
            {
                row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["brand"] = brand,
                    ["parts_count"] = 0,
                    ["total_qty"] = 0.0,
                    ["product_groups"] = new List<object?>()
                };
                brands[key] = row;
            }

            row["parts_count"] = ToInt(row["parts_count"]) + 1;
            row["total_qty"] = ToDouble(row["total_qty"]) + ToDouble(line.TryGetValue("qty", out var q) ? q : 0);
            var groupLabel = Str(line, "product_group").Trim();
            var groups = (List<object?>)row["product_groups"]!;
            if (groupLabel != "" && !groups.Any(g => string.Equals(Convert.ToString(g), groupLabel, StringComparison.Ordinal)))
            {
                groups.Add(groupLabel);
            }
        }

        var list = brands.Values.ToList();
        list.Sort((a, b) => ToInt(b.TryGetValue("parts_count", out var bc) ? bc : 0).CompareTo(ToInt(a.TryGetValue("parts_count", out var ac) ? ac : 0)));
        return list;
    }

    public static List<Dictionary<string, object?>> EpcPfProductsForCards(IEnumerable<Dictionary<string, object?>> products, int brandPreviewLimit = 10)
    {
        var output = new List<Dictionary<string, object?>>();
        foreach (var product in products)
        {
            var brands = product.TryGetValue("brands", out var raw) && raw is List<Dictionary<string, object?>> list
                ? list
                : [];
            var slice = EpcPfTopBrandsSlice(brands, brandPreviewLimit);
            output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = Str(product, "label"),
                ["parts_count"] = ToInt(product.TryGetValue("parts_count", out var pc) ? pc : 0),
                ["total_qty"] = ToDouble(product.TryGetValue("total_qty", out var tq) ? tq : 0),
                ["samples"] = product.TryGetValue("samples", out var samples) && samples is List<object?> sampleList ? sampleList : new List<object?>(),
                ["brands_top"] = slice["top"],
                ["brands_more_count"] = ToInt(slice["more_count"]),
                ["brands_total"] = ToInt(slice["total"])
            });
        }

        return output;
    }

    public static Dictionary<string, object?>? EpcPfFindGroup(Dictionary<string, Dictionary<string, object?>> productGroups, string label)
    {
        var needle = MbLower((label ?? "").Trim());
        foreach (var (key, group) in productGroups)
        {
            var groupLabel = MbLower(Str(group, "label").Trim());
            if (groupLabel == needle || key == needle)
            {
                return group;
            }
        }

        return null;
    }

    public static Dictionary<string, object?> EpcPfGroupDetail(Dictionary<string, object?> group, string brandFilter = "")
    {
        var parts = group.TryGetValue("parts", out var raw) && raw is List<object?> list
            ? list.Select(item => item is Dictionary<string, object?> d ? d : new Dictionary<string, object?>(StringComparer.Ordinal)).ToList()
            : [];
        brandFilter = (brandFilter ?? "").Trim();
        if (brandFilter != "")
        {
            var bu = MbUpper(brandFilter);
            parts = parts.Where(p => MbUpper(Str(p, "brand").Trim()) == bu).ToList();
        }

        var brandsMap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            var brand = Str(part, "brand").Trim();
            if (brand == "")
            {
                continue;
            }

            var key = MbUpper(brand);
            if (!brandsMap.TryGetValue(key, out var row))
            {
                row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["brand"] = brand,
                    ["parts_count"] = 0,
                    ["total_qty"] = 0.0
                };
                brandsMap[key] = row;
            }

            row["parts_count"] = ToInt(row["parts_count"]) + 1;
            row["total_qty"] = ToDouble(row["total_qty"]) + ToDouble(part.TryGetValue("qty", out var q) ? q : 0);
        }

        var brands = brandsMap.Values.ToList();
        brands.Sort((a, b) =>
        {
            var qa = ToDouble(a.TryGetValue("total_qty", out var av) ? av : 0);
            var qb = ToDouble(b.TryGetValue("total_qty", out var bv) ? bv : 0);
            if (qa != qb)
            {
                return qb.CompareTo(qa);
            }

            return ToInt(b.TryGetValue("parts_count", out var bc) ? bc : 0).CompareTo(ToInt(a.TryGetValue("parts_count", out var ac) ? ac : 0));
        });
        parts.Sort((a, b) =>
        {
            var ba = MbUpper(Str(a, "brand"));
            var bb = MbUpper(Str(b, "brand"));
            var cmp = string.CompareOrdinal(ba, bb);
            return cmp != 0 ? cmp : string.CompareOrdinal(Str(a, "article"), Str(b, "article"));
        });

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["label"] = Str(group, "label"),
            ["parts_count"] = parts.Count,
            ["total_qty"] = parts.Sum(p => ToDouble(p.TryGetValue("qty", out var q) ? q : 0)),
            ["brands"] = brands,
            ["parts"] = parts
        };
    }

    private static string Norm(object? value) => NormalizeArticle is not null ? NormalizeArticle(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "") : (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");

    private static Dictionary<string, object?> Copy(Dictionary<string, object?> row)
        => new(row, StringComparer.Ordinal);

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";

    private static string FirstNonEmpty(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = Str(row, key);
            if (value != "")
            {
                return value;
            }
        }

        return "";
    }

    private static string MbUpper(string value) => value.ToUpperInvariant();

    private static string MbLower(string value) => value.ToLowerInvariant();

    private static int ToInt(object? value)
    {
        if (value is int i)
        {
            return i;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : (int)ToDouble(value);
    }

    private static double ToDouble(object? value)
    {
        if (value is double d)
        {
            return d;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is decimal m)
        {
            return (double)m;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
