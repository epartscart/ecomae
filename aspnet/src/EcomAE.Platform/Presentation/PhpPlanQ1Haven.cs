using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-haven Electronicae storefront helper. PHP identifiers kept for the inventory:
/// <c>epc_electronicae_storefront_active</c>, <c>epc_electronicae_site_key</c>,
/// <c>epc_electronicae_lang_prefix</c>, <c>epc_electronicae_href</c>,
/// <c>epc_electronicae_preferred_line_slugs</c>, <c>epc_electronicae_line_visual</c>,
/// <c>epc_electronicae_normalize_image_url</c>, <c>epc_electronicae_root_category_id</c>,
/// <c>epc_electronicae_category_url</c>, <c>epc_electronicae_line_product_image</c>,
/// <c>epc_electronicae_line_tile</c>, <c>epc_electronicae_line_product_count</c>,
/// <c>epc_electronicae_product_line_tiles</c>, <c>epc_electronicae_mega_nav</c>,
/// <c>epc_electronicae_all_lines_href</c>, <c>epc_electronicae_hero_slides</c>,
/// <c>epc_electronicae_product_card</c>, <c>epc_electronicae_home_product_sections</c>,
/// <c>epc_electronicae_filter_menu_tree</c>, <c>epc_electronicae_category_subtree_ids</c>,
/// <c>epc_electronicae_category_sql_in</c>, <c>epc_electronicae_category_has_products</c>,
/// <c>epc_electronicae_category_prefers_products</c>, <c>epc_electronicae_render_empty_category</c>.
/// Path: <c>content/general_pages/epc_electronicae_storefront.php</c>.
/// GET never mints a session cookie. Leftover portal and APE stay injected
/// and are not written as unique leftover basenames.
/// </summary>
public static class PhpPlanQ1Haven
{
    public const string ElectronicaeStorefrontPath = "content/general_pages/epc_electronicae_storefront.php";

    public static Func<bool>? IsElectronicsRetail { get; set; }
    public static Func<string>? ResolveStorefrontSiteKey { get; set; }
    public static Func<string>? LangPrefix { get; set; }
    public static Func<MySqlConnection, string, string>? ResolveIndustry { get; set; }
    public static Func<string, string, string>? CategorySlug { get; set; }
    public static Func<MySqlConnection, string, int, int>? CategoryForTaxonomy { get; set; }
    public static Func<MySqlConnection, string, List<Dictionary<string, object?>>>? TaxFlat { get; set; }
    public static Func<List<Dictionary<string, object?>>, Dictionary<int, List<int>>>? TaxDescendantMap { get; set; }
    public static Action<MySqlConnection, string>? SyncCategories { get; set; }
    public static Func<MySqlConnection, string, List<Dictionary<string, object?>>>? ProductLineRankings { get; set; }
    public static Func<Dictionary<string, object?>, string, string>? CatalogueProductPath { get; set; }
    public static Func<string, string>? ProductImageUrl { get; set; }
    public static Func<string, int, Func<object>, object>? CacheRemember { get; set; }
    public static string ProductUrlMode { get; set; } = "alias";
    public static string LastCacheKey { get; private set; } = "";
    public static int SyncCount { get; private set; }

    public static void Reset()
    {
        IsElectronicsRetail = null;
        ResolveStorefrontSiteKey = null;
        LangPrefix = null;
        ResolveIndustry = null;
        CategorySlug = null;
        CategoryForTaxonomy = null;
        TaxFlat = null;
        TaxDescendantMap = null;
        SyncCategories = null;
        ProductLineRankings = null;
        CatalogueProductPath = null;
        ProductImageUrl = null;
        CacheRemember = null;
        ProductUrlMode = "alias";
        LastCacheKey = "";
        SyncCount = 0;
    }

    public static bool EpcElectronicaeStorefrontActive()
        => IsElectronicsRetail?.Invoke() == true;

    public static string EpcElectronicaeSiteKey(MySqlConnection pdo)
    {
        var sk = ResolveStorefrontSiteKey?.Invoke() ?? "";
        return sk != "" ? sk : "electronicae";
    }

    public static string EpcElectronicaeLangPrefix()
        => LangPrefix?.Invoke() ?? "/en";

