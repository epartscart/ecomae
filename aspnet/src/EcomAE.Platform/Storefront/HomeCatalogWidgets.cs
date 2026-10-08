using System.Data.Common;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Homepage widgets still request the PHP URLs
/// <c>/api/umapi_proxy.php</c> and <c>/content/shop/docpart/ajax_epc_product_family.php</c>.
/// These routes answer with the same JSON PHP returns (catalog rows, or the PHP error object).
/// </summary>
public static class HomeCatalogWidgets
{
    public const string ProductFamilyPath = "/content/shop/docpart/ajax_epc_product_family.php";
    public const string UmapiProxyPath = "/api/umapi_proxy.php";

    /// <summary>PHP <c>epc_config_key</c> fallback when config.php has no key.</summary>
    public const string DefaultUmapiKey = "2da16082-e7bc-4bd9-bee2-62b38c79ad8b";

    /// <summary>PHP <c>epc_config_key</c>: config.php <c>umapi_api_key</c>, else the last segment of <c>umapi_api_url</c>, else the built-in key.</summary>
    public static string ResolveUmapiKey(IReadOnlyDictionary<string, string>? config)
    {
        var key = string.Empty;
        if (config is not null)
        {
            if (config.TryGetValue("umapi_api_key", out var configured) && !string.IsNullOrWhiteSpace(configured))
            {
                key = configured;
            }
            else if (config.TryGetValue("umapi_api_url", out var url) && !string.IsNullOrWhiteSpace(url))
            {
                key = url;
            }
        }

        key = key.Trim().TrimEnd('/');
        var slash = key.LastIndexOf('/');
        if (slash >= 0)
        {
            key = key[(slash + 1)..];
        }

        key = key.Trim();
        return key.Length == 0 ? DefaultUmapiKey : key;
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Copied from PHP <c>epc_ensure_cache_tables</c>.</summary>
    private static readonly string[] UmapiCacheDdl =
    [
        """
        CREATE TABLE IF NOT EXISTS `epc_umapi_cache` (
            `cache_key` varchar(190) NOT NULL,
            `action` varchar(40) NOT NULL,
            `section` varchar(20) NOT NULL,
            `language` varchar(10) NOT NULL,
            `region` varchar(20) NOT NULL,
            `request_json` text NULL,
            `response_json` mediumtext NOT NULL,
            `rows_count` int NOT NULL DEFAULT 0,
            `http_status` int NOT NULL DEFAULT 200,
            `last_sync` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`cache_key`),
            KEY `action_section` (`action`, `section`),
            KEY `last_sync` (`last_sync`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_umapi_manufacturers` (
            `section` varchar(20) NOT NULL,
            `mfa_id` int NOT NULL,
            `manufacturer` varchar(255) NOT NULL,
            `manufacturer_ru` varchar(255) NULL,
            `type` varchar(255) NULL,
            `country` varchar(120) NULL,
            `popular` tinyint NOT NULL DEFAULT 0,
            `is_logo` tinyint NOT NULL DEFAULT 0,
            `raw_json` text NULL,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`section`, `mfa_id`),
            KEY `manufacturer` (`manufacturer`),
            KEY `popular` (`popular`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_umapi_models` (
            `section` varchar(20) NOT NULL,
            `mfa_id` int NOT NULL,
            `ms_id` int NOT NULL,
            `model_series` varchar(255) NOT NULL,
            `year_from` varchar(20) NULL,
            `year_to` varchar(20) NULL,
            `raw_json` text NULL,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`section`, `ms_id`),
            KEY `mfa_id` (`mfa_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_umapi_modifications` (
            `section` varchar(20) NOT NULL,
            `ms_id` int NOT NULL,
            `modification_id` int NOT NULL,
            `title` varchar(255) NOT NULL,
            `year_from` varchar(20) NULL,
            `year_to` varchar(20) NULL,
            `power_kw` varchar(50) NULL,
            `capacity_lt` varchar(50) NULL,
            `fuel_type` varchar(120) NULL,
            `raw_json` text NULL,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`section`, `modification_id`),
            KEY `ms_id` (`ms_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_umapi_brands` (
            `sup_id` int NOT NULL,
            `brand` varchar(255) NOT NULL,
            `full_name` varchar(255) NULL,
            `raw_json` text NULL,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`sup_id`),
            KEY `brand` (`brand`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_umapi_sync_status` (
            `id` tinyint NOT NULL,
            `connected` tinyint NOT NULL DEFAULT 0,
            `status_code` int NOT NULL DEFAULT 0,
            `message` varchar(255) NULL,
            `last_checked` int NOT NULL DEFAULT 0,
            `last_success` int NOT NULL DEFAULT 0,
            `last_error` int NOT NULL DEFAULT 0,
            `key_hash` varchar(64) NULL,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_umapi_vin_cache` (
            `vin` varchar(17) NOT NULL,
            `language` varchar(10) NOT NULL DEFAULT 'en',
            `region` varchar(20) NOT NULL DEFAULT 'WWW',
            `response_json` mediumtext NOT NULL,
            `vehicle_count` int NOT NULL DEFAULT 0,
            `manufacturer` varchar(255) NULL,
            `model_label` varchar(255) NULL,
            `http_status` int NOT NULL DEFAULT 200,
            `updated_at` int NOT NULL DEFAULT 0,
            PRIMARY KEY (`vin`, `language`, `region`),
            KEY `updated_at` (`updated_at`),
            KEY `vehicle_count` (`vehicle_count`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_umapi_usage_log` (
            `id` bigint NOT NULL AUTO_INCREMENT,
            `usage_date` date NOT NULL,
            `created_at` int NOT NULL DEFAULT 0,
            `action` varchar(40) NOT NULL,
            `section` varchar(20) NOT NULL DEFAULT '',
            `source` varchar(40) NOT NULL DEFAULT 'unknown',
            `request_path` varchar(255) NOT NULL DEFAULT '',
            `http_status` int NOT NULL DEFAULT 0,
            `from_cache` tinyint NOT NULL DEFAULT 0,
            `quota_blocked` tinyint NOT NULL DEFAULT 0,
            `is_live` tinyint NOT NULL DEFAULT 0,
            `message` varchar(255) NULL,
            `ip` varchar(45) NULL,
            PRIMARY KEY (`id`),
            KEY `usage_date` (`usage_date`),
            KEY `created_at` (`created_at`),
            KEY `action_date` (`action`, `usage_date`),
            KEY `source_date` (`source`, `usage_date`),
            KEY `live_date` (`is_live`, `usage_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
    ];

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(ProductFamilyPath, ["GET", "POST"], ProductFamilyAsync);
        endpoints.MapMethods(UmapiProxyPath, ["GET", "POST"], UmapiProxyAsync);
        UmapiImageProxy.Map(endpoints);
    }

    public static string UnavailableJson()
        => """{"status":false,"message":"Database unavailable"}""";

    public static string UnknownActionJson()
        => """{"status":false,"message":"Unknown action"}""";

    public static string FamilyNotFoundJson()
        => """{"status":false,"message":"Product family not found"}""";

    public static string CatalogErrorJson()
        => """{"status":false,"message":"Catalog error"}""";

    public static string UmapiUnknownJson()
        => """{"message":"Unknown catalog action."}""";

    public static string UmapiMessageJson(string message)
        => JsonSerializer.Serialize(new { message }, Json);

    public static string UmapiMessageBrandPartsEmpty(string brand)
        => JsonSerializer.Serialize(new { brand, rows = 0, data = Array.Empty<object>(), message = "Database connection unavailable." }, Json);

    /// <summary>Vehicle catalog widgets require a JSON array. An empty array is the offline list, not an error.</summary>
    public static string EmptyVehicleCatalogJson() => "[]";

    /// <summary>Parts-brand widget reads <c>data</c>/<c>rows</c>. Zero rows is "No brands found", not HTTP 402.</summary>
    public static string EmptySuppliersJson()
        => """{"rows":0,"data":[],"source":"database"}""";

    public readonly record struct CachedUmapiBrand(int SupId, string Brand, string FullName);

    public readonly record struct StockUmapiBrand(string Brand, int PartsCount);

    /// <summary>
    /// PHP <c>epc_cached_brands_payload</c>: rows from <c>epc_umapi_brands</c>, then
    /// in-stock manufacturers from <c>shop_docpart_prices_data</c> that are not already listed.
    /// </summary>
    public static string SuppliersJson(IReadOnlyList<CachedUmapiBrand> cached, IReadOnlyList<StockUmapiBrand> stock)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var data = new List<Dictionary<string, object?>>();
        foreach (var row in cached)
        {
            var brand = (row.Brand ?? string.Empty).Trim();
            if (brand.Length == 0)
            {
                continue;
            }

            seen.Add(brand.ToUpperInvariant());
            data.Add(new Dictionary<string, object?>
            {
                ["SUP_ID"] = row.SupId,
                ["SUP_BRAND"] = brand,
                ["SUP_FULL_NAME"] = row.FullName ?? string.Empty,
            });
        }

        foreach (var row in stock)
        {
            var brand = (row.Brand ?? string.Empty).Trim();
            if (brand.Length == 0)
            {
                continue;
            }

            if (!seen.Add(brand.ToUpperInvariant()))
            {
                continue;
            }

            data.Add(new Dictionary<string, object?>
            {
                ["SUP_ID"] = 0,
                ["SUP_BRAND"] = brand,
                ["SUP_FULL_NAME"] = "Loaded price-list brand: " + row.PartsCount.ToString(CultureInfo.InvariantCulture) + " part numbers",
                ["LOCAL_STOCK_COUNT"] = row.PartsCount,
            });
        }

        data.Sort(static (a, b) => string.Compare(
            a["SUP_BRAND"] as string,
            b["SUP_BRAND"] as string,
            StringComparison.OrdinalIgnoreCase));
        return JsonSerializer.Serialize(new { rows = data.Count, data, source = "database" }, Json);
    }

    /// <summary>PHP <c>epc_ensure_cache_tables</c> statements. Storefront catalog cache, not ERP posting.</summary>
    public static IReadOnlyList<string> UmapiCacheSchemaStatements() => UmapiCacheDdl;

    public static string InferLabel(string? partName)
    {
        var hay = (partName ?? string.Empty).ToLowerInvariant();
        (string Label, string Needle)[] rules =
        [
            ("Piston", "piston"),
            ("Gasket", "gasket"),
            ("Oil filter", "oil filter"),
            ("Filter", "filter"),
            ("Brake", "brake pad"),
            ("Brake", "brake disc"),
            ("Bearing", "bearing"),
            ("Engine", "engine"),
            ("Engine", "cylinder"),
            ("Engine", "valve"),
        ];
        foreach (var rule in rules)
        {
            if (hay.Contains(rule.Needle, StringComparison.Ordinal))
            {
                return rule.Label;
            }
        }

        return string.IsNullOrWhiteSpace(partName) ? "Uncategorized" : "Other parts";
    }

    public static string NormalizeArticle(string? article)
    {
        if (string.IsNullOrEmpty(article))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(article.Length);
        foreach (var c in article)
        {
            if (c is ' ' or '-' or '_' or '`' or '/' or '\'' or '"' or '\\' or '.' or ',' or '#' or '\r' or '\n' or '\t')
            {
                continue;
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }

    public static string SectionOf(string? section, string? vehicleType)
    {
        if (!string.IsNullOrWhiteSpace(vehicleType))
        {
            var type = vehicleType.Trim();
            if (type is "CV" or "Bus" or "E-Bus" or "Tractor")
            {
                return "commercial";
            }

            if (type is "Motorcycle" or "E-Motorcycle")
            {
                return "motorbike";
            }
        }

        var value = (section ?? "passenger").Trim().ToLowerInvariant();
        return value is "passenger" or "commercial" or "motorbike" ? value : "passenger";
    }

    public static string SummaryJson(IReadOnlyList<ProductFamilyLine> lines, bool refresh)
    {
        var catalog = Build(lines);
        var payload = new
        {
            status = true,
            action = "summary",
            products = catalog.Cards,
            summary = catalog.Summary,
            cached = !refresh,
        };
        return JsonSerializer.Serialize(payload, Json);
    }

    public static string? GroupJson(IReadOnlyList<ProductFamilyLine> lines, string? label, string? brand)
    {
        var catalog = Build(lines);
        var needle = (label ?? string.Empty).Trim();
        ProductFamilyGroup? match = null;
        foreach (var group in catalog.Groups)
        {
            if (string.Equals(group.Label, needle, StringComparison.OrdinalIgnoreCase))
            {
                match = group;
                break;
            }
        }

        if (match is null)
        {
            return null;
        }

        var parts = match.Parts;
        if (!string.IsNullOrWhiteSpace(brand))
        {
            parts = parts.Where(p => string.Equals(p.Brand, brand.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var brands = parts
            .GroupBy(p => p.Brand.ToUpperInvariant())
            .Select(g => new
            {
                brand = g.First().Brand,
                parts_count = g.Count(),
                total_qty = g.Sum(p => p.Qty),
            })
            .OrderByDescending(b => b.total_qty)
            .ThenByDescending(b => b.parts_count)
            .ToList();
        var detail = new
        {
            status = true,
            action = "group",
            group = new
            {
                label = match.Label,
                parts_count = parts.Count,
                total_qty = parts.Sum(p => p.Qty),
                brands,
                parts = parts.OrderBy(p => p.Brand, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Article, StringComparer.Ordinal).Select(p => new
                {
                    brand = p.Brand,
                    article = p.Article,
                    name = p.Name,
                    qty = p.Qty,
                }),
            },
        };
        return JsonSerializer.Serialize(detail, Json);
    }

    private static BuiltCatalog Build(IReadOnlyList<ProductFamilyLine> lines)
    {
        var merged = new Dictionary<string, ProductFamilyLine>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var brand = (line.Brand ?? string.Empty).Trim();
            var article = NormalizeArticle(line.Article);
            if (brand.Length == 0 || article.Length == 0 || line.Qty > 50000)
            {
                continue;
            }

            var key = brand.ToUpperInvariant() + "|" + article;
            if (!merged.TryGetValue(key, out var existing))
            {
                merged[key] = line with { Brand = brand, Article = string.IsNullOrWhiteSpace(line.ArticleShow) ? article : line.ArticleShow.Trim() };
                continue;
            }

            var name = existing.Name;
            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(line.Name))
            {
                name = line.Name;
            }

            merged[key] = existing with { Qty = existing.Qty + line.Qty, Name = name };
        }

        var groups = new Dictionary<string, ProductFamilyGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in merged.Values)
        {
            var label = InferLabel(line.Name);
            if (!groups.TryGetValue(label, out var group))
            {
                group = new ProductFamilyGroup(label);
                groups[label] = group;
            }

            group.Parts.Add(line);
        }

        var ordered = groups.Values
            .OrderByDescending(g => g.Parts.Count)
            .ThenBy(g => g.Label, StringComparer.Ordinal)
            .ToList();
        var cards = ordered.Select(g =>
        {
            var brandRows = g.Parts
                .GroupBy(p => p.Brand.ToUpperInvariant())
                .Select(b => new
                {
                    brand = b.First().Brand,
                    parts_count = b.Count(),
                    total_qty = b.Sum(p => p.Qty),
                })
                .OrderByDescending(b => b.total_qty)
                .ThenBy(b => b.brand, StringComparer.Ordinal)
                .ToList();
            var top = brandRows.Take(10).ToList();
            return new
            {
                label = g.Label,
                parts_count = g.Parts.Count,
                total_qty = g.Parts.Sum(p => p.Qty),
                samples = Array.Empty<object>(),
                brands_top = top,
                brands_more_count = Math.Max(0, brandRows.Count - top.Count),
                brands_total = brandRows.Count,
            };
        }).ToList();

        var brandSummary = merged.Values
            .GroupBy(p => p.Brand.ToUpperInvariant())
            .Select(g =>
            {
                var labels = g.Select(p => InferLabel(p.Name)).Distinct(StringComparer.Ordinal).ToArray();
                return new
                {
                    brand = g.First().Brand,
                    parts_count = g.Count(),
                    total_qty = g.Sum(p => p.Qty),
                    product_groups = labels,
                };
            })
            .OrderByDescending(b => b.parts_count)
            .ToList();

        var summary = new
        {
            parts_count = merged.Count,
            product_groups_count = ordered.Count,
            brands_count = brandSummary.Count,
            total_stock_qty = merged.Values.Sum(p => p.Qty),
            brands = brandSummary,
        };
        return new BuiltCatalog(ordered, cards, summary);
    }

    private static async Task<IResult> ProductFamilyAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var action = (context.Request.Query["action"].ToString() is { Length: > 0 } queryAction
            ? queryAction
            : context.Request.HasFormContentType
                ? context.Request.Form["action"].ToString()
                : "summary").Trim().ToLowerInvariant();
        if (action.Length == 0)
        {
            action = "summary";
        }

        if (action is not ("summary" or "group"))
        {
            return WriteJson(StatusCodes.Status200OK, UnknownActionJson(), noStore: true);
        }

        if (!connections.IsConfigured)
        {
            return WriteJson(StatusCodes.Status200OK, UnavailableJson(), noStore: true);
        }

        try
        {
            var limit = 2500;
            if (int.TryParse(context.Request.Query["limit"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                limit = Math.Clamp(parsed, 100, 5000);
            }

            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            var lines = await LoadLinesAsync(connection, limit, cancellationToken).ConfigureAwait(false);
            var refresh = string.Equals(context.Request.Query["refresh"], "1", StringComparison.Ordinal);
            if (action == "group")
            {
                var body = GroupJson(lines, context.Request.Query["label"], context.Request.Query["brand"]);
                return WriteJson(StatusCodes.Status200OK, body ?? FamilyNotFoundJson(), noStore: true);
            }

            return WriteJson(StatusCodes.Status200OK, SummaryJson(lines, refresh), noStore: true);
        }
        catch (DbException)
        {
            return WriteJson(StatusCodes.Status200OK, UnavailableJson(), noStore: true);
        }
        catch (InvalidOperationException)
        {
            return WriteJson(StatusCodes.Status200OK, UnavailableJson(), noStore: true);
        }
        catch (Exception)
        {
            return WriteJson(StatusCodes.Status200OK, CatalogErrorJson(), noStore: true);
        }
    }

    private static async Task<List<ProductFamilyLine>> LoadLinesAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        var fetch = Math.Min(limit * 3, 12000);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TRIM(`manufacturer`) AS brand, TRIM(`article`) AS article,
                   COALESCE(NULLIF(TRIM(`article_show`), ''), TRIM(`article`)) AS article_show,
                   TRIM(IFNULL(`name`, '')) AS name, IFNULL(`exist`, 0) AS qty
            FROM `shop_docpart_prices_data`
            WHERE IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
              AND TRIM(IFNULL(`manufacturer`, '')) != '' AND TRIM(`article`) != ''
            ORDER BY IFNULL(`exist`, 0) DESC
            LIMIT 
            """ + fetch.ToString(CultureInfo.InvariantCulture);
        var lines = new List<ProductFamilyLine>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new ProductFamilyLine(
                    reader.IsDBNull(0) ? "" : reader.GetString(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? 0 : Convert.ToDouble(reader.GetValue(4), CultureInfo.InvariantCulture)));
                if (lines.Count >= fetch)
                {
                    break;
                }
            }
        }
        catch (DbException)
        {
            return [];
        }

