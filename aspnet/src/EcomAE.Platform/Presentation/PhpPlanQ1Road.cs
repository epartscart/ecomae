using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-road warehouse spare-parts search. PHP identifiers kept for the inventory:
/// <c>epc_spare_parts_oem_brands</c>, <c>epc_spare_parts_catalogue_product_url</c>,
/// <c>epc_spare_parts_catalogue_sell_price</c>, <c>epc_spare_parts_warehouse_search</c>.
/// Path: <c>content/shop/epc_spare_parts_warehouse.php</c>.
/// GET never mints a session cookie. Leftover APE / demand-intelligence stay injected
/// and are not written as unique leftover basenames.
/// </summary>
public static class PhpPlanQ1Road
{
    public const string SparePartsWarehousePath = "content/shop/epc_spare_parts_warehouse.php";

    public static readonly string[] OemBrandLabels =
    [
        "Toyota", "Lexus", "Nissan", "Infiniti", "Honda", "Acura", "BMW", "Mercedes-Benz",
        "Ford", "Hyundai", "Kia", "Mitsubishi", "Land Rover", "Chevrolet", "GMC",
        "Bosch", "Denso", "NGK", "JS ASAKASHI", "555", "Hino", "Isuzu", "Volkswagen", "Audi"
    ];

    public static Func<List<string>>? QueryBrands { get; set; }
    public static Func<List<Dictionary<string, object?>>>? TaxSeedTree { get; set; }
    public static Func<int, Dictionary<string, object?>?>? QueryProduct { get; set; }
    public static Func<int, double>? QueryMinPrice { get; set; }
    public static Func<string, List<Dictionary<string, object?>>>? QueryPriceRows { get; set; }
    public static Func<bool>? QueryFailed { get; set; }
    public static Func<string, int>? FindCatalogue { get; set; }
    public static Func<object?, string, string, string>? DemandPartUrl { get; set; }
    public static Func<string>? LangPrefix { get; set; }
    public static string ProductUrlMode { get; set; } = "alias";
    public static object? Config { get; set; }

    private static List<Dictionary<string, object?>>? _brandCache;

    public static void Reset()
    {
        QueryBrands = null;
        TaxSeedTree = null;
        QueryProduct = null;
        QueryMinPrice = null;
        QueryPriceRows = null;
        QueryFailed = null;
        FindCatalogue = null;
        DemandPartUrl = null;
        LangPrefix = null;
        ProductUrlMode = "alias";
        Config = null;
        _brandCache = null;
    }

    public static List<Dictionary<string, object?>> EpcSparePartsOemBrands()
    {
        if (_brandCache is not null)
        {
            return _brandCache;
        }

        var labels = new List<string>(OemBrandLabels);
        if (TaxSeedTree is not null)
        {
            foreach (var node in TaxSeedTree())
            {
                if (Str(node.GetValueOrDefault("slug")) != "auto-oem-brands")
                {
                    continue;
                }

                if (node.GetValueOrDefault("children") is not IEnumerable<object?> children)
                {
                    continue;
                }

                foreach (var childObj in children)
                {
                    var child = AsDict(childObj);
                    var name = Str(child?.GetValueOrDefault("name")).Trim();
                    if (name == "")
                    {
                        continue;
                    }

                    labels.Add(Regex.Replace(name, @"\s*&.*$", "", RegexOptions.CultureInvariant));
                }
            }
        }

        try
        {
            foreach (var brand in QueryBrands?.Invoke() ?? [])
            {
                labels.Add(brand.Trim());
            }
        }
        catch
        {
            // PHP swallows the manufacturer query.
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var output = new List<Dictionary<string, object?>>();
        foreach (var raw in labels)
        {
            var label = raw.Trim();
            if (label == "")
            {
                continue;
            }

            var key = label.ToUpperInvariant();
            if (!seen.Add(key))
            {
                continue;
            }

            output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = label,
                ["label"] = label
            });
        }