    public static string EpcElectronicaeHref(string path, string lang = "")
    {
        if (lang == "")
        {
            lang = EpcElectronicaeLangPrefix();
        }

        if (path != "" && path[0] == '/')
        {
            return lang.TrimEnd('/') + path;
        }

        return path;
    }

    public static List<string> EpcElectronicaePreferredLineSlugs()
        =>
        [
            "cell-phones", "computers-laptops", "tv-video", "gaming",
            "smart-home", "headphones", "wearables", "cameras"
        ];

    public static Dictionary<string, string> EpcElectronicaeLineVisual(string slug)
    {
        slug = slug.ToLowerInvariant();
        var map = new (string Needle, string Icon, string Accent, string Label)[]
        {
            ("cell-phones", "fa-mobile", "#1a73e8", "Cell Phones"),
            ("computers-laptops", "fa-laptop", "#5f6368", "Laptops"),
            ("laptops", "fa-laptop", "#5f6368", "Laptops"),
            ("tv-video", "fa-television", "#202124", "TV"),
            ("televisions", "fa-television", "#202124", "TV"),
            ("gaming", "fa-gamepad", "#e10a0a", "Gaming"),
            ("smart-home", "fa-home", "#34a853", "Smart Home"),
            ("headphones", "fa-headphones", "#9334e6", "Headphones"),
            ("wearables", "fa-heartbeat", "#ea4335", "Wearables"),
            ("cameras", "fa-camera", "#fbbc04", "Camera"),
            ("computers-tablets", "fa-tablet", "#4285f4", "Tablets"),
            ("audio", "fa-volume-up", "#9334e6", "Audio")
        };
        foreach (var row in map)
        {
            if (slug.Contains(row.Needle, StringComparison.Ordinal))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["icon"] = row.Icon,
                    ["accent"] = row.Accent,
                    ["label"] = row.Label
                };
            }
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["icon"] = "fa-microchip",
            ["accent"] = "#e10a0a",
            ["label"] = ""
        };
    }

    public static string EpcElectronicaeNormalizeImageUrl(string path)
    {
        path = path.Trim();
        if (path == "")
        {
            return "";
        }

        if (Regex.IsMatch(path, @"^https?://", RegexOptions.IgnoreCase))
        {
            return path;
        }

        return ProductImageUrl?.Invoke(path) ?? path;
    }

    public static int EpcElectronicaeRootCategoryId(MySqlConnection pdo, string siteKey, string industryKey = "")
    {
        if (industryKey == "")
        {
            industryKey = ResolveIndustry?.Invoke(pdo, siteKey) ?? "electronics";
        }

        var rootAlias = CategorySlug?.Invoke(industryKey, "apai-root") ?? ("apai-" + industryKey + "-root");
        var row = QueryOne(pdo, "SELECT `id`, `url` FROM `shop_catalogue_categories` WHERE `alias` = @a AND `parent` = 0 LIMIT 1", ("@a", rootAlias));
        return row is null ? 0 : ToInt(row.GetValueOrDefault("id"));
    }

    public static string EpcElectronicaeCategoryUrl(MySqlConnection pdo, string siteKey, int taxonomyNodeId)
    {
        var catId = CategoryForTaxonomy?.Invoke(pdo, siteKey, taxonomyNodeId) ?? 0;
        if (catId <= 0)
        {
            return "";
        }

        var url = Scalar(pdo, "SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = @id LIMIT 1", ("@id", catId));
        return Str(url).Trim('/');
    }

    public static string EpcElectronicaeLineProductImage(MySqlConnection pdo, string siteKey, int taxonomyNodeId)
    {
        var ids = DescendantIds(pdo, siteKey, taxonomyNodeId);
        if (ids.Count == 0)
        {
            return "";
        }

        var sql = "SELECT q.`product_id` FROM `epc_product_discovery_queue` q WHERE q.`site_key` = @sk AND q.`status` = 'imported' AND q.`product_id` > 0 AND q.`taxonomy_node_id` IN (" +
                  string.Join(",", ids) + ") ORDER BY q.`updated_at` DESC LIMIT 1";
        var productId = ToInt(Scalar(pdo, sql, ("@sk", siteKey)));
        if (productId <= 0)
        {
            return "";
        }

        var file = Str(Scalar(pdo, "SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = @id ORDER BY `id` ASC LIMIT 1", ("@id", productId)));
        return file != "" ? EpcElectronicaeNormalizeImageUrl(file) : "";
    }

    public static Dictionary<string, object?> EpcElectronicaeLineTile(MySqlConnection pdo, string siteKey, Dictionary<string, object?> line)
    {
        var nodeId = ToInt(line.GetValueOrDefault("id"));
        var slug = Str(line.GetValueOrDefault("slug"));
        var name = Str(line.GetValueOrDefault("name_en"));
        var visual = EpcElectronicaeLineVisual(slug);
        if (name == "" && visual["label"] != "")
        {
            name = visual["label"];
        }

        var url = EpcElectronicaeCategoryUrl(pdo, siteKey, nodeId);
        var href = url != "" ? "/" + url : "/shop/search?q=" + Uri.EscapeDataString(name).Replace("+", "%20", StringComparison.Ordinal);
        var image = "";
        if (!PhpEmpty(line.GetValueOrDefault("preview_image")))
        {
            image = EpcElectronicaeNormalizeImageUrl(Str(line.GetValueOrDefault("preview_image")));
        }

        if (image == "")
        {
            image = EpcElectronicaeLineProductImage(pdo, siteKey, nodeId);
        }

        var productCount = EpcElectronicaeLineProductCount(pdo, siteKey, nodeId);
        var imported = ToInt(line.GetValueOrDefault("imported_count"));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = nodeId,
            ["name"] = name,
            ["slug"] = slug,
            ["href"] = href,
            ["image"] = image,
            ["icon"] = visual["icon"],
            ["accent"] = visual["accent"],
            ["product_count"] = Math.Max(productCount, imported),
            ["trend"] = Str(line.GetValueOrDefault("trend")),
            ["score"] = ToInt(line.GetValueOrDefault("score"))
        };
    }

    public static int EpcElectronicaeLineProductCount(MySqlConnection pdo, string siteKey, int taxonomyNodeId)
    {
        var ids = DescendantIds(pdo, siteKey, taxonomyNodeId);
        if (ids.Count == 0)
        {
            return 0;
        }

        var sql = "SELECT COUNT(DISTINCT q.`product_id`) FROM `epc_product_discovery_queue` q INNER JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id` AND scp.`published_flag` = 1 WHERE q.`site_key` = @sk AND q.`status` = 'imported' AND q.`taxonomy_node_id` IN (" +
                  string.Join(",", ids) + ")";
        return ToInt(Scalar(pdo, sql, ("@sk", siteKey)));
    }

    public static List<Dictionary<string, object?>> EpcElectronicaeProductLineTiles(MySqlConnection pdo, string siteKey = "", int limit = 12)
    {
        if (siteKey == "")
        {
            siteKey = EpcElectronicaeSiteKey(pdo);
        }

        var cacheKey = "epc_pl_tiles:v1:" + siteKey + ":" + limit;
        LastCacheKey = cacheKey;
        object Compute()
        {
            SyncCategories?.Invoke(pdo, siteKey);
            SyncCount++;
            var rankings = ProductLineRankings?.Invoke(pdo, siteKey) ?? [];
            var bySlug = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            foreach (var line in rankings)
            {
                if (ToInt(line.GetValueOrDefault("level")) != 1)
                {
                    continue;
                }

                bySlug[Str(line.GetValueOrDefault("slug"))] = line;
            }

            var chosen = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            foreach (var pref in EpcElectronicaePreferredLineSlugs())
            {
                foreach (var (slug, line) in bySlug)
                {
                    if (slug.StartsWith(pref, StringComparison.Ordinal) || slug == pref)
                    {
                        chosen[slug] = line;
                        break;
                    }
                }
            }

            foreach (var line in rankings)
            {
                if (ToInt(line.GetValueOrDefault("level")) != 1)
                {
                    continue;
                }

                var slug = Str(line.GetValueOrDefault("slug"));
                if (!chosen.ContainsKey(slug) && chosen.Count < limit)
                {
                    chosen[slug] = line;
                }
            }

            return chosen.Values.Take(limit).Select(line => EpcElectronicaeLineTile(pdo, siteKey, line)).ToList();
        }

        if (CacheRemember is not null)
        {
            return (List<Dictionary<string, object?>>)CacheRemember(cacheKey, 900, Compute);
        }

        return (List<Dictionary<string, object?>>)Compute();
    }

    public static List<Dictionary<string, object?>> EpcElectronicaeMegaNav(MySqlConnection pdo, string siteKey = "")
    {
        var tiles = EpcElectronicaeProductLineTiles(pdo, siteKey, 12);
        var nav = tiles.Select(tile => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["label"] = Str(tile.GetValueOrDefault("name")),
            ["href"] = Str(tile.GetValueOrDefault("href"), "/"),
            ["count"] = ToInt(tile.GetValueOrDefault("product_count"))
        }).ToList();
        if (nav.Count > 0)
        {
            nav.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "All product lines",
                ["href"] = EpcElectronicaeAllLinesHref(pdo, siteKey),
                ["highlight"] = true
            });
        }

        return nav;
    }

    public static string EpcElectronicaeAllLinesHref(MySqlConnection pdo, string siteKey = "")
    {
        if (siteKey == "")
        {
            siteKey = EpcElectronicaeSiteKey(pdo);
        }

        var industryKey = ResolveIndustry?.Invoke(pdo, siteKey) ?? "electronics";
        var rootAlias = CategorySlug?.Invoke(industryKey, "apai-root") ?? ("apai-" + industryKey + "-root");
        var url = Str(Scalar(pdo, "SELECT `url` FROM `shop_catalogue_categories` WHERE `alias` = @a LIMIT 1", ("@a", rootAlias))).Trim('/');
        return url != "" ? "/" + url : "/shop/catalogue";
    }

    public static List<Dictionary<string, object?>> EpcElectronicaeHeroSlides(MySqlConnection pdo, string siteKey = "")
    {
        var tiles = EpcElectronicaeProductLineTiles(pdo, siteKey, 4).Take(4).ToList();
        var tones = new[] { "dark", "light", "dark", "light" };
        var slides = new List<Dictionary<string, object?>>();
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            var name = Str(tile.GetValueOrDefault("name"));
            if (name == "")
            {
                continue;
            }

            slides.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = name,
                ["sub"] = ToInt(tile.GetValueOrDefault("product_count")) > 0
                    ? "Shop verified UAE listings — official warranty, prices in AED"
                    : "New arrivals coming soon — browse the full range",
                ["cta"] = "Shop " + name,
                ["href"] = Str(tile.GetValueOrDefault("href"), "/"),
                ["image"] = Str(tile.GetValueOrDefault("image")),
                ["icon"] = Str(tile.GetValueOrDefault("icon"), "fa-microchip"),
                ["accent"] = Str(tile.GetValueOrDefault("accent"), "#e10a0a"),
                ["alt"] = name,
                ["tone"] = tones[i % tones.Length]
            });
        }

        return slides;
    }

    public static Dictionary<string, object?> EpcElectronicaeProductCard(MySqlConnection pdo, Dictionary<string, object?> row, string productUrlMode = "alias")
    {
        var pid = ToInt(row.ContainsKey("product_id") ? row["product_id"] : row.GetValueOrDefault("id"));
        var href = CatalogueProductPath?.Invoke(row, productUrlMode) ?? "";
        if (href == "")
        {
            var catUrl = Str(row.GetValueOrDefault("category_url")).Trim('/');
            var slug = productUrlMode == "id" ? pid.ToString(CultureInfo.InvariantCulture) : Str(row.GetValueOrDefault("alias"));
            href = catUrl != "" && slug != "" ? "/" + catUrl + "/" + slug : "/shop/products/product?id=" + pid;
        }

        var image = "";
        if (!PhpEmpty(row.GetValueOrDefault("file_name")))
        {
            image = EpcElectronicaeNormalizeImageUrl(Str(row.GetValueOrDefault("file_name")));
        }

        var price = ToFloat(row.GetValueOrDefault("price"));
        var brand = Str(row.GetValueOrDefault("manufacturer"));
        var displayName = Str(row.ContainsKey("caption") && !PhpEmpty(row["caption"]) ? row["caption"] : row.GetValueOrDefault("title"));
        if (brand == "" && displayName != "")
        {
            var parts = Regex.Split(displayName.Trim(), @"\s+");
            brand = (parts.Length > 0 ? parts[0] : "").ToUpperInvariant();
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = pid,
            ["brand"] = brand,
            ["name"] = displayName,
            ["price"] = price,
            ["was"] = 0,
            ["image"] = image,
            ["href"] = href,
            ["alt"] = displayName,
            ["sku"] = ""
        };
    }

    public static List<Dictionary<string, object?>> EpcElectronicaeHomeProductSections(
        MySqlConnection pdo,
        string siteKey = "",
        int sectionLimit = 3,
        int productsPerSection = 6)
    {
        if (siteKey == "")
        {
            siteKey = EpcElectronicaeSiteKey(pdo);
        }

        var tiles = EpcElectronicaeProductLineTiles(pdo, siteKey, sectionLimit + 2);
        var sections = new List<Dictionary<string, object?>>();
        foreach (var tile in tiles)
        {
            if (sections.Count >= sectionLimit)
            {
                break;
            }

            var nodeId = ToInt(tile.GetValueOrDefault("id"));
            if (nodeId <= 0)
            {
                continue;
            }

            var ids = DescendantIds(pdo, siteKey, nodeId);
            if (ids.Count == 0)
            {
                continue;
            }

            var sql = "SELECT scp.`id`, scp.`alias`, scp.`caption`, scc.`url` AS `category_url`, q.`title`, " +
                      "(SELECT MIN(sd.`price`) FROM `shop_storages_data` sd WHERE sd.`product_id` = scp.`id` AND sd.`price` > 0) AS `price`, " +
                      "(SELECT spi.`file_name` FROM `shop_products_images` spi WHERE spi.`product_id` = scp.`id` ORDER BY spi.`id` ASC LIMIT 1) AS `file_name` " +
                      "FROM `epc_product_discovery_queue` q INNER JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id` AND scp.`published_flag` = 1 " +
                      "LEFT JOIN `shop_catalogue_categories` scc ON scc.`id` = scp.`category_id` " +
                      "WHERE q.`site_key` = @sk AND q.`status` = 'imported' AND q.`taxonomy_node_id` IN (" + string.Join(",", ids) + ") " +
                      "ORDER BY q.`updated_at` DESC LIMIT " + productsPerSection;
            var products = new List<Dictionary<string, object?>>();
            foreach (var row in Query(pdo, sql, ("@sk", siteKey)))
            {
                var card = EpcElectronicaeProductCard(pdo, row, ProductUrlMode);
                if (Str(card.GetValueOrDefault("image")) != "")
                {
                    products.Add(card);
                }
            }

            if (products.Count == 0)
            {
                continue;
            }

            sections.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = Str(tile.GetValueOrDefault("name"), "Featured"),
                ["href"] = Str(tile.GetValueOrDefault("href"), "/"),
                ["products"] = products
            });
        }

        return sections;
    }

    public static List<Dictionary<string, object?>> EpcElectronicaeFilterMenuTree(MySqlConnection? pdo, List<Dictionary<string, object?>> tree, string siteKey = "")
    {
        if (siteKey == "")
        {
            siteKey = pdo is null ? "electronicae" : EpcElectronicaeSiteKey(pdo);
        }

        var industryKey = pdo is null ? "electronics" : ResolveIndustry?.Invoke(pdo, siteKey) ?? "electronics";
        var rootAlias = CategorySlug?.Invoke(industryKey, "apai-root") ?? ("apai-" + industryKey + "-root");
        var extracted = Extract(tree, rootAlias);
        if (extracted.Count > 0)
        {
            return extracted;
        }

        var apai = tree.Where(node => Str(node.GetValueOrDefault("alias")).StartsWith("apai-", StringComparison.Ordinal)).ToList();
        return apai.Count > 0 ? apai : tree;
    }

    public static List<int> EpcElectronicaeCategorySubtreeIds(MySqlConnection pdo, int categoryId)
    {
        var ids = new List<int> { categoryId };
        Walk(categoryId);
        return ids.Where(id => id != 0).Distinct().ToList();

        void Walk(int parentId)
        {
            foreach (var row in Query(pdo, "SELECT `id` FROM `shop_catalogue_categories` WHERE `parent` = @p AND `published_flag` = 1", ("@p", parentId)))
            {
                var cid = ToInt(row.GetValueOrDefault("id"));
                if (cid == 0)
                {
                    continue;
                }

                ids.Add(cid);
                Walk(cid);
            }
        }
    }

    public static string EpcElectronicaeCategorySqlIn(MySqlConnection pdo, int categoryId)
    {
        var ids = EpcElectronicaeCategorySubtreeIds(pdo, categoryId);
        if (ids.Count == 0)
        {
            return categoryId.ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(",", ids);
    }

    public static bool EpcElectronicaeCategoryHasProducts(MySqlConnection pdo, int categoryId)
    {
        if (categoryId <= 0)
        {
            return false;
        }

        if (ToInt(Scalar(pdo, "SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `category_id` = @id AND `published_flag` = 1 LIMIT 1", ("@id", categoryId))) > 0)
        {
            return true;
        }

        return Walk(categoryId);

        bool Walk(int parentId)
        {
            foreach (var row in Query(pdo, "SELECT `id` FROM `shop_catalogue_categories` WHERE `parent` = @p AND `published_flag` = 1", ("@p", parentId)))
            {
                var cid = ToInt(row.GetValueOrDefault("id"));
                if (ToInt(Scalar(pdo, "SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `category_id` = @id AND `published_flag` = 1", ("@id", cid))) > 0)
                {
                    return true;
                }

                if (Walk(cid))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static bool EpcElectronicaeCategoryPrefersProducts(MySqlConnection pdo, int categoryId, int childCount)
        => childCount <= 0 || EpcElectronicaeCategoryHasProducts(pdo, categoryId);

    public static string EpcElectronicaeRenderEmptyCategory()
        => "<div class=\"epc-el-empty-category\"><div class=\"epc-el-empty-category__inner\">"
           + "<i class=\"fa fa-clock-o\" aria-hidden=\"true\"></i>"
           + "<h3>Coming soon</h3>"
           + "<p>New products for this category are being added. Check back shortly or browse our featured product lines.</p>"
           + "<a class=\"epc-er-btn epc-er-btn--primary\" href=\"" + H(EpcElectronicaeHref("/")) + "\">Back to shop</a>"
           + "</div></div>";

    private static List<Dictionary<string, object?>> Extract(List<Dictionary<string, object?>> nodes, string rootAlias)
    {
        foreach (var node in nodes)
        {
            if (Str(node.GetValueOrDefault("alias")) == rootAlias)
            {
                var children = node.GetValueOrDefault("data") is List<Dictionary<string, object?>> kids ? kids : [];
                return children.Count > 0 ? children : [node];
            }

            if (node.GetValueOrDefault("data") is List<Dictionary<string, object?>> sub && sub.Count > 0)
            {
                var found = Extract(sub, rootAlias);
                if (found.Count > 0)
                {
                    return found;
                }
            }
        }

        return [];
    }

    private static List<int> DescendantIds(MySqlConnection pdo, string siteKey, int taxonomyNodeId)
    {
        var industry = ResolveIndustry?.Invoke(pdo, siteKey) ?? "electronics";
        var flat = TaxFlat?.Invoke(pdo, industry) ?? [];
        var map = TaxDescendantMap?.Invoke(flat) ?? new Dictionary<int, List<int>>();
        if (map.TryGetValue(taxonomyNodeId, out var ids))
        {
            return ids;
        }

        return [taxonomyNodeId];
    }

    private static string H(string value)
        => WebUtility.HtmlEncode(value).Replace("&#39;", "&#039;", StringComparison.Ordinal).Replace("'", "&#039;", StringComparison.Ordinal);

    private static List<Dictionary<string, object?>> Query(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        using var reader = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : Box(reader.GetValue(i));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static Dictionary<string, object?>? QueryOne(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
        => Query(pdo, sql, args).FirstOrDefault();

    private static object? Scalar(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var scalar = cmd.ExecuteScalar();
        return scalar is null or DBNull ? null : Box(scalar);
    }

    private static object? Box(object value)
        => value switch
        {
            decimal d => d == decimal.Truncate(d) ? (double)d : (double)d,
            bool b => b ? 1 : 0,
            sbyte sb => (int)sb,
            byte b => (int)b,
            short s => (int)s,
            ushort us => (int)us,
            uint ui => (int)ui,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            ulong ul when ul <= int.MaxValue => (int)ul,
            _ => value
        };

    private static bool PhpEmpty(object? value)
    {
        if (value is null or DBNull or false)
        {
            return true;
        }

        return value switch
        {
            string s => s is "" or "0",
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            decimal m => m == 0,
            _ => false
        };
    }

    private static int ToInt(object? value)
    {
        if (value is null or DBNull or false)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static double ToFloat(object? value)
    {
        if (value is null or DBNull)
        {
            return 0;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static string Str(object? value, string fallback = "")
        => value is null or DBNull ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