        return lines;
    }

    private static async Task<IResult> UmapiProxyAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var action = context.Request.Query["action"].ToString().Trim().ToLowerInvariant();
        if (action.Length == 0 && context.Request.HasFormContentType)
        {
            action = context.Request.Form["action"].ToString().Trim().ToLowerInvariant();
        }

        var section = SectionOf(context.Request.Query["section"], context.Request.Query["vehicle_type"]);
        switch (action)
        {
            case "brand_parts" when string.IsNullOrWhiteSpace(context.Request.Query["brand"]):
                return WriteJson(StatusCodes.Status400BadRequest, UmapiMessageJson("Brand is required."), noStore: false);
            case "brands" when string.IsNullOrWhiteSpace(context.Request.Query["article"]):
                return WriteJson(StatusCodes.Status400BadRequest, UmapiMessageJson("Article number is required."), noStore: false);
            case "analogs" when string.IsNullOrWhiteSpace(context.Request.Query["article"]) || string.IsNullOrWhiteSpace(context.Request.Query["brand"]):
                return WriteJson(StatusCodes.Status400BadRequest, UmapiMessageJson("Article and brand are required."), noStore: false);
            case "article":
            case "article_links":
                if (!int.TryParse(context.Request.Query["id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var articleId) || articleId <= 0)
                {
                    return WriteJson(StatusCodes.Status400BadRequest, UmapiMessageJson("Article ID is required."), noStore: false);
                }

                break;
            case "engines":
                if (!int.TryParse(context.Request.Query["MFA_ID"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mfa) || mfa <= 0)
                {
                    return WriteJson(StatusCodes.Status400BadRequest, UmapiMessageJson("Manufacturer ID (MFA_ID) is required."), noStore: false);
                }

                break;
            case "vin":
                var vin = context.Request.Query["vin"].ToString().Trim();
                if (vin.Length is < 11 or > 17)
                {
                    return WriteJson(StatusCodes.Status400BadRequest, UmapiMessageJson("Valid VIN is required (11–17 characters)."), noStore: false);
                }

                break;
            case "":
                return WriteJson(StatusCodes.Status400BadRequest, UmapiUnknownJson(), noStore: false);
        }

        if (action is not (
            "status" or "manufacturers" or "models" or "modifications" or "suppliers" or "brand_parts"
            or "brands" or "analogs" or "article" or "article_links" or "vin" or "engines" or "engine"
            or "categories" or "products" or "articles" or "engine_search"))
        {
            return WriteJson(StatusCodes.Status400BadRequest, UmapiUnknownJson(), noStore: false);
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            var refresh = string.Equals(context.Request.Query["refresh"], "1", StringComparison.Ordinal);
            if (connections.IsConfigured && action is "status" or "suppliers" or "brand_parts" or "manufacturers" or "models" or "modifications" or "vin")
            {
                await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                await EnsureUmapiCacheTablesAsync(connection, cancellationToken).ConfigureAwait(false);
                if (action == "status")
                {
                    return WriteJson(StatusCodes.Status200OK, await UmapiStatusJsonAsync(connection, cancellationToken).ConfigureAwait(false), noStore: false, cacheSeconds: 60);
                }

                if (!refresh)
                {
                    var cached = await TryLocalUmapiAsync(connection, action, section, context, cancellationToken).ConfigureAwait(false);
                    if (cached is not null)
                    {
                        await LogUmapiAccessAsync(connection, action, section, 200, fromCache: true, isLive: false, "Saved catalog", cancellationToken).ConfigureAwait(false);
                        return WriteJson(StatusCodes.Status200OK, cached, noStore: false, cacheSeconds: action == "vin" ? 600 : 7200);
                    }
                }
            }
            else if (action == "status")
            {
                return WriteJson(StatusCodes.Status200OK, """{"connected":false,"message":"Database connection unavailable.","last_checked":0,"last_success":0,"counts":{"manufacturers":0,"models":0,"modifications":0,"brands":0}}""", noStore: false, cacheSeconds: 60);
            }

            if (action == "brand_parts")
            {
                return WriteJson(StatusCodes.Status200OK, UmapiMessageBrandPartsEmpty(context.Request.Query["brand"].ToString()), noStore: false, cacheSeconds: 600);
            }

            if (action is "categories" or "products" or "articles" or "vin" or "manufacturers" or "models" or "modifications" or "suppliers"
                or "brands" or "analogs" or "article" or "article_links" or "engines" or "engine")
            {
                var live = await TryLiveUmapiAsync(httpClientFactory, action, section, context, cancellationToken).ConfigureAwait(false);
                if (live is not null && live.Value.Status is >= 200 and < 300)
                {
                    var body = live.Value.Body;
                    if (connections.IsConfigured)
                    {
                        try
                        {
                            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                            await EnsureUmapiCacheTablesAsync(connection, cancellationToken).ConfigureAwait(false);
                            await PersistLiveUmapiAsync(connection, action, section, context, live.Value.Status, body, cancellationToken).ConfigureAwait(false);
                            var saved = await TryLocalUmapiAsync(connection, action, section, context, cancellationToken).ConfigureAwait(false);
                            if (saved is not null)
                            {
                                body = saved;
                            }
                        }
                        catch (DbException)
                        {
                            // The live catalog answer still goes back. A later request can save it.
                        }
                    }

                    var cache = action == "suppliers" ? 7200 : 3600;
                    return WriteJson(StatusCodes.Status200OK, body, noStore: false, cacheSeconds: cache);
                }

                if (connections.IsConfigured)
                {
                    try
                    {
                        await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                        await EnsureUmapiCacheTablesAsync(connection, cancellationToken).ConfigureAwait(false);
                        await SaveUmapiSyncStatusAsync(connection, false, live?.Status ?? StatusCodes.Status502BadGateway, UmapiErrorMessage(live?.Body, "Catalog service did not return a saved catalog."), UmapiAppKey(context), cancellationToken).ConfigureAwait(false);
                    }
                    catch (DbException)
                    {
                    }
                }

                // Homepage grids treat a missing TecDoc cache as an empty list.
                // Forwarding the rejected-key 402 makes response.ok false and the widget shows an error.
                if (action is "manufacturers" or "models" or "modifications")
                {
                    return WriteJson(StatusCodes.Status200OK, EmptyVehicleCatalogJson(), noStore: false, cacheSeconds: 60);
                }

                if (action == "suppliers")
                {
                    return WriteJson(StatusCodes.Status200OK, EmptySuppliersJson(), noStore: false, cacheSeconds: 60);
                }

                if (live is not null)
                {
                    return WriteJson(live.Value.Status, live.Value.Body, noStore: false);
                }
            }

            return WriteJson(StatusCodes.Status502BadGateway, """{"message":"Catalog service did not return a response.","statusCode":502}""", noStore: false);
        }
        catch (DbException)
        {
            return HomepageListFallback(action);
        }
        catch (Exception)
        {
            return HomepageListFallback(action);
        }
    }

    private static IResult HomepageListFallback(string action)
    {
        if (action is "manufacturers" or "models" or "modifications")
        {
            return WriteJson(StatusCodes.Status200OK, EmptyVehicleCatalogJson(), noStore: false, cacheSeconds: 60);
        }

        if (action == "suppliers")
        {
            return WriteJson(StatusCodes.Status200OK, EmptySuppliersJson(), noStore: false, cacheSeconds: 60);
        }

        return WriteJson(StatusCodes.Status502BadGateway, """{"message":"Catalog service did not return a response.","statusCode":502}""", noStore: false);
    }

    private static async Task<string> UmapiStatusJsonAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var connected = false;
        var statusCode = 0;
        var message = "No Epart catalog check saved yet.";
        long lastChecked = 0;
        long lastSuccess = 0;
        long lastError = 0;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `connected`, `status_code`, `message`, `last_checked`, `last_success`, `last_error` FROM `epc_umapi_sync_status` WHERE `id` = 1";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                connected = !reader.IsDBNull(0) && Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) == 1;
                statusCode = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                message = reader.IsDBNull(2) ? message : reader.GetString(2);
                lastChecked = reader.IsDBNull(3) ? 0 : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
                lastSuccess = reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture);
                lastError = reader.IsDBNull(5) ? 0 : Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture);
            }
        }
        catch (DbException)
        {
            message = "No Epart catalog check saved yet.";
        }

        var manufacturers = await CountUmapiAsync(connection, "SELECT COUNT(*) FROM `epc_umapi_manufacturers`", cancellationToken).ConfigureAwait(false);
        var models = await CountUmapiAsync(connection, "SELECT COUNT(*) FROM `epc_umapi_models`", cancellationToken).ConfigureAwait(false);
        var modifications = await CountUmapiAsync(connection, "SELECT COUNT(*) FROM `epc_umapi_modifications`", cancellationToken).ConfigureAwait(false);
        var brands = await CountUmapiAsync(connection, "SELECT COUNT(*) FROM `epc_umapi_brands`", cancellationToken).ConfigureAwait(false);
        var cacheRows = await CountUmapiAsync(connection, "SELECT COUNT(*) FROM `epc_umapi_cache`", cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["connected"] = connected,
            ["status_code"] = statusCode,
            ["message"] = message,
            ["last_checked"] = lastChecked,
            ["last_success"] = lastSuccess,
            ["last_error"] = lastError,
            ["counts"] = new Dictionary<string, int>
            {
                ["manufacturers"] = manufacturers,
                ["models"] = models,
                ["modifications"] = modifications,
                ["brands"] = brands,
            },
            ["cache_rows"] = cacheRows,
            ["offline_ready"] = manufacturers >= 20 || cacheRows >= 5,
        }, Json);
    }

    private static async Task<int> CountUmapiAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static async Task PersistLiveUmapiAsync(
        DbConnection connection,
        string action,
        string section,
        HttpContext context,
        int status,
        string body,
        CancellationToken cancellationToken)
    {
        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            document = null;
        }

        using (document)
        {
            var items = document is null ? [] : UmapiItems(document.RootElement);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (action == "manufacturers")
            {
                foreach (var item in items)
                {
                    var id = UmapiInt(item, "MFA_ID");
                    if (id <= 0)
                    {
                        continue;
                    }

                    var popularKey = section == "commercial" ? "POPULAR_CV" : section == "motorbike" ? "POPULAR_MTB" : "POPULAR_PC";
                    await ExecuteUmapiAsync(
                        connection,
                        """
                        REPLACE INTO `epc_umapi_manufacturers`
                        (`section`, `mfa_id`, `manufacturer`, `manufacturer_ru`, `type`, `country`, `popular`, `is_logo`, `raw_json`, `updated_at`)
                        VALUES (@section, @id, @name, @nameRu, @type, @country, @popular, @logo, @raw, @now)
                        """,
                        cancellationToken,
                        ("@section", Cut(section, 20)),
                        ("@id", id),
                        ("@name", Cut(UmapiText(item, "MANUFACTURER"), 255)),
                        ("@nameRu", Cut(UmapiText(item, "MANUFACTURER_RU"), 255)),
                        ("@type", Cut(UmapiText(item, "TYPE"), 255)),
                        ("@country", Cut(UmapiText(item, "COUNTRY"), 120)),
                        ("@popular", UmapiFlag(item, popularKey)),
                        ("@logo", UmapiFlag(item, "IS_LOGO")),
                        ("@raw", item.GetRawText()),
                        ("@now", now)).ConfigureAwait(false);
                }
            }
            else if (action == "models")
            {
                var mfa = UmapiQueryInt(context, "MFA_ID");
                foreach (var item in items)
                {
                    var id = UmapiInt(item, "MS_ID");
                    if (id <= 0)
                    {
                        continue;
                    }

                    await ExecuteUmapiAsync(
                        connection,
                        """
                        REPLACE INTO `epc_umapi_models`
                        (`section`, `mfa_id`, `ms_id`, `model_series`, `year_from`, `year_to`, `raw_json`, `updated_at`)
                        VALUES (@section, @mfa, @id, @name, @from, @to, @raw, @now)
                        """,
                        cancellationToken,
                        ("@section", Cut(section, 20)),
                        ("@mfa", mfa),
                        ("@id", id),
                        ("@name", Cut(UmapiText(item, "MODEL_SERIES"), 255)),
                        ("@from", Cut(UmapiText(item, "CI_FROM"), 20)),
                        ("@to", Cut(UmapiText(item, "CI_TO"), 20)),
                        ("@raw", item.GetRawText()),
                        ("@now", now)).ConfigureAwait(false);
                }
            }
            else if (action == "modifications")
            {
                var ms = UmapiQueryInt(context, "MS_ID");
                foreach (var item in items)
                {
                    var id = UmapiInt(item, "PC_ID");
                    if (id <= 0)
                    {
                        id = UmapiInt(item, "CV_ID");
                    }

                    if (id <= 0)
                    {
                        id = UmapiInt(item, "MTB_ID");
                    }

                    if (id <= 0)
                    {
                        id = UmapiInt(item, "ID");
                    }

                    if (id <= 0)
                    {
                        continue;
                    }

                    var title = UmapiText(item, "PASSENGER_CAR");
                    if (title.Length == 0)
                    {
                        title = UmapiText(item, "COMMERCIAL_VEHICLE");
                    }

                    if (title.Length == 0)
                    {
                        title = UmapiText(item, "MOTORBIKE");
                    }

                    await ExecuteUmapiAsync(
                        connection,
                        """
                        REPLACE INTO `epc_umapi_modifications`
                        (`section`, `ms_id`, `modification_id`, `title`, `year_from`, `year_to`, `power_kw`, `capacity_lt`, `fuel_type`, `raw_json`, `updated_at`)
                        VALUES (@section, @ms, @id, @title, @from, @to, @kw, @lt, @fuel, @raw, @now)
                        """,
                        cancellationToken,
                        ("@section", Cut(section, 20)),
                        ("@ms", ms),
                        ("@id", id),
                        ("@title", Cut(title, 255)),
                        ("@from", Cut(UmapiText(item, "CI_FROM"), 20)),
                        ("@to", Cut(UmapiText(item, "CI_TO"), 20)),
                        ("@kw", Cut(UmapiText(item, "POWER_KW"), 50)),
                        ("@lt", Cut(UmapiText(item, "CAPACITY_LT"), 50)),
                        ("@fuel", Cut(UmapiText(item, "FUEL_TYPE"), 120)),
                        ("@raw", item.GetRawText()),
                        ("@now", now)).ConfigureAwait(false);
                }
            }
            else if (action == "suppliers")
            {
                foreach (var item in items)
                {
                    var id = UmapiInt(item, "SUP_ID");
                    if (id <= 0)
                    {
                        continue;
                    }

                    var brand = UmapiText(item, "SUP_BRAND");
                    if (brand.Length == 0)
                    {
                        brand = UmapiText(item, "BRAND");
                    }

                    if (brand.Length == 0)
                    {
                        brand = UmapiText(item, "SUP_FULL_NAME");
                    }

                    if (brand.Length == 0)
                    {
                        continue;
                    }

                    await ExecuteUmapiAsync(
                        connection,
                        """
                        REPLACE INTO `epc_umapi_brands` (`sup_id`, `brand`, `full_name`, `raw_json`, `updated_at`)
                        VALUES (@id, @brand, @full, @raw, @now)
                        """,
                        cancellationToken,
                        ("@id", id),
                        ("@brand", Cut(brand, 255)),
                        ("@full", Cut(UmapiText(item, "SUP_FULL_NAME"), 255)),
                        ("@raw", item.GetRawText()),
                        ("@now", now)).ConfigureAwait(false);
                }
            }
            else if (action == "vin" && document is not null)
            {
                await SaveUmapiVinAsync(connection, context, document.RootElement, status, cancellationToken).ConfigureAwait(false);
            }

            var language = UmapiLanguage(context);
            var region = UmapiRegion(context);
            var requestJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["action"] = action,
                ["section"] = section,
            }, Json);
            var rows = items.Count;
            await ExecuteUmapiAsync(
                connection,
                """
                REPLACE INTO `epc_umapi_cache`
                (`cache_key`, `action`, `section`, `language`, `region`, `request_json`, `response_json`, `rows_count`, `http_status`, `last_sync`)
                VALUES (@key, @action, @section, @language, @region, @request, @response, @rows, @status, @now)
                """,
                cancellationToken,
                ("@key", UmapiCacheKey(action, section, language, region, requestJson)),
                ("@action", Cut(action, 40)),
                ("@section", Cut(section, 20)),
                ("@language", language),
                ("@region", region),
                ("@request", requestJson),
                ("@response", body),
                ("@rows", rows),
                ("@status", status),
                ("@now", now)).ConfigureAwait(false);
            await SaveUmapiSyncStatusAsync(connection, true, status, "Connected", UmapiAppKey(context), cancellationToken).ConfigureAwait(false);
            await LogUmapiAccessAsync(connection, action, section, status, fromCache: false, isLive: true, "HTTP " + status.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task SaveUmapiVinAsync(DbConnection connection, HttpContext context, JsonElement payload, int status, CancellationToken cancellationToken)
    {
        var vin = NormalizeArticle(context.Request.Query["vin"].ToString());
        if (vin.Length is < 11 or > 17)
        {
            return;
        }

        var vehicles = 0;
        var data = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("data", out var inner) && inner.ValueKind == JsonValueKind.Object
            ? inner
            : payload;
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("matchingVehicles", out var matching) && matching.ValueKind == JsonValueKind.Array)
        {
            vehicles = matching.GetArrayLength();
        }

        if (vehicles <= 0)
        {
            return;
        }

        var manufacturer = string.Empty;
        if (data.TryGetProperty("matchingManufacturers", out var makers) && makers.ValueKind == JsonValueKind.Array)
        {
            foreach (var maker in makers.EnumerateArray())
            {
                manufacturer = UmapiText(maker, "manuName");
                if (manufacturer.Length == 0)
                {
                    manufacturer = UmapiText(maker, "MANUFACTURER");
                }

                if (manufacturer.Length > 0)
                {
                    break;
                }
            }
        }

        await ExecuteUmapiAsync(
            connection,
            """
            REPLACE INTO `epc_umapi_vin_cache`
            (`vin`, `language`, `region`, `response_json`, `vehicle_count`, `manufacturer`, `model_label`, `http_status`, `updated_at`)
            VALUES (@vin, @language, @region, @response, @count, @manufacturer, '', @status, @now)
            """,
            cancellationToken,
            ("@vin", vin),
            ("@language", UmapiLanguage(context)),
            ("@region", UmapiRegion(context)),
            ("@response", payload.GetRawText()),
            ("@count", vehicles),
            ("@manufacturer", Cut(manufacturer, 255)),
            ("@status", status),
            ("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds())).ConfigureAwait(false);
    }

    private static async Task SaveUmapiSyncStatusAsync(DbConnection connection, bool connected, int status, string message, string appKey, CancellationToken cancellationToken)
    {
        long lastSuccess = 0;
        long lastError = 0;
        try
        {
            await using var read = connection.CreateCommand();
            read.CommandText = "SELECT `last_success`, `last_error` FROM `epc_umapi_sync_status` WHERE `id` = 1";
            await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lastSuccess = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                lastError = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            }
        }
        catch (DbException)
        {
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (connected)
        {
            lastSuccess = now;
        }
        else
        {
            lastError = now;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(appKey) ? DefaultUmapiKey : appKey))).ToLowerInvariant();
        await ExecuteUmapiAsync(
            connection,
            """
            REPLACE INTO `epc_umapi_sync_status`
            (`id`, `connected`, `status_code`, `message`, `last_checked`, `last_success`, `last_error`, `key_hash`)
            VALUES (1, @connected, @status, @message, @now, @success, @error, @hash)
            """,
            cancellationToken,
            ("@connected", connected ? 1 : 0),
            ("@status", status),
            ("@message", Cut(message, 255)),
            ("@now", now),
            ("@success", lastSuccess),
            ("@error", lastError),
            ("@hash", hash)).ConfigureAwait(false);
    }

    private static async Task LogUmapiAccessAsync(
        DbConnection connection,
        string action,
        string section,
        int status,
        bool fromCache,
        bool isLive,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteUmapiAsync(
                connection,
                """
                INSERT INTO `epc_umapi_usage_log`
                (`usage_date`, `created_at`, `action`, `section`, `source`, `request_path`, `http_status`, `from_cache`, `quota_blocked`, `is_live`, `message`, `ip`)
                VALUES (CURDATE(), @now, @action, @section, 'storefront', @path, @status, @cache, 0, @live, @message, '')
                """,
                cancellationToken,
                ("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                ("@action", Cut(action, 40)),
                ("@section", Cut(section, 20)),
                ("@path", Cut("/api/umapi_proxy.php?action=" + action, 255)),
                ("@status", status),
                ("@cache", fromCache ? 1 : 0),
                ("@live", isLive ? 1 : 0),
                ("@message", Cut(message, 255))).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static async Task EnsureUmapiCacheTablesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in UmapiCacheDdl)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // Same as PHP epc_ensure_cache_tables: a failed create does not throw to the widget.
            }
        }
    }

    private static async Task<string?> TryLocalUmapiAsync(
        DbConnection connection,
        string action,
        string section,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            if (action == "vin")
            {
                var vin = NormalizeArticle(context.Request.Query["vin"].ToString());
                command.CommandText = """
                    SELECT `response_json` FROM `epc_umapi_vin_cache`
                    WHERE `vin` = @vin AND `language` = @language AND `region` = @region AND `vehicle_count` > 0
                    LIMIT 1
                    """;
                Add(command, "@vin", vin);
                Add(command, "@language", UmapiLanguage(context));
                Add(command, "@region", UmapiRegion(context));
                var savedVin = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                var text = savedVin is string raw ? raw : null;
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }

            if (action == "manufacturers")
            {
                command.CommandText = "SELECT `raw_json` FROM `epc_umapi_manufacturers` WHERE `section` = @section ORDER BY `manufacturer` ASC";
                Add(command, "@section", section);
                var items = await ReadRawJsonArrayAsync(command, cancellationToken).ConfigureAwait(false);
                return items.Count == 0 ? null : JsonSerializer.Serialize(items, Json);
            }

            if (action == "models")
            {
                if (!int.TryParse(context.Request.Query["MFA_ID"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mfa) || mfa <= 0)
                {
                    return null;
                }

                command.CommandText = "SELECT `raw_json` FROM `epc_umapi_models` WHERE `section` = @section AND `mfa_id` = @mfa ORDER BY `model_series` ASC";
                Add(command, "@section", section);
                Add(command, "@mfa", mfa);
                var items = await ReadRawJsonArrayAsync(command, cancellationToken).ConfigureAwait(false);
                return items.Count == 0 ? null : JsonSerializer.Serialize(items, Json);
            }

            if (action == "modifications")
            {
                if (!int.TryParse(context.Request.Query["MS_ID"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) || ms <= 0)
                {
                    return null;
                }

                command.CommandText = "SELECT `raw_json` FROM `epc_umapi_modifications` WHERE `section` = @section AND `ms_id` = @ms";
                Add(command, "@section", section);
                Add(command, "@ms", ms);
                var items = await ReadRawJsonArrayAsync(command, cancellationToken).ConfigureAwait(false);
                return items.Count == 0 ? null : JsonSerializer.Serialize(items, Json);
            }

            if (action == "suppliers")
            {
                var cachedBrands = new List<CachedUmapiBrand>();
                command.CommandText = "SELECT `sup_id`, `brand`, `full_name` FROM `epc_umapi_brands` ORDER BY `brand` ASC";
                await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var brand = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim();
                        if (brand.Length == 0)
                        {
                            continue;
                        }

                        cachedBrands.Add(new CachedUmapiBrand(
                            reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                            brand,
                            reader.IsDBNull(2) ? "" : reader.GetString(2)));
                    }
                }

                var stock = new List<StockUmapiBrand>();
                try
                {
                    await using var stockCommand = connection.CreateCommand();
                    stockCommand.CommandText = """
                        SELECT UPPER(TRIM(`manufacturer`)) AS `brand`, COUNT(DISTINCT COALESCE(NULLIF(`article_show`, ''), `article`)) AS `parts_count`
                        FROM `shop_docpart_prices_data`
                        WHERE TRIM(IFNULL(`manufacturer`, '')) != '' AND TRIM(IFNULL(`article`, '')) != '' AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
                        GROUP BY UPPER(TRIM(`manufacturer`))
                        ORDER BY `brand` ASC
                        """;
                    await using var stockReader = await stockCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await stockReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var brand = stockReader.IsDBNull(0) ? "" : stockReader.GetString(0).Trim();
                        if (brand.Length == 0)
                        {
                            continue;
                        }

                        var parts = stockReader.IsDBNull(1) ? 0 : Convert.ToInt32(stockReader.GetValue(1), CultureInfo.InvariantCulture);
                        stock.Add(new StockUmapiBrand(brand, parts));
                    }
                }
                catch (DbException)
                {
                    // Price table missing. Saved brands still apply; PHP's combined query would have returned null.
                }

                if (cachedBrands.Count == 0 && stock.Count == 0)
                {
                    return null;
                }

                return SuppliersJson(cachedBrands, stock);
            }

            if (action == "brand_parts")
            {
                return await BrandPartsJsonAsync(connection, context.Request.Query["brand"].ToString(), context, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
            return null;
        }

        return null;
    }

    private static async Task<string?> BrandPartsJsonAsync(DbConnection connection, string brand, HttpContext context, CancellationToken cancellationToken)
    {
        var limit = 100;
        var offset = 0;
        if (int.TryParse(context.Request.Query["limit"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLimit))
        {
            limit = parsedLimit is < 1 or > 500 ? 100 : parsedLimit;
        }

        if (int.TryParse(context.Request.Query["offset"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOffset) && parsedOffset > 0)
        {
            offset = parsedOffset;
        }

        var brandUpper = brand.Trim().ToUpperInvariant();
        var compact = NormalizeArticle(brandUpper);
        await using var count = connection.CreateCommand();
        count.CommandText = """
            SELECT COUNT(*) FROM (
              SELECT COALESCE(NULLIF(`article_show`, ''), `article`) AS article_key
              FROM `shop_docpart_prices_data`
              WHERE TRIM(IFNULL(`manufacturer`, '')) != '' AND TRIM(IFNULL(`article`, '')) != ''
                AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
                AND (UPPER(TRIM(`manufacturer`)) = @brand OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(`manufacturer`)), ' ', ''), '-', ''), '.', '') = @compact)
              GROUP BY UPPER(TRIM(`manufacturer`)), COALESCE(NULLIF(`article_show`, ''), `article`)
            ) AS brand_items
            """;
        Add(count, "@brand", brandUpper);
        Add(count, "@compact", compact);
        var rows = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0, CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UPPER(TRIM(`manufacturer`)) AS manufacturer,
                   COALESCE(NULLIF(`article_show`, ''), `article`) AS article_show,
                   MIN(`article`) AS article,
                   MIN(`name`) AS name,
                   SUM(IFNULL(`exist`, 0)) AS exist,
                   MIN(`price`) AS price
            FROM `shop_docpart_prices_data`
            WHERE TRIM(IFNULL(`manufacturer`, '')) != '' AND TRIM(IFNULL(`article`, '')) != ''
              AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
              AND (UPPER(TRIM(`manufacturer`)) = @brand OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(`manufacturer`)), ' ', ''), '-', ''), '.', '') = @compact)
            GROUP BY UPPER(TRIM(`manufacturer`)), COALESCE(NULLIF(`article_show`, ''), `article`)
            ORDER BY article_show ASC
            LIMIT 
            """ + limit.ToString(CultureInfo.InvariantCulture) + " OFFSET " + offset.ToString(CultureInfo.InvariantCulture);
        Add(command, "@brand", brandUpper);
        Add(command, "@compact", compact);
        var data = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            data.Add(new
            {
                manufacturer = reader.IsDBNull(0) ? "" : reader.GetString(0),
                article_show = reader.IsDBNull(1) ? "" : reader.GetString(1),
                article = reader.IsDBNull(2) ? "" : reader.GetString(2),
                name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                exist = reader.IsDBNull(4) ? 0 : Convert.ToDouble(reader.GetValue(4), CultureInfo.InvariantCulture),
                price = reader.IsDBNull(5) ? 0 : Convert.ToDouble(reader.GetValue(5), CultureInfo.InvariantCulture),
            });
        }

        return JsonSerializer.Serialize(new
        {
            brand,
            rows,
            data,
            source = "shop_docpart_prices_data",
        }, Json);
    }

    private static async Task<List<JsonElement>> ReadRawJsonArrayAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var items = new List<JsonElement>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            var raw = reader.GetString(0);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                items.Add(doc.RootElement.Clone());
            }
            catch (JsonException)
            {
                // skip a bad cache row
            }
        }

        return items;
    }

    private static async Task<(int Status, string Body)?> TryLiveUmapiAsync(
        IHttpClientFactory httpClientFactory,
        string action,
        string section,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var language = context.Request.Query["language"].ToString().Trim().ToLowerInvariant();
        if (language.Length != 2)
        {
            language = "en";
        }

        var region = context.Request.Query["region"].ToString().Trim().ToUpperInvariant();
        if (region.Length == 0)
        {
            region = "WWW";
        }

        var catalog = "/v2/autocatalog/" + language + "-" + region;
        var path = action switch
        {
            "manufacturers" => catalog + "/Manufacturers?popular=false&type=PC&type=E-PC&type=LCV&type=E-LCV",
            "models" => catalog + "/ModelSeries?MFA_ID=" + Uri.EscapeDataString(context.Request.Query["MFA_ID"].ToString()) + "&type=PC",
            "modifications" => catalog + "/Passangers?MS_ID=" + Uri.EscapeDataString(context.Request.Query["MS_ID"].ToString()) + "&type=PC",
            "suppliers" => catalog + "/Suppliers?limit=100&offset=0",
            "vin" => catalog + "/Vin/" + Uri.EscapeDataString(context.Request.Query["vin"].ToString()),
            "categories" => catalog + "/Categories?ID=" + Uri.EscapeDataString(context.Request.Query["ID"].ToString()) + "&type=PC",
            "products" => catalog + "/Products?CATEGORY_ID=" + Uri.EscapeDataString(context.Request.Query["CATEGORY_ID"].ToString()) + "&ID=" + Uri.EscapeDataString(context.Request.Query["ID"].ToString()) + "&type=PC",
            "articles" => catalog + "/Articles?PT_IDS=" + Uri.EscapeDataString(context.Request.Query["PT_IDS"].ToString()) + "&type=PC",
            "brands" => catalog + "/BrandRefinement/" + Uri.EscapeDataString(context.Request.Query["article"].ToString()),
            "analogs" => catalog + "/Analogs/" + Uri.EscapeDataString(context.Request.Query["article"].ToString()) + "/" + Uri.EscapeDataString(context.Request.Query["brand"].ToString()),
            "article" => catalog + "/Article/" + Uri.EscapeDataString(context.Request.Query["id"].ToString()),
            "article_links" => catalog + "/ArticleLinks/" + Uri.EscapeDataString(context.Request.Query["id"].ToString()),
            "engines" => catalog + "/Engines?MFA_ID=" + Uri.EscapeDataString(context.Request.Query["MFA_ID"].ToString()),
            "engine" => catalog + "/Engine/" + Uri.EscapeDataString(context.Request.Query["id"].ToString()),
            _ => "",
        };
        if (section == "commercial" && action == "manufacturers")
        {
            path = catalog + "/Manufacturers?popular=false&type=CV&type=Bus&type=E-Bus&type=Tractor";
        }
        else if (section == "motorbike" && action == "manufacturers")
        {
            path = catalog + "/Manufacturers?popular=false&type=Motorcycle&type=E-Motorcycle";
        }
        else if (section == "commercial" && action == "modifications")
        {
            path = catalog + "/CommercialVehicles?MS_ID=" + Uri.EscapeDataString(context.Request.Query["MS_ID"].ToString()) + "&type=CV";
        }
        else if (section == "motorbike" && action == "modifications")
        {
            path = catalog + "/Motorbikes?MS_ID=" + Uri.EscapeDataString(context.Request.Query["MS_ID"].ToString()) + "&type=Motorcycle";
        }

        if (path.Length == 0)
        {
            return null;
        }

        try
        {
            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(12);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.umapi.ru" + path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("X-App-Key", UmapiAppKey(context));
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body) || body[0] is not ('[' or '{'))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (status is < 200 or >= 300)
            {
                // PHP forwards the catalog JSON and its HTTP status when the offline cache is empty.
                var forward = status is >= 400 and < 600 ? status : StatusCodes.Status502BadGateway;
                return (forward, body);
            }

            if (action == "manufacturers" && doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                return (StatusCodes.Status200OK, data.GetRawText());
            }

            return (StatusCodes.Status200OK, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private static List<JsonElement> UmapiItems(JsonElement root)
    {
        var items = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    items.Add(item);
                }
            }
        }

        return items;
    }

    private static int UmapiInt(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static string UmapiText(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => string.Empty,
            _ => string.Empty,
        };
    }

    private static int UmapiFlag(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return 1;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number == 0 ? 0 : 1;
        }

        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return string.IsNullOrWhiteSpace(text) || text is "0" or "false" ? 0 : 1;
    }

    private static int UmapiQueryInt(HttpContext context, string name)
        => int.TryParse(context.Request.Query[name], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static string UmapiAppKey(HttpContext context)
    {
        var reference = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>()?.Value ?? new PhpReferenceOptions();
        return ResolveUmapiKey(CpPhpConfig.Read(reference));
    }

    private static string UmapiErrorMessage(string? body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                var text = message.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }

    private static string UmapiLanguage(HttpContext context)
    {
        var language = context.Request.Query["language"].ToString().Trim().ToLowerInvariant();
        return language.Length == 2 ? language : "en";
    }

    private static string UmapiRegion(HttpContext context)
    {
        var region = context.Request.Query["region"].ToString().Trim().ToUpperInvariant();
        return region.Length == 0 ? "WWW" : Cut(region, 20);
    }

    private static string UmapiCacheKey(string action, string section, string language, string region, string requestJson)
        => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(action + "|" + section + "|" + language + "|" + region + "|" + requestJson))).ToLowerInvariant();

    private static string Cut(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }

    private static async Task ExecuteUmapiAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            Add(command, name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static IResult WriteJson(int status, string body, bool noStore, int cacheSeconds = 0)
        => new CatalogJsonResult(status, body, noStore, cacheSeconds);

    private sealed class CatalogJsonResult : IResult
    {
        private readonly int _status;
        private readonly string _body;
        private readonly bool _noStore;
        private readonly int _cacheSeconds;

        public CatalogJsonResult(int status, string body, bool noStore, int cacheSeconds)
        {
            _status = status;
            _body = body;
            _noStore = noStore;
            _cacheSeconds = cacheSeconds;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = _status;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            httpContext.Response.Headers.CacheControl = _noStore
                ? "no-store, no-cache, must-revalidate"
                : _cacheSeconds > 0
                    ? "public, max-age=" + _cacheSeconds.ToString(CultureInfo.InvariantCulture) + ", stale-while-revalidate=86400"
                    : "no-cache, must-revalidate";
            await httpContext.Response.WriteAsync(_body).ConfigureAwait(false);
        }
    }

    private sealed record BuiltCatalog(List<ProductFamilyGroup> Groups, object Cards, object Summary);

    private sealed class ProductFamilyGroup
    {
        public ProductFamilyGroup(string label) => Label = label;

        public string Label { get; }

        public List<ProductFamilyLine> Parts { get; } = [];
    }
}

public sealed record ProductFamilyLine(string Brand, string Article, string ArticleShow, string Name, double Qty);