        output.Sort(static (a, b) => string.Compare(Str(a["label"]), Str(b["label"]), StringComparison.OrdinalIgnoreCase));
        _brandCache = output;
        return output;
    }

    public static string EpcSparePartsCatalogueProductUrl(int productId, object? config = null)
    {
        if (productId <= 0)
        {
            return "";
        }

        var mode = ProductUrlModeOf(config);
        Dictionary<string, object?>? row;
        try
        {
            row = QueryProduct?.Invoke(productId);
        }
        catch
        {
            return "";
        }

        if (row is null)
        {
            return "";
        }

        var path = CatalogueProductPath(row, mode);
        if (path == "")
        {
            return "";
        }

        return Lang().TrimEnd('/') + path;
    }

    public static double EpcSparePartsCatalogueSellPrice(int productId)
    {
        if (productId <= 0)
        {
            return 0;
        }

        try
        {
            return QueryMinPrice?.Invoke(productId) ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public static Dictionary<string, object?> EpcSparePartsWarehouseSearch(string brand, string article, object? config = null)
    {
        brand = brand.Trim();
        article = article.Trim();
        var articleNorm = NormalizeArticle(article);
        var brandNorm = NormalizeBrand(brand);
        var baKey = BrandArticleKey(brand, article);

        if (articleNorm == "" || articleNorm.Length < 2)
        {
            return Fail("Enter a valid part number (at least 2 characters).");
        }

        if (brand == "")
        {
            return Fail("Select a brand.");
        }

        var rows = new List<Dictionary<string, object?>>();
        try
        {
            if (QueryFailed?.Invoke() == true)
            {
                return Fail("Warehouse lookup failed.");
            }

            var brandUpper = brand.ToUpperInvariant();
            foreach (var row in QueryPriceRows?.Invoke(articleNorm) ?? [])
            {
                var rowBrand = Str(row.GetValueOrDefault("manufacturer")).Trim();
                var rowBrandNorm = NormalizeBrand(rowBrand);
                if (brandNorm != "" && rowBrandNorm != "" && rowBrandNorm != brandNorm
                    && rowBrand.ToUpperInvariant() != brandUpper)
                {
                    continue;
                }

                var articleShow = Str(row.GetValueOrDefault("article_show"));
                rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["brand"] = rowBrand != "" ? rowBrand : brand,
                    ["article"] = articleShow != "" ? articleShow : FirstNonEmpty(Str(row.GetValueOrDefault("article")), article),
                    ["warehouse_cost"] = ToDouble(row.GetValueOrDefault("price")),
                    ["qty"] = ToDouble(row.GetValueOrDefault("exist")),
                    ["warehouse"] = Str(row.GetValueOrDefault("storage")),
                    ["price_list"] = Str(row.GetValueOrDefault("price_list_name")),
                    ["price_id"] = ToInt(row.GetValueOrDefault("price_id"))
                });
            }
        }
        catch
        {
            return Fail("Warehouse lookup failed.");
        }

        var productId = FindCatalogue?.Invoke(baKey) ?? 0;
        var sellPrice = EpcSparePartsCatalogueSellPrice(productId);
        var productUrl = EpcSparePartsCatalogueProductUrl(productId, config);
        var partsUrl = "";
        if (DemandPartUrl is not null && config is not null)
        {
            partsUrl = DemandPartUrl(config, brand, article);
        }

        var totalQty = 0d;
        var bestCost = 0d;
        var displayBrand = brand;
        var displayArticle = article;
        foreach (var row in rows)
        {
            totalQty += ToDouble(row.GetValueOrDefault("qty"));
            if (bestCost <= 0 && ToDouble(row.GetValueOrDefault("warehouse_cost")) > 0)
            {
                bestCost = ToDouble(row.GetValueOrDefault("warehouse_cost"));
            }

            if (displayBrand == "" && Str(row.GetValueOrDefault("brand")) != "")
            {
                displayBrand = Str(row.GetValueOrDefault("brand"));
            }

            if (displayArticle == "" && Str(row.GetValueOrDefault("article")) != "")
            {
                displayArticle = Str(row.GetValueOrDefault("article"));
            }
        }

        if (sellPrice <= 0 && bestCost > 0)
        {
            sellPrice = bestCost;
        }

        var inWarehouse = totalQty > 0 || rows.Count > 0;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["brand"] = displayBrand,
            ["article"] = displayArticle,
            ["brand_article_key"] = baKey,
            ["in_warehouse"] = inWarehouse,
            ["qty"] = totalQty,
            ["warehouse_cost"] = bestCost,
            ["sell_price"] = sellPrice,
            ["currency"] = "AED",
            ["warehouse_rows"] = rows,
            ["product_id"] = productId,
            ["product_url"] = productUrl,
            ["parts_url"] = partsUrl,
            ["redirect_url"] = productUrl,
            ["spare_parts_url"] = Lang().TrimEnd('/') + "/spare-parts/" + Uri.EscapeDataString(displayBrand) + "/" + Uri.EscapeDataString(displayArticle),
            ["message"] = inWarehouse ? "" : "Not in stock — contact us for availability."
        };
    }

    public static string NormalizeBrand(string value)
    {
        var collapsed = Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
        var slug = Regex.Replace(collapsed.Replace(' ', '-').Replace('_', '-'), @"[^a-z0-9\-]", "");
        return slug switch
        {
            "mercedes-benz" or "mercedesbenz" => "mercedes",
            "vw" => "volkswagen",
            "lexus-toyota" => "toyota",
            "gm" => "chevrolet",
            _ => slug
        };
    }

    public static string NormalizeArticle(string value)
        => Regex.Replace(value.Trim().ToUpperInvariant(), @"[\s\-\.]", "");

    public static string BrandArticleKey(string brand, string article)
    {
        brand = NormalizeBrand(brand);
        article = NormalizeArticle(article);
        return brand == "" || article == "" ? "" : brand + ":" + article;
    }

    public static string CatalogueProductPath(Dictionary<string, object?> productRow, string productUrlMode)
    {
        var catUrl = Str(productRow.GetValueOrDefault("category_url")).Trim('/');
        if (catUrl == "")
        {
            return "";
        }

        if (productUrlMode == "id")
        {
            var pid = ToInt(productRow.GetValueOrDefault("id"));
            return pid > 0 ? "/" + catUrl + "/" + pid.ToString(CultureInfo.InvariantCulture) : "";
        }

        var alias = Str(productRow.GetValueOrDefault("alias")).Trim('/');
        return alias == "" ? "" : "/" + catUrl + "/" + alias;
    }

    private static Dictionary<string, object?> Fail(string message)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["message"] = message };

    private static string Lang()
        => LangPrefix?.Invoke() ?? "/en";

    private static string ProductUrlModeOf(object? config)
    {
        if (config is Dictionary<string, object?> dict && dict.TryGetValue("product_url", out var mode) && Str(mode) != "")
        {
            return Str(mode);
        }

        return ProductUrlMode;
    }

    private static string FirstNonEmpty(string left, string right)
        => left != "" ? left : right;

    private static Dictionary<string, object?>? AsDict(object? value)
        => value as Dictionary<string, object?>;

    private static string Str(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static int ToInt(object? value)
        => int.TryParse(Str(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static double ToDouble(object? value)
        => double.TryParse(Str(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
